using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests
{
    public class ExhaustiveSwitchAnalyzerTests
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
        /// 多重継承: すべての具象型（KingSlime, QueenSlime, Orc）で処理する場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenMultipleInheritance_AllConcreteTypes_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Enemy { }

[Case]
public class Slime : Enemy { }

[Case]
public sealed class KingSlime : Slime { }

[Case]
public sealed class QueenSlime : Slime { }

[Case]
public sealed class Orc : Enemy { }

public class Program
{
    public void Process(Enemy enemy)
    {
        switch (enemy)
        {
            case KingSlime k:
                break;
            case QueenSlime q:
                break;
            case Orc o:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 多重継承: 中間クラス（Slime）と具象型（Orc）で処理する場合、エラーなし
        /// Slimeで処理すれば、その子孫のKingSlimeとQueenSlimeもカバーされる
        /// </summary>
        [Fact]
        public async Task WhenMultipleInheritance_IntermediateClass_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Enemy { }

[Case]
public class Slime : Enemy { }

[Case]
public sealed class KingSlime : Slime { }

[Case]
public sealed class QueenSlime : Slime { }

[Case]
public sealed class Orc : Enemy { }

public class Program
{
    public void Process(Enemy enemy)
    {
        switch (enemy)
        {
            case Slime s:
                break;
            case Orc o:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 多重継承: 一部の具象型が不足している場合、エラー
        /// KingSlimeとOrcのみで処理しているので、QueenSlimeが不足
        /// （Slimeは、KingSlimeとQueenSlimeがすべて処理されればカバーされるため、QueenSlimeのみエラー）
        /// </summary>
        [Fact]
        public async Task WhenMultipleInheritance_MissingConcreteType_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Enemy { }

[Case]
public class Slime : Enemy { }

[Case]
public sealed class KingSlime : Slime { }

[Case]
public sealed class QueenSlime : Slime { }

[Case]
public sealed class Orc : Enemy { }

public class Program
{
    public void Process(Enemy enemy)
    {
        {|#0:switch (enemy)
        {
            case KingSlime k:
                break;
            case Orc o:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("Enemy", "QueenSlime");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// 多重継承: 中間クラスのみで処理、Orcが不足している場合、エラー
        /// </summary>
        [Fact]
        public async Task WhenMultipleInheritance_MissingTopLevelType_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Enemy { }

[Case]
public class Slime : Enemy { }

[Case]
public sealed class KingSlime : Slime { }

[Case]
public sealed class QueenSlime : Slime { }

[Case]
public sealed class Orc : Enemy { }

public class Program
{
    public void Process(Enemy enemy)
    {
        {|#0:switch (enemy)
        {
            case Slime s:
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
        /// CodeFix: switch文で不足しているcaseを追加
        /// </summary>
        [Fact]
        public async Task CodeFix_AddMissingCaseToSwitchStatement()
        {
            var testCode = @"
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

            var fixedCode = @"
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
            case Orc orc:
                throw new System.NotImplementedException();
        }
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyCodeFixAsync(testCode, expected, fixedCode);
        }

        /// <summary>
        /// CodeFix: switch式で不足しているarmを追加
        /// </summary>
        [Fact]
        public async Task CodeFix_AddMissingArmToSwitchExpression()
        {
            var testCode = @"
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

            var fixedCode = @"
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
            Orc orc => throw new System.NotImplementedException(),
            _ => ""Unknown""
        };
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyCodeFixAsync(testCode, expected, fixedCode);
        }

        /// <summary>
        /// CodeFix: defaultセクションの前に不足しているcaseを追加
        /// </summary>
        [Fact]
        public async Task CodeFix_AddMissingCaseBeforeDefault()
        {
            var testCode = @"
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

            var fixedCode = @"
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
            case Orc orc:
                throw new System.NotImplementedException();
            default:
                break;
        }
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            await VerifyCodeFixAsync(testCode, expected, fixedCode);
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

        private static async Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
        {
            await VerifyCodeFixAsync(source, new[] { expected }, fixedSource, codeActionIndex: 0);
        }

        private static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource, int codeActionIndex)
        {
            var test = new CSharpCodeFixTest<ExhaustiveSwitchAnalyzer, ExhaustiveSwitchCodeFixProvider, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
                CodeActionEquivalenceKey = codeActionIndex == 1 ? "AddAllCases" : null,
            };

            // codeActionIndex が 1 の場合、BatchFixedCode を使用
            if (codeActionIndex == 1)
            {
                test.BatchFixedCode = fixedSource;
            }
            else
            {
                test.FixedCode = fixedSource;
            }

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);
            test.FixedState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);
            test.BatchFixedState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }
    }
}
