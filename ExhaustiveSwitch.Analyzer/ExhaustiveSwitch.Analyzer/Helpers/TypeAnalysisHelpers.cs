using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class TypeAnalysisHelpers
    {
        /// <summary>
        /// シンボルが指定された属性を持つかどうかを判定します。
        /// </summary>
        /// <param name="symbol">チェック対象のシンボル</param>
        /// <param name="attributeType">属性の型</param>
        /// <returns>属性を持つ場合はtrue</returns>
        public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        /// <summary>
        /// 型または型の基底型/実装インターフェースから、最初の[Exhaustive]型を検索します。
        /// </summary>
        /// <param name="type">検索対象の型</param>
        /// <param name="exhaustiveAttributeType">ExhaustiveAttribute型</param>
        /// <returns>[Exhaustive]属性を持つ最初の型、見つからない場合はnull</returns>
        public static INamedTypeSymbol FindExhaustiveBaseType(ITypeSymbol type, INamedTypeSymbol exhaustiveAttributeType)
        {
            // 型自体をチェック
            if (type is INamedTypeSymbol namedType && HasAttribute(namedType, exhaustiveAttributeType))
            {
                return namedType;
            }

            // インターフェースをチェック
            foreach (var iface in type.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                {
                    return iface;
                }
            }

            // 基底クラスをチェック
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (HasAttribute(baseType, exhaustiveAttributeType))
                {
                    return baseType;
                }

                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// 型または型の基底型/実装インターフェースから、すべての[Exhaustive]型を検索します。
        /// </summary>
        /// <param name="typeSymbol">検索対象の型</param>
        /// <param name="exhaustiveAttributeType">ExhaustiveAttribute型</param>
        /// <returns>[Exhaustive]属性を持つすべての型のリスト</returns>
        public static List<INamedTypeSymbol> FindAllExhaustiveTypes(INamedTypeSymbol typeSymbol, INamedTypeSymbol exhaustiveAttributeType)
        {
            var exhaustiveTypes = new List<INamedTypeSymbol>();

            // 型自体をチェック
            if (HasAttribute(typeSymbol, exhaustiveAttributeType))
            {
                exhaustiveTypes.Add(typeSymbol);
            }

            // インターフェースをチェック
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                {
                    exhaustiveTypes.Add(iface);
                }
            }

            // 基底クラスをチェック
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                if (HasAttribute(baseType, exhaustiveAttributeType))
                {
                    exhaustiveTypes.Add(baseType);
                }

                baseType = baseType.BaseType;
            }

            return exhaustiveTypes;
        }

        /// <summary>
        /// 型が指定された基底型を実装または継承しているかを判定します。
        /// </summary>
        /// <param name="typeSymbol">チェック対象の型</param>
        /// <param name="baseType">基底型またはインターフェース</param>
        /// <returns>実装/継承している場合はtrue、型自身が一致する場合もtrueを返す</returns>
        public static bool IsImplementingOrDerivedFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType)
        {
            // 型自身が一致するケース
            if (SymbolEqualityComparer.Default.Equals(typeSymbol, baseType))
            {
                return true;
            }

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
                {
                    return true;
                }

                current = current.BaseType;
            }

            return false;
        }

        /// <summary>
        /// switchパターンから型情報を抽出します。
        /// </summary>
        /// <param name="pattern">パターン構文ノード</param>
        /// <param name="semanticModel">セマンティックモデル</param>
        /// <returns>抽出された型、取得できない場合はnull</returns>
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

                default:
                    if (pattern is PatternSyntax patternSyntax)
                    {
                        return ExtractTypeFromPatternSyntax(patternSyntax, semanticModel);
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// パターン構文から型情報を抽出します。
        /// </summary>
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
            }

            return null;
        }
    }
}
