using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace ExhaustiveSwitch.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExhaustiveEnumCodeFixProvider)), Shared]
    public class ExhaustiveEnumCodeFixProvider : CodeFixProvider
    {
        private const string DiagnosticId = "EXH1001";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Fix Allは見づらい上、思想上使ってほしくないため、対応しない
            return null;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            if (root == null)
            {
                return;
            }

            var node = root.FindNode(diagnosticSpan);

            // switch文の場合
            var switchStatement = node.AncestorsAndSelf().OfType<SwitchStatementSyntax>().FirstOrDefault();
            if (switchStatement != null)
            {
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == DiagnosticId)
                    .ToList();

                var (missingMembers, enumTypeName, enumTypeMetadata) = ExtractDiagnosticInfo(allDiagnostics);

                if (missingMembers.Count == 0)
                {
                    return;
                }

                // すべて追加のCodeFix
                if (missingMembers.Count > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: Resources.CodeFixAddAllCases,
                            createChangedDocument: c => AddMissingEnumCasesToSwitchStatementAsync(
                                context.Document, switchStatement, missingMembers, enumTypeName, enumTypeMetadata, c),
                            equivalenceKey: "AddAllEnumCases"),
                        diagnostic);
                }

                // 個別追加のCodeFix
                foreach (var member in missingMembers)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: string.Format(Resources.CodeFixAddSingleCase, member),
                            createChangedDocument: c => AddMissingEnumCasesToSwitchStatementAsync(
                                context.Document, switchStatement, new[] { member }, enumTypeName, enumTypeMetadata, c),
                            equivalenceKey: $"AddSingleEnumCase_{member}"),
                        diagnostic);
                }
                return;
            }

            // switch式の場合
            var switchExpression = node.AncestorsAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault();
            if (switchExpression != null)
            {
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == DiagnosticId)
                    .ToArray();

                var (missingMembers, enumTypeName, enumTypeMetadata) = ExtractDiagnosticInfo(allDiagnostics);

                if (missingMembers.Count == 0)
                {
                    return;
                }

                // すべて追加のCodeFix
                if (missingMembers.Count > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: Resources.CodeFixAddAllCases,
                            createChangedDocument: c => AddMissingEnumCasesToSwitchExpressionAsync(
                                context.Document, switchExpression, missingMembers, enumTypeName, enumTypeMetadata, c),
                            equivalenceKey: "AddAllEnumCases"),
                        diagnostic);
                }

                // 個別追加のCodeFix
                foreach (var member in missingMembers)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: string.Format(Resources.CodeFixAddSingleCase, member),
                            createChangedDocument: c => AddMissingEnumCasesToSwitchExpressionAsync(
                                context.Document, switchExpression, new[] { member }, enumTypeName, enumTypeMetadata, c),
                            equivalenceKey: $"AddSingleEnumCase_{member}"),
                        diagnostic);
                }
            }
        }

        private async Task<Document> AddMissingEnumCasesToSwitchStatementAsync(
            Document document,
            SwitchStatementSyntax switchStatement,
            IReadOnlyList<string> missingMembers,
            string enumTypeName,
            string enumTypeMetadata,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null || missingMembers.Count == 0)
            {
                return document;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null)
            {
                return document;
            }

            // enumの型シンボルを取得
            var enumType = GetEnumTypeSymbol(semanticModel, enumTypeMetadata);
            if (enumType == null)
            {
                return document;
            }

            var sections = switchStatement.Sections;
            var defaultSection = sections.FirstOrDefault(s => s.Labels.Any(l => l is DefaultSwitchLabelSyntax));
            var defaultIndex = defaultSection != null ? sections.IndexOf(defaultSection) : sections.Count;

            var newSections = sections;
            // 逆順で挿入することで、リストの先頭から順に挿入される
            for (int i = missingMembers.Count - 1; i >= 0; i--)
            {
                var member = missingMembers[i];
                var newCaseSection = CreateCaseSectionForEnumMember(enumType, member);
                newSections = newSections.Insert(defaultIndex, newCaseSection);
            }

            var newSwitchStatement = switchStatement.WithSections(newSections)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchStatement, newSwitchStatement);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AddMissingEnumCasesToSwitchExpressionAsync(
            Document document,
            SwitchExpressionSyntax switchExpression,
            IReadOnlyList<string> missingMembers,
            string enumTypeName,
            string enumTypeMetadata,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null || missingMembers.Count == 0)
            {
                return document;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null)
            {
                return document;
            }

            // enumの型シンボルを取得
            var enumType = GetEnumTypeSymbol(semanticModel, enumTypeMetadata);
            if (enumType == null)
            {
                return document;
            }

            var arms = switchExpression.Arms;
            var discardArm = arms.FirstOrDefault(a => a.Pattern is DiscardPatternSyntax);
            var discardIndex = discardArm != null ? arms.IndexOf(discardArm) : arms.Count;

            var newArms = arms;
            // 逆順で挿入することで、リストの先頭から順に挿入される
            for (int i = missingMembers.Count - 1; i >= 0; i--)
            {
                var member = missingMembers[i];
                var newArm = CreateSwitchArmForEnumMember(enumType, member);
                newArms = newArms.Insert(discardIndex, newArm);
            }

            var newSwitchExpression = switchExpression.WithArms(newArms)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchExpression, newSwitchExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        private INamedTypeSymbol GetEnumTypeSymbol(SemanticModel semanticModel, string enumTypeMetadata)
        {
            if (string.IsNullOrEmpty(enumTypeMetadata))
            {
                return null;
            }

            return semanticModel.Compilation.GetTypeByMetadataName(enumTypeMetadata);
        }

        private SwitchSectionSyntax CreateCaseSectionForEnumMember(INamedTypeSymbol enumType, string memberName)
        {
            var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // case EnumType.Member:
            //     throw new NotImplementedException();
            var typeSyntax = SyntaxFactory.ParseTypeName(enumTypeName)
                .WithAdditionalAnnotations(Simplifier.Annotation);

            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                typeSyntax,
                SyntaxFactory.IdentifierName(memberName));

            var caseLabel = SyntaxFactory.CaseSwitchLabel(
                memberAccess,
                SyntaxFactory.Token(SyntaxKind.ColonToken));

            var throwStatement = SyntaxFactory.ThrowStatement(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

            var section = SyntaxFactory.SwitchSection()
                .AddLabels(caseLabel)
                .AddStatements(throwStatement);

            return section;
        }

        private SwitchExpressionArmSyntax CreateSwitchArmForEnumMember(INamedTypeSymbol enumType, string memberName)
        {
            var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // EnumType.Member => throw new NotImplementedException(),
            var typeSyntax = SyntaxFactory.ParseTypeName(enumTypeName)
                .WithAdditionalAnnotations(Simplifier.Annotation);

            var memberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                typeSyntax,
                SyntaxFactory.IdentifierName(memberName));

            var pattern = SyntaxFactory.ConstantPattern(memberAccess);

            var throwExpression = SyntaxFactory.ThrowExpression(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

            var arm = SyntaxFactory.SwitchExpressionArm(
                pattern,
                throwExpression);

            return arm;
        }

        /// <summary>
        /// 診断情報から不足しているenumメンバー情報を抽出
        /// </summary>
        private static (List<string> missingMembers, string enumTypeName, string enumTypeMetadata) ExtractDiagnosticInfo(
            IEnumerable<Diagnostic> diagnostics)
        {
            var missingMembers = new List<string>();
            string enumTypeName = null;
            string enumTypeMetadata = null;

            foreach (var diag in diagnostics)
            {
                if (diag.Properties.TryGetValue("MissingMember", out var member))
                {
                    if (!missingMembers.Contains(member))
                    {
                        missingMembers.Add(member);
                    }
                }
                if (diag.Properties.TryGetValue("EnumType", out var typeName))
                {
                    enumTypeName = typeName;
                }
                if (diag.Properties.TryGetValue("EnumTypeMetadata", out var typeMetadata))
                {
                    enumTypeMetadata = typeMetadata;
                }
            }

            return (missingMembers, enumTypeName, enumTypeMetadata);
        }
    }
}
