using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Systems
{
    [TestClass]
    public class TurnManagerTests
    {
        private Player _playerRed = null!;
        private Player _playerBlue = null!;

        // Use concrete class to access internal state easily if needed, 
        // though strictly speaking we are accessing CurrentTurnContext which is on the interface too.
        private TurnManager _turnManager = null!;

        private Card _shadowCard = null!;
        private Card _sorceryCard = null!;
        private Card _neutralCard = null!;

        [TestInitialize]
        public void Setup()
        {
            _playerRed = new Player(PlayerColor.Red);
            _playerBlue = new Player(PlayerColor.Blue);

            _turnManager = new TurnManager(new List<Player> { _playerRed, _playerBlue });

            _shadowCard = new Card("shadow", "Shadow Card", 1, CardAspect.Shadow, 1, 1, 0);
            _sorceryCard = new Card("sorcery", "Sorcery Card", 1, CardAspect.Sorcery, 1, 1, 0);
            _neutralCard = new Card("neutral", "Neutral Card", 0, CardAspect.Neutral, 0, 0, 0);
        }

        [TestMethod]
        public void Constructor_InitializesCorrectly()
        {
            Assert.HasCount(2, _turnManager.Players);
            CollectionAssert.Contains(_turnManager.Players, _playerRed);
            CollectionAssert.Contains(_turnManager.Players, _playerBlue);
            
            // ActivePlayer should be the first one in the randomized list
            Assert.AreEqual(_turnManager.Players[0], _turnManager.ActivePlayer);

            // Access PlayedAspectCounts via CurrentTurnContext
            Assert.IsNotNull(_turnManager.CurrentTurnContext.PlayedAspectCounts);
            Assert.IsEmpty(_turnManager.CurrentTurnContext.PlayedAspectCounts);
        }

        [TestMethod]
        public void EndTurn_SwitchesToNextPlayer()
        {
            var initialPlayer = _turnManager.ActivePlayer;
            
            _turnManager.EndTurn();
            Assert.AreNotEqual(initialPlayer, _turnManager.ActivePlayer);

            _turnManager.EndTurn();
            Assert.AreEqual(initialPlayer, _turnManager.ActivePlayer);
        }

        [TestMethod]
        public void EndTurn_ResetsPlayedAspectCounts()
        {
            _turnManager.PlayCard(_shadowCard);
            Assert.IsTrue(_turnManager.CurrentTurnContext.PlayedAspectCounts.ContainsKey(CardAspect.Shadow));

            _turnManager.EndTurn();

            // When turn ends, a NEW context is created, so it should be empty
            Assert.IsEmpty(_turnManager.CurrentTurnContext.PlayedAspectCounts);
        }

        [TestMethod]
        public void PlayCard_AddsNewAspect()
        {
            _turnManager.PlayCard(_shadowCard);

            Assert.HasCount(1, _turnManager.CurrentTurnContext.PlayedAspectCounts);
            Assert.AreEqual(1, _turnManager.CurrentTurnContext.PlayedAspectCounts[CardAspect.Shadow]);
        }

        [TestMethod]
        public void PlayCard_IncrementsExistingAspect()
        {
            _turnManager.PlayCard(_shadowCard);

            var secondShadowCard = new Card("shadow_2", "Shadow Card", 1, CardAspect.Shadow, 1, 1, 0);
            _turnManager.PlayCard(secondShadowCard);

            Assert.HasCount(1, _turnManager.CurrentTurnContext.PlayedAspectCounts);
            Assert.AreEqual(2, _turnManager.CurrentTurnContext.PlayedAspectCounts[CardAspect.Shadow]);
        }

        [TestMethod]
        public void PlayCard_TracksMultipleAspects()
        {
            _turnManager.PlayCard(_shadowCard);
            _turnManager.PlayCard(_sorceryCard);
            _turnManager.PlayCard(_neutralCard);

            var secondSorceryCard = new Card("sorcery_2", "Sorcery Card", 1, CardAspect.Sorcery, 1, 1, 0);
            _turnManager.PlayCard(secondSorceryCard);

            Assert.HasCount(3, _turnManager.CurrentTurnContext.PlayedAspectCounts);
            Assert.AreEqual(1, _turnManager.CurrentTurnContext.PlayedAspectCounts[CardAspect.Shadow]);
            Assert.AreEqual(2, _turnManager.CurrentTurnContext.PlayedAspectCounts[CardAspect.Sorcery]);
            Assert.AreEqual(1, _turnManager.CurrentTurnContext.PlayedAspectCounts[CardAspect.Neutral]);
        }
    }
}


