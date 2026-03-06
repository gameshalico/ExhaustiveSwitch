using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ExhaustiveSwitch.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExhaustiveEnumAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "EXH1001";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.EnumAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
            nameof(Resources.EnumAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(
            nameof(Resources.EnumAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        private const string NullableDiagnosticId = "EXH1002";

        private static readonly LocalizableString NullableTitle = new LocalizableResourceString(
            nameof(Resources.NullableEnumAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString NullableMessageFormat = new LocalizableResourceString(
            nameof(Resources.NullableEnumAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString NullableDescription = new LocalizableResourceString(
            nameof(Resources.NullableEnumAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor NullableRule = new DiagnosticDescriptor(
            NullableDiagnosticId,
            NullableTitle,
            NullableMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: NullableDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, NullableRule);

        private static readonly SymbolDisplayFormat SimpleTypeNameFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var exhaustiveAttributeType = compilationContext.Compilation.GetTypeByMetadataName(
                    "ExhaustiveSwitch.ExhaustiveAttribute");

                if (exhaustiveAttributeType == null)
                {
                    return;
                }

                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchStatement(nodeContext, exhaustiveAttributeType, compilationContext.Compilation),
                    SyntaxKind.SwitchStatement);

                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchExpression(nodeContext, exhaustiveAttributeType, compilationContext.Compilation),
                    SyntaxKind.SwitchExpression);
            });
        }

        private void AnalyzeSwitchStatement(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            Compilation compilation)
        {
            var switchStatement = (SwitchStatementSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var typeInfo = semanticModel.GetTypeInfo(switchStatement.Expression);
            var rawType = typeInfo.Type;
            if (rawType == null) return;

            // Ignore if the type is not an enum
            var enumType = TypeAnalysisHelpers.UnwrapNullable(rawType, compilation) as INamedTypeSymbol;
            if (enumType?.TypeKind != TypeKind.Enum)
            {
                return;
            }

            // Ignore if the [Flags] attribute is present
            if (EnumAnalysisHelpers.HasFlagsAttribute(enumType, compilation))
            {
                return;
            }

            // Check for the [Exhaustive] attribute
            if (!EnumAnalysisHelpers.HasAttribute(enumType, exhaustiveAttributeType))
            {
                return;
            }

            // Collect handled members
            var handledMembers = EnumAnalysisHelpers.CollectHandledEnumMembers(
                switchStatement.Sections.SelectMany(s => s.Labels),
                semanticModel,
                enumType);

            // Detect missing members
            var missingMembers = EnumAnalysisHelpers.GetAllEnumMembers(enumType);
            missingMembers.ExceptWith(handledMembers);

            // Check for missing members
            foreach (var missing in missingMembers)
            {
                var simpleTypeNameFormat = enumType.ToDisplayString(SimpleTypeNameFormat);
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingMember", missing);
                properties.Add("EnumType", simpleTypeNameFormat);
                properties.Add("EnumTypeMetadata", MetadataHelpers.GetFullMetadataName(enumType));

                var diagnostic = Diagnostic.Create(
                    Rule,
                    switchStatement.GetLocation(),
                    properties.ToImmutable(),
                    simpleTypeNameFormat,
                    missing);
                context.ReportDiagnostic(diagnostic);
            }

            // Check for a missing null case when the type is nullable
            var isNullable = TypeAnalysisHelpers.IsNullableValueType(rawType, compilation);
            var labels = switchStatement.Sections.SelectMany(s => s.Labels);
            if (isNullable && !TypeAnalysisHelpers.HasNullPattern(labels))
            {
                var simpleTypeNameFormat = enumType.ToDisplayString(SimpleTypeNameFormat);
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingMember", "null");
                properties.Add("EnumType", simpleTypeNameFormat);
                properties.Add("EnumTypeMetadata", MetadataHelpers.GetFullMetadataName(enumType));

                var diagnostic = Diagnostic.Create(
                    NullableRule,
                    switchStatement.GetLocation(),
                    properties.ToImmutable(),
                    simpleTypeNameFormat);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void AnalyzeSwitchExpression(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            Compilation compilation)
        {
            var switchExpression = (SwitchExpressionSyntax)context.Node;
            var semanticModel = context.SemanticModel;

            var typeInfo = semanticModel.GetTypeInfo(switchExpression.GoverningExpression);
            var rawType = typeInfo.Type;
            if (rawType == null) return;

            // Ignore if the type is not an enum
            var enumType = TypeAnalysisHelpers.UnwrapNullable(rawType, compilation) as INamedTypeSymbol;
            if (enumType?.TypeKind != TypeKind.Enum)
            {
                return;
            }

            // Ignore if the [Flags] attribute is present
            if (EnumAnalysisHelpers.HasFlagsAttribute(enumType, compilation))
            {
                return;
            }

            // Check for the [Exhaustive] attribute
            if (!EnumAnalysisHelpers.HasAttribute(enumType, exhaustiveAttributeType))
            {
                return;
            }

            // Collect handled members
            var handledMembers = EnumAnalysisHelpers.CollectHandledEnumMembers(
                switchExpression.Arms.Select(a => (SyntaxNode)a.Pattern),
                semanticModel,
                enumType);

            // Detect missing members
            var missingMembers = EnumAnalysisHelpers.GetAllEnumMembers(enumType);
            missingMembers.ExceptWith(handledMembers);

            // Check for missing members
            foreach (var missing in missingMembers)
            {
                var simpleTypeNameFormat = enumType.ToDisplayString(SimpleTypeNameFormat);
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingMember", missing);
                properties.Add("EnumType", simpleTypeNameFormat);
                properties.Add("EnumTypeMetadata", MetadataHelpers.GetFullMetadataName(enumType));

                var diagnostic = Diagnostic.Create(
                    Rule,
                    switchExpression.GetLocation(),
                    properties.ToImmutable(),
                    simpleTypeNameFormat,
                    missing);
                context.ReportDiagnostic(diagnostic);
            }

            // Check for a missing null case when the type is nullable
            var isNullable = TypeAnalysisHelpers.IsNullableValueType(rawType, compilation);
            var armPatterns = switchExpression.Arms.Select(a => (SyntaxNode)a.Pattern);
            if (isNullable && !TypeAnalysisHelpers.HasNullPattern(armPatterns))
            {
                var simpleTypeNameFormat = enumType.ToDisplayString(SimpleTypeNameFormat);
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingMember", "null");
                properties.Add("EnumType", simpleTypeNameFormat);
                properties.Add("EnumTypeMetadata", MetadataHelpers.GetFullMetadataName(enumType));

                var diagnostic = Diagnostic.Create(
                    NullableRule,
                    switchExpression.GetLocation(),
                    properties.ToImmutable(),
                    simpleTypeNameFormat);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
