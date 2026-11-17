using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcessTests
{
    public sealed class ProcessTests
    {
        [Test]
        public async Task ConnectsTypedResultsAndKeepsMainThreadAcrossAwait()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var probe = new Probe();
            var process = new OrderedProcess(new OrderedProcess.Input { Seed = "hello", Probe = probe }, probe);

            var result = await process.ExecuteAsync();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Output.Text, Is.EqualTo("hello!?"));
            Assert.That(probe.Calls, Is.EqualTo(new[] { "source-enter:hello", "source-exit:hello", "consume", "finish" }));
            Assert.That(probe.Threads, Is.All.EqualTo(mainThread));
        }

        [Test]
        public async Task RepeatedTypesHaveIndependentIdsAndAliases()
        {
            var probe = new Probe();
            var process = new AliasesProcess(new AliasesProcess.Input { FirstSeed = "a", SecondSeed = "b", Probe = probe }, probe);

            var result = await process.ExecuteAsync();

            Assert.That(result.Output.First, Is.EqualTo("a!"));
            Assert.That(result.Output.Second, Is.EqualTo("b!"));
            Assert.That(probe.Calls, Is.EqualTo(new[] { "source-enter:a", "source-exit:a", "source-enter:b", "source-exit:b" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task OptionalFailureLogsAndSatisfiesOrderingDependency(bool throwException)
        {
            var probe = new Probe { Throw = throwException };
            var process = new OptionalProcess(new OptionalProcess.Input { Probe = probe }, probe);

            var result = await process.ExecuteAsync();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(probe.Calls, Is.EqualTo(new[] { "fail", "finish" }));
            Assert.That(probe.Logs.Count, Is.EqualTo(1));
            Assert.That(probe.Logs[0], Does.Contain("optional"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RequiredFailurePreventsSuccessor(bool throwException)
        {
            var probe = new Probe { Throw = throwException };
            var process = new RequiredProcess(new RequiredProcess.Input { Probe = probe }, probe);

            var result = await process.ExecuteAsync();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(probe.Calls, Is.EqualTo(new[] { "fail" }));
            Assert.That(probe.Logs.Count, Is.EqualTo(1));
            Assert.That(result.Exception != null, Is.EqualTo(throwException));
        }

        [Test]
        public async Task ConstructorFailureIsAttributedToItsStep()
        {
            var probe = new Probe { ThrowInConstructor = true };
            var process = new OrderedProcess(new OrderedProcess.Input { Probe = probe }, probe);

            var result = await process.ExecuteAsync();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(probe.Calls, Is.Empty);
            Assert.That(probe.Logs[0], Does.Contain(nameof(SourceStep)));
        }

        [Test]
        public async Task PreCancelledProcessDoesNotStartSteps()
        {
            var probe = new Probe();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var process = new OrderedProcess(new OrderedProcess.Input { Probe = probe }, probe);

            await AssertCancelled(process.ExecuteAsync(cancellation.Token).AsTask());

            Assert.That(probe.Calls, Is.Empty);
        }

        [Test]
        public async Task CancellationWinsOverSuccessfulStepReturn()
        {
            using var cancellation = new CancellationTokenSource();
            var probe = new Probe { CancelAfterSuccess = cancellation };
            var process = new OrderedProcess(new OrderedProcess.Input { Seed = "x", Probe = probe }, probe);

            await AssertCancelled(process.ExecuteAsync(cancellation.Token).AsTask());

            Assert.That(probe.Calls, Is.EqualTo(new[] { "source-enter:x", "source-exit:x" }));
        }

        [Test]
        public async Task CancellationWaitsForCleanupEvenForOptionalStep()
        {
            var probe = new Probe();
            using var cancellation = new CancellationTokenSource();
            var process = new CancellationProcess(new CancellationProcess.Input { Probe = probe }, probe);
            var running = process.ExecuteAsync(cancellation.Token).AsTask();
            cancellation.Cancel();

            try
            {
                await probe.Cleaning.Task;
                Assert.That(running.IsCompleted, Is.False);
            }
            finally
            {
                probe.ReleaseCleanup.TrySetResult(true);
            }

            await AssertCancelled(running);
            Assert.That(probe.Cleaned, Is.True);
            Assert.That(probe.Calls, Is.EqualTo(new[] { "wait" }));
            Assert.That(probe.Logs, Is.Empty);
        }

        [Test]
        public async Task UnrelatedCancellationIsAStepFailure()
        {
            var probe = new Probe { ThrowUnrelatedCancellation = true };
            var result = await new RequiredProcess(new RequiredProcess.Input { Probe = probe }, probe).ExecuteAsync();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Exception, Is.TypeOf<OperationCanceledException>());
        }

        [Test]
        public void FailedResultDoesNotExposeAnOutput()
        {
            var result = StepResult<FinishStep.Output>.Failure("Unavailable.");
            Assert.Throws<InvalidOperationException>(() =>
            {
                var output = result.Output;
            });
        }

        private static async Task AssertCancelled(Task task)
        {
            try
            {
                await task;
                Assert.Fail("The process should propagate cancellation.");
            }
            catch (OperationCanceledException)
            {
                // Expected: cancellation is not an ordinary optional-step failure.
            }
        }
    }
}
