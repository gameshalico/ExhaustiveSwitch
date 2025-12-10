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
        private const string DiagnosticId = "EIA0001";

        private static readonly LocalizableString Title = "網羅性の不足";
        private static readonly LocalizableString MessageFormat = "Exhaustive 型 '{0}' の '{1}' ケースが switch で処理されていません。";
        private static readonly LocalizableString Description = "[Case]属性を持つすべての具象型が明示的に処理されている必要があります.";
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

                // コンパイレーション全体から[Case]型を収集してキャッシュ（重複排除対応）
                // 外側: [Exhaustive]型 -> 内側: [Case]型のセット（ConcurrentDictionaryをHashSetとして使用）
                var caseCache = new ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>>(SymbolEqualityComparer.Default);

                // Step A: 外部参照の最適化スキャン（Cold Path）
                var attributeDefiningAssembly = exhaustiveAttributeType.ContainingAssembly;
                var referencedAssemblies = compilationContext.Compilation.References
                    .Select(r => compilationContext.Compilation.GetAssemblyOrModuleSymbol(r) as IAssemblySymbol)
                    .Where(a => a != null)
                    .ToList();

                // 並列処理で外部アセンブリをスキャン
                Parallel.ForEach(referencedAssemblies, assembly =>
                {
                    // フィルタリング: 属性定義アセンブリを参照していないアセンブリはスキップ
                    if (!ReferencesAssembly(assembly, attributeDefiningAssembly))
                        return;

                    // このアセンブリ内のすべての型を再帰的にスキャン
                    ScanTypesInNamespace(assembly.GlobalNamespace, exhaustiveAttributeType, caseAttributeType, caseCache);
                });

                // Step B: 内部コードの逐次スキャン（Hot Path）
                compilationContext.RegisterSymbolAction(symbolContext =>
                {
                    var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                    // 具象クラスかつ[Case]属性を持つ場合
                    if (!typeSymbol.IsAbstract &&
                        typeSymbol.TypeKind == TypeKind.Class &&
                        HasAttribute(typeSymbol, caseAttributeType))
                    {
                        // この型が実装/継承しているすべての[Exhaustive]型を見つける
                        var exhaustiveTypes = FindAllExhaustiveTypes(typeSymbol, exhaustiveAttributeType);
                        foreach (var exhaustiveType in exhaustiveTypes)
                        {
                            var caseSet = caseCache.GetOrAdd(exhaustiveType, _ => new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default));
                            caseSet.TryAdd(typeSymbol, 0); // 重複排除
                        }
                    }
                }, SymbolKind.NamedType);

                // switch文の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchStatement(nodeContext, exhaustiveAttributeType, caseCache),
                    SyntaxKind.SwitchStatement);

                // switch式の解析
                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchExpression(nodeContext, exhaustiveAttributeType, caseCache),
                    SyntaxKind.SwitchExpression);
            });
        }

        private void AnalyzeSwitchStatement(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>> caseCache)
        {
            var switchStatement = (SwitchStatementSyntax)context.Node;
            var switchExpression = switchStatement.Expression;

            AnalyzeSwitchConstruct(context, switchExpression, switchStatement.GetLocation(),
                switchStatement.Sections.SelectMany(s => s.Labels).ToList(),
                exhaustiveAttributeType, caseCache);
        }

        private void AnalyzeSwitchExpression(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>> caseCache)
        {
            var switchExpression = (SwitchExpressionSyntax)context.Node;
            var governingExpression = switchExpression.GoverningExpression;

            AnalyzeSwitchConstruct(context, governingExpression, switchExpression.GetLocation(),
                switchExpression.Arms.Select(a => a.Pattern).ToList(),
                exhaustiveAttributeType, caseCache);
        }

        private void AnalyzeSwitchConstruct(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax governingExpression,
            Location location,
            IReadOnlyList<SyntaxNode> patterns,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>> caseCache)
        {
            var semanticModel = context.SemanticModel;
            var typeInfo = semanticModel.GetTypeInfo(governingExpression);
            var switchedType = typeInfo.Type;

            if (switchedType == null)
                return;

            // [Exhaustive]属性を持つ型を探す
            var exhaustiveType = FindExhaustiveBaseType(switchedType, exhaustiveAttributeType);
            if (exhaustiveType == null)
                return;

            // S_expected: [Case]を持つすべての具象型を取得（キャッシュから）
            if (!caseCache.TryGetValue(exhaustiveType, out var expectedCasesDict))
                return;

            // ConcurrentDictionaryのキーからHashSetに変換（既に重複排除済み）
            var expectedCases = new HashSet<INamedTypeSymbol>(expectedCasesDict.Keys, SymbolEqualityComparer.Default);
            if (expectedCases.Count == 0)
                return;

            // S_actual: switch内で明示的に処理されている具象型を収集
            var actualCases = CollectHandledCases(patterns, semanticModel, expectedCases);

            // S_expected \ S_actual を計算（処理されていないケース）
            var missingCases = expectedCases.Except<INamedTypeSymbol>(actualCases, SymbolEqualityComparer.Default).ToList();

            // 不足している型のうち、他の不足している型の祖先である型を除外
            // （祖先型は、その子孫がすべて処理されればカバーされるため）
            var casesToReport = new List<INamedTypeSymbol>();
            foreach (var missingCase in missingCases)
            {
                // missingCaseの子孫のうち、expectedCasesに含まれる型
                var descendants = new List<INamedTypeSymbol>();
                foreach (var expectedCase in expectedCases)
                {
                    if (!SymbolEqualityComparer.Default.Equals(expectedCase, missingCase) &&
                        IsImplementingOrDerivedFrom(expectedCase, missingCase))
                    {
                        descendants.Add(expectedCase);
                    }
                }

                // 子孫が存在しない場合、または子孫が全て処理済みの場合はエラーとして報告
                // 子孫が存在し、かつ少なくとも1つが不足している場合は、この祖先型は報告しない
                bool hasUnhandledDescendant = descendants.Any(d => missingCases.Contains(d, SymbolEqualityComparer.Default));
                if (descendants.Count == 0 || !hasUnhandledDescendant)
                {
                    casesToReport.Add(missingCase);
                }
            }

            // エラーを報告
            foreach (var missingCase in casesToReport)
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingType", missingCase.ToDisplayString());

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
        /// 指定された型またはその基底型から[Exhaustive]属性を持つ型を探す
        /// </summary>
        private INamedTypeSymbol FindExhaustiveBaseType(ITypeSymbol type, INamedTypeSymbol exhaustiveAttributeType)
        {
            // 型自体をチェック
            if (type is INamedTypeSymbol namedType && HasAttribute(namedType, exhaustiveAttributeType))
                return namedType;

            // インターフェースをチェック
            foreach (var iface in type.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                    return iface;
            }

            // 基底クラスをチェック
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (HasAttribute(baseType, exhaustiveAttributeType))
                    return baseType;
                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// 指定された型が実装/継承しているすべての[Exhaustive]型を見つける
        /// </summary>
        private List<INamedTypeSymbol> FindAllExhaustiveTypes(INamedTypeSymbol typeSymbol, INamedTypeSymbol exhaustiveAttributeType)
        {
            var exhaustiveTypes = new List<INamedTypeSymbol>();

            // インターフェースをチェック
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                    exhaustiveTypes.Add(iface);
            }

            // 基底クラスをチェック
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                if (HasAttribute(baseType, exhaustiveAttributeType))
                    exhaustiveTypes.Add(baseType);
                baseType = baseType.BaseType;
            }

            return exhaustiveTypes;
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
                        if (IsImplementingOrDerivedFrom(expectedCase, typeSymbol))
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
                            !SymbolEqualityComparer.Default.Equals(e, expectedCase) && IsImplementingOrDerivedFrom(e, expectedCase)).ToList();

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
                // switch文: case Goblin g:
                case CaseSwitchLabelSyntax caseLabel:
                    if (caseLabel.Value is IdentifierNameSyntax identifierName)
                    {
                        var symbolInfo = semanticModel.GetSymbolInfo(identifierName);
                        return symbolInfo.Symbol as INamedTypeSymbol;
                    }
                    break;

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

                case ConstantPatternSyntax constantPattern:
                    var constantTypeInfo = semanticModel.GetTypeInfo(constantPattern.Expression);
                    return constantTypeInfo.Type as INamedTypeSymbol;

                default:
                    return null;
            }
        }

        private bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        private bool IsImplementingOrDerivedFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType)
        {
            // インターフェースの実装をチェック
            if (baseType.TypeKind == TypeKind.Interface)
            {
                return typeSymbol.AllInterfaces.Contains(baseType, SymbolEqualityComparer.Default);
            }

            // 基底クラスの継承をチェック
            var current = typeSymbol.BaseType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                    return true;
                current = current.BaseType;
            }

            return SymbolEqualityComparer.Default.Equals(typeSymbol, baseType);
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
                if (referencedAssembly.Name == targetAssembly.Name)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 名前空間内のすべての型を再帰的にスキャン
        /// </summary>
        private void ScanTypesInNamespace(
            INamespaceSymbol namespaceSymbol,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>> cache)
        {
            // この名前空間内の型をスキャン
            foreach (var typeMember in namespaceSymbol.GetTypeMembers())
            {
                ScanType(typeMember, exhaustiveAttributeType, caseAttributeType, cache);
            }

            // ネストされた名前空間を再帰的にスキャン
            foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                ScanTypesInNamespace(nestedNamespace, exhaustiveAttributeType, caseAttributeType, cache);
            }
        }

        /// <summary>
        /// 型とそのネストされた型をスキャン
        /// </summary>
        private void ScanType(
            INamedTypeSymbol typeSymbol,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ConcurrentDictionary<INamedTypeSymbol, byte>> cache)
        {
            // 具象クラスかつ[Case]属性を持つ場合
            if (!typeSymbol.IsAbstract &&
                typeSymbol.TypeKind == TypeKind.Class &&
                HasAttribute(typeSymbol, caseAttributeType))
            {
                // この型が実装/継承しているすべての[Exhaustive]型を見つける
                var exhaustiveTypes = FindAllExhaustiveTypes(typeSymbol, exhaustiveAttributeType);
                foreach (var exhaustiveType in exhaustiveTypes)
                {
                    var caseSet = cache.GetOrAdd(exhaustiveType, _ => new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default));
                    caseSet.TryAdd(typeSymbol, 0); // 重複排除
                }
            }

            // ネストされた型を再帰的にスキャン
            foreach (var nestedType in typeSymbol.GetTypeMembers())
            {
                ScanType(nestedType, exhaustiveAttributeType, caseAttributeType, cache);
            }
        }
    }
}
