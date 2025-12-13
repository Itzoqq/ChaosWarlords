using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosWarlords.Tests.Source.Utilities
{
    [TestClass]
    public class CachedIntTextTests
    {
        [TestMethod]
        public void Constructor_BuildsCorrectInitialString()
        {
            // Arrange & Act
            var cache = new CachedIntText("HP: ", 100, "/100");

            // Assert
            Assert.AreEqual("HP: 100/100", cache.Output.ToString());
        }

        [TestMethod]
        public void Update_UpdatesText_WhenValueChanges()
        {
            // Arrange
            var cache = new CachedIntText("Power: ", 0);
            Assert.AreEqual("Power: 0", cache.Output.ToString());

            // Act
            cache.Update(5);

            // Assert
            Assert.AreEqual("Power: 5", cache.Output.ToString());
        }

        [TestMethod]
        public void Update_DoesNotChangeText_WhenValueIsSame()
        {
            // Arrange
            var cache = new CachedIntText("Score: ", 10);
            string originalReference = cache.Output.ToString();

            // Act
            // We update with the SAME value. 
            // In a real memory test, we'd verify no new alloc, but here we ensure logic holds.
            cache.Update(10);

            // Assert
            Assert.AreEqual("Score: 10", cache.Output.ToString());
        }

        [TestMethod]
        public void Update_ForceRebuild_UpdatesEvenIfValueSame()
        {
            // This tests the 'force' flag functionality
            // Arrange
            var cache = new CachedIntText("Turn: ", 1);

            // Act
            cache.Update(1, force: true);

            // Assert
            Assert.AreEqual("Turn: 1", cache.Output.ToString());
        }
    }
}