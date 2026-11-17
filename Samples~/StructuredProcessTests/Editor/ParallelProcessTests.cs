using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Tools.Samples.StructuredProcessTests
{
    public sealed class ParallelProcessTests
    {
        [Test]
        public async Task StartsIndependentStepsAndUnlocksOnlyReadyConsumers()
        {
            var left = new StepControl();
            var right = new StepControl();
            var probe = new ParallelProbe();
            var running = new ForkJoinProcess(new ForkJoinProcess.Input { Left = left, Right = right, Probe = probe }, probe)
                .ExecuteAsync().AsTask();
            try
            {
                await Wait(Task.WhenAll(left.Started.Task, right.Started.Task));
                Assert.That(probe.Calls, Is.Empty);
                left.Release.SetResult(4);
                await Wait(probe.LeftConsumed.Task);
                Assert.That(right.Release.Task.IsCompleted, Is.False);
                Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "left" }));
                Assert.That(running.IsCompleted, Is.False);
                right.Release.SetResult(7);
                var result = await running;
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Output.Total, Is.EqualTo(11));
                Assert.That(result.Output.Text, Is.EqualTo("4"));
                Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "left", "sum" }));
            }
            finally
            {
                left.Unblock();
                right.Unblock();
                await Drain(running);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RequiredFailureCancelsOtherStepsAndWaitsForCleanup(bool throwException)
        {
            var left = new StepControl(true);
            var right = new StepControl { Fail = !throwException, Throw = throwException };
            var probe = new ParallelProbe();
            var running = new ForkJoinProcess(new ForkJoinProcess.Input { Left = left, Right = right, Probe = probe }, probe)
                .ExecuteAsync().AsTask();
            try
            {
                await Wait(Task.WhenAll(left.Started.Task, right.Started.Task));
                right.Release.SetResult(0);
                await Wait(left.Cancelled.Task);
                await Wait(left.Cleaning.Task);
                Assert.That(running.IsCompleted, Is.False);
                Assert.That(probe.Calls, Is.Empty);
                left.ReleaseCleanup.SetResult(true);
                var result = await running;
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.Exception != null, Is.EqualTo(throwException));
                Assert.That(left.Cleaned, Is.True);
                Assert.That(right.Cleaned, Is.True);
                Assert.That(probe.Logs.Count, Is.EqualTo(1));
                Assert.That(probe.Logs.ToArray()[0], Does.Contain("right"));
            }
            finally
            {
                left.Unblock();
                right.Unblock();
                await Drain(running);
            }
        }

        [Test]
        public async Task ExternalCancellationAwaitsEveryStartedStep()
        {
            var left = new StepControl(true);
            var right = new StepControl(true);
            var probe = new ParallelProbe();
            using var cancellation = new CancellationTokenSource();
            var running = new OptionalParallelProcess(new OptionalParallelProcess.Input { Left = left, Right = right, Probe = probe }, probe)
                .ExecuteAsync(cancellation.Token).AsTask();
            try
            {
                await Wait(Task.WhenAll(left.Started.Task, right.Started.Task));
                cancellation.Cancel();
                await Wait(Task.WhenAll(left.Cleaning.Task, right.Cleaning.Task));
                Assert.That(running.IsCompleted, Is.False);
                left.ReleaseCleanup.SetResult(true);
                Assert.That(running.IsCompleted, Is.False);
                right.ReleaseCleanup.SetResult(true);
                try
                {
                    await running;
                    Assert.Fail("Cancellation should propagate.");
                }
                catch (OperationCanceledException)
                {
                    Assert.That(left.Cleaned && right.Cleaned, Is.True);
                    Assert.That(probe.Calls, Is.Empty);
                }
            }
            finally
            {
                left.Unblock();
                right.Unblock();
                await Drain(running);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task OptionalFailureAllowsOrderingSuccessorWhileIndependentStepIsRunning(bool throwException)
        {
            var left = new StepControl { Fail = !throwException, Throw = throwException };
            var right = new StepControl();
            var probe = new ParallelProbe();
            var running = new OptionalParallelProcess(new OptionalParallelProcess.Input { Left = left, Right = right, Probe = probe }, probe)
                .ExecuteAsync().AsTask();
            try
            {
                await Wait(Task.WhenAll(left.Started.Task, right.Started.Task));
                left.Release.SetResult(0);
                await Wait(probe.AfterCompleted.Task);
                Assert.That(right.Cancelled.Task.IsCompleted, Is.False);
                Assert.That(running.IsCompleted, Is.False);
                right.Release.SetResult(0);
                Assert.That((await running).IsSuccess, Is.True);
                Assert.That(probe.Logs.Count, Is.EqualTo(1));
            }
            finally
            {
                left.Unblock();
                right.Unblock();
                await Drain(running);
            }
        }

        [Test]
        public async Task AnyStepSuppliesTypedOutputToMainStep()
        {
            var control = new StepControl();
            var probe = new ParallelProbe();
            var running = new BackgroundProcess(new BackgroundProcess.Input { Control = control, Probe = probe }, probe)
                .ExecuteAsync().AsTask();
            try
            {
                await Wait(control.Started.Task);
                control.Release.SetResult(12);
                Assert.That((await running).Output.Text, Is.EqualTo("12"));
            }
            finally
            {
                control.Unblock();
                await Drain(running);
            }
        }

        [Test]
        public async Task SynchronousRequiredFailureDoesNotStartAnotherRoot()
        {
            var left = new StepControl { Fail = true };
            var right = new StepControl();
            var probe = new ParallelProbe();
            left.Release.SetResult(0);
            var result = await new ForkJoinProcess(new ForkJoinProcess.Input { Left = left, Right = right, Probe = probe }, probe)
                .ExecuteAsync();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(right.Started.Task.IsCompleted, Is.False);
            Assert.That(probe.Calls, Is.Empty);
        }

        [Test]
        public async Task CleanupFailureIsObservedWithoutReplacingPrimaryFailure()
        {
            var left = new StepControl(true) { ThrowDuringCleanup = true };
            var right = new StepControl { Fail = true };
            var probe = new ParallelProbe();
            var running = new ForkJoinProcess(new ForkJoinProcess.Input { Left = left, Right = right, Probe = probe }, probe)
                .ExecuteAsync().AsTask();
            try
            {
                await Wait(Task.WhenAll(left.Started.Task, right.Started.Task));
                right.Release.SetResult(0);
                await Wait(left.Cleaning.Task);
                left.ReleaseCleanup.SetResult(true);
                var result = await running;
                Assert.That(result.Error, Is.EqualTo("Controlled failure."));
                Assert.That(probe.Logs.Count, Is.EqualTo(2));
                Assert.That(left.Cleaned, Is.True);
            }
            finally
            {
                left.Unblock();
                right.Unblock();
                await Drain(running);
            }
        }

        [Test]
        public async Task PreCancelledParallelProcessStartsNothing()
        {
            var left = new StepControl();
            var right = new StepControl();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var running = new ForkJoinProcess(new ForkJoinProcess.Input { Left = left, Right = right, Probe = new ParallelProbe() })
                .ExecuteAsync(cancellation.Token).AsTask();
            await Drain(running);
            Assert.That(running.IsCanceled, Is.True);
            Assert.That(left.Started.Task.IsCompleted || right.Started.Task.IsCompleted, Is.False);
        }

        internal static async Task Wait(Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(5000)) != task)
            {
                Assert.Fail("The expected signal was not received.");
            }

            await task;
        }

        internal static async Task Drain(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // All controlled operations have been released before leaving the test.
            }
        }
    }
}
