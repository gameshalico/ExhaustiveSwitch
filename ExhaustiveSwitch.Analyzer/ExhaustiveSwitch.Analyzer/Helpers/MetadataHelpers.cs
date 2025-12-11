using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class MetadataHelpers
    {
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
