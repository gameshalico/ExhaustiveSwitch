using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class DiagnosticHelpers
    {
        /// <summary>
        /// 診断情報から不足している型のシンボルを取得します。
        /// </summary>
        /// <param name="diagnostic">診断情報</param>
        /// <param name="compilation">コンパイル情報</param>
        /// <returns>不足している型のシンボル、取得できない場合はnull</returns>
        public static INamedTypeSymbol GetMissingTypeFromDiagnostic(Diagnostic diagnostic, Compilation compilation)
        {
            if (diagnostic.Properties.TryGetValue("MissingTypeMetadata", out var metadataName) && !string.IsNullOrEmpty(metadataName))
            {
                return compilation.GetTypeByMetadataName(metadataName);
            }

            return null;
        }

        /// <summary>
        /// 診断情報から不足している型の表示名を取得します。
        /// </summary>
        /// <param name="diagnostic">診断情報</param>
        /// <returns>不足している型の表示名</returns>
        public static string GetMissingTypeNameFromDiagnostic(Diagnostic diagnostic)
        {
            diagnostic.Properties.TryGetValue("MissingType", out var typeName);
            return typeName;
        }
    }
}
