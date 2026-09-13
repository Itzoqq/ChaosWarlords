using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Functional
{
    [TestClass]
    [TestCategory("Integration")]
    public class FixedRecruitPilesScenarioTests
    {
        [TestMethod]
        public void BuyHouseGuard_FromFixedPile_AcquiresItAndRevealsTheNextCopy()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var pile = scenario.Context.MarketManager.FixedRecruitPiles!.Single(p => p.DefinitionId == "core_house_guard");
            var purchasedCard = pile.AvailableCard!;
            red.AddInfluence(purchasedCard.Cost);

            scenario.Dispatch(new BuyCardCommand(purchasedCard));

            Assert.HasCount(14, pile.Cards);
            Assert.DoesNotContain(purchasedCard, pile.Cards);
            Assert.Contains(purchasedCard, red.DiscardPile);
            Assert.IsNotNull(pile.AvailableCard);
            Assert.AreEqual("core_house_guard", pile.AvailableCard.DefinitionId);
            Assert.AreEqual(0, red.Influence);
        }

        [TestMethod]
        public void BuyFixedPileCard_WithoutEnoughInfluence_IsRejectedWithoutConsumingSupply()
        {
            var scenario = MatchScenario.Build();
            scenario.AsActivePlayer(PlayerColor.Red);
            var pile = scenario.Context.MarketManager.FixedRecruitPiles!.Single(p => p.DefinitionId == "core_priestess");
            var card = pile.AvailableCard!;

            scenario.AssertRejected(new BuyCardCommand(card));

            Assert.HasCount(15, pile.Cards);
            Assert.AreSame(card, pile.AvailableCard);
        }

        [TestMethod]
        public void BuyFixedPileCard_DispatchedTwice_SecondDispatchIsRejectedAndDoesNotConsumeAnotherCopy()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var pile = scenario.Context.MarketManager.FixedRecruitPiles!.Single(p => p.DefinitionId == "core_house_guard");
            var card = pile.AvailableCard!;
            red.AddInfluence(card.Cost * 2);

            scenario.DispatchTwice(new BuyCardCommand(card));

            Assert.HasCount(14, pile.Cards);
            Assert.AreEqual(card.Cost, red.Influence, "The stale command must not spend Influence a second time.");
        }

        [TestMethod]
        public void BuyFixedPileCard_WhenPileIsEmpty_IsRejected()
        {
            var scenario = MatchScenario.Build();
            var red = scenario.AsActivePlayer(PlayerColor.Red);
            var pile = scenario.Context.MarketManager.FixedRecruitPiles!.Single(p => p.DefinitionId == "core_priestess");
            var exhaustedCard = pile.AvailableCard!;
            pile.Cards.Clear();
            red.AddInfluence(exhaustedCard.Cost);

            scenario.AssertRejected(new BuyCardCommand(exhaustedCard));

            Assert.IsNull(pile.AvailableCard);
            Assert.AreEqual(exhaustedCard.Cost, red.Influence);
        }
    }
}
