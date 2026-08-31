using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Core.Tests
{
    /// <summary>
    /// Trivial no-op IGameLogger for tests that need an instance to satisfy a constructor but
    /// don't care about logged output. ChaosWarlords.Tests has its own TestLogger (backed by
    /// BufferedAsyncLogger, writing to a file) - this project deliberately doesn't reuse it
    /// (that would mean depending on the other test project, which no test project in this
    /// solution does), so it gets this much smaller equivalent instead.
    /// </summary>
    public sealed class NullTestLogger : IGameLogger
    {
        public static readonly NullTestLogger Instance = new();

        public void Log(string message, LogChannel channel = LogChannel.General) { }
        public void Log(object message, LogChannel channel = LogChannel.General) { }
    }
}
