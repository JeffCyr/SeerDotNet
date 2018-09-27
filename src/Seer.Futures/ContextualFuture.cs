using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Seer.Futures.Runtime;

namespace Seer.Futures
{
    [AsyncMethodBuilder(typeof(ContextualFutureAsyncMethodBuilder))]
    public readonly struct ContextualFuture : IFuture, IEquatable<ContextualFuture>, IEquatable<Future>
    {
        private readonly Future _future;

        public bool IsCompleted => _future.IsCompleted;
        public bool IsSucceeded => _future.IsSucceeded;
        public bool IsFailed => _future.IsFailed;
        public Exception Exception => _future.Exception;

        public void ThrowIfFailed() => _future.ThrowIfFailed();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ContextualFuture(in Future future)
        {
            _future = future;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ContextualFuture(Promise promise)
        {
            _future = new Future(promise);
        }

        public FutureAwaiter GetAwaiter()
        {
            return _future.GetAwaiter();
        }

        public ScheduledFutureAwaitable ContinueOn(FutureScheduler scheduler)
        {
            return _future.ContinueOn(scheduler);
        }

        public Future ContinueWith(Action<Future> action, FutureScheduler scheduler = null)
        {
            return _future.ContinueWith(action, scheduler);
        }

        public Future<TResult> ContinueWith<TResult>(Func<Future, TResult> func, FutureScheduler scheduler = null)
        {
            return _future.ContinueWith(func, scheduler);
        }

        public Task ToTask()
        {
            return _future.ToTask();
        }

        internal Promise GetPromise()
        {
            return _future.GetPromise();
        }

        Future IFuture.ToFuture()
        {
            return _future;
        }

        Future<T> IFuture.ToFuture<T>()
        {
            return _future.ToFutureOf<T>();
        }

        public static implicit operator Future(ContextualFuture future)
        {
            return future._future;
        }

        public bool Equals(ContextualFuture other)
        {
            return _future.Equals(other._future);
        }

        public bool Equals(Future other)
        {
            return _future.Equals(other);
        }

        public override bool Equals(object obj)
        {
            return _future.Equals(obj);
        }

        public override int GetHashCode()
        {
            return _future.GetHashCode();
        }

        public static bool operator ==(ContextualFuture left, ContextualFuture right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContextualFuture left, ContextualFuture right)
        {
            return !left.Equals(right);
        }
    }

    [AsyncMethodBuilder(typeof(ContextualFutureAsyncMethodBuilder<>))]
    public readonly struct ContextualFuture<T> : IFuture<T>, IEquatable<ContextualFuture<T>>
    {
        private readonly Future<T> _future;

        public bool IsCompleted => _future.IsCompleted;
        public bool IsSucceeded => _future.IsSucceeded;
        public bool IsFailed => _future.IsFailed;

        public T Value => _future.Value;

        public Exception Exception => _future.Exception;

        public void ThrowIfFailed() => _future.ThrowIfFailed();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ContextualFuture(T value)
        {
            _future = new Future<T>(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ContextualFuture(in Future<T> future)
        {
            _future = future;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ContextualFuture(Promise<T> promise)
        {
            _future = new Future<T>(promise);
        }

        public FutureAwaiter<T> GetAwaiter()
        {
            return _future.GetAwaiter();
        }

        public ScheduledFutureAwaitable<T> ContinueOn(FutureScheduler scheduler)
        {
            return _future.ContinueOn(scheduler);
        }

        public Future ContinueWith(Action<Future<T>> action, FutureScheduler scheduler = null)
        {
            return _future.ContinueWith(action, scheduler);
        }

        public Future<TResult> ContinueWith<TResult>(Func<Future<T>, TResult> func, FutureScheduler scheduler = null)
        {
            return _future.ContinueWith(func, scheduler);
        }

        public Task<T> ToTask()
        {
            return _future.ToTask();
        }

        internal Promise<T> GetPromise()
        {
            return _future.GetPromise();
        }

        Future IFuture.ToFuture()
        {
            return _future;
        }

        Future<TCast> IFuture.ToFuture<TCast>()
        {
            return _future.ToFutureOf<TCast>();
        }

        public static implicit operator Future<T>(ContextualFuture<T> future)
        {
            return future._future;
        }

        public bool Equals(ContextualFuture<T> other)
        {
            return _future.Equals(other._future);
        }

        public bool Equals(Future other)
        {
            return _future.Equals(other);
        }

        public bool Equals(Future<T> other)
        {
            return _future.Equals(other);
        }

        public override bool Equals(object obj)
        {
            return _future.Equals(obj);
        }

        public override int GetHashCode()
        {
            return _future.GetHashCode();
        }

        public static bool operator ==(ContextualFuture<T> left, ContextualFuture<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ContextualFuture<T> left, ContextualFuture<T> right)
        {
            return !left.Equals(right);
        }
    }
}