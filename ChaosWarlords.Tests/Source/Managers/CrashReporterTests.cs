using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using System.IO;

namespace ChaosWarlords.Tests.Source.Managers
{
    [TestClass]
    [TestCategory("Unit")]
    public class CrashReporterTests
    {
        private IGameLogger _logger = null!;
        private CrashReporter _reporter = null!;
        private readonly List<string> _filesToCleanUp = new();

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<IGameLogger>();
            _reporter = new CrashReporter(_logger);
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var file in _filesToCleanUp)
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }

        [TestMethod]
        public void ReportCrash_AlwaysLogsTheExceptionAtErrorLevel_RegardlessOfReplayManager()
        {
            var ex = new InvalidOperationException("boom");

            _reporter.ReportCrash(ex, replayManager: null, source: "Update");

            _logger.Received(1).Log(Arg.Is<string>(s => s.Contains("Update")), LogChannel.Error);
            _logger.Received(1).Log(ex, LogChannel.Error);
        }

        [TestMethod]
        public void ReportCrash_ReturnsNull_AndDumpsNothing_WhenReplayManagerIsNull()
        {
            string? path = _reporter.ReportCrash(new Exception("x"), replayManager: null, source: "Draw");

            Assert.IsNull(path);
        }

        [TestMethod]
        public void ReportCrash_ReturnsNull_AndDumpsNothing_WhenCurrentlyReplaying()
        {
            var replayManager = Substitute.For<IReplayManager>();
            replayManager.IsReplaying.Returns(true);

            string? path = _reporter.ReportCrash(new Exception("x"), replayManager, source: "Update");

            Assert.IsNull(path);
            _ = replayManager.DidNotReceive().GetRecordingJson();
        }

        [TestMethod]
        public void ReportCrash_DumpsTheRecordingToDisk_WhenNotReplaying()
        {
            var replayManager = Substitute.For<IReplayManager>();
            replayManager.IsReplaying.Returns(false);
            replayManager.GetRecordingJson().Returns("{\"seed\":123}");

            string? path = _reporter.ReportCrash(new Exception("x"), replayManager, source: "Update");
            if (path != null) _filesToCleanUp.Add(path);

            Assert.IsNotNull(path);
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual("{\"seed\":123}", File.ReadAllText(path!));
            Assert.Contains("Update", path!);
        }

        [TestMethod]
        public void ReportCrash_OnlyDumpsOnce_AcrossMultipleCallsOnTheSameInstance()
        {
            // Avoids flooding disk if the same recoverable per-frame exception recurs every
            // frame - one repro is what's valuable, see ICrashReporter's own doc comment.
            var replayManager = Substitute.For<IReplayManager>();
            replayManager.IsReplaying.Returns(false);
            replayManager.GetRecordingJson().Returns("{}");

            string? first = _reporter.ReportCrash(new Exception("x"), replayManager, source: "Update");
            if (first != null) _filesToCleanUp.Add(first);
            string? second = _reporter.ReportCrash(new Exception("x"), replayManager, source: "Update");

            Assert.IsNotNull(first);
            Assert.IsNull(second, "A second crash on the same reporter instance must not dump a second file.");
        }

        [TestMethod]
        public void ReportCrash_ReturnsNull_AndLogsTheFailure_WhenGetRecordingJsonThrows()
        {
            // The crash-reporting path itself must never throw - a failure while reporting a
            // crash must not cause a second crash.
            var replayManager = Substitute.For<IReplayManager>();
            replayManager.IsReplaying.Returns(false);
            replayManager.GetRecordingJson().Returns(_ => throw new InvalidOperationException("no active recording"));

            string? path = _reporter.ReportCrash(new Exception("x"), replayManager, source: "Update");

            Assert.IsNull(path);
            _logger.Received(1).Log(Arg.Is<string>(s => s.Contains("Failed to dump crash replay")), LogChannel.Error);
        }
    }
}
