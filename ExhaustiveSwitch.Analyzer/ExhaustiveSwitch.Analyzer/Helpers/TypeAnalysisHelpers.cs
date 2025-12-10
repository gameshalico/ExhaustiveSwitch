using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExhaustiveSwitch.Analyzer
{
    /// <summary>
    /// 型の階層構造の分析と属性チェックを行うヘルパーメソッド
    /// </summary>
    internal static class TypeAnalysisHelpers
    {
        /// <summary>
        /// シンボルが指定された属性を持っているかチェック
        /// </summary>
        public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        /// <summary>
        /// 指定された型またはその基底型から[Exhaustive]属性を持つ型を探す
        /// </summary>
        public static INamedTypeSymbol FindExhaustiveBaseType(ITypeSymbol type, INamedTypeSymbol exhaustiveAttributeType)
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
        public static List<INamedTypeSymbol> FindAllExhaustiveTypes(INamedTypeSymbol typeSymbol, INamedTypeSymbol exhaustiveAttributeType)
        {
            var exhaustiveTypes = new List<INamedTypeSymbol>();

            // 型自体をチェック
            if (HasAttribute(typeSymbol, exhaustiveAttributeType))
                exhaustiveTypes.Add(typeSymbol);

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
        /// typeSymbol が baseType を実装または継承しているかチェック
        /// </summary>
        public static bool IsImplementingOrDerivedFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType)
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
        /// 不足している型のうち、報告すべき型をフィルタリング
        /// 他の不足している型の祖先である型は除外（祖先型は、その子孫がすべて処理されればカバーされるため）
        /// </summary>
        public static List<INamedTypeSymbol> FilterAncestorsWithUnhandledDescendants(
            HashSet<INamedTypeSymbol> missingCases,
            HashSet<INamedTypeSymbol> expectedCases)
        {
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

            return casesToReport;
        }
        
        /// <summary>
        /// パターンから型を抽出
        /// </summary>
        public static INamedTypeSymbol ExtractTypeFromPattern(SyntaxNode pattern, SemanticModel semanticModel)
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

        private static INamedTypeSymbol ExtractTypeFromPatternSyntax(PatternSyntax pattern, SemanticModel semanticModel)
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

    }
}
