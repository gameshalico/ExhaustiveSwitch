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
                
                var lazyInheritanceMap = new Lazy<ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo>>(
                    () => BuildInheritanceMap(compilationContext.Compilation, exhaustiveAttributeType, caseAttributeType), true);
                
                // switch文の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchStatement(nodeContext, exhaustiveAttributeType, lazyInheritanceMap.Value),
                    SyntaxKind.SwitchStatement);

                // switch式の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchExpression(nodeContext, exhaustiveAttributeType, lazyInheritanceMap.Value),
                    SyntaxKind.SwitchExpression);
            });
        }
        
        /// <summary>
        /// コンパイル単位内の全型をスキャンし、[Exhaustive]な親と[Case]な子の関係マップを構築する
        /// </summary>
        private ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> BuildInheritanceMap(
            Compilation compilation,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType)
        {
            // 結果格納用マップ
            // Key: [Exhaustive]な親クラス/インターフェース
            // Value: それを継承/実装している [Case] 属性付きの子クラス一覧
            var map = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        
            // ヘルパー: 型をチェックしてマップに追加する
            void ProcessType(INamedTypeSymbol typeSymbol)
            {
                // 1. [Case] 属性がついているかチェック
                if (TypeAnalysisHelpers.HasAttribute(typeSymbol, caseAttributeType))
                {
                    // 2. [Case]がついているなら、その親となる [Exhaustive] 型をすべて探す
                    var exhaustiveBases = TypeAnalysisHelpers.FindAllExhaustiveTypes(typeSymbol, exhaustiveAttributeType);

                    foreach (var exhaustiveBase in exhaustiveBases)
                    {
                        if (!map.TryGetValue(exhaustiveBase, out var children))
                        {
                            children = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                            map[exhaustiveBase] = children;
                        }
                        children.Add(typeSymbol);
                    }
                }

                // ネストされた型の再帰スキャン（[Case]属性の有無に関わらず常に処理）
                foreach (var nested in typeSymbol.GetTypeMembers())
                {
                    ProcessType(nested);
                }
            }
        
            // ヘルパー: 名前空間を再帰的に掘る
            void ProcessNamespace(INamespaceSymbol namespaceSymbol)
            {
                foreach (var typeMember in namespaceSymbol.GetTypeMembers())
                {
                    ProcessType(typeMember);
                }
        
                foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
                {
                    ProcessNamespace(nestedNamespace);
                }
            }
        
            // 1. 現在のプロジェクトのソースコード (GlobalNamespace) をスキャン
            ProcessNamespace(compilation.GlobalNamespace);
        
            // 2. 参照アセンブリをスキャン
            var definitionAssembly = exhaustiveAttributeType.ContainingAssembly;

            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    // [Exhaustive]属性が定義されているアセンブリを参照していないアセンブリはスキャンをスキップ
                    if (!ReferencesAssembly(compilation, assembly, definitionAssembly))
                    {
                        continue;
                    }

                    ProcessNamespace(assembly.GlobalNamespace);
                }
            }

            // 3. 最終的なマップを構築して返す
            var result = new ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo>(SymbolEqualityComparer.Default);
            foreach (var kvp in map)
            {
                result[kvp.Key] = new ExhaustiveHierarchyInfo(kvp.Value);
            }
            return result;
        }

        private void AnalyzeSwitchStatement(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoMap)
        {
            var switchStatement = (SwitchStatementSyntax)context.Node;
            var switchExpression = switchStatement.Expression;

            AnalyzeSwitchConstruct(context, switchExpression, switchStatement.GetLocation(),
                switchStatement.Sections.SelectMany(s => s.Labels).ToList(),
                exhaustiveAttributeType, hierarchyInfoMap);
        }

        private void AnalyzeSwitchExpression(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoMap)
        {
            var switchExpression = (SwitchExpressionSyntax)context.Node;
            var governingExpression = switchExpression.GoverningExpression;

            AnalyzeSwitchConstruct(context, governingExpression, switchExpression.GetLocation(),
                switchExpression.Arms.Select(a => a.Pattern).ToList(),
                exhaustiveAttributeType, hierarchyInfoMap);
        }

        private void AnalyzeSwitchConstruct(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax governingExpression,
            Location location,
            IReadOnlyList<SyntaxNode> patterns,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoMap)
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

            // S_expected: [Exhaustive]な型に対応するすべての[Case]型を取得
            if (hierarchyInfoMap.TryGetValue(exhaustiveType, out var hierarchyInfo) == false)
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
            var casesToReport = FilterAncestorsWithUnhandledDescendants(missingCases, hierarchyInfo);

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
        
        
        /// <summary>
        /// 不足している型のうち、報告すべき型をフィルタリング
        /// 他の不足している型の祖先である型は除外（祖先型は、その子孫がすべて処理されればカバーされるため）
        /// </summary>
        private static List<INamedTypeSymbol> FilterAncestorsWithUnhandledDescendants(
            HashSet<INamedTypeSymbol> missingCases,
            ExhaustiveHierarchyInfo hierarchyInfo)
        {
            var casesToReport = new List<INamedTypeSymbol>();

            foreach (var missingCase in missingCases)
            {
                if (hierarchyInfo.DirectChildrenMap.TryGetValue(missingCase, out var children))
                {
                    // 具象クラスの場合は子孫のチェックは不要
                    if (missingCase.TypeKind == TypeKind.Class && !missingCase.IsAbstract)
                    {
                        casesToReport.Add(missingCase);
                        continue;
                    }
                    
                    // 不足している子孫がいるか
                    bool hasMissingChild = false;
                    foreach (var child in children)
                    {
                        if (missingCases.Contains(child))
                        {
                            hasMissingChild = true;
                            break;
                        }
                    }
                    
                    if (!hasMissingChild)
                    {
                        casesToReport.Add(missingCase);
                    }
                }
                else
                {
                    casesToReport.Add(missingCase);
                }
            }

            return casesToReport;
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
                var typeSymbol = TypeAnalysisHelpers.ExtractTypeFromPattern(pattern, semanticModel);
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
            // ただし、型がabstractまたはsealedでない場合、その型自体もインスタンス化可能なため、
            // 子クラスのカバレッジだけでは不十分（明示的な処理が必要）
            if (hierarchyInfo.DirectChildrenMap.TryGetValue(type, out var children) && children.Count > 0)
            {
                // abstractまたはsealedの場合のみ、子クラスのカバレッジで親をカバー可能
                // interfaceの場合も子クラスのカバレッジで十分
                bool canBeCoveredByChildren = type.IsAbstract || type.IsSealed || type.TypeKind == TypeKind.Interface;

                if (canBeCoveredByChildren)
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
        /// アセンブリが指定されたアセンブリを参照しているかチェック（transitive参照を含む）
        /// </summary>
        private bool ReferencesAssembly(Compilation compilation, IAssemblySymbol assembly, IAssemblySymbol targetAssembly)
        {
            // 自分自身の場合はtrue
            if (SymbolEqualityComparer.Default.Equals(assembly, targetAssembly))
                return true;

            // 探索済みのアセンブリを記録（循環参照対策）
            var visited = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            var queue = new Queue<IAssemblySymbol>();
            queue.Enqueue(assembly);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                    continue;

                // 参照しているアセンブリをチェック
                foreach (var referencedAssemblyIdentity in current.Modules.SelectMany(m => m.ReferencedAssemblies))
                {
                    if (referencedAssemblyIdentity.Name == targetAssembly.Identity.Name)
                        return true;

                    // Compilationから参照先のアセンブリシンボルを取得して再帰的にチェック
                    foreach (var reference in compilation.References)
                    {
                        if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol referencedAssembly)
                        {
                            if (referencedAssembly.Identity.Name == referencedAssemblyIdentity.Name)
                            {
                                queue.Enqueue(referencedAssembly);
                                break;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
