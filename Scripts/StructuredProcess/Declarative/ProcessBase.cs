using System;
using Logging;

namespace Tools.StructuredProcess.Declarative
{
    public abstract class ProcessBase
    {
        private readonly ILog _logger;

        protected ProcessBase(ILog logger)
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
