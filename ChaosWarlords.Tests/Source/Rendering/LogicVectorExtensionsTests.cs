using ChaosWarlords.Source.Core.Data;
using ChaosWarlords.Source.Rendering;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Source.Rendering
{
    [TestClass]
    [TestCategory("Unit")]
    public class LogicVectorExtensionsTests
    {
        [TestMethod]
        public void ToLogicVector2_FromVector2_IsAccurate()
        {
            var worldPos = new Vector2(123.456f, 789.012f);
            var logic = worldPos.ToLogicVector2();

            // 123.456 * 1000 = 123456
            Assert.AreEqual(123456, logic.X);
            Assert.AreEqual(789012, logic.Y);
        }

        [TestMethod]
        public void ToVector2_FromLogicVector2_IsAccurate()
        {
            var logic = new LogicVector2(123456, 789012);
            var world = logic.ToVector2();

            Assert.AreEqual(123.456f, world.X, 0.0001f);
            Assert.AreEqual(789.012f, world.Y, 0.0001f);
        }

        [TestMethod]
        public void ToRectangle_FromLogicRectangle_ScalesDownToPixelSpace()
        {
            var logicRect = new LogicRectangle(
                10 * LogicVector2.ScaleFactor, 20 * LogicVector2.ScaleFactor,
                30 * LogicVector2.ScaleFactor, 40 * LogicVector2.ScaleFactor);

            var rect = logicRect.ToRectangle();

            Assert.AreEqual(10, rect.X);
            Assert.AreEqual(20, rect.Y);
            Assert.AreEqual(30, rect.Width);
            Assert.AreEqual(40, rect.Height);
        }
    }
}
