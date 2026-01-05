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

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
            var enumType = typeInfo.Type as INamedTypeSymbol;

            if (enumType?.TypeKind != TypeKind.Enum)
            {
                return;
            }

            // Ignore if [Flags] attribute is present
            if (EnumAnalysisHelpers.HasFlagsAttribute(enumType, compilation))
            {
                return;
            }

            // Check for [Exhaustive] attribute
            if (!EnumAnalysisHelpers.HasAttribute(enumType, exhaustiveAttributeType))
            {
                return;
            }

            // Get all enum members
            var allMembers = EnumAnalysisHelpers.GetAllEnumMembers(enumType);

            // Collect handled members
            var handledMembers = EnumAnalysisHelpers.CollectHandledEnumMembers(
                switchStatement.Sections.SelectMany(s => s.Labels).ToList(),
                semanticModel,
                enumType);

            // Detect missing members
            var missingMembers = allMembers.Except(handledMembers);

            foreach (var missing in missingMembers)
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingMember", missing);
                properties.Add("EnumType", enumType.ToDisplayString(SimpleTypeNameFormat));
                properties.Add("EnumTypeMetadata", MetadataHelpers.GetFullMetadataName(enumType));

                var diagnostic = Diagnostic.Create(
                    Rule,
                    switchStatement.GetLocation(),
                    properties.ToImmutable(),
                    enumType.ToDisplayString(SimpleTypeNameFormat),
                    missing);
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
            var enumType = typeInfo.Type as INamedTypeSymbol;

            if (enumType?.TypeKind != TypeKind.Enum)
            {
                return;
            }

            // Ignore if [Flags] attribute is present
            if (EnumAnalysisHelpers.HasFlagsAttribute(enumType, compilation))
            {
                return;
            }

            // Check for [Exhaustive] attribute
            if (!EnumAnalysisHelpers.HasAttribute(enumType, exhaustiveAttributeType))
            {
                return;
            }

            // Get all enum members
            var allMembers = EnumAnalysisHelpers.GetAllEnumMembers(enumType);

            // Collect handled members
            var handledMembers = EnumAnalysisHelpers.CollectHandledEnumMembers(
                switchExpression.Arms.Select(a => (SyntaxNode)a.Pattern).ToList(),
                semanticModel,
                enumType);

            // Detect missing members
            var missingMembers = allMembers.Except(handledMembers);

            foreach (var missing in missingMembers)
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingMember", missing);
                properties.Add("EnumType", enumType.ToDisplayString(SimpleTypeNameFormat));
                properties.Add("EnumTypeMetadata", MetadataHelpers.GetFullMetadataName(enumType));

                var diagnostic = Diagnostic.Create(
                    Rule,
                    switchExpression.GetLocation(),
                    properties.ToImmutable(),
                    enumType.ToDisplayString(SimpleTypeNameFormat),
                    missing);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
