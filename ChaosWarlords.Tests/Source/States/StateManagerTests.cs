using ChaosWarlords.Source.States;
using ChaosWarlords.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Tests.States
{
    [TestClass]
    public class StateManagerTests
    {
        private StateManager _manager = null!;

        [TestInitialize]
        public void Setup()
        {
            // We can pass null for Game here because StateManager just stores the reference
            // and passes it to states, but doesn't use it itself.
            _manager = new StateManager(null!);
        }

        [TestMethod]
        public void PushState_LoadsContent_AndAddsToStack()
        {
            var state = new MockGenericState();
            _manager.PushState(state);

            Assert.IsTrue(state.LoadContentCalled);
            Assert.IsFalse(state.UnloadContentCalled);
        }

        [TestMethod]
        public void PopState_UnloadsContent_AndRemovesFromStack()
        {
            var state = new MockGenericState();
            _manager.PushState(state);
            _manager.PopState();

            Assert.IsTrue(state.UnloadContentCalled);
        }

        [TestMethod]
        public void Update_CallsUpdateOnTopState()
        {
            var state = new MockGenericState();
            _manager.PushState(state);

            _manager.Update(new GameTime());

            Assert.AreEqual(1, state.UpdateCount);
        }

        [TestMethod]
        public void Draw_CallsDrawOnTopState()
        {
            var state = new MockGenericState();
            _manager.PushState(state);

            _manager.Draw(null!); // SpriteBatch can be null for this mock test

            Assert.AreEqual(1, state.DrawCount);
        }

        [TestMethod]
        public void ChangeState_PopsOld_AndPushesNew()
        {
            var oldState = new MockGenericState();
            var newState = new MockGenericState();

            _manager.PushState(oldState);
            _manager.ChangeState(newState);

            Assert.IsTrue(oldState.UnloadContentCalled, "Old state should be unloaded.");
            Assert.IsTrue(newState.LoadContentCalled, "New state should be loaded.");
        }

        [TestMethod]
        public void Update_DoesNothing_WhenStackEmpty()
        {
            // Should not crash
            _manager.Update(new GameTime());
        }
    }
}