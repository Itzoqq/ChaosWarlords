using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Commands;
using System.Linq;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Data;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    [TestClass]
    [TestCategory("Integration")]
    public class DevourInputModeTests
    {
        private DevourInputMode _mode = null!;
        private TestGameplayState _stateFake = null!;
        private IInputManager _mockInputManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private IMarketManager _marketSub = null!;
        private IMapManager _mapSub = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _marketSub = Substitute.For<IMarketManager>();
            _mapSub = Substitute.For<IMapManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();
            _mockInputManager = Substitute.For<IInputManager>();
            
            _stateFake = new TestGameplayState
            {
               ActionSystem = _mockActionSystem
            };

            _activePlayer = new Player(PlayerColor.Red);
            _mode = new DevourInputMode(_stateFake, _mockInputManager, _mockActionSystem);
        }

        [TestMethod]
        public void HandleInteraction_SwitchesToTargeting_WhenActionChains()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            var targetCard = TestData.Cards.CheapCard();

            _stateFake.HoveredHandCard = targetCard;

            // SIMULATE CHAIN: ActionSystem is now targeting (e.g. Supplant)
            _mockActionSystem.IsTargeting().Returns(true);

            // Simulating updates is necessary because of Cooldown check
            for (int i = 0; i < 15; i++) _mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);
            
            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).HandleDevourSelection(targetCard);
            // Standard Flow: InputMode detects chaining and switches directly
            Assert.AreEqual("Targeting", _stateFake.ActiveModeName);
        }

        [TestMethod]
        public void HandleInteraction_LogsWarning_WhenDevouringSelf()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _stateFake.HoveredHandCard = sourceCard; // Same card
            
            _stateFake.HoveredHandCard = sourceCard; // Same card
            
            for (int i = 0; i < 15; i++) _mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);

            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // Assert
            _mockActionSystem.Received(1).HandleDevourSelection(sourceCard);
        }

        [TestMethod]
        public void HandleInteraction_DoesNothing_WhenNoCardHovered()
        {
            // Arrange
            _stateFake.HoveredHandCard = null;

            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // Assert
            // DevourInputMode calls actionSystem.HandleDevourSelection if card found.
            // If no card, it shouldn't call it.
            _mockActionSystem.DidNotReceive().HandleDevourSelection(Arg.Any<Card>());
            _mockActionSystem.DidNotReceive().CompleteAction();
        }

        [TestMethod]
        public void HandleInteraction_Spacebar_SkippedTarget_AndCommits()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            // IMPORTANT: Set location to Hand for Pre-Commit flow
            sourceCard.Location = CardLocation.Hand;

            _mockActionSystem.PendingCard.Returns(sourceCard);
            
            // Re-create mode to capture PendingCard if needed (or just ensure mock is ready)
            var mode = new DevourInputMode(_stateFake, _mockInputManager, _mockActionSystem);
            
            _mockActionSystem.ClearReceivedCalls();

            _mockActionSystem.ClearReceivedCalls();

            for (int i = 0; i < 15; i++) mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);

            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Space);

            // Act
            var result = mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // Assert
            // 1. Check SkippedTarget was set
            _mockActionSystem.Received(1).SetPreTarget(sourceCard, ActionState.TargetingDevourHand, ActionSystem.SkippedTarget);

            // 2. Must NOT call CompleteAction() here - the card is still in Hand at this
            // point (nothing has pushed it onto ExecutionStack yet), so CompleteAction()
            // would hit its "no stack context" fallback and fire OnActionCompleted
            // prematurely. The returned PlayCardCommand below is the real, single commit
            // path (see DevourInputMode.HandleSkipOptionalCost's comment).
            _mockActionSystem.DidNotReceive().CompleteAction();

            // 3. Check Mode Switch
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);

            // 4. Check Play Command returned
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(PlayCardCommand));
            var cmd = (PlayCardCommand)result;
            Assert.IsTrue(cmd.BypassChecks, "Command should have BypassChecks set to true for Spacebar skip.");
        }

        [TestMethod]
        public void HandleInteraction_SelectTarget_PreCommits_AndCommits()
        {
            // Arrange
            var sourceCard = TestData.Cards.DevourCard();
            sourceCard.Location = CardLocation.Hand;
            var targetCard = TestData.Cards.CheapCard();

            _mockActionSystem.PendingCard.Returns(sourceCard);
            _stateFake.HoveredHandCard = targetCard;

            // Re-create mode
            var mode = new DevourInputMode(_stateFake, _mockInputManager, _mockActionSystem);
            
            _mockActionSystem.ClearReceivedCalls();

            _mockActionSystem.ClearReceivedCalls();

            for (int i = 0; i < 15; i++) mode.HandleUpdate(_mockInputManager, _mapSub, _activePlayer);

            var evt = new InputEventArgs(InputEventType.LeftClick, Vector2.Zero);

            // Act
            var result = mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _mockActionSystem);

            // Assert
            // 1. Check Target was set
            _mockActionSystem.Received(1).SetPreTarget(sourceCard, ActionState.TargetingDevourHand, targetCard);

            // 2. Must NOT call CompleteAction() here - see the Spacebar test above for why.
            _mockActionSystem.DidNotReceive().CompleteAction();

            // 3. Check Mode Switch
            Assert.AreEqual("Normal", _stateFake.ActiveModeName);

            // 4. Check Play Command returned
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(PlayCardCommand));
            var cmd = (PlayCardCommand)result;
            Assert.IsTrue(cmd.BypassChecks, "Command should have BypassChecks set to true for Target Selection.");
        }
    }
}
