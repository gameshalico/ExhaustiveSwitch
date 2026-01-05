using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    public class ExhaustiveSwitchCodeFixTests
    {
        /// <summary>
        /// CodeFix: add missing case to switch statement
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
        /// CodeFix: add missing arm to switch expression
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
        /// CodeFix: add missing case before default section
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

        private static async Task VerifyCodeFixAsync(string source, DiagnosticResult expected, string fixedSource)
        {
            await VerifyCodeFixAsync(source, new[] { expected }, fixedSource, codeActionIndex: 0);
        }

        private static async Task VerifyCodeFixAsync(string source, DiagnosticResult[] expected, string fixedSource, int codeActionIndex)
        {
            var test = new CSharpCodeFixTest<ExhaustiveTypeAnalyzer, ExhaustiveTypeCodeFixProvider, DefaultVerifier>
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

            // Add Analyzer project itself as reference (to use attributes)
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);
            test.FixedState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);
            test.BatchFixedState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }
    }
}
