using ExhaustiveSwitch;
using ExhaustiveSwitchSamples.MultiAssembly.Core;
using UnityEngine;

namespace ExhaustiveSwitchSamples.MultiAssembly.Entities
{
    /// <summary>
    /// プレイヤーキャラクター
    /// Entitiesアセンブリに配置された具象クラス
    /// </summary>
    [Case]
    public class Player : ICharacter
    {
        public string Name { get; private set; }
        public int HP { get; private set; }
        public int MaxHP { get; private set; }
        public int MP { get; private set; }
        public int MaxMP { get; private set; }
        public int Level { get; private set; }
        public int Experience { get; private set; }

        public Player(string name, int level = 1)
        {
            Name = name;
            Level = level;
            MaxHP = 100 + (level - 1) * 10;
            HP = MaxHP;
            MaxMP = 50 + (level - 1) * 5;
            MP = MaxMP;
            Experience = 0;
        }

        public void TakeDamage(int damage)
        {
            HP = Mathf.Max(0, HP - damage);
            Debug.Log($"{Name}が{damage}ダメージを受けた! 残りHP: {HP}/{MaxHP}");
        }

        public void Heal(int amount)
        {
            HP = Mathf.Min(MaxHP, HP + amount);
            Debug.Log($"{Name}が{amount}回復した! HP: {HP}/{MaxHP}");
        }

        public void GainExperience(int exp)
        {
            Experience += exp;
            Debug.Log($"{Name}が{exp}の経験値を獲得! 合計: {Experience}");
        }

        public void CastSpell(string spellName, int mpCost)
        {
            if (MP >= mpCost)
            {
                MP -= mpCost;
                Debug.Log($"{Name}が{spellName}を発動! 残りMP: {MP}/{MaxMP}");
            }
            else
            {
                Debug.LogWarning($"MPが足りない! 必要: {mpCost}, 現在: {MP}");
            }
        }
    }
}
