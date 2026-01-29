using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using ChaosWarlords.Source.Mechanics.Actions;

namespace ChaosWarlords.Tests.Source.Mechanics.Actions
{
    /// <summary>
    /// Verifies that obsolete methods have been successfully removed from CardPlaySystem.
    /// This test ensures the refactoring completed as planned.
    /// </summary>
    [TestClass]
    [TestCategory("Unit")]
    public class ObsoleteMethodRemovalTests
    {
        [TestMethod]
        public void CardPlaySystem_GetTargetingState_ShouldNotExist()
        {
            // Arrange
            var type = typeof(CardPlaySystem);

            // Act
            var method = type.GetMethod("GetTargetingState",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

            // Assert
            Assert.IsNull(method, 
                "GetTargetingState should have been deleted. It was marked [Obsolete] and replaced by CardRuleEngine.GetStrategy().GetTargetingState()");
        }

        [TestMethod]
        public void CardPlaySystem_IsTargetingEffect_ShouldNotExist()
        {
            // Arrange
            var type = typeof(CardPlaySystem);

            // Act
            var method = type.GetMethod("IsTargetingEffect",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);

            // Assert
            Assert.IsNull(method,
                "IsTargetingEffect should have been deleted. It was marked [Obsolete] and replaced by CardRuleEngine.GetStrategy().IsTargetingEffect");
        }
    }
}
