# State machines

`StateMachine<TId>` runs synchronous states. `StateMachineAsync<TId>` runs asynchronous states. They are independent implementations. `TId` must be a value type: use an enum for named states or `int` for integer IDs.

## State contracts and composition

Derive asynchronous states from `StateBaseAsync<TId, TPayload, TOutput>` and implement `OnEnterAsync(payload, context)` and `OnExitAsync()`. Synchronous states derive from `StateBase<TId, TPayload, TOutput>` and implement `OnEnter(payload, context)` and `OnExit()`.

`TPayload` must implement `IStatePayload`; `TOutput` must implement `IStateOutput`. These marker interfaces make the purpose of each application contract explicit. An empty struct implementing the appropriate interface is sufficient when a state has no input or output data. The initial payload and every resolved payload must match the receiving state's declared type; null is rejected.

Each entry receives a `StateContext<TId, TOutput>` with its `Cancellation` token and one transition method: `void RequestTransition(TId destination, TOutput output)`. The state supplies its own output. It does not construct the destination state's payload.

The typed context forwards to `IStateTransitionSink<TId>`; external code can call the same public `RequestTransition(destination, output)` method on the machine. An external caller supplies the active state's `IStateOutput`, not the destination's payload. There must be an active state when the request is made.

Implement `IStateTransitionResolver<TId>` in the application's composition layer and pass it to the machine constructor alongside the states. Its `Resolve(source, destination, output)` method returns the destination's `IStatePayload`. This layer knows both contracts and performs the conversion. An unsupported route or output should throw. The destination ID and payload are checked synchronously during the request, before leaving the source. An invalid request throws to its caller and leaves the active state running.

## Entry and transition lifetime

Each entry requests at most one next transition. In the async machine, the request records the next destination and returns without awaiting it. A request made inside Enter is processed after that Enter finishes. A second pending request is a contract violation and throws.

The async order is: await Enter, wait for a requested transition, cancel the entry token, await Exit, then enter the destination. `StateEntered` fires after Enter completes. `CurrentState` is cleared before Exit starts. Lifecycle failures propagate from `RunAsync` after the active entry's cleanup.

The async context holds a separate `TransitionGatekeeper<TId>` through the sink interface. The transitor has no reference or callback to the machine. It calls the existing resolver, checks the destination and payload, and stores the prepared `Transition` in one reusable awaitable. The machine's public request method forwards to that same transitor. `WaitAsync` returns the prepared state and payload; Enter, Exit and state cancellation remain in the machine.

Requests made during Enter remain available when the runner starts waiting. Completing a pending wait schedules the runner asynchronously, so a request does not invoke Exit inline. The transitor resets for the next entry. One explicit registration on the owner's token cancels its wait during shutdown; cancellation throws instead of returning an empty transition. Each entry still has its own linked cancellation source, cancelled before Exit; its token covers the full active state lifetime.

The state owns every object, subscription, resource and task it creates during an entry, including a partially completed Enter. Exit must release or finish all of them before returning. Only an explicit transfer to a global owner permits something to outlive the entry.

Either await an operation in Enter so it finishes before departure, or retain its task, observe the entry cancellation, and await its completion in Exit. Requesting cancellation alone does not finish an operation. Exit must also remove event subscriptions and finish callbacks belonging to that entry before it returns.

Use the async machine on the application's synchronization context, normally Unity's main thread. Its owner runs `RunAsync(initialId, initialPayload, cancellation)` and awaits that task for the full lifetime. To shut down, the owner cancels its token and awaits the same task so Exit completes. Cancellation is reported after cleanup through the returned task.

For example, an application runner supplies its states, resolver, initial payload and owner's token:

```csharp
using System.Threading;
using System.Threading.Tasks;
using StateMachine;

public async Task RunAsync(CancellationToken ownerToken)
{
    var machine = new StateMachineAsync<ScreenId>(_states, _transitions);
    await machine.RunAsync(ScreenId.Menu, new MenuPayload(), ownerToken);
}
```

The synchronous machine uses `Start(initialId, initialPayload)` and `Stop()`; disposing it calls Stop. Its Enter and Exit finish on the calling thread. A request made during Enter is deferred until Enter returns; a request made while the synchronous machine is idle processes the transition before the call returns. Use the asynchronous machine when cleanup must await work.

## Application example and checks

The GameStarter template keeps output-to-payload conversion in `GameTransitionManager` in Bootstrap. Startup, reload and session navigation are application policy implemented by its states and resolver.

In Unity's Package Manager, select Tools and import **State Machine Examples** from Samples for runnable synchronous and asynchronous examples. Their README explains the menu commands and state ownership.

To run the regression suite, install Unity Test Framework (`com.unity.test-framework`) and import **State Machine Tests**. Open **Window → General → Test Runner**, select **EditMode**, and run the Tools state-machine tests. Each behavior is a separate test; the async tests run on Unity's actual synchronization context. See the [tests README](../../Samples~/StateMachineTests/README.md).
