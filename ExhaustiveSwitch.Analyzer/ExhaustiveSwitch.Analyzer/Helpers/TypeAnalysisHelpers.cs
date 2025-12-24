using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class TypeAnalysisHelpers
    {
        /// <summary>
        /// シンボルが指定された属性を持つかどうかを判定します。
        /// </summary>
        /// <param name="symbol">チェック対象のシンボル</param>
        /// <param name="attributeType">属性の型</param>
        /// <returns>属性を持つ場合はtrue</returns>
        public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        /// <summary>
        /// 型または型の基底型/実装インターフェースから、最初の[Exhaustive]型を検索します。
        /// </summary>
        /// <param name="type">検索対象の型</param>
        /// <param name="exhaustiveAttributeType">ExhaustiveAttribute型</param>
        /// <returns>[Exhaustive]属性を持つ最初の型、見つからない場合はnull</returns>
        public static INamedTypeSymbol FindExhaustiveBaseType(ITypeSymbol type, INamedTypeSymbol exhaustiveAttributeType)
        {
            // 型自体をチェック
            if (type is INamedTypeSymbol namedType && HasAttribute(namedType, exhaustiveAttributeType))
            {
                return namedType;
            }

            // インターフェースをチェック
            foreach (var iface in type.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                {
                    return iface;
                }
            }

            // 基底クラスをチェック
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
        /// 型または型の基底型/実装インターフェースから、すべての[Exhaustive]型を検索します。
        /// </summary>
        /// <param name="typeSymbol">検索対象の型</param>
        /// <param name="exhaustiveAttributeType">ExhaustiveAttribute型</param>
        /// <returns>[Exhaustive]属性を持つすべての型のリスト</returns>
        public static List<INamedTypeSymbol> FindAllExhaustiveTypes(INamedTypeSymbol typeSymbol, INamedTypeSymbol exhaustiveAttributeType)
        {
            var exhaustiveTypes = new List<INamedTypeSymbol>();

            // 型自体をチェック
            if (HasAttribute(typeSymbol, exhaustiveAttributeType))
            {
                exhaustiveTypes.Add(typeSymbol);
            }

            // インターフェースをチェック
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                if (HasAttribute(iface, exhaustiveAttributeType))
                {
                    exhaustiveTypes.Add(iface);
                }
            }

            // 基底クラスをチェック
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
        /// 型が指定された基底型を実装または継承しているかを判定します。
        /// ジェネリック型の場合、型引数の一致もチェックします。
        /// </summary>
        /// <param name="typeSymbol">チェック対象の型</param>
        /// <param name="baseType">基底型またはインターフェース</param>
        /// <returns>実装/継承している場合はtrue、型自身が一致する場合もtrueを返す</returns>
        public static bool IsImplementingOrDerivedFrom(INamedTypeSymbol typeSymbol, INamedTypeSymbol baseType)
        {
            // 型自身が一致するケース
            if (SymbolEqualityComparer.Default.Equals(typeSymbol, baseType))
            {
                return true;
            }

            // ジェネリック型の場合、型定義と型引数を両方チェック
            if (baseType.IsGenericType)
            {
                // インターフェースの実装をチェック
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

                // 基底クラスの継承をチェック
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

            // 非ジェネリック型の場合の既存の処理
            // インターフェースの実装をチェック
            if (baseType.TypeKind == TypeKind.Interface)
            {
                return typeSymbol.AllInterfaces.Contains(baseType, SymbolEqualityComparer.Default);
            }

            // 基底クラスの継承をチェック
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
        /// ジェネリック型が一致するかをチェックします。
        /// 型定義（OriginalDefinition）と型引数の両方をチェックします。
        /// </summary>
        private static bool IsGenericTypeMatch(INamedTypeSymbol type1, INamedTypeSymbol type2)
        {
            // 型定義が一致するかチェック
            if (!SymbolEqualityComparer.Default.Equals(type1.OriginalDefinition, type2.OriginalDefinition))
            {
                return false;
            }

            // 型引数の数が一致するかチェック
            if (type1.TypeArguments.Length != type2.TypeArguments.Length)
            {
                return false;
            }

            // 各型引数が一致するかチェック
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
        /// ジェネリック型のCase型が、指定された構築型に一致するかをチェックします。
        /// 例: Success<T> と Success<int> の場合、型引数を適用して一致するかを判定
        /// </summary>
        /// <param name="caseType">Case属性が付いたジェネリック型（例: Success<T>）</param>
        /// <param name="constructedType">具体的な型引数を持つ型（例: Success<int>）</param>
        /// <returns>一致する場合はtrue</returns>
        public static bool IsGenericCaseMatch(INamedTypeSymbol caseType, INamedTypeSymbol constructedType)
        {
            // 型定義が一致しない場合は不一致
            if (!SymbolEqualityComparer.Default.Equals(caseType.OriginalDefinition, constructedType.OriginalDefinition))
            {
                return false;
            }

            // 型引数の数が一致しない場合は不一致
            if (caseType.TypeArguments.Length != constructedType.TypeArguments.Length)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// switchパターンから型情報を抽出します。
        /// </summary>
        /// <param name="pattern">パターン構文ノード</param>
        /// <param name="semanticModel">セマンティックモデル</param>
        /// <returns>抽出された型、取得できない場合はnull</returns>
        public static INamedTypeSymbol ExtractTypeFromPattern(SyntaxNode pattern, SemanticModel semanticModel)
        {
            switch (pattern)
            {
                // switch文: case Goblin g when ...:
                case CasePatternSwitchLabelSyntax casePatternLabel:
                    return ExtractTypeFromPatternSyntax(casePatternLabel.Pattern, semanticModel);

                // switch文の型名のみのパターン: case Goblin:
                // これはCaseSwitchLabelSyntax（定数パターン用）として解析されるが、
                // Valueが型名の場合は型パターンとして扱う
                case CaseSwitchLabelSyntax caseLabel:
                    if (caseLabel.Value != null)
                    {
                        var typeInfo = semanticModel.GetTypeInfo(caseLabel.Value);
                        // Valueが型を表す場合（Type != null かつ ConvertedTypeが型そのもの）
                        if (typeInfo.Type != null && typeInfo.ConvertedType != null &&
                            SymbolEqualityComparer.Default.Equals(typeInfo.Type, typeInfo.ConvertedType))
                        {
                            return typeInfo.Type as INamedTypeSymbol;
                        }
                    }
                    return null;

                // switch式または直接PatternSyntax: Goblin g => ... や Goblin => ...
                case PatternSyntax patternSyntax:
                    return ExtractTypeFromPatternSyntax(patternSyntax, semanticModel);
            }

            return null;
        }

        /// <summary>
        /// パターン構文から型情報を抽出します。
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
                    // switch式での型名のみのパターン（Goblin =>）は ConstantPatternSyntax として解析される
                    if (constantPattern.Expression != null)
                    {
                        var constantTypeInfo = semanticModel.GetTypeInfo(constantPattern.Expression);
                        // Expressionが型を表す場合
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
