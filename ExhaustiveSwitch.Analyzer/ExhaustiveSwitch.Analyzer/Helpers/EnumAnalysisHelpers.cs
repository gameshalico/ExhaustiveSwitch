using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class EnumAnalysisHelpers
    {
        /// <summary>
        /// Determines whether a symbol has the specified attribute.
        /// </summary>
        public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        /// <summary>
        /// Checks whether an enum has the [Flags] attribute.
        /// </summary>
        public static bool HasFlagsAttribute(INamedTypeSymbol enumType, Compilation compilation)
        {
            var flagsAttributeType = compilation.GetTypeByMetadataName("System.FlagsAttribute");
            if (flagsAttributeType == null)
            {
                return false;
            }

            return enumType.GetAttributes()
                .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, flagsAttributeType));
        }

        /// <summary>
        /// Gets all member names of an enum.
        /// </summary>
        public static HashSet<string> GetAllEnumMembers(INamedTypeSymbol enumType)
        {
            var members = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.IsConst && f.HasConstantValue)
                .Select(f => f.Name);

            return new HashSet<string>(members);
        }

        /// <summary>
        /// Gets all member names of an enum in their definition order.
        /// </summary>
        public static List<string> GetAllEnumMembersInOrder(INamedTypeSymbol enumType)
        {
            return enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.IsConst && f.HasConstantValue)
                .Select(f => f.Name)
                .ToList();
        }

        /// <summary>
        /// Collects enum members that are handled in a switch.
        /// </summary>
        public static HashSet<string> CollectHandledEnumMembers(
            IEnumerable<SyntaxNode> patterns,
            SemanticModel semanticModel,
            INamedTypeSymbol enumType)
        {
            var handled = new HashSet<string>();

            foreach (var pattern in patterns)
            {
                switch (pattern)
                {
                    // switch statement: case GameState.Playing:
                    case CaseSwitchLabelSyntax caseLabel:
                        var memberName = ExtractEnumMemberName(caseLabel.Value, semanticModel, enumType);
                        if (memberName != null)
                        {
                            handled.Add(memberName);
                        }
                        break;

                    // switch statement: case GameState.Playing when condition:
                    case CasePatternSwitchLabelSyntax patternLabel:
                        var patternMemberName = ExtractEnumMemberFromPattern(patternLabel.Pattern, semanticModel, enumType);
                        if (patternMemberName != null)
                        {
                            handled.Add(patternMemberName);
                        }
                        break;

                    // switch expression pattern
                    case ConstantPatternSyntax constantPattern:
                        var constantMemberName = ExtractEnumMemberName(constantPattern.Expression, semanticModel, enumType);
                        if (constantMemberName != null)
                        {
                            handled.Add(constantMemberName);
                        }
                        break;
                }
            }

            return handled;
        }

        /// <summary>
        /// Extracts an enum member from a pattern.
        /// </summary>
        private static string ExtractEnumMemberFromPattern(
            PatternSyntax pattern,
            SemanticModel semanticModel,
            INamedTypeSymbol enumType)
        {
            if (pattern is ConstantPatternSyntax constantPattern)
            {
                return ExtractEnumMemberName(constantPattern.Expression, semanticModel, enumType);
            }

            return null;
        }

        /// <summary>
        /// Extracts an enum member name from an expression.
        /// </summary>
        private static string ExtractEnumMemberName(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            INamedTypeSymbol enumType)
        {
            // Get symbol information
            var symbolInfo = semanticModel.GetSymbolInfo(expression);
            if (symbolInfo.Symbol is IFieldSymbol fieldSymbol &&
                fieldSymbol.IsConst &&
                SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, enumType))
            {
                return fieldSymbol.Name;
            }

            // Reverse lookup from constant value
            var constantValue = semanticModel.GetConstantValue(expression);
            if (!constantValue.HasValue)
            {
                return null;
            }

            var member = enumType.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.IsConst &&
                                   f.HasConstantValue &&
                                   Equals(f.ConstantValue, constantValue.Value));

            return member?.Name;
        }
    }
}
