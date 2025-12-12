using ExhaustiveSwitch;

[Exhaustive]
public interface IItem { }

public interface IConsumable { }

public interface IEquippable { }

[Case]
public class Potion : IItem, IConsumable { }

[Case]
public class Bomb : IItem, IConsumable { }

[Case]
public class Armor : IItem, IEquippable { }
