using System;

namespace Tools.StructuredProcess.Declarative
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class DataAttribute : Attribute
    {
        public string Id { get; }

        public DataAttribute(string id)
        {
            Id = id;
        }
    }
}
