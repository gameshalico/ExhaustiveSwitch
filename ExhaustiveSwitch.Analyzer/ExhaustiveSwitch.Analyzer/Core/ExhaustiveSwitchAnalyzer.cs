using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        private const string OrphanCaseDiagnosticId = "EXH0002";

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

        private static readonly LocalizableString OrphanCaseTitle = new LocalizableResourceString(
            nameof(Resources.OrphanCaseTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString OrphanCaseMessageFormat = new LocalizableResourceString(
            nameof(Resources.OrphanCaseMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString OrphanCaseDescription = new LocalizableResourceString(
            nameof(Resources.OrphanCaseDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor OrphanCaseRule = new DiagnosticDescriptor(
            OrphanCaseDiagnosticId,
            OrphanCaseTitle,
            OrphanCaseMessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: OrphanCaseDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, OrphanCaseRule);

        private static readonly SymbolDisplayFormat SimpleTypeNameFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

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
                {
                    return;
                }

                var lazyHierarchyInfoMap = new Lazy<ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo>>(
                    () => BuildInheritanceMap(compilationContext.Compilation, exhaustiveAttributeType, caseAttributeType), true);

                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchStatement(nodeContext, exhaustiveAttributeType, lazyHierarchyInfoMap.Value),
                    SyntaxKind.SwitchStatement);

                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchExpression(nodeContext, exhaustiveAttributeType, lazyHierarchyInfoMap.Value),
                    SyntaxKind.SwitchExpression);

                compilationContext.RegisterSymbolAction(symbolContext =>
                    AnalyzeTypeSymbol(symbolContext, exhaustiveAttributeType, caseAttributeType),
                    SymbolKind.NamedType);
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
            // Key: [Exhaustive]な親クラス/インターフェース
            // Value: それを継承/実装している [Case] 属性付きの子クラス一覧
            var map = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            void ProcessType(INamedTypeSymbol typeSymbol)
            {
                if (TypeAnalysisHelpers.HasAttribute(typeSymbol, caseAttributeType))
                {
                    var exhaustiveBases = TypeAnalysisHelpers.FindAllExhaustiveTypes(typeSymbol, exhaustiveAttributeType);

                    foreach (var exhaustiveBase in exhaustiveBases)
                    {
                        // ジェネリック型の場合は型定義（OriginalDefinition）をキーとして使用
                        var exhaustiveBaseKey = exhaustiveBase.IsGenericType ? exhaustiveBase.OriginalDefinition : exhaustiveBase;

                        if (!map.TryGetValue(exhaustiveBaseKey, out var children))
                        {
                            children = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                            map[exhaustiveBaseKey] = children;
                        }

                        // Case型もジェネリックの場合は型定義を格納
                        var typeToAdd = typeSymbol.IsGenericType ? typeSymbol.OriginalDefinition : typeSymbol;
                        children.Add(typeToAdd);
                    }
                }

                // ネストされた型の再帰スキャン
                var nestedTypes = typeSymbol.GetTypeMembers();
                if (nestedTypes.IsEmpty)
                {
                    return;
                }

                foreach (var nested in nestedTypes)
                {
                    ProcessType(nested);
                }
            }

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

            // 現在のプロジェクトのソースコードをスキャン
            ProcessNamespace(compilation.GlobalNamespace);

            // 参照アセンブリをスキャン
            var definitionAssembly = exhaustiveAttributeType.ContainingAssembly;

            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    // [Exhaustive]属性が定義されているアセンブリを参照していないアセンブリはスキャンをスキップ
                    if (!ReferencesAssembly(assembly, definitionAssembly))
                    {
                        continue;
                    }

                    ProcessNamespace(assembly.GlobalNamespace);
                }
            }

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

            // ジェネリック型の場合、型定義（OriginalDefinition）でマップを検索
            var exhaustiveTypeKey = exhaustiveType.IsGenericType ? exhaustiveType.OriginalDefinition : exhaustiveType;

            // S_expected: [Exhaustive]な型に対応するすべての[Case]型を取得
            if (hierarchyInfoMap.TryGetValue(exhaustiveTypeKey, out var hierarchyInfo) == false)
            {
                return;
            }

            // ジェネリック型の場合、型引数を適用
            if (hierarchyInfo.IsGeneric && exhaustiveType.IsGenericType)
            {
                hierarchyInfo = hierarchyInfo.ApplyTypeArguments(exhaustiveType);
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

            // すべての欠けている型の情報を準備（最初の診断で使用）
            var allMissingTypesNames = string.Join(";", casesToReport.Select(t => t.ToDisplayString(SimpleTypeNameFormat)));
            var allMissingTypesMetadata = string.Join(";", casesToReport.Select(MetadataHelpers.GetFullMetadataName));

            bool isFirst = true;
            foreach (var missingCase in casesToReport)
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingType", missingCase.ToDisplayString(SimpleTypeNameFormat));
                properties.Add("MissingTypeMetadata", MetadataHelpers.GetFullMetadataName(missingCase));

                // 最初の診断の場合のみ、すべての欠けている型の情報を追加
                if (isFirst)
                {
                    properties.Add("IsFirstDiagnostic", "true");
                    properties.Add("AllMissingTypes", allMissingTypesNames);
                    properties.Add("AllMissingTypesMetadata", allMissingTypesMetadata);
                    isFirst = false;
                }
                else
                {
                    properties.Add("IsFirstDiagnostic", "false");
                }

                var diagnostic = Diagnostic.Create(
                    Rule,
                    location,
                    properties.ToImmutable(),
                    exhaustiveType.ToDisplayString(SimpleTypeNameFormat),
                    missingCase.ToDisplayString(SimpleTypeNameFormat));
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
            var explicitlyHandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var pattern in patterns)
            {
                var typeSymbol = TypeAnalysisHelpers.ExtractTypeFromPattern(pattern, semanticModel);
                if (typeSymbol == null)
                {
                    continue;
                }

                // パターンの型が[Case]型である場合、直接追加
                if (hierarchyInfo.AllCases.Contains(typeSymbol))
                {
                    explicitlyHandled.Add(typeSymbol);
                }
                else
                {
                    // パターンの型が[Case]型でない場合、その型を実装/継承している[Case]型をすべて追加
                    foreach (var caseType in hierarchyInfo.AllCases)
                    {
                        if (TypeAnalysisHelpers.IsImplementingOrDerivedFrom(caseType, typeSymbol))
                        {
                            explicitlyHandled.Add(caseType);
                        }
                    }
                }
            }

            var finalHandledCases = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // メモ化用辞書 (true: カバー済み, false: 未カバー)
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

            // 循環参照防止のため一旦falseを設定（DAGなら循環しないが念のため）
            memo[type] = false;

            if (explicitlyHandled.Contains(type))
            {
                memo[type] = true;
                return true;
            }

            // 親のいずれかが明示的に処理されている場合、自身もカバー済みとみなす
            if (IsAnyAncestorExplicitlyHandled(type, explicitlyHandled, hierarchyInfo))
            {
                memo[type] = true;
                return true;
            }

            // abstract/sealed/interfaceのみ子クラスのカバレッジで親をカバー可能
            if (hierarchyInfo.DirectChildrenMap.TryGetValue(type, out var children) && children.Count > 0)
            {
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
            var queue = new Queue<INamedTypeSymbol>();
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            if (hierarchyInfo.DirectParentsMap.TryGetValue(type, out var parents))
            {
                foreach (var p in parents)
                {
                    queue.Enqueue(p);
                }
            }
        
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (explicitlyHandled.Contains(current))
                {
                    return true;
                }

                if (hierarchyInfo.DirectParentsMap.TryGetValue(current, out var grandParents))
                {
                    foreach (var gp in grandParents)
                    {
                        queue.Enqueue(gp);
                    }
                }
            }

            return false;
        }

        private bool ReferencesAssembly(IAssemblySymbol assembly, IAssemblySymbol targetAssembly)
        {
            if (SymbolEqualityComparer.Default.Equals(assembly, targetAssembly))
            {
                return true;
            }

            // 直接参照のみをチェック
            var targetName = targetAssembly.Identity.Name;
            foreach (var module in assembly.Modules)
            {
                foreach (var refAssembly in module.ReferencedAssemblies)
                {
                    if (refAssembly.Name == targetName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 型シンボルが[Case]属性を持つが[Exhaustive]型を継承/実装していない場合に警告を出す
        /// </summary>
        private void AnalyzeTypeSymbol(
            SymbolAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;

            // [Case]属性を持つかチェック
            if (!TypeAnalysisHelpers.HasAttribute(typeSymbol, caseAttributeType))
            {
                return;
            }

            // 上位に[Exhaustive]型があるかチェック
            var exhaustiveBases = TypeAnalysisHelpers.FindAllExhaustiveTypes(typeSymbol, exhaustiveAttributeType);
            if (exhaustiveBases.Count == 0)
            {
                // [Case]属性があるが、[Exhaustive]型が見つからない場合は警告
                var diagnostic = Diagnostic.Create(
                    OrphanCaseRule,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.ToDisplayString(SimpleTypeNameFormat));
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
