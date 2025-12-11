using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.NestedTypes
{
    /// <summary>
    /// 武器を表すインターフェース
    /// </summary>
    [Exhaustive]
    public interface IWeapon
    {
        string Name { get; }
        int BaseDamage { get; }
        void Use();
    }

    /// <summary>
    /// 近接武器の抽象クラス
    /// [Case]と[Exhaustive]の両方を付けることで、
    /// - IWeaponの具象型の1つである
    /// - さらにこのクラスを継承する型が存在する
    /// ことを示します
    /// </summary>
    [Case, Exhaustive]
    public abstract class MeleeWeapon : IWeapon
    {
        public abstract string Name { get; }
        public abstract int BaseDamage { get; }
        public abstract float AttackRange { get; }

        public void Use()
        {
            Debug.Log($"{Name}を振る! 射程: {AttackRange}m");
        }

        public int CalculateMeleeDamage(float criticalMultiplier)
        {
            return Mathf.RoundToInt(BaseDamage * criticalMultiplier);
        }
    }

    /// <summary>
    /// 遠距離武器の抽象クラス
    /// </summary>
    [Case, Exhaustive]
    public abstract class RangedWeapon : IWeapon
    {
        public abstract string Name { get; }
        public abstract int BaseDamage { get; }
        public abstract float MaxRange { get; }
        public abstract int AmmoCapacity { get; }

        public void Use()
        {
            Debug.Log($"{Name}を発射! 最大射程: {MaxRange}m");
        }

        public int CalculateRangedDamage(float distance)
        {
            float damageMultiplier = 1f - (distance / MaxRange * 0.5f);
            return Mathf.RoundToInt(BaseDamage * Mathf.Max(0.5f, damageMultiplier));
        }
    }

    // === 近接武器の具象クラス ===

    /// <summary>
    /// 剣
    /// </summary>
    [Case]
    public sealed class Sword : MeleeWeapon
    {
        public override string Name => "ロングソード";
        public override int BaseDamage => 30;
        public override float AttackRange => 2.0f;
        public int Sharpness { get; set; } = 100;

        public void Slash()
        {
            Debug.Log($"{Name}で斬撃!");
        }
    }

    /// <summary>
    /// 斧
    /// </summary>
    [Case]
    public sealed class Axe : MeleeWeapon
    {
        public override string Name => "バトルアックス";
        public override int BaseDamage => 45;
        public override float AttackRange => 1.5f;
        public int Weight { get; set; } = 20;

        public void Chop()
        {
            Debug.Log($"{Name}で叩き割る!");
        }
    }

    // === 遠距離武器の具象クラス ===

    /// <summary>
    /// 弓
    /// </summary>
    [Case]
    public sealed class Bow : RangedWeapon
    {
        public override string Name => "ロングボウ";
        public override int BaseDamage => 25;
        public override float MaxRange => 50f;
        public override int AmmoCapacity => 30;
        public float DrawSpeed { get; set; } = 1.2f;

        public void ChargeShot()
        {
            Debug.Log($"{Name}でチャージショット!");
        }
    }

    /// <summary>
    /// クロスボウ
    /// </summary>
    [Case]
    public sealed class Crossbow : RangedWeapon
    {
        public override string Name => "ヘビークロスボウ";
        public override int BaseDamage => 40;
        public override float MaxRange => 60f;
        public override int AmmoCapacity => 10;
        public float ReloadTime { get; set; } = 3.0f;

        public void PiercingShot()
        {
            Debug.Log($"{Name}で貫通ショット!");
        }
    }
}
