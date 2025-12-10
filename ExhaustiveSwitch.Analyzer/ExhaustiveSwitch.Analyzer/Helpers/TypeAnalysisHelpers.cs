using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

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
    }
}
