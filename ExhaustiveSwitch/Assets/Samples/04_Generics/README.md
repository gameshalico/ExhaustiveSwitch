# 04_Generics - ジェネリクスとの組み合わせ

このサンプルでは、ExhaustiveSwitchをジェネリック型と組み合わせて使う方法を学びます。

## 概要

以下の実用的なパターンを実装します:
1. **Result<T>パターン**: 成功/失敗を表現
2. **Command<TContext>パターン**: コマンドパターンの実装
3. **Event<TPayload>パターン**: イベントシステムの実装

## 学習ポイント

1. **ジェネリックインターフェースとExhaustive**
   - `IResult<T>`のような型でもExhaustiveSwitchが使える
   - 型パラメータは具象クラスで決定される

2. **実践的なパターン**
   - Result型でエラーハンドリング
   - Commandパターンで処理の抽象化
   - Eventシステムで疎結合な実装

3. **型安全性**
   - ジェネリクスとExhaustiveSwitchで完全な型安全性を実現
   - すべてのケースを処理することを強制

## コード例

### Result<T>パターン

```csharp
IResult<int> result = LoadPlayerScore();

switch (result)
{
    case Success<int> success:
        Debug.Log($"スコア: {success.Value}");
        break;
    case Failure<int> failure:
        Debug.LogError($"エラー: {failure.Error}");
        break;
}
```

### Commandパターン

```csharp
ICommand<GameContext> command = GetNextCommand();

switch (command)
{
    case MoveCommand move:
        ExecuteMove(context, move);
        break;
    case AttackCommand attack:
        ExecuteAttack(context, attack);
        break;
    case UseItemCommand useItem:
        ExecuteUseItem(context, useItem);
        break;
}
```

### Eventパターン

```csharp
IGameEvent gameEvent = eventQueue.Dequeue();

switch (gameEvent)
{
    case PlayerEvent playerEvent:
        HandlePlayerEvent(playerEvent);
        break;
    case EnemyEvent enemyEvent:
        HandleEnemyEvent(enemyEvent);
        break;
    case SystemEvent systemEvent:
        HandleSystemEvent(systemEvent);
        break;
}
```