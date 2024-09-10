using System;

namespace Tools.StructuredProcess.Simple
{
    public interface IStepResult
    {
        public ProcessStepResultStatus Status { get; }
        
        public int DurationMilliseconds { get; }

        public Exception Exception { get; }
    }
}