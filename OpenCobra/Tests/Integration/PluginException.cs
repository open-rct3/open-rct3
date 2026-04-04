using System;

namespace OvlTestBench.Tests
{
    // Thrown when a plugin abort is invoked via the host abort function
    public class PluginException : Exception
    {
        public PluginException(string message) : base(message) { }
    }
}
