using ExhaustiveSwitch;

namespace ExhaustiveSwitchSamples.MultiAssembly.Core
{
    /// <summary>
    /// キャラクターを表すインターフェース
    /// このインターフェースはCoreアセンブリに配置され、
    /// 他のアセンブリから参照されます
    /// </summary>
    [Exhaustive]
    public interface ICharacter
    {
        string Name { get; }
        int HP { get; }
        int MaxHP { get; }
        void TakeDamage(int damage);
        void Heal(int amount);
    }
}
