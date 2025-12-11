using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class MetadataHelpers
    {
        /// <summary>
        /// 型の完全なメタデータ名を取得します。
        /// ジェネリック型の場合、アリティ（`1など）を含む形式で返します。
        /// ネストされた型の場合、"+"で結合します。
        /// 例: "Namespace.OuterClass+InnerClass`1"
        /// </summary>
        /// <param name="type">対象の型シンボル</param>
        /// <returns>完全なメタデータ名</returns>
        public static string GetFullMetadataName(INamedTypeSymbol type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var currentType = type;
            while (currentType != null)
            {
                // MetadataNameはジェネリック型の場合 "TypeName`1" のような形式になっている
                parts.Insert(0, currentType.MetadataName);
                currentType = currentType.ContainingType;
            }

            var namespaceName = GetNamespaceName(type.ContainingNamespace);
            if (!string.IsNullOrEmpty(namespaceName))
            {
                return namespaceName + "." + string.Join("+", parts);
            }

            return string.Join("+", parts);
        }

        /// <summary>
        /// 名前空間の完全な名前を取得します。
        /// グローバル名前空間の場合は空文字列を返します。
        /// </summary>
        /// <param name="namespaceSymbol">名前空間シンボル</param>
        /// <returns>名前空間の完全な名前（"."で区切られた形式）</returns>
        public static string GetNamespaceName(INamespaceSymbol namespaceSymbol)
        {
            if (namespaceSymbol == null || namespaceSymbol.IsGlobalNamespace)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var current = namespaceSymbol;
            while (current != null && !current.IsGlobalNamespace)
            {
                parts.Insert(0, current.Name);
                current = current.ContainingNamespace;
            }

            return string.Join(".", parts);
        }
    }
}
