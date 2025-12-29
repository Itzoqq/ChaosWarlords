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
            _player = new PlayerBuilder().WithColor(PlayerColor.Red).Build();
        }

        [DataTestMethod]
        [DataRow(5, 5)]
        [DataRow(10, 10)]
        [DataRow(0, 0)]
        public void AddPower_IncreasesPlayerPower(int amount, int expected)
        {
            _manager.AddPower(_player, amount);
            Assert.AreEqual(expected, _player.Power);
        }

        [TestMethod]
        public void AddPower_IgnoresNegativeAmount()
        {
            _player.Power = 10;
            _manager.AddPower(_player, -5);
            Assert.AreEqual(10, _player.Power);
        }

        [DataTestMethod]
        [DataRow(10, 4, true, 6)]
        [DataRow(3, 5, false, 3)]
        [DataRow(5, 5, true, 0)]
        public void TrySpendPower_HandlesVariousScenarios(
            int initialPower,
            int spendAmount,
            bool expectedSuccess,
            int expectedFinal)
        {
            _player.Power = initialPower;
            bool success = _manager.TrySpendPower(_player, spendAmount);
            Assert.AreEqual(expectedSuccess, success);
            Assert.AreEqual(expectedFinal, _player.Power);
        }

        [DataTestMethod]
        [DataRow(5, 5)]
        [DataRow(10, 10)]
        public void AddInfluence_IncreasesPlayerInfluence(int amount, int expected)
        {
            _manager.AddInfluence(_player, amount);
            Assert.AreEqual(expected, _player.Influence);
        }

        [DataTestMethod]
        [DataRow(10, 4, true, 6)]
        [DataRow(3, 5, false, 3)]
        public void TrySpendInfluence_HandlesVariousScenarios(
            int initialInfluence,
            int spendAmount,
            bool expectedSuccess,
            int expectedFinal)
        {
            _player.Influence = initialInfluence;
            bool success = _manager.TrySpendInfluence(_player, spendAmount);
            Assert.AreEqual(expectedSuccess, success);
            Assert.AreEqual(expectedFinal, _player.Influence);
        }

        [DataTestMethod]
        [DataRow(10, 10)]
        [DataRow(5, 5)]
        public void AddVictoryPoints_IncreasesVictoryPoints(int amount, int expected)
        {
            _manager.AddVictoryPoints(_player, amount);
            Assert.AreEqual(expected, _player.VictoryPoints);
        }

        [DataTestMethod]
        [DataRow(3)]
        [DataRow(5)]
        [DataRow(1)]
        public void AddTroops_IncreasesTroops(int amount)
        {
            int initial = _player.TroopsInBarracks;
            _manager.AddTroops(_player, amount);
            Assert.AreEqual(initial + amount, _player.TroopsInBarracks);
        }

        [DataTestMethod]
        [DataRow(10, 3, 7)]
        [DataRow(2, 5, 0)]  // Boundary: doesn't go below zero
        [DataRow(5, 5, 0)]
        public void RemoveTroops_DecreasesTroops(int initial, int removeAmount, int expected)
        {
            _player.TroopsInBarracks = initial;
            _manager.RemoveTroops(_player, removeAmount);
            Assert.AreEqual(expected, _player.TroopsInBarracks);
        }

        [DataTestMethod]
        [DataRow(2)]
        [DataRow(3)]
        public void AddSpies_IncreasesSpies(int amount)
        {
            int initial = _player.SpiesInBarracks;
            _manager.AddSpies(_player, amount);
            Assert.AreEqual(initial + amount, _player.SpiesInBarracks);
        }

        [DataTestMethod]
        [DataRow(5, 2, 3)]
        [DataRow(3, 3, 0)]
        public void RemoveSpies_DecreasesSpies(int initial, int removeAmount, int expected)
        {
            _player.SpiesInBarracks = initial;
            _manager.RemoveSpies(_player, removeAmount);
            Assert.AreEqual(expected, _player.SpiesInBarracks);
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
            var card = new CardBuilder().WithName("c1").WithAspect(CardAspect.Neutral).Build();
            _player.DeckManager.AddToTop(card);

            _manager.DrawCards(_player, 1, mockRandom);

            Assert.HasCount(1, _player.Hand);
            Assert.AreEqual(card, _player.Hand[0]);
        }

        [TestMethod]
        public void PlayCard_MovesCardFromHandToPlayed()
        {
            var card = new CardBuilder().WithName("c1").WithAspect(CardAspect.Neutral).Build();
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
            var card = new CardBuilder().WithName("c1").WithAspect(CardAspect.Neutral).Build();
            _manager.AcquireCard(_player, card);

            Assert.Contains(card, _player.DiscardPile);
        }

        [TestMethod]
        public void TryPromoteCard_DelegatesToPlayer()
        {
            var card = new CardBuilder().WithName("c1").WithAspect(CardAspect.Neutral).Build();
            _player.Hand.Add(card);

            bool success = _manager.TryPromoteCard(_player, card, out string error);

            Assert.IsTrue(success);
            Assert.Contains(card, _player.InnerCircle);
            Assert.AreEqual(CardLocation.InnerCircle, card.Location);
        }

        [TestMethod]
        public void DevourCard_RemovesFromHandAndSetsLocationToVoid()
        {
            var card = new CardBuilder().WithName("c1").WithAspect(CardAspect.Neutral).Build();
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
            var card = new CardBuilder().WithName("c1").WithAspect(CardAspect.Neutral).Build();
            _player.Hand.Add(card);

            _manager.CleanUpTurn(_player);

            Assert.AreEqual(0, _player.Power);
            Assert.AreEqual(0, _player.Influence);
            Assert.IsEmpty(_player.Hand);
            Assert.Contains(card, _player.DiscardPile);
        }
    }
}
