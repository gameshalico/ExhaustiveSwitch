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

        /// <summary>
        /// When all enum members and null are handled, no diagnostic
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumAllMembersAndNullHandled_NoDiagnostic()
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
    public void Process(GameState? state)
    {
        switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
                break;
            case GameState.Paused:
                break;
            case null:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// When all enum members handled and default covers null, no diagnostic
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumAllMembersAndDefaultHandled_NoDiagnostic()
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
    public void Process(GameState? state)
    {
        switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
                break;
            case GameState.Paused:
                break;
            default:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Report EXH1002 when all enum members are handled but null is missing
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumMissingNull_Diagnostic()
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
    public void Process(GameState? state)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
                break;
            case GameState.Paused:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH1002", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// Report both EXH1001 and EXH1002 when nullable enum is missing members and null
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumMissingMembersAndNull_ReportsBoth()
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
    public void Process(GameState? state)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
        }|}
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            var expected3 = new DiagnosticResult("EXH1002", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState");

            await VerifyAnalyzerAsync(test, expected1, expected2, expected3);
        }

        /// <summary>
        /// Report only EXH1001 when nullable enum is missing members but null is handled
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumMissingMembersButNullHandled_OnlyEXH1001()
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
    public void Process(GameState? state)
    {
        {|#0:switch (state)
        {
            case GameState.Menu:
                break;
            case null:
                break;
        }|}
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyAnalyzerAsync(test, expected1, expected2);
        }

        // ---- Switch Expression Nullable ----

        /// <summary>
        /// Switch expression: all members + null handled, no diagnostic
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumSwitchExpressionAllHandledWithNull_NoDiagnostic()
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
    public string Process(GameState? state)
    {
        return state switch
        {
            GameState.Menu => ""Menu"",
            GameState.Playing => ""Playing"",
            GameState.Paused => ""Paused"",
            null => ""Null"",
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Switch expression: all members + discard covers null, no diagnostic
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumSwitchExpressionAllHandledWithDiscard_NoDiagnostic()
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
    public string Process(GameState? state)
    {
        return state switch
        {
            GameState.Menu => ""Menu"",
            GameState.Playing => ""Playing"",
            GameState.Paused => ""Paused"",
            _ => ""Default"",
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Switch expression: report EXH1002 when all members are handled but null is missing
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumSwitchExpressionMissingNull_Diagnostic()
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
    public string Process(GameState? state)
    {
        return {|#0:state switch
        {
            GameState.Menu => ""Menu"",
            GameState.Playing => ""Playing"",
            GameState.Paused => ""Paused"",
        }|};
    }
}";

            var expected = new DiagnosticResult("EXH1002", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// Switch expression: missing members and null, reports both
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumSwitchExpressionMissingMembersAndNull_ReportsBoth()
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
    public string Process(GameState? state)
    {
        return {|#0:state switch
        {
            GameState.Menu => ""Menu"",
        }|};
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            var expected3 = new DiagnosticResult("EXH1002", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState");

            await VerifyAnalyzerAsync(test, expected1, expected2, expected3);
        }

        /// <summary>
        /// When null case is in the middle (not last), no EXH1002
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumNullCaseInMiddle_NoDiagnostic()
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
    public void Process(GameState? state)
    {
        switch (state)
        {
            case GameState.Menu:
                break;
            case null:
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
        /// Switch expression: null arm in the middle, no EXH1002
        /// </summary>
        [Fact]
        public async Task WhenNullableEnumSwitchExpressionNullArmInMiddle_NoDiagnostic()
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
    public string Process(GameState? state)
    {
        return state switch
        {
            GameState.Menu => ""Menu"",
            null => ""Null"",
            GameState.Playing => ""Playing"",
            GameState.Paused => ""Paused"",
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Non-nullable enum should not produce EXH1002 even without null handling
        /// </summary>
        [Fact]
        public async Task WhenNonNullableEnumAllHandled_NoNullDiagnostic()
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
        switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.Playing:
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
