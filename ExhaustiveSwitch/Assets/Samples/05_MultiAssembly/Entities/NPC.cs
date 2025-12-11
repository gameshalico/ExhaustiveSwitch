using ExhaustiveSwitch;
using ExhaustiveSwitchSamples.MultiAssembly.Core;
using UnityEngine;

namespace ExhaustiveSwitchSamples.MultiAssembly.Entities
{
    /// <summary>
    /// NPCキャラクター
    /// </summary>
    [Case]
    public class NPC : ICharacter
    {
        public string Name { get; private set; }
        public int HP { get; private set; }
        public int MaxHP { get; private set; }
        public string Role { get; private set; }
        public string[] Dialogues { get; private set; }
        public bool IsQuestGiver { get; private set; }

        public NPC(string name, string role, string[] dialogues, bool isQuestGiver = false)
        {
            Name = name;
            Role = role;
            Dialogues = dialogues;
            IsQuestGiver = isQuestGiver;
            MaxHP = 50; // NPCは戦闘に参加しないが、一応HPを持つ
            HP = MaxHP;
        }

        public void TakeDamage(int damage)
        {
            HP = Mathf.Max(0, HP - damage);
            Debug.Log($"{Name}が攻撃された! これは犯罪です!");
        }

        public void Heal(int amount)
        {
            HP = Mathf.Min(MaxHP, HP + amount);
            Debug.Log($"{Name}が{amount}回復した");
        }

        public void Talk()
        {
            if (Dialogues.Length > 0)
            {
                string dialogue = Dialogues[Random.Range(0, Dialogues.Length)];
                Debug.Log($"{Name} ({Role}): {dialogue}");
            }
        }

        public void GiveQuest()
        {
            if (IsQuestGiver)
            {
                Debug.Log($"{Name}がクエストを提供しています...");
            }
            else
            {
                Debug.Log($"{Name}はクエストを持っていません");
            }
        }
    }
}
