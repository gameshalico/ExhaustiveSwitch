using System;
using MultiAssemblySample.Core;
using MultiAssemblySample.Entities;
using UnityEngine;

namespace MultiAssemblySample.GameLogic
{
    public class CharacterProcessor
    {
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
                    throw new ArgumentOutOfRangeException(nameof(character));
            }
        }

        public void DisplayInfo(ICharacter character)
        {
            Debug.Log($"Name: {character.Name}, HP: {character.HP}");

            switch (character)
            {
                case Player player:
                    Debug.Log($"Level: {player.Level}");
                    break;
                case Enemy enemy:
                    Debug.Log($"Attack: {enemy.AttackPower}");
                    break;
                case NPC npc:
                    Debug.Log($"Role: {npc.Role}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(character));
            }
        }
    }
}
