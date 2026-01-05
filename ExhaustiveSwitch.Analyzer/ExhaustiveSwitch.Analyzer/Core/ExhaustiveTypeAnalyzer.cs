using System;
using System.Collections.Concurrent;
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
    public class ExhaustiveTypeAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "EXH0001";
        private const string OrphanCaseDiagnosticId = "EXH0002";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
            nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(
            nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: Description);

        private static readonly LocalizableString OrphanCaseTitle = new LocalizableResourceString(
            nameof(Resources.OrphanCaseTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString OrphanCaseMessageFormat = new LocalizableResourceString(
            nameof(Resources.OrphanCaseMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString OrphanCaseDescription = new LocalizableResourceString(
            nameof(Resources.OrphanCaseDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor OrphanCaseRule = new DiagnosticDescriptor(
            OrphanCaseDiagnosticId,
            OrphanCaseTitle,
            OrphanCaseMessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: OrphanCaseDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, OrphanCaseRule);

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
                var caseAttributeType = compilationContext.Compilation.GetTypeByMetadataName(
                    "ExhaustiveSwitch.CaseAttribute");

                if (exhaustiveAttributeType == null || caseAttributeType == null)
                {
                    return;
                }

                var lazyHierarchyInfoMap = new Lazy<ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo>>(
                    () => BuildInheritanceMap(compilationContext.Compilation, exhaustiveAttributeType, caseAttributeType), true);

                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchStatement(nodeContext, exhaustiveAttributeType, lazyHierarchyInfoMap.Value),
                    SyntaxKind.SwitchStatement);

                compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                    AnalyzeSwitchExpression(nodeContext, exhaustiveAttributeType, lazyHierarchyInfoMap.Value),
                    SyntaxKind.SwitchExpression);

                compilationContext.RegisterSymbolAction(symbolContext =>
                    AnalyzeTypeSymbol(symbolContext, exhaustiveAttributeType, caseAttributeType),
                    SymbolKind.NamedType);
            });
        }
        
        /// <summary>
        /// Scans all types in the compilation unit and builds a relationship map between [Exhaustive] parents and [Case] children.
        /// </summary>
        private ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> BuildInheritanceMap(
            Compilation compilation,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType)
        {
            // Key: [Exhaustive] parent class/interface
            // Value: List of child classes with [Case] attribute that inherit/implement it
            var map = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            void ProcessType(INamedTypeSymbol typeSymbol)
            {
                if (TypeAnalysisHelpers.HasAttribute(typeSymbol, caseAttributeType))
                {
                    var exhaustiveBases = TypeAnalysisHelpers.FindAllExhaustiveTypes(typeSymbol, exhaustiveAttributeType);

                    foreach (var exhaustiveBase in exhaustiveBases)
                    {
                        // For generic types, use the type definition (OriginalDefinition) as the key
                        var exhaustiveBaseKey = exhaustiveBase.IsGenericType ? exhaustiveBase.OriginalDefinition : exhaustiveBase;

                        if (!map.TryGetValue(exhaustiveBaseKey, out var children))
                        {
                            children = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                            map[exhaustiveBaseKey] = children;
                        }

                        // For Case types that are generic, store the type definition
                        var typeToAdd = typeSymbol.IsGenericType ? typeSymbol.OriginalDefinition : typeSymbol;
                        children.Add(typeToAdd);
                    }
                }

                // Recursive scan of nested types
                var nestedTypes = typeSymbol.GetTypeMembers();
                if (nestedTypes.IsEmpty)
                {
                    return;
                }

                foreach (var nested in nestedTypes)
                {
                    ProcessType(nested);
                }
            }

            void ProcessNamespace(INamespaceSymbol namespaceSymbol)
            {
                foreach (var typeMember in namespaceSymbol.GetTypeMembers())
                {
                    ProcessType(typeMember);
                }
        
                foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
                {
                    ProcessNamespace(nestedNamespace);
                }
            }

            // Scan the source code of the current project
            ProcessNamespace(compilation.GlobalNamespace);

            // Scan referenced assemblies
            var definitionAssembly = exhaustiveAttributeType.ContainingAssembly;

            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    // Skip assemblies that don't reference the assembly where [Exhaustive] attribute is defined
                    if (!ReferencesAssembly(assembly, definitionAssembly))
                    {
                        continue;
                    }

                    ProcessNamespace(assembly.GlobalNamespace);
                }
            }

            var result = new ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo>(SymbolEqualityComparer.Default);
            foreach (var kvp in map)
            {
                result[kvp.Key] = new ExhaustiveHierarchyInfo(kvp.Value);
            }
            return result;
        }

        private void AnalyzeSwitchStatement(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoMap)
        {
            var switchStatement = (SwitchStatementSyntax)context.Node;
            var switchExpression = switchStatement.Expression;

            AnalyzeSwitchConstruct(context, switchExpression, switchStatement.GetLocation(),
                switchStatement.Sections.SelectMany(s => s.Labels).ToList(),
                exhaustiveAttributeType, hierarchyInfoMap);
        }

        private void AnalyzeSwitchExpression(
            SyntaxNodeAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoMap)
        {
            var switchExpression = (SwitchExpressionSyntax)context.Node;
            var governingExpression = switchExpression.GoverningExpression;

            AnalyzeSwitchConstruct(context, governingExpression, switchExpression.GetLocation(),
                switchExpression.Arms.Select(a => a.Pattern).ToList(),
                exhaustiveAttributeType, hierarchyInfoMap);
        }

        private void AnalyzeSwitchConstruct(
            SyntaxNodeAnalysisContext context,
            ExpressionSyntax governingExpression,
            Location location,
            IReadOnlyList<SyntaxNode> patterns,
            INamedTypeSymbol exhaustiveAttributeType,
            ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoMap)
        {
            var semanticModel = context.SemanticModel;
            var typeInfo = semanticModel.GetTypeInfo(governingExpression);
            var switchedType = typeInfo.Type;

            if (switchedType == null)
            {
                return;
            }

            // Find the type with [Exhaustive] attribute
            var exhaustiveType = TypeAnalysisHelpers.FindExhaustiveBaseType(switchedType, exhaustiveAttributeType);
            if (exhaustiveType == null)
            {
                return;
            }

            // For generic types, search the map using the type definition (OriginalDefinition)
            var exhaustiveTypeKey = exhaustiveType.IsGenericType ? exhaustiveType.OriginalDefinition : exhaustiveType;

            // S_expected: Get all [Case] types corresponding to the [Exhaustive] type
            if (hierarchyInfoMap.TryGetValue(exhaustiveTypeKey, out var hierarchyInfo) == false)
            {
                return;
            }

            // For generic types, apply type arguments
            if (hierarchyInfo.IsGeneric && exhaustiveType.IsGenericType)
            {
                hierarchyInfo = hierarchyInfo.ApplyTypeArguments(exhaustiveType);
            }

            var handledCases = CollectHandledCases(patterns, semanticModel, hierarchyInfo);
            var missingCases = new HashSet<INamedTypeSymbol>(hierarchyInfo.AllCases, SymbolEqualityComparer.Default);
            missingCases.ExceptWith(handledCases);

            if (missingCases.Count == 0)
            {
                return;
            }

            // Filter the types to report among missing types
            var casesToReport = FilterAncestorsWithUnhandledDescendants(missingCases, hierarchyInfo);

            // Prepare information about all missing types (used in the first diagnostic)
            var allMissingTypesNames = string.Join(";", casesToReport.Select(t => t.ToDisplayString(SimpleTypeNameFormat)));
            var allMissingTypesMetadata = string.Join(";", casesToReport.Select(MetadataHelpers.GetFullMetadataName));

            bool isFirst = true;
            foreach (var missingCase in casesToReport)
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add("MissingType", missingCase.ToDisplayString(SimpleTypeNameFormat));
                properties.Add("MissingTypeMetadata", MetadataHelpers.GetFullMetadataName(missingCase));

                // Add information about all missing types only for the first diagnostic
                if (isFirst)
                {
                    properties.Add("IsFirstDiagnostic", "true");
                    properties.Add("AllMissingTypes", allMissingTypesNames);
                    properties.Add("AllMissingTypesMetadata", allMissingTypesMetadata);
                    isFirst = false;
                }
                else
                {
                    properties.Add("IsFirstDiagnostic", "false");
                }

                var diagnostic = Diagnostic.Create(
                    Rule,
                    location,
                    properties.ToImmutable(),
                    exhaustiveType.ToDisplayString(SimpleTypeNameFormat),
                    missingCase.ToDisplayString(SimpleTypeNameFormat));
                context.ReportDiagnostic(diagnostic);
            }
        }
        

        /// <summary>
        /// Filters types to report among missing types.
        /// Excludes ancestor types of other missing types (since ancestor types are covered when all their descendants are handled).
        /// </summary>
        private static List<INamedTypeSymbol> FilterAncestorsWithUnhandledDescendants(
            HashSet<INamedTypeSymbol> missingCases,
            ExhaustiveHierarchyInfo hierarchyInfo)
        {
            var casesToReport = new List<INamedTypeSymbol>();

            foreach (var missingCase in missingCases)
            {
                if (hierarchyInfo.DirectChildrenMap.TryGetValue(missingCase, out var children))
                {
                    // For concrete classes, no need to check descendants
                    if (missingCase.TypeKind == TypeKind.Class && !missingCase.IsAbstract)
                    {
                        casesToReport.Add(missingCase);
                        continue;
                    }

                    // Check if there are any missing children
                    bool hasMissingChild = false;
                    foreach (var child in children)
                    {
                        if (missingCases.Contains(child))
                        {
                            hasMissingChild = true;
                            break;
                        }
                    }
                    
                    if (!hasMissingChild)
                    {
                        casesToReport.Add(missingCase);
                    }
                }
                else
                {
                    casesToReport.Add(missingCase);
                }
            }

            return casesToReport;
        }
        
        
        private HashSet<INamedTypeSymbol> CollectHandledCases(
            IReadOnlyList<SyntaxNode> patterns,
            SemanticModel semanticModel,
            ExhaustiveHierarchyInfo hierarchyInfo)
        {
            var explicitlyHandled = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var pattern in patterns)
            {
                var typeSymbol = TypeAnalysisHelpers.ExtractTypeFromPattern(pattern, semanticModel);
                if (typeSymbol == null)
                {
                    continue;
                }

                // If the pattern type is a [Case] type, add it directly
                if (hierarchyInfo.AllCases.Contains(typeSymbol))
                {
                    explicitlyHandled.Add(typeSymbol);
                }
                else
                {
                    // If the pattern type is not a [Case] type, add all [Case] types that implement/inherit from it
                    foreach (var caseType in hierarchyInfo.AllCases)
                    {
                        if (TypeAnalysisHelpers.IsImplementingOrDerivedFrom(caseType, typeSymbol))
                        {
                            explicitlyHandled.Add(caseType);
                        }
                    }
                }
            }

            var finalHandledCases = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // Memoization dictionary (true: covered, false: not covered)
            var memo = new Dictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);

            foreach (var candidate in hierarchyInfo.AllCases)
            {
                if (CheckCoverageRecursive(candidate, explicitlyHandled, hierarchyInfo, memo))
                {
                    finalHandledCases.Add(candidate);
                }
            }

            return finalHandledCases;
        }

        /// <summary>
        /// Recursively determines coverage (with memoization).
        /// </summary>
        private bool CheckCoverageRecursive(
            INamedTypeSymbol type,
            HashSet<INamedTypeSymbol> explicitlyHandled,
            ExhaustiveHierarchyInfo hierarchyInfo,
            Dictionary<INamedTypeSymbol, bool> memo)
        {
            if (memo.TryGetValue(type, out var cachedResult))
            {
                return cachedResult;
            }

            // Set to false temporarily to prevent circular references (should not occur in a DAG, but just in case)
            memo[type] = false;

            if (explicitlyHandled.Contains(type))
            {
                memo[type] = true;
                return true;
            }

            // If any ancestor is explicitly handled, consider this type as covered
            if (IsAnyAncestorExplicitlyHandled(type, explicitlyHandled, hierarchyInfo))
            {
                memo[type] = true;
                return true;
            }

            // Only abstract/sealed/interface types can be covered by child class coverage
            if (hierarchyInfo.DirectChildrenMap.TryGetValue(type, out var children) && children.Count > 0)
            {
                bool canBeCoveredByChildren = type.IsAbstract || type.IsSealed || type.TypeKind == TypeKind.Interface;

                if (canBeCoveredByChildren)
                {
                    bool allChildrenCovered = true;
                    foreach (var child in children)
                    {
                        if (!CheckCoverageRecursive(child, explicitlyHandled, hierarchyInfo, memo))
                        {
                            allChildrenCovered = false;
                            break;
                        }
                    }

                    if (allChildrenCovered)
                    {
                        memo[type] = true;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if any ancestor is explicitly handled by traversing up the hierarchy.
        /// </summary>
        private bool IsAnyAncestorExplicitlyHandled(
            INamedTypeSymbol type,
            HashSet<INamedTypeSymbol> explicitlyHandled,
            ExhaustiveHierarchyInfo hierarchyInfo)
        {
            var queue = new Queue<INamedTypeSymbol>();
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            if (hierarchyInfo.DirectParentsMap.TryGetValue(type, out var parents))
            {
                foreach (var p in parents)
                {
                    queue.Enqueue(p);
                }
            }
        
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (explicitlyHandled.Contains(current))
                {
                    return true;
                }

                if (hierarchyInfo.DirectParentsMap.TryGetValue(current, out var grandParents))
                {
                    foreach (var gp in grandParents)
                    {
                        queue.Enqueue(gp);
                    }
                }
            }

            return false;
        }

        private bool ReferencesAssembly(IAssemblySymbol assembly, IAssemblySymbol targetAssembly)
        {
            if (SymbolEqualityComparer.Default.Equals(assembly, targetAssembly))
            {
                return true;
            }

            // Check only direct references
            var targetName = targetAssembly.Identity.Name;
            foreach (var module in assembly.Modules)
            {
                foreach (var refAssembly in module.ReferencedAssemblies)
                {
                    if (refAssembly.Name == targetName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Reports a warning if a type symbol has the [Case] attribute but does not inherit/implement an [Exhaustive] type.
        /// </summary>
        private void AnalyzeTypeSymbol(
            SymbolAnalysisContext context,
            INamedTypeSymbol exhaustiveAttributeType,
            INamedTypeSymbol caseAttributeType)
        {
            var typeSymbol = (INamedTypeSymbol)context.Symbol;

            // Check if it has [Case] attribute
            if (!TypeAnalysisHelpers.HasAttribute(typeSymbol, caseAttributeType))
            {
                return;
            }

            // Check if there is an [Exhaustive] type in the hierarchy
            var exhaustiveBases = TypeAnalysisHelpers.FindAllExhaustiveTypes(typeSymbol, exhaustiveAttributeType);
            if (exhaustiveBases.Count == 0)
            {
                // Report a warning if [Case] attribute exists but no [Exhaustive] type is found
                var diagnostic = Diagnostic.Create(
                    OrphanCaseRule,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.ToDisplayString(SimpleTypeNameFormat));
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
