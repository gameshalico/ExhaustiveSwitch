using System;
using UnityEngine;

namespace ExhaustiveSwitchSamples.BasicUsage
{
    /// <summary>
    /// 敵の制御を行うクラス
    /// ExhaustiveSwitchを使って、すべての敵タイプを確実に処理します
    /// </summary>
    public class EnemyController
    {
        /// <summary>
        /// 敵ごとに異なるダメージ計算を行う
        /// すべての具象型を個別に処理する例
        /// </summary>
        public int CalculateDamage(IEnemy enemy)
        {
            switch (enemy)
            {
                case Goblin goblin:
                    // ゴブリンは近接攻撃
                    return goblin.ClubDamage;

                case Dragon dragon:
                    // ドラゴンは火炎攻撃で高ダメージ
                    return dragon.FireBreathDamage;

                case Harpy harpy:
                    // ハーピーは爪攻撃
                    return harpy.ClawDamage;

                default:
                    throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
            }
        }

        /// <summary>
        /// 敵の移動処理
        /// インターフェース型を使って処理を分岐する例
        /// </summary>
        public void ProcessMovement(IEnemy enemy)
        {
            switch (enemy)
            {
                case Goblin goblin:
                    // 地上を歩く
                    Debug.Log($"{goblin.Name}が地上を移動");
                    break;

                case IFlyable flyable:
                    // 空を飛ぶ（DragonとHarpyの両方をカバー）
                    flyable.Fly();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
            }
        }

        /// <summary>
        /// 敵のAI更新処理
        /// それぞれの敵に固有の行動を実行
        /// </summary>
        public void UpdateAI(IEnemy enemy)
        {
            switch (enemy)
            {
                case Goblin goblin:
                    // ゴブリンは接近して攻撃
                    Debug.Log($"{goblin.Name}のAI: プレイヤーに接近");
                    if (UnityEngine.Random.value > 0.7f)
                    {
                        goblin.ThrowRock();
                    }
                    break;

                case Dragon dragon:
                    // ドラゴンは上空から攻撃
                    Debug.Log($"{dragon.Name}のAI: 上空を旋回");
                    if (UnityEngine.Random.value > 0.8f)
                    {
                        dragon.Roar();
                    }
                    break;

                case Harpy harpy:
                    // ハーピーは急降下攻撃
                    Debug.Log($"{harpy.Name}のAI: 急降下攻撃の準備");
                    if (UnityEngine.Random.value > 0.6f)
                    {
                        harpy.Screech();
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
            }
        }

        /// <summary>
        /// 敵のエフェクトを取得
        /// 敵の種類に応じて異なるエフェクトを返す
        /// </summary>
        public string GetEffectName(IEnemy enemy)
        {
            switch (enemy)
            {
                case Goblin _:
                    return "DustCloud";

                case Dragon _:
                    return "FireBreath";

                case Harpy _:
                    return "Feathers";

                default:
                    throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
            }
        }

        /// <summary>
        /// 使用例
        /// </summary>
        public void Example()
        {
            IEnemy[] enemies = new IEnemy[]
            {
                new Goblin(),
                new Dragon(),
                new Harpy()
            };

            foreach (var enemy in enemies)
            {
                Debug.Log($"\n=== {enemy.Name} ===");
                Debug.Log($"HP: {enemy.HP}");
                Debug.Log($"ダメージ: {CalculateDamage(enemy)}");
                Debug.Log($"エフェクト: {GetEffectName(enemy)}");

                ProcessMovement(enemy);
                UpdateAI(enemy);
                enemy.Attack();
            }
        }
    }
}
