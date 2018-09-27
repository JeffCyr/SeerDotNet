using System;

namespace Seer.Futures
{
    public interface IFuture
    {
        bool IsCompleted { get; }
        bool IsSucceeded { get; }
        bool IsFailed { get; }
        Exception Exception { get; }
        void ThrowIfFailed();

        Future ToFuture();
        Future<T> ToFuture<T>();

        bool Equals(Future other);
    }

    public interface IFuture<T> : IFuture
    {
        T Value { get; }

        bool Equals(Future<T> other);
    }
}