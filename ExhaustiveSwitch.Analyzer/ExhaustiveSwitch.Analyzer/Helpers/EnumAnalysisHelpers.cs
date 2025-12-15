using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExhaustiveSwitch.Analyzer
{
    internal static class EnumAnalysisHelpers
    {
        /// <summary>
        /// シンボルが指定された属性を持つかどうかを判定します。
        /// </summary>
        public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeType)
        {
            return symbol.GetAttributes().Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
        }

        /// <summary>
        /// enumが[Flags]属性を持つかチェック
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
        /// enumのすべてのメンバー名を取得
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
        /// switchで処理されているenumメンバーを収集
        /// </summary>
        public static HashSet<string> CollectHandledEnumMembers(
            IReadOnlyList<SyntaxNode> patterns,
            SemanticModel semanticModel,
            INamedTypeSymbol enumType)
        {
            var handled = new HashSet<string>();

            foreach (var pattern in patterns)
            {
                switch (pattern)
                {
                    // switch文: case GameState.Playing:
                    case CaseSwitchLabelSyntax caseLabel:
                        var memberName = ExtractEnumMemberName(caseLabel.Value, semanticModel, enumType);
                        if (memberName != null)
                        {
                            handled.Add(memberName);
                        }
                        break;

                    // switch文: case GameState.Playing when condition:
                    case CasePatternSwitchLabelSyntax patternLabel:
                        var patternMemberName = ExtractEnumMemberFromPattern(patternLabel.Pattern, semanticModel, enumType);
                        if (patternMemberName != null)
                        {
                            handled.Add(patternMemberName);
                        }
                        break;

                    // switch式のパターン
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
        /// パターンからenumメンバーを抽出
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
        /// 式からenumメンバー名を抽出
        /// </summary>
        private static string ExtractEnumMemberName(
            ExpressionSyntax expression,
            SemanticModel semanticModel,
            INamedTypeSymbol enumType)
        {
            // シンボル情報を取得
            var symbolInfo = semanticModel.GetSymbolInfo(expression);
            if (symbolInfo.Symbol is IFieldSymbol fieldSymbol &&
                fieldSymbol.IsConst &&
                SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, enumType))
            {
                return fieldSymbol.Name;
            }

            // 定数値から逆引き
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
