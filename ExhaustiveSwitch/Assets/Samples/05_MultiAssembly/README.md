# 05_MultiAssembly - マルチアセンブリでの使用

このサンプルでは、複数のアセンブリに分割されたプロジェクトでExhaustiveSwitchを使う方法を学びます。

## 概要

実際の大規模プロジェクトを想定し、以下のアセンブリ構成で実装します:
- **Core**: インターフェース定義（ICharacter）
- **Entities**: 具象クラスの実装（Player, Enemy, NPC）
- **GameLogic**: ビジネスロジック（CharacterProcessor）

## アセンブリ構成

```
05_MultiAssembly/
├── Core/
│   ├── ICharacter.cs
│   └── MultiAssemblySample.Core.asmdef
├── Entities/
│   ├── Player.cs
│   ├── Enemy.cs
│   ├── NPC.cs
│   └── MultiAssemblySample.Entities.asmdef
└── GameLogic/
    ├── CharacterProcessor.cs
    └── MultiAssemblySample.GameLogic.asmdef
```

## 学習ポイント

1. **アセンブリ分割のメリット**
   - インターフェースと実装の分離
   - コンパイル時間の短縮
   - 依存関係の明確化

2. **ExhaustiveSwitchの動作**
   - 参照アセンブリ内の[Case]型も自動検出
   - アセンブリをまたいでも網羅性チェックが機能
   - インターフェース定義と実装が分離されていてもOK

3. **実践的な設計**
   - Core: 共通インターフェース
   - Entities: ドメインモデル
   - GameLogic: ビジネスロジック

## アセンブリ参照関係

```
GameLogic
  ↓ (参照)
  ├─ Core
  └─ Entities
       ↓ (参照)
       └─ Core
```

## 重要な注意点

ExhaustiveSwitchは、**参照しているアセンブリ**から[Case]型を検出します。
そのため、以下のような設計が必要です:

- `ICharacter`を定義するアセンブリ（Core）を参照する
- `Player`, `Enemy`, `NPC`を定義するアセンブリ（Entities）を参照する
- switch文を書くアセンブリ（GameLogic）は両方を参照する
