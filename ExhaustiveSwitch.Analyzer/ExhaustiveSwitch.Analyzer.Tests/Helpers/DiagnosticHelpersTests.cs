using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Helpers
{
    public class DiagnosticHelpersTests
    {
        /// <summary>
        /// Get metadata name of missing type from diagnostic properties
        /// </summary>
        [Fact]
        public void GetMissingTypeFromDiagnostic_WithMetadata_ReturnsTypeSymbol()
        {
            var code = @"
public class MyClass { }
";
            var compilation = CreateCompilation(code);
            var diagnostic = CreateDiagnostic("MyClass", "MyClass");

            var result = DiagnosticHelpers.GetMissingTypeFromDiagnostic(diagnostic, compilation);

            Assert.NotNull(result);
            Assert.Equal("MyClass", result.Name);
        }

        /// <summary>
        /// When no metadata, returns null
        /// </summary>
        [Fact]
        public void GetMissingTypeFromDiagnostic_WithoutMetadata_ReturnsNull()
        {
            var code = @"
public class MyClass { }
";
            var compilation = CreateCompilation(code);
            var diagnostic = CreateDiagnostic(null!, "MyClass");

            var result = DiagnosticHelpers.GetMissingTypeFromDiagnostic(diagnostic, compilation);

            Assert.Null(result);
        }

        /// <summary>
        /// When metadata name is empty, returns null
        /// </summary>
        [Fact]
        public void GetMissingTypeFromDiagnostic_EmptyMetadata_ReturnsNull()
        {
            var code = @"
public class MyClass { }
";
            var compilation = CreateCompilation(code);
            var diagnostic = CreateDiagnostic("", "MyClass");

            var result = DiagnosticHelpers.GetMissingTypeFromDiagnostic(diagnostic, compilation);

            Assert.Null(result);
        }

        /// <summary>
        /// When metadata name for non-existent type, returns null
        /// </summary>
        [Fact]
        public void GetMissingTypeFromDiagnostic_NonExistentType_ReturnsNull()
        {
            var code = @"
public class MyClass { }
";
            var compilation = CreateCompilation(code);
            var diagnostic = CreateDiagnostic("NonExistentClass", "NonExistentClass");

            var result = DiagnosticHelpers.GetMissingTypeFromDiagnostic(diagnostic, compilation);

            Assert.Null(result);
        }

        /// <summary>
        /// Get display name of missing type from diagnostic
        /// </summary>
        [Fact]
        public void GetMissingTypeNameFromDiagnostic_WithTypeName_ReturnsName()
        {
            var diagnostic = CreateDiagnostic("MyClass", "MyClass");

            var result = DiagnosticHelpers.GetMissingTypeNameFromDiagnostic(diagnostic);

            Assert.Equal("MyClass", result);
        }

        /// <summary>
        /// When no type name, returns null
        /// </summary>
        [Fact]
        public void GetMissingTypeNameFromDiagnostic_WithoutTypeName_ReturnsNull()
        {
            var diagnostic = CreateDiagnostic("MyClass", null!);

            var result = DiagnosticHelpers.GetMissingTypeNameFromDiagnostic(diagnostic);

            Assert.Null(result);
        }

        /// <summary>
        /// When diagnostic with empty properties, returns null
        /// </summary>
        [Fact]
        public void GetMissingTypeNameFromDiagnostic_EmptyProperties_ReturnsNull()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText("class Test {}");
            var diagnostic = Diagnostic.Create(
                new DiagnosticDescriptor("TEST001", "Test", "Test message", "Test", DiagnosticSeverity.Error, true),
                Location.None,
                ImmutableDictionary<string, string>.Empty!);

            var result = DiagnosticHelpers.GetMissingTypeNameFromDiagnostic(diagnostic);

            Assert.Null(result);
        }

        private static Compilation CreateCompilation(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation;
        }

        private static Diagnostic CreateDiagnostic(string? metadataName, string? typeName)
        {
            var properties = ImmutableDictionary<string, string?>.Empty;

            if (metadataName != null)
            {
                properties = properties.Add("MissingTypeMetadata", metadataName);
            }

            if (typeName != null)
            {
                properties = properties.Add("MissingType", typeName);
            }

            return Diagnostic.Create(
                new DiagnosticDescriptor("TEST001", "Test", "Test message", "Test", DiagnosticSeverity.Error, true),
                Location.None,
                properties);
        }
    }
}
