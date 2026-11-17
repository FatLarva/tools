using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tools.Samples.StructuredProcess.MatchPreparation;
using UnityEditor;
using UnityEngine;

namespace Tools.Samples.StructuredProcess
{
    public static class MatchPreparationExample
    {
        [MenuItem("Tools/Structured Process/Run 20-step match example")]
        public static async void Run()
        {
            await RunAsync();
        }

        public static async Task<MatchPreparationWork> RunAsync(CancellationToken cancellationToken = default)
        {
            var work = new MatchPreparationWork();
            var process = new PrepareMatchProcess(new PrepareMatchProcess.Input
            {
                Work = work,
                PlayerId = PrepareMatchProcess.Player,
                OpponentId = PrepareMatchProcess.Opponent
            });
            var result = await process.ExecuteAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.Error, result.Exception);
            }

            work.Validate(PrepareMatchProcess.Steps);
            var trace = work.Snapshot();
            var text = new StringBuilder();
            text.AppendLine($"Match ready: {trace.Length} steps, {work.DataEdges} data edges, {work.ExplicitEdges} explicit edges.");
            text.AppendLine($"Peak active operations: {work.PeakActive}; on the main thread: {work.PeakMainActive}.");
            text.AppendLine($"Typed result fingerprint: {result.Output.Ready.Value.Fingerprint}.");
            foreach (var step in trace)
            {
                text.AppendLine($"{step.StartedMilliseconds,7:F1}–{step.CompletedMilliseconds,7:F1} ms | thread {step.StartThread} → {step.EndThread} | {step.Name}");
            }

            Debug.Log(text.ToString());
            return work;
        }
    }
}
