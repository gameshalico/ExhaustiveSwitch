using System;
using System.Collections.Generic;
using ExhaustiveSwitch;
using UnityEngine;

namespace ExhaustiveSwitchSamples.AdvancedPatterns
{
    // ===== 式木（Expression Tree）パターン =====
    // ExhaustiveSwitchを使うことで、従来のVisitorパターンのような
    // ダブルディスパッチを避けつつ、型安全に式を評価できます

    /// <summary>
    /// 数式を表すインターフェース
    /// </summary>
    [Exhaustive]
    public interface IExpression
    {
    }

    /// <summary>
    /// 数値リテラル
    /// </summary>
    [Case]
    public sealed class NumberExpression : IExpression
    {
        public float Value { get; }

        public NumberExpression(float value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 変数参照
    /// </summary>
    [Case]
    public sealed class VariableExpression : IExpression
    {
        public string VariableName { get; }

        public VariableExpression(string variableName)
        {
            VariableName = variableName;
        }
    }

    /// <summary>
    /// 加算
    /// </summary>
    [Case]
    public sealed class AddExpression : IExpression
    {
        public IExpression Left { get; }
        public IExpression Right { get; }

        public AddExpression(IExpression left, IExpression right)
        {
            Left = left;
            Right = right;
        }
    }

    /// <summary>
    /// 減算
    /// </summary>
    [Case]
    public sealed class SubtractExpression : IExpression
    {
        public IExpression Left { get; }
        public IExpression Right { get; }

        public SubtractExpression(IExpression left, IExpression right)
        {
            Left = left;
            Right = right;
        }
    }

    /// <summary>
    /// 乗算
    /// </summary>
    [Case]
    public sealed class MultiplyExpression : IExpression
    {
        public IExpression Left { get; }
        public IExpression Right { get; }

        public MultiplyExpression(IExpression left, IExpression right)
        {
            Left = left;
            Right = right;
        }
    }

    /// <summary>
    /// 除算
    /// </summary>
    [Case]
    public sealed class DivideExpression : IExpression
    {
        public IExpression Left { get; }
        public IExpression Right { get; }

        public DivideExpression(IExpression left, IExpression right)
        {
            Left = left;
            Right = right;
        }
    }

    // ===== 式の評価と操作 =====

    /// <summary>
    /// 式の評価器
    /// ExhaustiveSwitchを使うことで、すべての式タイプを確実に処理
    /// </summary>
    public class ExpressionEvaluator
    {
        private Dictionary<string, float> variables = new Dictionary<string, float>();

        public void SetVariable(string name, float value)
        {
            variables[name] = value;
        }

        /// <summary>
        /// 式を評価して結果を返す
        /// すべての式タイプを処理する必要がある
        /// </summary>
        public float Evaluate(IExpression expression)
        {
            switch (expression)
            {
                case NumberExpression number:
                    return number.Value;

                case VariableExpression variable:
                    if (variables.TryGetValue(variable.VariableName, out float value))
                    {
                        return value;
                    }
                    throw new Exception($"未定義の変数: {variable.VariableName}");

                case AddExpression add:
                    return Evaluate(add.Left) + Evaluate(add.Right);

                case SubtractExpression subtract:
                    return Evaluate(subtract.Left) - Evaluate(subtract.Right);

                case MultiplyExpression multiply:
                    return Evaluate(multiply.Left) * Evaluate(multiply.Right);

                case DivideExpression divide:
                    float rightValue = Evaluate(divide.Right);
                    if (Mathf.Approximately(rightValue, 0f))
                    {
                        throw new DivideByZeroException();
                    }
                    return Evaluate(divide.Left) / rightValue;

                default:
                    throw new ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }

    /// <summary>
    /// 式を文字列に変換（Pretty Print）
    /// </summary>
    public class ExpressionPrinter
    {
        /// <summary>
        /// 式を人間が読める形式に変換
        /// </summary>
        public string Print(IExpression expression)
        {
            switch (expression)
            {
                case NumberExpression number:
                    return number.Value.ToString("F2");

                case VariableExpression variable:
                    return variable.VariableName;

                case AddExpression add:
                    return $"({Print(add.Left)} + {Print(add.Right)})";

                case SubtractExpression subtract:
                    return $"({Print(subtract.Left)} - {Print(subtract.Right)})";

                case MultiplyExpression multiply:
                    return $"({Print(multiply.Left)} * {Print(multiply.Right)})";

                case DivideExpression divide:
                    return $"({Print(divide.Left)} / {Print(divide.Right)})";

                default:
                    throw new ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }

    /// <summary>
    /// 式の最適化
    /// 定数畳み込みなどの最適化を行う
    /// </summary>
    public class ExpressionOptimizer
    {
        /// <summary>
        /// 式を最適化する
        /// 例: (2 + 3) → 5, (x + 0) → x, (x * 1) → x
        /// </summary>
        public IExpression Optimize(IExpression expression)
        {
            switch (expression)
            {
                case NumberExpression number:
                    return number;

                case VariableExpression variable:
                    return variable;

                case AddExpression add:
                    var leftAdd = Optimize(add.Left);
                    var rightAdd = Optimize(add.Right);

                    // 両方が数値なら計算
                    if (leftAdd is NumberExpression leftNum && rightAdd is NumberExpression rightNum)
                    {
                        return new NumberExpression(leftNum.Value + rightNum.Value);
                    }
                    // x + 0 → x
                    if (rightAdd is NumberExpression rightZero && Mathf.Approximately(rightZero.Value, 0f))
                    {
                        return leftAdd;
                    }
                    // 0 + x → x
                    if (leftAdd is NumberExpression leftZero && Mathf.Approximately(leftZero.Value, 0f))
                    {
                        return rightAdd;
                    }
                    return new AddExpression(leftAdd, rightAdd);

                case SubtractExpression subtract:
                    var leftSub = Optimize(subtract.Left);
                    var rightSub = Optimize(subtract.Right);

                    if (leftSub is NumberExpression leftSubNum && rightSub is NumberExpression rightSubNum)
                    {
                        return new NumberExpression(leftSubNum.Value - rightSubNum.Value);
                    }
                    // x - 0 → x
                    if (rightSub is NumberExpression rightSubZero && Mathf.Approximately(rightSubZero.Value, 0f))
                    {
                        return leftSub;
                    }
                    return new SubtractExpression(leftSub, rightSub);

                case MultiplyExpression multiply:
                    var leftMul = Optimize(multiply.Left);
                    var rightMul = Optimize(multiply.Right);

                    if (leftMul is NumberExpression leftMulNum && rightMul is NumberExpression rightMulNum)
                    {
                        return new NumberExpression(leftMulNum.Value * rightMulNum.Value);
                    }
                    // x * 0 → 0
                    if ((leftMul is NumberExpression leftMulZero && Mathf.Approximately(leftMulZero.Value, 0f)) ||
                        (rightMul is NumberExpression rightMulZero && Mathf.Approximately(rightMulZero.Value, 0f)))
                    {
                        return new NumberExpression(0f);
                    }
                    // x * 1 → x
                    if (rightMul is NumberExpression rightMulOne && Mathf.Approximately(rightMulOne.Value, 1f))
                    {
                        return leftMul;
                    }
                    // 1 * x → x
                    if (leftMul is NumberExpression leftMulOne && Mathf.Approximately(leftMulOne.Value, 1f))
                    {
                        return rightMul;
                    }
                    return new MultiplyExpression(leftMul, rightMul);

                case DivideExpression divide:
                    var leftDiv = Optimize(divide.Left);
                    var rightDiv = Optimize(divide.Right);

                    if (leftDiv is NumberExpression leftDivNum && rightDiv is NumberExpression rightDivNum)
                    {
                        if (!Mathf.Approximately(rightDivNum.Value, 0f))
                        {
                            return new NumberExpression(leftDivNum.Value / rightDivNum.Value);
                        }
                    }
                    // x / 1 → x
                    if (rightDiv is NumberExpression rightDivOne && Mathf.Approximately(rightDivOne.Value, 1f))
                    {
                        return leftDiv;
                    }
                    return new DivideExpression(leftDiv, rightDiv);

                default:
                    throw new ArgumentOutOfRangeException(nameof(expression), expression, null);
            }
        }
    }

    /// <summary>
    /// 式木パターンの使用例
    /// 従来のVisitorパターンではダブルディスパッチが必要だったが、
    /// ExhaustiveSwitchを使うことでシンプルに実装できる
    /// </summary>
    public class ExpressionTreeExample
    {
        public void Example()
        {
            // 式を構築: ((10 + x) * 2) / (y - 3)
            IExpression expression = new DivideExpression(
                new MultiplyExpression(
                    new AddExpression(
                        new NumberExpression(10f),
                        new VariableExpression("x")
                    ),
                    new NumberExpression(2f)
                ),
                new SubtractExpression(
                    new VariableExpression("y"),
                    new NumberExpression(3f)
                )
            );

            var printer = new ExpressionPrinter();
            Debug.Log($"元の式: {printer.Print(expression)}");

            // 評価
            var evaluator = new ExpressionEvaluator();
            evaluator.SetVariable("x", 5f);
            evaluator.SetVariable("y", 8f);

            float result = evaluator.Evaluate(expression);
            Debug.Log($"x=5, y=8 のとき: {result}");

            // 最適化の例
            Debug.Log("\n=== 式の最適化 ===");

            // 例1: (2 + 3) * x → 5 * x
            IExpression expr1 = new MultiplyExpression(
                new AddExpression(
                    new NumberExpression(2f),
                    new NumberExpression(3f)
                ),
                new VariableExpression("x")
            );

            var optimizer = new ExpressionOptimizer();
            Debug.Log($"最適化前: {printer.Print(expr1)}");
            IExpression optimized1 = optimizer.Optimize(expr1);
            Debug.Log($"最適化後: {printer.Print(optimized1)}");

            // 例2: (x + 0) * 1 → x
            IExpression expr2 = new MultiplyExpression(
                new AddExpression(
                    new VariableExpression("x"),
                    new NumberExpression(0f)
                ),
                new NumberExpression(1f)
            );

            Debug.Log($"\n最適化前: {printer.Print(expr2)}");
            IExpression optimized2 = optimizer.Optimize(expr2);
            Debug.Log($"最適化後: {printer.Print(optimized2)}");

            // 例3: 完全に定数の式 (5 + 3) * (10 - 2) → 64
            IExpression expr3 = new MultiplyExpression(
                new AddExpression(
                    new NumberExpression(5f),
                    new NumberExpression(3f)
                ),
                new SubtractExpression(
                    new NumberExpression(10f),
                    new NumberExpression(2f)
                )
            );

            Debug.Log($"\n最適化前: {printer.Print(expr3)}");
            IExpression optimized3 = optimizer.Optimize(expr3);
            Debug.Log($"最適化後: {printer.Print(optimized3)}");

            Debug.Log("\n=== Visitorパターンとの比較 ===");
            Debug.Log("従来のVisitorパターン:");
            Debug.Log("- IExpression に Accept(IVisitor) メソッドが必要");
            Debug.Log("- 各具象型で visitor.Visit(this) を呼ぶ（ダブルディスパッチ）");
            Debug.Log("- 型を追加するたびに IVisitor に新しいメソッドが必要");
            Debug.Log("\nExhaustiveSwitch:");
            Debug.Log("- IExpression はデータだけを持つ（処理ロジックと分離）");
            Debug.Log("- switch文で直接型判定（ダブルディスパッチ不要）");
            Debug.Log("- 新しい処理の追加が容易");
            Debug.Log("- 型を追加すると全ての switch でコンパイルエラーが出る");
        }
    }
}
