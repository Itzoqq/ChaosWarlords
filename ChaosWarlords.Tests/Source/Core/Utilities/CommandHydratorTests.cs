using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Core.Data;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Tests.Source.Core.Utilities
{
    // Covers CommandHydrator - reconstructing a live IGameCommand from a recorded GameCommandDto.
    // Serialization (live state to DTO) tests live in DtoMapperTests.cs instead, mirroring the
    // production DtoMapper/CommandHydrator split.
    [TestClass]
    [TestCategory("Unit")]
    public class CommandHydratorTests
    {
        [TestMethod]
        public void HydrateCommand_PlayCard_ReturnsCorrectCommand()
        {
            // Arrange
            var player = new Player(PlayerColor.Red, Guid.NewGuid());
            player.SeatIndex = 0;
            var card = new Card("c_bolt", "Bolt", 2, CardAspect.Sorcery, 0, 0, 0);
            player.AddToHand(card);

            var dto = new PlayCardCommandDto
            {
                Seat = 0,
                HandIdx = 0,
                CardId = "c_bolt"
            };

            var state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            // Setup mocking infrastructure
            state.TurnManager.Players.Returns(new List<Player> { player });

            // Act
            var resultCommand = CommandHydrator.HydrateCommand(dto, state.MatchContext);

            // Assert
            Assert.IsInstanceOfType(resultCommand, typeof(PlayCardCommand));
            var playCmd = resultCommand as PlayCardCommand;
            Assert.IsNotNull(playCmd);
            Assert.AreEqual(card.RuntimeId, playCmd.CardRuntimeId);
            Assert.AreEqual(card.Id, playCmd.CardId);
        }

        [TestMethod]
        public void HydrateCommand_BuyCard_ReturnsCorrectCommand()
        {
            var dto = new BuyCardCommandDto { CardId = "market_c1" };
            var state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            var marketCard = new Card("market_c1", "Market Item", 2, CardAspect.Neutral, 0, 0, 0);

            state.MarketManager.MarketRow.Returns(new List<Card> { marketCard });
            // state.TurnManager is auto-initialized in fake, but we can override or configure it
            state.TurnManager.Players.Returns(new List<Player>()); // Fix for HydrateCommand accessing Players

            var cmd = CommandHydrator.HydrateCommand(dto, state.MatchContext) as BuyCardCommand;

            Assert.IsNotNull(cmd);
            Assert.AreEqual("market_c1", cmd.CardId);
        }

        [TestMethod]
        public void HydrateCommand_DeployTroop_ReturnsCorrectCommand()
        {
            var dto = new DeployTroopCommandDto { NodeId = 50, Seat = 1 };
            var state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            var player = new Player(PlayerColor.Blue) { SeatIndex = 1 };

            state.MapManager.Nodes.Returns(new List<MapNode> { new MapNode(50, LogicVector2.Zero) });
            state.TurnManager.Players.Returns(new List<Player> { player });

            var cmd = CommandHydrator.HydrateCommand(dto, state.MatchContext) as DeployTroopCommand;

            Assert.IsNotNull(cmd);
            Assert.AreEqual(50, cmd!.NodeId);
        }

        [TestMethod]
        public void HydrateCommand_Assassinate_ReturnsCorrectCommand()
        {
            var dto = new AssassinateCommandDto { NodeId = 303, CardId = "c_ash" };

            var state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            state.TurnManager.Players.Returns(new List<Player>()); // Fix for HydrateCommand accessing Players

            var cmd = CommandHydrator.HydrateCommand(dto, state.MatchContext) as AssassinateCommand;

            Assert.IsNotNull(cmd);
            Assert.AreEqual(303, cmd.TargetNodeId);
            Assert.AreEqual("c_ash", cmd.CardId);
        }

        [TestMethod]
        public void HydrateCommand_ResolveSpy_ReturnsCorrectCommand()
        {
            var dto = new ResolveSpyCommandDto { SiteId = 404, Color = "Red", CardId = "c_spy" };

            var state = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            state.TurnManager.Players.Returns(new List<Player>()); // Fix for HydrateCommand accessing Players

            var cmd = CommandHydrator.HydrateCommand(dto, state.MatchContext) as ResolveSpyCommand;

            Assert.IsNotNull(cmd);
            Assert.AreEqual(404, cmd.SiteId);
            Assert.AreEqual(PlayerColor.Red, cmd.SpyColor);
            Assert.AreEqual("c_spy", cmd.CardId);
        }

        [TestMethod]
        public void HydrateCommand_PreferCardIdOverIndex()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            // Card(string id, string name, int cost, CardAspect aspect, int deckVp, int innerCircleVp, int influence)
            var card1 = new Card("noble_111", "Noble", 0, CardAspect.Neutral, 1, 1, 1);
            var card2 = new Card("soldier_222", "Soldier", 0, CardAspect.Neutral, 1, 1, 0);
            p.AddToHand(card1);
            p.AddToHand(card2);
            p.SeatIndex = 0;

            var loggerMock = Substitute.For<IGameLogger>();
            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.Logger = loggerMock;

            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new PlayCardCommandDto
            {
                CardId = "noble_111",
                HandIdx = 1, // Wrong index (Soldier is here)
                Seat = 0
            };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as PlayCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual("noble_111", result.CardId, "Hydration should prefer ID over Index!");
        }

        [TestMethod]
        public void HydrateCommand_FallbackToIndexIfIdMissing()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            var card1 = new Card("noble_111", "Noble", 0, CardAspect.Neutral, 1, 1, 1);
            p.AddToHand(card1);
            p.SeatIndex = 0;

            var loggerMock = Substitute.For<IGameLogger>();
            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.Logger = loggerMock;
            // TestGameplayState.MatchContext is wired up once at construction time (see its
            // own comment), so it still holds the pre-reassignment Logger unless we rebuild
            // it - matters now that HydrateCommand reads the logger via MatchContext.Logger
            // rather than IGameplayState.Logger directly.
            stateMock.InitializeMatchContext();

            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new PlayCardCommandDto
            {
                CardId = "noble_old_XX",
                HandIdx = 0,
                Seat = 0
            };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as PlayCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual("noble_111", result.CardId, "Hydration should fallback to index if ID not found.");

            // Verify warning logged
            loggerMock.Received().Log(Arg.Is<string>(s => s.Contains("Fell back to Index")), LogChannel.Warning);
        }

        [TestMethod]
        public void HydrateCommand_Devour_PreferCardIdOverIndex()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            var card1 = new Card("devour_111", "Devour1", 0, CardAspect.Neutral, 1, 1, 1);
            var card2 = new Card("devour_222", "Devour2", 0, CardAspect.Neutral, 1, 1, 0);
            p.AddToHand(card1);
            p.AddToHand(card2);
            p.SeatIndex = 0;

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new DevourCardCommandDto
            {
                CardId = "devour_111",
                HandIdx = 1, // Wrong index (card2 is here)
                Seat = 0
            };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual("devour_111", result.CardId, "Hydration should prefer ID over Index!");
        }

        [TestMethod]
        public void HydrateCommand_Devour_FallbackToIndexIfIdMissing()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            var card1 = new Card("devour_111", "Devour1", 0, CardAspect.Neutral, 1, 1, 1);
            p.AddToHand(card1);
            p.SeatIndex = 0;

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new DevourCardCommandDto
            {
                CardId = "devour_old_XX", // ID not found
                HandIdx = 0,
                Seat = 0
            };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual("devour_111", result.CardId, "Hydration should fallback to index if ID not found.");
        }

        [TestMethod]
        public void HydrateCommand_Devour_WithNullCardId_UsesIndex()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            var card1 = new Card("devour_111", "Devour1", 0, CardAspect.Neutral, 1, 1, 1);
            p.AddToHand(card1);
            p.SeatIndex = 0;

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new DevourCardCommandDto
            {
                CardId = null, // Explicitly null
                HandIdx = 0,
                Seat = 0
            };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual("devour_111", result.CardId);
        }
        [TestMethod]
        public void HydrateCommand_Devour_FromInnerCircle()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid());
            var card1 = new Card("inner_111", "Inner1", 0, CardAspect.Neutral, 1, 1, 1);
            card1.Location = CardLocation.InnerCircle;
            p.AddToInnerCircle(card1);
            p.SeatIndex = 0;

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new DevourCardCommandDto
            {
                CardId = "inner_111",
                Location = "InnerCircle",
                Seat = 0
            };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual("inner_111", result.CardId);
            Assert.AreEqual(CardLocation.InnerCircle, result.LocationAtConstruction);
        }

        // --- FindDevourTargetCard's RuntimeId-lookup branch (planning.txt TIER 1 item 4,
        // risk-hotspot remediation, 2026-09-01) - this was the worst Crap score in the
        // solution (110), and largely because NOTHING above exercises dto.CardRuntimeId at
        // all; every existing case above resolves by CardId/HandIdx instead. Added as
        // characterization tests BEFORE extracting the lookup into its own helper. ---

        [TestMethod]
        public void HydrateCommand_Devour_ByRuntimeId_FindsCardInHand()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid()) { SeatIndex = 0 };
            var card = new Card("hand_card", "HandCard", 0, CardAspect.Neutral, 0, 0, 0);
            p.AddToHand(card);

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new DevourCardCommandDto { CardRuntimeId = card.RuntimeId, Seat = 0 };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual(card.RuntimeId, result.CardRuntimeId);
        }

        [TestMethod]
        public void HydrateCommand_Devour_ByRuntimeId_FindsCardInInnerCircle()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid()) { SeatIndex = 0 };
            var card = new Card("inner_card", "InnerCard", 0, CardAspect.Neutral, 0, 0, 0);
            p.AddToInnerCircle(card);

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new DevourCardCommandDto { CardRuntimeId = card.RuntimeId, Seat = 0 };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual(card.RuntimeId, result.CardRuntimeId);
        }

        [TestMethod]
        public void HydrateCommand_Devour_ByRuntimeId_FindsCardInPlayedCards()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid()) { SeatIndex = 0 };
            var card = new Card("played_card", "PlayedCard", 0, CardAspect.Neutral, 0, 0, 0);
            p.AddToPlayed(card);

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });

            var dto = new DevourCardCommandDto { CardRuntimeId = card.RuntimeId, Seat = 0 };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual(card.RuntimeId, result.CardRuntimeId);
        }

        [TestMethod]
        public void HydrateCommand_Devour_ByRuntimeId_FindsCardInMarket()
        {
            var p = new Player(PlayerColor.Red, Guid.NewGuid()) { SeatIndex = 0 };
            var card = new Card("market_card", "MarketCard", 0, CardAspect.Neutral, 0, 0, 0);
            card.Location = CardLocation.Market;

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });
            stateMock.MarketManager.MarketRow.Returns(new List<Card> { card });

            var dto = new DevourCardCommandDto { CardRuntimeId = card.RuntimeId, Seat = 0 };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual(card.RuntimeId, result.CardRuntimeId);
        }

        [TestMethod]
        public void HydrateCommand_Devour_MarketLocation_ByCardId()
        {
            // Distinct from the RuntimeId-based Market lookup above - this is the
            // Location=="Market" + CardId fallback path (no CardRuntimeId set at all).
            var p = new Player(PlayerColor.Red, Guid.NewGuid()) { SeatIndex = 0 };
            var card = new Card("market_by_id", "MarketById", 0, CardAspect.Neutral, 0, 0, 0);
            card.Location = CardLocation.Market;

            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player> { p });
            stateMock.MarketManager.MarketRow.Returns(new List<Card> { card });

            var dto = new DevourCardCommandDto { CardId = "market_by_id", Location = "Market", Seat = 0 };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext) as DevourCardCommand;

            Assert.IsNotNull(result);
            Assert.AreEqual("market_by_id", result.CardId);
            Assert.AreEqual(CardLocation.Market, result.LocationAtConstruction);
        }

        [TestMethod]
        public void HydrateCommand_Devour_UnknownSeat_ReturnsNull()
        {
            var stateMock = new ChaosWarlords.Tests.Source.Doubles.State.TestGameplayState();
            stateMock.TurnManager.Players.Returns(new List<Player>()); // No player at any seat.

            var dto = new DevourCardCommandDto { CardId = "whatever", Seat = 0 };

            var result = CommandHydrator.HydrateCommand(dto, stateMock.MatchContext);

            Assert.IsNull(result);
        }
    }
}
