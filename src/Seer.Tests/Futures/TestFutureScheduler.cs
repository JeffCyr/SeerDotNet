using System;
using System.Collections.Concurrent;
using System.Threading;
using Xunit;

namespace Seer.Futures.Tests
{
    public class TestFutureScheduler : FutureScheduler
    {
        private class CallbackData
        {
            public readonly Action<object> Callback;
            public readonly object State;

            public CallbackData(Action<object> callback, object state)
            {
                Callback = callback;
                State = state;
            }
        }

        private readonly BlockingCollection<CallbackData> _workQueue;
        private readonly Thread _thread;

        public TestFutureScheduler(string threadName)
        {
            _workQueue = new BlockingCollection<CallbackData>();
            _thread = new Thread(Run);
            _thread.Name = threadName;
            _thread.IsBackground = true;
            _thread.Start();
        }

        private void Run()
        {
            FutureScheduler.SetContextualScheduler(this);

            foreach (var c  in _workQueue.GetConsumingEnumerable())
            {
                c.Callback(c.State);
            }
        }

        public override void Schedule(Action<object> action, object state = null)
        {
            _workQueue.Add(new CallbackData(action, state));
        }

        public void AssertCurrentContext()
        {
            Assert.Equal(this, FutureScheduler.ContextualScheduler);
            Assert.Equal(_thread, Thread.CurrentThread);
        }

        public void Dispose()
        {
            _workQueue.CompleteAdding();
        }
    }
}