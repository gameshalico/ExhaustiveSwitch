# ExhaustiveSwitch

**ExhaustiveSwitch** は、Roslyn Analyzerを使用して、`switch`文/式における継承階層の網羅性を強制するライブラリです。
`[Exhaustive]`属性と`[Case]`属性を使用することで、
switch文/式で扱うべき型が処理されていないことをエラーとして検出することができます。

複数のアセンブリをまたいでも動作し、型安全に型によるパターンマッチングを行うことを可能にします。

また、CodeFixProvider により、網羅されていないケースを追加することができます。

![](/docs/images/code-fix.png)


## 使用方法

### 基本的な使い方

```csharp
using ExhaustiveSwitch;

// [Exhaustive]属性を付与
[Exhaustive]
public interface IEnemy
{
    void Attack();
}

// 各具象クラスに[Case]属性を付与
[Case]
public class Goblin : IEnemy
{
    public void Attack() { }
}

[Exhaustive]
public interface IFlyable
{
    public void Fly() { }
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

public void ProcessEnemy(IEnemy enemy)
{
    // switch文で明示的にすべての具象型を処理する必要がある
    switch (enemy)
    {
        case Goblin goblin:
            // Goblinの処理
            break;
        case Dragon dragon:
            // Dragonの処理
            break;
        case Harpy harpy:
            // Harpyの処理
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
    }

    // 上位の型で処理することも可能
    switch (enemy)
    {
        case Goblin goblin:
            // Goblinの処理
            break;
        case IFlyable flyable:
            // IFlyableの処理
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
    }
}
```

### エラーメッセージ

すべての`[Case]`型が明示的に処理されていない場合、以下のエラーが発行されます：
尚、上位の型で処理されている場合は、エラーは発行されません。

```
エラー EXH0001: Exhaustive 型 'IEnemy' の 'Dragon' ケースが switch で処理されていません。
```

### 制限

以下のような、複数の型の組み合わせに対しては対応していません
```csharp
switch (sample1, sample2)
{
    case (ConcreteA1 _, ConcreteB1 _):
        Debug.Log("It's (ConcreteA1, ConcreteB1)");
        break;
    case (ConcreteA2 _, ConcreteB2 _):
        Debug.Log("It's (ConcreteA2, ConcreteB2)");
        break;
    default:
        throw new ArgumentOutOfRangeException();
}
```

# セットアップ
## 要件
- Unity 2022.3.12f1 以降

## インストール

Unity Package Managerを使用して、ExhaustiveSwitchをプロジェクトに追加します。

1. Window > Package Management > PackageManager からPackage Managerを開く
2. 左上の「+」ボタンをクリックし、「Add package from git URL...」を選択
3. 以下のURLを入力して「Install」ボタンをクリック

```
https://github.com/gameshalico/ExhaustiveSwitch.git?path=ExhastiveSwitch/Assets/ExhaustiveSwitch
```

