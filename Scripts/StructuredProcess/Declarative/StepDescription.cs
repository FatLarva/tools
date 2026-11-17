using System;
using System.Collections.Generic;

namespace Tools.StructuredProcess.Declarative
{
    /// <summary>
    /// A compile-time declaration read by the process generator.
    /// Runtime changes do not change the generated execution flow.
    /// </summary>
    public sealed class StepDescription
    {
        public Type StepType { get; }
        public StepId Id { get; set; }
        public bool Optional { get; set; }
        public StepThread Thread { get; set; } = StepThread.Main;
        public List<StepId> After { get; } = new List<StepId>();
        public Dictionary<string, string> Inputs { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> Outputs { get; } = new Dictionary<string, string>();

        public StepDescription(Type stepType)
        {
            StepType = stepType ?? throw new ArgumentNullException(nameof(stepType));
            Id = new StepId(stepType);
        }
    }
}
