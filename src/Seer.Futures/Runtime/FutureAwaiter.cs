using System;
using System.Runtime.CompilerServices;

namespace Seer.Futures.Runtime
{
    public readonly struct FutureAwaiter : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
    {
        private readonly Future _future;

        public bool IsCompleted => _future.IsCompleted;

        public FutureAwaiter(Future future)
        {
            _future = future;
        }

        public void GetResult() => _future.ThrowIfFailed();

        public void OnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(continuation);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately
            continuation();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.UnsafeAddContinuation(continuation);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately
            continuation();
        }

        void IStateMachineBoxAwareAwaiter.UnsafeOnCompleted(IStateMachineBox box)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(box);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately
            box.MoveNext();
        }
    }

    public readonly struct ScheduledFutureAwaiter : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
    {
        private readonly Future _future;
        private readonly FutureScheduler _scheduler;

        public bool IsCompleted => false;

        public ScheduledFutureAwaiter(Future future, FutureScheduler scheduler)
        {
            ThrowHelper.NotNull(scheduler, nameof(scheduler));

            _future = future;
            _scheduler = scheduler;
        }

        public void GetResult() => _future.ThrowIfFailed();

        public void OnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(continuation, _scheduler);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately

            if (_scheduler != null)
            {
                _scheduler.Schedule(continuation);
                return;
            }

            continuation();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.UnsafeAddContinuation(continuation, _scheduler);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately

            if (_scheduler != null)
            {
                _scheduler.UnsafeSchedule(continuation);
                return;
            }

            continuation();
        }

        void IStateMachineBoxAwareAwaiter.UnsafeOnCompleted(IStateMachineBox box)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(box);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately

            if (_scheduler != null)
            {
                box.SetScheduler(_scheduler);
                box.Invoke();
                return;
            }

            box.MoveNext();
        }
    }

    public readonly struct ScheduledFutureAwaitable
    {
        private readonly Future _future;
        private readonly FutureScheduler _scheduler;

        public ScheduledFutureAwaitable(Future future, FutureScheduler scheduler)
        {
            _future = future;
            _scheduler = scheduler;
        }

        public ScheduledFutureAwaiter GetAwaiter()
        {
            return new ScheduledFutureAwaiter(_future, _scheduler);
        }
    }

    public readonly struct FutureAwaiter<T> : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
    {
        private readonly Future<T> _future;

        public bool IsCompleted => _future.IsCompleted;

        public FutureAwaiter(Future<T> future)
        {
            _future = future;
        }

        public T GetResult() => _future.Value;

        public void OnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(continuation);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately
            continuation();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.UnsafeAddContinuation(continuation);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately
            continuation();
        }

        void IStateMachineBoxAwareAwaiter.UnsafeOnCompleted(IStateMachineBox box)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(box);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately
            box.MoveNext();
        }
    }

    public readonly struct ScheduledFutureAwaiter<T> : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
    {
        private readonly Future<T> _future;
        private readonly FutureScheduler _scheduler;

        public bool IsCompleted => false;

        public ScheduledFutureAwaiter(Future<T> future, FutureScheduler scheduler)
        {
            ThrowHelper.NotNull(scheduler, nameof(scheduler));

            _future = future;
            _scheduler = scheduler;
        }

        public T GetResult() => _future.Value;

        public void OnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(continuation, _scheduler);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately

            if (_scheduler != null)
            {
                _scheduler.Schedule(continuation);
                return;
            }

            continuation();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.UnsafeAddContinuation(continuation, _scheduler);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately

            if (_scheduler != null)
            {
                _scheduler.UnsafeSchedule(continuation);
                return;
            }

            continuation();
        }

        void IStateMachineBoxAwareAwaiter.UnsafeOnCompleted(IStateMachineBox box)
        {
            var promise = _future.GetPromise();

            if (promise != null)
            {
                promise.AddContinuation(box);
                return;
            }

            // If promise is null, it means the future was created from a result,
            // invoke the continuation immediately

            if (_scheduler != null)
            {
                box.SetScheduler(_scheduler);
                _scheduler.UnsafeSchedule(state => ((IStateMachineBox)state).MoveNext(), box);
                return;
            }

            box.MoveNext();
        }
    }

    public readonly struct ScheduledFutureAwaitable<T>
    {
        private readonly Future<T> _future;
        private readonly FutureScheduler _scheduler;

        public ScheduledFutureAwaitable(Future<T> future, FutureScheduler scheduler)
        {
            _future = future;
            _scheduler = scheduler;
        }

        public ScheduledFutureAwaiter<T> GetAwaiter()
        {
            return new ScheduledFutureAwaiter<T>(_future, _scheduler);
        }
    }
}