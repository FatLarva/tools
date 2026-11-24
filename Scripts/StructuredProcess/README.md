# Declarative structured processes

`Tools.StructuredProcess.Declarative` provides source-generated linear and parallel processes alongside the existing `Simple.AsyncLinearProcess`. The existing implementation and its API are unchanged.

The runtime is a separate assembly inside the Tools package. Source Gen Builder is optional: the package includes a prebuilt analyzer, and its source can also be built with the .NET SDK.

## Declare a process

```csharp
public partial class PrepareProcess : LinearProcess
{
    public static readonly StepDescription[] Steps =
    {
        new StepDescription(typeof(ShowProfileStep)),
        new StepDescription(typeof(LoadProfileStep)),
        new StepDescription(typeof(ReportReadyStep))
        {
            Optional = true,
            Thread = StepThread.Any,
            After = { typeof(ShowProfileStep) }
        }
    };

    public struct Output : IProcessOutput
    {
        public Profile Profile;
    }
}
```

Each step is a concrete class implementing `IProcessStep<ItsOutput>`. It declares public nested `Input : IProcessInput` and `Output : IProcessOutput` structs, takes `Input` in its constructor, and exposes:

```csharp
public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
{
    var profile = await _input.Repository.LoadAsync(_input.AccountId, cancellationToken);
    return StepResult<Output>.Success(new Output { Profile = profile });
}
```

The generator supplies the process's nested `Input`, constructor `(Input input, ILog logger = null)`, and `ExecuteAsync(CancellationToken cancellationToken = default)`. The latter returns `ValueTask<StepResult<Output>>`. `StepResult<TOutput>` is the single typed outcome container used by both steps and the process. Successful outputs travel through concrete typed locals; there is no interface-based result storage or result boxing.

`StepResult<Output>.Failure("Reason")` represents expected failure. Exceptions from step construction/execution are captured as failures, preserving the original exception. Reading `Output` from a failed result throws. A default-initialized result is a failure.

Call the process on the main thread. The linear executor constructs and awaits each step there, preserving the caller's synchronization context. Steps must honor their thread contract internally. `Thread = StepThread.Any` permits background execution; the linear executor still runs it on the calling thread.

## Parallel execution

Derive the declaration from `ParallelProcess` instead of `LinearProcess` to generate a parallel flow. The same step contracts, bindings, aliases, optionality and ordering rules apply.

The generated scheduler starts all ready steps, awaits `Task.WhenAny`, observes completed results, then starts newly ready successors. A successor runs as soon as its own dependencies finish; it does not wait for unrelated steps or an entire topological layer. `Main` steps are called directly and may overlap while awaiting. `Any` steps are constructed and executed through `Task.Run`. Coordination, output assembly and failure logging resume on the caller's main-thread synchronization context.

Each step retains a `Task<StepResult<ItsOutput>>` and its concrete output local. A separate collection of base `Task` references is used only to wait for completions; outputs are never boxed or retrieved through runtime casts.

One linked cancellation source belongs to each parallel run. A required failure stops further launches, cancels other started steps, and awaits every one of them before returning the failure. External cancellation does the same cleanup and propagates cancellation. Additional failures during shutdown are logged without replacing the primary required-step failure. Optional failures do not cancel independent work and still satisfy `After` dependencies.

## Data binding

- An unnamed input uses the only output of exactly the same type, excluding its own step. Multiple matches are a generation error.
- `[Data("id")]` binds by exact type and ID. One output may feed multiple inputs.
- An input without a producer becomes a field of the generated process `Input`. Shared external inputs are deduplicated by type and ID.
- Process `Output` binds to step outputs or existing external inputs. It never silently invents another external input.
- Members are public instance fields or properties. Step inputs and process outputs must be writable; step outputs must be readable.
- Generated input names use the data ID, or the original member name when unnamed. For example, `player-id` becomes `PlayerId`. Naming collisions are diagnosed.

Use occurrence aliases to reuse one step type:

```csharp
new StepDescription(typeof(LoadProfileStep))
{
    Id = "Player",
    Inputs = { ["account-id"] = "player-id" },
    Outputs = { ["profile"] = "player-profile" }
}
```

An alias's key is the member's local `[Data]` ID, or its name if unlabelled. Aliases affect only that occurrence; the step class stays reusable. Every occurrence of a repeated step type needs a unique explicit `Id`. Unique types use their type as their ID.

## Ordering, errors and cancellation

Data dependencies and `After` form a directed graph. Array order does not establish execution order. Independent ready steps use a deterministic order; add `After` if their relative order matters. `After` accepts explicit string IDs and unique step types. Cycles and unresolved/ambiguous references are generation errors.

Steps are required by default. A required failure stops the linear process before another step starts. `Optional = true` logs failure and allows execution to continue. An `After` dependency waits for completion, so an optional failure still satisfies ordering. Consuming an optional step's output is a generation error, including in the process's output.

Keep fallback inside a required step: accept fallback data in its Input, handle the expected operational failure, log it, and return a successful Output. Do not convert process cancellation into fallback success.

Cancellation propagates as `OperationCanceledException`, including from optional steps. The executor awaits the current step and its cleanup before returning. A step owns its operations: await their completion/cleanup before completing `ExecuteAsync`; do not leave detached work behind. Cancellation is checked before construction and after completion, including when a step returns success. An unrelated `OperationCanceledException` while the process token is not cancelled is treated as a step failure.

The parallel executor drains all started steps on failure or cancellation. The linear executor has no other concurrently running steps to cancel.

## Trace a run

Pass a new `ProcessTrace` to the generated overload when you want to inspect an execution. The existing overload runs without recording.

```csharp
var trace = new ProcessTrace();
var result = await process.ExecuteAsync(trace, cancellationToken);
var snapshot = trace.GetSnapshot();
```

`GetSnapshot()` can be called while the process is running. It provides ordered events with elapsed times, step durations, failures, and the final process outcome. Each step exposes its explicit ID or its fully qualified type name, dependency indexes, optionality, and declared thread policy. `Waiting` measures the time from the start of the run until the executor observes all dependency completions. Steps without dependencies begin `Ready`, which covers the time before launch. A step that never launches ends as `Skipped`. The run time of a launched step includes preparation and, for a parallel `Any` step, time queued for the worker.

In the Editor, open **Window > Tools > Structured Process Trace**. Pass a trace to `StructuredProcessTraceWindow.ShowTrace(trace)` from editor code that references the `Tools.StructuredProcess.Editor` assembly. The window follows one trace until you clear it or open another. A **BG** badge and a purple stripe identify parallel steps dispatched to the thread pool. The linear executor uses the calling thread for every step. To try it, import the **Structured Process Examples** UPM sample and choose **Tools > Structured Process > Run 20-step match example**.

## Declaration boundaries

Generated C# uses ordinary imports and short type names where Roslyn resolves them unambiguously. Qualification is retained where a user type/member would shadow the intended symbol. Identifier escaping (`@`) is retained for keywords, and omitted for ordinary names. The scheduler's `task0`, `task1`, etc. are local variables, not public step IDs; twenty occurrences produce `task0` through `task19`.

Processes are top-level, non-generic, non-abstract partial classes directly deriving from `LinearProcess` or `ParallelProcess`. Steps and their data contracts must be accessible to the process. The generated constructor and `ExecuteAsync` belong to the generator.

`Steps` must be a directly initialized static `StepDescription[]`. Explicit/implicit array creation and target-typed `new` are supported. Settings use constants, `const` fields, `nameof`, `typeof`, nested `After` initializers and indexer alias initializers. Calls such as `BuildSteps()` are diagnosed. The generator reads syntax and symbols; it does not execute constructors or setters. Mutating the declaration array at runtime does not change generated execution.

| Diagnostic | Meaning |
| --- | --- |
| SPG001 | Unsupported declaration or nonconstant setting |
| SPG002 | Invalid/inaccessible step or data contract |
| SPG003 | Duplicate/missing occurrence ID |
| SPG004 | Unknown or ambiguous explicit dependency |
| SPG005 | Duplicate producer or ambiguous data |
| SPG006 | Missing process output or external input name collision |
| SPG007 | Data depends on an optional producer |
| SPG008 | Dependency cycle |
| SPG009 | Invalid data ID or alias |

Import **Structured Process Examples** or **Structured Process Tests** from the Tools package's Samples section. Tests require Unity Test Framework. Generator build instructions are in `Generator~/README.md`.
