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
        private const string NullableDiagnosticId = "EXH1002";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId, NullableDiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Fix All is not supported as it is difficult to use and discouraged by design
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

            // For switch statement case
            var switchStatement = node.AncestorsAndSelf().OfType<SwitchStatementSyntax>().FirstOrDefault();
            if (switchStatement != null)
            {
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == DiagnosticId || d.Id == NullableDiagnosticId)
                    .ToList();

                var (missingMembers, enumTypeMetadata) = ExtractDiagnosticInfo(allDiagnostics);

                if (missingMembers.Count == 0)
                {
                    return;
                }

                // CodeFix for adding all cases
                if (missingMembers.Count > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: Resources.CodeFixAddAllCases,
                            createChangedDocument: c => AddMissingEnumCasesToSwitchStatementAsync(
                                context.Document, switchStatement, missingMembers, enumTypeMetadata, c),
                            equivalenceKey: "AddAllEnumCases"),
                        diagnostic);
                }

                // CodeFix for adding individual cases
                foreach (var member in missingMembers)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: string.Format(Resources.CodeFixAddSingleCase, member),
                            createChangedDocument: c => AddMissingEnumCasesToSwitchStatementAsync(
                                context.Document, switchStatement, new[] { member }, enumTypeMetadata, c),
                            equivalenceKey: $"AddSingleEnumCase_{member}"),
                        diagnostic);
                }
                return;
            }

            // For switch expression case
            var switchExpression = node.AncestorsAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault();
            if (switchExpression != null)
            {
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == DiagnosticId || d.Id == NullableDiagnosticId)
                    .ToArray();

                var (missingMembers, enumTypeMetadata) = ExtractDiagnosticInfo(allDiagnostics);

                if (missingMembers.Count == 0)
                {
                    return;
                }

                // CodeFix for adding all cases
                if (missingMembers.Count > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: Resources.CodeFixAddAllCases,
                            createChangedDocument: c => AddMissingEnumCasesToSwitchExpressionAsync(
                                context.Document, switchExpression, missingMembers, enumTypeMetadata, c),
                            equivalenceKey: "AddAllEnumCases"),
                        diagnostic);
                }

                // CodeFix for adding individual cases
                foreach (var member in missingMembers)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: string.Format(Resources.CodeFixAddSingleCase, member),
                            createChangedDocument: c => AddMissingEnumCasesToSwitchExpressionAsync(
                                context.Document, switchExpression, new[] { member }, enumTypeMetadata, c),
                            equivalenceKey: $"AddSingleEnumCase_{member}"),
                        diagnostic);
                }
            }
        }

        private async Task<Document> AddMissingEnumCasesToSwitchStatementAsync(
            Document document,
            SwitchStatementSyntax switchStatement,
            IReadOnlyList<string> missingMembers,
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

            // Get the enum type symbol
            var enumType = GetEnumTypeSymbol(semanticModel, enumTypeMetadata);
            if (enumType == null)
            {
                return document;
            }

            var sections = switchStatement.Sections;
            var defaultIndex = sections.IndexOf(s => s.Labels.Any(l => l is DefaultSwitchLabelSyntax));
            if (defaultIndex < 0) defaultIndex = sections.Count;

            var newSections = sections;
            // Insert in reverse order so items are inserted from the beginning of the list
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

            // Get the enum type symbol
            var enumType = GetEnumTypeSymbol(semanticModel, enumTypeMetadata);
            if (enumType == null)
            {
                return document;
            }

            var arms = switchExpression.Arms;
            var discardIndex = arms.IndexOf(a => a.Pattern is DiscardPatternSyntax);
            if (discardIndex < 0) discardIndex = arms.Count;

            var newArms = arms;
            // Insert in reverse order so items are inserted from the beginning of the list
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

        private static INamedTypeSymbol GetEnumTypeSymbol(SemanticModel semanticModel, string enumTypeMetadata)
        {
            if (string.IsNullOrEmpty(enumTypeMetadata))
            {
                return null;
            }

            return semanticModel.Compilation.GetTypeByMetadataName(enumTypeMetadata);
        }

        private SwitchSectionSyntax CreateCaseSectionForEnumMember(INamedTypeSymbol enumType, string memberName)
        {
            var throwStatement = SyntaxFactory.ThrowStatement(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

            SwitchLabelSyntax caseLabel;
            if (memberName == "null")
            {
                caseLabel = SyntaxFactory.CaseSwitchLabel(
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression),
                    SyntaxFactory.Token(SyntaxKind.ColonToken));
            }
            else
            {
                var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var typeSyntax = SyntaxFactory.ParseTypeName(enumTypeName)
                    .WithAdditionalAnnotations(Simplifier.Annotation);

                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    typeSyntax,
                    SyntaxFactory.IdentifierName(memberName));

                caseLabel = SyntaxFactory.CaseSwitchLabel(
                    memberAccess,
                    SyntaxFactory.Token(SyntaxKind.ColonToken));
            }

            return SyntaxFactory.SwitchSection()
                .AddLabels(caseLabel)
                .AddStatements(throwStatement);
        }

        private SwitchExpressionArmSyntax CreateSwitchArmForEnumMember(INamedTypeSymbol enumType, string memberName)
        {
            var throwExpression = SyntaxFactory.ThrowExpression(
                SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                    .WithArgumentList(SyntaxFactory.ArgumentList()));

            PatternSyntax pattern;
            if (memberName == "null")
            {
                pattern = SyntaxFactory.ConstantPattern(
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
            }
            else
            {
                var enumTypeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var typeSyntax = SyntaxFactory.ParseTypeName(enumTypeName)
                    .WithAdditionalAnnotations(Simplifier.Annotation);

                var memberAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    typeSyntax,
                    SyntaxFactory.IdentifierName(memberName));

                pattern = SyntaxFactory.ConstantPattern(memberAccess);
            }

            return SyntaxFactory.SwitchExpressionArm(pattern, throwExpression);
        }

        /// <summary>
        /// Extracts missing enum member information from diagnostic data
        /// </summary>
        private static (List<string> missingMembers, string enumTypeMetadata) ExtractDiagnosticInfo(
            IEnumerable<Diagnostic> diagnostics)
        {
            var missingMembers = new List<string>();
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

                if (diag.Properties.TryGetValue("EnumTypeMetadata", out var typeMetadata))
                {
                    enumTypeMetadata = typeMetadata;
                }
            }

            return (missingMembers, enumTypeMetadata);
        }
    }
}
