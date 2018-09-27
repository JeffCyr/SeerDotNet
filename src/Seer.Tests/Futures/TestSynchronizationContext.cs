using System;
using System.Collections.Concurrent;
using System.Threading;
using Xunit;

namespace Seer.Futures.Tests
{
    public class TestSynchronizationContext : SynchronizationContext, IDisposable
    {
        private class CallbackData
        {
            public SendOrPostCallback Callback;
            public object State;

            public CallbackData(SendOrPostCallback callback, object state)
            {
                Callback = callback;
                State = state;
            }
        }

        private readonly BlockingCollection<CallbackData> _workQueue;
        private readonly Thread _thread;

        public TestSynchronizationContext()
        {
            _workQueue = new BlockingCollection<CallbackData>();
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Start();
        }

        private void Run()
        {
            SetSynchronizationContext(this);

            foreach (var c  in _workQueue.GetConsumingEnumerable())
            {
                c.Callback(c.State);
            }
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            _workQueue.Add(new CallbackData(callback, state));
        }

        public override void Send(SendOrPostCallback callback, object state)
        {
            throw new NotImplementedException();
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public void AssertCurrentContext()
        {
            Assert.Equal(this, SynchronizationContext.Current);
            Assert.Equal(_thread, Thread.CurrentThread);
        }

        public void Dispose()
        {
            _workQueue.CompleteAdding();
        }
    }
}