using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExhaustiveSwitchSamples.Generics
{
    /// <summary>
    /// ジェネリクスとExhaustiveSwitchを組み合わせた使用例
    /// </summary>
    public class GenericExample
    {
        // ===== Result型の使用例 =====

        /// <summary>
        /// プレイヤースコアを読み込む
        /// 失敗する可能性がある処理をResult型で表現
        /// </summary>
        public IResult<int> LoadPlayerScore(string playerId)
        {
            // 実際の実装では、ファイルやネットワークから読み込む
            if (string.IsNullOrEmpty(playerId))
            {
                return Result.Fail<int>("プレイヤーIDが無効です", 400);
            }

            if (playerId == "test")
            {
                return Result.Ok(12345);
            }

            return Result.Fail<int>("プレイヤーが見つかりません", 404);
        }

        /// <summary>
        /// Result型を処理する
        /// すべてのケースを確実に処理する必要がある
        /// </summary>
        public void ProcessScore(IResult<int> result)
        {
            switch (result)
            {
                case Success<int> success:
                    Debug.Log($"スコア読み込み成功: {success.Value}");
                    DisplayScore(success.Value);
                    break;

                case Failure<int> failure:
                    Debug.LogError($"スコア読み込み失敗: {failure.Error} (コード: {failure.ErrorCode})");
                    ShowErrorDialog(failure.Error);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        // ===== Commandパターンの使用例 =====

        private GameContext gameContext = new GameContext
        {
            PlayerPosition = Vector3.zero,
            PlayerHP = 100,
            PlayerMP = 50
        };

        /// <summary>
        /// コマンドを実行する
        /// コマンドの種類に応じて異なる処理を行う
        /// </summary>
        public void ExecuteCommand(ICommand<GameContext> command)
        {
            // 実行可能かチェック
            if (!command.CanExecute(gameContext))
            {
                Debug.LogWarning($"コマンド '{command.Name}' は実行できません");
                return;
            }

            // コマンドの種類に応じて処理
            switch (command)
            {
                case MoveCommand move:
                    gameContext.PlayerPosition += move.Direction.normalized * move.Speed;
                    Debug.Log($"移動: 新しい位置 = {gameContext.PlayerPosition}");
                    break;

                case AttackCommand attack:
                    Debug.Log($"攻撃: 目標 = {attack.TargetPosition}, ダメージ = {attack.Damage}");
                    break;

                case UseItemCommand useItem:
                    string item = gameContext.Inventory[useItem.SlotIndex];
                    Debug.Log($"アイテム使用: {item}");
                    gameContext.Inventory[useItem.SlotIndex] = null;
                    break;

                case CastSkillCommand castSkill:
                    gameContext.PlayerMP -= castSkill.MPCost;
                    Debug.Log($"スキル発動: {castSkill.SkillName} (残りMP: {gameContext.PlayerMP})");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        /// <summary>
        /// コマンドのコストを取得
        /// </summary>
        public int GetCommandCost(ICommand<GameContext> command)
        {
            switch (command)
            {
                case MoveCommand _:
                    return 0; // 移動は無料

                case AttackCommand _:
                    return 5; // スタミナ消費

                case UseItemCommand _:
                    return 3;

                case CastSkillCommand castSkill:
                    return castSkill.MPCost;

                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        // ===== Eventパターンの使用例 =====

        private Queue<IGameEvent> eventQueue = new Queue<IGameEvent>();

        /// <summary>
        /// イベントを処理する
        /// </summary>
        public void ProcessEvent(IGameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case PlayerEvent playerEvent:
                    Debug.Log($"[{playerEvent.Timestamp:F2}] プレイヤーイベント: {playerEvent.EventType}");
                    HandlePlayerEvent(playerEvent);
                    break;

                case EnemyEvent enemyEvent:
                    Debug.Log($"[{enemyEvent.Timestamp:F2}] 敵イベント: 敵ID={enemyEvent.EnemyId}, アクション={enemyEvent.Action}");
                    HandleEnemyEvent(enemyEvent);
                    break;

                case SystemEvent systemEvent:
                    Debug.Log($"[{systemEvent.Timestamp:F2}] システムイベント: [{systemEvent.Category}] {systemEvent.Message}");
                    HandleSystemEvent(systemEvent);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(gameEvent), gameEvent, null);
            }
        }

        /// <summary>
        /// すべてのイベントを処理
        /// </summary>
        public void ProcessAllEvents()
        {
            while (eventQueue.Count > 0)
            {
                IGameEvent gameEvent = eventQueue.Dequeue();
                ProcessEvent(gameEvent);
            }
        }

        // ===== 使用例 =====

        public void Example()
        {
            Debug.Log("=== Result型の例 ===");
            var result1 = LoadPlayerScore("test");
            ProcessScore(result1);

            var result2 = LoadPlayerScore("");
            ProcessScore(result2);

            Debug.Log("\n=== Commandパターンの例 ===");
            ICommand<GameContext>[] commands = new ICommand<GameContext>[]
            {
                new MoveCommand(Vector3.forward, 5f),
                new AttackCommand(new Vector3(10, 0, 10), 25),
                new CastSkillCommand("ファイアボール", 20)
            };

            foreach (var command in commands)
            {
                Debug.Log($"コマンド: {command.Name}, コスト: {GetCommandCost(command)}");
                ExecuteCommand(command);
            }

            Debug.Log("\n=== Eventパターンの例 ===");
            eventQueue.Enqueue(new PlayerEvent("LevelUp", 10));
            eventQueue.Enqueue(new EnemyEvent(1, "Spawn"));
            eventQueue.Enqueue(new SystemEvent("Game", "ゲーム開始"));

            ProcessAllEvents();
        }

        // ヘルパーメソッド
        private void DisplayScore(int score) { }
        private void ShowErrorDialog(string error) { }
        private void HandlePlayerEvent(PlayerEvent evt) { }
        private void HandleEnemyEvent(EnemyEvent evt) { }
        private void HandleSystemEvent(SystemEvent evt) { }
    }
}
