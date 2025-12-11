using System;
using UnityEngine;

namespace ExhaustiveSwitchSamples.GettingStarted
{
    /// <summary>
    /// 図形を描画するクラス
    /// ExhaustiveSwitchを使用して、すべての図形を確実に処理します
    /// </summary>
    public class ShapeRenderer
    {
        /// <summary>
        /// 図形を描画する
        /// すべてのIShapeの具象型を処理する必要があります
        /// </summary>
        public void Render(IShape shape)
        {
            // すべてのケースを処理する必要があります
            // 1つでも欠けているとコンパイルエラーになります
            switch (shape)
            {
                case Circle circle:
                    Debug.Log($"円を描画: 半径={circle.Radius}, 面積={circle.GetArea()}");
                    break;
                case Rectangle rectangle:
                    Debug.Log($"長方形を描画: 幅={rectangle.Width}, 高さ={rectangle.Height}, 面積={rectangle.GetArea()}");
                    break;
                case Square square:
                    Debug.Log($"正方形を描画: サイズ={square.Size}, 面積={square.GetArea()}");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }

        /// <summary>
        /// 図形の色を取得する（例）
        /// 同じIShapeに対して異なる処理を書くこともできます
        /// </summary>
        public Color GetColor(IShape shape)
        {
            switch (shape)
            {
                case Circle _:
                    return Color.red;
                case Rectangle _:
                    return Color.blue;
                case Square _:
                    return Color.green;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }

        /// <summary>
        /// 使用例
        /// </summary>
        public void Example()
        {
            IShape[] shapes = new IShape[]
            {
                new Circle(5f),
                new Rectangle(10f, 20f),
                new Square(15f)
            };

            foreach (var shape in shapes)
            {
                Render(shape);
                Color color = GetColor(shape);
                Debug.Log($"色: {color}");
            }
        }
    }
}
