using System;
using ExhaustiveSwitch;
using UnityEngine;

[Exhaustive]
public interface ISample
{
    
}

[Case]
public sealed class ConcreteA : ISample
{
    
}

[Case]
[Exhaustive]
public class ConcreteB : ISample
{
    
}

[Case]
public sealed class ConcreteB1 : ConcreteB
{
    
}

[Case]
public sealed class ConcreteB2 : ConcreteB
{
    
}



public class MinimalSample
{
    public void Execute(ISample sample)
    {

        switch (sample)
        {
            case ConcreteA a:
                Debug.Log("It's ConcreteA");
                break;
            case ConcreteB1 b1:
                Debug.Log("It's ConcreteB1");
                break;
            case ConcreteB2 b2:
                Debug.Log("It's ConcreteB2");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sample), sample, null);
        }
        
        switch (sample)
        {
            case ConcreteA a:
                Debug.Log("It's ConcreteA");
                break;
            case ConcreteB b:
                Debug.Log("It's ConcreteB");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sample), sample, null);
        }
    }
}
