using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcessTests
{
    public sealed class StepControl
    {
        public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<int> Release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Cleaning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> ReleaseCleanup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Fail;
        public bool Throw;
        public bool ThrowDuringCleanup;
        public bool Cleaned;
        public int ConstructorThread;
        public int EnterThread;
        public int ExitThread;

        public StepControl(bool holdCleanup = false)
        {
            if (!holdCleanup)
            {
                ReleaseCleanup.SetResult(true);
            }
        }

        public void Unblock()
        {
            Release.TrySetResult(0);
            ReleaseCleanup.TrySetResult(true);
        }
    }

    public sealed class ParallelProbe : ILog
    {
        public readonly ConcurrentQueue<string> Calls = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<string> Logs = new ConcurrentQueue<string>();
        public readonly TaskCompletionSource<bool> LeftConsumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> AfterCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ConsumerThread;

        public void Info(string message)
        {
            Logs.Enqueue(message);
        }

        public void Warning(string message)
        {
            Logs.Enqueue(message);
        }

        public void Error(string message)
        {
            Logs.Enqueue(message);
        }

        public void Exception(string message, Exception exception)
        {
            Logs.Enqueue(message);
        }
    }

    public sealed class ControlledStep : IProcessStep<ControlledStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            [Data("control")] public StepControl Control;
        }

        public struct Output : IProcessOutput
        {
            [Data("value")] public int Value;
        }

        public ControlledStep(Input input)
        {
            _input = input;
            input.Control.ConstructorThread = Thread.CurrentThread.ManagedThreadId;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var control = _input.Control;
            control.EnterThread = Thread.CurrentThread.ManagedThreadId;
            control.Started.SetResult(true);
            using var registration = cancellationToken.Register(() =>
            {
                control.Cancelled.TrySetResult(true);
                control.Release.TrySetCanceled(cancellationToken);
            });

            try
            {
                int value = await control.Release.Task;
                if (control.Throw)
                {
                    throw new InvalidOperationException("Controlled exception.");
                }

                if (control.Fail)
                {
                    return StepResult<Output>.Failure("Controlled failure.");
                }

                return StepResult<Output>.Success(new Output { Value = value });
            }
            finally
            {
                control.Cleaning.TrySetResult(true);
                await control.ReleaseCleanup.Task;
                control.ExitThread = Thread.CurrentThread.ManagedThreadId;
                control.Cleaned = true;
                if (control.ThrowDuringCleanup)
                {
                    throw new InvalidOperationException("Cleanup failure.");
                }
            }
        }
    }

    public sealed class LeftConsumerStep : IProcessStep<LeftConsumerStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            [Data("left-value")] public int Value;
            public ParallelProbe Probe;
        }

        public struct Output : IProcessOutput
        {
            public string Text;
        }

        public LeftConsumerStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Probe.ConsumerThread = Thread.CurrentThread.ManagedThreadId;
            _input.Probe.Calls.Enqueue("left");
            _input.Probe.LeftConsumed.SetResult(true);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Text = _input.Value.ToString() }));
        }
    }

    public sealed class SumStep : IProcessStep<SumStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            [Data("left-value")] public int Left;
            [Data("right-value")] public int Right;
            public ParallelProbe Probe;
        }

        public struct Output : IProcessOutput
        {
            [Data("total")] public int Total;
        }

        public SumStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Probe.Calls.Enqueue("sum");
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Total = _input.Left + _input.Right }));
        }
    }

    public sealed class AfterStep : IProcessStep<AfterStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public ParallelProbe Probe;
        }

        public struct Output : IProcessOutput
        {
        }

        public AfterStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Probe.Calls.Enqueue("after");
            _input.Probe.AfterCompleted.TrySetResult(true);
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(default));
        }
    }

    public partial class ForkJoinProcess : ParallelProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(SumStep)),
            new StepDescription(typeof(LeftConsumerStep)),
            new StepDescription(typeof(ControlledStep))
            {
                Id = "left",
                Inputs = { ["control"] = "left" },
                Outputs = { ["value"] = "left-value" }
            },
            new StepDescription(typeof(ControlledStep))
            {
                Id = "right",
                Inputs = { ["control"] = "right" },
                Outputs = { ["value"] = "right-value" }
            }
        };

        public struct Output : IProcessOutput
        {
            [Data("total")] public int Total;
            public string Text;
        }
    }

    public partial class OptionalParallelProcess : ParallelProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(AfterStep)) { After = { "left" } },
            new StepDescription(typeof(ControlledStep))
            {
                Id = "left",
                Optional = true,
                Inputs = { ["control"] = "left" },
                Outputs = { ["value"] = "left-value" }
            },
            new StepDescription(typeof(ControlledStep))
            {
                Id = "right",
                Inputs = { ["control"] = "right" },
                Outputs = { ["value"] = "right-value" }
            }
        };

        public struct Output : IProcessOutput
        {
        }
    }

    public partial class BackgroundProcess : ParallelProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(ControlledStep))
            {
                Thread = StepThread.Any,
                Outputs = { ["value"] = "left-value" }
            },
            new StepDescription(typeof(LeftConsumerStep))
        };

        public struct Output : IProcessOutput
        {
            public string Text;
        }
    }
}
