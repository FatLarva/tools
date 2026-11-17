# Declarative structured processes

`Tools.StructuredProcess.Declarative` provides a source-generated linear process alongside the existing `Simple.AsyncLinearProcess`. The existing implementation and its API are unchanged.

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

Call the process on the main thread. The linear executor constructs and awaits each step there, preserving the caller's synchronization context. Steps must honor their thread contract internally. `Thread = StepThread.Any` permits background execution; this first, linear executor still runs it on the calling thread. A parallel executor is not included yet.

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

The future parallel executor must stop starting steps on required failure, cancel those already running, and await their completion. The current linear executor has no other concurrently running steps to cancel.

## Declaration boundaries

Processes are top-level, non-generic, non-abstract partial classes directly deriving from `LinearProcess`. Steps and their data contracts must be accessible to the process. The generated constructor and `ExecuteAsync` belong to the generator.

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
