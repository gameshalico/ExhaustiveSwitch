using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// Tests for basic switch statement/expression exhaustiveness checking
    /// </summary>
    public class ExhaustiveTypeAnalyzerBasicTests
    {
        /// <summary>
        /// When all cases are handled, no diagnostic
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
        /// When some cases are missing, diagnostic
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
        /// When missing case even with default, diagnostic
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
        /// When all cases are handled in switch expression, no diagnostic
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
        /// When missing case in switch expression, diagnostic
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
        /// Exhaustiveness check with abstract class
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
        /// Types without Case attribute are not validated
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

// No Case attribute
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
        /// When multiple cases missing, reports all
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

            // When multiple missing, all are reported
            var expected1 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Orc");

            var expected2 = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Dragon");

            await VerifyAnalyzerAsync(test, expected1, expected2);
        }

        /// <summary>
        /// Empty switch is missing all cases
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
        /// When Case attribute without Exhaustive base, warning
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
        /// When Case attribute with Exhaustive base, no warning
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
        /// When multiple Cases with mixed Exhaustive bases, warning for orphan only
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
        /// Interface without Exhaustive attribute does not check switch, but Case types get warning
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

            // No EXH0001 for switch, but EXH0002 for Case types
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
            var test = new CSharpAnalyzerTest<ExhaustiveTypeAnalyzer, DefaultVerifier>
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
