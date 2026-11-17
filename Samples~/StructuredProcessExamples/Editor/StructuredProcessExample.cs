using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using UnityEditor;
using UnityEngine;

namespace Tools.Samples.StructuredProcess
{
    public static class StructuredProcessExample
    {
        [MenuItem("Tools/Structured Process/Run example")]
        public static async void Run()
        {
            var services = new ExampleServices();
            var process = new PrepareProfilesProcess(new PrepareProfilesProcess.Input
            {
                Source = services,
                View = services,
                Analytics = services,
                Log = Log.Default,
                PlayerId = "player",
                OpponentId = "offline-opponent",
                Fallback = new PlayerProfile("Guest")
            });

            var result = await process.ExecuteAsync();
            if (result.IsSuccess)
            {
                Debug.Log($"Typed process output: {result.Output.Profiles.Player.DisplayName} / {result.Output.Profiles.Opponent.DisplayName}");
            }
        }

        [MenuItem("Tools/Structured Process/Run parallel example")]
        public static async void RunParallel()
        {
            var services = new ExampleServices();
            var process = new PrepareProfilesParallelProcess(new PrepareProfilesParallelProcess.Input
            {
                Source = services,
                View = services,
                Analytics = services,
                Log = Log.Default,
                PlayerId = "player",
                OpponentId = "offline-opponent",
                Fallback = new PlayerProfile("Guest")
            });

            var result = await process.ExecuteAsync();
            if (result.IsSuccess)
            {
                Debug.Log($"Parallel typed process output: {result.Output.Profiles.Player.DisplayName} / {result.Output.Profiles.Opponent.DisplayName}");
            }
        }

        private sealed class ExampleServices : IProfileSource, IProfileView, IProfileAnalytics
        {
            public async ValueTask<PlayerProfile> LoadAsync(string accountId, CancellationToken cancellationToken)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                if (accountId == "offline-opponent")
                {
                    throw new IOException("The example deliberately simulates an unavailable profile.");
                }

                return new PlayerProfile("Player");
            }

            public void Show(ProfilePair profiles)
            {
                Debug.Log($"Displaying {profiles.Player.DisplayName} and {profiles.Opponent.DisplayName} on the main thread.");
            }

            public ValueTask ReportAsync(CancellationToken cancellationToken)
            {
                Debug.Log("Reporting runs after the view has been updated.");
                return default;
            }
        }
    }
}
