using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class DiagnosticHelpers
    {
        public static INamedTypeSymbol GetMissingTypeFromDiagnostic(Diagnostic diagnostic, Compilation compilation)
        {
            if (diagnostic.Properties.TryGetValue("MissingTypeMetadata", out var metadataName) && !string.IsNullOrEmpty(metadataName))
            {
                return compilation.GetTypeByMetadataName(metadataName);
            }

            return null;
        }

        public static string GetMissingTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            diagnostic.Properties.TryGetValue("MissingType", out var typeName);
            return typeName;
        }
    }
}
