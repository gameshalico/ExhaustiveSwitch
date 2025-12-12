[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE)
# ExhaustiveSwitch

**ExhaustiveSwitch** is a library that enforces exhaustive handling of inheritance hierarchies in `switch` statements/expressions using Roslyn Analyzer.

By using the `[Exhaustive]` and `[Case]` attributes, you can detect unhandled types in `switch` statements/expressions as errors.

This provides the following benefits:
- Prevents oversight when adding new classes
- Enables type-safe branching based on types (Unlike the Visitor pattern, the abstract layer doesn't need to know about concrete layers, allowing for more abstract handling)

Also supports CodeFixProvider to add missing cases automatically.

[日本語版READMEはこちら](./README_JA.md)

![](/docs/images/code-fix.png)


## Usage

```csharp
using ExhaustiveSwitch;

// Add [Exhaustive] attribute
[Exhaustive]
public interface IEnemy
{
    public void Attack();
}

public interface IWalkable
{
    public void Walk();
}

public interface IFlyable
{
    public void Fly() { }
}

// Add [Case] attribute to each concrete class
[Case]
public class Goblin : IEnemy, IWalkable
{
    public void Attack() { }
    public void Walk() { }
}

[Case]
public class Dragon : IEnemy, IFlyable
{
    public void Attack() { }
    public void Fly() { }
}

[Case]
public class Harpy : IEnemy, IFlyable
{
    public void Attack() { }
    public void Fly() { }
}

public void ProcessEnemy(IEnemy enemy)
{
    // Branch by concrete type (error if a new enemy is implemented)
    switch (enemy)
    {
        case Goblin goblin:
            // Goblin-specific processing
            break;
        case Dragon dragon:
            // Dragon-specific processing
            break;
        case Harpy harpy:
            // Harpy-specific processing
            break;
    }

    // Branch by interface type (error if a type that neither walks nor flies is implemented)
    switch (enemy)
    {
        case IWalkable walkable:
            // Processing for walking enemies (Goblin)
            break;
        case IFlyable flyable:
            // Processing for flying enemies (Dragon and Harpy)
            break;
    }
}

```

For other usage examples, please refer to [Samples](ExhaustiveSwitch/Assets/Samples/README.md).

### Error Messages

If not all `[Case]` types are explicitly handled, the following error will be issued.
Note that if a type is handled by a parent type, no error will be issued.

```
Error EXH0001: Case 'Dragon' of Exhaustive type 'IEnemy' is not handled in the switch.
```

If a type with the `[Case]` attribute does not inherit/implement a type with the `[Exhaustive]` attribute, the following warning will be issued.

```
Warning EXH0002: Type 'Goblin' with Case attribute does not inherit/implement Exhaustive type 'IEnemy'.
```

### Limitations

Combinations of multiple types like the following are not supported:
```csharp
switch (sample1, sample2)
{
    case (ConcreteA1 _, ConcreteB1 _):
        Debug.Log("It's (ConcreteA1, ConcreteB1)");
        break;
    case (ConcreteA2 _, ConcreteB2 _):
        Debug.Log("It's (ConcreteA2, ConcreteB2)");
        break;
    default:
        throw new ArgumentOutOfRangeException();
}
```

# Setup
## Requirements
- Unity 2022.3.12f1 or later

## Installation

Add ExhaustiveSwitch to your project using Unity Package Manager.

1. Open Package Manager from Window > Package Management > Package Manager
2. Click the "+" button in the top left and select "Add package from git URL..."
3. Enter the following URL and click the "Install" button

```
https://github.com/gameshalico/ExhaustiveSwitch.git?path=ExhaustiveSwitch/Assets/ExhaustiveSwitch
```

