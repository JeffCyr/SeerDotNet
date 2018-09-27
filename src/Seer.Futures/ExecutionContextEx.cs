using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Seer.Futures
{
    using RunDelegateType = Action<ExecutionContext, ContextCallback, object, bool>;

    internal class ExecutionContextEx
    {
        private class CallbackData
        {
            public readonly ContextCallback Callback;
            public readonly object State;
            public readonly SynchronizationContext PreviousSyncContext;

            public CallbackData(ContextCallback callback, object state, SynchronizationContext previousSyncContext)
            {
                Callback = callback;
                State = state;
                PreviousSyncContext = previousSyncContext;
            }
        }

#if NETFRAMEWORK
        private static readonly Func<SynchronizationContext> s_currentSyncContext;
        private static readonly Func<ExecutionContext> s_capture;
        private static readonly RunDelegateType s_run;

        static ExecutionContextEx()
        {
            var method = typeof(SynchronizationContext).GetProperty("CurrentNoFlow", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetGetMethod(true);
            Debug.Assert(method != null, "SynchronizationContext.CurrentNoFlow not found.");

            s_currentSyncContext = method != null ?
                (Func<SynchronizationContext>)Delegate.CreateDelegate(typeof(Func<SynchronizationContext>), method) :
                () => SynchronizationContext.Current;

            method = typeof(ExecutionContext).GetMethod("FastCapture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            Debug.Assert(method != null, "ExecutionContext.FastCapture not found.");

            s_capture = method != null ?
                (Func<ExecutionContext>)Delegate.CreateDelegate(typeof(Func<ExecutionContext>), method) :
                ExecutionContext.Capture;

            method = typeof(ExecutionContext).GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(ExecutionContext), typeof(ContextCallback), typeof(object), typeof(bool) }, null);

            Debug.Assert(method != null, "ExecutionContext.Run(ExecutionContext, ContextCallback, object, bool) not found.");

            s_run = method != null ?
                (RunDelegateType)Delegate.CreateDelegate(typeof(RunDelegateType), method) :
                SafeRun;
        }
#endif

        public static SynchronizationContext CurrentSyncContext
        {
            get
            {
#if NETFRAMEWORK
                return s_currentSyncContext();
#else
                return SynchronizationContext.Current;
#endif
            }
        }

        public static ExecutionContext Capture()
        {
#if NETFRAMEWORK
            return s_capture();
#else
            return ExecutionContext.Capture();
#endif
        }

        public static void Run(ExecutionContext context, ContextCallback callback, object state)
        {
            if (context == null)
            {
                callback(state);
                return;
            }

#if NETFRAMEWORK
            s_run(context, callback, state, true);
#else
            ExecutionContext.Run(context, callback, state);
#endif
        }

        private static void SafeRun(ExecutionContext context, ContextCallback callback, object state, bool ignored)
        {
            var previousSyncContext = SynchronizationContext.Current;
            ExecutionContext.Run(context, s =>
            {
                var capture = (CallbackData)s;

                if (capture.PreviousSyncContext != SynchronizationContext.Current)
                    SynchronizationContext.SetSynchronizationContext(capture.PreviousSyncContext);

                capture.Callback(capture.State);
            }, new CallbackData(callback, state, previousSyncContext));
        }
    }
}