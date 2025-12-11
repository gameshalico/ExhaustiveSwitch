using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class CodeGenerationHelpers
    {
        /// <summary>
        /// 型名からcamelCase形式の変数名を生成します。
        /// C#のキーワードと衝突する場合は@プレフィックスを付けます。
        /// </summary>
        /// <param name="type">変数名を生成する型</param>
        /// <returns>生成された変数名（例: "Goblin" → "goblin", "String" → "@string"）</returns>
        public static string GetVariableName(INamedTypeSymbol type)
        {
            if (type == null)
            {
                return "value";
            }

            var name = type.Name;
            if (string.IsNullOrEmpty(name))
            {
                return "value";
            }

            if (name.Length == 1)
            {
                return name.ToLower();
            }

            var result = char.ToLower(name[0]) + name.Substring(1);

            // C#キーワードとの衝突を回避
            if (SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None)
            {
                result = "@" + result;
            }
            return result;
        }
    }
}
