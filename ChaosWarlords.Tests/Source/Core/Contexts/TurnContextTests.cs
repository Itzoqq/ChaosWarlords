using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Tests.Contexts
{
    [TestClass]

    [TestCategory("Unit")]
    public class TurnContextTests
    {
        private TurnContext _turnContext = null!;
        private Player _dummyPlayer = null!;
        private Card _cardA = null!;
        private Card _cardB = null!;

        [TestInitialize]
        public void Setup()
        {
            _dummyPlayer = TestData.Players.RedPlayer();
            _turnContext = new TurnContext(_dummyPlayer, Utilities.TestLogger.Instance);

            _cardA = TestData.Cards.CheapCard();
            _cardB = TestData.Cards.ExpensiveCard();
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

        // --- CanDeclineRemainingPromotions ("up to N" vs. plain mandatory Promote credits) ---

        [TestMethod]
        public void CanDeclineRemainingPromotions_NoCreditsOutstanding_ReturnsTrue()
        {
            Assert.IsTrue(_turnContext.CanDeclineRemainingPromotions, "Vacuously true - nothing left to decline.");
        }

        [TestMethod]
        public void CanDeclineRemainingPromotions_DefaultsToMandatory_ReturnsFalse()
        {
            // AddPromotionCredit's isOptional defaults to false - a plain "promote a card
            // played this turn" (core_noble) must stay mandatory unless a card explicitly
            // opts into the "up to N" shape.
            _turnContext.AddPromotionCredit(_cardA, 1);

            Assert.IsFalse(_turnContext.CanDeclineRemainingPromotions);
        }

        [TestMethod]
        public void CanDeclineRemainingPromotions_AllCreditsOptional_ReturnsTrue()
        {
            // Cultist of Myrkul/Zuggtmoy's "promote up to 2 other cards played this turn".
            _turnContext.AddPromotionCredit(_cardA, 2, isOptional: true);

            Assert.IsTrue(_turnContext.CanDeclineRemainingPromotions);
        }

        [TestMethod]
        public void CanDeclineRemainingPromotions_OneMandatoryCreditAmongOptionalOnes_ReturnsFalse()
        {
            // A player who played BOTH core_noble (mandatory 1) and Cultist of Myrkul
            // (optional 2) the same turn must still resolve the mandatory one before being
            // allowed to stop.
            _turnContext.AddPromotionCredit(_cardA, 1, isOptional: false);
            _turnContext.AddPromotionCredit(_cardB, 2, isOptional: true);

            Assert.IsFalse(_turnContext.CanDeclineRemainingPromotions);
        }

        [TestMethod]
        public void CanDeclineRemainingPromotions_AfterTheMandatoryCreditIsConsumed_ReturnsTrue()
        {
            _turnContext.AddPromotionCredit(_cardA, 1, isOptional: false); // Added first - earliest in the credit list.
            _turnContext.AddPromotionCredit(_cardB, 2, isOptional: true);

            // ConsumeCreditFor picks the FIRST credit not sourced from its target - cardA's
            // mandatory credit (added first, and not sourced from cardB) is the one consumed.
            _turnContext.ConsumeCreditFor(_cardB);

            Assert.AreEqual(2, _turnContext.PendingPromotionsCount, "Setup check: 2 optional credits should remain.");
            Assert.IsTrue(_turnContext.CanDeclineRemainingPromotions, "Only optional credits remain now - declining should be allowed.");
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
            var diff = DateTime.Now - _turnContext.ActionHistory[0].Timestamp;
            Assert.IsLessThan(5.0, diff.TotalSeconds);
        }
    }
}


