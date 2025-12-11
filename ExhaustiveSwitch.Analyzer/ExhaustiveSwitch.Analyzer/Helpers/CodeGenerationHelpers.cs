using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class CodeGenerationHelpers
    {
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

            if (SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None)
            {
                result = "@" + result;
            }
            return result;
        }
    }
}
