using System;

namespace ExhaustiveSwitch
{
    /// <summary>
    /// Attribute to be applied to the type that serves as the base for exhaustiveness checking.
    /// Switch statements on interfaces or abstract classes with this attribute must explicitly handle all types with the [Case] attribute.
    /// For enums, all enum members must be explicitly handled in switch statements.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public sealed class ExhaustiveAttribute : Attribute
    {
    }
}
