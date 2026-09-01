using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;

namespace ChaosWarlords.Tests.Mechanics.Commands
{
    // Unit-level Validate()/Execute() coverage for PlayFromMarketCommand - added as TIER 1
    // item 2 (planning.txt, test-hardening audit, 2026-09-01). See DiscardCardCommandTests.cs's
    // own doc comment for why this file exists now and not when the command was first built.
    [TestClass]
    [TestCategory("Unit")]
    public class PlayFromMarketCommandTests
    {
        private TestGameplayState _state = null!;
        private Card _marketCard = null!;
        private Card _sourceCard = null!; // Ulitharid-shaped: PlayFromMarket effect, maxCost 4.

        [TestInitialize]
        public void Setup()
        {
            _state = new TestGameplayState();

            _marketCard = new Card("core_house_guard", "House Guard", 3, CardAspect.Warlord, 1, 2, 0);
            _state.MarketManager.MarketRow.Returns(new List<Card> { _marketCard });

            _sourceCard = new Card("ulitharid", "Ulitharid", 6, CardAspect.Oblivion, 3, 6, 0);
            _sourceCard.AddEffect(new CardEffect(EffectType.PlayFromMarket, 4));
            _state.ActionSystem.PendingCard.Returns(_sourceCard);
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenMarketCardNotFound()
        {
            _state.MarketManager.MarketRow.Returns(new List<Card>());
            var command = new PlayFromMarketCommand(_marketCard, _sourceCard);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenNoPendingCard()
        {
            _state.ActionSystem.PendingCard.Returns((Card?)null);
            var command = new PlayFromMarketCommand(_marketCard, _sourceCard);

            Assert.IsFalse(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Validate_ReturnsFalse_WhenMarketCardCostExceedsTheLimit()
        {
            var expensiveCard = new Card("neogi", "Neogi", 7, CardAspect.Warlord, 4, 8, 0); // Cost 7 > 4.
            _state.MarketManager.MarketRow.Returns(new List<Card> { expensiveCard });
            var command = new PlayFromMarketCommand(expensiveCard, _sourceCard);

            Assert.IsFalse(command.Validate(_state.MatchContext), "Server-side re-check must reject an over-cost selection even if a client's own filter should have caught it.");
        }

        [TestMethod]
        public void Validate_ReturnsTrue_WhenMarketCardCostIsExactlyTheLimit()
        {
            var boundaryCard = new Card("boundary", "Boundary", 4, CardAspect.Neutral, 0, 0, 0); // Cost == max (4).
            _state.MarketManager.MarketRow.Returns(new List<Card> { boundaryCard });
            var command = new PlayFromMarketCommand(boundaryCard, _sourceCard);

            Assert.IsTrue(command.Validate(_state.MatchContext));
        }

        [TestMethod]
        public void Execute_WhenMarketCardNotFound_DoesNothing()
        {
            _state.MarketManager.MarketRow.Returns(new List<Card>());
            var command = new PlayFromMarketCommand(_marketCard, _sourceCard);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.DidNotReceive().CompleteAction();
            _state.MatchManager.DidNotReceive().PlayCardFromMarket(Arg.Any<Card>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void Execute_WhenNoPendingCard_DoesNothing()
        {
            _state.ActionSystem.PendingCard.Returns((Card?)null);
            var command = new PlayFromMarketCommand(_marketCard, _sourceCard);

            command.Execute(_state.MatchContext);

            _state.ActionSystem.DidNotReceive().CompleteAction();
            _state.MatchManager.DidNotReceive().PlayCardFromMarket(Arg.Any<Card>(), Arg.Any<Card>());
        }

        [TestMethod]
        public void Execute_WhenValid_CompletesThisSelectionFirst_ThenStartsTheMarketCardsOwnResolution()
        {
            // Ordering matters (see PlayFromMarketCommand's own doc comment): CompleteAction()
            // must resolve/pop THIS effect (the "which market card" selection) before
            // PlayCardFromMarket pushes the market card's own, independent effect chain onto
            // the now-empty stack - calling PlayCardFromMarket first would leave this
            // EffectContext dangling underneath it, and the stack would never fully drain.
            var command = new PlayFromMarketCommand(_marketCard, _sourceCard);

            command.Execute(_state.MatchContext);

            Received.InOrder(() =>
            {
                _state.ActionSystem.CompleteAction();
                _state.MatchManager.PlayCardFromMarket(_marketCard, _sourceCard);
            });
        }
    }
}
