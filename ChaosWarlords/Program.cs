using System.Diagnostics.CodeAnalysis;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ChaosWarlords.Tests")]

namespace ChaosWarlords
{
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        static void Main()
        {
            // COMPOSITION ROOT: Initialize Logger
            // We verify BufferedAsyncLogger is used for file I/O and diposed correctly.
            using BufferedAsyncLogger logger = new BufferedAsyncLogger();

            using var game = new Game1(logger);

            try
            {
                game.Run();
            }
            catch (Exception ex)
            {
                // 1. Log the fatal error and dump a crash-repro replay (best-effort - the game
                // may have crashed before ReplayManager was even assigned, e.g. during
                // LoadContent, in which case there's nothing to dump).
                logger.Log("FATAL CRASH DETECTED", LogChannel.Error);
                new CrashReporter(logger).ReportCrash(ex, game.ReplayManager, "Fatal");

                // 2. Flush immediately just in case - Dispose handles this via FlushRemaining
                logger.Dispose();
            }
            // 'using' block handles logger.Dispose() which flushes logs normally.
        }
    }
}

