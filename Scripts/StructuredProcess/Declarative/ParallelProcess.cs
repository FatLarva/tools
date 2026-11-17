using Logging;

namespace Tools.StructuredProcess.Declarative
{
    /// <summary>
    /// Base for a generated dependency-driven parallel process. Start ExecuteAsync
    /// on the main thread. Main steps can overlap while awaiting; Any steps run on
    /// the thread pool. Completion always waits for all started steps and cleanup.
    /// </summary>
    public abstract class ParallelProcess : ProcessBase
    {
        protected ParallelProcess(ILog logger) : base(logger)
        {
        }
    }
}
