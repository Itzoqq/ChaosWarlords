using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Rules;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ChaosWarlords.Tests.Integration.Mechanics
{
    [TestClass]
    [TestCategory("Integration")]
    public class DevourMechanicsTests
    {
        private MatchContext _context = null!;
        private Player _player = null!;
        private ActionSystem _actionSystem = null!;
        private IMarketManager _marketManager = null!;
        private IMapManager _mapManager = null!;
        private IMatchManager _matchManager = null!;
        private IUIEventMediator _uiMediator = null!;
        private IGameLogger _logger = null!;
        private IPlayerStateManager _playerStateManager = null!;
        private IMarketStateManager _marketStateManager = null!;

        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            _logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;

            _player = new Player(PlayerColor.Red);

            var turnManager = Substitute.For<ITurnManager>();
            turnManager.ActivePlayer.Returns(_player);

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _matchManager = Substitute.For<IMatchManager>();
            _marketStateManager = Substitute.For<IMarketStateManager>();
            _marketStateManager = Substitute.For<IMarketStateManager>();
            _playerStateManager = new PlayerStateManager(_logger); // Real State Manager for Integration

            _actionSystem = new ActionSystem(turnManager, _mapManager, _logger);
            _actionSystem.SetMatchManager(_matchManager);
            _actionSystem.SetMarketManager(_marketManager);
            _actionSystem.SetMarketStateManager(_marketStateManager);
            _actionSystem.SetPlayerStateManager(_playerStateManager);

            _uiMediator = Substitute.For<IUIEventMediator>();

            _context = new MatchContext(
                turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                Substitute.For<ICardDatabase>(),
                _playerStateManager,
                _uiMediator,
                _logger,
                12345
            );

            // Use Real MatchManager to test integration logic (DevourMarketCard -> MarketManager)
            _matchManager = new MatchManager(_context, _logger, Substitute.For<IVictoryManager>());
            _actionSystem.SetMatchManager(_matchManager);
        }

        #region FEATURE: Market Devour & Replace (Carrion Crawler)

        [TestMethod]
        public void MarketDevour_WithReplace_ReplacesCard()
        {
             // Arrange
            // 1. Create Source Card (Carrion Crawler style)
            var sourceCard = new Card("carrion", "Carrion Crawler", 4, CardAspect.Oblivion, 1, 3, 0);
            sourceCard.AddEffect(new CardEffect(EffectType.Devour, 1) 
            { 
                TargetLocation = CardLocation.Market,
                ReplaceWithSource = true
            });
            sourceCard.Location = CardLocation.Played; 
            _player.PlayedCards.Add(sourceCard);

            // 2. Create Target Card in Market
            var targetCard = new Card("m1", "Market Victim", 0, CardAspect.Neutral, 0, 0, 0);
            targetCard.Location = CardLocation.Market;

            // 3. Start Targeting
            _actionSystem.StartTargeting(ActionState.TargetingDevourMarket, sourceCard);

            // Act
            // Act
            var cmd = _actionSystem.HandleDevourMarketSelection(targetCard);
            var state = Substitute.For<IGameplayState>();
            state.MatchManager.Returns(_matchManager); // Used for DevourMarketCard
            state.ActionSystem.Returns(_actionSystem); // Used for AdvanceDevourChain (Clearing State)
            state.MatchContext.Returns((MatchContext)null!); // Explicit null for test setup, suppress warning
            // Wait, DevourCardCommand uses state.MatchContext?.RecordAction. 
            // So null match context is fine (propagates null).
            // But DevourMarketCard is on IMatchManager interface now.
            
            Assert.IsNotNull(cmd);
            cmd.Execute(state);

            // Assert
            // Moves card to market (State Check)
            Assert.DoesNotContain(sourceCard, _player.PlayedCards, "Source card should be moved out of played cards.");
            // Replaces target
            _marketManager.Received(1).ReplaceCard(targetCard, sourceCard);
            // Clears state
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void MarketDevour_Structure_StandardRemove()
        {
             // Arrange
            var sourceCard = new Card("corruptor", "Market Corruptor", 3, CardAspect.Sorcery, 1, 1, 0);
            sourceCard.AddEffect(new CardEffect(EffectType.Devour, 1) 
            { 
                TargetLocation = CardLocation.Market,
                ReplaceWithSource = false 
            });

            var targetCard = new Card("m1", "Market Victim", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Market };

            _actionSystem.StartTargeting(ActionState.TargetingDevourMarket, sourceCard);

            // Act
            // Act
            var cmd = _actionSystem.HandleDevourMarketSelection(targetCard);
            var state = Substitute.For<IGameplayState>();
            state.MatchManager.Returns(_matchManager);
            state.ActionSystem.Returns(_actionSystem);
            Assert.IsNotNull(cmd);
            cmd.Execute(state);

            // Assert
            // Standard removal
            _marketManager.Received(1).RemoveCard(targetCard);
            _marketManager.DidNotReceive().ReplaceCard(Arg.Any<Card>(), Arg.Any<Card>());
        }

        #endregion

        #region FEATURE: Hand Devour (Wight / Standard)

        [TestMethod]
        public void HandDevour_ValidSelection_CallsDevourCard()
        {
             // Arrange
             var handCard = new Card("h1", "Hand Victim", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Hand };
             _player.Hand.Add(handCard);

             // Act
            // Directly check ActionSystem's handling of specific devours if exposed, 
            // OR use the flow via TryStartDevourHand
            
            // Setup a pending source
            var sourceCard = new Card("src", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            _actionSystem.TryStartDevourHand(sourceCard);

            // Simulate selection
            var cmd = _actionSystem.HandleDevourSelection(handCard);
            // Confirm/Execute
            var state = Substitute.For<IGameplayState>();
            state.MatchManager.Returns(_matchManager);
            state.ActionSystem.Returns(_actionSystem); // For CompleteAction
            
            Assert.IsNotNull(cmd);
            cmd.Execute(state);

            // Assert
            // Used real MatchManager, so check VoidPile and Hand
            Assert.Contains(handCard, _context.VoidPile, "Hand card should be moved to Void.");
            Assert.DoesNotContain(handCard, _player.Hand, "Hand card should be removed from Hand.");
        }

        #endregion

        #region FEATURE: Optional Popup Integration

        [TestMethod]
        public void OptionalPopup_Shown_WhenValid()
        {
             var card = new Card("opt", "Optional", 3, CardAspect.Sorcery, 1, 2, 0);
             var devourEffect = new CardEffect(EffectType.Devour, 1)
             {
                 TargetLocation = CardLocation.Market,
                 IsOptional = true,
                 OnSuccess = new CardEffect(EffectType.GainResource, 1)
             };
             card.Effects.Add(devourEffect);

            // Setup valid market
            var marketCard = new Card("m1", "MarketCard", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Market };
            _marketManager.MarketRow.Returns(new List<Card> { marketCard });

             // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, _logger);

            // Assert
            _uiMediator.Received(1).RequestOptionalEffect(card, devourEffect, Arg.Any<System.Action>(), Arg.Any<System.Action>());
        }

        [TestMethod]
        public void OptionalPopup_Skipped_WhenInvalid_WightFix()
        {
             // Scenario: Card has Devour -> Supplant (or similar).
             // Supplant target is missing.
             // Should NOT show popup.

             var card = new Card("wight", "Wight", 3, CardAspect.Shadow, 1, 2, 0);
             var devourEffect = new CardEffect(EffectType.Devour, 1)
             {
                 TargetLocation = CardLocation.Hand,
                 IsOptional = true,
                 OnSuccess = new CardEffect(EffectType.Supplant, 1) // Dependent effect
             };
             card.Effects.Add(devourEffect);

             // Arrange: Player has hand cards (so base Devour is valid)
             _player.Hand.Add(new Card("h1", "Hand", 0, CardAspect.Neutral, 0, 0, 0));

             // Arrange: Map has NO valid targets for Supplant
             _mapManager.HasValidAssassinationTarget(_player).Returns(false); // Supplant checks this

             // Act
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus: false, _logger);

            // Assert
            // The Deep Lookahead should kick in and prevent the popup
            _uiMediator.DidNotReceive().RequestOptionalEffect(card, devourEffect, Arg.Any<System.Action>(), Arg.Any<System.Action>());
        }

        #endregion

        #region FEATURE: Edge Cases

        [TestMethod]
        public void TryStartDevourMarket_EmptyMarket_DoesNotEnterTargeting()
        {
            // Arrange
            _marketManager.MarketRow.Returns(new List<Card>()); // Empty Market

            var card = new Card("test", "Test", 0, CardAspect.Neutral, 0, 0, 0);

            // Act
            _actionSystem.TryStartDevourMarket(card);

            // Assert
            Assert.AreNotEqual(ActionState.TargetingDevourMarket, _actionSystem.CurrentState, "Should not enter targeting if market is empty.");
        }

        #endregion
    }
}
