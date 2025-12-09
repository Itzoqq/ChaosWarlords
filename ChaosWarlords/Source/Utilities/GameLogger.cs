using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.Utilities
{
    public struct LogEntry
    {
        public string Timestamp;
        public LogChannel Channel;
        public string Message;
        public Color Color;

        public override string ToString()
        {
            return $"[{Timestamp}] [{Channel}] {Message}";
        }
    }

    public static class GameLogger
    {
        // Settings
        private const int MaxOnScreenLogs = 15;
        private static readonly string LogFilePath = "session_log.txt";

        // Storage
        private static List<LogEntry> _logs = new List<LogEntry>();
        private static StringBuilder _fileBuffer = new StringBuilder();

        // Filters (Toggle these to hide spam!)
        public static Dictionary<LogChannel, bool> ChannelVisibility = new Dictionary<LogChannel, bool>
        {
            { LogChannel.General, true },
            { LogChannel.Input, false }, // Default to hidden to reduce noise
            { LogChannel.Combat, true },
            { LogChannel.Economy, true },
            { LogChannel.AI, true },
            { LogChannel.Error, true }
        };

        public static void Initialize()
        {
            // Clear previous log file
            try { File.WriteAllText(LogFilePath, $"--- CHAOS WARLORDS SESSION START: {DateTime.Now} ---\n"); }
            catch { /* File access might fail, ignore */ }

            Log("Logger Initialized", LogChannel.General);
        }

        public static void Log(string message, LogChannel channel = LogChannel.General)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                Channel = channel,
                Message = message,
                Color = GetColorForChannel(channel)
            };

            _logs.Add(entry);

            // Limit memory usage (Keep last 1000 logs in memory)
            if (_logs.Count > 1000) _logs.RemoveAt(0);

            // Write to file buffer
            _fileBuffer.AppendLine(entry.ToString());

            // Auto-flush to file every 10 logs (prevents data loss on crash)
            if (_logs.Count % 10 == 0) FlushToFile();
        }

        public static void Log(Exception ex)
        {
            // Format the error nicely
            string message = $"CRASH: {ex.Message}\nStack Trace:\n{ex.StackTrace}";

            // Log it to the Error channel
            Log(message, LogChannel.Error);

            // Force save immediately because the game is about to die
            FlushToFile();
        }

        public static void FlushToFile()
        {
            try
            {
                File.AppendAllText(LogFilePath, _fileBuffer.ToString());
                _fileBuffer.Clear();
            }
            catch { /* Fail silently if file is locked */ }
        }

        private static Color GetColorForChannel(LogChannel channel)
        {
            return channel switch
            {
                LogChannel.Error => Color.Red,
                LogChannel.Combat => Color.Orange,
                LogChannel.Economy => Color.Gold,
                LogChannel.Input => Color.Gray,
                LogChannel.AI => Color.Cyan,
                _ => Color.White
            };
        }

        // --- VISUALIZATION ---
        public static void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (font == null) return;

            // Draw a semi-transparent background for the console
            // (Assuming you want it bottom-left)
            int startY = 500;
            int lineHeight = 18;

            // Filter logs for display
            var visibleLogs = new List<LogEntry>();
            for (int i = _logs.Count - 1; i >= 0; i--)
            {
                if (ChannelVisibility[_logs[i].Channel])
                    visibleLogs.Add(_logs[i]);

                if (visibleLogs.Count >= MaxOnScreenLogs) break;
            }

            // Draw from bottom up
            for (int i = 0; i < visibleLogs.Count; i++)
            {
                var log = visibleLogs[i];
                string text = $"[{log.Timestamp}] {log.Message}";
                Vector2 pos = new Vector2(10, startY + (i * lineHeight)); // Drawing downwards or adjust for upwards

                // Let's actually draw them bottom-up (newest at bottom)
                // We'll reverse the drawing position logic:
                // Let's stick the console in the Bottom-Right for now, or just overlay Left

                // FIXED POSITIONING: Bottom Left
                float yPos = 700 - (i * lineHeight);

                // Shadow for readability
                spriteBatch.DrawString(font, text, new Vector2(12, yPos + 1), Color.Black);
                // Main Text
                spriteBatch.DrawString(font, text, new Vector2(10, yPos), log.Color);
            }
        }
    }
}