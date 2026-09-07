using System;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Last line of defense for an uncaught exception: logs it verbosely and, if a
    /// currently-recording IReplayManager is supplied, dumps its in-flight recording to disk so
    /// the crash has an exact, deterministic repro sequence instead of just a log line. Never
    /// throws itself - a failure while reporting a crash must not cause a second crash.
    /// </summary>
    public interface ICrashReporter
    {
        /// <summary>
        /// Reports <paramref name="exception"/>, caught while running <paramref name="source"/>
        /// (a short label, e.g. "Update"/"Draw"/"Fatal", used in the log line and dump
        /// filename). Dumps <paramref name="replayManager"/>'s current recording to disk unless
        /// it's null, already replaying (nothing meaningful to dump), or this reporter has
        /// already dumped once this session (avoids flooding disk if the same recoverable
        /// exception recurs every frame - one repro is what matters).
        /// </summary>
        /// <returns>The path the replay was dumped to, or null if nothing was dumped.</returns>
        string? ReportCrash(Exception exception, IReplayManager? replayManager, string source);
    }
}
