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
            var process = new OptionalBindingProcess(new OptionalBindingProcess.Input
            {
                Probe = probe,
                GetterFailure = getterThrows
            }, probe);

            Assert.That((await process.ExecuteAsync()).IsSuccess, Is.True);
            Assert.That(probe.Calls.ToArray(), Is.EqualTo(new[] { "after" }));
            Assert.That(probe.Logs.Single(), Does.Contain(error));
        }

        [Test]
        public async Task RequiredBindingFailureReturnsTypedFailure()
        {
            var probe = new ParallelProbe();
            var process = new RequiredBindingProcess(new RequiredBindingProcess.Input { Probe = probe }, probe);

            var result = await process.ExecuteAsync();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error, Is.EqualTo("Input setter failed."));
            Assert.That(probe.Calls, Is.Empty);
        }

        [Test]
        public async Task EarlierFailureDuringAnotherLaunchPreventsFurtherLaunches()
        {
            var probe = new ParallelProbe();
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
                pending = process.ExecuteAsync();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(priorContext);
            }

            Assert.That((await pending).Error, Is.EqualTo("Earlier step failed."));
            Assert.That(probe.Calls, Is.Empty);
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
}
