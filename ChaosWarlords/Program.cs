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
    [ExcludeFromCodeCoverage] // Now we have a class to attach this to!
    public static class Program
    {
        [STAThread] // Good practice for MonoGame/Windows apps
        static void Main()
        {
            using var game = new ChaosWarlords.Game1();

            try
            {
                game.Run();
            }
            catch (Exception ex)
            {
                // 1. Initialize logger if the crash happened BEFORE LoadContent
                // (This is safe to call multiple times because of your specific implementation)

                // 2. Log the fatal error
                GameLogger.Log("FATAL CRASH DETECTED", LogChannel.Error);
                GameLogger.Log(ex);

                // 3. Optional: Re-throw if you want the standard Windows "App has stopped working" dialog
                // throw; 
            }
            finally
            {
                // Ensure logs are saved even if we crash
                GameLogger.FlushToFile();
            }
        }
    }
}

