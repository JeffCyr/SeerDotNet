using System.Threading;
using System.Threading.Tasks;
using Seer.Futures;
using Seer.Futures.Tests;
using Xunit;

namespace Seer.Tests.Futures
{
    public class ContextualFutureFacts
    {
        [Fact]
        public async Task ContextualAsyncTest()
        {
            var syncContext = new TestSynchronizationContext();
            var scheduler = FutureScheduler.FromSynchronizationContext(syncContext);

            async ContextualFuture LocalTest()
            {
                syncContext.AssertCurrentContext();

                await LocalTest2();
            }

            async Future LocalTest2()
            {
                await FutureScheduler.ThreadPool;
                Assert.Null(SynchronizationContext.Current);
            }

            await scheduler.Run(() => LocalTest());
        }
    }
}