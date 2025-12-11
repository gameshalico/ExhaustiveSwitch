using ExhaustiveSwitch;
using ExhaustiveSwitchSamples.MultiAssembly.Core;
using UnityEngine;

namespace ExhaustiveSwitchSamples.MultiAssembly.Entities
{
    /// <summary>
    /// 敵キャラクター
    /// </summary>
    [Case]
    public class Enemy : ICharacter
    {
        public string Name { get; private set; }
        public int HP { get; private set; }
        public int MaxHP { get; private set; }
        public int AttackPower { get; private set; }
        public int DefensePower { get; private set; }
        public int ExpReward { get; private set; }

        public Enemy(string name, int maxHP, int attackPower, int defensePower, int expReward)
        {
            Name = name;
            MaxHP = maxHP;
            HP = MaxHP;
            AttackPower = attackPower;
            DefensePower = defensePower;
            ExpReward = expReward;
        }

        public void TakeDamage(int damage)
        {
            int actualDamage = Mathf.Max(1, damage - DefensePower);
            HP = Mathf.Max(0, HP - actualDamage);
            Debug.Log($"{Name}が{actualDamage}ダメージを受けた! 残りHP: {HP}/{MaxHP}");

            if (HP == 0)
            {
                Debug.Log($"{Name}を倒した! 経験値{ExpReward}を獲得!");
            }
        }

        public void Heal(int amount)
        {
            HP = Mathf.Min(MaxHP, HP + amount);
            Debug.Log($"{Name}が{amount}回復した! HP: {HP}/{MaxHP}");
        }

        public void Attack(ICharacter target)
        {
            Debug.Log($"{Name}が{target.Name}を攻撃!");
            target.TakeDamage(AttackPower);
        }
    }
}
