using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tools.StructuredProcess.Declarative;

namespace Tools.Samples.StructuredProcess.MatchPreparation
{
    /// <summary>Simulated I/O and CPU work with a trace that validates the executed graph.</summary>
    public sealed class MatchPreparationWork
    {
        private readonly object _traceLock = new object();
        private readonly List<StepTrace> _trace = new List<StepTrace>();
        private readonly long _start = Stopwatch.GetTimestamp();
        private readonly int _mainThread = Thread.CurrentThread.ManagedThreadId;
        private int _active;
        private int _activeMain;

        public int PeakActive { get; private set; }
        public int PeakMainActive { get; private set; }
        public int DataEdges { get; private set; }
        public int ExplicitEdges { get; private set; }

        public async ValueTask<WorkProduct> RunIoAsync(string name, CancellationToken cancellationToken, params WorkProduct[] inputs)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThread)
            {
                throw new InvalidOperationException(name + " must start on the main thread.");
            }

            var trace = Begin(name, inputs);
            try
            {
                await Task.Delay(40 + inputs.Length * 10, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new WorkProduct(name, Combine(name, inputs));
            }
            finally
            {
                End(trace);
            }
        }

        public WorkProduct RunCpu(string name, CancellationToken cancellationToken, params WorkProduct[] inputs)
        {
            if (Thread.CurrentThread.ManagedThreadId == _mainThread)
            {
                throw new InvalidOperationException(name + " should run on a worker in this example.");
            }

            var trace = Begin(name, inputs);
            try
            {
                int value = Combine(name, inputs);
                for (int index = 0; index < 25000; index++)
                {
                    if ((index & 255) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    value = unchecked(value * 31 + index);
                }

                return new WorkProduct(name, value);
            }
            finally
            {
                End(trace);
            }
        }

        public StepTrace[] Snapshot()
        {
            lock (_traceLock)
            {
                return _trace.OrderBy(step => step.StartedMilliseconds).ToArray();
            }
        }

        public void Validate(StepDescription[] declarations)
        {
            var trace = Snapshot();
            if (trace.Length != declarations.Length)
            {
                throw new InvalidOperationException("Not every declared step executed exactly once.");
            }

            var byName = trace.ToDictionary(step => step.Name, StringComparer.Ordinal);
            DataEdges = 0;
            ExplicitEdges = 0;
            foreach (var declaration in declarations)
            {
                string name = declaration.Id.Name ?? declaration.StepType.Name;
                var step = byName[name];
                bool main = declaration.Thread == StepThread.Main;
                if ((step.StartThread == _mainThread) != main || (step.EndThread == _mainThread) != main)
                {
                    throw new InvalidOperationException(name + " violated its declared thread requirement.");
                }

                foreach (string parent in step.DataDependencies)
                {
                    CheckDependency(step, byName[parent]);
                    DataEdges++;
                }

                foreach (var dependency in declaration.After)
                {
                    CheckDependency(step, byName[dependency.Name ?? dependency.Type.Name]);
                    ExplicitEdges++;
                }
            }
        }

        private StepTrace Begin(string name, WorkProduct[] inputs)
        {
            var trace = new StepTrace
            {
                Name = name,
                StartThread = Thread.CurrentThread.ManagedThreadId,
                StartedMilliseconds = Milliseconds(),
                DataDependencies = inputs.Select(input => input.Origin).Distinct().ToArray()
            };
            lock (_traceLock)
            {
                _trace.Add(trace);
                _active++;
                if (trace.StartThread == _mainThread)
                {
                    _activeMain++;
                }

                PeakActive = Math.Max(PeakActive, _active);
                PeakMainActive = Math.Max(PeakMainActive, _activeMain);
            }

            return trace;
        }

        private void End(StepTrace trace)
        {
            lock (_traceLock)
            {
                trace.EndThread = Thread.CurrentThread.ManagedThreadId;
                trace.CompletedMilliseconds = Milliseconds();
                _active--;
                if (trace.StartThread == _mainThread)
                {
                    _activeMain--;
                }
            }
        }

        private double Milliseconds()
        {
            return (Stopwatch.GetTimestamp() - _start) * 1000.0 / Stopwatch.Frequency;
        }

        private static int Combine(string name, WorkProduct[] inputs)
        {
            int value = 17;
            foreach (char character in name)
            {
                value = unchecked(value * 31 + character);
            }

            foreach (var input in inputs)
            {
                value = unchecked(value * 31 + input.Fingerprint);
            }

            return value;
        }

        private static void CheckDependency(StepTrace step, StepTrace parent)
        {
            if (parent.CompletedMilliseconds > step.StartedMilliseconds)
            {
                throw new InvalidOperationException(step.Name + " started before " + parent.Name + " completed.");
            }
        }

        public sealed class StepTrace
        {
            public string Name;
            public string[] DataDependencies;
            public int StartThread;
            public int EndThread;
            public double StartedMilliseconds;
            public double CompletedMilliseconds;
        }
    }
}
