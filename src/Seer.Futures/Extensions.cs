using System;
using System.Threading;

namespace Seer.Futures
{
    internal static class Extensions
    {
        internal static Action CaptureExecutionContext(this Action action)
        {
            var context = ExecutionContextEx.Capture();

            if (context == null)
                return action;

            return () => ExecutionContextEx.Run(context, state => ((Action)state).Invoke(), action);
        }
    }
}