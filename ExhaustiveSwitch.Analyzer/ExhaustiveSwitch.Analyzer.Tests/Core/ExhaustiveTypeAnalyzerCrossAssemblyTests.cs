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
    /// クロスアセンブリに関するテスト
    /// </summary>
    public class ExhaustiveTypeAnalyzerCrossAssemblyTests
    {
        /// <summary>
        /// クロスアセンブリ: 別アセンブリの[Case]型を検出
        /// </summary>
        [Fact]
        public async Task WhenCaseTypeInReferencedAssembly_Diagnostic()
        {
            // 参照アセンブリのコード（ライブラリ側）
            var libraryCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // メインプロジェクトのコード
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
        /// クロスアセンブリ: 別アセンブリですべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenAllCasesFromReferencedAssembly_NoDiagnostic()
        {
            // 参照アセンブリのコード（ライブラリ側）
            var libraryCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // メインプロジェクトのコード
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
        /// Transitive参照: A → B → Attributesの間接参照で[Case]型を検出
        /// </summary>
        [Fact]
        public async Task WhenTransitiveReference_Diagnostic()
        {
            // ライブラリBのコード（Attributesを直接参照）
            var libraryBCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // ライブラリCのコード（ライブラリBを参照、Attributesは間接参照）
            var libraryCCode = @"
[ExhaustiveSwitch.Case]
public sealed class Dragon : IEnemy { }
";

            // メインプロジェクトのコード（ライブラリBとCを参照）
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
        /// クロスアセンブリテスト用のヘルパーメソッド
        /// </summary>
        private static async Task VerifyCrossAssemblyAnalyzerAsync(string libraryCode, string mainCode, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveTypeAnalyzer, DefaultVerifier>
            {
                TestCode = mainCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            // ライブラリコードをコンパイルしてMetadataReferenceとして追加
            var libraryReference = await CompileToMetadataReferenceAsync("LibraryAssembly", libraryCode);
            test.TestState.AdditionalReferences.Add(libraryReference);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        /// <summary>
        /// Transitive参照テスト用のヘルパーメソッド
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

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            // ライブラリB（Attributesを直接参照）をコンパイル
            var libraryBReference = await CompileToMetadataReferenceAsync("LibraryB", libraryBCode);

            // ライブラリC（ライブラリBを参照、Attributesは間接参照）をコンパイル
            var libraryCReference = await CompileToMetadataReferenceAsync("LibraryC", libraryCCode, libraryBReference);

            // メインプロジェクトに両方のライブラリを追加
            test.TestState.AdditionalReferences.Add(libraryBReference);
            test.TestState.AdditionalReferences.Add(libraryCReference);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        /// <summary>
        /// コードをコンパイルしてMetadataReferenceを作成
        /// </summary>
        private static async Task<MetadataReference> CompileToMetadataReferenceAsync(string assemblyName, string code, params MetadataReference[] additionalReferences)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            // ReferenceAssembliesから参照を取得
            var referenceAssemblies = ReferenceAssemblies.Net.Net60;
            var resolvedReferences = await referenceAssemblies.ResolveAsync(LanguageNames.CSharp, default);

            // 基本的な参照を追加
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
