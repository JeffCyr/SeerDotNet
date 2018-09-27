# Seer.Futures
The purpose of `Seer.Futures` is to propose an alternative to `System.Threading.Tasks` based on Future/Promise.

## Similarities with System.Threading.Tasks
- `Future`/`Future<T>` is the equivalent of `Task`/`Task<T>`
-  `Promise<T>` is the equivalent of `TaskCompletionSource<T>`
- `FutureScheduler` is the equivalent of `TaskScheduler`

For basic usage, you can just replace `Task` by `Future` and `TaskCompletionSource<T>` by `Promise<T>`, the API is mostly the same.

```c#
public async Future DoSomethingAsync()
{
    await ...;
}
```

```c#
var promise = new Promise<int>();
promise.SetResult(10);
...
return promise.Future;

```

`Seer.Futures` is greatly inspired/copied from `System.Threading.Tasks`, it uses all the its tricks to acheive similar or sometime better performance.

## Differences
### A leaner API
`Seer.Futures` has a minimalist API.
- A `Future` can be either "not completed", "succeeded" or "failed". There is no "canceled" concept, to represent a canceled future it can be completed with an `OperationCanceledException`
- No equivalent to `TaskCreationOpions` or `TaskContinuationOptions`
- No concept of task hierarchy (`TaskCreationOption.AttachedToParent`)

Having fewer concepts makes the API easier to use, yet these basic concepts can be combined to implement most of what was removed from the `Task` API.

### Explicit vs Implicit threading context
`System.Threading.Tasks` defaults to an implicit threading model backed by `SynchronizationContext.Current` and `TaskScheduler.Current`. This choice was made to abstract threading away from the usage and magically schedule back the continuations on the thread that initiated the call (after an `await` or `ContinueWith`).

`System.Threading.Tasks` works great when you only use `TaskScheduler.Default`. But when you start using a custom `TaskScheduler`, you will start having inefficiencies by too many context switch or deadlocks if you forgot `.ConfigureAwait(false)`.

`Seer.Futures` defaults to an explicit threading context. This means that, by default, `await` continue on the same thread that completed the `Future`. You can still control which threading context should be used on the promise side with
```c#
void Promise.SetResult(T value, FutureScheduler scheduler)
```
 or on the future side with
 ```c#
 ScheduledFutureAwaitable Future.ContinueOn(FutureScheduler scheduler)
 ```
or
```c#
Future Future.ContinueWith(Action action, FutureScheduler scheduler)
```

The intended usage is that you rarely need to control the threading context on the future side (and no longer need `.ConfigureAwait(false)` in library code). But you need to think about where you want the continuations to run when you complete a `Promise` (most common answer is `FutureScheduler.Inline` or `FutureScheduler.ThreadPool`).

## ContextualFuture
