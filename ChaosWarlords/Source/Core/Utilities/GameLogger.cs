using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Utilities
{
    [ExcludeFromCodeCoverage]
    public struct LogEntry
    {
        public string Timestamp { get; set; }
        public LogChannel Channel { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return $"[{Timestamp}] [{Channel}] {Message}";
        }
    }

    public static class GameLogger
    {
        // Settings
        private static readonly string LogFilePath = "session_log.txt";

        // Storage (Buffer for file writing)
        private static StringBuilder _fileBuffer = new StringBuilder();

        // Thread Safety Lock
        private static readonly object _lock = new object();

        public static void Initialize()
        {
            // Clear previous session log on startup
            try
            {
                File.WriteAllText(LogFilePath, $"--- Session Started: {DateTime.Now} ---\n");
            }
            catch { }
        }

        public static void Log(string message, LogChannel channel = LogChannel.General)
        {
            Log((object)message, channel);
        }

        public static void Log(object messageObj, LogChannel channel = LogChannel.General)
        {
            if (messageObj is null) return;

            string message = messageObj.ToString() ?? string.Empty;
            string timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            lock (_lock)
            {
                // Format the line
                string line = $"[{timestamp}] [{channel}] {message}";

                // Buffer it
                _fileBuffer.AppendLine(line);

                // Flush to file immediately (or buffer logic if you prefer performance)
                // For a turn-based game, flushing immediately is safer for debugging crashes.
                FlushToFile();
            }
        }

        public static void FlushToFile()
        {
            try
            {
                File.AppendAllText(LogFilePath, _fileBuffer.ToString());
                _fileBuffer.Clear();
            }
            catch
            {
                // Silently fail if file is locked (don't crash the game)
            }
        }
    }
}

