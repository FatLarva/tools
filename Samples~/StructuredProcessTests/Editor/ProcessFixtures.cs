using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Logging;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcessTests
{
    public sealed class Probe : ILog
    {
        public readonly List<string> Calls = new List<string>();
        public readonly List<int> Threads = new List<int>();
        public readonly List<string> Logs = new List<string>();
        public readonly TaskCompletionSource<bool> Cleaning = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> ReleaseCleanup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Throw;
        public bool ThrowInConstructor;
        public bool ThrowUnrelatedCancellation;
        public bool Cleaned;
        public CancellationTokenSource CancelAfterSuccess;

        public void Record(string call)
        {
            Calls.Add(call);
            Threads.Add(Thread.CurrentThread.ManagedThreadId);
        }

        public void Info(string message)
        {
            Logs.Add(message);
        }

        public void Warning(string message)
        {
            Logs.Add(message);
        }

        public void Error(string message)
        {
            Logs.Add(message);
        }

        public void Exception(string message, Exception exception)
        {
            Logs.Add(message);
        }
    }

    public sealed class SourceStep : IProcessStep<SourceStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public Probe Probe;
            [Data("seed")] public string Seed;
        }

        public struct Output : IProcessOutput
        {
            [Data("value")] public string Value;
        }

        public SourceStep(Input input)
        {
            _input = input;
            if (input.Probe.ThrowInConstructor)
            {
                throw new InvalidOperationException("Constructor failure.");
            }
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Probe.Record("source-enter:" + _input.Seed);
            await Task.Yield();
            _input.Probe.Record("source-exit:" + _input.Seed);
            _input.Probe.CancelAfterSuccess?.Cancel();
            return StepResult<Output>.Success(new Output { Value = _input.Seed + "!" });
        }
    }

    public sealed class ConsumeStep : IProcessStep<ConsumeStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public Probe Probe;
            [Data("value")] public string Value;
        }

        public struct Output : IProcessOutput
        {
            [Data("text")] public string Text;
        }

        public ConsumeStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Probe.Record("consume");
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(new Output { Text = _input.Value + "?" }));
        }
    }

    public sealed class FinishStep : IProcessStep<FinishStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public Probe Probe;
        }

        public struct Output : IProcessOutput
        {
        }

        public FinishStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Probe.Record("finish");
            return new ValueTask<StepResult<Output>>(StepResult<Output>.Success(default));
        }
    }

    public sealed class FailStep : IProcessStep<FailStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public Probe Probe;
        }

        public struct Output : IProcessOutput
        {
        }

        public FailStep(Input input)
        {
            _input = input;
        }

        public ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _input.Probe.Record("fail");
            if (_input.Probe.Throw)
            {
                throw new InvalidOperationException("Unexpected failure.");
            }

            if (_input.Probe.ThrowUnrelatedCancellation)
            {
                throw new OperationCanceledException("An unrelated operation was cancelled.");
            }

            return new ValueTask<StepResult<Output>>(StepResult<Output>.Failure("Expected failure."));
        }
    }

    public sealed class WaitStep : IProcessStep<WaitStep.Output>
    {
        private readonly Input _input;

        public struct Input : IProcessInput
        {
            public Probe Probe;
        }

        public struct Output : IProcessOutput
        {
        }

        public WaitStep(Input input)
        {
            _input = input;
        }

        public async ValueTask<StepResult<Output>> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                _input.Probe.Record("wait");
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return StepResult<Output>.Success(default);
            }
            finally
            {
                _input.Probe.Cleaning.SetResult(true);
                await _input.Probe.ReleaseCleanup.Task;
                _input.Probe.Cleaned = true;
            }
        }
    }

    public partial class OrderedProcess : LinearProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(FinishStep)) { After = { typeof(ConsumeStep) } },
            new StepDescription(typeof(ConsumeStep)),
            new StepDescription(typeof(SourceStep))
        };

        public struct Output : IProcessOutput
        {
            [Data("text")] public string Text;
        }
    }

    public partial class OptionalProcess : LinearProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(FinishStep)) { After = { "optional" } },
            new StepDescription(typeof(FailStep)) { Id = "optional", Optional = true, Thread = StepThread.Any }
        };

        public struct Output : IProcessOutput
        {
        }
    }

    public partial class RequiredProcess : LinearProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(FinishStep)) { After = { typeof(FailStep) } },
            new StepDescription(typeof(FailStep))
        };

        public struct Output : IProcessOutput
        {
        }
    }

    public partial class CancellationProcess : LinearProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(FinishStep)) { After = { typeof(WaitStep) } },
            new StepDescription(typeof(WaitStep)) { Optional = true }
        };

        public struct Output : IProcessOutput
        {
        }
    }

    public partial class AliasesProcess : LinearProcess
    {
        public static readonly StepDescription[] Steps =
        {
            new StepDescription(typeof(SourceStep))
            {
                Id = "first",
                Inputs = { ["seed"] = "first-seed" },
                Outputs = { ["value"] = "first-value" }
            },
            new StepDescription(typeof(SourceStep))
            {
                Id = "second",
                Inputs = { ["seed"] = "second-seed" },
                Outputs = { ["value"] = "second-value" },
                After = { "first" }
            }
        };

        public struct Output : IProcessOutput
        {
            [Data("first-value")] public string First;
            [Data("second-value")] public string Second;
        }
    }
}
