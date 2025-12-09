using System;
using ChaosWarlords.Source.Utilities; // Needed for GameLogger

using var game = new ChaosWarlords.Game1();

try
{
    game.Run();
}
catch (Exception ex)
{
    // 1. Initialize logger if the crash happened BEFORE LoadContent
    // (This is safe to call multiple times because of your specific implementation)
    // But ideally, we just try to log.

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