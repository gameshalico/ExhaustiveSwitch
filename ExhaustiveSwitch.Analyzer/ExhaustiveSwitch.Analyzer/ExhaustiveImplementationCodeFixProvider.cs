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
        private const string Title = "不足しているケースを追加";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("EIA0001");

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return null;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // switch文またはswitch式を見つける
            var node = root.FindNode(diagnosticSpan);

            // switch文の場合
            var switchStatement = node.AncestorsAndSelf().OfType<SwitchStatementSyntax>().FirstOrDefault();
            if (switchStatement != null)
            {
                // このswitch文に関連するすべての診断を取得
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == "EIA0001")
                    .ToList();

                // 複数の診断がある場合、すべてのケースを一括追加するCodeFix
                if (allDiagnostics.Count > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "すべての不足しているケースを追加",
                            createChangedDocument: c => AddMissingCasesToSwitchStatementAsync(context.Document, switchStatement, allDiagnostics, c),
                            equivalenceKey: "AddAllCases"),
                        allDiagnostics.ToArray());
                }
                
                // 各診断に対して個別のCodeFixを登録
                var individualActions = new List<CodeAction>();
                foreach (var diag in allDiagnostics)
                {
                    var typeName = GetMissingTypeNameFromDiagnostic(diag);
                    individualActions.Add(CodeAction.Create(
                        title: $"'{typeName}' ケースを追加",
                        createChangedDocument: c => AddMissingCasesToSwitchStatementAsync(context.Document, switchStatement, new[] { diag }, c),
                        equivalenceKey: $"AddSingleCase_{typeName}"));
                }
                context.RegisterCodeFix(
                    CodeAction.Create("個別のケースを追加...", individualActions.ToImmutableArray(), true),
                    allDiagnostics);

                return;
            }

            // switch式の場合
            var switchExpression = node.AncestorsAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault();
            if (switchExpression != null)
            {
                // このswitch式に関連するすべての診断を取得
                var allDiagnostics = context.Diagnostics
                    .Where(d => d.Id == "EIA0001")
                    .ToArray();

                // 複数の診断がある場合、すべてのケースを一括追加するCodeFix
                if (allDiagnostics.Length > 1)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "すべての不足しているケースを追加",
                            createChangedDocument: c => AddMissingCasesToSwitchExpressionAsync(context.Document, switchExpression, allDiagnostics, c),
                            equivalenceKey: "AddAllCases"),
                        allDiagnostics);
                }
                
                // 各診断に対して個別のCodeFixを登録
                var individualActions = new List<CodeAction>();
                foreach (var diag in allDiagnostics)
                {
                    var typeName = GetMissingTypeNameFromDiagnostic(diag);
                    individualActions.Add(CodeAction.Create(
                        title: $"'{typeName}' ケースを追加",
                        createChangedDocument: c => AddMissingCasesToSwitchExpressionAsync(context.Document, switchExpression, new[] { diag }, c),
                        equivalenceKey: $"AddSingleCase_{typeName}"));
                }
                context.RegisterCodeFix(
                    CodeAction.Create("個別のケースを追加...", individualActions.ToImmutableArray(), true),
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

            // すべての診断から不足している型を取得
            var missingTypes = new List<INamedTypeSymbol>();
            foreach (var diagnostic in diagnostics)
            {
                var missingTypeName = GetMissingTypeNameFromDiagnostic(diagnostic);
                if (string.IsNullOrEmpty(missingTypeName))
                    continue;

                var missingType = FindTypeByName(semanticModel.Compilation, missingTypeName);
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
                var newCaseSection = CreateCaseSectionForType(missingType, switchStatement);
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

            // すべての診断から不足している型を取得
            var missingTypes = new List<INamedTypeSymbol>();
            foreach (var diagnostic in diagnostics)
            {
                var missingTypeName = GetMissingTypeNameFromDiagnostic(diagnostic);
                if (string.IsNullOrEmpty(missingTypeName))
                    continue;

                var missingType = FindTypeByName(semanticModel.Compilation, missingTypeName);
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
                var newArm = CreateSwitchArmForType(missingType, switchExpression);
                newArms = newArms.Insert(discardIndex, newArm);
                discardIndex++; // 次のケースは現在挿入した位置の後に挿入
            }

            var newSwitchExpression = switchExpression.WithArms(newArms);

            // フォーマットを適用
            newSwitchExpression = newSwitchExpression.WithAdditionalAnnotations(Formatter.Annotation);

            var newRoot = root.ReplaceNode(switchExpression, newSwitchExpression);
            return document.WithSyntaxRoot(newRoot);
        }

        private SwitchSectionSyntax CreateCaseSectionForType(INamedTypeSymbol type, SwitchStatementSyntax switchStatement)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var variableName = GetVariableName(type);

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

        private SwitchExpressionArmSyntax CreateSwitchArmForType(INamedTypeSymbol type, SwitchExpressionSyntax switchExpression)
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var variableName = GetVariableName(type);

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

        private string GetVariableName(INamedTypeSymbol type)
        {
            var name = type.Name;
            if (string.IsNullOrEmpty(name))
                return "value";

            // 最初の文字を小文字に変換
            return char.ToLower(name[0]) + name.Substring(1);
        }

        private string GetMissingTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            // 診断のPropertiesから型名を取得
            if (diagnostic.Properties.TryGetValue("MissingType", out var typeName))
            {
                return typeName;
            }

            return null;
        }

        private INamedTypeSymbol FindTypeByName(Compilation compilation, string typeName)
        {
            // グローバル名前空間から型を検索
            var allTypes = GetAllTypes(compilation.GlobalNamespace);
            return allTypes.FirstOrDefault(t => t.ToDisplayString() == typeName || t.Name == typeName);
        }

        private IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol namespaceSymbol)
        {
            foreach (var type in namespaceSymbol.GetTypeMembers())
            {
                yield return type;

                // ネストされた型も含める
                foreach (var nestedType in GetNestedTypes(type))
                {
                    yield return nestedType;
                }
            }

            foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (var type in GetAllTypes(childNamespace))
                {
                    yield return type;
                }
            }
        }

        private IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
        {
            foreach (var nestedType in type.GetTypeMembers())
            {
                yield return nestedType;

                foreach (var deeplyNestedType in GetNestedTypes(nestedType))
                {
                    yield return deeplyNestedType;
                }
            }
        }
    }
}
