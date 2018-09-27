> Disclaimer: This library is in an experimental stage

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

### `Future`/`Future<T>` are structs
In `Seer.Futures` there is no ValueTask/Task concept, `Future` is itself a struct and the mutable state is kept in a `Promise` private field. Like `ValueTask`, future does not need to allocate when an operation is completed synchronously.

`Future` and `Future<T>` is emulating polymorphism with implicit and explicit cast operators, so this is possible:
```c#
Future<int> futureInt = Future.FromValue(10);
Future future = futureInt;
Future<int> futureInt2 = (Future<int>)future;
```

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
When you actually want to be back on the implicit threading context after each await in an async method (e.g. the UI thread), you can use a `ContextualFuture`/`ContextualFuture<T>` async method:

```c#
public async ContextualFuture DoSomethingAsync()
{
    Future future = ...;
    await future;
    
    Task task = ...;
    await task;
}
```

The implicit context is captured at the moment the async method is called. Then the AsyncStateMachine is responsible of continuing on the initial threading context.

This is different than a Task async method where each awaiter are responsible of capturing the threading context and scheduling the continuation back on it.

## AsyncAwaitLocals
> Not implemented yet

`AsyncLocals` are flowed in every async callback; `Task.Run`, `ThreadPool.QueueUserWorkItem`, `Task.ContinueWith`, `new Thread(action).Start()`, `CancellationToken.RegisterCancellation`, etc. But in reality, the main use-case is to flow between awaits of an async method and flowing it elsewhere is dangerous for memory leaks.

The idea is to mimic `AsyncLocals`, but `AsyncAwaitLocals` are not part of the `ExecutionContext` and are only flowed by async methods.

> `AsyncAwaitLocals` can only work with `Future`/`Future<T>`/`ContextualFuture`/`ContextualFuture<T>` async methods. The use is limited unless this become part of the runtime and all AsyncMethodBuilder are flowing it.
