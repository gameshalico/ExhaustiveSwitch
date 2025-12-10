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

namespace ExhaustiveSwitch.Analyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExhaustiveSwitchCodeFixProvider)), Shared]
    public class ExhaustiveSwitchCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("EXH0001");
        
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // Fix Allは不要
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

            // switch文またはswitch式を見つける
            var node = root.FindNode(diagnosticSpan);

            // switch文の場合
            var switchStatement = node.AncestorsAndSelf().OfType<SwitchStatementSyntax>().FirstOrDefault();
            if (switchStatement != null)
            {
                // このswitch文に関連するすべての診断を取得
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == "EXH0001")
                    .ToList();

                // 複数の診断がある場合、すべてのケースを一括追加するCodeFix
                if (allDiagnostics.Count > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: Resources.CodeFixAddAllCases,
                            createChangedDocument: c => AddMissingCasesToSwitchStatementAsync(context.Document, switchStatement, allDiagnostics, c),
                            equivalenceKey: "AddAllCases"),
                        allDiagnostics.ToArray());
                }

                // 各診断に対して個別のCodeFixを登録
                var individualActions = new List<CodeAction>();
                foreach (var diag in allDiagnostics)
                {
                    var typeName = DiagnosticHelpers.GetMissingTypeNameFromDiagnostic(diag);
                    individualActions.Add(CodeAction.Create(
                        title: string.Format(Resources.CodeFixAddSingleCase, typeName),
                        createChangedDocument: c => AddMissingCasesToSwitchStatementAsync(context.Document, switchStatement, new[] { diag }, c),
                        equivalenceKey: $"AddSingleCase_{typeName}"));
                }
                context.RegisterCodeFix(
                    CodeAction.Create(Resources.CodeFixAddIndividualCases, individualActions.ToImmutableArray(), true),
                    allDiagnostics);

                return;
            }

            // switch式の場合
            var switchExpression = node.AncestorsAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault();
            if (switchExpression != null)
            {
                // このswitch式に関連するすべての診断を取得
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == "EXH0001")
                    .ToArray();

                // 複数の診断がある場合、すべてのケースを一括追加するCodeFix
                if (allDiagnostics.Length > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: Resources.CodeFixAddAllCases,
                            createChangedDocument: c => AddMissingCasesToSwitchExpressionAsync(context.Document, switchExpression, allDiagnostics, c),
                            equivalenceKey: "AddAllCases"),
                        allDiagnostics);
                }

                // 各診断に対して個別のCodeFixを登録
                var individualActions = new List<CodeAction>();
                foreach (var diag in allDiagnostics)
                {
                    var typeName = DiagnosticHelpers.GetMissingTypeNameFromDiagnostic(diag);
                    individualActions.Add(CodeAction.Create(
                        title: string.Format(Resources.CodeFixAddSingleCase, typeName),
                        createChangedDocument: c => AddMissingCasesToSwitchExpressionAsync(context.Document, switchExpression, new[] { diag }, c),
                        equivalenceKey: $"AddSingleCase_{typeName}"));
                }
                context.RegisterCodeFix(
                    CodeAction.Create(Resources.CodeFixAddIndividualCases, individualActions.ToImmutableArray(), true),
                    allDiagnostics);
            }
        }

        private async Task<Document> AddMissingCasesToSwitchStatementAsync(
            Document document,
            SwitchStatementSyntax switchStatement,
            IEnumerable<Diagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            
            if (root == null || semanticModel == null)
            {
                return document;
            }

            // すべての診断から不足している型を取得
            var missingTypes = new List<INamedTypeSymbol>();
            foreach (var diagnostic in diagnostics)
            {
                var missingType = DiagnosticHelpers.GetMissingTypeFromDiagnostic(diagnostic, semanticModel.Compilation);
                if (missingType != null)
                {
                    missingTypes.Add(missingType);
                }
            }

            if (missingTypes.Count == 0)
                return document;

            // 既存のsectionsの最後（default以外）に追加
            var sections = switchStatement.Sections;
            var defaultSection = sections.FirstOrDefault(s => s.Labels.Any(l => l is DefaultSwitchLabelSyntax));
            var defaultIndex = defaultSection != null ? sections.IndexOf(defaultSection) : sections.Count;

            // 新しいcaseセクションを作成して挿入
            var newSections = sections;
            foreach (var missingType in missingTypes)
            {
                var newCaseSection = CreateCaseSectionForType(missingType);
                newSections = newSections.Insert(defaultIndex, newCaseSection);
                defaultIndex++; // 次のケースは現在挿入した位置の後に挿入
            }

            var newSwitchStatement = switchStatement.WithSections(newSections);

            // フォーマットを適用
            newSwitchStatement = newSwitchStatement.WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchStatement, newSwitchStatement);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AddMissingCasesToSwitchExpressionAsync(
            Document document,
            SwitchExpressionSyntax switchExpression,
            IEnumerable<Diagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            
            if (root == null || semanticModel == null)
            {
                return document;
            }

            // すべての診断から不足している型を取得
            var missingTypes = new List<INamedTypeSymbol>();
            foreach (var diagnostic in diagnostics)
            {
                var missingType = DiagnosticHelpers.GetMissingTypeFromDiagnostic(diagnostic, semanticModel.Compilation);
                if (missingType != null)
                {
                    missingTypes.Add(missingType);
                }
            }

            if (missingTypes.Count == 0)
                return document;

            // 既存のarmsの最後（discard以外）に追加
            var arms = switchExpression.Arms;
            var discardArm = arms.FirstOrDefault(a => a.Pattern is DiscardPatternSyntax);
            var discardIndex = discardArm != null ? arms.IndexOf(discardArm) : arms.Count;

            // 新しいswitch armを作成して挿入
            var newArms = arms;
            foreach (var missingType in missingTypes)
            {
                var newArm = CreateSwitchArmForType(missingType);
                newArms = newArms.Insert(discardIndex, newArm);
                discardIndex++; // 次のケースは現在挿入した位置の後に挿入
            }

            var newSwitchExpression = switchExpression.WithArms(newArms);

            // フォーマットを適用
            newSwitchExpression = newSwitchExpression.WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchExpression, newSwitchExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        private SwitchSectionSyntax CreateCaseSectionForType(INamedTypeSymbol type)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var variableName = CodeGenerationHelpers.GetVariableName(type);

            // case TypeName variableName:
            //     throw new NotImplementedException();
            var caseLabel = SyntaxFactory.CasePatternSwitchLabel(
                SyntaxFactory.DeclarationPattern(
                    SyntaxFactory.ParseTypeName(typeName),
                    SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(variableName))),
                null,
                SyntaxFactory.Token(SyntaxKind.ColonToken));

            var throwStatement = SyntaxFactory.ThrowStatement(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

            var breakStatement = SyntaxFactory.BreakStatement();

            var section = SyntaxFactory.SwitchSection()
                .AddLabels(caseLabel)
                .AddStatements(throwStatement, breakStatement);

            return section;
        }

        private SwitchExpressionArmSyntax CreateSwitchArmForType(INamedTypeSymbol type)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var variableName = CodeGenerationHelpers.GetVariableName(type);

            // TypeName variableName => throw new NotImplementedException(),
            var pattern = SyntaxFactory.DeclarationPattern(
                SyntaxFactory.ParseTypeName(typeName),
                SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(variableName)));

            var throwExpression = SyntaxFactory.ThrowExpression(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.ParseTypeName("System.NotImplementedException"))
                .WithArgumentList(SyntaxFactory.ArgumentList()));

            var arm = SyntaxFactory.SwitchExpressionArm(
                pattern,
                throwExpression);

            return arm;
        }
    }
}
