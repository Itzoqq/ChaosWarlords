using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using NSubstitute;
using System.Collections.Generic;
using System;
using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Tests.Source.Core.Utilities
{
    [TestClass]
    [TestCategory("Unit")]
    public class DtoMapperTests
    {
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
            player.Power = 10;
            player.Hand.Add(new Card("c1", "C1", 1, CardAspect.Neutral, 0, 0, 0));

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
            player.Hand.Add(card);

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
        public void HydrateCommand_PlayCard_ReturnsCorrectCommand()
        {
            // Arrange
            var player = new Player(PlayerColor.Red, Guid.NewGuid());
            player.SeatIndex = 0;
            var card = new Card("c_bolt", "Bolt", 2, CardAspect.Sorcery, 0, 0, 0);
            player.Hand.Add(card);

            var dto = new PlayCardCommandDto
            {
                Seat = 0,
                HandIdx = 0,
                CardId = "c_bolt"
            };

            var state = Substitute.For<IGameplayState>();
            var turnManager = Substitute.For<ITurnManager>();

            // Setup mocking infrastructure
            turnManager.Players.Returns(new List<Player> { player });
            state.TurnManager.Returns(turnManager);

            // Act
            var resultCommand = DtoMapper.HydrateCommand(dto, state);

            // Assert
            Assert.IsInstanceOfType(resultCommand, typeof(PlayCardCommand));
            var playCmd = resultCommand as PlayCardCommand;
            Assert.IsNotNull(playCmd);
            Assert.AreEqual(card.Id, playCmd.Card.Id);
            Assert.AreEqual(card.Name, playCmd.Card.Name);
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
            var node = new MapNode(99, Microsoft.Xna.Framework.Vector2.Zero);
            var player = new Player(PlayerColor.Red);
            var command = new DeployTroopCommand(node, player);
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
        public void HydrateCommand_BuyCard_ReturnsCorrectCommand()
        {
            var dto = new BuyCardCommandDto { CardId = "market_c1" };
            var state = Substitute.For<IGameplayState>();
            var marketCard = new Card("market_c1", "Market Item", 2, CardAspect.Neutral, 0, 0, 0);
            
            state.MarketManager.MarketRow.Returns(new List<Card> { marketCard });
            state.TurnManager.Returns(Substitute.For<ITurnManager>());
            state.TurnManager.Players.Returns(new List<Player>()); // Fix for HydrateCommand accessing Players

            var cmd = DtoMapper.HydrateCommand(dto, state) as BuyCardCommand;
            
            Assert.IsNotNull(cmd);
            Assert.AreEqual("market_c1", cmd.Card.Id);
        }

        [TestMethod]
        public void HydrateCommand_DeployTroop_ReturnsCorrectCommand()
        {
            var dto = new DeployTroopCommandDto { NodeId = 50, Seat = 1 };
            var state = Substitute.For<IGameplayState>();
            var player = new Player(PlayerColor.Blue) { SeatIndex = 1 };
            
            state.MapManager.Nodes.Returns(new List<MapNode> { new MapNode(50, Microsoft.Xna.Framework.Vector2.Zero) });
            state.TurnManager.Players.Returns(new List<Player> { player });

            var cmd = DtoMapper.HydrateCommand(dto, state) as DeployTroopCommand;

            Assert.IsNotNull(cmd);
            Assert.AreEqual(50, cmd!.Node.Id);
            Assert.AreEqual(PlayerColor.Blue, cmd!.Player!.Color);
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

        [TestMethod]
        public void HydrateCommand_Assassinate_ReturnsCorrectCommand()
        {
             var dto = new AssassinateCommandDto { NodeId = 303, CardId = "c_ash" };
             
             var state = Substitute.For<IGameplayState>();
             state.TurnManager.Players.Returns(new List<Player>()); // Fix for HydrateCommand accessing Players
             
             var cmd = DtoMapper.HydrateCommand(dto, state) as AssassinateCommand;
             
             Assert.IsNotNull(cmd);
             Assert.AreEqual(303, cmd.TargetNodeId);
             Assert.AreEqual("c_ash", cmd.CardId);
        }

        [TestMethod]
        public void HydrateCommand_ResolveSpy_ReturnsCorrectCommand()
        {
             var dto = new ResolveSpyCommandDto { SiteId = 404, Color = "Red", CardId = "c_spy" };
             
             var state = Substitute.For<IGameplayState>();
             state.TurnManager.Players.Returns(new List<Player>()); // Fix for HydrateCommand accessing Players

             var cmd = DtoMapper.HydrateCommand(dto, state) as ResolveSpyCommand;
             
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
                p.Hand.Add(card1);
                p.Hand.Add(card2);
                p.SeatIndex = 0;
                
                var loggerMock = Substitute.For<IGameLogger>();
                var stateMock = Substitute.For<IGameplayState>();
                stateMock.Logger.Returns(loggerMock);
                
                var tmMock = Substitute.For<ITurnManager>();
                tmMock.Players.Returns(new List<Player> { p });
                stateMock.TurnManager.Returns(tmMock);
                
                var dto = new PlayCardCommandDto
                {
                    CardId = "noble_111",
                    HandIdx = 1, // Wrong index (Soldier is here)
                    Seat = 0
                };
                
                var result = ChaosWarlords.Source.Core.Utilities.DtoMapper.HydrateCommand(dto, stateMock) as PlayCardCommand;
                
                Assert.IsNotNull(result);
                Assert.AreEqual("noble_111", result.Card.Id, "Hydration should prefer ID over Index!");
            }
            
            [TestMethod]
            public void HydrateCommand_FallbackToIndexIfIdMissing()
            {
                var p = new Player(PlayerColor.Red, Guid.NewGuid());
                var card1 = new Card("noble_111", "Noble", 0, CardAspect.Neutral, 1, 1, 1);
                p.Hand.Add(card1);
                p.SeatIndex = 0;
                
                var loggerMock = Substitute.For<IGameLogger>();
                var stateMock = Substitute.For<IGameplayState>();
                stateMock.Logger.Returns(loggerMock);
                
                var tmMock = Substitute.For<ITurnManager>();
                tmMock.Players.Returns(new List<Player> { p });
                stateMock.TurnManager.Returns(tmMock);
                
                var dto = new PlayCardCommandDto
                {
                    CardId = "noble_old_XX",
                    HandIdx = 0, 
                    Seat = 0
                };
                
                var result = ChaosWarlords.Source.Core.Utilities.DtoMapper.HydrateCommand(dto, stateMock) as PlayCardCommand;
                
                Assert.IsNotNull(result);
                Assert.AreEqual("noble_111", result.Card.Id, "Hydration should fallback to index if ID not found.");
                
                // Verify warning logged
                loggerMock.Received().Log(Arg.Is<string>(s => s.Contains("Fell back to Index")), LogChannel.Warning);
            }
    }
}
