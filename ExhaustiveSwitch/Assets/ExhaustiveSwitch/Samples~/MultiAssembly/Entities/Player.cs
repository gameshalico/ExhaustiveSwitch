using ExhaustiveSwitch;
using MultiAssemblySample.Core;

namespace MultiAssemblySample.Entities
{
    [Case]
    public class Player : ICharacter
    {
        public string Name { get; }
        public int HP { get; }
        public int Level { get; }

        public Player(string name, int hp, int level)
        {
            Name = name;
            HP = hp;
            Level = level;
        }
    }
}
