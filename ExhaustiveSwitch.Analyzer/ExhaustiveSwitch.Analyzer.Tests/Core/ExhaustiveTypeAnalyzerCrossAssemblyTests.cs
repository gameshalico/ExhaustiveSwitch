using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// Tests for cross-assembly scenarios
    /// </summary>
    public class ExhaustiveTypeAnalyzerCrossAssemblyTests
    {
        /// <summary>
        /// Cross-assembly: detect [Case] types in referenced assembly
        /// </summary>
        [Fact]
        public async Task WhenCaseTypeInReferencedAssembly_Diagnostic()
        {
            // Referenced assembly code (library side)
            var libraryCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // Main project code
            var mainCode = @"
public class Program
{
    public void Process(IEnemy enemy)
    {
        {|#0:switch (enemy)
        {
            case Goblin g:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyCrossAssemblyAnalyzerAsync(libraryCode, mainCode, expected);
        }

        /// <summary>
        /// Cross-assembly: when all cases from referenced assembly are handled, no diagnostic
        /// </summary>
        [Fact]
        public async Task WhenAllCasesFromReferencedAssembly_NoDiagnostic()
        {
            // Referenced assembly code (library side)
            var libraryCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // Main project code
            var mainCode = @"
public class Program
{
    public void Process(IEnemy enemy)
    {
        switch (enemy)
        {
            case Goblin g:
                break;
            case Orc o:
                break;
        }
    }
}";

            await VerifyCrossAssemblyAnalyzerAsync(libraryCode, mainCode);
        }

        /// <summary>
        /// Transitive reference: detect [Case] types through indirect reference A → B → Attributes
        /// </summary>
        [Fact]
        public async Task WhenTransitiveReference_Diagnostic()
        {
            // Library B code (directly references Attributes)
            var libraryBCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // Library C code (references library B, Attributes are indirect reference)
            var libraryCCode = @"
[ExhaustiveSwitch.Case]
public sealed class Dragon : IEnemy { }
";

            // Main project code (references libraries B and C)
            var mainCode = @"
public class Program
{
    public void Process(IEnemy enemy)
    {
        {|#0:switch (enemy)
        {
            case Goblin g:
                break;
            case Orc o:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Dragon");

            await VerifyTransitiveReferenceAnalyzerAsync(libraryBCode, libraryCCode, mainCode, expected);
        }

        /// <summary>
        /// Helper method for cross-assembly tests
        /// </summary>
        private static async Task VerifyCrossAssemblyAnalyzerAsync(string libraryCode, string mainCode, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveTypeAnalyzer, DefaultVerifier>
            {
                TestCode = mainCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Add Analyzer project itself as reference (to use attributes)
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            // Compile library code and add as MetadataReference
            var libraryReference = await CompileToMetadataReferenceAsync("LibraryAssembly", libraryCode);
            test.TestState.AdditionalReferences.Add(libraryReference);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        /// <summary>
        /// Helper method for transitive reference tests
        /// </summary>
        private static async Task VerifyTransitiveReferenceAnalyzerAsync(
            string libraryBCode,
            string libraryCCode,
            string mainCode,
            params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveTypeAnalyzer, DefaultVerifier>
            {
                TestCode = mainCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Add Analyzer project itself as reference (to use attributes)
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            // Compile library B (directly references Attributes)
            var libraryBReference = await CompileToMetadataReferenceAsync("LibraryB", libraryBCode);

            // Compile library C (references library B, Attributes are indirect reference)
            var libraryCReference = await CompileToMetadataReferenceAsync("LibraryC", libraryCCode, libraryBReference);

            // Add both libraries to main project
            test.TestState.AdditionalReferences.Add(libraryBReference);
            test.TestState.AdditionalReferences.Add(libraryCReference);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        /// <summary>
        /// Compile code and create MetadataReference
        /// </summary>
        private static async Task<MetadataReference> CompileToMetadataReferenceAsync(string assemblyName, string code, params MetadataReference[] additionalReferences)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            // Get references from ReferenceAssemblies
            var referenceAssemblies = ReferenceAssemblies.Net.Net60;
            var resolvedReferences = await referenceAssemblies.ResolveAsync(LanguageNames.CSharp, default);

            // Add basic references
            var references = new List<MetadataReference>(resolvedReferences);
            references.Add(MetadataReference.CreateFromFile(typeof(ExhaustiveAttribute).Assembly.Location));
            references.AddRange(additionalReferences);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var failures = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
                throw new System.Exception($"Compilation failed: {string.Join(", ", failures.Select(d => d.GetMessage()))}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return MetadataReference.CreateFromStream(ms);
        }
    }
}
