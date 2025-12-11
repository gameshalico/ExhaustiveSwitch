using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.Generics
{
    /// <summary>
    /// ゲームイベントを表すインターフェース
    /// </summary>
    [Exhaustive]
    public interface IGameEvent
    {
        float Timestamp { get; }
    }

    /// <summary>
    /// プレイヤー関連のイベント
    /// </summary>
    [Case]
    public sealed class PlayerEvent : IGameEvent
    {
        public float Timestamp { get; }
        public string EventType { get; }
        public object Data { get; }

        public PlayerEvent(string eventType, object data)
        {
            Timestamp = Time.time;
            EventType = eventType;
            Data = data;
        }
    }

    /// <summary>
    /// 敵関連のイベント
    /// </summary>
    [Case]
    public sealed class EnemyEvent : IGameEvent
    {
        public float Timestamp { get; }
        public int EnemyId { get; }
        public string Action { get; }

        public EnemyEvent(int enemyId, string action)
        {
            Timestamp = Time.time;
            EnemyId = enemyId;
            Action = action;
        }
    }

    /// <summary>
    /// システムイベント
    /// </summary>
    [Case]
    public sealed class SystemEvent : IGameEvent
    {
        public float Timestamp { get; }
        public string Category { get; }
        public string Message { get; }
        public LogType LogLevel { get; }

        public SystemEvent(string category, string message, LogType logLevel = LogType.Log)
        {
            Timestamp = Time.time;
            Category = category;
            Message = message;
            LogLevel = logLevel;
        }
    }
}
