using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using Seer.Futures;

namespace Seer.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //BenchmarkRunner.Run<FutureAsyncMethodBuilderCompletedAsynchronouslyTest>();

            var sw = Stopwatch.StartNew();
            var test = new VoidFutureAsyncMethodBuilderCompletedSynchronouslyTest();
            int n;
            Console.WriteLine(GC.CollectionCount(0));
            for (int i = 0; i < 10000000; i++)
            {
                //n = test.AsyncTask();
                //n = test.AsyncFuture();
                n = test.AsyncContextualFuture();
            }
            Console.WriteLine(GC.CollectionCount(0));

            Console.WriteLine(sw.Elapsed);
        }
    }

    public class MultipleRuntimeConfig : ManualConfig
    {
        public MultipleRuntimeConfig()
        {
            Add(Job.Default.With(CsProjCoreToolchain.NetCoreApp21));
            Add(Job.Default.With(CsProjClassicNetToolchain.Net471)); 
        }
    }

    [MemoryDiagnoser]
    [Config(typeof(MultipleRuntimeConfig))]
    public class PromiseCompletionTest
    {
        [Benchmark]
        public Future<int> Promise()
        {
            var promise = new Promise<int>();
            promise.SetValue(99);
            return promise.Future;
        }

        [Benchmark]
        public Task<int> TaskCompletionSource()
        {
            var completion = new TaskCompletionSource<int>();
            completion.TrySetResult(99);
            return completion.Task;
        }
    }

    [MemoryDiagnoser]
    [Config(typeof(MultipleRuntimeConfig))]
    public class FutureAsyncMethodBuilderCompletedSynchronouslyTest
    {
        [Benchmark]
        public int AsyncFuture()
        {
            async Future<int> Test()
            {
                return 1;
            }

            return Test().Value;
        }

        [Benchmark]
        public int AsyncCachedTask()
        {
            async Task<int> Test()
            {
                return 1;
            }

            return Test().Result;
        }

        [Benchmark]
        public int AsyncTask()
        {
            async Task<int> Test()
            {
                return 99;
            }

            return Test().Result;
        }

        [Benchmark]
        public int AsyncValueTask()
        {
            async ValueTask<int> Test()
            {
                return 1;
            }

            return Test().Result;
        }

        [Benchmark]
        public int AsyncContextualFuture()
        {
            async ContextualFuture<int> Test()
            {
                return 1;
            }

            return Test().Value;
        }
    }

    [MemoryDiagnoser]
    [Config(typeof(MultipleRuntimeConfig))]
    public class VoidFutureAsyncMethodBuilderCompletedSynchronouslyTest
    {
        [Benchmark]
        public int AsyncFuture()
        {
            int n = 0;
            async Future Test()
            {
                await FutureScheduler.Inline;
                ++n;
            }

            Test();
            return n;
        }

        [Benchmark]
        public int AsyncContextualFuture()
        {
            int n = 0;
            async ContextualFuture Test()
            {
                await FutureScheduler.Inline;
                ++n;
            }
            Test();
            return n;
        }
    }

    [MemoryDiagnoser]
    [Config(typeof(MultipleRuntimeConfig))]
    public class FutureAsyncMethodBuilderCompletedAsynchronouslyTest
    {
        [Benchmark]
        public int AsyncFuture()
        {
            async Future<int> Test()
            {
                await FutureScheduler.Inline;
                return 1;
            }

            return Test().Value;
        }

        [Benchmark]
        public int AsyncCachedTask()
        {
            async Task<int> Test()
            {
                await FutureScheduler.Inline;
                return 1;
            }

            return Test().Result;
        }

        [Benchmark]
        public int AsyncTask()
        {
            async Task<int> Test()
            {
                await FutureScheduler.Inline;
                return 99;
            }

            return Test().Result;
        }

        [Benchmark]
        public int AsyncValueTask()
        {
            async ValueTask<int> Test()
            {
                await FutureScheduler.Inline;
                return 1;
            }

            return Test().Result;
        }

        [Benchmark]
        public int AsyncContextualFuture()
        {
            async ContextualFuture<int> Test()
            {
                await FutureScheduler.Inline;
                return 1;
            }

            return Test().Value;
        }
    }
}
