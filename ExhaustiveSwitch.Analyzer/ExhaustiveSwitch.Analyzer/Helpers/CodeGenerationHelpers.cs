using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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

            // 1文字の型名の場合
            if (name.Length == 1)
            {
                return name.ToLower();
            }

            // 最初の文字を小文字に変換
            var result = char.ToLower(name[0]) + name.Substring(1);
            
            // 予約後の場合は@を付与
            if (SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None)
            {
                result = "@" + result;
            }
            return result;
        }
    }
}
