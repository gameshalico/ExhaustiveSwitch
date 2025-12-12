using ExhaustiveSwitch;
using MultiAssemblySample.Core;

namespace MultiAssemblySample.Entities
{
    [Case]
    public class Enemy : ICharacter
    {
        public string Name { get; }
        public int HP { get; }
        public int AttackPower { get; }

        public Enemy(string name, int hp, int attackPower)
        {
            Name = name;
            HP = hp;
            AttackPower = attackPower;
        }
    }
}
