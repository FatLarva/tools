using System;
using System.Collections.Generic;

namespace Tools.StructuredProcess.Simple
{
    public class StepsLedger : IStepsLedger
    {
        private readonly HashSet<Type> _stepsResults = new ();
            
        public void SetStepCompleted(IProcessStep processStep)
        {
            _stepsResults.Add(processStep.GetType());
        }
        
        public bool IsStepCompleted(IProcessStep processStep)
        {
            return _stepsResults.Contains(processStep.GetType());
        }
    }
}