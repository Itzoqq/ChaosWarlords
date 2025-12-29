using ChaosWarlords.Source.Core.Interfaces.Services;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// A high-performance logger that queues messages in memory and writes them to disk
    /// on a background thread. This prevents I/O from blocking the main game loop.
    /// </summary>
    public class BufferedAsyncLogger : IGameLogger, IDisposable
    {
        private readonly string _logFilePath;
        private readonly ConcurrentQueue<string> _logQueue = new();
        private readonly Task _processingTask;
        private readonly CancellationTokenSource _cts = new();
        private readonly AutoResetEvent _signal = new(false);
        
        // Configuration
        private const int FlushIntervalMs = 500;
        private const int MaxBatchSize = 100;

        public bool IsEnabled { get; set; } = true;

        public BufferedAsyncLogger(string logFilePath = "session_log.txt")
        {
            _logFilePath = logFilePath;
            
            // Initialize file
            try 
            {
                File.WriteAllText(_logFilePath, $"--- Session Started: {DateTime.Now} ---\n");
            }
            catch (Exception ex)
            {
                // Fallback if file access fails (system console)
                Console.WriteLine($"[BufferedAsyncLogger] Failed to init log file: {ex.Message}");
            }

            // Start background processing
            _processingTask = Task.Run(ProcessQueue);
        }

        public void Log(string message, LogChannel channel = LogChannel.General)
        {
            if (!IsEnabled) return;
            
            // Handle null string explicitly
            message ??= "null";

            string timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            string formattedLine = $"[{timestamp}] [{channel}] {message}";
            
            _logQueue.Enqueue(formattedLine);
            _signal.Set(); // Signal writer thread that data is available
        }

        public void Log(object message, LogChannel channel = LogChannel.General)
        {
            Log(message?.ToString() ?? "null", channel);
        }

        private async Task ProcessQueue()
        {
            var buffer = new StringBuilder();
            
            while (!_cts.Token.IsCancellationRequested)
            {
                // Wait for signal OR timeout (periodic flush)
                _signal.WaitOne(FlushIntervalMs);

                if (_logQueue.IsEmpty) continue;

                // Drain queue up to batch size
                int processed = 0;
                while (_logQueue.TryDequeue(out string? result) && processed < MaxBatchSize)
                {
                    buffer.AppendLine(result);
                    processed++;
                }

                if (buffer.Length > 0)
                {
                    try 
                    {
                        await WriteToFileAsync(buffer.ToString());
                        buffer.Clear();
                    }
                    catch (OperationCanceledException)
                    {
                        // If cancelled during write, we keep the buffer and exit loop to FlushRemaining
                        break; 
                    }
                    catch 
                    {
                        // If IO error, we might want to retry or just drop to avoid infinite loop. 
                        // For safe shutdown, let's keep it in buffer and try synchronous write in FlushRemaining.
                         break;
                    }
                }
            }

            // Flush remaining on exit
            FlushRemaining(buffer);
        }

        private async Task WriteToFileAsync(string text)
        {
            // Propagate cancellation so we can catch it in the loop
            await File.AppendAllTextAsync(_logFilePath, text, _cts.Token);
        }

        private void FlushRemaining(StringBuilder buffer)
        {
            // Drain remaining queue items into the existing buffer (which might have failed writes)
            while (_logQueue.TryDequeue(out string? result))
            {
                buffer.AppendLine(result);
            }

            if (buffer.Length > 0)
            {
                try
                {
                    // Synchronous write for safety during shutdown
                    File.AppendAllText(_logFilePath, buffer.ToString());
                }
                catch { }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _processingTask.Wait(1000); // Give it a second to finish
            }
            catch { } // Ignore task cancellation exceptions
            
            _cts.Dispose();
            _signal.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
