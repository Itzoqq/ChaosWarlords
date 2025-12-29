using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using System;
using System.Diagnostics.CodeAnalysis;
using ChaosWarlords.Source.Utilities;
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

            using var game = new ChaosWarlords.Game1(logger);

            try
            {
                game.Run();
            }
            catch (Exception ex)
            {
                // 1. Log the fatal error using our instance
                logger.Log("FATAL CRASH DETECTED", LogChannel.Error);
                logger.Log(ex, LogChannel.Error);

                // 2. Flush immediately just in case - Dispose handles this via FlushRemaining
                logger.Dispose(); 
            }
            // 'using' block handles logger.Dispose() which flushes logs normally.
        }
    }
}

