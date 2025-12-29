using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChaosWarlords.Tests.Source.Core.Utilities
{
    [TestClass]
    public class BufferedAsyncLoggerTests
    {
        private string _testLogPath = null!;

        [TestInitialize]
        public void Setup()
        {
            _testLogPath = Path.Combine(Path.GetTempPath(), $"test_log_{Path.GetRandomFileName()}.txt");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_testLogPath))
            {
                // Ensure file handles are released
                try { File.Delete(_testLogPath); } catch { }
            }
        }

        [TestMethod]
        public void Log_ShouldWriteToFile_Eventually()
        {
            // Arrange
            using var logger = new BufferedAsyncLogger(_testLogPath);

            // Act
            logger.Log("Test Message");

            // Wait for partial flush (default interval is 500ms, wait slightly longer)
            Thread.Sleep(1000);

            // Assert
            Assert.IsTrue(File.Exists(_testLogPath), "Log file should exist.");
            string content = File.ReadAllText(_testLogPath);
            StringAssert.Contains(content, "Test Message");
        }

        [TestMethod]
        public void Dispose_ShouldFlushRemainingLogs()
        {
            // Arrange
            // Create scope for using block to force Dispose
            {
                using var logger = new BufferedAsyncLogger(_testLogPath);
                logger.Log("Message 1");
                logger.Log("Message 2");
                // Dispose triggers immediately at end of block
            }

            // Assert
            Assert.IsTrue(File.Exists(_testLogPath));
            string content = File.ReadAllText(_testLogPath);
            StringAssert.Contains(content, "Message 1");
            StringAssert.Contains(content, "Message 2");
        }
        [TestMethod]
        public void Log_WhenDisabled_ShouldNotWrite()
        {
            // Arrange
            using var logger = new BufferedAsyncLogger(_testLogPath)
            {
                IsEnabled = false
            };

            // Act
            logger.Log("This should not appear");
            logger.Log("Neither should this");

            // Wait for potential flush (though none should happen)
            Thread.Sleep(600);

            // Assert
            // File might exist because constructor writes "Session Started", 
            // but our messages should not be there.
            if (File.Exists(_testLogPath))
            {
                string content = File.ReadAllText(_testLogPath);
                Assert.DoesNotContain("This should not appear", content);
            }
        }

        [TestMethod]
        public void Log_WithNullObject_ShouldLogNullString()
        {
            // Arrange
            using var logger = new BufferedAsyncLogger(_testLogPath);

            // Act
            logger.Log(null!);

            Thread.Sleep(600);

            // Assert
            string content = File.ReadAllText(_testLogPath);
            StringAssert.Contains(content, "null");
            // Or just "null" depending on implementation. 
            // Looking at impl: Log(object) -> Log(obj?.ToString() ?? "null")
            // So it should contain "null"
        }

        [TestMethod]
        public void Log_FromMultipleThreads_ShouldHandleConcurrency()
        {
            // Arrange
            using var logger = new BufferedAsyncLogger(_testLogPath);
            int threadCount = 10;
            int logsPerThread = 50;

            // Act
            Parallel.For(0, threadCount, i =>
            {
                for (int j = 0; j < logsPerThread; j++)
                {
                    logger.Log($"Thread {i} - Msg {j}");
                }
            });

            // Wait/Dispose to flush
            // We'll rely on Dispose here since Parallel.For is fast
        }

        // This is separate to verify the flush part of the concurrency test
        [TestMethod]
        public void Concurrency_StressTest_Verification()
        {
            // Arrange
            using (var logger = new BufferedAsyncLogger(_testLogPath))
            {
                Parallel.For(0, 10, i =>
                {
                    for (int j = 0; j < 50; j++)
                    {
                        logger.Log($"Thread {i} - Msg {j}");
                    }
                });
            } // Dispose flushes here

            // Assert
            string[] lines = File.ReadAllLines(_testLogPath);
            // 1 header line + 500 logs
            // Allow for maybe 1 extra empty line depending on WriteLine behavior
            Assert.IsGreaterThanOrEqualTo(lines.Length, 501, $"Expected at least 501 lines, got {lines.Length}");
        }
    }
}
