using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tools.Samples.StateMachineTests
{
    internal static class TestTask
    {
        // EditMode tests use Unity's own synchronization context while the runner advances frames.
        public static IEnumerator Run(Task task)
        {
            var elapsed = Stopwatch.StartNew();
            var timeout = TimeSpan.FromSeconds(120);
            while (!task.IsCompleted && elapsed.Elapsed < timeout)
            {
                yield return null;
            }

            if (!task.IsCompleted)
            {
                Assert.Fail("The asynchronous test did not finish within " + timeout.TotalSeconds + " seconds.");
            }

            task.GetAwaiter().GetResult();
        }

        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            using var timerCancellation = new CancellationTokenSource();
            Task timer = Task.Delay(timeout, timerCancellation.Token);
            Task completed = await Task.WhenAny(task, timer);
            if (completed != task)
            {
                throw new TimeoutException("The expected operation did not finish within " + timeout.TotalSeconds + " seconds.");
            }

            timerCancellation.Cancel();
            await task;
        }

        public static async Task WithCancellation(this Task task, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellation.Register(() =>
            {
                canceled.TrySetCanceled();
            });
            await Task.WhenAny(task, canceled.Task);
            cancellation.ThrowIfCancellationRequested();
            await task;
        }
    }
}
