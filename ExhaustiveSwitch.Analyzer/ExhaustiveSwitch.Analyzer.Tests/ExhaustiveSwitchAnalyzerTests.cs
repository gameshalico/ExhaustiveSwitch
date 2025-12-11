using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Xunit;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Linq;
using System.Collections.Generic;

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
        /// whenガード条件を持つパターンでも網羅性をチェック
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
        /// プロパティパターンを使用している場合でも網羅性をチェック
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

        /// <summary>
        /// Exhaustive属性のないインターフェースはチェックされない
        /// </summary>
        [Fact]
        public async Task WhenNoExhaustiveAttribute_NoDiagnostic()
        {
            var test = @"
using ExhaustiveSwitch;

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
        }
    }
}";

            await VerifyAnalyzerAsync(test);
        }

        /// <summary>
        /// RecursivePatternでも型を正しく認識
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
        /// クロスアセンブリ: 別アセンブリの[Case]型を検出
        /// </summary>
        [Fact]
        public async Task WhenCaseTypeInReferencedAssembly_Diagnostic()
        {
            // 参照アセンブリのコード（ライブラリ側）
            var libraryCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // メインプロジェクトのコード
            var mainCode = @"
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

            await VerifyCrossAssemblyAnalyzerAsync(libraryCode, mainCode, expected);
        }

        /// <summary>
        /// クロスアセンブリ: 別アセンブリですべてのケースが処理されている場合、エラーなし
        /// </summary>
        [Fact]
        public async Task WhenAllCasesFromReferencedAssembly_NoDiagnostic()
        {
            // 参照アセンブリのコード（ライブラリ側）
            var libraryCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // メインプロジェクトのコード
            var mainCode = @"
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

            await VerifyCrossAssemblyAnalyzerAsync(libraryCode, mainCode);
        }

        /// <summary>
        /// Transitive参照: A → B → Attributesの間接参照で[Case]型を検出
        /// </summary>
        [Fact]
        public async Task WhenTransitiveReference_Diagnostic()
        {
            // ライブラリBのコード（Attributesを直接参照）
            var libraryBCode = @"
using ExhaustiveSwitch;

[Exhaustive]
public interface IEnemy { }

[Case]
public sealed class Goblin : IEnemy { }

[Case]
public sealed class Orc : IEnemy { }
";

            // ライブラリCのコード（ライブラリBを参照、Attributesは間接参照）
            var libraryCCode = @"
[ExhaustiveSwitch.Case]
public sealed class Dragon : IEnemy { }
";

            // メインプロジェクトのコード（ライブラリBとCを参照）
            var mainCode = @"
public class Program
{
    public void Process(IEnemy enemy)
    {
        {|#0:switch (enemy)
        {
            case Goblin g:
                break;
            case Orc o:
                break;
        }|}
    }
}";

            var expected = new DiagnosticResult("EXH0001", DiagnosticSeverity.Error)
                .WithLocation(0)
                .WithArguments("IEnemy", "Dragon");

            await VerifyTransitiveReferenceAnalyzerAsync(libraryBCode, libraryCCode, mainCode, expected);
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

        /// <summary>
        /// クロスアセンブリテスト用のヘルパーメソッド
        /// </summary>
        private static async Task VerifyCrossAssemblyAnalyzerAsync(string libraryCode, string mainCode, params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveSwitchAnalyzer, DefaultVerifier>
            {
                TestCode = mainCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            // ライブラリコードをコンパイルしてMetadataReferenceとして追加
            var libraryReference = await CompileToMetadataReferenceAsync("LibraryAssembly", libraryCode);
            test.TestState.AdditionalReferences.Add(libraryReference);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        /// <summary>
        /// Transitive参照テスト用のヘルパーメソッド
        /// </summary>
        private static async Task VerifyTransitiveReferenceAnalyzerAsync(
            string libraryBCode,
            string libraryCCode,
            string mainCode,
            params DiagnosticResult[] expected)
        {
            var test = new CSharpAnalyzerTest<ExhaustiveSwitchAnalyzer, DefaultVerifier>
            {
                TestCode = mainCode,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            };

            // Analyzerプロジェクト自体を参照に追加（属性を使用するため）
            test.TestState.AdditionalReferences.Add(typeof(ExhaustiveAttribute).Assembly);

            // ライブラリB（Attributesを直接参照）をコンパイル
            var libraryBReference = await CompileToMetadataReferenceAsync("LibraryB", libraryBCode);

            // ライブラリC（ライブラリBを参照、Attributesは間接参照）をコンパイル
            var libraryCReference = await CompileToMetadataReferenceAsync("LibraryC", libraryCCode, libraryBReference);

            // メインプロジェクトに両方のライブラリを追加
            test.TestState.AdditionalReferences.Add(libraryBReference);
            test.TestState.AdditionalReferences.Add(libraryCReference);

            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
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
        /// コードをコンパイルしてMetadataReferenceを作成
        /// </summary>
        private static async Task<MetadataReference> CompileToMetadataReferenceAsync(string assemblyName, string code, params MetadataReference[] additionalReferences)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            // ReferenceAssembliesから参照を取得
            var referenceAssemblies = ReferenceAssemblies.Net.Net60;
            var resolvedReferences = await referenceAssemblies.ResolveAsync(LanguageNames.CSharp, default);

            // 基本的な参照を追加
            var references = new List<MetadataReference>(resolvedReferences);
            references.Add(MetadataReference.CreateFromFile(typeof(ExhaustiveAttribute).Assembly.Location));
            references.AddRange(additionalReferences);

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var failures = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
                throw new System.Exception($"Compilation failed: {string.Join(", ", failures.Select(d => d.GetMessage()))}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            return MetadataReference.CreateFromStream(ms);
        }
    }
}
