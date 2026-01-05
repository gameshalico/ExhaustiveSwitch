using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// Tests for pattern matching
    /// </summary>
    public class ExhaustiveTypeAnalyzerPatternTests
    {
        /// <summary>
        /// Check exhaustiveness even with when guard conditions
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
        /// Check exhaustiveness even when using property patterns
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
        /// Correctly recognize types even with RecursivePattern
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
        /// Correctly recognize type-only pattern (TypePattern) - switch statement
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
        /// Correctly recognize type-only pattern (TypePattern) - switch expression
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
        /// Error when type pattern is missing cases
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

        /// <summary>
        /// Mixing type pattern and declaration pattern - switch statement
        /// </summary>
        [Fact]
        public async Task WhenMixingTypePatternAndDeclarationPattern_NoDiagnostic()
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
            case Orc o:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Mixing type pattern and declaration pattern - switch expression
        /// </summary>
        [Fact]
        public async Task WhenMixingTypePatternAndDeclarationPatternInExpression_NoDiagnostic()
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
            Orc o => o.ToString(),
        };
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Mixing patterns but missing case
        /// </summary>
        [Fact]
        public async Task WhenMixingPatternsButMissingCase_Diagnostic()
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
            case Goblin:
                break;
            case Orc o:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Dragon");

            await VerifyAnalyzerAsync(test, expected);
        }

        private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveTypeAnalyzer, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Set C# 9.0 language version
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", @"
is_global = true
build_property.LangVersion = 9.0
"));

            // Add Analyzer project itself as reference (to use attributes)
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }
    }
}
