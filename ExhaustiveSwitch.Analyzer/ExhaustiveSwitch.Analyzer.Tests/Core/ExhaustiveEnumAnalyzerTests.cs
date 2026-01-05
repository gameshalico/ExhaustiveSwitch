using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// Tests for Exhaustive enum exhaustiveness checking
    /// </summary>
    public class ExhaustiveEnumAnalyzerTests
    {
        /// <summary>
        /// When all enum members are handled, no diagnostic
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
        /// When some enum members are missing, diagnostic
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
        /// When missing enum member even with default, diagnostic
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
        /// When all enum members are handled in switch expression, no diagnostic
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
        /// When enum member missing in switch expression, diagnostic
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
        /// When multiple enum members missing, reports all
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
        /// Enums without [Exhaustive] attribute are not validated
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
        /// Enums with [Flags] attribute are not validated
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
        /// Empty switch is missing all enum members
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
        /// Detect even when handling enum values as integers
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

        /// <summary>
        /// When handling enum members with when guard patterns
        /// </summary>
        [Fact]
        public async Task WhenHandlingEnumWithWhenGuard_RecognizesHandledMember()
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
    public void Process(GameState state, bool isActive)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing when isActive:
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
        /// When all enum members handled with when guard, no diagnostic
        /// </summary>
        [Fact]
        public async Task WhenAllEnumMembersHandledWithWhenGuard_NoDiagnostic()
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
    public void Process(GameState state, bool isActive)
    {
        switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing when isActive:
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

        private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveEnumAnalyzer, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Add Analyzer project itself as reference (to use attributes)
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }
    }
}
