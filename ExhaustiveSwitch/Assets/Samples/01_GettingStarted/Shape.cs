using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.GettingStarted
{
    /// <summary>
    /// 図形を表すインターフェース
    /// [Exhaustive]属性を付けることで、このインターフェースを実装するすべての型を
    /// switch文で処理する必要があることを示します
    /// </summary>
    [Exhaustive]
    public interface IShape
    {
        float GetArea();
    }

    /// <summary>
    /// 円を表すクラス
    /// [Case]属性を付けることで、IShapeの具象型の1つであることを示します
    /// </summary>
    [Case]
    public sealed class Circle : IShape
    {
        public float Radius { get; set; }

        public Circle(float radius)
        {
            Radius = radius;
        }

        public float GetArea()
        {
            return Mathf.PI * Radius * Radius;
        }
    }

    /// <summary>
    /// 長方形を表すクラス
    /// </summary>
    [Case]
    public sealed class Rectangle : IShape
    {
        public float Width { get; set; }
        public float Height { get; set; }

        public Rectangle(float width, float height)
        {
            Width = width;
            Height = height;
        }

        public float GetArea()
        {
            return Width * Height;
        }
    }

    /// <summary>
    /// 正方形を表すクラス
    /// </summary>
    [Case]
    public sealed class Square : IShape
    {
        public float Size { get; set; }

        public Square(float size)
        {
            Size = size;
        }

        public float GetArea()
        {
            return Size * Size;
        }
    }
}
