using System;
using UnityEngine;

namespace ExhaustiveSwitchSamples.NestedTypes
{
    /// <summary>
    /// 武器の制御を行うクラス
    /// 階層的な型定義を様々な抽象度で処理する例を示します
    /// </summary>
    public class WeaponController
    {
        /// <summary>
        /// 武器の種類に応じた攻撃処理
        /// すべての具象型を個別に処理する例
        /// </summary>
        public void PerformAttack(IWeapon weapon)
        {
            switch (weapon)
            {
                case Sword sword:
                    sword.Slash();
                    Debug.Log($"鋭さ: {sword.Sharpness}");
                    break;

                case Axe axe:
                    axe.Chop();
                    Debug.Log($"重量: {axe.Weight}kg");
                    break;

                case Bow bow:
                    bow.ChargeShot();
                    Debug.Log($"引き速度: {bow.DrawSpeed}秒");
                    break;

                case Crossbow crossbow:
                    crossbow.PiercingShot();
                    Debug.Log($"リロード時間: {crossbow.ReloadTime}秒");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null);
            }
        }

        /// <summary>
        /// 武器のカテゴリに応じた処理
        /// 中間層の抽象クラスで分岐する例
        /// </summary>
        public string GetWeaponCategory(IWeapon weapon)
        {
            switch (weapon)
            {
                case MeleeWeapon melee:
                    return $"近接武器 (射程: {melee.AttackRange}m)";

                case RangedWeapon ranged:
                    return $"遠距離武器 (最大射程: {ranged.MaxRange}m, 弾数: {ranged.AmmoCapacity})";

                default:
                    throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null);
            }
        }

        /// <summary>
        /// ダメージ計算
        /// 抽象クラスと具象クラスを組み合わせた分岐
        /// </summary>
        public int CalculateDamage(IWeapon weapon, float distance)
        {
            switch (weapon)
            {
                case MeleeWeapon melee when distance <= melee.AttackRange:
                    // 近接武器: 射程内ならクリティカル倍率を適用
                    float criticalMultiplier = distance < melee.AttackRange * 0.5f ? 1.5f : 1.0f;
                    return melee.CalculateMeleeDamage(criticalMultiplier);

                case MeleeWeapon _:
                    // 近接武器: 射程外なら0ダメージ
                    return 0;

                case RangedWeapon ranged when distance <= ranged.MaxRange:
                    // 遠距離武器: 距離に応じてダメージ減衰
                    return ranged.CalculateRangedDamage(distance);

                case RangedWeapon _:
                    // 遠距離武器: 射程外なら0ダメージ
                    return 0;

                default:
                    throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null);
            }
        }

        /// <summary>
        /// 武器のスタミナ消費量を取得
        /// 具象型ごとに異なる値を返す
        /// </summary>
        public int GetStaminaCost(IWeapon weapon)
        {
            switch (weapon)
            {
                case Sword _:
                    return 10;

                case Axe _:
                    return 20; // 重いので多めに消費

                case Bow _:
                    return 5;

                case Crossbow _:
                    return 15; // リロードが重いので多めに消費

                default:
                    throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null);
            }
        }

        /// <summary>
        /// 武器のアイコン名を取得
        /// カテゴリと具体的な武器を組み合わせた例
        /// </summary>
        public string GetIconName(IWeapon weapon)
        {
            switch (weapon)
            {
                case Sword _:
                    return "icon_sword";

                case Axe _:
                    return "icon_axe";

                case Bow _:
                    return "icon_bow";

                case Crossbow _:
                    return "icon_crossbow";

                default:
                    throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null);
            }
        }

        /// <summary>
        /// 使用例
        /// </summary>
        public void Example()
        {
            IWeapon[] weapons = new IWeapon[]
            {
                new Sword(),
                new Axe(),
                new Bow(),
                new Crossbow()
            };

            foreach (var weapon in weapons)
            {
                Debug.Log($"\n=== {weapon.Name} ===");
                Debug.Log($"カテゴリ: {GetWeaponCategory(weapon)}");
                Debug.Log($"基礎ダメージ: {weapon.BaseDamage}");
                Debug.Log($"スタミナ消費: {GetStaminaCost(weapon)}");
                Debug.Log($"アイコン: {GetIconName(weapon)}");

                // 様々な距離でのダメージ計算
                float[] distances = { 1f, 5f, 30f };
                foreach (var distance in distances)
                {
                    int damage = CalculateDamage(weapon, distance);
                    Debug.Log($"距離{distance}m でのダメージ: {damage}");
                }

                PerformAttack(weapon);
            }
        }
    }
}
