using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.Generics
{
    /// <summary>
    /// ゲームコンテキスト（コマンドが操作する対象）
    /// </summary>
    public class GameContext
    {
        public Vector3 PlayerPosition { get; set; }
        public int PlayerHP { get; set; } = 100;
        public int PlayerMP { get; set; } = 50;
        public string[] Inventory { get; set; } = new string[10];
    }

    /// <summary>
    /// コマンドを表すインターフェース
    /// TContextは、このコマンドが操作する対象の型
    /// </summary>
    [Exhaustive]
    public interface ICommand<TContext>
    {
        string Name { get; }
        bool CanExecute(TContext context);
    }

    /// <summary>
    /// 移動コマンド
    /// </summary>
    [Case]
    public sealed class MoveCommand : ICommand<GameContext>
    {
        public string Name => "移動";
        public Vector3 Direction { get; set; }
        public float Speed { get; set; }

        public MoveCommand(Vector3 direction, float speed)
        {
            Direction = direction;
            Speed = speed;
        }

        public bool CanExecute(GameContext context)
        {
            return true; // 移動は常に可能
        }
    }

    /// <summary>
    /// 攻撃コマンド
    /// </summary>
    [Case]
    public sealed class AttackCommand : ICommand<GameContext>
    {
        public string Name => "攻撃";
        public Vector3 TargetPosition { get; set; }
        public int Damage { get; set; }

        public AttackCommand(Vector3 targetPosition, int damage)
        {
            TargetPosition = targetPosition;
            Damage = damage;
        }

        public bool CanExecute(GameContext context)
        {
            return context.PlayerHP > 0;
        }
    }

    /// <summary>
    /// アイテム使用コマンド
    /// </summary>
    [Case]
    public sealed class UseItemCommand : ICommand<GameContext>
    {
        public string Name => "アイテム使用";
        public string ItemName { get; set; }
        public int SlotIndex { get; set; }

        public UseItemCommand(string itemName, int slotIndex)
        {
            ItemName = itemName;
            SlotIndex = slotIndex;
        }

        public bool CanExecute(GameContext context)
        {
            return SlotIndex >= 0 &&
                   SlotIndex < context.Inventory.Length &&
                   context.Inventory[SlotIndex] != null;
        }
    }

    /// <summary>
    /// スキル発動コマンド
    /// </summary>
    [Case]
    public sealed class CastSkillCommand : ICommand<GameContext>
    {
        public string Name => "スキル発動";
        public string SkillName { get; set; }
        public int MPCost { get; set; }

        public CastSkillCommand(string skillName, int mpCost)
        {
            SkillName = skillName;
            MPCost = mpCost;
        }

        public bool CanExecute(GameContext context)
        {
            return context.PlayerMP >= MPCost && context.PlayerHP > 0;
        }
    }
}
