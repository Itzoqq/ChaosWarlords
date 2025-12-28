using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;

namespace ChaosWarlords.Tests.Managers
{
    [TestClass]
    public class PlayerStateManagerTests
    {
        private PlayerStateManager _manager = null!;
        private Player _player = null!;

        [TestInitialize]
        public void Setup()
        {
            _manager = new PlayerStateManager();
            _player = new Player(PlayerColor.Red);
        }

        [TestMethod]
        public void AddPower_IncreasesPlayerPower()
        {
            _manager.AddPower(_player, 5);
            Assert.AreEqual(5, _player.Power);
        }

        [TestMethod]
        public void AddPower_IgnoresNegativeAmount()
        {
            _player.Power = 10;
            _manager.AddPower(_player, -5);
            Assert.AreEqual(10, _player.Power);
        }

        [TestMethod]
        public void TrySpendPower_DecreasesPowerIfSufficient()
        {
            _player.Power = 10;
            bool success = _manager.TrySpendPower(_player, 4);
            Assert.IsTrue(success);
            Assert.AreEqual(6, _player.Power);
        }

        [TestMethod]
        public void TrySpendPower_ReturnsFalseIfInsufficient()
        {
            _player.Power = 3;
            bool success = _manager.TrySpendPower(_player, 5);
            Assert.IsFalse(success);
            Assert.AreEqual(3, _player.Power);
        }

        [TestMethod]
        public void AddInfluence_IncreasesPlayerInfluence()
        {
            _manager.AddInfluence(_player, 5);
            Assert.AreEqual(5, _player.Influence);
        }

        [TestMethod]
        public void TrySpendInfluence_DecreasesInfluenceIfSufficient()
        {
            _player.Influence = 10;
            bool success = _manager.TrySpendInfluence(_player, 4);
            Assert.IsTrue(success);
            Assert.AreEqual(6, _player.Influence);
        }

        [TestMethod]
        public void AddVictoryPoints_IncreasesVictoryPoints()
        {
            _manager.AddVictoryPoints(_player, 10);
            Assert.AreEqual(10, _player.VictoryPoints);
        }

        [TestMethod]
        public void AddTroops_IncreasesTroops()
        {
            int initial = _player.TroopsInBarracks;
            _manager.AddTroops(_player, 3);
            Assert.AreEqual(initial + 3, _player.TroopsInBarracks);
        }

        [TestMethod]
        public void RemoveTroops_DecreasesTroops()
        {
            _player.TroopsInBarracks = 10;
            _manager.RemoveTroops(_player, 3);
            Assert.AreEqual(7, _player.TroopsInBarracks);
        }

        [TestMethod]
        public void RemoveTroops_DoesNotGoBelowZero()
        {
            _player.TroopsInBarracks = 2;
            _manager.RemoveTroops(_player, 5);
            Assert.AreEqual(0, _player.TroopsInBarracks);
        }

        [TestMethod]
        public void AddSpies_IncreasesSpies()
        {
            int initial = _player.SpiesInBarracks;
            _manager.AddSpies(_player, 2);
            Assert.AreEqual(initial + 2, _player.SpiesInBarracks);
        }

        [TestMethod]
        public void RemoveSpies_DecreasesSpies()
        {
            _player.SpiesInBarracks = 5;
            _manager.RemoveSpies(_player, 2);
            Assert.AreEqual(3, _player.SpiesInBarracks);
        }

        [TestMethod]
        public void AddTrophy_IncreasesTrophyHall()
        {
            Assert.AreEqual(0, _player.TrophyHall);
            _manager.AddTrophy(_player);
            Assert.AreEqual(1, _player.TrophyHall);
        }

        [TestMethod]
        public void DrawCards_DelegatesToPlayer()
        {
            var mockRandom = Substitute.For<IGameRandom>();
            var card = new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            _player.DeckManager.AddToTop(card);

            _manager.DrawCards(_player, 1, mockRandom);

            Assert.HasCount(1, _player.Hand);
            Assert.AreEqual(card, _player.Hand[0]);
        }

        [TestMethod]
        public void PlayCard_MovesCardFromHandToPlayed()
        {
            var card = new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            _player.Hand.Add(card);
            card.Location = CardLocation.Hand;

            _manager.PlayCard(_player, card);

            Assert.DoesNotContain(card, _player.Hand);
            Assert.Contains(card, _player.PlayedCards);
            Assert.AreEqual(CardLocation.Played, card.Location);
        }

        [TestMethod]
        public void AcquireCard_AddsToDiscard()
        {
            var card = new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            _manager.AcquireCard(_player, card);

            Assert.Contains(card, _player.DiscardPile);
        }

        [TestMethod]
        public void TryPromoteCard_DelegatesToPlayer()
        {
            var card = new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            _player.Hand.Add(card);

            bool success = _manager.TryPromoteCard(_player, card, out string error);

            Assert.IsTrue(success);
            Assert.Contains(card, _player.InnerCircle);
            Assert.AreEqual(CardLocation.InnerCircle, card.Location);
        }

        [TestMethod]
        public void DevourCard_RemovesFromHandAndSetsLocationToVoid()
        {
            var card = new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            _player.Hand.Add(card);
            card.Location = CardLocation.Hand;

            _manager.DevourCard(_player, card);

            Assert.DoesNotContain(card, _player.Hand);
            Assert.AreEqual(CardLocation.Void, card.Location);
        }

        [TestMethod]
        public void CleanUpTurn_ResetsResourcesAndDiscardsCards()
        {
            _player.Power = 10;
            _player.Influence = 5;
            var card = new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0, 0);
            _player.Hand.Add(card);

            _manager.CleanUpTurn(_player);

            Assert.AreEqual(0, _player.Power);
            Assert.AreEqual(0, _player.Influence);
            Assert.IsEmpty(_player.Hand);
            Assert.Contains(card, _player.DiscardPile);
        }
    }
}
