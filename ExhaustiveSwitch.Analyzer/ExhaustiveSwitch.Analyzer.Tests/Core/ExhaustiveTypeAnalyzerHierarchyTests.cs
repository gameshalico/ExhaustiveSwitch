using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// Tests for hierarchy structures (multiple inheritance, nested Exhaustive)
    /// </summary>
    public class ExhaustiveTypeAnalyzerHierarchyTests
    {
        /// <summary>
        /// Multiple inheritance: when all concrete types (KingSlime, QueenSlime, Orc) are handled, no diagnostic
        /// Since Slime is abstract, handling all its child classes covers it
        /// </summary>
        [Fact]
        public async Task WhenMultipleInheritance_AllConcreteTypes_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Enemy { }

[Case]
public abstract class Slime : Enemy { }

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
        /// Multiple inheritance: when handled by intermediate class (Slime) and concrete type (Orc), no diagnostic
        /// Handling Slime covers its descendants KingSlime and QueenSlime
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
        /// Multiple inheritance: when some concrete types are missing, diagnostic
        /// Only KingSlime and Orc are handled, so QueenSlime is missing
        /// (Since Slime is abstract, it is covered if all KingSlime and QueenSlime are handled. Only QueenSlime errors)
        /// </summary>
        [Fact]
        public async Task WhenMultipleInheritance_MissingConcreteType_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public abstract class Enemy { }

[Case]
public abstract class Slime : Enemy { }

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
        /// Multiple inheritance: when only intermediate class is handled and Orc is missing, diagnostic
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
        /// Interface + nested Exhaustive: when intermediate class is [Case, Exhaustive], handling intermediate class is sufficient
        /// </summary>
        [Fact]
        public async Task WhenNestedExhaustiveInterface_IntermediateClass_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface ISample { }

[Case]
public sealed class ConcreteA : ISample { }

[Case, Exhaustive]
public class ConcreteB : ISample { }

[Case]
public sealed class ConcreteB1 : ConcreteB { }

[Case]
public sealed class ConcreteB2 : ConcreteB { }

public class Program
{
    public void Process(ISample sample)
    {
        switch (sample)
        {
            case ConcreteA a:
                break;
            case ConcreteB b:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Interface + nested Exhaustive: when intermediate class is [Case, Exhaustive] and only child classes are handled, intermediate class is missing
        /// (If intermediate class is not abstract, it can be instantiated so explicit handling is required)
        /// </summary>
        [Fact]
        public async Task WhenNestedExhaustiveInterface_OnlyChildClasses_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface ISample { }

[Case]
public sealed class ConcreteA : ISample { }

[Case, Exhaustive]
public class ConcreteB : ISample { }

[Case]
public sealed class ConcreteB1 : ConcreteB { }

[Case]
public sealed class ConcreteB2 : ConcreteB { }

public class Program
{
    public void Process(ISample sample)
    {
        {|#0:switch (sample)
        {
            case ConcreteA a:
                break;
            case ConcreteB1 b1:
                break;
            case ConcreteB2 b2:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("ISample", "ConcreteB");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// Interface + nested Exhaustive: when intermediate class is abstract and [Case, Exhaustive], only child classes are sufficient
        /// </summary>
        [Fact]
        public async Task WhenNestedExhaustiveInterface_AbstractIntermediateClass_OnlyChildClasses_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface ISample { }

[Case]
public sealed class ConcreteA : ISample { }

[Case, Exhaustive]
public abstract class ConcreteB : ISample { }

[Case]
public sealed class ConcreteB1 : ConcreteB { }

[Case]
public sealed class ConcreteB2 : ConcreteB { }

public class Program
{
    public void Process(ISample sample)
    {
        switch (sample)
        {
            case ConcreteA a:
                break;
            case ConcreteB1 b1:
                break;
            case ConcreteB2 b2:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// When handled by super interface, no diagnostic
        /// Dragon and Harpy implementing IFlyable are handled together
        /// </summary>
        [Fact]
        public async Task WhenHandledBySuperInterface_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy
{
    void Attack();
}

public interface IFlyable
{
    void Fly();
}

[Case]
public class Goblin : IEnemy
{
    public void Attack() { }
}

[Case]
public class Dragon : IEnemy, IFlyable
{
    public void Attack() { }
    public void Fly() { }
}

[Case]
public class Harpy : IEnemy, IFlyable
{
    public void Attack() { }
    public void Fly() { }
}

public class Program
{
    public void ProcessEnemy(IEnemy enemy)
    {
        switch (enemy)
        {
            case Goblin goblin:
                break;
            case IFlyable flyable:
                // Handle Dragon and Harpy together
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// Mixed pattern of super interface and concrete types, no diagnostic
        /// </summary>
        [Fact]
        public async Task WhenMixedSuperInterfaceAndConcrete_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

public interface IFlyable { }

[Case]
public class Goblin : IEnemy { }

[Case]
public class Dragon : IEnemy, IFlyable { }

[Case]
public class Harpy : IEnemy, IFlyable { }

[Case]
public class Skeleton : IEnemy { }

public class Program
{
    public void ProcessEnemy(IEnemy enemy)
    {
        switch (enemy)
        {
            case IFlyable flyable:
                // Handle Dragon and Harpy together
                break;
            case Goblin goblin:
                break;
            case Skeleton skeleton:
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// When super interface handling exists but cases not covered by it are missing, diagnostic
        /// </summary>
        [Fact]
        public async Task WhenSuperInterfaceDoesNotCoverAll_Diagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

public interface IFlyable { }

[Case]
public class Goblin : IEnemy { }

[Case]
public class Dragon : IEnemy, IFlyable { }

[Case]
public class Harpy : IEnemy, IFlyable { }

public class Program
{
    public void ProcessEnemy(IEnemy enemy)
    {
        {|#0:switch (enemy)
        {
            case IFlyable flyable:
                // Only Dragon and Harpy are handled
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Goblin");

            await VerifyAnalyzerAsync(test, expected);
        }

        /// <summary>
        /// Exhaustiveness check for nested switch
        /// </summary>
        [Fact]
        public async Task WhenNestedSwitch_BothChecked()
        {
            var test = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }

[Exhaustive]
public interface IItem { }

[Case]
public sealed class Sword : IItem { }

[Case]
public sealed class Shield : IItem { }

public class Program
{
    public void Process(IEnemy enemy, IItem item)
    {
        switch (enemy)
        {
            case Goblin g:
                {|#0:switch (item)
                {
                    case Sword s:
                        break;
                }|}
                break;
            case Orc o:
                break;
        }
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IItem", "Shield");

            await VerifyAnalyzerAsync(test, expected);
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
