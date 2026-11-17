using Logging;

namespace Tools.StructuredProcess.Declarative
{
    /// <summary>
    /// Base for a partial process with a Steps initializer and a nested Output struct.
    /// Call the generated ExecuteAsync on the main thread. The linear executor also
    /// runs Any steps there. ParallelProcess can execute those steps on the thread pool.
    /// </summary>
    public abstract class LinearProcess : ProcessBase
    {
        protected LinearProcess(ILog logger) : base(logger)
        {
        }
    }
}
