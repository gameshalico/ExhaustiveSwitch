# 02_BasicUsage - 基本的な使い方

このサンプルでは、実際のゲーム開発でよくあるシナリオを使ってExhaustiveSwitchの使い方を学びます。

## 概要

敵キャラクターのシステムを例に、以下を実装します:
- 複数の敵タイプ（Goblin, Dragon, Harpy）
- 飛行能力を持つ敵を表す`IFlyable`インターフェース
- 敵の処理を行う`EnemyController`
- 
`[Exhaustive]`属性は、あくまでswitch時に網羅性をチェックするマーカーとしての役割であり、
`IFlyable`インターフェースには付与する必要はありません。

## 学習ポイント

1. **複数のインターフェースの実装**
   - `Dragon`と`Harpy`は`IEnemy`と`IFlyable`の両方を実装
   - switch文でインターフェース型を使って分岐できる

2. **抽象度の選択**
   - 具象型で分岐: すべての敵を個別に処理
   - インターフェース型で分岐: 共通の特性でグループ化

3. **実践的なパターン**
   - ダメージ計算
   - AI処理の切り替え
   - エフェクトの選択

## コード例

```csharp
// 具象型で分岐
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

// インターフェース型で分岐
switch (enemy)
{
    case Goblin goblin:
        // 地上の敵の処理
        break;
    case IFlyable flyable:
        // 飛行する敵の処理（DragonとHarpy）
        break;
}
```
