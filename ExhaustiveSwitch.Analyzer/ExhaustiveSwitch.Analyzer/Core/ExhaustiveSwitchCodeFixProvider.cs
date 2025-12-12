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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExhaustiveSwitchCodeFixProvider)), Shared]
    public class ExhaustiveSwitchCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("EXH0001");
        
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

            var switchStatement = node.AncestorsAndSelf().OfType<SwitchStatementSyntax>().FirstOrDefault();
            if (switchStatement != null)
            {
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == "EXH0001")
                    .ToList();

                foreach (var diag in allDiagnostics)
                {
                    // 最初の診断の場合のみ、"すべて追加"のCodeFixを登録
                    if (diag.Properties.TryGetValue("IsFirstDiagnostic", out var isFirst) && isFirst == "true")
                    {
                        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                        var allMissingTypes = DiagnosticHelpers.GetAllMissingTypesFromDiagnostic(diag, semanticModel.Compilation);

                        if (allMissingTypes.Count > 1)
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(
                                    title: Resources.CodeFixAddAllCases,
                                    createChangedDocument: c => AddMissingCasesToSwitchStatementAsync(context.Document, switchStatement, allMissingTypes, c),
                                    equivalenceKey: "AddAllCases"),
                                diag);
                        }
                    }

                    // 各診断に対して個別のCodeFixを登録
                    var typeName = DiagnosticHelpers.GetMissingTypeNameFromDiagnostic(diag);
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: string.Format(Resources.CodeFixAddSingleCase, typeName),
                            createChangedDocument: c => AddMissingCasesToSwitchStatementAsync(context.Document, switchStatement, new[] { diag }, c),
                            equivalenceKey: $"AddSingleCase_{typeName}"),
                        diag);
                }
                return;
            }

            var switchExpression = node.AncestorsAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault();
            if (switchExpression != null)
            {
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == "EXH0001")
                    .ToArray();

                foreach (var diag in allDiagnostics)
                {
                    // 最初の診断の場合のみ、"すべて追加"のCodeFixを登録
                    if (diag.Properties.TryGetValue("IsFirstDiagnostic", out var isFirst) && isFirst == "true")
                    {
                        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                        var allMissingTypes = DiagnosticHelpers.GetAllMissingTypesFromDiagnostic(diag, semanticModel.Compilation);

                        if (allMissingTypes.Count > 1)
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(
                                    title: Resources.CodeFixAddAllCases,
                                    createChangedDocument: c => AddMissingCasesToSwitchExpressionAsync(context.Document, switchExpression, allMissingTypes, c),
                                    equivalenceKey: "AddAllCases"),
                                diag);
                        }
                    }

                    // 各診断に対して個別のCodeFixを登録
                    var typeName = DiagnosticHelpers.GetMissingTypeNameFromDiagnostic(diag);
                    context.RegisterCodeFix(CodeAction.Create(
                        title: string.Format(Resources.CodeFixAddSingleCase, typeName),
                        createChangedDocument: c => AddMissingCasesToSwitchExpressionAsync(context.Document, switchExpression, new[] { diag }, c),
                        equivalenceKey: $"AddSingleCase_{typeName}"), diag);
                }
            }
        }

        private async Task<List<INamedTypeSymbol>> GetMissingTypesFromDiagnosticsAsync(
            Document document,
            IEnumerable<Diagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null)
            {
                return new List<INamedTypeSymbol>();
            }

            var missingTypes = new List<INamedTypeSymbol>();
            foreach (var diagnostic in diagnostics)
            {
                var missingType = DiagnosticHelpers.GetMissingTypeFromDiagnostic(diagnostic, semanticModel.Compilation);
                if (missingType != null)
                {
                    missingTypes.Add(missingType);
                }
            }

            return missingTypes;
        }

        private async Task<Document> AddMissingCasesToSwitchStatementAsync(
            Document document,
            SwitchStatementSyntax switchStatement,
            IEnumerable<Diagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return document;
            }

            var missingTypes = await GetMissingTypesFromDiagnosticsAsync(document, diagnostics, cancellationToken).ConfigureAwait(false);
            if (missingTypes.Count == 0)
            {
                return document;
            }

            var sections = switchStatement.Sections;
            var defaultSection = sections.FirstOrDefault(s => s.Labels.Any(l => l is DefaultSwitchLabelSyntax));
            var defaultIndex = defaultSection != null ? sections.IndexOf(defaultSection) : sections.Count;

            var newSections = sections;
            foreach (var missingType in missingTypes)
            {
                var newCaseSection = CreateCaseSectionForType(missingType);
                newSections = newSections.Insert(defaultIndex, newCaseSection);
                defaultIndex++;
            }

            var newSwitchStatement = switchStatement.WithSections(newSections)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchStatement, newSwitchStatement);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AddMissingCasesToSwitchStatementAsync(
            Document document,
            SwitchStatementSyntax switchStatement,
            IEnumerable<INamedTypeSymbol> missingTypes,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return document;
            }

            if (!missingTypes.Any())
            {
                return document;
            }

            var sections = switchStatement.Sections;
            var defaultSection = sections.FirstOrDefault(s => s.Labels.Any(l => l is DefaultSwitchLabelSyntax));
            var defaultIndex = defaultSection != null ? sections.IndexOf(defaultSection) : sections.Count;

            var newSections = sections;
            foreach (var missingType in missingTypes)
            {
                var newCaseSection = CreateCaseSectionForType(missingType);
                newSections = newSections.Insert(defaultIndex, newCaseSection);
                defaultIndex++;
            }

            var newSwitchStatement = switchStatement.WithSections(newSections)
                .WithAdditionalAnnotations(Formatter.Annotation);

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
            if (root == null)
            {
                return document;
            }

            var missingTypes = await GetMissingTypesFromDiagnosticsAsync(document, diagnostics, cancellationToken).ConfigureAwait(false);
            if (missingTypes.Count == 0)
            {
                return document;
            }

            var arms = switchExpression.Arms;
            var discardArm = arms.FirstOrDefault(a => a.Pattern is DiscardPatternSyntax);
            var discardIndex = discardArm != null ? arms.IndexOf(discardArm) : arms.Count;

            var newArms = arms;
            foreach (var missingType in missingTypes)
            {
                var newArm = CreateSwitchArmForType(missingType);
                newArms = newArms.Insert(discardIndex, newArm);
                discardIndex++;
            }

            var newSwitchExpression = switchExpression.WithArms(newArms)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchExpression, newSwitchExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Document> AddMissingCasesToSwitchExpressionAsync(
            Document document,
            SwitchExpressionSyntax switchExpression,
            IEnumerable<INamedTypeSymbol> missingTypes,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return document;
            }

            if (!missingTypes.Any())
            {
                return document;
            }

            var arms = switchExpression.Arms;
            var discardArm = arms.FirstOrDefault(a => a.Pattern is DiscardPatternSyntax);
            var discardIndex = discardArm != null ? arms.IndexOf(discardArm) : arms.Count;

            var newArms = arms;
            foreach (var missingType in missingTypes)
            {
                var newArm = CreateSwitchArmForType(missingType);
                newArms = newArms.Insert(discardIndex, newArm);
                discardIndex++;
            }

            var newSwitchExpression = switchExpression.WithArms(newArms)
                .WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchExpression, newSwitchExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        private SwitchSectionSyntax CreateCaseSectionForType(INamedTypeSymbol type)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var variableName = CodeGenerationHelpers.GetVariableName(type);
            
            var typeSyntax = SyntaxFactory.ParseTypeName(typeName)
                .WithAdditionalAnnotations(Simplifier.Annotation);

            // case TypeName variableName:
            //     throw new NotImplementedException();
            var caseLabel = SyntaxFactory.CasePatternSwitchLabel(
                SyntaxFactory.DeclarationPattern(
                    typeSyntax,
                    SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(variableName))),
                null,
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

        private SwitchExpressionArmSyntax CreateSwitchArmForType(INamedTypeSymbol type)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var variableName = CodeGenerationHelpers.GetVariableName(type);
            
            var typeSyntax = SyntaxFactory.ParseTypeName(typeName)
                .WithAdditionalAnnotations(Simplifier.Annotation);

            // TypeName variableName => throw new NotImplementedException(),
            var pattern = SyntaxFactory.DeclarationPattern(
                typeSyntax,
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
