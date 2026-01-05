using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class MetadataHelpers
    {
        /// <summary>
        /// Gets the full metadata name of a type.
        /// For generic types, returns a format that includes arity (e.g., `1).
        /// For nested types, joins with "+".
        /// Example: "Namespace.OuterClass+InnerClass`1"
        /// </summary>
        /// <param name="type">The target type symbol</param>
        /// <returns>The full metadata name</returns>
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
                // MetadataName is in the format "TypeName`1" for generic types
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
        /// Gets the full name of a namespace.
        /// Returns an empty string for the global namespace.
        /// </summary>
        /// <param name="namespaceSymbol">The namespace symbol</param>
        /// <returns>The full namespace name (separated by ".")</returns>
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
