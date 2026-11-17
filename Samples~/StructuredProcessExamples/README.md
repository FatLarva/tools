# Structured Process Examples

For a larger graph, run **Tools > Structured Process > Run 20-step match example** and open `MatchPreparation/PrepareMatchProcess.cs`. Its README includes the full dependency graph and thread assignments. The run validates all data and explicit dependencies against a recorded execution trace.

Import this sample from the Tools package and select **Tools > Structured Process > Run example** or **Run parallel example**. The Tools package includes the generator DLL; Source Gen Builder is not required to run either example.

`PrepareProfilesProcess` declares two occurrences of `LoadProfileStep`. Their IDs and input/output aliases distinguish player and opponent data while sharing the source, logger and fallback profile. `PublishProfilesStep` appears first in the array but runs after both producers because of its data dependencies. `ReportProfilesStep` is optional and explicitly ordered after publishing. Main-thread execution is the default.

`PrepareProfilesParallelProcess` uses the same graph and step classes with the `ParallelProcess` base. Both profile loads start before either has completed, while staying on the main thread. Publishing waits for both outputs. Analytics runs on the thread pool because it is declared `Any`. Shared services must support the overlap allowed by the graph; add explicit `After` dependencies when a service requires serialized access.

The editor example deliberately makes the opponent profile unavailable. The required loading step catches the expected I/O failure, logs it, and returns the supplied fallback. Cancellation is not swallowed.

Construct the process with its generated Input and await its typed result:

```csharp
var process = new PrepareProfilesProcess(new PrepareProfilesProcess.Input
{
    Source = source,
    View = view,
    Analytics = analytics,
    Log = logger,
    PlayerId = playerId,
    OpponentId = opponentId,
    Fallback = fallbackProfile
}, logger);

var result = await process.ExecuteAsync(cancellationToken);
if (result.IsSuccess)
{
    ProfilePair profiles = result.Output.Profiles;
}
```

Step construction, awaiting and data transfer are generated. Use the linear base for sequential execution or the parallel base to run ready steps concurrently.
