# Structured Process Examples

Import this sample from the Tools package and select **Tools > Structured Process > Run example**. The Tools package includes the generator DLL; Source Gen Builder is not required to run the example.

`PrepareProfilesProcess` declares two occurrences of `LoadProfileStep`. Their IDs and input/output aliases distinguish player and opponent data while sharing the source, logger and fallback profile. `PublishProfilesStep` appears first in the array but runs after both producers because of its data dependencies. `ReportProfilesStep` is optional and explicitly ordered after publishing. Main-thread execution is the default; `Any` on analytics permits a future parallel executor to use a background thread.

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

Step construction, awaiting and data transfer are generated. The process runs sequentially; the declaration already distinguishes data dependencies, explicit ordering and permitted execution threads for a later parallel executor.
