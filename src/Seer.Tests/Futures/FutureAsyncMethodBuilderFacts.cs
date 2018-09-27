using System.Threading.Tasks;
using Xunit;

namespace Seer.Futures.Tests
{
    public class FutureAsyncMethodBuilderFacts
    {
        [Fact]
        public async Task Test()
        {
            async Future<int> LocalTest()
            {
                await Task.Delay(100);

                return await Future.FromValue(5);
            }

            int n = await LocalTest();

            Assert.Equal(5, n);
        }
    }
}