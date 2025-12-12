using ExhaustiveSwitch;
using MultiAssemblySample.Core;

namespace MultiAssemblySample.Entities
{
    [Case]
    public class NPC : ICharacter
    {
        public string Name { get; }
        public int HP { get; }
        public string Role { get; }

        public NPC(string name, int hp, string role)
        {
            Name = name;
            HP = hp;
            Role = role;
        }
    }
}
