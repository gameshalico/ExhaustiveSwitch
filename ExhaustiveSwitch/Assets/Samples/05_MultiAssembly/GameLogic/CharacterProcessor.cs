using System;
using ExhaustiveSwitchSamples.MultiAssembly.Core;
using ExhaustiveSwitchSamples.MultiAssembly.Entities;
using UnityEngine;

namespace ExhaustiveSwitchSamples.MultiAssembly.GameLogic
{
    /// <summary>
    /// キャラクターの処理を行うクラス
    /// GameLogicアセンブリからCoreとEntitiesの両方を参照している
    ///
    /// ExhaustiveSwitchは参照アセンブリから[Case]型を検出するため、
    /// このアセンブリでもすべての具象型（Player, Enemy, NPC）を
    /// 処理する必要があります
    /// </summary>
    public class CharacterProcessor
    {
        /// <summary>
        /// キャラクタータイプに応じた色を取得
        /// すべての具象型を処理する必要がある
        /// </summary>
        public Color GetCharacterColor(ICharacter character)
        {
            switch (character)
            {
                case Player _:
                    return Color.blue;

                case Enemy _:
                    return Color.red;

                case NPC _:
                    return Color.green;

                default:
                    throw new ArgumentOutOfRangeException(nameof(character), character, null);
            }
        }

        /// <summary>
        /// キャラクターのUIアイコン名を取得
        /// </summary>
        public string GetIconName(ICharacter character)
        {
            switch (character)
            {
                case Player player:
                    return $"icon_player_lv{player.Level}";

                case Enemy enemy:
                    return $"icon_enemy_{enemy.Name.ToLower()}";

                case NPC npc:
                    return npc.IsQuestGiver ? "icon_npc_quest" : "icon_npc_normal";

                default:
                    throw new ArgumentOutOfRangeException(nameof(character), character, null);
            }
        }

        /// <summary>
        /// キャラクターの詳細情報を表示
        /// </summary>
        public void DisplayCharacterInfo(ICharacter character)
        {
            Debug.Log($"\n=== {character.Name} ===");
            Debug.Log($"HP: {character.HP}/{character.MaxHP}");

            switch (character)
            {
                case Player player:
                    Debug.Log($"タイプ: プレイヤー");
                    Debug.Log($"レベル: {player.Level}");
                    Debug.Log($"MP: {player.MP}/{player.MaxMP}");
                    Debug.Log($"経験値: {player.Experience}");
                    break;

                case Enemy enemy:
                    Debug.Log($"タイプ: 敵");
                    Debug.Log($"攻撃力: {enemy.AttackPower}");
                    Debug.Log($"防御力: {enemy.DefensePower}");
                    Debug.Log($"経験値報酬: {enemy.ExpReward}");
                    break;

                case NPC npc:
                    Debug.Log($"タイプ: NPC");
                    Debug.Log($"役割: {npc.Role}");
                    Debug.Log($"クエスト提供: {(npc.IsQuestGiver ? "はい" : "いいえ")}");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(character), character, null);
            }
        }

        /// <summary>
        /// キャラクター同士の相互作用
        /// </summary>
        public void Interact(ICharacter actor, ICharacter target)
        {
            switch (actor)
            {
                case Player player when target is Enemy enemy:
                    Debug.Log($"{player.Name}が{enemy.Name}と戦闘を開始!");
                    enemy.Attack(player);
                    break;

                case Player player when target is NPC npc:
                    Debug.Log($"{player.Name}が{npc.Name}に話しかけた");
                    npc.Talk();
                    npc.GiveQuest();
                    break;

                case Enemy enemy when target is Player player:
                    Debug.Log($"{enemy.Name}が{player.Name}を攻撃!");
                    enemy.Attack(player);
                    break;

                case Enemy _ when target is Enemy _:
                    Debug.Log("敵同士は戦いません");
                    break;

                case Enemy enemy when target is NPC npc:
                    Debug.Log($"{enemy.Name}が{npc.Name}を襲撃!");
                    enemy.Attack(npc);
                    break;

                case NPC npc:
                    Debug.Log($"{npc.Name}は戦闘に参加しません");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(actor), actor, null);
            }
        }

        /// <summary>
        /// 使用例
        /// </summary>
        public void Example()
        {
            // 様々なキャラクターを作成
            ICharacter[] characters = new ICharacter[]
            {
                new Player("勇者アレックス", 5),
                new Enemy("ゴブリン", 30, 10, 2, 50),
                new NPC("村長", "村のリーダー", new[] { "ようこそ!", "困ったことがあれば聞いてくれ" }, true)
            };

            // すべてのキャラクターの情報を表示
            foreach (var character in characters)
            {
                DisplayCharacterInfo(character);
                Debug.Log($"色: {GetCharacterColor(character)}");
                Debug.Log($"アイコン: {GetIconName(character)}");
            }

            // 相互作用の例
            Debug.Log("\n=== 相互作用 ===");
            Interact(characters[0], characters[1]); // プレイヤー vs 敵
            Interact(characters[0], characters[2]); // プレイヤー vs NPC
            Interact(characters[1], characters[0]); // 敵 vs プレイヤー
        }
    }
}
