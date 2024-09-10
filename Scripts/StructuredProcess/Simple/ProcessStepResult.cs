using System;

namespace Tools.StructuredProcess.Simple
{
    public readonly struct ProcessStepResult<T> : IStepResult
    {
        public readonly T Result;

        public int DurationMilliseconds { get; }
        public Exception Exception { get; }
        public ProcessStepResultStatus Status { get; }

        public ProcessStepResult(ProcessStepResultStatus status, int durationMilliseconds, T result)
        {
            Status = status;
            Exception = default;
            DurationMilliseconds = durationMilliseconds;
            Result = result;
        }
        
        public ProcessStepResult(ProcessStepResultStatus status, int durationMilliseconds, Exception exception)
        {
            Status = status;
            Exception = exception;
            DurationMilliseconds = durationMilliseconds;
            Result = default;
        }
        
        public ProcessStepResult(ProcessStepResultStatus status, int durationMilliseconds)
        {
            Status = status;
            Exception = default;
            DurationMilliseconds = durationMilliseconds;
            Result = default;
        }
    }
}