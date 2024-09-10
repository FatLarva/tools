namespace Tools.StructuredProcess.Simple
{
    public readonly struct InternalStepResult<T>
    {
        public readonly ProcessStepResultStatus Status;
        public readonly T Result;

        public InternalStepResult(ProcessStepResultStatus status,T result)
        {
            Status = status;
            Result = result;
        }
        
        public InternalStepResult(ProcessStepResultStatus status)
        {
            Status = status;
            Result = default;
        }
        
        public static implicit operator InternalStepResult<T>(T resultValue) { return new InternalStepResult<T>(ProcessStepResultStatus.Success, resultValue); }
        
        public static implicit operator InternalStepResult<T>(ProcessStepResultStatus status) { return new InternalStepResult<T>(status); } 
    }
}