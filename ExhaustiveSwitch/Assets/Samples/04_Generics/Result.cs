using ExhaustiveSwitch;

namespace ExhaustiveSwitchSamples.Generics
{
    /// <summary>
    /// 処理の成功/失敗を表すResult型
    /// ジェネリクスとExhaustiveSwitchを組み合わせた例
    /// </summary>
    [Exhaustive]
    public interface IResult<T>
    {
        bool IsSuccess { get; }
    }

    /// <summary>
    /// 成功を表すクラス
    /// </summary>
    [Case]
    public sealed class Success<T> : IResult<T>
    {
        public T Value { get; }
        public bool IsSuccess => true;

        public Success(T value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 失敗を表すクラス
    /// </summary>
    [Case]
    public sealed class Failure<T> : IResult<T>
    {
        public string Error { get; }
        public int ErrorCode { get; }
        public bool IsSuccess => false;

        public Failure(string error, int errorCode = -1)
        {
            Error = error;
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Result型を生成するヘルパークラス
    /// </summary>
    public static class Result
    {
        public static IResult<T> Ok<T>(T value) => new Success<T>(value);
        public static IResult<T> Fail<T>(string error, int errorCode = -1) => new Failure<T>(error, errorCode);
    }
}
