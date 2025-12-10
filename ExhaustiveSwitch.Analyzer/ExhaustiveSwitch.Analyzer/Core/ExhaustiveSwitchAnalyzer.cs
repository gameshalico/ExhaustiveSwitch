using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveSwitch.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExhaustiveSwitchAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "EXH0001";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
            nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(
            nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var exhaustiveAttributeType = compilationContext.Compilation.GetTypeByMetadataName(
                    "ExhaustiveSwitch.ExhaustiveAttribute");
                var caseAttributeType = compilationContext.Compilation.GetTypeByMetadataName(
                    "ExhaustiveSwitch.CaseAttribute");

                if (exhaustiveAttributeType == null || caseAttributeType == null)
                    return;

                // [Exhaustive] -> [Case] のキャッシュ
                var implementationCache = new ConcurrentDictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

                // switch文の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchStatement(nodeContext, exhaustiveAttributeType, caseAttributeType, implementationCache),
                    SyntaxKind.SwitchStatement);

                // switch式の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchExpression(nodeContext, exhaustiveAttributeType, caseAttributeType, implementationCache),
                    SyntaxKind.SwitchExpression);
            });
        }

        private void AnalyzeSwitchStatement(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> implementationCache)
        {
            var switchStatement = (SwitchStatementSyntax)context.Node;
            var switchExpression = switchStatement.Expression;

            AnalyzeSwitchConstruct(context, switchExpression, switchStatement.GetLocation(),
                switchStatement.Sections.SelectMany(s => s.Labels).ToList(),
                exhaustiveAttributeType, caseAttributeType, implementationCache);
        }

        private void AnalyzeSwitchExpression(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> implementationCache)
        {
            var switchExpression = (SwitchExpressionSyntax)context.Node;
            var governingExpression = switchExpression.GoverningExpression;

            AnalyzeSwitchConstruct(context, governingExpression, switchExpression.GetLocation(),
                switchExpression.Arms.Select(a => a.Pattern).ToList(),
                exhaustiveAttributeType, caseAttributeType, implementationCache);
        }

        private void AnalyzeSwitchConstruct(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax governingExpression,
            Location location,
            IReadOnlyList<SyntaxNode> patterns,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> implementationCache)
        {
            var semanticModel = context.SemanticModel;
            var typeInfo = semanticModel.GetTypeInfo(governingExpression);
            var switchedType = typeInfo.Type;

            if (switchedType == null)
                return;

            // [Exhaustive]属性を持つ型を探す
            var exhaustiveType = TypeAnalysisHelpers.FindExhaustiveBaseType(switchedType, exhaustiveAttributeType);
            if (exhaustiveType == null)
                return;

            // S_expected: キャッシュを確認し、なければスキャンを実行する
            var expectedCases = implementationCache.GetOrAdd(
                exhaustiveType,
                _ => ScanForDerivedTypes(context.Compilation, exhaustiveType, caseAttributeType));
            if (expectedCases.Count == 0)
                return;

            // S_actual: switch内で明示的に処理されている具象型を収集
            var actualCases = CollectHandledCases(patterns, semanticModel, expectedCases);

            // S_expected \ S_actual を計算（処理されていないケース）
            var missingCases = expectedCases.Except(actualCases, SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
                .ToList();

            // 不足している型のうち、報告すべき型をフィルタリング
            var casesToReport = TypeAnalysisHelpers.FilterAncestorsWithUnhandledDescendants(missingCases, expectedCases);

            foreach (var missingCase in casesToReport)
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingType", missingCase.ToDisplayString());
                properties.Add("MissingTypeMetadata", MetadataHelpers.GetFullMetadataName(missingCase));

                var diagnostic = Diagnostic.Create(
                    Rule,
                    location,
                    properties.ToImmutable(),
                    exhaustiveType.ToDisplayString(),
                    missingCase.ToDisplayString());
                context.ReportDiagnostic(diagnostic);
            }
        }
        
        private HashSet<INamedTypeSymbol> ScanForDerivedTypes(
            Compilation compilation,
            INamedTypeSymbol exhaustiveBaseType,
            INamedTypeSymbol caseAttributeType)
        {
            var expectedCases = new ConcurrentHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var definitionAssembly = exhaustiveBaseType.ContainingAssembly;
            
            ScanTypesInNamespace(compilation.GlobalNamespace, exhaustiveBaseType, caseAttributeType, expectedCases);

            // アセンブリをスキャン
            foreach (var reference in compilation.References)
            {
                var assembly = compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol;
                // 定義アセンブリを参照していない場合はスキップ
                if (assembly == null || !ReferencesAssembly(assembly, definitionAssembly))
                    continue;

                ScanTypesInNamespace(assembly.GlobalNamespace, exhaustiveBaseType, caseAttributeType, expectedCases);
            }

            return expectedCases.ToHashSet();
        }

        /// <summary>
        /// switch内で明示的に処理されている具象型を収集
        /// 継承関係を考慮し、処理された型の子孫と祖先もカバー済みとする
        /// </summary>
        private HashSet<INamedTypeSymbol> CollectHandledCases(
            IReadOnlyList<SyntaxNode> patterns,
            SemanticModel semanticModel,
            HashSet<INamedTypeSymbol> expectedCases)
        {
            var handledCases = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // ステップ1: すべてのパターンから型を抽出し、直接カバーされた型とその子孫を収集
            foreach (var pattern in patterns)
            {
                var typeSymbol = ExtractTypeFromPattern(pattern, semanticModel);
                if (typeSymbol != null)
                {
                    // switch内で処理された型自体が[Case]の場合、それを追加
                    if (expectedCases.Contains(typeSymbol, SymbolEqualityComparer.Default))
                    {
                        handledCases.Add(typeSymbol);
                    }

                    // この型の子孫のうち、[Case]を持つ型もすべてカバー済みとする
                    foreach (var expectedCase in expectedCases)
                    {
                        if (TypeAnalysisHelpers.IsImplementingOrDerivedFrom(expectedCase, typeSymbol))
                        {
                            handledCases.Add(expectedCase);
                        }
                    }
                }
            }

            // ステップ2: すべての子孫がカバー済みの祖先をカバー済みとする（反復的に）
            bool changed;
            do
            {
                changed = false;
                foreach (var expectedCase in expectedCases)
                {
                    if (!handledCases.Contains(expectedCase, SymbolEqualityComparer.Default))
                    {
                        // expectedCaseのすべての子孫（expectedCasesに含まれる）がhandledCasesに含まれるか確認
                        var descendants = expectedCases.Where(e =>
                            !SymbolEqualityComparer.Default.Equals(e, expectedCase) && TypeAnalysisHelpers.IsImplementingOrDerivedFrom(e, expectedCase)).ToList();

                        // 子孫がいて、すべての子孫がカバー済みなら、この祖先もカバー済みとする
                        if (descendants.Count > 0 && descendants.All(d => handledCases.Contains(d, SymbolEqualityComparer.Default)))
                        {
                            handledCases.Add(expectedCase);
                            changed = true;
                        }
                    }
                }
            } while (changed);

            return handledCases;
        }

        /// <summary>
        /// パターンから型を抽出
        /// </summary>
        private INamedTypeSymbol ExtractTypeFromPattern(SyntaxNode pattern, SemanticModel semanticModel)
        {
            switch (pattern)
            {
                // switch文: case Goblin g when ...:
                case CasePatternSwitchLabelSyntax casePatternLabel:
                    return ExtractTypeFromPatternSyntax(casePatternLabel.Pattern, semanticModel);

                // switch式: Goblin g => ...
                case DeclarationPatternSyntax declarationPattern:
                    return ExtractTypeFromPatternSyntax(declarationPattern, semanticModel);

                // その他のパターン
                default:
                    if (pattern is PatternSyntax patternSyntax)
                        return ExtractTypeFromPatternSyntax(patternSyntax, semanticModel);
                    break;
            }

            return null;
        }

        private INamedTypeSymbol ExtractTypeFromPatternSyntax(PatternSyntax pattern, SemanticModel semanticModel)
        {
            switch (pattern)
            {
                case DeclarationPatternSyntax declarationPattern:
                    var typeInfo = semanticModel.GetTypeInfo(declarationPattern.Type);
                    return typeInfo.Type as INamedTypeSymbol;

                case RecursivePatternSyntax recursivePattern when recursivePattern.Type != null:
                    var recursiveTypeInfo = semanticModel.GetTypeInfo(recursivePattern.Type);
                    return recursiveTypeInfo.Type as INamedTypeSymbol;

                default:
                    return null;
            }
        }


        /// <summary>
        /// アセンブリが指定されたアセンブリを参照しているかチェック
        /// </summary>
        private bool ReferencesAssembly(IAssemblySymbol assembly, IAssemblySymbol targetAssembly)
        {
            // 自分自身の場合はtrue
            if (SymbolEqualityComparer.Default.Equals(assembly, targetAssembly))
                return true;

            // 参照しているアセンブリをチェック
            foreach (var referencedAssembly in assembly.Modules.SelectMany(m => m.ReferencedAssemblies))
            {
                if (referencedAssembly.Name == targetAssembly.Identity.Name)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 名前空間内のすべての型を再帰的にスキャンし、exhaustiveBaseTypeを実装/継承しており、caseAttributeType属性を持つ型を収集
        /// </summary>
        private void ScanTypesInNamespace(
            INamespaceSymbol namespaceSymbol,
            INamedTypeSymbol exhaustiveBaseType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentHashSet<INamedTypeSymbol> results)
        {
            // この名前空間内の型をスキャン
            foreach (var typeMember in namespaceSymbol.GetTypeMembers())
            {
                ScanTypeRecursively(typeMember, exhaustiveBaseType, caseAttributeType, results);
            }

            // ネストされた名前空間を再帰的にスキャン
            foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                ScanTypesInNamespace(nestedNamespace, exhaustiveBaseType, caseAttributeType, results);
            }
        }

        /// <summary>
        /// 型とそのネストされた型をスキャンし、exhaustiveBaseTypeを実装/継承しており、caseAttributeType属性を持つ型を収集
        /// </summary>
        private void ScanTypeRecursively(
            INamedTypeSymbol typeSymbol,
            INamedTypeSymbol exhaustiveBaseType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentHashSet<INamedTypeSymbol> results)
        {
            // [Case]属性を持つ型
            if (TypeAnalysisHelpers.HasAttribute(typeSymbol, caseAttributeType))
            {
                if (TypeAnalysisHelpers.IsImplementingOrDerivedFrom(typeSymbol, exhaustiveBaseType))
                {
                    results.Add(typeSymbol);
                }
            }

            // ネストされた型を再帰的にスキャン
            foreach (var nestedType in typeSymbol.GetTypeMembers())
            {
                ScanTypeRecursively(nestedType, exhaustiveBaseType, caseAttributeType, results);
            }
        }
    }
}
