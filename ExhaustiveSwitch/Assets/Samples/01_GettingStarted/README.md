# 01_GettingStarted - はじめてのExhaustiveSwitch

このサンプルでは、ExhaustiveSwitchの基本的な使い方を学びます。

## 概要

`Shape`という図形のインターフェースを定義し、それぞれの図形を表す具象クラスを作成します。
`ShapeRenderer`では、すべての図形を確実に処理するためにExhaustiveSwitchを使用します。

## 学習ポイント

1. `[Exhaustive]`属性をインターフェースに付ける
2. `[Case]`属性を各具象クラスに付ける
3. switch文ですべてのケースを処理する必要がある
4. ケースが不足している場合、コンパイルエラーが発生する

## 使い方

1. `Shape.cs`で定義されている図形の種類を確認する
2. `ShapeRenderer.cs`のRenderメソッドを見る
3. 試しに`Circle`のcaseをコメントアウトしてみる → エラーが発生する
4. 新しい図形（例: `Triangle`）を追加してみる → すべてのswitch文でエラーが発生する

