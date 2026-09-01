using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Input.Modes;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using NSubstitute;
using ChaosWarlords.Source.Core.Events;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Tests.Integration.Input.Modes
{
    /// <summary>
    /// DiscardInputMode had 0% coverage as of the 2026-09-01 coverage run (see planning.txt
    /// TIER 1) - added this session for Insane Outcast/Neogi and never exercised at all.
    /// Mirrors PromoteInputModeTests.cs's shape: DiscardInputMode is a thin, client-only
    /// "produce a command from a click" boundary (mocked IGameplayState/IInputManager), not a
    /// scenario-harness concern - the harness (ChaosWarlords.Tests/Source/Functional/) covers
    /// what DiscardCardCommand itself does once dispatched.
    /// </summary>
    [TestClass]
    [TestCategory("Integration")]
    public class DiscardInputModeTests
    {
        private DiscardInputMode _mode = null!;
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

            var turnManagerSub = Substitute.For<ITurnManager>();
            turnManagerSub.ActivePlayer.Returns(_activePlayer);
            turnManagerSub.CurrentTurnContext.Returns(new TurnContext(_activePlayer, Utilities.TestLogger.Instance));

            var cardDb = Substitute.For<ICardDatabase>();
            var ps = new PlayerStateManager(Utilities.TestLogger.Instance);

            var matchContext = new MatchContext(
                turnManagerSub, _mapSub, _marketSub, _actionSub, cardDb, ps, Utilities.TestLogger.Instance);

            _stateFake = new TestGameplayState
            {
                MatchContext = matchContext,
                TurnManager = turnManagerSub,
                InputManager = _inputSub,
                ActionSystem = _actionSub,
                MarketManager = _marketSub,
                MapManager = _mapSub
            };

            _mode = new DiscardInputMode(_stateFake, _inputSub, _actionSub);
        }

        [TestMethod]
        public void HandleInteraction_RightClick_ReturnsNull()
        {
            // No cancel/escape for a forced discard - see DiscardInputMode's own doc comment.
            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleInteraction_LeftClickWithNothingHovered_ReturnsNull()
        {
            _stateFake.HoveredHandCard = null;
            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void HandleInteraction_LeftClickOnHoveredHandCard_ReturnsDiscardCardCommandForTheActivePlayer()
        {
            var card = TestData.Cards.CheapCard();
            _activePlayer.AddToHand(card);
            _stateFake.HoveredHandCard = card;

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var result = _mode.HandleInteraction(evt, _marketSub, _mapSub, _activePlayer, _actionSub);

            var discardCmd = result as DiscardCardCommand;
            Assert.IsNotNull(discardCmd, "Should return a DiscardCardCommand for the hovered hand card.");
            Assert.AreEqual(_activePlayer.Color, discardCmd!.TargetPlayerColor,
                "Should target whoever ActivePlayer resolves to at click time - correct for both Insane Outcast (self) and Neogi's forced-actor override (the owing opponent).");
            Assert.AreEqual(card.Id, discardCmd.CardId);
        }
    }
}
