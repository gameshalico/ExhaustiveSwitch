using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class TypeAnalysisHelpers
    {
        /// <summary>
        /// Unwraps Nullable&lt;T&gt; to its underlying type T.
        /// Returns the original type if not nullable.
        /// </summary>
        public static ITypeSymbol UnwrapNullable(ITypeSymbol type, Compilation compilation)
        {
            var nullableType = compilation.GetTypeByMetadataName("System.Nullable`1");
            if (nullableType != null &&
                type is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.Equals(nullableType, SymbolEqualityComparer.Default) &&
                namedType.TypeArguments.Length == 1)
            {
                return namedType.TypeArguments[0];
            }
            return type;
        }

        /// <summary>
        /// Checks whether the type is Nullable&lt;T&gt;.
        /// </summary>
        public static bool IsNullableValueType(ITypeSymbol type, Compilation compilation)
        {
            var nullableType = compilation.GetTypeByMetadataName("System.Nullable`1");
            return nullableType != null &&
                type is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.Equals(nullableType, SymbolEqualityComparer.Default);
        }

        /// <summary>
        /// Checks whether any pattern in the list handles null.
        /// </summary>
        public static bool HasNullPattern(IEnumerable<SyntaxNode> patterns)
        {
            foreach (var pattern in patterns)
            {
                switch (pattern)
                {
                    case DefaultSwitchLabelSyntax _:
                        return true;
                    case CaseSwitchLabelSyntax caseLabel:
                        if (caseLabel.Value is LiteralExpressionSyntax literal &&
                            literal.IsKind(SyntaxKind.NullLiteralExpression))
                            return true;
                        break;
                    case CasePatternSwitchLabelSyntax patternLabel:
                        if (patternLabel.Pattern is ConstantPatternSyntax constPattern &&
                            constPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression))
                            return true;
                        break;
                    case DiscardPatternSyntax _:
                        return true;
                    case ConstantPatternSyntax constantPattern:
                        if (constantPattern.Expression.IsKind(SyntaxKind.NullLiteralExpression))
                            return true;
                        break;
                }
            }
            return false;
        }

        /// <summary>
        /// Determines whether a symbol has the specified attribute.
        /// </summary>
        /// <param name="symbol">The symbol to check</param>
        /// <param name="attributeType">The attribute type</param>
        /// <returns>True if the symbol has the attribute</returns>
        public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        /// <summary>
        /// Finds the first [Exhaustive] type from the type or its base types/implemented interfaces.
        /// </summary>
        /// <param name="type">The type to search</param>
        /// <param name="exhaustiveAttributeType">The ExhaustiveAttribute type</param>
        /// <returns>The first type with [Exhaustive] attribute, or null if not found</returns>
        public static INamedTypeSymbol FindExhaustiveBaseType(ITypeSymbol type, INamedTypeSymbol exhaustiveAttributeType)
        {
            // Check the type itself
            if (type is INamedTypeSymbol namedType && HasAttribute(namedType, exhaustiveAttributeType))
            {
                return namedType;
            }

            // Check interfaces
            foreach (var iface in type.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                {
                    return iface;
                }
            }

            // Check base classes
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (HasAttribute(baseType, exhaustiveAttributeType))
                {
                    return baseType;
                }

                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Finds all [Exhaustive] types from the type or its base types/implemented interfaces.
        /// </summary>
        /// <param name="typeSymbol">The type to search</param>
        /// <param name="exhaustiveAttributeType">The ExhaustiveAttribute type</param>
        /// <returns>A list of all types with [Exhaustive] attribute</returns>
        public static List<INamedTypeSymbol> FindAllExhaustiveTypes(INamedTypeSymbol typeSymbol, INamedTypeSymbol exhaustiveAttributeType)
        {
            var exhaustiveTypes = new List<INamedTypeSymbol>();

            // Check the type itself
            if (HasAttribute(typeSymbol, exhaustiveAttributeType))
            {
                exhaustiveTypes.Add(typeSymbol);
            }

            // Check interfaces
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                {
                    exhaustiveTypes.Add(iface);
                }
            }

            // Check base classes
            var baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                if (HasAttribute(baseType, exhaustiveAttributeType))
                {
                    exhaustiveTypes.Add(baseType);
                }

                baseType = baseType.BaseType;
            }

            return exhaustiveTypes;
        }

        /// <summary>
        /// Determines whether a type implements or is derived from the specified base type.
        /// For generic types, also checks type argument matching.
        /// </summary>
        /// <param name="typeSymbol">The type to check</param>
        /// <param name="baseType">The base type or interface</param>
        /// <returns>True if the type implements/inherits the base type, or if the type itself matches</returns>
        public static bool IsImplementingOrDerivedFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType)
        {
            // Case where the type itself matches
            if (SymbolEqualityComparer.Default.Equals(typeSymbol, baseType))
            {
                return true;
            }

            // For generic types, check both type definition and type arguments
            if (baseType.IsGenericType)
            {
                // Check interface implementation
                if (baseType.TypeKind == TypeKind.Interface)
                {
                    foreach (var iface in typeSymbol.AllInterfaces)
                    {
                        if (IsGenericTypeMatch(iface, baseType))
                        {
                            return true;
                        }
                    }
                }

                // Check base class inheritance
                var current = typeSymbol.BaseType;
                while (current != null)
                {
                    if (IsGenericTypeMatch(current, baseType))
                    {
                        return true;
                    }
                    current = current.BaseType;
                }

                return false;
            }

            // For non-generic types, use existing logic
            // Check interface implementation
            if (baseType.TypeKind == TypeKind.Interface)
            {
                return typeSymbol.AllInterfaces.Contains(baseType, SymbolEqualityComparer.Default);
            }

            // Check base class inheritance
            var currentType = typeSymbol.BaseType;
            while (currentType != null)
            {
                if (SymbolEqualityComparer.Default.Equals(currentType, baseType))
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Checks whether generic types match.
        /// Checks both type definition (OriginalDefinition) and type arguments.
        /// </summary>
        private static bool IsGenericTypeMatch(INamedTypeSymbol type1, INamedTypeSymbol type2)
        {
            // Check if type definitions match
            if (!SymbolEqualityComparer.Default.Equals(type1.OriginalDefinition, type2.OriginalDefinition))
            {
                return false;
            }

            // Check if number of type arguments match
            if (type1.TypeArguments.Length != type2.TypeArguments.Length)
            {
                return false;
            }

            // Check if each type argument matches
            for (int i = 0; i < type1.TypeArguments.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(type1.TypeArguments[i], type2.TypeArguments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether a generic Case type matches the specified constructed type.
        /// Example: For Success&lt;T&gt; and Success&lt;int&gt;, determines if they match when type arguments are applied.
        /// </summary>
        /// <param name="caseType">Generic type with Case attribute (e.g., Success&lt;T&gt;)</param>
        /// <param name="constructedType">Type with concrete type arguments (e.g., Success&lt;int&gt;)</param>
        /// <returns>True if they match</returns>
        public static bool IsGenericCaseMatch(INamedTypeSymbol caseType, INamedTypeSymbol constructedType)
        {
            // No match if type definitions don't match
            if (!SymbolEqualityComparer.Default.Equals(caseType.OriginalDefinition, constructedType.OriginalDefinition))
            {
                return false;
            }

            // No match if number of type arguments don't match
            if (caseType.TypeArguments.Length != constructedType.TypeArguments.Length)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Extracts type information from a switch pattern.
        /// </summary>
        /// <param name="pattern">The pattern syntax node</param>
        /// <param name="semanticModel">The semantic model</param>
        /// <returns>The extracted type, or null if not available</returns>
        public static INamedTypeSymbol ExtractTypeFromPattern(SyntaxNode pattern, SemanticModel semanticModel)
        {
            switch (pattern)
            {
                // switch statement: case Goblin g when ...:
                case CasePatternSwitchLabelSyntax casePatternLabel:
                    return ExtractTypeFromPatternSyntax(casePatternLabel.Pattern, semanticModel);

                // switch statement with type name only: case Goblin:
                // This is parsed as CaseSwitchLabelSyntax (for constant patterns),
                // but if Value is a type name, treat it as a type pattern
                case CaseSwitchLabelSyntax caseLabel:
                    if (caseLabel.Value != null)
                    {
                        var typeInfo = semanticModel.GetTypeInfo(caseLabel.Value);
                        // If Value represents a type (Type != null and ConvertedType is the type itself)
                        if (typeInfo.Type != null && typeInfo.ConvertedType != null &&
                            SymbolEqualityComparer.Default.Equals(typeInfo.Type, typeInfo.ConvertedType))
                        {
                            return typeInfo.Type as INamedTypeSymbol;
                        }
                    }
                    return null;

                // switch expression or direct PatternSyntax: Goblin g => ... or Goblin => ...
                case PatternSyntax patternSyntax:
                    return ExtractTypeFromPatternSyntax(patternSyntax, semanticModel);
            }

            return null;
        }

        /// <summary>
        /// Extracts type information from a pattern syntax.
        /// </summary>
        private static INamedTypeSymbol ExtractTypeFromPatternSyntax(PatternSyntax pattern, SemanticModel semanticModel)
        {
            switch (pattern)
            {
                case DeclarationPatternSyntax declarationPattern:
                    var typeInfo = semanticModel.GetTypeInfo(declarationPattern.Type);
                    return typeInfo.Type as INamedTypeSymbol;

                case RecursivePatternSyntax recursivePattern when recursivePattern.Type != null:
                    var recursiveTypeInfo = semanticModel.GetTypeInfo(recursivePattern.Type);
                    return recursiveTypeInfo.Type as INamedTypeSymbol;

                case TypePatternSyntax typePattern:
                    var typePatternInfo = semanticModel.GetTypeInfo(typePattern.Type);
                    return typePatternInfo.Type as INamedTypeSymbol;

                case ConstantPatternSyntax constantPattern:
                    // Type name only pattern in switch expression (Goblin =>) is parsed as ConstantPatternSyntax
                    if (constantPattern.Expression != null)
                    {
                        var constantTypeInfo = semanticModel.GetTypeInfo(constantPattern.Expression);
                        // If Expression represents a type
                        if (constantTypeInfo.Type != null && constantTypeInfo.ConvertedType != null &&
                            SymbolEqualityComparer.Default.Equals(constantTypeInfo.Type, constantTypeInfo.ConvertedType))
                        {
                            return constantTypeInfo.Type as INamedTypeSymbol;
                        }
                    }
                    return null;
            }

            return null;
        }
    }
}
