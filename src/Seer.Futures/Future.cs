using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Seer.Futures.Runtime;

namespace Seer.Futures
{
    [AsyncMethodBuilder(typeof(FutureAsyncMethodBuilder))]
    public readonly partial struct Future : IEquatable<Future>, IFuture
    {
        private readonly Promise _promise;

        public bool IsCompleted => _promise == null || _promise.IsCompleted;

        public bool IsSucceeded => _promise == null || _promise.IsSucceeded;

        public bool IsFailed => _promise != null && _promise.IsFailed;

        public Exception Exception => _promise?.Exception;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Future(Promise promise)
        {
            _promise = promise;
        }

        internal Promise GetPromise()
        {
            return _promise;
        }

        public FutureAwaiter GetAwaiter()
        {
            return new FutureAwaiter(this);
        }

        /// <summary>
        /// Continue the execution on the specified scheduler after an await
        /// </summary>
        /// <param name="scheduler">The scheduler</param>
        public ScheduledFutureAwaitable ContinueOn(FutureScheduler scheduler)
        {
            return new ScheduledFutureAwaitable(this, scheduler);
        }

        public Future ContinueWith(Action<Future> action, FutureScheduler scheduler = null)
        {
            if (IsCompleted && FutureScheduler.IsInline(scheduler))
            {
                try
                {
                    action(this);
                    return new Future();
                }
                catch (Exception ex)
                {
                    return Future.FromException(ex);
                }
            }

            var continueWith = new ContinueWithPromise(_promise, action, scheduler);
            _promise.AddContinuation(continueWith);

            return continueWith.Future;
        }

        public Future<TResult> ContinueWith<TResult>(Func<Future, TResult> func, FutureScheduler scheduler = null)
        {
            if (IsCompleted && FutureScheduler.IsInline(scheduler))
            {
                try
                {
                    return new Future<TResult>(func(this));
                }
                catch (Exception ex)
                {
                    return Future.FromException<TResult>(ex);
                }
            }

            var continueWith = new ContinueWithPromise<TResult>(_promise, func, scheduler);
            _promise.AddContinuation(continueWith);

            return continueWith.Future;
        }

        public Task ToTask()
        {
            if (IsCompleted)
            {
                if (IsSucceeded)
                    return Task.CompletedTask;

                if (IsFailed)
                    return Task.FromException(Exception);
            }

            var completion = new TaskCompletionSourceContinuation(_promise);
            _promise.AddContinuation(completion);

            return completion.Task;
        }

        public ValueTask ToValueTask()
        {
            if (IsSucceeded)
                return new ValueTask();

            return new ValueTask(_promise, 0);
        }

        public void ThrowIfFailed()
        {
            _promise?.ThrowIfFailed();
        }

        Future IFuture.ToFuture()
        {
            return this;
        }

        Future<TCast> IFuture.ToFuture<TCast>()
        {
            return ToFutureOf<TCast>();
        }

        internal Future<T> ToFutureOf<T>()
        {
            if (_promise is Promise<T> promise)
                return new Future<T>(promise);

            throw new InvalidCastException();
        }

        #region Equality members

        public override int GetHashCode()
        {
            if (_promise != null)
                return _promise.GetHashCode();

            return 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is IFuture future)
                return future.Equals(this);

            return false;
        }

        public bool Equals(Future other)
        {
            if (ReferenceEquals(_promise, other._promise))
                return true;

            if (_promise == null || other._promise == null)
                return false;

            return _promise.Equals(other._promise);
        }

        public static bool operator ==(Future left, Future right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Future left, Future right)
        {
            return !left.Equals(right);
        }

        #endregion
    }

    [StructLayout(LayoutKind.Sequential)]
    [AsyncMethodBuilder(typeof(FutureAsyncMethodBuilder<>))]
    public readonly struct Future<T> : IEquatable<Future<T>>, IEquatable<Future>, IFuture<T>
    {
        // This must be the first field to be able to reinterpret_cast to Future
        private readonly Promise<T> _promise;
        private readonly T _value;

        public bool IsCompleted => _promise == null || _promise.IsCompleted;

        public bool IsSucceeded => _promise == null || _promise.IsSucceeded;

        public bool IsFailed => _promise != null && _promise.IsFailed;

        public T Value
        {
            get
            {
                if (_promise != null)
                    return _promise.Value;

                return _value;
            }
        }

        public Exception Exception => _promise?.Exception;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Future(T value)
        {
            _promise = null;
            _value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Future(Promise<T> promise)
        {
            _promise = promise ?? throw new ArgumentNullException(nameof(promise));
            _value = default;
        }

        internal Promise<T> GetPromise()
        {
            return _promise;
        }

        public FutureAwaiter<T> GetAwaiter()
        {
            return new FutureAwaiter<T>(this);
        }

        /// <summary>
        /// Continue the execution on the specified scheduler after an await
        /// </summary>
        /// <param name="scheduler">The scheduler</param>
        public ScheduledFutureAwaitable<T> ContinueOn(FutureScheduler scheduler)
        {
            return new ScheduledFutureAwaitable<T>(this, scheduler);
        }

        public Future ContinueWith(Action<Future<T>> action, FutureScheduler scheduler = null)
        {
            if (IsCompleted && FutureScheduler.IsInline(scheduler))
            {
                try
                {
                    action(this);
                    return new Future();
                }
                catch (Exception ex)
                {
                    return Future.FromException(ex);
                }
            }

            var continueWith = new ContinueWithPromiseOf<T>(_promise, action, scheduler);
            _promise.AddContinuation(continueWith);

            return continueWith.Future;
        }

        public Future<TResult> ContinueWith<TResult>(Func<Future<T>, TResult> func, FutureScheduler scheduler = null)
        {
            if (IsCompleted && FutureScheduler.IsInline(scheduler))
            {
                try
                {
                    return new Future<TResult>(func(this));
                }
                catch (Exception ex)
                {
                    return Future.FromException<TResult>(ex);
                }
            }

            var continueWith = new ContinueWithPromiseOf<T, TResult>(_promise, func, scheduler);
            _promise.AddContinuation(continueWith);

            return continueWith.Future;
        }

        public Task<T> ToTask()
        {
            if (IsCompleted)
            {
                if (IsSucceeded)
                    return Task.FromResult(Value);

                if (IsFailed)
                    return Task.FromException<T>(Exception);
            }

            var completion = new TaskCompletionSourceContinuation<T>(_promise);
            _promise.AddContinuation(completion);

            return completion.Task;
        }

        public void ThrowIfFailed()
        {
            _promise?.ThrowIfFailed();
        }

        Future IFuture.ToFuture()
        {
            return this;
        }

        internal Future<TCast> ToFutureOf<TCast>()
        {
            return _promise != null ? new Future<TCast>((Promise<TCast>)(object)_promise) : new Future<TCast>((TCast)(object)_value);
        }

        Future<TCast> IFuture.ToFuture<TCast>()
        {
            return ToFutureOf<TCast>();
        }

        public static implicit operator Future(Future<T> future)
        {
            if (future._promise != null)
                return new Future(future._promise);

            return new Future(new CompletedCastPromise<T>(future._value));
        }

        public static explicit operator Future<T>(Future future)
        {
            return future.ToFutureOf<T>();
        }

        #region Equality members

        public override int GetHashCode()
        {
            if (_promise != null)
                return _promise.GetHashCode();

            return _value?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is Future<T> future)
                return Equals(future);

            if (obj is Future futureBase)
                return Equals(futureBase);

            return false;
        }

        public bool Equals(Future<T> other)
        {
            if (_promise != null)
            {
                if (other._promise != null)
                    return _promise.Equals(other._promise);

                if (_promise is CompletedCastPromise<T> castedPromise)
                    return EqualityComparer<T>.Default.Equals(other._value, castedPromise.Value);

                return false;
            }

            if (other._promise != null)
            {
                if (_promise != null)
                    return _promise.Equals(other._promise);

                if (other._promise is CompletedCastPromise<T> castedPromise)
                    return EqualityComparer<T>.Default.Equals(_value, castedPromise.Value);

                return false;
            }

            return EqualityComparer<T>.Default.Equals(_value, other._value);
        }

        public bool Equals(Future other)
        {
            var otherPromise = other.GetPromise();

            if (otherPromise == null)
                return false;

            if (_promise == null)
            {
                if (otherPromise is CompletedCastPromise<T> castedPromise)
                    return EqualityComparer<T>.Default.Equals(_value, castedPromise.Value);

                return false;
            }

            return _promise.Equals(otherPromise);
        }

        public static bool operator ==(Future<T> left, Future<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Future<T> left, Future<T> right)
        {
            return !left.Equals(right);
        }

        #endregion
    }
}
