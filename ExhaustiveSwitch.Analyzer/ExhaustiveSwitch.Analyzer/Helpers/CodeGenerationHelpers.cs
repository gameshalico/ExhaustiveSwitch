using Microsoft.CodeAnalysis;

namespace ExhaustiveSwitch.Analyzer
{
    /// <summary>
    /// コード生成に関するヘルパーメソッド
    /// </summary>
    internal static class CodeGenerationHelpers
    {
        /// <summary>
        /// 型名から変数名を生成（先頭を小文字に変換）
        /// </summary>
        public static string GetVariableName(INamedTypeSymbol type)
        {
            var name = type.Name;
            if (string.IsNullOrEmpty(name))
            {
                return "value";
            }

            // 最初の文字を小文字に変換
            return char.ToLower(name[0]) + name.Substring(1);
        }
    }
}
