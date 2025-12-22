using System;

namespace ExhaustiveSwitch
{
    /// <summary>
    /// Attribute to be applied to types that should be exhaustively handled.
    /// Types with this attribute must be explicitly handled in switch statements on [Exhaustive] types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class CaseAttribute : Attribute
    {
    }
}
