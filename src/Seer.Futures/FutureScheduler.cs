using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Seer.Futures.Runtime;

namespace Seer.Futures
{
    public abstract class FutureScheduler
    {
        public struct FutureSchedulerAwaiter : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
        {
            private readonly FutureScheduler _scheduler;

            public bool IsCompleted => false;

            public FutureSchedulerAwaiter(FutureScheduler scheduler)
            {
                _scheduler = scheduler;
            }

            public void GetResult()
            { }

            public void OnCompleted(Action continuation)
            {
                _scheduler.Schedule(continuation);
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                _scheduler.UnsafeSchedule(continuation);
            }

            void IStateMachineBoxAwareAwaiter.UnsafeOnCompleted(IStateMachineBox box)
            {
                box.SetScheduler(_scheduler);
                box.Invoke();
            }
        }

        [ThreadStatic]
        private static FutureScheduler t_contextualScheduler;

        public static FutureScheduler ContextualScheduler => t_contextualScheduler;

        public static FutureScheduler Inline { get; } = new InlineFutureScheduler();
        public static FutureScheduler NewThread { get; } = new NewThreadFutureScheduler();
        public static FutureScheduler ThreadPool { get; } = new ThreadPoolFutureScheduler();

        internal static bool IsInline(FutureScheduler scheduler)
        {
            return scheduler == null || ReferenceEquals(scheduler, Inline);
        }

        public static FutureScheduler FromSynchronizationContext(SynchronizationContext syncContext)
        {
            ThrowHelper.NotNull(syncContext, nameof(syncContext));

            return new SynchronizationContextFutureScheduler(syncContext);
        }

        public static FutureScheduler FromTaskScheduler(TaskScheduler scheduler)
        {
            ThrowHelper.NotNull(scheduler, nameof(scheduler));

            if (scheduler == TaskScheduler.Default)
                return new ThreadPoolFutureScheduler();

            return new TaskSchedulerFutureScheduler(scheduler);
        }

        public static void SetContextualScheduler(FutureScheduler scheduler)
        {
            t_contextualScheduler = scheduler;
        }

        internal static FutureScheduler GetContextualScheduler()
        {
            var futureScheduler = t_contextualScheduler;
            if (futureScheduler != null)
                return futureScheduler;

            var syncContext = ExecutionContextEx.CurrentSyncContext;
            if (syncContext != null && syncContext.GetType() != typeof(SynchronizationContext))
                return new SynchronizationContextFutureScheduler(syncContext);

            if (Thread.CurrentThread.IsThreadPoolThread)
                return ThreadPool;

            var taskScheduler = TaskScheduler.Current;
            if (taskScheduler != TaskScheduler.Default)
                return new TaskSchedulerFutureScheduler(taskScheduler);

            return Inline;
        }

        protected virtual bool CanScheduleInline()
        {
            return ReferenceEquals(t_contextualScheduler, this);
        }

        public Future Run(Action action)
        {
            var promise = new RunPromise(action);

            Schedule(state => ((RunPromise)state).Run(), promise);

            return promise.Future;
        }

        public Future<TResult> Run<TResult>(Func<TResult> func)
        {
            var promise = new RunPromise<TResult>(func);

            Schedule(state => ((RunPromise<TResult>)state).Run(), promise);

            return promise.Future;
        }

        public Future Run(Func<ContextualFuture> func)
        {
            return Run(() => (Future)func());
        }

        public Future<TResult> Run<TResult>(Func<ContextualFuture<TResult>> func)
        {
            return Run(() => (Future<TResult>)func());
        }

        public Future Run(Func<Future> func)
        {
            var promise = new RunFuturePromise(func);

            Schedule(state => ((RunFuturePromise)state).Run(), promise);

            return promise.Future;
        }

        public Future<TResult> Run<TResult>(Func<Future<TResult>> func)
        {
            var promise = new RunFuturePromise<TResult>(func);

            Schedule(state => ((RunFuturePromise<TResult>)state).Run(), promise);

            return promise.Future;
        }

        public void ScheduleAllowInline(Action action)
        {
            if (CanScheduleInline())
            {
                action();
                return;
            }

            Schedule(action);
        }

        public void Schedule(Action action)
        {
            Schedule(state => ((Action)state).Invoke(), action);
        }

        public void ScheduleAllowInline(Action<object> action, object state = null)
        {
            if (CanScheduleInline())
            {
                action(state);
                return;
            }

            Schedule(action, state);
        }

        public abstract void Schedule(Action<object> action, object state = null);

        public void UnsafeScheduleAllowInline(Action action)
        {
            if (CanScheduleInline())
            {
                action();
                return;
            }

            UnsafeSchedule(action);
        }

        public void UnsafeSchedule(Action action)
        {
            UnsafeSchedule(state => ((Action)state).Invoke(), action);
        }

        public void UnsafeScheduleAllowInline(Action<object> action, object state = null)
        {
            if (CanScheduleInline())
            {
                action(state);
                return;
            }

            UnsafeSchedule(action, state);
        }

        public virtual void UnsafeSchedule(Action<object> action, object state = null)
        {
            Schedule(action, state);
        }

        public FutureSchedulerAwaiter GetAwaiter()
        {
            return new FutureSchedulerAwaiter(this);
        }
    }

    public sealed class InlineFutureScheduler : FutureScheduler
    {
        internal InlineFutureScheduler()
        { }

        protected override bool CanScheduleInline()
        {
            return true;
        }

        public override void Schedule(Action<object> action, object state = null)
        {
            action(state);
        }

        public override void UnsafeSchedule(Action<object> action, object state = null)
        {
            action(state);
        }
    }

    public sealed class ThreadPoolFutureScheduler : FutureScheduler
    {
        internal ThreadPoolFutureScheduler()
        { }

        protected override bool CanScheduleInline()
        {
            return Thread.CurrentThread.IsThreadPoolThread;
        }

        public override void Schedule(Action<object> action, object state = null)
        {
#if NETCOREAPP2_1
            System.Threading.ThreadPool.QueueUserWorkItem(action, state, preferLocal:true);
#else
            System.Threading.ThreadPool.QueueUserWorkItem(new WaitCallback(action), state);
#endif
        }

        public override void UnsafeSchedule(Action<object> action, object state = null)
        {
#if NETCOREAPP2_1
            // preferLocal is unfortunately not exposed in UnsafeQueueUserWorkItem
            System.Threading.ThreadPool.QueueUserWorkItem(action, state, preferLocal:true);
#else
            System.Threading.ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(action), state);
#endif
        }
    }

    public sealed class NewThreadFutureScheduler : FutureScheduler
    {
        public string ThreadName { get; }

        public NewThreadFutureScheduler()
            : this("NewThreadFutureScheduler")
        { }

        public NewThreadFutureScheduler(string threadName)
        {
            ThreadName = threadName;
        }

        protected override bool CanScheduleInline()
        {
            return false;
        }

        public override void Schedule(Action<object> action, object state = null)
        {
            Thread t = new Thread(new ParameterizedThreadStart(action));
            t.IsBackground = true;
            t.Name = ThreadName;
            t.Start(state);
        }
    }

    public sealed class SynchronizationContextFutureScheduler : FutureScheduler
    {
        private readonly SynchronizationContext _syncContext;

        internal SynchronizationContextFutureScheduler(SynchronizationContext syncContext)
        {
            _syncContext = syncContext;
        }

        protected override bool CanScheduleInline()
        {
            return ExecutionContextEx.CurrentSyncContext == _syncContext;
        }

        public override void Schedule(Action<object> action, object state = null)
        {
            _syncContext.Post(new SendOrPostCallback(action), state);
        }
    }

    public sealed class TaskSchedulerFutureScheduler : FutureScheduler
    {
        private readonly TaskScheduler _scheduler;

        public TaskSchedulerFutureScheduler(TaskScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        protected override bool CanScheduleInline()
        {
            return TaskScheduler.Current == _scheduler;
        }

        public override void Schedule(Action<object> action, object state = null)
        {
            Task.Factory.StartNew(action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, _scheduler);
        }
    }
}
