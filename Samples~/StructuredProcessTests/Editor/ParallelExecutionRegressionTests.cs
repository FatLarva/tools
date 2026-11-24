using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Tools.StructuredProcess.Declarative;
using Data = Tools.StructuredProcess.Declarative.DataAttribute;

namespace Tools.Samples.StructuredProcessTests
{
    public sealed class ParallelExecutionRegressionTests
    {
        [TestCase(false, "Input setter failed.")]
        [TestCase(true, "Output getter failed.")]
        public async Task OptionalBindingFailureIsLoggedAndAllowsSuccessor(bool getterThrows, string error)
        {
            var probe = new ParallelProbe();
            var trace = new ProcessTrace();
            var process = new OptionalBindingProcess(new OptionalBindingProcess.Input
            {
                Probe = probe,
                GetterFailure = getterThrows
            }, probe);

            Assert.That((await process.ExecuteAsync(trace, default)).IsSuccess, Is.True);
            Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "after" }));
            Assert.That(probe.Logs.Single(), Does.Contain(error));

            var snapshot = trace.GetSnapshot();
            int binding = FindStep(snapshot, "bind");
            int successor = FindStep(snapshot, typeof(AfterStep).FullName);
            var events = snapshot.Events.Where(item => item.StepIndex == binding).ToArray();
            Assert.That(snapshot.Status, Is.EqualTo(ProcessTraceStatus.Succeeded));
            Assert.That(snapshot.Steps[binding].Optional, Is.True);
            Assert.That(snapshot.Steps[successor].Dependencies, Is.EqualTo(new[] { binding }));
            Assert.That(events.Select(item => item.Status), Is.EqualTo(new[]
            {
                ProcessTraceStepStatus.Waiting, ProcessTraceStepStatus.Ready,
                ProcessTraceStepStatus.Running, ProcessTraceStepStatus.Failed
            }));
            Assert.That(events[3].Duration, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
            Assert.That(events[3].Error, Is.EqualTo(error));
            Assert.That(events[3].Exception, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task RequiredBindingFailureReturnsTypedFailure()
        {
            var probe = new ParallelProbe();
            var trace = new ProcessTrace();
            var process = new RequiredBindingProcess(new RequiredBindingProcess.Input { Probe = probe }, probe);

            var result = await process.ExecuteAsync(trace, default);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("Input setter failed."));
            Assert.That(probe.Calls, Is.Empty);
            var snapshot = trace.GetSnapshot();
            Assert.That(snapshot.Status, Is.EqualTo(ProcessTraceStatus.Failed));
            Assert.That(snapshot.Error, Is.EqualTo(result.Error));
            var skipped = snapshot.Events.Last(item => item.StepIndex == FindStep(snapshot, typeof(AfterStep).FullName));
            Assert.That(skipped.Status, Is.EqualTo(ProcessTraceStepStatus.Skipped));
            Assert.That(skipped.Error, Does.Contain(result.Error));
        }

        [Test]
        public async Task EarlierFailureDuringAnotherLaunchPreventsFurtherLaunches()
        {
            var probe = new ParallelProbe();
            var trace = new ProcessTrace();
            var process = new FailBetweenLaunchesProcess(new FailBetweenLaunchesProcess.Input
            {
                Probe = probe,
                Gate = new InlineFailureGate()
            }, probe);

            // The inline gate must complete the first step on the same call stack in any test host.
            var priorContext = SynchronizationContext.Current;
            ValueTask<StepResult<FailBetweenLaunchesProcess.Output>> pending;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                pending = process.ExecuteAsync(trace, default);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(priorContext);
            }

            Assert.That((await pending).Error, Is.EqualTo("Earlier step failed."));
            Assert.That(probe.Calls, Is.Empty);
            var snapshot = trace.GetSnapshot();
            var second = snapshot.Events.Last(item => item.StepIndex == FindStep(snapshot, "01-release"));
            var third = snapshot.Events.Last(item => item.StepIndex == FindStep(snapshot, "02-later"));
            Assert.That(second.Status, Is.EqualTo(ProcessTraceStepStatus.Canceled));
            Assert.That(third.Status, Is.EqualTo(ProcessTraceStepStatus.Skipped));
        }

        [Test]
        public async Task LinearTraceRecordsReadinessAndTypedSuccess()
        {
            var probe = new ParallelProbe();
            var trace = new ProcessTrace();
            var process = new LinearTraceProcess(new LinearTraceProcess.Input { Probe = probe }, probe);

            Assert.That((await process.ExecuteAsync(default)).IsSuccess, Is.True);
            Assert.That((await process.ExecuteAsync(trace, default)).IsSuccess, Is.True);
            var snapshot = trace.GetSnapshot();
            var events = snapshot.Events.Where(item => item.StepIndex == FindStep(snapshot, "second")).ToArray();
            Assert.That(snapshot.Status, Is.EqualTo(ProcessTraceStatus.Succeeded));
            Assert.That(snapshot.ProcessType, Is.EqualTo(typeof(LinearTraceProcess)));
            Assert.That(events.Select(item => item.Status), Is.EqualTo(new[]
            {
                ProcessTraceStepStatus.Waiting, ProcessTraceStepStatus.Ready,
                ProcessTraceStepStatus.Running, ProcessTraceStepStatus.Succeeded
            }));
            Assert.That(events.Select(item => item.Elapsed), Is.Ordered);
        }

        [Test]
        public void PreCanceledTraceContainsSkippedStepsAndCanceledOutcome()
        {
            var probe = new ParallelProbe();
            var trace = new ProcessTrace();
            var process = new LinearTraceProcess(new LinearTraceProcess.Input { Probe = probe }, probe);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.CatchAsync<OperationCanceledException>(async () => { await process.ExecuteAsync(trace, cancellation.Token); });
            var snapshot = trace.GetSnapshot();
            Assert.That(snapshot.Status, Is.EqualTo(ProcessTraceStatus.Canceled));
            Assert.That(snapshot.Events.Count(item => item.Status == ProcessTraceStepStatus.Skipped), Is.EqualTo(2));
            Assert.That(snapshot.Events.Any(item => item.Status == ProcessTraceStepStatus.Running), Is.False);
            Assert.That(snapshot.Events.Last().Error, Does.Contain("canceled"));
        }

        [Test]
        public async Task RunningSnapshotIsStableWhileBackgroundStepCompletes()
        {
            var control = new StepControl();
            var probe = new ParallelProbe();
            var trace = new ProcessTrace();
            var process = new BackgroundProcess(new BackgroundProcess.Input { Control = control, Probe = probe }, probe);
            var running = process.ExecuteAsync(trace, default).AsTask();
            try
            {
                await ParallelProcessTests.Wait(control.Started.Task);
                var before = trace.GetSnapshot();
                Assert.That(before.Status, Is.EqualTo(ProcessTraceStatus.Running));
                int background = FindStep(before, typeof(ControlledStep).FullName);
                Assert.That(before.Steps[background].Thread, Is.EqualTo(StepThread.Any));
                Assert.That(before.Events.Last(item => item.StepIndex == background).Status,
                    Is.EqualTo(ProcessTraceStepStatus.Running));
                control.Release.SetResult(9);
                Assert.That((await running).Output.Text, Is.EqualTo("9"));
                var after = trace.GetSnapshot();
                Assert.That(after.Status, Is.EqualTo(ProcessTraceStatus.Succeeded));
                Assert.That(after.Events.Count, Is.GreaterThan(before.Events.Count));
            }
            finally
            {
                control.Unblock();
                await ParallelProcessTests.Drain(running);
            }
        }

        private static int FindStep(ProcessTrace.Snapshot snapshot, string id)
        {
            for (int index = 0; index < snapshot.Steps.Count; index++)
            {
                if (snapshot.Steps[index].Id == id)
                {
                    return index;
                }
            }

            throw new InvalidOperationException($"Trace step '{id}' was absent.");
        }
    }

    public sealed class OutputAccessorStep : IProcessStep<OutputAccessorStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            [Data("getter-failure")] public bool GetterFailure;
        }

        public struct Output : IProcessOutput
        {
            private readonly bool _getterFailure;

            [Data("accessor-data")]
            public bool Value
            {
                get
                {
                    if (_getterFailure)
                    {
                        throw new InvalidOperationException("Output getter failed.");
                    }

                    return true;
                }
            }

            public Output(bool getterFailure)
            {
                _getterFailure = getterFailure;
            }
        }

        public OutputAccessorStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output(_input.GetterFailure)));
        }
    }

    public sealed class InputAccessorStep : IProcessStep<InputAccessorStep.Output>
    {
        public struct Input : IProcessInput
        {
            [Data("accessor-data")]
            public bool Value
            {
                set
                {
                    if (value)
                    {
                        throw new InvalidOperationException("Input setter failed.");
                    }
                }
            }
        }

        public struct Output : IProcessOutput
        {
        }

        public InputAccessorStep(Input input)
        {
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(default));
        }
    }

    public partial class OptionalBindingProcess : ParallelProcess
    {
        private static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(OutputAccessorStep)),
            new StepDescription(typeof(InputAccessorStep)) { Id = "bind", Optional = true, Thread = StepThread.Any },
            new StepDescription(typeof(AfterStep)) { After = { "bind" } }
        };

        public struct Output : IProcessOutput
        {
        }
    }

    public partial class RequiredBindingProcess : ParallelProcess
    {
        private static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(OutputAccessorStep)),
            new StepDescription(typeof(InputAccessorStep)),
            new StepDescription(typeof(AfterStep)) { After = { typeof(InputAccessorStep) } }
        };

        public struct Output : IProcessOutput
        {
        }
    }

    public sealed class InlineFailureGate
    {
        public readonly TaskCompletionSource<bool> Release = new TaskCompletionSource<bool>();
    }

    public sealed class InlineFailureStep : IProcessStep<InlineFailureStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public InlineFailureGate Gate;
        }

        public struct Output : IProcessOutput
        {
        }

        public InlineFailureStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            await _input.Gate.Release.Task;
            return StepResult<Output>.Failure("Earlier step failed.");
        }
    }

    public sealed class ReleaseFailureStep : IProcessStep<ReleaseFailureStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public InlineFailureGate Gate;
        }

        public struct Output : IProcessOutput
        {
        }

        public ReleaseFailureStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Gate.Release.SetResult(true);
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return StepResult<Output>.Success(default);
        }
    }

    public partial class FailBetweenLaunchesProcess : ParallelProcess
    {
        private static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(InlineFailureStep)) { Id = "00-fail" },
            new StepDescription(typeof(ReleaseFailureStep)) { Id = "01-release" },
            new StepDescription(typeof(AfterStep)) { Id = "02-later" }
        };

        public struct Output : IProcessOutput
        {
        }
    }

    public partial class LinearTraceProcess : LinearProcess
    {
        private static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(AfterStep)) { Id = "first" },
            new StepDescription(typeof(AfterStep)) { Id = "second", After = { "first" } }
        };

        public struct Output : IProcessOutput
        {
        }
    }
}
