using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.States;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NSubstitute;
using ChaosWarlords.Source.Core.Interfaces.State;

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
            // Arrange
            var state = Substitute.For<IState>();

            // Act
            _manager.PushState(state);

            // Assert
            state.Received(1).LoadContent();
            state.DidNotReceive().UnloadContent();
        }

        [TestMethod]
        public void PopState_UnloadsContent_AndRemovesFromStack()
        {
            // Arrange
            var state = Substitute.For<IState>();
            _manager.PushState(state);

            // Act
            _manager.PopState();

            // Assert
            state.Received(1).UnloadContent();
        }

        [TestMethod]
        public void Update_CallsUpdateOnTopState()
        {
            // Arrange
            var state = Substitute.For<IState>();
            _manager.PushState(state);
            var gameTime = new GameTime();

            // Act
            _manager.Update(gameTime);

            // Assert
            state.Received(1).Update(gameTime);
        }

        [TestMethod]
        public void Draw_CallsDrawOnTopState()
        {
            // Arrange
            // We need a mock that implements BOTH IState and IDrawableState
            var state = Substitute.For<IState, IDrawableState>();
            _manager.PushState(state);
            SpriteBatch? sb = null; // Can be null for this test as we just check the call

            // Act
            _manager.Draw(sb!);

            // Assert
            ((IDrawableState)state).Received(1).Draw(sb!);
        }

        [TestMethod]
        public void ChangeState_PopsOld_AndPushesNew()
        {
            // Arrange
            var oldState = Substitute.For<IState>();
            var newState = Substitute.For<IState>();

            _manager.PushState(oldState);

            // Act
            _manager.ChangeState(newState);

            // Assert
            oldState.Received(1).UnloadContent();
            newState.Received(1).LoadContent();
        }

        [TestMethod]
        public void Update_DoesNothing_WhenStackEmpty()
        {
            // Act & Assert (Should not throw)
            _manager.Update(new GameTime());
        }
    }
}

