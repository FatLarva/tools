using System;
using System.Collections.Generic;

namespace Logging
{
    // The runtime uses the existing Tools logger. These compiler tests run without Unity.
    public static class Log
    {
        public static ILog Default { get; } = new TestLog();
    }

    public sealed class TestLog : ILog
    {
        public readonly List<string> Messages = new List<string>();

        public void Info(string message)
        {
            Messages.Add(message);
        }

        public void Warning(string message)
        {
            Messages.Add(message);
        }

        public void Error(string message)
        {
            Messages.Add(message);
        }

        public void Exception(string message, Exception exception)
        {
            Messages.Add(message);
        }
    }
}
