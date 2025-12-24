using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// パターンマッチングに関するテスト
    /// </summary>
    public class ExhaustiveTypeAnalyzerPatternTests
    {
        /// <summary>
        /// whenガード条件を持つパターンでも網羅性をチェック
        /// </summary>
        [Fact]
        public async Task WhenPatternHasWhenClause_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy
{
    public int Health { get; set; }
}

[Case]
public sealed class Orc : IEnemy { }

public class Program
{
    public void Process(IEnemy enemy)
    {
        {|#0:switch (enemy)
        {
            case Goblin g when g.Health > 50:
                break;
            case Goblin g:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// プロパティパターンを使用している場合でも網羅性をチェック
        /// </summary>
        [Fact]
        public async Task WhenUsingPropertyPattern_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy
{
    public int Health { get; set; }
}

[Case]
public sealed class Orc : IEnemy
{
    public int Power { get; set; }
}

public class Program
{
    public void Process(IEnemy enemy)
    {
        switch (enemy)
        {
            case Goblin { Health: > 0 }:
                break;
            case Goblin:
                break;
            case Orc { Power: > 10 }:
                break;
            case Orc:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// RecursivePatternでも型を正しく認識
        /// </summary>
        [Fact]
        public async Task WhenUsingRecursivePattern_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy
{
    public string Name { get; set; }
}

[Case]
public sealed class Orc : IEnemy
{
    public string Name { get; set; }
}

public class Program
{
    public string Process(IEnemy enemy)
    {
        return enemy switch
        {
            Goblin { Name: var name } => name,
            Orc { Name: var name } => name,
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 型名だけのパターン(TypePattern)を正しく認識 - switch文
        /// </summary>
        [Fact]
        public async Task WhenUsingTypePatternInSwitchStatement_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }

public class Program
{
    public void Process(IEnemy enemy)
    {
        switch (enemy)
        {
            case Goblin:
                break;
            case Orc:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 型名だけのパターン(TypePattern)を正しく認識 - switch式
        /// </summary>
        [Fact]
        public async Task WhenUsingTypePatternInSwitchExpression_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }

public class Program
{
    public string Process(IEnemy enemy)
    {
        return enemy switch
        {
            Goblin => ""Goblin"",
            Orc => ""Orc"",
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 型名だけのパターンで網羅が不足している場合はエラー
        /// </summary>
        [Fact]
        public async Task WhenTypePatternMissingCase_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }

public class Program
{
    public void Process(IEnemy enemy)
    {
        {|#0:switch (enemy)
        {
            case Goblin:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyAnalyzerAsync(test, expected);
        }

        private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveTypeAnalyzer, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // C# 9.0の言語バージョンを設定
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", @"
is_global = true
build_property.LangVersion = 9.0
"));

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }
    }
}
