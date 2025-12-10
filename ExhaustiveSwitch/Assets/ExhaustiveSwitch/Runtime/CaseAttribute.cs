using System;

namespace ExhaustiveSwitch
{
    /// <summary>
    /// 網羅されるべき型に付与する属性。
    /// この属性が付与された型は、[Exhaustive]型に対するswitch処理で明示的に処理される必要があります。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class CaseAttribute : Attribute
    {
    }
}
