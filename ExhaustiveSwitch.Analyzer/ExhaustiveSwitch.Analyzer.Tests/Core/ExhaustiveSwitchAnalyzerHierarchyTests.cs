using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;

namespace ExhaustiveSwitch.Analyzer.Tests.Core
{
    /// <summary>
    /// 階層構造（多重継承、ネストしたExhaustive）に関するテスト
    /// </summary>
    public class ExhaustiveSwitchAnalyzerHierarchyTests
    {
        /// <summary>
        /// 多重継承: すべての具象型（KingSlime, QueenSlime, Orc）で処理する場合、エラーなし
        /// Slimeはabstractなので、その子クラスをすべて処理すればカバーされる
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
        /// （Slimeはabstractなので、KingSlimeとQueenSlimeがすべて処理されればカバーされる。QueenSlimeのみエラー）
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
        /// Interface + 入れ子のExhaustive: 中間クラスが[Case, Exhaustive]の場合、中間クラスを処理すれば網羅OK
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
        /// Interface + 入れ子のExhaustive: 中間クラスが[Case, Exhaustive]で、子クラスのみ処理した場合、中間クラスが不足
        /// （中間クラスがabstractでない場合、インスタンス化可能なので明示的な処理が必要）
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
        /// Interface + 入れ子のExhaustive: 中間クラスがabstractで[Case, Exhaustive]の場合、子クラスのみで網羅OK
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
        /// 上位インターフェースでまとめて処理する場合、エラーなし
        /// IFlyableインターフェースを実装しているDragonとHarpyをまとめて処理
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
                // DragonとHarpyをまとめて処理
                break;
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// 上位インターフェースと具象型の混在パターン、エラーなし
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
                // DragonとHarpyをまとめて処理
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
        /// 上位インターフェースでの処理があっても、それでカバーされないケースが不足している場合、エラー
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
                // DragonとHarpyのみ処理
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
        /// ネストしたswitchでも網羅性をチェック
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
