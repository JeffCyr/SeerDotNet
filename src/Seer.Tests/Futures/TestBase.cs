using System.Threading.Tasks;

namespace Seer.Futures.Tests
{
    public class TestBase
    {
        public async Task<T> AwaitFuture<T>(Future<T> future)
        {
            return await future;
        }

        public Future<T> CreateCompleted<T>(T value)
        {
            Promise<T> promise = new Promise<T>();
            promise.SetValue(value);
            return promise.Future;
        }
    }
}