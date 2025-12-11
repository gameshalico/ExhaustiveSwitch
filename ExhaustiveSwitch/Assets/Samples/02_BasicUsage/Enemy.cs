using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.BasicUsage
{
    /// <summary>
    /// 敵キャラクターを表すインターフェース
    /// </summary>
    [Exhaustive]
    public interface IEnemy
    {
        string Name { get; }
        int HP { get; }
        void Attack();
    }

    /// <summary>
    /// 飛行能力を持つ敵を表すインターフェース
    /// </summary>
    public interface IFlyable
    {
        float FlyHeight { get; }
        void Fly();
    }

    /// <summary>
    /// ゴブリン（地上の敵）
    /// </summary>
    [Case]
    public class Goblin : IEnemy
    {
        public string Name => "ゴブリン";
        public int HP { get; set; } = 50;
        public int ClubDamage { get; set; } = 10;

        public void Attack()
        {
            Debug.Log($"{Name}が棍棒で攻撃! ダメージ: {ClubDamage}");
        }

        public void ThrowRock()
        {
            Debug.Log($"{Name}が石を投げた!");
        }
    }

    /// <summary>
    /// ドラゴン（空を飛ぶ敵）
    /// </summary>
    [Case]
    public class Dragon : IEnemy, IFlyable
    {
        public string Name => "ドラゴン";
        public int HP { get; set; } = 200;
        public float FlyHeight { get; set; } = 50f;
        public int FireBreathDamage { get; set; } = 50;

        public void Attack()
        {
            Debug.Log($"{Name}が火を噴いた! ダメージ: {FireBreathDamage}");
        }

        public void Fly()
        {
            Debug.Log($"{Name}が高度{FlyHeight}mで飛行中");
        }

        public void Roar()
        {
            Debug.Log($"{Name}が咆哮した!");
        }
    }

    /// <summary>
    /// ハーピー（空を飛ぶ敵）
    /// </summary>
    [Case]
    public class Harpy : IEnemy, IFlyable
    {
        public string Name => "ハーピー";
        public int HP { get; set; } = 80;
        public float FlyHeight { get; set; } = 30f;
        public int ClawDamage { get; set; } = 20;

        public void Attack()
        {
            Debug.Log($"{Name}が爪で攻撃! ダメージ: {ClawDamage}");
        }

        public void Fly()
        {
            Debug.Log($"{Name}が高度{FlyHeight}mで飛行中");
        }

        public void Screech()
        {
            Debug.Log($"{Name}が金切り声を上げた!");
        }
    }
}
