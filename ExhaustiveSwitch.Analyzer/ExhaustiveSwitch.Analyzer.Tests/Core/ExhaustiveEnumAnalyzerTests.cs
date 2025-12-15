using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// Exhaustive enum網羅性チェックのテスト
    /// </summary>
    public class ExhaustiveEnumAnalyzerTests
    {
        /// <summary>
        /// すべてのenumメンバーが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenAllEnumMembersAreHandled_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu,
    Playing,
    Paused
}

public class Program
{
    public void Process(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
                break;
            case GameState.Paused:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 一部のenumメンバーが処理されていない場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMissingEnumMember_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu,
    Playing,
    Paused
}

public class Program
{
    public void Process(GameState state)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// defaultがあってもenumメンバーが不足している場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMissingEnumMemberWithDefault_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu,
    Playing,
    Paused
}

public class Program
{
    public void Process(GameState state)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
                break;
            default:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// switch式ですべてのenumメンバーが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenAllEnumMembersAreHandledInSwitchExpression_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu,
    Playing,
    Paused
}

public class Program
{
    public string Process(GameState state)
    {
        return state switch
        {
            GameState.Menu => ""Menu"",
            GameState.Playing => ""Playing"",
            GameState.Paused => ""Paused"",
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// switch式でenumメンバーが不足している場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMissingEnumMemberInSwitchExpression_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu,
    Playing,
    Paused
}

public class Program
{
    public string Process(GameState state)
    {
        return {|#0:state switch
        {
            GameState.Menu => ""Menu"",
            GameState.Playing => ""Playing"",
            _ => ""Unknown""
        }|};
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// 複数のenumメンバーが不足している場合、すべて報告
        /// </summary>
        [Fact]
        public async Task WhenMultipleEnumMembersMissing_ReportsAll()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu,
    Playing,
    Paused,
    GameOver
}

public class Program
{
    public void Process(GameState state)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
                break;
        }|}
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "GameOver");

            await VerifyAnalyzerAsync(test, expected1, expected2);
        }

        /// <summary>
        /// [Exhaustive]属性がないenumは検証されない
        /// </summary>
        [Fact]
        public async Task WhenEnumHasNoExhaustiveAttribute_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

public enum GameState
{
    Menu,
    Playing,
    Paused
}

public class Program
{
    public void Process(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// [Flags]属性付きenumは検証されない
        /// </summary>
        [Fact]
        public async Task WhenEnumHasFlagsAttribute_NoDiagnostic()
        {
            var test = @"
using System;
using ExhaustiveSwitch;

[Flags]
[Exhaustive]
public enum Permissions
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}

public class Program
{
    public void Process(Permissions permissions)
    {
        switch (permissions)
        {
            case Permissions.Read:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 空のswitchはすべてのenumメンバーが不足
        /// </summary>
        [Fact]
        public async Task WhenEmptySwitch_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu,
    Playing
}

public class Program
{
    public void Process(GameState state)
    {
        {|#0:switch (state)
        {
        }|}
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Menu");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            await VerifyAnalyzerAsync(test, expected1, expected2);
        }

        /// <summary>
        /// enum値を整数で処理している場合も検出
        /// </summary>
        [Fact]
        public async Task WhenHandlingEnumByIntValue_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public enum GameState
{
    Menu = 0,
    Playing = 1,
    Paused = 2
}

public class Program
{
    public void Process(GameState state)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
            case (GameState)1:  // Playing
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyAnalyzerAsync(test, expected);
        }

        private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveEnumAnalyzer, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }
    }
}
