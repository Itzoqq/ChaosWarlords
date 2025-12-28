using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Contexts
{
    [TestClass]
    public class TurnContextTests
    {
        private TurnContext _turnContext = null!;
        private Player _dummyPlayer = null!;
        private Card _cardA = null!;
        private Card _cardB = null!;

        [TestInitialize]
        public void Setup()
        {
            _dummyPlayer = new Player(PlayerColor.Red);
            _turnContext = new TurnContext(_dummyPlayer);

            _cardA = new Card("A", "Card A", 1, CardAspect.Warlord, 0, 0, 0);
            _cardB = new Card("B", "Card B", 1, CardAspect.Sorcery, 0, 0, 0);
        }

        [TestMethod]
        public void Constructor_StartsEmpty()
        {
            Assert.IsNotNull(_turnContext.PlayedAspectCounts);
            Assert.AreEqual(0, _turnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void AddPromotionCredit_IncreasesPendingCount()
        {
            _turnContext.AddPromotionCredit(_cardA, 1);
            Assert.AreEqual(1, _turnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void HasValidCreditFor_SelfPromotion_ReturnsFalse()
        {
            // Arrange: Card A provides the only credit
            _turnContext.AddPromotionCredit(_cardA, 1);

            // Act: Check if we can use this credit to promote Card A
            bool result = _turnContext.HasValidCreditFor(_cardA);

            // Assert
            Assert.IsFalse(result, "Should not allow promoting a card using its own credit.");
        }

        [TestMethod]
        public void HasValidCreditFor_CrossPromotion_ReturnsTrue()
        {
            // Arrange: Card A provides credit
            _turnContext.AddPromotionCredit(_cardA, 1);

            // Act: Check if we can use it for Card B
            bool result = _turnContext.HasValidCreditFor(_cardB);

            // Assert
            Assert.IsTrue(result, "Should allow promoting a different card.");
        }

        [TestMethod]
        public void ConsumeCreditFor_ConsumesCorrectCredit()
        {
            // Arrange: Both A and B provide credits
            _turnContext.AddPromotionCredit(_cardA, 1);
            _turnContext.AddPromotionCredit(_cardB, 1);

            Assert.AreEqual(2, _turnContext.PendingPromotionsCount);

            // Act: Consume credit for Card A (must use B's credit)
            _turnContext.ConsumeCreditFor(_cardA);

            // Assert
            Assert.AreEqual(1, _turnContext.PendingPromotionsCount);

            // The remaining credit must be A's (since B's was consumed).
            // Therefore, A cannot use the remaining credit.
            Assert.IsFalse(_turnContext.HasValidCreditFor(_cardA));

            // But B can use A's credit
            Assert.IsTrue(_turnContext.HasValidCreditFor(_cardB));
        }

        [TestMethod]
        public void RecordAction_AddsToHistory()
        {
            _turnContext.RecordAction("TestType", "Test Summary");

            Assert.HasCount(1, _turnContext.ActionHistory);
            Assert.AreEqual("TestType", _turnContext.ActionHistory[0].ActionType);
            Assert.AreEqual("Test Summary", _turnContext.ActionHistory[0].Summary);
        }

        [TestMethod]
        public void RecordAction_IncrementsSequence()
        {
            _turnContext.RecordAction("Action1", "Summary1");
            _turnContext.RecordAction("Action2", "Summary2");

            Assert.HasCount(2, _turnContext.ActionHistory);
            Assert.AreEqual(0, _turnContext.ActionHistory[0].Sequence);
            Assert.AreEqual(1, _turnContext.ActionHistory[1].Sequence);
        }

        [TestMethod]
        public void RecordAction_CapturesCorrectPlayerId()
        {
            _turnContext.RecordAction("Type", "Summary");

            Assert.AreEqual(_dummyPlayer.PlayerId, _turnContext.ActionHistory[0].PlayerId);
        }

        [TestMethod]
        public void RecordAction_StoresTimestamp()
        {
            _turnContext.RecordAction("Type", "Summary");

            // Timestamp should be recent
            var diff = System.DateTime.Now - _turnContext.ActionHistory[0].Timestamp;
            Assert.IsLessThan(5.0, diff.TotalSeconds);
        }
    }
}


