# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

**ExhaustiveSwitchAnalyzer** は、C#のRoslyn Analyzerです。`[Exhaustive]`属性と`[Case]`属性を使用して、`switch`文/式における継承階層の網羅性をコンパイル時に検証します。

### 目的
- `[Exhaustive]`属性を持つインターフェース/抽象クラスに対するswitch処理で、`[Case]`属性を持つすべての具象型が明示的に処理されているかを検証
- 網羅性が不足している場合、コンパイルエラー（EXH0001）を発行
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

### ソリューション構成

このソリューションは3つのプロジェクトで構成されています：

#### 1. ExhaustiveSwitch.Attributes
- **役割**: `[Exhaustive]`と`[Case]`属性の定義
- **ターゲットフレームワーク**: .NET Standard 2.0
- **依存関係**: なし
- **配置先**: Unity側とAnalyzer側の両方で使用

#### 2. ExhaustiveSwitch.Analyzer
- **役割**: Roslyn Analyzerとコードフィックスの実装
- **ターゲットフレームワーク**: .NET Standard 2.0
- **依存パッケージ**:
  - Microsoft.CodeAnalysis.CSharp 4.3.0
- **言語バージョン**: C# 8.0

#### 3. ExhaustiveSwitch.Analyzer.Tests
- **役割**: ユニットテストと統合テスト
- **ターゲットフレームワーク**: .NET 8.0
- **テストフレームワーク**: xUnit
- **依存パッケージ**:
  - Microsoft.CodeAnalysis.CSharp.Analyzer.Testing
  - ExhaustiveSwitch.Analyzer (プロジェクト参照)

### コア機能の実装方針

#### 1. 属性の定義
- `[Exhaustive]`: インターフェース/抽象クラスに付与され、網羅性検証のルートを示す
- `[Case]`: 具象クラスに付与され、switchで明示的に処理されるべきケースを示す

#### 2. 検証ロジック
Analyzerは以下のステップで検証を行う必要があります：

1. **$S_{expected}$の特定**: プロジェクト内および参照アセンブリから、`[Exhaustive]`型を継承/実装し、かつ`[Case]`属性を持つすべての具象クラスを収集
2. **$S_{actual}$の特定**: switch文/式のパターンから、明示的に処理されている具象型を抽出
3. **エラー判定**: $S_{expected} \setminus S_{actual} \neq \emptyset$ の場合、診断ID **EXH0001** (Error) を発行

#### 3. 重要な制約
- **上位型マッチングの処理**: `case IFlyable f:` のような共通インターフェースでマッチする場合、そのインターフェースを実装するすべての[Case]型がカバーされる
- **default/_は網羅を補完しない**: これらのパターンが存在しても、明示的な具象型処理が不足していればエラーを出力
- **クロスアセンブリ対応**: 参照アセンブリ内の`[Case]`型も検出する必要がある
- **ジェネリクス対応**: `IResult<T>`のようなジェネリック型では、型引数を適用した構築型（例: `Success<int>`, `Failure<int>`）で検証

## 診断メッセージ

| 診断ID | レベル | メッセージ |
|--------|--------|-----------|
| EXH0001 | Error | Exhaustive 型 '{0}' の '{1}' ケースが switch で処理されていません。 |

## フォルダ構成

```
ExhaustiveSwitch.Analyzer/
├─ ExhaustiveSwitch.Attributes/
│   ├─ ExhaustiveAttribute.cs         # [Exhaustive]属性の定義
│   └─ CaseAttribute.cs               # [Case]属性の定義
│
├─ ExhaustiveSwitch.Analyzer/
│   ├─ Core/                          # Analyzer本体
│   │   ├─ ExhaustiveSwitchAnalyzer.cs
│   │   ├─ ExhaustiveSwitchCodeFixProvider.cs
│   │   └─ ExhaustiveHierarchyInfo.cs # 継承階層情報の管理
│   │
│   ├─ Helpers/                       # ヘルパークラス群
│   │   ├─ TypeAnalysisHelpers.cs    # 型分析関連のヘルパー（ジェネリック対応含む）
│   │   ├─ MetadataHelpers.cs        # メタデータ探索のヘルパー
│   │   ├─ DiagnosticHelpers.cs      # 診断作成のヘルパー
│   │   └─ CodeGenerationHelpers.cs  # コード生成のヘルパー
│   │
│   └─ Resources/                     # リソースファイル
│       ├─ Resources.resx             # ローカライズ可能な文字列
│       └─ Resources.Designer.cs      # リソースアクセス用コード
│
└─ ExhaustiveSwitch.Analyzer.Tests/
    ├─ Core/                          # コア機能のテスト
    │   ├─ ExhaustiveSwitchAnalyzerBasicTests.cs       # 基本機能テスト
    │   ├─ ExhaustiveSwitchAnalyzerHierarchyTests.cs   # 階層構造テスト
    │   ├─ ExhaustiveSwitchAnalyzerPatternTests.cs     # パターンマッチングテスト
    │   ├─ ExhaustiveSwitchAnalyzerCrossAssemblyTests.cs # クロスアセンブリテスト
    │   ├─ ExhaustiveSwitchAnalyzerGenericsTests.cs    # ジェネリック型テスト
    │   ├─ ExhaustiveHierarchyInfoTests.cs
    │   └─ ExhaustiveSwitchCodeFixTests.cs
    │
    └─ Helpers/                       # ヘルパークラスのテスト
        ├─ TypeAnalysisHelpersTests.cs
        ├─ MetadataHelpersTests.cs
        ├─ DiagnosticHelpersTests.cs
        └─ CodeGenerationHelpersTests.cs
```

### 主要ファイルの説明

#### ExhaustiveSwitch.Attributes/
- Unity側とAnalyzer側の両方で参照される共有プロジェクト
- 依存関係がないため、どの環境でも使用可能

#### ExhaustiveSwitch.Analyzer/Core/
- **ExhaustiveSwitchAnalyzer.cs**: メインのAnalyzer実装
  - `CompilationStartAction`で全[Case]型をキャッシュ（`BuildInheritanceMap`）
  - ジェネリック型の場合は型定義（`OriginalDefinition`）をキーとして使用
  - switch文/式ごとに網羅性を検証（`AnalyzeSwitchConstruct`）
- **ExhaustiveSwitchCodeFixProvider.cs**: コードフィックスの実装
  - 不足しているcaseを自動追加する機能
- **ExhaustiveHierarchyInfo.cs**: 継承階層の情報を管理
  - `AllCases`: すべての[Case]型のセット
  - `DirectChildrenMap`, `DirectParentsMap`: 親子関係のマップ
  - `ApplyTypeArguments`: ジェネリック型に型引数を適用して構築型を生成

#### ExhaustiveSwitch.Analyzer/Helpers/
- **TypeAnalysisHelpers.cs**: 型の継承関係や網羅性の分析
  - `IsImplementingOrDerivedFrom`: ジェネリック型の型引数も考慮した継承判定
  - `IsGenericTypeMatch`: ジェネリック型の一致判定（型定義と型引数の両方をチェック）
  - `ExtractTypeFromPattern`: switch文/式のパターンから型情報を抽出
- **MetadataHelpers.cs**: 参照アセンブリからの属性付き型の検索
- **DiagnosticHelpers.cs**: 診断情報の作成
- **CodeGenerationHelpers.cs**: CodeFixでのコード生成ロジック

#### ExhaustiveSwitch.Analyzer/Resources/
- **Resources.resx**: 診断メッセージやコードフィックスタイトルの定義
- **Resources.Designer.cs**: リソースへの型付きアクセスを提供

#### ExhaustiveSwitch.Analyzer.Tests/
- xUnitとRoslyn Analyzer Testing frameworkを使用
- `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`による統合テスト
- Analyzerとコードフィックスの両方をテスト
- テストは機能ごとにファイルを分割:
  - **BasicTests**: 基本的なswitch文/式の網羅性チェック
  - **HierarchyTests**: 多重継承、ネストしたExhaustive、上位インターフェースでの処理
  - **PatternTests**: whenガード、プロパティパターン、RecursivePattern
  - **CrossAssemblyTests**: 参照アセンブリの型検出、Transitive参照
  - **GenericsTests**: ジェネリック型の網羅性チェック（`IResult<int>`など）

## 実装の重要ポイント

### キャッシング戦略
Analyzerは`CompilationStartAction`で以下のキャッシュを構築します：
```csharp
ConcurrentDictionary<INamedTypeSymbol, ExhaustiveHierarchyInfo> hierarchyInfoMap
```
- **キー**: [Exhaustive]型の型定義（ジェネリックの場合は`OriginalDefinition`）
  - 例: `IResult<T>` → キーは `IResult<T>`（型定義）
- **値**: `ExhaustiveHierarchyInfo`
  - `AllCases`: その型を継承/実装する[Case]型のセット（型定義）
  - `DirectChildrenMap`, `DirectParentsMap`: 親子関係のマップ
- これによりswitch解析時に都度スキャンする必要がなくなる

### ジェネリック型の処理
1. **型定義でのキャッシング**: `Success<T>`, `Failure<T>` は型定義として`hierarchyInfoMap`に格納
2. **実行時の型引数適用**: switch文で `IResult<int>` を処理する際、`ApplyTypeArguments`で型引数を適用
   - `Success<T>` → `Success<int>`
   - `Failure<T>` → `Failure<int>`
3. **型比較**: `IsGenericTypeMatch`で型定義と型引数の両方を厳密に比較
   - `Success<int>` と `Success<string>` は異なる型として扱う
   - `SymbolEqualityComparer.Default`を使用

## ジェネリック型のサポート

### 対応している機能
- **ジェネリックインターフェース/抽象クラス**: `[Exhaustive] interface IResult<T>`
- **ジェネリック[Case]型**: `[Case] class Success<T> : IResult<T>`
- **複数の型パラメータ**: `IEither<TLeft, TRight>`
- **構築型での網羅性チェック**: `IResult<int>` のswitchで `Success<int>` と `Failure<int>` を検証

### 実装例
```csharp
[Exhaustive]
public interface IResult<T> { }

[Case]
public sealed class Success<T> : IResult<T>
{
    public T Value { get; }
}

[Case]
public sealed class Failure<T> : IResult<T>
{
    public string Error { get; }
}

// switch文での使用
public void Process(IResult<int> result)
{
    switch (result)
    {
        case Success<int> s:  // 型引数を明示
            Console.WriteLine(s.Value);
            break;
        case Failure<int> f:  // 型引数を明示
            Console.WriteLine(f.Error);
            break;
    }
}
```

### 注意点
- switch文/式では**型引数を明示**する必要があります（`Success<int>`, `Failure<int>`）
- 型引数が異なれば別の型として扱われます（`Success<int>` ≠ `Success<string>`）
- 非ジェネリック型とジェネリック型の混在も可能

## Unity側への配置

### 必要なファイル
Unity側では以下のファイルが必要です：

1. **ExhaustiveSwitch.Attributes.dll**
   - `ExhaustiveSwitch.Attributes/bin/Release/netstandard2.0/ExhaustiveSwitch.Attributes.dll`
   - Unityプロジェクトの`Assets/Plugins/`に配置

2. **ExhaustiveSwitch.Analyzer.dll** (Roslyn Analyzer)
   - `ExhaustiveSwitch.Analyzer/bin/Release/netstandard2.0/ExhaustiveSwitch.Analyzer.dll`
   - Unityプロジェクトの`Assets/RoslynAnalyzers/`に配置
   - または、NuGetパッケージとして配布

### 配置の注意点
- **Attributes.dll**: Unity側のゲームコードから参照されるため、Pluginsフォルダに配置
- **Analyzer.dll**: コンパイル時のみ使用されるため、通常のPluginsフォルダとは別に配置
- Attributes.dllは軽量で依存関係がないため、Unity側への影響は最小限

### リソースファイルの注意点
- `Resources/Resources.Designer.cs`の42行目で、リソース名を`"ExhaustiveSwitch.Analyzer.Resources.Resources"`に設定
- フォルダ構成を変更した場合は、この名前を一致させる必要がある
