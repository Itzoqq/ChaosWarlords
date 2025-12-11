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

        // Thread Safety Lock
        private static readonly object _lock = new object();

        // Filters
        public static Dictionary<LogChannel, bool> ChannelVisibility = new Dictionary<LogChannel, bool>
        {
            { LogChannel.General, true },
            { LogChannel.Input, false },
            { LogChannel.Combat, true },
            { LogChannel.Economy, true },
            { LogChannel.AI, true },
            { LogChannel.Error, true }
        };

        public static void Initialize()
        {
            lock (_lock)
            {
                try { File.WriteAllText(LogFilePath, $"--- CHAOS WARLORDS SESSION START: {DateTime.Now} ---\n"); }
                catch { /* Ignore file access errors */ }
            }
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

            lock (_lock)
            {
                _logs.Add(entry);

                // Limit memory usage
                if (_logs.Count > 1000) _logs.RemoveAt(0);

                // Write to file buffer
                _fileBuffer.AppendLine(entry.ToString());

                // Auto-flush every 10 logs
                if (_logs.Count % 10 == 0) FlushToFile();
            }
        }

        public static void Log(Exception ex)
        {
            string message = $"CRASH: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
            Log(message, LogChannel.Error);

            // Force flush immediately on crash
            lock (_lock)
            {
                FlushToFile();
            }
        }

        public static void FlushToFile()
        {
            // Note: This method is called from inside a lock in Log(), 
            // but we lock again here in case it's called externally.
            // Recursive locks are allowed in C#, but just to be safe/explicit:
            if (_fileBuffer.Length == 0) return;

            try
            {
                // We grab the text and clear the buffer inside the lock
                string textToWrite;
                lock (_lock)
                {
                    textToWrite = _fileBuffer.ToString();
                    _fileBuffer.Clear();
                }

                // Write to disk (File.AppendAllText manages its own file locking usually, 
                // but doing it sequentially is safer for tests)
                File.AppendAllText(LogFilePath, textToWrite);
            }
            catch { /* Fail silently if file is locked by another process */ }
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

        public static void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (font == null) return;
            int lineHeight = 18;

            // Create a local copy of logs to draw so we don't lock the UI thread for long
            List<LogEntry> logsToDraw;
            lock (_lock)
            {
                logsToDraw = new List<LogEntry>(_logs);
            }

            var visibleLogs = new List<LogEntry>();
            // Iterate backwards on our local copy
            for (int i = logsToDraw.Count - 1; i >= 0; i--)
            {
                if (ChannelVisibility[logsToDraw[i].Channel])
                    visibleLogs.Add(logsToDraw[i]);

                if (visibleLogs.Count >= MaxOnScreenLogs) break;
            }

            for (int i = 0; i < visibleLogs.Count; i++)
            {
                var log = visibleLogs[i];
                string text = $"[{log.Timestamp}] {log.Message}";
                float yPos = 700 - (i * lineHeight);

                spriteBatch.DrawString(font, text, new Vector2(12, yPos + 1), Color.Black);
                spriteBatch.DrawString(font, text, new Vector2(10, yPos), log.Color);
            }
        }
    }
}