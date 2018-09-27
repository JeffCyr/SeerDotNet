using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks.Sources;
using Seer.Futures.Runtime;

namespace Seer.Futures
{
    public abstract class Promise : IValueTaskSource
    {
        protected static readonly object SucceededState = new object();

        public abstract bool IsCompleted { get; }

        public abstract bool IsSucceeded { get; }

        public abstract bool IsFailed { get; }

        public abstract Exception Exception { get; }

        internal abstract void AddContinuation(IPromiseContinuation continuation);

        public abstract void ThrowIfFailed();

        public static Promise<T> FromValue<T>(T value)
        {
            var promise = new Promise<T>();
            promise.SetValue(value);
            return promise;
        }

        public static Promise<T> FromException<T>(T value)
        {
            var promise = new Promise<T>();
            promise.SetValue(value);
            return promise;
        }

        public void AddContinuation(Action continuation, FutureScheduler scheduler = null)
        {
            AddContinuation(new PromiseContinuation(continuation, scheduler, ExecutionContextEx.Capture()));
        }

        public void AddContinuation(Action<object> continuation, object state, FutureScheduler scheduler = null)
        {
            AddContinuation(new PromiseContinuation(continuation, state, scheduler, ExecutionContextEx.Capture()));
        }

        public void UnsafeAddContinuation(Action continuation, FutureScheduler scheduler = null)
        {
            AddContinuation(new PromiseContinuation(continuation, scheduler, null));
        }

        public void UnsafeAddContinuation(Action<object> continuation, object state, FutureScheduler scheduler = null)
        {
            AddContinuation(new PromiseContinuation(continuation, state, scheduler, null));
        }

        #region IValueTaskSource Implementation

        private protected abstract ValueTaskSourceStatus GetValueTaskSourceStatus();

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            return GetValueTaskSourceStatus();
        }

        private protected abstract void OnValueTaskSourceCompleted(Action<object> continuation, object state, ValueTaskSourceOnCompletedFlags flags);

        void IValueTaskSource.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            OnValueTaskSourceCompleted(continuation, state, flags);
        }

        void IValueTaskSource.GetResult(short token)
        {
            ThrowIfFailed();
        }

        #endregion
    }

    public class Promise<T> : Promise, IFuture<T>, IValueTaskSource<T>
    {
        private class ContinuationList
        {
            private readonly IPromiseContinuation _continuation;
            private readonly ContinuationList _previous;
            private readonly int _count;

            public ContinuationList(IPromiseContinuation continuation, ContinuationList previous = null)
            {
                _continuation = continuation;
                _previous = previous;

                if (previous == null)
                    _count = 1;
                else
                    _count = previous._count + 1;
            }

            public ContinuationList Add(IPromiseContinuation continuation)
            {
                return new ContinuationList(continuation, this);
            }

            public void InvokeContinuations()
            {
                // Continuations are executed in the reverse order they were queued.
                // Otherwise it would need a recursive call or an array allocation.
                // Anyway the execution order can be undeterministic depending on the scheduler.

                var current = this;

                do
                {
                    current._continuation.Invoke();
                    current = current._previous;
                }
                while (current._previous != null);
            }
        }

        private T _unsafeValue;
        private object _unsafeState;

        public sealed override bool IsCompleted
        {
            get
            {
                object state = GetCurrentState();
                return IsCompletedState(state);
            }
        }

        public sealed override bool IsSucceeded
        {
            get
            {
                object state = GetCurrentState();
                ThrowIfNotCompleted(state);
                return IsSucceededState(state);
            }
        }

        public sealed override bool IsFailed
        {
            get
            {
                object state = GetCurrentState();
                ThrowIfNotCompleted(state);
                return IsFailedState(state);
            }
        }

        public sealed override Exception Exception
        {
            get
            {
                object state = GetCurrentState();
                ThrowIfNotCompleted(state);

                ExceptionDispatchInfo exception = state as ExceptionDispatchInfo;
                return exception?.SourceException;
            }
        }

        public T Value
        {
            get
            {
                object state = GetCurrentState();
                ThrowIfNotCompleted(state);
                ThrowIfFailed(state);

                Thread.MemoryBarrier();
                return _unsafeValue;
            }
        }

        public Future<T> Future => new Future<T>(this);

        public Promise()
        { }

        internal Promise(T value)
        {
            _unsafeValue = value;
            _unsafeState = SucceededState;
        }

        internal Promise(Exception exception)
        {
            _unsafeState = ExceptionDispatchInfo.Capture(exception);
        }

        public FutureAwaiter<T> GetAwaiter()
        {
            return new Future<T>(this).GetAwaiter();
        }

        /// <summary>
        /// Continue the execution on the specified scheduler after an await
        /// </summary>
        /// <param name="scheduler">The scheduler</param>
        public ScheduledFutureAwaitable<T> ContinueOn(FutureScheduler scheduler)
        {
            return new ScheduledFutureAwaitable<T>(new Future<T>(this), scheduler);
        }

        public bool SetValue(T value, FutureScheduler scheduler = null)
        {
            // Setting the value before changing the state to completed
            _unsafeValue = value;

            // Explicit MemoryBarrier to force _value to be visible by other cores before updating the state to Succeeded.
            Thread.MemoryBarrier();

            return TryCompletePromise(SucceededState, scheduler);
        }

        public bool SetException(Exception exception, FutureScheduler scheduler = null)
        {
            return TryCompletePromise(ExceptionDispatchInfo.Capture(exception), scheduler);
        }

        public sealed override void ThrowIfFailed()
        {
            object state = GetCurrentState();

            ThrowIfNotCompleted(state);
            ThrowIfFailed(state);
        }

        internal sealed override void AddContinuation(IPromiseContinuation continuation)
        {
            while (true)
            {
                object state = GetCurrentState();

                if (IsCompletedState(state))
                {
                    continuation.Invoke();
                    return;
                }

                object newState = UpdateContinuation(state, continuation);

                if (TryUpdateState(state, newState))
                    break;
            }
        }

        private static void InvokeContinuations(object continuations, FutureScheduler scheduler)
        {
            if (continuations == null)
                return;

            if (scheduler == null)
            {
                s_internalInvokeContinuations(continuations);
                return;
            }

            // Don't schedule twice if there is only one continuation with the same scheduler
            if (continuations is IPromiseContinuation singleContinuation && singleContinuation.Scheduler == scheduler)
            {
                singleContinuation.Invoke();
            }
            else
            {
                scheduler.UnsafeSchedule(s_internalInvokeContinuations, continuations);
            }
        }

        private static readonly Action<object> s_internalInvokeContinuations = (object continuations) =>
        {
            if (continuations is IPromiseContinuation singleContinuation)
            {
                singleContinuation.Invoke();
                return;
            }

            ContinuationList continuationList = (ContinuationList)continuations;
            continuationList.InvokeContinuations();
        };

        private static object UpdateContinuation(object existingContinuations, IPromiseContinuation newContinuation)
        {
            // The existing continuations can be in one of these forms:
            // - Null if there was no registered continuations
            // - IPromiseContinuation
            // - ContinuationList where each item is either an Action or a SchedulerContinuation

            // If _continuations was null, just assign it with the new continuation
            if (existingContinuations == null)
                return newContinuation;

            // If _continuations was already a List, add the new continuation
            if (existingContinuations is ContinuationList continuationList)
                return continuationList.Add(newContinuation);

            // If we are here, _continuations was assigned to a single continuation,
            // we reassign _continuations with a list containing the old and the new continuations

            continuationList = new ContinuationList((IPromiseContinuation)existingContinuations).Add(newContinuation);

            return continuationList;
        }

        private object GetCurrentState()
        {
            return Volatile.Read(ref _unsafeState);
        }

        private bool TryUpdateState(object expectedOldState, object newState)
        {
            var originalState = Interlocked.CompareExchange(ref _unsafeState, newState, expectedOldState);

            return ReferenceEquals(originalState, expectedOldState);
        }

        private protected void ResetState()
        {
            _unsafeValue = default;
            _unsafeState = null;
        }

        private bool TryCompletePromise(object completedState, FutureScheduler scheduler)
        {
            bool updated = false;

            while (true)
            {
                object currentState = GetCurrentState();

                if (IsCompletedState(currentState))
                    return false;

                object continuations = currentState;

                // Thread.Abort protection, if the state is set to completed, the continuations MUST be invoked
                try
                { }
                finally
                {
                    if (TryUpdateState(currentState, completedState))
                    {
                        InvokeContinuations(continuations, scheduler);
                        updated = true;
                    }
                }

                if (updated)
                    return true;
            }
        }

        private static bool IsCompletedState(object state)
        {
            return IsSucceededState(state) || IsFailedState(state);
        }

        private static bool IsSucceededState(object state)
        {
            return ReferenceEquals(SucceededState, state);
        }

        private static bool IsFailedState(object state)
        {
            return state is ExceptionDispatchInfo;
        }

        private static void ThrowIfNotCompleted(object state)
        {
            if (!IsCompletedState(state))
                throw new InvalidOperationException("The Future is not completed.");
        }

        private void ThrowIfFailed(object state)
        {
            ExceptionDispatchInfo exception = state as ExceptionDispatchInfo;
            exception?.Throw();
        }

        Future IFuture.ToFuture()
        {
            return new Future(this);
        }

        Future<TCast> IFuture.ToFuture<TCast>()
        {
            return new Future<TCast>((Promise<TCast>)(object)this);
        }

        bool IFuture.Equals(Future other)
        {
            return Future.Equals(other);
        }

        bool IFuture<T>.Equals(Future<T> other)
        {
            return Future.Equals(other);
        }

        #region IValueTaskSource Implementation

        private protected sealed override ValueTaskSourceStatus GetValueTaskSourceStatus()
        {
            var state = GetCurrentState();

            if (IsSucceededState(state))
                return ValueTaskSourceStatus.Succeeded;

            if (IsFailedState(state))
            {
                var exception = (ExceptionDispatchInfo)state;

                if (exception.SourceException is OperationCanceledException)
                    return ValueTaskSourceStatus.Canceled;

                return ValueTaskSourceStatus.Faulted;
            }

            return ValueTaskSourceStatus.Pending;
        }

        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token)
        {
            return GetValueTaskSourceStatus();
        }

        private protected sealed override void OnValueTaskSourceCompleted(Action<object> continuation, object state, ValueTaskSourceOnCompletedFlags flags)
        {
            FutureScheduler scheduler = null;
            ExecutionContext executionContext = null;

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext)
                scheduler = FutureScheduler.GetContextualScheduler();
            
            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext)
                executionContext = ExecutionContextEx.Capture();

            AddContinuation(new PromiseContinuation(continuation, state, scheduler, executionContext));
        }

        void IValueTaskSource<T>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            OnValueTaskSourceCompleted(continuation, state, flags);
        }

        T IValueTaskSource<T>.GetResult(short token)
        {
            return Value;
        }

        #endregion
    }

        internal class RunPromise : Promise<VoidType>
    {
        private readonly Action _action;

        public RunPromise(Action action)
        {
            _action = action;
        }

        public void Run()
        {
            try
            {
                _action();
                SetValue(VoidType.Value);
            }
            catch (Exception ex)
            {
                SetException(ex);
            }
        }
    }

    public class ResettablePromise<T> : Promise<T>
    {
        /// <summary>
        /// Reset the promise for reuse. The promise must no longer be in use.
        /// </summary>
        public void Reset()
        {
            ResetState();
        }
    }

    internal class RunPromise<T> : Promise<T>
    {
        private readonly Func<T> _func;

        public RunPromise(Func<T> func)
        {
            _func = func;
        }

        public void Run()
        {
            try
            {
                SetValue(_func());
            }
            catch (Exception ex)
            {
                SetException(ex);
            }
        }
    }
        internal class RunFuturePromise : Promise<VoidType>
    {
        private readonly Func<Future> _func;

        public RunFuturePromise(Func<Future> func)
        {
            _func = func;
        }

        public void Run()
        {
            try
            {
                var future = _func();

                future.ContinueWith(f =>
                {
                    if (f.IsFailed)
                    {
                        SetException(f.Exception);
                        return;
                    }

                    SetValue(VoidType.Value);
                });
            }
            catch (Exception ex)
            {
                SetException(ex);
            }
        }
    }

    internal class RunFuturePromise<T> : Promise<T>
    {
        private readonly Func<Future<T>> _func;

        public RunFuturePromise(Func<Future<T>> func)
        {
            _func = func;
        }

        public void Run()
        {
            try
            {
                var future = _func();

                future.ContinueWith(f =>
                {
                    if (f.IsFailed)
                    {
                        SetException(f.Exception);
                        return;
                    }

                    SetValue(f.Value);
                });
            }
            catch (Exception ex)
            {
                SetException(ex);
            }
        }
    }

    internal class ContinueWithPromise : Promise<VoidType>, IPromiseContinuation
    {
        private readonly Promise _parent;
        private readonly Action<Future> _action;
        private readonly FutureScheduler _continuationScheduler;
        private readonly ExecutionContext _executionContext;

        public FutureScheduler Scheduler => null;

        public ContinueWithPromise(Promise parent, Action<Future> action, FutureScheduler scheduler)
        {
            _parent = parent;
            _action = action;
            _continuationScheduler = scheduler;
            _executionContext = ExecutionContextEx.Capture();
        }

        private void CompleteContinuation()
        {
            try
            {
                _action(new Future(_parent));
                SetValue(VoidType.Value, _continuationScheduler);
            }
            catch (Exception ex)
            {
                SetException(ex, _continuationScheduler);
            }
        }

        public void Invoke()
        {
            ExecutionContextEx.Run(_executionContext, state => ((ContinueWithPromise)state).CompleteContinuation(), this);
        }
    }

    internal sealed class ContinueWithPromise<TResult> : Promise<TResult>, IPromiseContinuation
    {
        private readonly Promise _parent;
        private readonly Func<Future, TResult> _func;
        private readonly FutureScheduler _continuationScheduler;
        private readonly ExecutionContext _executionContext;

        public FutureScheduler Scheduler => null;

        public ContinueWithPromise(Promise parent, Func<Future, TResult> func, FutureScheduler scheduler)
        {
            _parent = parent;
            _func = func;
            _continuationScheduler = scheduler;
            _executionContext = ExecutionContextEx.Capture();
        }

        private void CompleteContinuation()
        {
            try
            {
                SetValue(_func(new Future(_parent)), _continuationScheduler);
            }
            catch (Exception ex)
            {
                SetException(ex, _continuationScheduler);
            }
        }

        public void Invoke()
        {
            ExecutionContextEx.Run(_executionContext, state => ((ContinueWithPromise<TResult>)state).CompleteContinuation(), this);
        }
    }

    internal class ContinueWithPromiseOf<T> : Promise<VoidType>, IPromiseContinuation
    {
        private readonly Promise<T> _parent;
        private readonly Action<Future<T>> _action;
        private readonly FutureScheduler _continuationScheduler;
        private readonly ExecutionContext _executionContext;

        public FutureScheduler Scheduler => null;

        public ContinueWithPromiseOf(Promise<T> parent, Action<Future<T>> action, FutureScheduler scheduler)
        {
            _parent = parent;
            _action = action;
            _continuationScheduler = scheduler;
            _executionContext = ExecutionContextEx.Capture();
        }

        private void CompleteContinuation()
        {
            try
            {
                _action(new Future<T>(_parent));
                SetValue(VoidType.Value, _continuationScheduler);
            }
            catch (Exception ex)
            {
                SetException(ex, _continuationScheduler);
            }
        }

        public void Invoke()
        {
            ExecutionContextEx.Run(_executionContext, state => ((ContinueWithPromiseOf<T>)state).CompleteContinuation(), this);
        }
    }

    internal sealed class ContinueWithPromiseOf<T, TResult> : Promise<TResult>, IPromiseContinuation
    {
        private readonly Promise<T> _parent;
        private readonly Func<Future<T>, TResult> _func;
        private readonly FutureScheduler _continuationScheduler;
        private readonly ExecutionContext _executionContext;

        public FutureScheduler Scheduler => null;

        public ContinueWithPromiseOf(Promise<T> parent, Func<Future<T>, TResult> func, FutureScheduler scheduler)
        {
            _parent = parent;
            _func = func;
            _continuationScheduler = scheduler;
            _executionContext = ExecutionContextEx.Capture();
        }

        private void CompleteContinuation()
        {
            try
            {
                SetValue(_func(new Future<T>(_parent)), _continuationScheduler);
            }
            catch (Exception ex)
            {
                SetException(ex, _continuationScheduler);
            }
        }

        public void Invoke()
        {
            ExecutionContextEx.Run(_executionContext, state => ((ContinueWithPromiseOf<T, TResult>)state).CompleteContinuation(), this);
        }
    }

    internal sealed class CompletedCastPromise<T> : Promise<T>
    {
        internal CompletedCastPromise(T value)
            : base(value)
        { }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is CompletedCastPromise<T> other && EqualityComparer<T>.Default.Equals(Value, other.Value);
        }

        public override int GetHashCode()
        {
            return EqualityComparer<T>.Default.GetHashCode(Value);
        }
    }
}
