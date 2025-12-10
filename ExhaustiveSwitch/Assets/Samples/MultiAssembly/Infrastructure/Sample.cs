
using System;
using UnityEngine;

class EntityViewFactory
{
    public void CreateView(IEntity entity)
    {
        // Direct type-based approach
        switch (entity)
        {
            case Goblin enemy:
                Debug.Log("Creating view for Goblin");
                break;
            case Slime slime:
                Debug.Log("Creating view for Slime");
                break;
            case HealItem healItem:
                Debug.Log("Creating view for Item");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entity), entity, null);
        }

        // Alternative approach using interface
        switch (entity)
        {
            case IEnemy enemy:
                Debug.Log("Creating view for Enemy");
                break;
            case Item item:
                Debug.Log("Creating view for Item");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entity), entity, null);
        }
    }
}