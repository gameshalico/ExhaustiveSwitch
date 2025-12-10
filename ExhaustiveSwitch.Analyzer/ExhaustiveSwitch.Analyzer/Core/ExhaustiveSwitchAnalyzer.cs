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
                var hierarchyInfoCache = new ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo>(SymbolEqualityComparer.Default);

                // switch文の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchStatement(nodeContext, exhaustiveAttributeType, caseAttributeType, hierarchyInfoCache),
                    SyntaxKind.SwitchStatement);

                // switch式の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchExpression(nodeContext, exhaustiveAttributeType, caseAttributeType, hierarchyInfoCache),
                    SyntaxKind.SwitchExpression);
            });
        }

        private void AnalyzeSwitchStatement(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoCache)
        {
            var switchStatement = (SwitchStatementSyntax)context.Node;
            var switchExpression = switchStatement.Expression;

            AnalyzeSwitchConstruct(context, switchExpression, switchStatement.GetLocation(),
                switchStatement.Sections.SelectMany(s => s.Labels).ToList(),
                exhaustiveAttributeType, caseAttributeType, hierarchyInfoCache);
        }

        private void AnalyzeSwitchExpression(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoCache)
        {
            var switchExpression = (SwitchExpressionSyntax)context.Node;
            var governingExpression = switchExpression.GoverningExpression;

            AnalyzeSwitchConstruct(context, governingExpression, switchExpression.GetLocation(),
                switchExpression.Arms.Select(a => a.Pattern).ToList(),
                exhaustiveAttributeType, caseAttributeType, hierarchyInfoCache);
        }

        private void AnalyzeSwitchConstruct(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax governingExpression,
            Location location,
            IReadOnlyList<SyntaxNode> patterns,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoCache)
        {
            var semanticModel = context.SemanticModel;
            var typeInfo = semanticModel.GetTypeInfo(governingExpression);
            var switchedType = typeInfo.Type;

            if (switchedType == null)
            {
                return;
            }

            // [Exhaustive]属性を持つ型を探す
            var exhaustiveType = TypeAnalysisHelpers.FindExhaustiveBaseType(switchedType, exhaustiveAttributeType);
            if (exhaustiveType == null)
            {
                return;
            }

            // S_expected: キャッシュを確認し、なければスキャンを実行する
            var hierarchyInfo = hierarchyInfoCache.GetOrAdd(
                exhaustiveType,
                _ => new ExhaustiveHierarchyInfo( ScanForDerivedTypes(context.Compilation, exhaustiveType, caseAttributeType)));
            if (hierarchyInfo.AllCases.Count == 0)
            {
                return;
            }
            
            var handledCases = CollectHandledCases(patterns, semanticModel, hierarchyInfo);
            var missingCases = new HashSet<INamedTypeSymbol>(hierarchyInfo.AllCases, SymbolEqualityComparer.Default);
            missingCases.ExceptWith(handledCases);
            
            if (missingCases.Count == 0)
            {
                return;
            }
            
            // 不足している型のうち、報告すべき型をフィルタリング
            var casesToReport = TypeAnalysisHelpers.FilterAncestorsWithUnhandledDescendants(missingCases, hierarchyInfo.AllCases);

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
        
        private HashSet<INamedTypeSymbol> CollectHandledCases(
            IReadOnlyList<SyntaxNode> patterns,
            SemanticModel semanticModel,
            ExhaustiveHierarchyInfo hierarchyInfo)
        {
            // 1. Switch文で明示的に処理された型
            var explicitlyHandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            
            foreach (var pattern in patterns)
            {
                var typeSymbol = ExtractTypeFromPattern(pattern, semanticModel);
                if (typeSymbol != null && hierarchyInfo.AllCases.Contains(typeSymbol))
                {
                    explicitlyHandled.Add(typeSymbol);
                }
            }
        
            // 2. グラフ探索でカバレッジを判定
            var finalHandledCases = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        
            // メモ化用辞書（再帰呼び出しのコスト削減と循環参照回避）
            // true: カバー済み, false: 未カバー
            var memo = new Dictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
        
            foreach (var candidate in hierarchyInfo.AllCases)
            {
                if (CheckCoverageRecursive(candidate, explicitlyHandled, hierarchyInfo, memo))
                {
                    finalHandledCases.Add(candidate);
                }
            }
        
            return finalHandledCases;
        }
        
        /// <summary>
        /// 再帰的にカバレッジを判定（メモ化付き）
        /// </summary>
        private bool CheckCoverageRecursive(
            INamedTypeSymbol type,
            HashSet<INamedTypeSymbol> explicitlyHandled,
            ExhaustiveHierarchyInfo hierarchyInfo,
            Dictionary<INamedTypeSymbol, bool> memo)
        {
            if (memo.TryGetValue(type, out var cachedResult))
            {
                return cachedResult;
            }
        
            // 循環参照防止のため、一旦falseを入れておく（DAGなら本来循環しないが念のため）
            memo[type] = false;
        
            // 条件1: 自身が明示的に書かれている
            if (explicitlyHandled.Contains(type))
            {
                memo[type] = true;
                return true;
            }
        
            // 条件2: 親（先祖）のいずれかが明示的に書かれている
            // ※ここが重要：親が複数いる場合、どれか1つでもカバーされていれば、自分もカバーされたとみなす
            // （例: switch(interface) で interface で受けていれば、実装クラスはすべてOK）
            if (IsAnyAncestorExplicitlyHandled(type, explicitlyHandled, hierarchyInfo))
            {
                memo[type] = true;
                return true;
            }
        
            // 条件3: すべての「直接の子」がカバーされている
            if (hierarchyInfo.DirectChildrenMap.TryGetValue(type, out var children) && children.Count > 0)
            {
                bool allChildrenCovered = true;
                foreach (var child in children)
                {
                    if (!CheckCoverageRecursive(child, explicitlyHandled, hierarchyInfo, memo))
                    {
                        allChildrenCovered = false;
                        break;
                    }
                }
        
                if (allChildrenCovered)
                {
                    memo[type] = true;
                    return true;
                }
            }
        
            // 条件1,2,3どれも満たさない -> 未カバー
            return false;
        }
        
        /// <summary>
        /// 祖先を辿って「明示的にハンドルされているか」を確認
        /// </summary>
        private bool IsAnyAncestorExplicitlyHandled(
            INamedTypeSymbol type,
            HashSet<INamedTypeSymbol> explicitlyHandled,
            ExhaustiveHierarchyInfo hierarchyInfo)
        {
            // 幅優先探索で親を遡る
            var queue = new Queue<INamedTypeSymbol>();
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            
            // 初期親を追加
            if (hierarchyInfo.DirectParentsMap.TryGetValue(type, out var parents))
            {
                foreach (var p in parents) queue.Enqueue(p);
            }
        
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current)) continue;
        
                if (explicitlyHandled.Contains(current))
                {
                    return true;
                }
        
                // さらに上の親へ
                if (hierarchyInfo.DirectParentsMap.TryGetValue(current, out var grandParents))
                {
                    foreach (var gp in grandParents) queue.Enqueue(gp);
                }
            }
        
            return false;
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
