# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

**ExhaustiveImplementationAnalyzer** は、C#のRoslyn Analyzerです。`[Exhaustive]`属性と`[ExhaustiveCase]`属性を使用して、`switch`文/式における継承階層の網羅性をコンパイル時に検証します。

### 目的
- `[Exhaustive]`属性を持つインターフェース/抽象クラスに対するswitch処理で、`[ExhaustiveCase]`属性を持つすべての具象型が明示的に処理されているかを検証
- 網羅性が不足している場合、コンパイルエラー（EIA0001）を発行
- `default`やディスカードパターン(`_`)があっても、明示的な具象型の処理が不足していればエラーを出力（意図的に網羅性を強制）

## ビルドとテストのコマンド

### ビルド
```bash
dotnet build
```

### テスト実行
```bash
dotnet test
```

### 特定のテストのみ実行
```bash
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

## アーキテクチャ

### プロジェクト構成
- **ターゲットフレームワーク**: .NET Standard 2.0
- **依存パッケージ**: Microsoft.CodeAnalysis.CSharp 4.3.0
- **言語バージョン**: C# 8.0
- **テストフレームワーク**: xUnit (.NET 8.0)

### コア機能の実装方針

#### 1. 属性の定義
- `[Exhaustive]`: インターフェース/抽象クラスに付与され、網羅性検証のルートを示す
- `[ExhaustiveCase]`: 具象クラスに付与され、switchで明示的に処理されるべきケースを示す

#### 2. 検証ロジック
Analyzerは以下のステップで検証を行う必要があります：

1. **$S_{expected}$の特定**: プロジェクト内および参照アセンブリから、`[Exhaustive]`型を継承/実装し、かつ`[ExhaustiveCase]`属性を持つすべての具象クラスを収集
2. **$S_{actual}$の特定**: switch文/式のパターンから、明示的に処理されている具象型を抽出
3. **エラー判定**: $S_{expected} \setminus S_{actual} \neq \emptyset$ の場合、診断ID **EIA0001** (Error) を発行

#### 3. 重要な制約
- **上位型マッチングは網羅に含まれない**: `case IEnemy e:` のような上位型でのマッチングは、明示的な具象型処理として扱わない
- **default/_は網羅を補完しない**: これらのパターンが存在しても、明示的な具象型処理が不足していればエラーを出力
- **クロスアセンブリ対応**: 参照アセンブリ内の`[ExhaustiveCase]`型も検出する必要がある
- **ジェネリクス対応**: `IEnemy<T>`のようなジェネリック型でも、型パラメータを考慮して正確に検証

## 診断メッセージ

| 診断ID | レベル | メッセージ |
|--------|--------|-----------|
| EIA0001 | Error | Exhaustive 型 '{0}' の '{1}' ケースが switch で処理されていません。 |

## 主要ファイルの構成

### Analyzerプロジェクト (ExhaustiveImplementationAnalyzer/)
- **ExhaustiveImplementationAnalyzer.cs**: メインのAnalyzer実装
  - `CompilationStartAction`で全[ExhaustiveCase]型をキャッシュ
  - switch文/式ごとに網羅性を検証
- **ExhaustiveAttribute.cs**: [Exhaustive]属性の定義
- **ExhaustiveCaseAttribute.cs**: [ExhaustiveCase]属性の定義

### テストプロジェクト (ExhaustiveImplementationAnalyzer.Tests/)
- xUnitとRoslyn Analyzer Testing frameworkを使用
- `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`による統合テスト

## 実装の重要ポイント

### キャッシング戦略
Analyzerは`CompilationStartAction`で以下のキャッシュを構築します：
```csharp
ConcurrentDictionary<INamedTypeSymbol, ConcurrentBag<INamedTypeSymbol>> exhaustiveCaseCache
```
- キー: [Exhaustive]型（例: IEnemy）
- 値: その型を継承/実装する[ExhaustiveCase]型のコレクション
- これによりswitch解析時に都度スキャンする必要がなくなる

### 型の比較
- `SymbolEqualityComparer.Default`を使用してジェネリクス型パラメータを含めた厳密な型比較を実施
- `OriginalDefinition`を使用して構築されたジェネリック型（例: `List<int>`）を未構築型（例: `List<T>`）に変換して比較

## 参考資料

詳細な仕様は `PROJECT_GUIDE.md` を参照してください。
