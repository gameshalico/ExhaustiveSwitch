using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// ジェネリック型に関するExhaustiveSwitchAnalyzerのテスト
    /// </summary>
    public class ExhaustiveSwitchAnalyzerGenericsTests
    {
        /// <summary>
        /// ジェネリックインターフェース: すべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenGenericInterface_AllCasesHandled_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IResult<T> { }

[Case]
public sealed class Success<T> : IResult<T> { }

[Case]
public sealed class Failure<T> : IResult<T> { }

public class Program
{
    public void Process(IResult<int> result)
    {
        switch (result)
        {
            case Success<int> success:
                break;
            case Failure<int> failure:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// ジェネリックインターフェース: 一部のケースが不足している場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenGenericInterface_MissingCase_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IResult<T> { }

[Case]
public sealed class Success<T> : IResult<T> { }

[Case]
public sealed class Failure<T> : IResult<T> { }

public class Program
{
    public void Process(IResult<int> result)
    {
        {|#0:switch (result)
        {
            case Success<int> success:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IResult<int>", "Failure<int>");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// ジェネリックインターフェース: switch式ですべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenGenericInterface_SwitchExpression_AllCasesHandled_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IResult<T> { }

[Case]
public sealed class Success<T> : IResult<T>
{
    public T Value { get; set; }
}

[Case]
public sealed class Failure<T> : IResult<T> { }

public class Program
{
    public string Process(IResult<int> result)
    {
        return result switch
        {
            Success<int> s => s.Value.ToString(),
            Failure<int> f => ""Failed"",
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// ジェネリックインターフェース: switch式で一部のケースが不足している場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenGenericInterface_SwitchExpression_MissingCase_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IResult<T> { }

[Case]
public sealed class Success<T> : IResult<T>
{
    public T Value { get; set; }
}

[Case]
public sealed class Failure<T> : IResult<T> { }

public class Program
{
    public string Process(IResult<int> result)
    {
        return {|#0:result switch
        {
            Success<int> s => s.Value.ToString(),
            _ => ""Unknown""
        }|};
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IResult<int>", "Failure<int>");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// 複数の型引数を持つジェネリック: すべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenMultipleTypeParameters_AllCasesHandled_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEither<TLeft, TRight> { }

[Case]
public sealed class Left<TLeft, TRight> : IEither<TLeft, TRight> { }

[Case]
public sealed class Right<TLeft, TRight> : IEither<TLeft, TRight> { }

public class Program
{
    public void Process(IEither<string, int> either)
    {
        switch (either)
        {
            case Left<string, int> left:
                break;
            case Right<string, int> right:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 複数の型引数を持つジェネリック: 一部のケースが不足している場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMultipleTypeParameters_MissingCase_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEither<TLeft, TRight> { }

[Case]
public sealed class Left<TLeft, TRight> : IEither<TLeft, TRight> { }

[Case]
public sealed class Right<TLeft, TRight> : IEither<TLeft, TRight> { }

public class Program
{
    public void Process(IEither<string, int> either)
    {
        {|#0:switch (either)
        {
            case Left<string, int> left:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEither<string, int>", "Right<string, int>");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// ジェネリック抽象クラス: すべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenGenericAbstractClass_AllCasesHandled_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Option<T> { }

[Case]
public sealed class Some<T> : Option<T> { }

[Case]
public sealed class None<T> : Option<T> { }

public class Program
{
    public void Process(Option<string> option)
    {
        switch (option)
        {
            case Some<string> some:
                break;
            case None<string> none:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// ジェネリック型の階層構造: すべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenGenericHierarchy_AllCasesHandled_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface ICommand<TContext> { }

[Case]
public sealed class MoveCommand<TContext> : ICommand<TContext> { }

[Case]
public sealed class AttackCommand<TContext> : ICommand<TContext> { }

[Case]
public sealed class DefendCommand<TContext> : ICommand<TContext> { }

public class GameContext { }

public class Program
{
    public void Process(ICommand<GameContext> command)
    {
        switch (command)
        {
            case MoveCommand<GameContext> move:
                break;
            case AttackCommand<GameContext> attack:
                break;
            case DefendCommand<GameContext> defend:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// ジェネリック型の階層構造: 一部のケースが不足している場合、複数のエラー
        /// </summary>
        [Fact]
        public async Task WhenGenericHierarchy_MultipleCasesMissing_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface ICommand<TContext> { }

[Case]
public sealed class MoveCommand<TContext> : ICommand<TContext> { }

[Case]
public sealed class AttackCommand<TContext> : ICommand<TContext> { }

[Case]
public sealed class DefendCommand<TContext> : ICommand<TContext> { }

public class GameContext { }

public class Program
{
    public void Process(ICommand<GameContext> command)
    {
        {|#0:switch (command)
        {
            case MoveCommand<GameContext> move:
                break;
        }|}
    }
}";

            var expected1 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ICommand<GameContext>", "AttackCommand<GameContext>");

            var expected2 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ICommand<GameContext>", "DefendCommand<GameContext>");

            await VerifyAnalyzerAsync(test, expected1, expected2);
        }

        /// <summary>
        /// 非ジェネリック型とジェネリック型の混在:
        /// ジェネリック型の場合、どのような型引数でも処理できるパターンが必要
        /// </summary>
        [Fact]
        public async Task WhenMixedGenericAndNonGeneric_MissingGenericCase_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IMessage { }

[Case]
public sealed class TextMessage : IMessage { }

[Case]
public sealed class DataMessage<T> : IMessage { }

public class Program
{
    public void Process(IMessage message)
    {
        {|#0:switch (message)
        {
            case TextMessage text:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IMessage", "DataMessage<T>");

            await VerifyAnalyzerAsync(test, expected);
        }

        private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveSwitchAnalyzer, DefaultVerifier>
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
