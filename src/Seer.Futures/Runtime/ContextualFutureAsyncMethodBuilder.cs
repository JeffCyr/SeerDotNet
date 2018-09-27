using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Seer.Futures.Runtime
{
    public struct ContextualFutureAsyncMethodBuilder
    {
        private static readonly Promise<VoidType> CompletedPromise = new Promise<VoidType>(VoidType.Value);

        private Promise<VoidType> _promise;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)] 
        public ContextualFuture Task
        {
            get
            {
                void ThrowPromiseNotSet() => throw new InvalidOperationException("The Promise has not been set yet");

                if (_promise == null)
                {
                    ThrowPromiseNotSet();
                    return default;
                }

                return new ContextualFuture(_promise);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ContextualFutureAsyncMethodBuilder Create()
        {
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            // Piggyback AsyncTaskMethodBuilder to get the ExecutionContext undo behavior
            default(AsyncTaskMethodBuilder<VoidType>).Start(ref stateMachine);
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult()
        {
            if (_promise == null)
                _promise = CompletedPromise;
            else
                _promise.SetValue(VoidType.Value);
        }

        public void SetException(Exception exception)
        {
            if (_promise != null)
                _promise.SetException(exception);
            else
                _promise = new Promise<VoidType>(exception);
        }

        private IStateMachineBox GetStateMachineBox<TStateMachine>(in TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            Debugger.NotifyOfCrossThreadDependency();

            ContextualStateMachineBox<TStateMachine, VoidType> box;
            var context = ExecutionContextEx.Capture();
            if (_promise != null)
            {
                box = (ContextualStateMachineBox<TStateMachine, VoidType>)_promise;
                
                if (box.ExecutionContext != context)
                    box.ExecutionContext = context;
            }
            else
            {
                _promise = box = new ContextualStateMachineBox<TStateMachine, VoidType>();
                box.StateMachine = stateMachine;
                box.ExecutionContext = context;
            }

            return box;
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            var box = GetStateMachineBox(stateMachine);
            awaiter.OnCompleted(box.MoveNextAction);
        }

        /// <summary>Schedules the state machine to proceed to the next action when the specified awaiter completes.</summary>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        /// <param name="awaiter">the awaiter</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            var box = GetStateMachineBox(stateMachine);

            // The null tests here ensure that the jit can optimize away the interface
            // tests when TAwaiter is a ref type.

            if ((null != (object)default(TAwaiter)) && (awaiter is IStateMachineBoxAwareAwaiter))
            {
                ((IStateMachineBoxAwareAwaiter)awaiter).UnsafeOnCompleted(box);
                return;
            }

            awaiter.UnsafeOnCompleted(box.MoveNextAction);
        }
    }

    public struct ContextualFutureAsyncMethodBuilder<T>
    {
        private static readonly Promise<T> CompletedSynchronously = new Promise<T>();
        private Promise<T> _promise;
        private T _synchronousResult;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)] 
        public ContextualFuture<T> Task
        {
            get
            {
                void ThrowPromiseNotSet() => throw new InvalidOperationException("The Promise has not been set yet");

                if (_promise == null)
                {
                    ThrowPromiseNotSet();
                    return default;
                }

                if (ReferenceEquals(_promise, CompletedSynchronously))
                    return new ContextualFuture<T>(_synchronousResult);

                return new ContextualFuture<T>(_promise);
            }
        }

        public static ContextualFutureAsyncMethodBuilder<T> Create()
        {
            return default;
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            // Piggyback AsyncTaskMethodBuilder to get the ExecutionContext undo behavior
            default(AsyncTaskMethodBuilder<TStateMachine>).Start(ref stateMachine);
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        { }

        public void SetResult(T result)
        {
            if (_promise == null)
            {
                _promise = CompletedSynchronously;
                _synchronousResult = result;
            }
            else
            {
                _promise.SetValue(result);
            }
        }

        public void SetException(Exception exception)
        {
            if (_promise != null)
            {
                _promise.SetException(exception);
            }
            else
            {
                _promise = new Promise<T>(exception);
            }
        }

        private IStateMachineBox GetStateMachineBox<TStateMachine>(in TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            Debugger.NotifyOfCrossThreadDependency();

            ContextualStateMachineBox<TStateMachine, T> box;
            var context = ExecutionContextEx.Capture();
            if (_promise != null)
            {
                box = (ContextualStateMachineBox<TStateMachine, T>)_promise;
                
                if (box.ExecutionContext != context)
                    box.ExecutionContext = context;
            }
            else
            {
                _promise = box = new ContextualStateMachineBox<TStateMachine, T>();
                box.StateMachine = stateMachine;
                box.ExecutionContext = context;
            }

            return box;
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            var box = GetStateMachineBox(stateMachine);
            awaiter.OnCompleted(box.MoveNextAction);
        }

        /// <summary>Schedules the state machine to proceed to the next action when the specified awaiter completes.</summary>
        /// <typeparam name="TAwaiter">The type of the awaiter.</typeparam>
        /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
        /// <param name="awaiter">the awaiter</param>
        /// <param name="stateMachine">The state machine.</param>
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            var box = GetStateMachineBox(stateMachine);

            // The null tests here ensure that the jit can optimize away the interface
            // tests when TAwaiter is a ref type.

            if ((null != (object)default(TAwaiter)) && (awaiter is IStateMachineBoxAwareAwaiter))
            {
                ((IStateMachineBoxAwareAwaiter)awaiter).UnsafeOnCompleted(box);
                return;
            }

            awaiter.UnsafeOnCompleted(box.MoveNextAction);
        }
    }

    internal class ContextualStateMachineBox<TStateMachine, TResult> : Promise<TResult>, IStateMachineBox
        where TStateMachine : IAsyncStateMachine
    {
        public TStateMachine StateMachine;
        public ExecutionContext ExecutionContext;
        private Action _moveNextAction;

        public Action MoveNextAction => _moveNextAction ?? (_moveNextAction = MoveNext);
        public FutureScheduler Scheduler { get; set; }

        public ContextualStateMachineBox()
        {
            Scheduler = FutureScheduler.GetContextualScheduler();
        }

        public void MoveNext()
        {
            ExecutionContextEx.Run(ExecutionContext, state => ((ContextualStateMachineBox<TStateMachine, TResult>)state).StateMachine.MoveNext(), this);
        }

        public void SetScheduler(FutureScheduler scheduler)
        {
            //throw new NotSupportedException("Scheduled awaiter not supported in ContextualFuture async methods");
        }

        void IPromiseContinuation.Invoke()
        {
            if (Scheduler == null)
                MoveNext();
            else
                Scheduler.UnsafeSchedule(state => ((ContextualStateMachineBox<TStateMachine, TResult>)state).MoveNext(), this);
        }
    }
}