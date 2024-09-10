using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tools.StructuredProcess.Simple
{
    public readonly struct StepResultWrapper<TResult>
        where TResult : IStepResult
    {
        public TResult Result { get; private init; }

        public static implicit operator StepResultWrapper<TResult>(TResult resultValue) { return new StepResultWrapper<TResult> { Result = resultValue }; }
    }
        
    public readonly struct ProcessStepResult : IStepResult
    {
        public ProcessStepResultStatus Status { get; init; }
        public int DurationMilliseconds { get; init; }
        public Exception Exception { get; init; }
    }
    
    public abstract class AsyncLinearProcessStep<T> : IProcessStep
    {
        public abstract bool ShouldBeExecutedMoreThanOnce { get; }

        public async ValueTask<StepResultWrapper<ProcessStepResult>> ExecuteAsync(CancellationToken cancellationToken)
        {
            var specificResult = await SpecificExecuteAsync(cancellationToken);

            return new ProcessStepResult
                {
                    Status = specificResult.Status,
                    Exception = specificResult.Exception,
                    DurationMilliseconds = specificResult.DurationMilliseconds,
                };
        }
        
        private async ValueTask<ProcessStepResult<T>> SpecificExecuteAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                InternalStepResult<T> result = await InternalExecute(cancellationToken);

                var stepDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                if (result.Status == ProcessStepResultStatus.Success)
                {
                    return new ProcessStepResult<T>(result.Status, (int)stepDurationMs, result.Result);
                }
                
                return new ProcessStepResult<T>(result.Status, (int)stepDurationMs);
            }
            catch (OperationCanceledException cancelException)
            {
                // Log.Default.Exception("Operation was cancelled", cancelException);
                
                var stepDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                return new ProcessStepResult<T>(ProcessStepResultStatus.Cancelled, (int)stepDurationMs, cancelException);
            }
            catch (Exception exception)
            {
                var stepDurationMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

                return new ProcessStepResult<T>(ProcessStepResultStatus.Failure, (int)stepDurationMs, exception);
            }
        }

        protected abstract ValueTask<InternalStepResult<T>> InternalExecute(CancellationToken cancellationToken);
    }
}