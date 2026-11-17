using System;
using Logging;

namespace Tools.StructuredProcess.Declarative
{
    /// <summary>
    /// Base for a partial process with a Steps initializer and a nested Output struct.
    /// Call the generated ExecuteAsync on the main thread. The linear executor also
    /// runs Any steps there; Any permits background execution in a future scheduler.
    /// </summary>
    public abstract class LinearProcess
    {
        private readonly ILog _logger;

        protected LinearProcess(ILog logger)
        {
            _logger = logger ?? Log.Default;
        }

        protected void ReportStepFailure(StepId step, string error, Exception exception)
        {
            string message = $"{GetType().Name}: step '{step}' failed: {error}";
            if (exception == null)
            {
                _logger.Warning(message);
            }
            else
            {
                _logger.Exception(message, exception);
            }
        }
    }
}
