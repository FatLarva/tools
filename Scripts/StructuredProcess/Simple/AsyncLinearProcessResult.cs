using System;

namespace Tools.StructuredProcess.Simple
{
    public sealed class AsyncLinearProcessResult : IAsyncLinearProcessResult
    {
        public ProcessResultStatus Status { get; private set; }
        public int DurationMs { get; private set; }
        public Exception Exception { get; private set; }
        public IProcessStep FailedStep { get; private set; }
        public IStepsLedger StepsLedger { get; private set; }

        public AsyncLinearProcessResult(IStepsLedger stepsLedger)
        {
            StepsLedger = stepsLedger;
        }
        
        public void SetDuration(int duration)
        {
            DurationMs = duration;
        }

        public void CompleteGracefully()
        {
            Status = ProcessResultStatus.Success;
        }
        
        public void Fail(IProcessStep step, Exception exception = null)
        {
            Status = ProcessResultStatus.Failure;
            FailedStep = step;
            Exception = exception;
        }
        
        public void Cancel(IProcessStep step, Exception exception = null)
        {
            Status = ProcessResultStatus.Cancelled;
            FailedStep = step;
            Exception = exception;
        }
    }
}