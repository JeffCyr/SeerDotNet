using System;
using System.Threading;
using System.Threading.Tasks;

namespace Seer.Futures
{
    internal interface IPromiseContinuation
    {
        FutureScheduler Scheduler { get; }
        void Invoke();
    }

    internal sealed class PromiseContinuation : IPromiseContinuation
    {
        private readonly Action<object> _action;
        private readonly object _state;
        private readonly ExecutionContext _executionContext;

        public FutureScheduler Scheduler { get; }

        public PromiseContinuation(Action action, FutureScheduler scheduler, ExecutionContext executionContext)
            : this(state => ((Action)state).Invoke(), action, scheduler, executionContext)
        { }

        public PromiseContinuation(Action<object> action, object state, FutureScheduler scheduler, ExecutionContext executionContext)
        {
            _action = action;
            _state = state;
            _executionContext = executionContext;
            Scheduler = FutureScheduler.IsInline(scheduler) ? null : scheduler;
        }

        private static readonly ContextCallback s_executionContextCallback = state =>
        {
            var c = (PromiseContinuation)state;
            c._action(c._state);
        };

        private static readonly Action<object> s_schedulerCallback = state =>
        {
            var c = (PromiseContinuation)state;

            if (c._executionContext == null)
                c._action(c._state);
            else
                ExecutionContextEx.Run(c._executionContext, s_executionContextCallback, c);
        };

        public void Invoke()
        {
            if (Scheduler == null)
            {
                if (_executionContext == null)
                {
                    _action(_state);
                }
                else
                {
                    ExecutionContextEx.Run(_executionContext, s_executionContextCallback, this);
                }
            }
            else
            {
                Scheduler.UnsafeSchedule(s_schedulerCallback, this);
            }
        }
    }

    internal sealed class TaskCompletionSourceContinuation : TaskCompletionSource<VoidType>, IPromiseContinuation
    {
        private readonly Promise _promise;
        public FutureScheduler Scheduler => null;

        public TaskCompletionSourceContinuation(Promise promise)
        {
            _promise = promise;
        }

        public void Invoke()
        {
            if (_promise.IsSucceeded)
                SetResult(VoidType.Value);
            else
                SetException(_promise.Exception);
        }
    }

    internal sealed class TaskCompletionSourceContinuation<T> : TaskCompletionSource<T>, IPromiseContinuation
    {
        private readonly Promise<T> _promise;
        public FutureScheduler Scheduler => null;

        public TaskCompletionSourceContinuation(Promise<T> promise)
        {
            _promise = promise;
        }

        public void Invoke()
        {
            if (_promise.IsSucceeded)
                SetResult(_promise.Value);
            else
                SetException(_promise.Exception);
        }
    }
}