using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Logging;

namespace Tools.StructuredProcess.Simple
{
    public class AsyncLinearProcess
    {
        private readonly ILog _logger;
        private readonly IStepResultSpecialHandler _stepResultSpecialHandler;

        private IProcessStep _currentStep;

        public IProcessStep CurrentStep => _currentStep;
        
        public AsyncLinearProcess(ILog logger, IStepResultSpecialHandler stepResultSpecialHandler)
        {
            _logger = logger;
            _stepResultSpecialHandler = stepResultSpecialHandler;
        }

        public async ValueTask<IAsyncLinearProcessResult> ExecuteStepsAsync(IEnumerable<IProcessStep> steps, IStepsLedger stepsLedger, CancellationToken cancellationToken)
        {
            var processType = GetType();
            
            _logger?.Info($"{processType.Name} started.");
            
            var startTime = DateTime.UtcNow;

            _currentStep = null;
            var processResult = new AsyncLinearProcessResult(stepsLedger);
            
            foreach (IProcessStep step in steps)
            {
                var isStepAlreadyCompleted = processResult.StepsLedger.IsStepCompleted(step); 
                if (!isStepAlreadyCompleted || step.ShouldBeExecutedMoreThanOnce)
                {
                    Type stepType = step.GetType();
                    
                    _logger?.Info($"{stepType.Name} started.");

                    _currentStep = step;
                    var stepResult = (await step.ExecuteAsync(cancellationToken)).Result;

                    if (stepResult.Status == ProcessStepResultStatus.Success)
                    {
                        _logger?.Info($"{stepType.Name} finished with result: {stepResult.Status}. Step time: {stepResult.DurationMilliseconds.ToString()}");
                    }
                    else
                    {
                        _logger?.Warning($"{stepType.Name} finished with result: {stepResult.Status}. Step time: {stepResult.DurationMilliseconds.ToString()}");
                    }

                    if (stepResult.Status == ProcessStepResultStatus.Success)
                    {
                        if (_stepResultSpecialHandler?.CanHandle(step) ?? false)
                        {
                            _stepResultSpecialHandler.HandleStepResult(step, stepResult);
                        }
                        
                        processResult.StepsLedger.SetStepCompleted(step);
                        processResult.CompleteGracefully();
                    }
                    else
                    {
                        Fail(step, stepResult, processResult);
                        break;
                    }
                }
                
                if (processResult.Status != ProcessResultStatus.Success && cancellationToken.IsCancellationRequested)
                {
                    processResult.Cancel(step);
                }
            }
            
            _currentStep = null;
            
            var durationMs = GetDurationMs(in startTime);
            processResult.SetDuration(durationMs);
            
            _logger?.Info($"{processType.Name} finished with result: {processResult.Status}. Total duration: {durationMs.ToString()}ms");
            
            return processResult;
        }

        private void Fail(IProcessStep step, IStepResult stepResult, AsyncLinearProcessResult processResult)
        {
            switch (stepResult.Status)
            {
                case ProcessStepResultStatus.Cancelled:
                    processResult.Cancel(step, stepResult.Exception);
                    break;
                case ProcessStepResultStatus.Failure:
                    processResult.Fail(step, stepResult.Exception);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stepResult.Status), stepResult.Status, $"Unexpected status value: {stepResult.Status}");
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetDurationMs(in DateTime startTime)
        {
            return (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        }
    }
}