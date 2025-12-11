using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// 基本的なswitch文/式の網羅性チェックのテスト
    /// </summary>
    public class ExhaustiveSwitchAnalyzerBasicTests
    {
        /// <summary>
        /// すべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenAllCasesAreHandled_NoDiagnostic()
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
            case Goblin g:
                break;
            case Orc o:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 一部のケースが処理されていない場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMissingCase_Diagnostic()
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
        /// defaultがあってもケースが不足している場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMissingCaseWithDefault_Diagnostic()
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
            case Goblin g:
                break;
            default:
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
        /// switch式ですべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenAllCasesAreHandledInSwitchExpression_NoDiagnostic()
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
            Goblin g => ""Goblin"",
            Orc o => ""Orc"",
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// switch式で一部のケースが処理されていない場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMissingCaseInSwitchExpression_Diagnostic()
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
        return {|#0:enemy switch
        {
            Goblin g => ""Goblin"",
            _ => ""Unknown""
        }|};
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// 抽象クラスでの網羅性チェック
        /// </summary>
        [Fact]
        public async Task WhenUsingAbstractClass_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Enemy { }

[Case]
public sealed class Goblin : Enemy { }

[Case]
public sealed class Orc : Enemy { }

public class Program
{
    public void Process(Enemy enemy)
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
                .WithArguments("Enemy", "Orc");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// Case属性がない型は検証対象外
        /// </summary>
        [Fact]
        public async Task WhenTypeHasNoCaseAttribute_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

// Case属性なし
public sealed class Orc : IEnemy { }

public class Program
{
    public void Process(IEnemy enemy)
    {
        switch (enemy)
        {
            case Goblin g:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 複数の不足ケースがある場合、すべて報告
        /// </summary>
        [Fact]
        public async Task WhenMultipleCasesMissing_ReportsAll()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }

[Case]
public sealed class Dragon : IEnemy { }

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

            // 複数不足している場合、すべて報告される
            var expected1 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            var expected2 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Dragon");

            await VerifyAnalyzerAsync(test, expected1, expected2);
        }

        /// <summary>
        /// 空のswitchはすべてのケースが不足
        /// </summary>
        [Fact]
        public async Task WhenEmptySwitch_Diagnostic()
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
        }|}
    }
}";

            var expected1 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Goblin");

            var expected2 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyAnalyzerAsync(test, expected1, expected2);
        }

        /// <summary>
        /// [Case]属性があるが[Exhaustive]型を継承/実装していない場合、警告
        /// </summary>
        [Fact]
        public async Task WhenCaseWithoutExhaustiveBase_Warning()
        {
            var test = @"
using ExhaustiveSwitch;

[Case]
public sealed class {|#0:OrphanClass|} { }";

            var expected = new DiagnosticResult("EXH0002", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("OrphanClass");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// [Case]属性があり[Exhaustive]型を継承している場合、警告なし
        /// </summary>
        [Fact]
        public async Task WhenCaseWithExhaustiveBase_NoWarning()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 複数の[Case]型があり、一部のみ[Exhaustive]型を継承していない場合、該当する型のみ警告
        /// </summary>
        [Fact]
        public async Task WhenMultipleCasesWithMixedExhaustiveBases_WarningForOrphanOnly()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class {|#0:OrphanClass|} { }";

            var expected = new DiagnosticResult("EXH0002", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("OrphanClass");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// Exhaustive属性のないインターフェースはswitchのチェックはされないが、[Case]型には警告が出る
        /// </summary>
        [Fact]
        public async Task WhenNoExhaustiveAttribute_CaseWarningOnly()
        {
            var test = @"
using ExhaustiveSwitch;

public interface IEnemy { }

[Case]
public sealed class {|#0:Goblin|} : IEnemy { }

[Case]
public sealed class {|#1:Orc|} : IEnemy { }

public class Program
{
    public void Process(IEnemy enemy)
    {
        switch (enemy)
        {
            case Goblin g:
                break;
        }
    }
}";

            // switchのEXH0001は出ないが、[Case]型に対するEXH0002は出る
            var expected1 = new DiagnosticResult("EXH0002", DiagnosticSeverity.Warning)
                .WithLocation(0)
                .WithArguments("Goblin");

            var expected2 = new DiagnosticResult("EXH0002", DiagnosticSeverity.Warning)
                .WithLocation(1)
                .WithArguments("Orc");

            await VerifyAnalyzerAsync(test, expected1, expected2);
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
