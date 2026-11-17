using System;

namespace Tools.StructuredProcess.Declarative
{
    public readonly struct StepId
    {
        public Type Type { get; }
        public string Name { get; }

        public StepId(Type type)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Name = null;
        }

        public StepId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A step ID cannot be empty.", nameof(name));
            }

            Type = null;
            Name = name;
        }

        public static implicit operator StepId(Type type)
        {
            return new StepId(type);
        }

        public static implicit operator StepId(string name)
        {
            return new StepId(name);
        }

        public override string ToString()
        {
            return Name ?? Type?.FullName ?? "<unset>";
        }
    }
}
