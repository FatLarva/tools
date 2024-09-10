using System.Threading;
using System.Threading.Tasks;

namespace Tools.StructuredProcess.Simple
{
    public interface IProcessStep
    {
        ValueTask<StepResultWrapper<ProcessStepResult>> ExecuteAsync(CancellationToken cancellationToken);
        
        bool ShouldBeExecutedMoreThanOnce { get; }
    }
}