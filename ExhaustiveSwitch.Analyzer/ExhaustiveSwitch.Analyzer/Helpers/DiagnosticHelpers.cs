using System.Collections.Generic;
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

        /// <summary>
        /// 診断情報からすべての不足している型のシンボルを取得します。
        /// </summary>
        /// <param name="diagnostic">診断情報</param>
        /// <param name="compilation">コンパイル情報</param>
        /// <returns>すべての不足している型のシンボルのリスト</returns>
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
