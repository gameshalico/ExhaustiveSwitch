using System;
using ExhaustiveSwitch;

namespace EnemySample
{
    // [Exhaustive]属性を付与
    [Exhaustive]
    public interface IEnemy
    {
        void Attack();
    }

    // 各具象クラスに[Case]属性を付与
    [Case]
    public class Goblin : IEnemy
    {
        public void Attack() { }
    }

    [Exhaustive]
    public interface IFlyable
    {
        public void Fly() { }
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

    public class EnemySample
    {
        public void ProcessEnemy(IEnemy enemy)
        {
            // switch文で明示的にすべての具象型を処理する必要がある
            switch (enemy)
            {
                case Goblin goblin:
                    // Goblinの処理
                    break;
                case Dragon dragon:
                    // Dragonの処理
                    break;
                case Harpy harpy:
                    // Harpyの処理
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
            }

            // 上位の型で処理することも可能
            switch (enemy)
            {
                case Goblin goblin:
                    // Goblinの処理
                    break;
                case IFlyable flyable:
                    // IFlyableの処理
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(enemy), enemy, null);
            }
        }
    }
    
}