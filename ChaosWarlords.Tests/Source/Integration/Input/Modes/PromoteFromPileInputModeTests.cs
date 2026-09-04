using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class PromoteFromPileInputModeTests
    {
        private PromoteFromPileInputMode _mode = null!;
        private TestGameplayState _stateFake = null!;
        private IInputManager _inputSub = null!;
        private IActionSystem _actionSub = null!;
        private IMarketManager _marketSub = null!;
        private IMapManager _mapSub = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _inputSub = Substitute.For<IInputManager>();
            _actionSub = Substitute.For<IActionSystem>();
            _marketSub = Substitute.For<IMarketManager>();
            _mapSub = Substitute.For<IMapManager>();
            _activePlayer = TestData.Players.RedPlayer();

            _stateFake = new TestGameplayState
            {
                ActionSystem = _actionSub
            };

            _mode = new PromoteFromPileInputMode(_stateFake, _inputSub, _actionSub);
        }

        private void ClearCooldown()
        {
            // HandleInteraction ignores every input until CooldownFrames updates have elapsed.
            for (int i = 0; i < 15; i++) _mode.HandleUpdate(_inputSub, _mapSub, _activePlayer);
        }

        [TestMethod]
        public void HandleInteraction_WithinCooldown_ReturnsNull_EvenOnClick()
        {
            // Arrange - no HandleUpdate calls, so cooldown has not elapsed.
            _stateFake.HoveredBrowserCard = TestData.Cards.CheapCard();
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.IsNull(result, "Input during the cooldown window must be ignored.");
            _actionSub.DidNotReceive().HandlePromoteFromPileSelection(Arg.Any<Card>());
        }

        [TestMethod]
        public void HandleInteraction_RightClick_CancelsTargetingAndSwitchesToNormal()
        {
            // Arrange
            ClearCooldown();
            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            _actionSub.Received(1).CancelTargeting();
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInteraction_EscapeKey_CancelsTargetingAndSwitchesToNormal()
        {
            // Arrange
            ClearCooldown();
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Escape);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            _actionSub.Received(1).CancelTargeting();
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);
            Assert.IsInstanceOfType(result, typeof(SwitchToNormalModeCommand));
        }

        [TestMethod]
        public void HandleInteraction_OtherKeyDown_DoesNothing()
        {
            // Arrange
            ClearCooldown();
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Space);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.IsNull(result);
            _actionSub.DidNotReceive().CancelTargeting();
        }

        [TestMethod]
        public void HandleInteraction_LeftClick_NoHoveredCard_ReturnsNull()
        {
            // Arrange
            ClearCooldown();
            _stateFake.HoveredBrowserCard = null;
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.IsNull(result);
            _actionSub.DidNotReceive().HandlePromoteFromPileSelection(Arg.Any<Card>());
        }

        [TestMethod]
        public void HandleInteraction_LeftClick_InvalidTarget_ReturnsNull()
        {
            // Arrange
            ClearCooldown();
            var hovered = TestData.Cards.CheapCard();
            _stateFake.HoveredBrowserCard = hovered;
            _actionSub.HandlePromoteFromPileSelection(hovered).Returns((PromoteCommand?)null);
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.IsNull(result, "An invalid selection (rejected by ActionSystem) must not produce a command.");
        }

        [TestMethod]
        public void HandleInteraction_LeftClick_ValidTarget_ChainedIntoAnotherTargetingStep_SwitchesToTargeting()
        {
            // Arrange
            ClearCooldown();
            var hovered = TestData.Cards.CheapCard();
            var cmd = new PromoteCommand(hovered.Id, isChainedEffect: true);
            _stateFake.HoveredBrowserCard = hovered;
            _actionSub.HandlePromoteFromPileSelection(hovered).Returns(cmd);
            _actionSub.IsTargeting().Returns(true);
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.AreSame(cmd, result);
            Assert.AreEqual("Targeting", _stateFake.ActiveModeName);
        }

        [TestMethod]
        public void HandleInteraction_LeftClick_ValidTarget_ChainResolved_SwitchesToNormal()
        {
            // Arrange
            ClearCooldown();
            var hovered = TestData.Cards.CheapCard();
            var cmd = new PromoteCommand(hovered.Id, isChainedEffect: true);
            _stateFake.HoveredBrowserCard = hovered;
            _actionSub.HandlePromoteFromPileSelection(hovered).Returns(cmd);
            _actionSub.IsTargeting().Returns(false);
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            // Assert
            Assert.AreSame(cmd, result);
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);
        }
    }
}
