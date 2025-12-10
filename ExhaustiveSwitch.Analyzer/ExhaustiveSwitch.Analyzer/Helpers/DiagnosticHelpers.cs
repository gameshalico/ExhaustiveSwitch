using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    /// <summary>
    /// 診断情報の取得を行うヘルパーメソッド
    /// </summary>
    internal static class DiagnosticHelpers
    {
        /// <summary>
        /// 診断から不足している型のメタデータ名を取得し、型シンボルを返す
        /// </summary>
        public static INamedTypeSymbol GetMissingTypeFromDiagnostic(Diagnostic diagnostic, Compilation compilation)
        {
            if (diagnostic.Properties.TryGetValue("MissingTypeMetadata", out var metadataName) && !string.IsNullOrEmpty(metadataName))
            {
                return compilation.GetTypeByMetadataName(metadataName);
            }

            return null;
        }

        /// <summary>
        /// 診断から不足している型の表示名を取得
        /// </summary>
        public static string GetMissingTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            diagnostic.Properties.TryGetValue("MissingType", out var typeName);
            return typeName;
        }
    }
}
