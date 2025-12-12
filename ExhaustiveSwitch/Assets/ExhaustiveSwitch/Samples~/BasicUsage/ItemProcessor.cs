using System;
using UnityEngine;

public class ItemProcessor
{
    public void ProcessItemByConcreteType(IItem item)
    {
        switch (item)
        {
            case Potion potion:
                Debug.Log("Potion");
                break;
            case Bomb bomb:
                Debug.Log("Bomb");
                break;
            case Armor armor:
                Debug.Log("Armor");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }

    public void ProcessItemByInterface(IItem item)
    {
        switch (item)
        {
            case IConsumable consumable:
                Debug.Log("Consumable item");
                break;
            case IEquippable equippable:
                Debug.Log("Equippable item");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(item));
        }
    }
}
