using System;

namespace ExhaustiveSwitch
{
    /// <summary>
    /// 網羅性の基点となる型に付与する属性。
    /// この属性が付与されたインターフェースまたは抽象クラスに対するswitch処理は、
    /// すべての[Case]を持つ型を明示的に処理する必要があります。
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ExhaustiveAttribute : Attribute
    {
    }
}
