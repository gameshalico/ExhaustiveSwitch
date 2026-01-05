using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class CodeGenerationHelpers
    {
        /// <summary>
        /// Generates a camelCase variable name from a type name.
        /// Adds @ prefix if the name conflicts with C# keywords.
        /// </summary>
        /// <param name="type">The type to generate a variable name for</param>
        /// <returns>The generated variable name (e.g., "Goblin" → "goblin", "String" → "@string")</returns>
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

            // Avoid collision with C# keywords
            if (SyntaxFacts.GetKeywordKind(result) != SyntaxKind.None)
            {
                result = "@" + result;
            }
            return result;
        }
    }
}
