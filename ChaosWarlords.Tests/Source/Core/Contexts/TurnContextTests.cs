using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;

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

        // --- Aspect-filtered credits (Air/Fire/Water Elemental Myrmidon: "promote an
        // Obedience card played this turn") ---

        [TestMethod]
        public void HasValidCreditFor_AspectFilteredCredit_MatchingAspect_ReturnsTrue()
        {
            var orderCard = new CardBuilder().WithName("order_card").WithAspect(CardAspect.Order).Build();
            _turnContext.AddPromotionCredit(_cardA, 1, requiredAspect: CardAspect.Order);

            Assert.IsTrue(_turnContext.HasValidCreditFor(orderCard));
        }

        [TestMethod]
        public void HasValidCreditFor_AspectFilteredCredit_MismatchedAspect_ReturnsFalse()
        {
            var shadowCard = new CardBuilder().WithName("shadow_card").WithAspect(CardAspect.Shadow).Build();
            _turnContext.AddPromotionCredit(_cardA, 1, requiredAspect: CardAspect.Order);

            Assert.IsFalse(_turnContext.HasValidCreditFor(shadowCard), "The credit is filtered to Order - a Shadow card must not be a valid target.");
        }

        [TestMethod]
        public void HasValidCreditFor_AspectFilteredCredit_StillExcludesItsOwnSource_EvenIfAspectMatches()
        {
            // The source card itself matches its own credit's aspect filter (it granted the
            // credit BY BEING played, its Aspect is whatever it is) - self-exclusion is a
            // SIBLING restriction, not superseded by a matching aspect.
            var orderSource = new CardBuilder().WithName("order_source").WithAspect(CardAspect.Order).Build();
            _turnContext.AddPromotionCredit(orderSource, 1, requiredAspect: CardAspect.Order);

            Assert.IsFalse(_turnContext.HasValidCreditFor(orderSource));
        }

        [TestMethod]
        public void ConsumeCreditFor_AspectFilteredCredit_SkipsItForAMismatchedTarget_FallsBackToAnUnfilteredOne()
        {
            // A player who played BOTH an Air Elemental Myrmidon (Order-filtered) and a
            // core_noble (unfiltered) the same turn - promoting a non-Order card must consume
            // the UNFILTERED credit, not the Order-filtered one (which can't apply to it).
            var shadowCard = new CardBuilder().WithName("shadow_target").WithAspect(CardAspect.Shadow).Build();
            _turnContext.AddPromotionCredit(_cardA, 1, requiredAspect: CardAspect.Order);
            _turnContext.AddPromotionCredit(_cardB, 1); // Unfiltered.

            _turnContext.ConsumeCreditFor(shadowCard);

            Assert.AreEqual(1, _turnContext.PendingPromotionsCount, "Exactly one credit should have been consumed.");
            // The Order-filtered credit must still be the one left outstanding - it's useless
            // against the Shadow card that was just promoted, but still valid against a future
            // Order card played later this turn.
            var laterOrderCard = new CardBuilder().WithName("later_order_card").WithAspect(CardAspect.Order).Build();
            Assert.IsTrue(_turnContext.HasValidCreditFor(laterOrderCard));
        }

        [TestMethod]
        public void ConsumeCreditFor_TargetMatchesBothAFilteredAndAnUnfilteredCredit_PrefersTheFilteredOne()
        {
            // Both credits can promote an Order card, but consuming the FILTERED one first
            // preserves the UNFILTERED one's flexibility for whatever's promoted next (it can
            // absorb ANY aspect, the filtered one can't) - reduces (without fully eliminating)
            // how often click order strands a sibling filtered credit. See
            // ForfeitUnsatisfiableCredits for the actual soft-lock safety net.
            var orderCard = new CardBuilder().WithName("order_target").WithAspect(CardAspect.Order).Build();
            var shadowCard = new CardBuilder().WithName("shadow_target").WithAspect(CardAspect.Shadow).Build();
            _turnContext.AddPromotionCredit(_cardA, 1); // Unfiltered - added FIRST, so a naive
                                                         // first-match search would wrongly pick
                                                         // this one for orderCard.
            _turnContext.AddPromotionCredit(_cardB, 1, requiredAspect: CardAspect.Order);

            _turnContext.ConsumeCreditFor(orderCard);

            // The unfiltered credit must be the one still outstanding - it's the only one that
            // can still promote the Shadow card left over.
            Assert.IsTrue(_turnContext.HasValidCreditFor(shadowCard), "The unfiltered credit should have been preserved for the Shadow card.");
            _turnContext.ConsumeCreditFor(shadowCard);
            Assert.AreEqual(0, _turnContext.PendingPromotionsCount, "Both credits should now be fully resolved.");
        }

        // --- ForfeitUnsatisfiableCredits (soft-lock safety net for aspect-filtered credits) ---

        [TestMethod]
        public void ForfeitUnsatisfiableCredits_CreditWithNoMatchingPlayedCard_IsForfeited()
        {
            // Dead on arrival: an Order-filtered credit when zero Order cards were played at all.
            var shadowCard = new CardBuilder().WithName("shadow_card").WithAspect(CardAspect.Shadow).Build();
            _turnContext.AddPromotionCredit(_cardA, 1, requiredAspect: CardAspect.Order);

            int forfeited = _turnContext.ForfeitUnsatisfiableCredits(new[] { _cardA, shadowCard });

            Assert.AreEqual(1, forfeited);
            Assert.AreEqual(0, _turnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void ForfeitUnsatisfiableCredits_CreditWithAMatchingPlayedCard_IsNotForfeited()
        {
            var orderCard = new CardBuilder().WithName("order_card").WithAspect(CardAspect.Order).Build();
            _turnContext.AddPromotionCredit(_cardA, 1, requiredAspect: CardAspect.Order);

            int forfeited = _turnContext.ForfeitUnsatisfiableCredits(new[] { _cardA, orderCard });

            Assert.AreEqual(0, forfeited);
            Assert.AreEqual(1, _turnContext.PendingPromotionsCount);
        }

        [TestMethod]
        public void ForfeitUnsatisfiableCredits_TwoFilteredMandatoryCreditsSharingOneMatchingCard_StrandedSiblingIsForfeitedNotSoftLocked()
        {
            // THE core regression this exists to prevent: Air Elemental Myrmidon + Fire
            // Elemental Myrmidon (both mandatory, both Order-filtered) played the same turn,
            // with only ONE Order card also played. Consuming the shared card for one credit
            // must not leave the other one stuck forever with zero possible targets.
            var orderCard = new CardBuilder().WithName("order_card").WithAspect(CardAspect.Order).Build();
            var airMyrmidon = new CardBuilder().WithName("air_myrmidon").WithAspect(CardAspect.Shadow).Build();
            var fireMyrmidon = new CardBuilder().WithName("fire_myrmidon").WithAspect(CardAspect.Sorcery).Build();
            _turnContext.AddPromotionCredit(airMyrmidon, 1, requiredAspect: CardAspect.Order);
            _turnContext.AddPromotionCredit(fireMyrmidon, 1, requiredAspect: CardAspect.Order);

            Assert.IsTrue(_turnContext.HasValidCreditFor(orderCard), "Setup check: orderCard is a legal target for at least one credit.");
            _turnContext.ConsumeCreditFor(orderCard);
            Assert.AreEqual(1, _turnContext.PendingPromotionsCount, "One credit consumed, one left - now stranded with zero remaining legal targets.");

            // orderCard has now actually been promoted (moved out of Played, in real play) - the
            // remaining played-cards pool passed to the real PromoteCommand-driven flow would no
            // longer include it either.
            var playedCardsAfterPromoting = new[] { airMyrmidon, fireMyrmidon };
            int forfeited = _turnContext.ForfeitUnsatisfiableCredits(playedCardsAfterPromoting);

            Assert.AreEqual(1, forfeited, "The stranded sibling credit must be forfeited, not left mandatory-and-unsatisfiable forever.");
            Assert.AreEqual(0, _turnContext.PendingPromotionsCount);
            Assert.IsTrue(_turnContext.CanDeclineRemainingPromotions, "With nothing outstanding, the redemption session must be cleanly over.");
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

        // --- Promotion completion effects (Blue Dragon: "...then gain 1 VP for every 3 cards
        // in your inner circle") ---

        [TestMethod]
        public void DrainPromotionCompletionEffects_NothingRegistered_ReturnsEmpty()
        {
            Assert.IsEmpty(_turnContext.DrainPromotionCompletionEffects());
        }

        [TestMethod]
        public void RegisterPromotionCompletionEffect_ThenDrain_ReturnsItExactlyOnce()
        {
            var completionEffect = new CardEffect(EffectType.GainResource, 1, ResourceType.VictoryPoints);

            _turnContext.RegisterPromotionCompletionEffect(_cardA, completionEffect);

            var drained = _turnContext.DrainPromotionCompletionEffects();
            Assert.HasCount(1, drained);
            Assert.AreSame(_cardA, drained[0].Source);
            Assert.AreSame(completionEffect, drained[0].Effect);
        }

        [TestMethod]
        public void DrainPromotionCompletionEffects_CalledTwiceAfterOneRegistration_SecondCallReturnsEmpty()
        {
            var completionEffect = new CardEffect(EffectType.GainResource, 1, ResourceType.VictoryPoints);
            _turnContext.RegisterPromotionCompletionEffect(_cardA, completionEffect);

            _turnContext.DrainPromotionCompletionEffects();
            var secondDrain = _turnContext.DrainPromotionCompletionEffects();

            Assert.IsEmpty(secondDrain, "A completion effect must only ever be applied once - draining must clear the registration.");
        }

        [TestMethod]
        public void RegisterPromotionCompletionEffect_TwoDistinctSources_BothDrainIndependently()
        {
            var effectA = new CardEffect(EffectType.GainResource, 1, ResourceType.VictoryPoints);
            var effectB = new CardEffect(EffectType.GainResource, 2, ResourceType.Influence);

            _turnContext.RegisterPromotionCompletionEffect(_cardA, effectA);
            _turnContext.RegisterPromotionCompletionEffect(_cardB, effectB);

            var drained = _turnContext.DrainPromotionCompletionEffects();
            Assert.HasCount(2, drained, "2 distinct sources (e.g. 2 physical Blue Dragon copies played the same turn) must each register and drain independently.");
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


