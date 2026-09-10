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
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Tests.Utilities;

namespace ChaosWarlords.Tests.Source.Core.Utilities
{
    // Covers DtoMapper - live game state/entities to DTO serialization. Deserialization
    // (DTO back to a live IGameCommand) tests live in CommandHydratorTests.cs instead, mirroring
    // the production DtoMapper/CommandHydrator split.
    [TestClass]
    [TestCategory("Unit")]
    public class DtoMapperTests
    {
        [TestMethod]
        public void ToGameStateDto_StateHash_MatchesMatchContextGetStateHash()
        {
            // GameStateDto.StateHash must be populated FROM MatchContext.GetStateHash(), not
            // computed independently - see both types' doc comments for why (this used to be
            // two separate, unequally-thorough hash implementations: GameStateDto had its own
            // CalculateChecksum() covering only Seed/TurnNumber/Phase/SequenceNumber, while
            // GetStateHash() also covered map/players/market - so a desync outside those four
            // fields would never have shown up in the DTO that would actually travel over the
            // wire). Verify the single remaining implementation is what actually lands on the
            // DTO, and that it's sensitive to a field GetStateHash covers but the old
            // CalculateChecksum() never did (map node occupant).
            var player = new ChaosWarlords.Source.Entities.Actors.Player(PlayerColor.Red);
            var turnManager = Substitute.For<ChaosWarlords.Source.Core.Interfaces.Services.ITurnManager>();
            turnManager.Players.Returns(new List<ChaosWarlords.Source.Entities.Actors.Player> { player });
            var mapManager = Substitute.For<ChaosWarlords.Source.Core.Interfaces.Services.IMapManager>();
            var node = new MapNode(1, new LogicVector2(0, 0));
            mapManager.Nodes.Returns(new List<MapNode> { node });
            var marketManager = Substitute.For<ChaosWarlords.Source.Core.Interfaces.Services.IMarketManager>();
            marketManager.MarketRow.Returns(new List<Card>());
            var playerStateManager = Substitute.For<ChaosWarlords.Source.Core.Interfaces.Services.IPlayerStateManager>();
            var actionSystem = new ActionSystem(turnManager, mapManager, TestLogger.Instance, playerStateManager, marketManager);

            var context = new ChaosWarlords.Source.Contexts.MatchContext(
                turnManager,
                mapManager,
                marketManager,
                actionSystem,
                Substitute.For<ChaosWarlords.Source.Core.Interfaces.Data.ICardDatabase>(),
                Substitute.For<ChaosWarlords.Source.Core.Interfaces.Services.IPlayerStateManager>(),
                TestLogger.Instance,
                42);
            actionSystem.SetMatchContext(context);

            var dto1 = DtoMapper.ToGameStateDto(context);
            Assert.AreEqual(context.GetStateHash(), dto1.StateHash);

            node.Occupant = PlayerColor.Red;
            var dto2 = DtoMapper.ToGameStateDto(context);
            Assert.AreEqual(context.GetStateHash(), dto2.StateHash);
            Assert.AreNotEqual(dto1.StateHash, dto2.StateHash, "A map change GetStateHash() covers must change the DTO's StateHash too.");
        }

        [TestMethod]
        public void ToDto_Card_ReturnsCorrectDto()
        {
            // Arrange
            var card = new Card("id_1", "Test Card", 5, CardAspect.Warlord, 1, 1, 1);
            card.Location = CardLocation.Hand;

            // Act
            var dto = DtoMapper.ToDto(card, 2);

            // Assert
            Assert.IsNotNull(dto);
            Assert.AreEqual("id_1", dto.DefinitionId);
            Assert.AreEqual(CardLocation.Hand, dto.Location);
            Assert.AreEqual(2, dto.ListIndex);
        }

        [TestMethod]
        public void ToDto_Player_ReturnsCorrectDto()
        {
            // Arrange
            var player = new Player(PlayerColor.Red, Guid.NewGuid(), "Red Player");
            player.AddPower(10);
            player.AddToHand(new Card("c1", "C1", 1, CardAspect.Neutral, 0, 0, 0));

            // Act
            var dto = DtoMapper.ToDto(player);

            // Assert
            Assert.IsNotNull(dto);
            Assert.AreEqual(player.PlayerId, dto.PlayerId);
            Assert.AreEqual(10, dto.Power);
            Assert.HasCount(1, dto.Hand);
            Assert.AreEqual("c1", dto.Hand[0].DefinitionId);
            Assert.AreEqual(0, dto.Hand[0].ListIndex);
        }

        [TestMethod]
        public void ToDto_PlayCardCommand_ReturnsCorrectDto()
        {
            // Arrange
            var player = new Player(PlayerColor.Blue);
            var card = new Card("c_fireball", "Fireball", 3, CardAspect.Sorcery, 0, 0, 0);
            player.AddToHand(card);

            var command = new PlayCardCommand(card);

            // Act
            var dto = DtoMapper.ToDto(command, 42, player);

            // Assert
            Assert.IsNotNull(dto);
            Assert.IsInstanceOfType(dto, typeof(PlayCardCommandDto));
            var playDto = (PlayCardCommandDto)dto;
            Assert.AreEqual(42, playDto.Seq);
            Assert.AreEqual(player.SeatIndex, playDto.Seat);
            Assert.AreEqual("c_fireball", playDto.CardId);
            Assert.AreEqual(0, playDto.HandIdx);
        }

        [TestMethod]
        public void ToDto_BuyCardCommand_ReturnsCorrectDto()
        {
            var card = new Card("c_buy", "BuyMe", 5, CardAspect.Neutral, 1, 1, 1);
            var command = new BuyCardCommand(card);
            var dto = DtoMapper.ToDto(command, 10, new Player(PlayerColor.Red)); // Dummy player

            Assert.IsInstanceOfType(dto, typeof(BuyCardCommandDto));
            var buyDto = (BuyCardCommandDto)dto;
            Assert.AreEqual("c_buy", buyDto.CardId);
        }

        [TestMethod]
        public void ToDto_DeployTroopCommand_ReturnsCorrectDto()
        {
            var node = new MapNode(99, LogicVector2.Zero);
            var player = new Player(PlayerColor.Red);
            var command = new DeployTroopCommand(node);
            var dto = DtoMapper.ToDto(command, 11, player);

            Assert.IsInstanceOfType(dto, typeof(DeployTroopCommandDto));
            var deployDto = (DeployTroopCommandDto)dto;
            Assert.AreEqual(99, deployDto.NodeId);
        }

        [TestMethod]
        public void ToDto_EmptyCommands_ReturnsCorrectDtos()
        {
            var player = new Player(PlayerColor.Red);
            Assert.IsInstanceOfType(DtoMapper.ToDto(new EndTurnCommand(), 1, player), typeof(EndTurnCommandDto));
            Assert.IsInstanceOfType(DtoMapper.ToDto(new CancelActionCommand(), 2, player), typeof(CancelActionCommandDto));
            Assert.IsInstanceOfType(DtoMapper.ToDto(new ToggleMarketCommand(), 3, player), typeof(ToggleMarketCommandDto));
            Assert.IsInstanceOfType(DtoMapper.ToDto(new SwitchToNormalModeCommand(), 4, player), typeof(SwitchModeCommandDto));
            Assert.IsInstanceOfType(DtoMapper.ToDto(new ActionCompletedCommand(), 5, player), typeof(ActionCompletedCommandDto));
        }

        [TestMethod]
        public void ToDto_AssassinateCommand_ReturnsCorrectDto()
        {
            var command = new AssassinateCommand(101, "killer_card");
            var dto = DtoMapper.ToDto(command, 1, new Player(PlayerColor.Red));

            Assert.IsInstanceOfType(dto, typeof(AssassinateCommandDto));
            var ashDto = (AssassinateCommandDto)dto;
            Assert.AreEqual(101, ashDto.NodeId);
            Assert.AreEqual("killer_card", ashDto.CardId);
        }

        [TestMethod]
        public void ToDto_ResolveSpyCommand_ReturnsCorrectDto()
        {
            var command = new ResolveSpyCommand(202, PlayerColor.Blue, "spy_card");
            var dto = DtoMapper.ToDto(command, 1, new Player(PlayerColor.Red));

            Assert.IsInstanceOfType(dto, typeof(ResolveSpyCommandDto));
            var spyDto = (ResolveSpyCommandDto)dto;
            Assert.AreEqual(202, spyDto.SiteId);
            Assert.AreEqual("Blue", spyDto.Color);
            Assert.AreEqual("spy_card", spyDto.CardId);
        }
    }
}
