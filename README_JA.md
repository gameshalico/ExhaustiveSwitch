[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE)
# ExhaustiveSwitch

**ExhaustiveSwitch** は、Roslyn Analyzerを使用して、`switch`文/式における継承階層の網羅を強制するライブラリです。

`[Exhaustive]`属性と`[Case]`属性を使用することで、`switch`文/式で扱うべき型が処理されていないことをエラーとして検出することができます。

これにより、以下のようなメリットを得ることができます
- 新しくクラスを追加した際の考慮漏れを防止できる
- 型安全な型による処理の分岐が実現できる (Visitorパターンと違い、抽象レイヤーが具象レイヤーを知る必要がない、抽象的に扱える)

また、CodeFixProvider による、網羅されていないケースの追加にも対応しています。

[English README is here](./README.md)

![](/docs/images/code-fix.png)


## 使用方法

```csharp
using ExhaustiveSwitch;

// [Exhaustive]属性を付与
[Exhaustive]
public interface IEnemy
{
    public void Attack();
}

public interface IWalkable
{
    public void Walk();
}

public interface IFlyable
{
    public void Fly() { }
}

// 各具象クラスに[Case]属性を付与
[Case]
public class Goblin : IEnemy, IWalkable
{
    public void Attack() { }
    public void Walk() { }
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
    // 具象型で分岐 (新たに敵が実装されるとエラー)
    switch (enemy)
    {
        case Goblin goblin:
            // Goblin専用の処理
            break;
        case Dragon dragon:
            // Dragon専用の処理
            break;
        case Harpy harpy:
            // Harpy専用の処理
            break;
    }

    // インターフェース型で分岐 (歩くのでも、飛行するのでもない型が実装されるとエラー)
    switch (enemy)
    {
        case IWalkable walkable:
            // 歩く敵の処理 (Goblin)
            break;
        case IFlyable flyable:
            // 飛行する敵の処理(DragonとHarpy)
            break;
    }
}

```

その他の使用方法は、[Samples](ExhaustiveSwitch/Assets/Samples/README.md) を参照してください。

### エラーメッセージ

すべての`[Case]`型が明示的に処理されていない場合、以下のようなエラーが発行されます。
尚、上位の型で処理されている場合は、エラーは発行されません。

```
エラー EXH0001: Exhaustive 型 'IEnemy' の 'Dragon' ケースが switch で処理されていません。
```

`[Case]`属性が付与された型が`[Exhaustive]`属性を継承/実装していない場合、以下のような警告が発行されます。

```
警告 EXH0002: Case 属性が付与された型 'Goblin' は Exhaustive 型 'IEnemy' を継承/実装していません。
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
https://github.com/gameshalico/ExhaustiveSwitch.git?path=ExhaustiveSwitch/Assets/ExhaustiveSwitch
```

