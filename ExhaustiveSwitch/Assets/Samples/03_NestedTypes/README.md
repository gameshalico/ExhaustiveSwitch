# 03_NestedTypes - ネストした型の扱い

このサンプルでは、階層的な型定義とExhaustiveSwitchの組み合わせを示します。
抽象クラスにするより、別のinterfaceを作成したほうがいいと思いますが、
ExhaustiveSwitch抽象クラスでの使い方を示すために用いています。

## 概要

武器システムを例に、以下の階層構造を実装します:
- `IWeapon`（最上位のインターフェース）
  - `MeleeWeapon`（近接武器の抽象クラス）
    - `Sword`（剣）
    - `Axe`（斧）
  - `RangedWeapon`（遠距離武器の抽象クラス）
    - `Bow`（弓）
    - `Crossbow`（クロスボウ）

## 学習ポイント

1. **階層的な型定義**
   - 抽象クラスに`[Case]`と`[Exhaustive]`の両方を付ける
   - 具象クラスには`[Case]`のみを付ける

2. **異なる抽象度でのswitch**
   - 最下層の具象型で分岐
   - 中間層の抽象クラスで分岐
   - 両方を組み合わせた分岐

3. **実践的なパターン**
   - カテゴリごとに共通処理
   - 個別の武器ごとに固有処理

## コード例

```csharp
// 具象型で完全に分岐
switch (weapon)
{
    case Sword sword:
        // 剣固有の処理
        break;
    case Axe axe:
        // 斧固有の処理
        break;
    case Bow bow:
        // 弓固有の処理
        break;
    case Crossbow crossbow:
        // クロスボウ固有の処理
        break;
}

// カテゴリで分岐
switch (weapon)
{
    case MeleeWeapon melee:
        // 近接武器全般の処理
        break;
    case RangedWeapon ranged:
        // 遠距離武器全般の処理
        break;
}
```