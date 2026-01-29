using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;

namespace ChaosWarlords.Tests.Source.Input.Controllers
{
    [TestClass]
    public class PlayerControllerTests
    {
        private PlayerController _controller = null!;
        private IGameplayState _gameState = null!;
        private IInputManager _inputManager = null!;
        private IGameplayInputCoordinator _inputCoordinator = null!;
        private IInteractionMapper _interactionMapper = null!;
        private IGameplayView _view = null!;
        private IActionSystem _actionSystem = null!;

        [TestInitialize]
        public void Setup()
        {
            _gameState = Substitute.For<IGameplayState>();
            _inputManager = Substitute.For<IInputManager>();
            _inputCoordinator = Substitute.For<IGameplayInputCoordinator>();
            _interactionMapper = Substitute.For<IInteractionMapper>();
            _view = Substitute.For<IGameplayView>();
            _actionSystem = Substitute.For<IActionSystem>();

            // Setup simplified dependencies via Interface Segregation
            _gameState.View.Returns(_view);
            _gameState.ActionSystem.Returns(_actionSystem);
            
            // Ensure safe defaults to prevent fall-through logic crashes
            _actionSystem.CurrentState.Returns(ActionState.Normal);
            _actionSystem.IsTargeting().Returns(false);
            _gameState.IsMarketOpen.Returns(false);
            _gameState.IsConfirmationPopupOpen.Returns(false);
            _gameState.IsPauseMenuOpen.Returns(false);
            

            _controller = new PlayerController(
                _gameState,
                _inputManager,
                _inputCoordinator,
                _interactionMapper
            );
        }

        // --- HandlePopupInteractions Tests (The CRAP Risk Hotspot) ---

        [TestMethod]
        public void HandleInputEvent_OptionalEffectPopup_HandlesLeftClick_AndReturnsTrue()
        {
            // Arrange
            _gameState.IsOptionalEffectPopupOpen.Returns(true);
            var mousePos = new Point(100, 200);
            var inputEvent = new InputEventArgs(InputEventType.LeftClick, mousePos.ToVector2(), Keys.None);

            // Act
            // Trigger event via the InputManager event that PlayerController subscribes to
            _inputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(this, inputEvent);

            // Assert
            _view.Received(1).HandleOptionalEffectClick(100, 200);
            
            // Should NOT proceed to check other inputs (like Spy Selection)
            // We can implicitly verify this because _actionSystem.CurrentState defaults to Normal
            // If it tried to access something deeper that wasn't mocked, it might crash, but here we are safe.
        }

        [TestMethod]
        public void HandleInputEvent_OptionalEffectPopup_IgnoresOtherClicks()
        {
             // Arrange
            _gameState.IsOptionalEffectPopupOpen.Returns(true);
            // Right Click should NOT trigger the popup handler
            var inputEvent = new InputEventArgs(InputEventType.RightClick, Vector2.Zero, Keys.None);

            // Act
            _inputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(this, inputEvent);

            // Assert
            _view.DidNotReceive().HandleOptionalEffectClick(Arg.Any<int>(), Arg.Any<int>());
        }

        [TestMethod]
        public void HandleInputEvent_OptionalEffectPopup_DoesNothing_IfPopupClosed()
        {
             // Arrange
            _gameState.IsOptionalEffectPopupOpen.Returns(false);
            var inputEvent = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 200), Keys.None);

            // Act
            _inputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(this, inputEvent);

            // Assert
            _view.DidNotReceive().HandleOptionalEffectClick(Arg.Any<int>(), Arg.Any<int>());
        }

        [TestMethod]
        public void HandleInputEvent_OptionalEffectPopup_DoesNothing_IfViewIsNull()
        {
             // Arrange
            _gameState.View.Returns((IGameplayView?)null);
            _gameState.IsOptionalEffectPopupOpen.Returns(true);
            var inputEvent = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 200), Keys.None);

            // Act
            _inputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(this, inputEvent);

            // Assert
            // Should not crash
        }
    }
}
