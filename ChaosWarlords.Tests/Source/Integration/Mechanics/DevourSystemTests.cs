using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Mechanics.Actions; // ActionSystem
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
using System.Linq;

namespace ChaosWarlords.Tests.Source.Systems
{
    [TestClass]
    [TestCategory("Integration")]
    public class DevourSystemTests
    {
        private ActionSystem _actionSystem = null!;
        private MatchContext _context = null!;
        private Player _player = null!;
        private IMapManager _mapManager = null!;
        private IMarketManager _marketManager = null!;
        private IMatchManager _matchManager = null!; // Need this for DevourCard? Or use Mock behavior.
        private IMarketStateManager _marketStateManager = null!;
        
        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
            
            _player = new Player(PlayerColor.Red);
            var turnSub = Substitute.For<ITurnManager>();
            turnSub.ActivePlayer.Returns(_player);

            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            
            // We need to mock MatchManager because ActionSystem calls it for execution
            _matchManager = Substitute.For<IMatchManager>();
            _marketStateManager = Substitute.For<IMarketStateManager>();

            var logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;
            
            _actionSystem = new ActionSystem(turnSub, _mapManager, logger);
            _actionSystem.SetMatchManager(_matchManager); // Inject dependency
            _actionSystem.SetMarketManager(_marketManager); // Inject dependency
            _actionSystem.SetMarketStateManager(_marketStateManager); // Inject dependency

            _context = new MatchContext(
                turnSub, 
                _mapManager, 
                _marketManager, 
                _actionSystem, 
                Substitute.For<ICardDatabase>(), 
                Substitute.For<IPlayerStateManager>(), 
                null, 
                logger, 
                12345
            );
        }

        [TestMethod]
        public void TryStartDevourMarket_WithCards_StartsTargeting()
        {
            // Arrange
            var sourceCard = new Card("src", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            
            // Mock Market having cards
            // Mock Market having cards
            var marketCards = new List<Card>
            {
                new Card("m1", "MarketCard", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Market }
            };
            _marketManager.MarketRow.Returns(marketCards);

            // Act
            _actionSystem.TryStartDevourMarket(sourceCard);

            // Assert
            Assert.AreEqual(ActionState.TargetingDevourMarket, _actionSystem.CurrentState);
            _marketStateManager.Received(1).OpenForDevour(Arg.Any<Action<Card>>());
        }

        [TestMethod]
        public void HandleDevourMarketSelection_ValidCard_CallsDevourCard()
        {
            // Arrange
            var marketCard = new Card("m1", "MarketCard", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Market };
            
            // Ensure we are in the correct state (TargetingDevourMarket)
            // Otherwise ActionSystem might ignore the selection or consider it invalid
            _actionSystem.StartTargeting(ActionState.TargetingDevourMarket);

            // Act
            _actionSystem.HandleDevourMarketSelection(marketCard);

            // Assert
            // The system handles market removal manually because MatchManager.DevourCard typically assumes Hand cards
            _marketManager.Received(1).RemoveCard(marketCard);
            Assert.AreEqual(CardLocation.Void, marketCard.Location);
            
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState); // Should clear state after complete
        }

        [TestMethod]
        public void HandleDevourMarketSelection_InvalidLocation_DoesNotDevour()
        {
            // Arrange
            var handCard = new Card("h1", "HandCard", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Hand };
            
            // Act
            _actionSystem.HandleDevourMarketSelection(handCard);

            // Assert
            _matchManager.DidNotReceive().DevourCard(handCard);
        }
        [TestMethod]
        public void TryStartDevourDeck_WithCards_DevoursTopCard()
        {
            // Arrange
            var sourceCard = new Card("src", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            var deckCard = new Card("d1", "DeckCard", 0, CardAspect.Neutral, 0, 0, 0) { Location = CardLocation.Deck };
            
            _player.DeckManager.AddToTop(deckCard);
            
            // Act
            _actionSystem.TryStartDevourDeck(sourceCard);

            // Assert
#pragma warning disable MSTEST0037
            Assert.AreEqual(0, _player.Deck.Count, "Deck should be empty.");
#pragma warning restore MSTEST0037
            _matchManager.Received(1).DevourCard(deckCard);
        }

        [TestMethod]
        public void TryStartDevourDeck_EmptyDeck_DoesNothing()
        {
            // Arrange
            var sourceCard = new Card("src", "Source", 0, CardAspect.Neutral, 0, 0, 0);
            // Deck is empty by default on new Player
            
            // Act
            _actionSystem.TryStartDevourDeck(sourceCard);

            // Assert
            _matchManager.DidNotReceive().DevourCard(Arg.Any<Card>());
        }
    }
}
