using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Tools.StructuredProcess.Declarative
{
    public enum ProcessTraceStatus
    {
        NotStarted,
        Running,
        Succeeded,
        Failed,
        Canceled
    }

    public enum ProcessTraceStepStatus
    {
        Waiting,
        Ready,
        Running,
        Succeeded,
        Failed,
        Canceled,
        Skipped
    }

    /// <summary>Opt-in recording for one execution of a generated process.</summary>
    public sealed class ProcessTrace
    {
        private readonly object _gate = new object();
        private readonly List<Entry> _events = new List<Entry>();
        private readonly Stopwatch _clock = new Stopwatch();
        private IReadOnlyList<Step> _steps = Array.Empty<Step>();
        private Entry[] _latest = Array.Empty<Entry>();
        private Type _processType;
        private ProcessTraceStatus _status;
        private string _error;
        private Exception _exception;

        /// <summary>Returns a consistent view of the run, including while steps are executing.</summary>
        public Snapshot GetSnapshot()
        {
            lock (_gate)
            {
                return new Snapshot(_processType, _status, _clock.Elapsed, _error, _exception,
                    _steps, Array.AsReadOnly(_events.ToArray()));
            }
        }

        internal void Begin(Type processType, Step[] steps)
        {
            lock (_gate)
            {
                if (_status != ProcessTraceStatus.NotStarted)
                {
                    throw new InvalidOperationException("Use a new ProcessTrace for each execution.");
                }

                _processType = processType;
                _status = ProcessTraceStatus.Running;
                _steps = Array.AsReadOnly(steps);
                _latest = new Entry[steps.Length];
                _clock.Start();
                for (int index = 0; index < steps.Length; index++)
                {
                    var status = steps[index].Dependencies.Count == 0
                        ? ProcessTraceStepStatus.Ready
                        : ProcessTraceStepStatus.Waiting;
                    Append(index, status, TimeSpan.Zero);
                }
            }
        }

        internal void Record(int step, ProcessTraceStepStatus status, string error = null, Exception exception = null)
        {
            lock (_gate)
            {
                Entry previous = _latest[step];
                if (previous.Status == status)
                {
                    return;
                }

                TimeSpan now = _clock.Elapsed;
                TimeSpan? duration = previous.Status == ProcessTraceStepStatus.Running
                    ? now - previous.Elapsed
                    : null;
                Append(step, status, now, duration, error, exception);
            }
        }

        internal void End(ProcessTraceStatus status, string error, Exception exception)
        {
            lock (_gate)
            {
                _clock.Stop();
                _status = status;
                _error = error;
                _exception = exception;
                TimeSpan now = _clock.Elapsed;
                string stopReason = status == ProcessTraceStatus.Canceled ? "The process was canceled."
                    : status == ProcessTraceStatus.Failed ? $"The process failed: {error}" : null;
                for (int index = 0; index < _latest.Length; index++)
                {
                    Entry previous = _latest[index];
                    switch (previous.Status)
                    {
                        case ProcessTraceStepStatus.Waiting:
                        case ProcessTraceStepStatus.Ready:
                            Append(index, ProcessTraceStepStatus.Skipped, now, error: stopReason);
                            break;
                        case ProcessTraceStepStatus.Running:
                            Append(index, ProcessTraceStepStatus.Canceled, now, now - previous.Elapsed, stopReason);
                            break;
                    }
                }
            }
        }

        private void Append(int step, ProcessTraceStepStatus status, TimeSpan elapsed, TimeSpan? duration = null,
            string error = null, Exception exception = null)
        {
            var entry = new Entry(step, status, elapsed, duration, error, exception);
            _latest[step] = entry;
            _events.Add(entry);
        }

        public readonly struct Step
        {
            /// <summary>The declared occurrence ID, or the fully qualified type name when no ID was declared.</summary>
            public string Id { get; }
            public Type Type { get; }
            public bool Optional { get; }
            public StepThread Thread { get; }
            /// <summary>Indexes into the snapshot's step list, including data and explicit dependencies.</summary>
            public IReadOnlyList<int> Dependencies { get; }

            public Step(string id, Type type, bool optional, StepThread thread, params int[] dependencies)
            {
                Id = id;
                Type = type;
                Optional = optional;
                Thread = thread;
                Dependencies = Array.AsReadOnly((int[])dependencies.Clone());
            }
        }

        public readonly struct Entry
        {
            public int StepIndex { get; }
            public ProcessTraceStepStatus Status { get; }
            public TimeSpan Elapsed { get; }
            public TimeSpan? Duration { get; }
            public string Error { get; }
            public Exception Exception { get; }

            internal Entry(int stepIndex, ProcessTraceStepStatus status, TimeSpan elapsed, TimeSpan? duration,
                string error, Exception exception)
            {
                StepIndex = stepIndex;
                Status = status;
                Elapsed = elapsed;
                Duration = duration;
                Error = error;
                Exception = exception;
            }
        }

        public readonly struct Snapshot
        {
            public Type ProcessType { get; }
            public ProcessTraceStatus Status { get; }
            public TimeSpan Elapsed { get; }
            public string Error { get; }
            public Exception Exception { get; }
            public IReadOnlyList<Step> Steps { get; }
            public IReadOnlyList<Entry> Events { get; }

            internal Snapshot(Type processType, ProcessTraceStatus status, TimeSpan elapsed, string error,
                Exception exception, IReadOnlyList<Step> steps, IReadOnlyList<Entry> events)
            {
                ProcessType = processType;
                Status = status;
                Elapsed = elapsed;
                Error = error;
                Exception = exception;
                Steps = steps;
                Events = events;
            }
        }
    }
}
