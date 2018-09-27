using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Seer.Futures
{
    public readonly partial struct Future
    {
        public static Future CompletedFuture => new Future();

        public static Future<T> FromValue<T>(T value)
        {
            return new Future<T>(value);
        }

        public static Future FromException(Exception exception)
        {
            return FromException<VoidType>(exception);
        }

        public static Future<T> FromException<T>(Exception exception)
        {
            return new Promise<T>(exception).Future;
        }

        public static Future<VoidType> WhenAll(params Future[] futures)
        {
            if (futures == null)
                throw new ArgumentNullException(nameof(futures));

            return InternalWhenAll(futures);
        }

        public static Future<VoidType> WhenAll(IEnumerable<Future> futures)
        {
            if (futures is ICollection<Future> collection)
                return InternalWhenAll(collection);

            if (futures == null)
                throw new ArgumentNullException(nameof(futures));

            return InternalWhenAll(futures.ToArray());
        }

        private static Future<VoidType> InternalWhenAll(ICollection<Future> futures)
        {
            if (futures == null)
                throw new ArgumentNullException(nameof(futures));

            Promise<VoidType> promise = new Promise<VoidType>();

            int futureCount = futures.Count;
            int completedCount = 0;
            Action continuation = () =>
            {
                int count = Interlocked.Increment(ref completedCount);

                if (count == futureCount)
                {
                    List<Exception> exceptions = null;

                    foreach (var future in futures)
                    {
                        var exception = future.Exception;
                        if (exception != null)
                        {
                            if (exceptions == null)
                                exceptions = new List<Exception>();

                            exceptions.Add(exception);
                        }
                    }

                    if (exceptions == null)
                        promise.SetValue(VoidType.Value);
                    else
                        promise.SetException(new AggregateException(exceptions));
                }
            };

            foreach (var future in futures)
            {
                if (future.IsCompleted)
                    continuation();
                else
                    future.GetPromise().UnsafeAddContinuation(continuation);
            }

            return promise.Future;
        }

        //TODO Find a solution for the Future != Future<T> issue
        public static Future<Future> WhenAny(params Future[] futures)
        {
            if (futures == null)
                throw new ArgumentNullException(nameof(futures));

            return InternalWhenAny(futures);
        }

        public static Future<Future> WhenAny(IEnumerable<Future> futures)
        {
            if (futures is ICollection<Future> collection)
                return InternalWhenAny(collection);

            if (futures == null)
                throw new ArgumentNullException(nameof(futures));

            return InternalWhenAny(futures.ToArray());
        }

        private static Future<Future> InternalWhenAny(ICollection<Future> futures)
        {
            if (futures == null)
                throw new ArgumentNullException(nameof(futures));

            Promise<Future> promise = new Promise<Future>();

            Action<Future> continuation = winner =>
            {
                promise.SetValue(winner);
            };

            foreach (var future in futures)
            {
                if (promise.IsCompleted)
                    break;

                if (future.IsCompleted)
                {
                    continuation(future);
                    break;
                }

                var f = future;
                future.GetPromise().UnsafeAddContinuation(() => continuation(f));
            }

            return promise.Future;
        }
    }
}
