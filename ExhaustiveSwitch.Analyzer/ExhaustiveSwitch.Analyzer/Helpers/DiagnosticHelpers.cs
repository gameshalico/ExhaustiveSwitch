using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class DiagnosticHelpers
    {
        /// <summary>
        /// Gets the missing type symbol from diagnostic information.
        /// </summary>
        /// <param name="diagnostic">The diagnostic information</param>
        /// <param name="compilation">The compilation information</param>
        /// <returns>The missing type symbol, or null if not available</returns>
        public static INamedTypeSymbol GetMissingTypeFromDiagnostic(Diagnostic diagnostic, Compilation compilation)
        {
            if (diagnostic.Properties.TryGetValue("MissingTypeMetadata", out var metadataName) && !string.IsNullOrEmpty(metadataName))
            {
                return compilation.GetTypeByMetadataName(metadataName);
            }

            return null;
        }

        /// <summary>
        /// Gets the display name of the missing type from diagnostic information.
        /// </summary>
        /// <param name="diagnostic">The diagnostic information</param>
        /// <returns>The display name of the missing type</returns>
        public static string GetMissingTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            diagnostic.Properties.TryGetValue("MissingType", out var typeName);
            return typeName;
        }

        /// <summary>
        /// Gets all missing type symbols from diagnostic information.
        /// </summary>
        /// <param name="diagnostic">The diagnostic information</param>
        /// <param name="compilation">The compilation information</param>
        /// <returns>A list of all missing type symbols</returns>
        public static List<INamedTypeSymbol> GetAllMissingTypesFromDiagnostic(Diagnostic diagnostic, Compilation compilation)
        {
            var result = new List<INamedTypeSymbol>();

            if (diagnostic.Properties.TryGetValue("AllMissingTypesMetadata", out var allMetadataNames) &&
                !string.IsNullOrEmpty(allMetadataNames))
            {
                var metadataNames = allMetadataNames.Split(';');
                foreach (var metadataName in metadataNames)
                {
                    if (!string.IsNullOrEmpty(metadataName))
                    {
                        var type = compilation.GetTypeByMetadataName(metadataName);
                        if (type != null)
                        {
                            result.Add(type);
                        }
                    }
                }
            }

            return result;
        }
    }
}
