using ExhaustiveSwitch;

namespace MultiAssemblySample.Core
{
    [Exhaustive]
    public interface ICharacter
    {
        string Name { get; }
        int HP { get; }
    }
}
