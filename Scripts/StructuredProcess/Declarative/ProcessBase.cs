using System;
using Logging;

namespace Tools.StructuredProcess.Declarative
{
    public abstract class ProcessBase
    {
        private readonly ILog _logger;

        protected ProcessBase(ILog logger)
        {
            _logger = logger ?? Log.Default;
        }

        protected void ReportStepFailure(StepId step, string error, Exception exception)
        {
            string message = $"{GetType().Name}: step '{step}' failed: {error}";
            if (exception == null)
            {
                _logger.Warning(message);
            }
            else
            {
                _logger.Exception(message, exception);
            }
        }

        protected TraceRecorder BeginTrace(ProcessTrace trace, ProcessTrace.Step[] steps)
        {
            trace.Begin(GetType(), steps);
            return new TraceRecorder(trace);
        }

        protected sealed class TraceRecorder
        {
            private readonly ProcessTrace _trace;

            internal TraceRecorder(ProcessTrace trace)
            {
                _trace = trace;
            }

            public void StepReady(int step)
            {
                _trace.Record(step, ProcessTraceStepStatus.Ready);
            }

            public void StepStarted(int step)
            {
                _trace.Record(step, ProcessTraceStepStatus.Running);
            }

            public void StepCanceled(int step, Exception exception = null)
            {
                _trace.Record(step, ProcessTraceStepStatus.Canceled, exception?.Message, exception);
            }

            public void StepFinished<TOutput>(int step, StepResult<TOutput> result) where TOutput : struct, IProcessOutput
            {
                _trace.Record(step, result.IsSuccess ? ProcessTraceStepStatus.Succeeded : ProcessTraceStepStatus.Failed,
                    result.Error, result.Exception);
            }

            public void Finish<TOutput>(StepResult<TOutput> result) where TOutput : struct, IProcessOutput
            {
                _trace.End(result.IsSuccess ? ProcessTraceStatus.Succeeded : ProcessTraceStatus.Failed,
                    result.Error, result.Exception);
            }

            public void Finish(Exception exception, bool canceled)
            {
                _trace.End(canceled ? ProcessTraceStatus.Canceled : ProcessTraceStatus.Failed, exception.Message, exception);
            }
        }
    }
}
