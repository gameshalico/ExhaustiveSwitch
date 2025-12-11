# ExhaustiveSwitch サンプル集

このディレクトリには、ExhaustiveSwitchの使い方を学ぶためのサンプルが含まれています。

## 📚 サンプル一覧

### [01_GettingStarted](01_GettingStarted/) - はじめてのExhaustiveSwitch
- **内容**: ExhaustiveSwitchの基本概念を学ぶ最もシンプルなサンプル
- **主なトピック**:
  - `[Exhaustive]`属性の使い方
  - `[Case]`属性の使い方
  - switch文での網羅性チェック

### [02_BasicUsage](02_BasicUsage/) - 基本的な使い方
- **内容**: 実際のゲーム開発でよくあるシナリオでの使い方
- **主なトピック**:
  - 複数のインターフェースの実装
  - 抽象度の選択（具象型 vs インターフェース型）
  - 実践的なパターン

### [03_NestedTypes](03_NestedTypes/) - ネストした型の扱い
- **内容**: 階層的な型定義とExhaustiveSwitchの組み合わせ
- **主なトピック**:
  - 抽象クラスに`[Case]`と`[Exhaustive]`の両方を付ける
  - 異なる抽象度でのswitch
  - カテゴリごとの共通処理

### [04_Generics](04_Generics/) - ジェネリクスとの組み合わせ
- **内容**: ジェネリック型とExhaustiveSwitchの組み合わせ
- **主なトピック**:
  - `Result<T>`パターン
  - `Command<TContext>`パターン
  - `Event<TPayload>`パターン
  - 型安全なエラーハンドリング

### [05_MultiAssembly](05_MultiAssembly/) - マルチアセンブリでの使用
- **内容**: 複数のアセンブリに分割されたプロジェクトでの使用方法
- **主なトピック**:
  - アセンブリ分割のメリット
  - 参照アセンブリからの型検出
  - 大規模プロジェクトでの設計パターン

### [06_AdvancedPatterns](06_AdvancedPatterns/) - 高度なデザインパターン
- **内容**: ExhaustiveSwitchを活用した高度なデザインパターン
- **主なトピック**:
  - ステートマシンパターン
  - コマンドパターン（Undo/Redo）
  - 式木（Expression Tree）パターン

## 💡 よくある質問

### Q: ExhaustiveSwitchを使うメリットは？
A:
- 新しい型を追加したときにコンパイルエラーで通知される
- すべてのケースを処理し忘れることがない
- リファクタリングが安全になる

### Q: パフォーマンスへの影響は？
A:
- 実行時のオーバーヘッドはほぼゼロ
- コンパイル時のチェックのみ

### Q: 既存のプロジェクトに導入できる？
A:
- 段階的に導入可能
- 重要な部分から徐々に適用することを推奨

### Q: default節は必要？
A:
- はい、ExhaustiveSwitchでもdefault節は必要
- 通常は`throw new ArgumentOutOfRangeException()`を書きます
