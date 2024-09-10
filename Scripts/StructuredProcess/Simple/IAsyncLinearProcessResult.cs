using System;

namespace Tools.StructuredProcess.Simple
{
    public interface IAsyncLinearProcessResult
    {
        ProcessResultStatus Status { get; }
        int DurationMs { get; }
        Exception Exception { get; }
        IProcessStep FailedStep { get; }
        IStepsLedger StepsLedger { get; }
    }
}