using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// Exhaustive enum CodeFixのテスト
    /// </summary>
    public class ExhaustiveEnumCodeFixTests
    {
        /// <summary>
        /// switch文で1つのenumメンバーを追加
        /// </summary>
        [Fact]
        public async Task AddSingleEnumMemberToSwitchStatement()
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

            var fixedCode = @"
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
                throw new System.NotImplementedException();
        }
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyCodeFixAsync(test, expected, fixedCode);
        }

        /// <summary>
        /// switch文ですべてのenumメンバーを一括追加
        /// </summary>
        [Fact]
        public async Task AddAllEnumMembersToSwitchStatement()
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
        }|}
    }
}";

            var fixedCode = @"
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
        switch (state)
        {
            case GameState.Menu:
                break;
            case GameState.GameOver:
                throw new System.NotImplementedException();
            case GameState.Paused:
                throw new System.NotImplementedException();
            case GameState.Playing:
                throw new System.NotImplementedException();
        }
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "GameOver");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            var expected3 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            await VerifyCodeFixAsync(test, new[] { expected1, expected2, expected3 }, fixedCode, 0);
        }

        /// <summary>
        /// switch文でdefaultの前にenumメンバーを追加
        /// </summary>
        [Fact]
        public async Task AddEnumMemberBeforeDefault()
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
            default:
                break;
        }|}
    }
}";

            var fixedCode = @"
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
            case GameState.Paused:
                throw new System.NotImplementedException();
            case GameState.Playing:
                throw new System.NotImplementedException();
            default:
                break;
        }
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            await VerifyCodeFixAsync(test, new[] { expected1, expected2 }, fixedCode, 0);
        }

        /// <summary>
        /// switch式でenumメンバーを追加
        /// </summary>
        [Fact]
        public async Task AddEnumMemberToSwitchExpression()
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

            var fixedCode = @"
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
            GameState.Paused => throw new System.NotImplementedException(),
            _ => ""Unknown""
        };
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyCodeFixAsync(test, expected, fixedCode);
        }

        /// <summary>
        /// switch式で複数のenumメンバーを追加
        /// </summary>
        [Fact]
        public async Task AddAllEnumMembersToSwitchExpression()
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
            _ => ""Unknown""
        }|};
    }
}";

            var fixedCode = @"
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
            GameState.Paused => throw new System.NotImplementedException(),
            GameState.Playing => throw new System.NotImplementedException(),
            _ => ""Unknown""
        };
    }
}";

            var expected1 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            var expected2 = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            await VerifyCodeFixAsync(test, new[] { expected1, expected2 }, fixedCode, 0);
        }

        /// <summary>
        /// switch式でdiscardパターンがない場合、末尾に追加
        /// </summary>
        [Fact]
        public async Task AddEnumMemberToSwitchExpressionWithoutDiscard()
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
            GameState.Playing => ""Playing""
        }|};
    }
}";

            var fixedCode = @"
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
            GameState.Paused => throw new System.NotImplementedException()
        };
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Paused");

            await VerifyCodeFixAsync(test, expected, fixedCode);
        }

        /// <summary>
        /// 完全修飾名のenumでもCodeFixが動作する
        /// </summary>
        [Fact]
        public async Task AddEnumMemberWithFullyQualifiedName()
        {
            var test = @"
namespace MyNamespace
{
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
                case GameState.Menu:
                    break;
            }|}
        }
    }
}";

            var fixedCode = @"
namespace MyNamespace
{
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
                    throw new System.NotImplementedException();
            }
        }
    }
}";

            var expected = new DiagnosticResult("EXH1001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("GameState", "Playing");

            await VerifyCodeFixAsync(test, expected, fixedCode);
        }

        private static async Task VerifyCodeFixAsync(
            string source,
            DiagnosticResult expected,
            string fixedSource,
            int codeActionIndex = 0)
        {
            await VerifyCodeFixAsync(source, new[] { expected }, fixedSource, codeActionIndex);
        }

        private static async Task VerifyCodeFixAsync(
            string source,
            DiagnosticResult[] expected,
            string fixedSource,
            int codeActionIndex = 0)
        {
            var test = new CSharpCodeFixTest<ExhaustiveEnumAnalyzer, ExhaustiveEnumCodeFixProvider, DefaultVerifier>
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                CodeActionIndex = codeActionIndex,
            };

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }
    }
}
