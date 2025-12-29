using ChaosWarlords.Source.Core.Events;

namespace ChaosWarlords.Tests.Source.Core.Events
{
    [TestClass]
    [TestCategory("Unit")]
    public class StateChangeEventTests
    {
        [TestMethod]
        public void Constructor_WithValidValues_SetsProperties()
        {
            // Act
            var evt = new StateChangeEvent("PlayerPower", 10, 5);
            
            // Assert
            Assert.AreEqual("PlayerPower", evt.StateName);
            Assert.AreEqual("10", evt.NewValue);
            Assert.AreEqual("5", evt.OldValue);
            Assert.AreEqual("StateChange", evt.Context);
        }

        [TestMethod]
        public void Constructor_WithNullNewValue_SetsNullString()
        {
            // Act
            var evt = new StateChangeEvent("TestState", null, 5);
            
            // Assert
            Assert.AreEqual("null", evt.NewValue);
            Assert.AreEqual("5", evt.OldValue);
        }

        [TestMethod]
        public void Constructor_WithNullOldValue_SetsNullString()
        {
            // Act
            var evt = new StateChangeEvent("TestState", 10, null);
            
            // Assert
            Assert.AreEqual("10", evt.NewValue);
            Assert.AreEqual("null", evt.OldValue);
        }

        [TestMethod]
        public void Constructor_WithComplexObjects_ConvertsToString()
        {
            // Arrange
            var player = TestData.Players.RedPlayer();
            
            // Act
            var evt = new StateChangeEvent("CurrentPlayer", player, null);
            
            // Assert
            Assert.IsNotNull(evt.NewValue);
            Assert.AreEqual("null", evt.OldValue);
            Assert.AreEqual("StateChange", evt.Context);
        }

        [TestMethod]
        public void Constructor_WithBothNullValues_SetsBothToNullString()
        {
            // Act
            var evt = new StateChangeEvent("TestState", null, null);
            
            // Assert
            Assert.AreEqual("null", evt.NewValue);
            Assert.AreEqual("null", evt.OldValue);
        }

        [TestMethod]
        public void Constructor_WithDifferentTypes_ConvertsToString()
        {
            // Act
            var evt1 = new StateChangeEvent("IntState", 42, 0);
            var evt2 = new StateChangeEvent("BoolState", true, false);
            var evt3 = new StateChangeEvent("StringState", "new", "old");
            
            // Assert
            Assert.AreEqual("42", evt1.NewValue);
            Assert.AreEqual("0", evt1.OldValue);
            Assert.AreEqual("True", evt2.NewValue);
            Assert.AreEqual("False", evt2.OldValue);
            Assert.AreEqual("new", evt3.NewValue);
            Assert.AreEqual("old", evt3.OldValue);
        }
    }
}
