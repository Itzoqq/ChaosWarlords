using System;
using System.IO;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <inheritdoc cref="ICrashReporter"/>
    public class CrashReporter : ICrashReporter
    {
        private readonly IGameLogger _logger;
        private bool _hasDumpedThisSession;

        public CrashReporter(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string? ReportCrash(Exception exception, IReplayManager? replayManager, string source)
        {
            _logger.Log($"CRASH CAUGHT in {source}:", LogChannel.Error);
            _logger.Log(exception, LogChannel.Error);

            if (_hasDumpedThisSession || replayManager is null || replayManager.IsReplaying)
            {
                return null;
            }

            try
            {
                string json = replayManager.GetRecordingJson();
                string fileName = $"crash_replay_{source}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.json";
                File.WriteAllText(fileName, json);
                _hasDumpedThisSession = true;
                _logger.Log($"Crash replay dumped to {fileName} - use it to reproduce this exact crash.", LogChannel.Error);
                return fileName;
            }
            catch (Exception dumpEx)
            {
                _logger.Log($"Failed to dump crash replay: {dumpEx.Message}", LogChannel.Error);
                return null;
            }
        }
    }
}
