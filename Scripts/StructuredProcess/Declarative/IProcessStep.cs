using System.Threading;
using System.Threading.Tasks;

namespace Tools.StructuredProcess.Declarative
{
    public interface IProcessStep<TOutput> where TOutput : struct, IProcessOutput
    {
        ValueTask<StepResult<TOutput>> ExecuteAsync(CancellationToken cancellationToken);
    }
}
