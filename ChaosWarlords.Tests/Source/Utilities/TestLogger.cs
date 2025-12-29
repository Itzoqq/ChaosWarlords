using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Tests.Utilities
{
    [ExcludeFromCodeCoverage]
    public static class TestLogger
    {
        // We use a specific log file for tests to distinguish from main game logs
        private static readonly BufferedAsyncLogger _logger = new BufferedAsyncLogger("test_session_log.txt");

        public static IGameLogger Instance => _logger;

        public static void Initialize()
        {
            // No-op, matches previous API
        }

        public static bool IsEnabled
        {
            get => _logger.IsEnabled;
            set => _logger.IsEnabled = value;
        }
    }
}
