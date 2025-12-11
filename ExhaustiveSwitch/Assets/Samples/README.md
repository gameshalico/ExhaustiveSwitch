# ExhaustiveSwitch サンプル集

このディレクトリには、ExhaustiveSwitchの使い方を学ぶためのサンプルが含まれています。

## サンプル一覧

### [01_GettingStarted](01_GettingStarted/) - 最小限のサンプル
- **内容**: ExhaustiveSwitchの基本概念を学ぶ最もシンプルなサンプル
  - `[Exhaustive]`属性の使い方
  - `[Case]`属性の使い方
  - switch文での網羅性チェック

### [02_BasicUsage](02_BasicUsage/) - 基本的な使い方
- **内容**: 実際のゲーム開発でよくあるシナリオでの使い方
  - 複数のインターフェースの実装
  - 抽象度の選択（具象型 vs インターフェース型）
  - 実践的なパターン

### [03_NestedTypes](03_NestedTypes/) - ネストした型の扱い
- **内容**: 階層的な型定義とExhaustiveSwitchの組み合わせ
  - 抽象クラスに`[Case]`と`[Exhaustive]`の両方を付ける
  - 異なる抽象度でのswitch
  - カテゴリごとの共通処理

### [04_Generics](04_Generics/) - ジェネリクスとの組み合わせ
- **内容**: ジェネリック型とExhaustiveSwitchの組み合わせ
  - `Result<T>`パターン
  - `Command<TContext>`パターン
  - `Event<TPayload>`パターン
  - 型安全なエラーハンドリング

### [05_MultiAssembly](05_MultiAssembly/) - マルチアセンブリでの使用
- **内容**: 複数のアセンブリに分割されたプロジェクトでの使用方法
  - アセンブリ分割のメリット
  - 参照アセンブリからの型検出
  - 大規模プロジェクトでの設計パターン

### [06_AdvancedPatterns](06_AdvancedPatterns/) - 高度なデザインパターン
- **内容**: ExhaustiveSwitchを活用した高度なデザインパターン
  - ステートマシンパターン
  - コマンドパターン（Undo/Redo）
  - 式木（Expression Tree）パターン
