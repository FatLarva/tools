# State machine examples

These two examples run in the Editor and log their execution order to the Console. They do not require a scene or Play Mode.

## Import and run

1. Open **Window → Package Management → Package Manager** and select **Tools**.
2. Under **Samples**, import **State Machine Examples**.
3. Open the Console and run **Tools → State Machine Samples → Run Synchronous Example**.
4. Run **Tools → State Machine Samples → Run Asynchronous Example**. After a short delay, the Console shows `Asynchronous example complete; all states have exited.`

The imported code is in `Assets/Samples/Tools/<version>/State Machine Examples/Editor`. The examples use an Editor-only assembly and are excluded from player builds.

## Synchronous door

`SynchronousDoorExample.cs` composes **Closed** and **Open** states. Each state has its own input and output contracts implementing marker interfaces. The Closed state's output identifies who opened the door; the Open state's output specifies whether to lock it after closing. `DoorTransitions` knows both contracts and creates the destination payload.

An external controller calls `machine.RequestTransition(Open, ClosedOutput)`. A method on the active Open state then requests a transition back through its `StateContext<DoorId, OpenOutput>`. Both requests use the same resolver. External callers supply the active state's output; a state can pass only its declared output type through its context.

The Console shows this order: `Closed.Enter` → `Closed.Exit` → `Open.Enter` → `Open.Exit` → `Closed.Enter` → `Closed.Exit`. Calling `Stop` triggers the final exit. In the synchronous machine, a request made outside Enter completes the entire transition on the calling thread before returning. A request made during Enter is deferred until Enter finishes.

Use this machine when entry and resource cleanup complete synchronously.

## Asynchronous session

`AsynchronousSessionExample.cs` runs a finite **Loading → Menu → Game → shutdown** sequence. Delays simulate service work; the example does not make network requests or load a real profile.

- Loading awaits an operation directly in `OnEnterAsync`, then requests Menu through its context. The machine waits for the operation and Enter to finish before starting Exit.
- Menu starts and retains a preview task. The task uses the state's cancellation token and continues after Enter returns.
- The machine owner receives `StateEntered(Menu)` and requests Game externally. Before `Menu.Exit`, the machine cancels the state's token. The preview task observes cancellation, then spends another 100 ms cleaning up.
- `Menu.Exit` awaits the task's completion. The Console therefore shows `Game.Enter` only after `Menu preview cleanup finished` and `Menu.Exit complete`.
- After Game enters, the owner cancels the overall token and awaits `RunAsync` until `Game.Exit` finishes. The example reports completion after cleanup.

`DemoApplication` owns the machine and its overall cancellation source. `RunAsync` represents the machine's full lifetime, including cleanup. `LoadingState`, `MenuState`, and `GameState` know their own contracts; `SessionTransitions` converts source output to destination payload.

The asynchronous state's context forwards requests to a separate `TransitionGatekeeper`. It calls `SessionTransitions`, validates the destination payload, and prepares the transition immediately. External requests use the same path. Invalid requests throw to their caller synchronously. The machine obtains a valid transition from `WaitAsync` and executes it after the current Enter finishes. The gatekeeper has no reference to the machine and does not execute Enter or Exit.

The only `async void` method is the Unity MenuItem entry point, which catches errors and logs them to the Console. The demonstration itself returns a `Task` through `RunAsynchronousExampleAsync`, so other Editor code can await it.

Use the asynchronous machine when entry or exit must await operations. A state owns the tasks, objects, subscriptions, and resources it creates. Exit completes or releases them, including anything created during a partially completed Enter. Only an explicit transfer to a global owner allows a resource to outlive the state.
