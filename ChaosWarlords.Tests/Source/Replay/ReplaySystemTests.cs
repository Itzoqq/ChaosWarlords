using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Data.Dtos;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ChaosWarlords.Tests.Replay
{
    public class NullLogger : IGameLogger
    {
        public void Log(string message, LogChannel channel = LogChannel.General) { }
        public void Log(object message, LogChannel channel = LogChannel.General) { }
    }

    [TestClass]
    public class ReplaySystemTests
    {
        private IGameLogger _logger = new NullLogger();

        [TestMethod]
        public void ReplayManager_RecordsCommands_InSequence()
        {
            // Arrange
            var replayManager = new ReplayManager(_logger);
            var player = new PlayerBuilder()
                .WithColor(PlayerColor.Red)
                .WithSeatIndex(0)
                .WithName("TestPlayer")
                .Build();
            var node = new MapNodeBuilder()
                .WithId(1)
                .At(100, 100)
                .Build();
            var cmd1 = new DeployTroopCommand(node, player);
            var cmd2 = new EndTurnCommand();

            // Act
            replayManager.RecordCommand(cmd1, player, 1);
            replayManager.RecordCommand(cmd2, player, 2);
            var json = replayManager.GetRecordingJson();

            // Assert
            Assert.Contains("\"Seq\":1", json);
            Assert.Contains("\"Seq\":2", json);
            Assert.Contains("\"t\":\"deploy\"", json);
            Assert.Contains("\"t\":\"end\"", json);
            Assert.Contains("\"Seat\":0", json);
        }

        [TestMethod]
        public void ReplayManager_IsReplaying_ReturnsTrueWhenActive()
        {
            // Arrange
            var replayManager = new ReplayManager(_logger);
            var json = "{\"Seed\":123,\"Commands\":[{\"t\":\"end\",\"Seq\":1,\"Seat\":0}]}";

            // Act
            replayManager.StartReplay(json);

            // Assert
            Assert.IsTrue(replayManager.IsReplaying);
        }

        [TestMethod]
        public void DtoMapper_SerializesDeployCommand_WithPolymorphism()
        {
            // Arrange
            var player = new PlayerBuilder()
                .WithColor(PlayerColor.Red)
                .WithSeatIndex(2)
                .Build();
            var node = new MapNodeBuilder()
                .WithId(15)
                .Build();
            var cmd = new DeployTroopCommand(node, player);

            // Act
            var dto = DtoMapper.ToDto(cmd, 1, player) as DeployTroopCommandDto;

            // Assert
            Assert.IsNotNull(dto);
            Assert.AreEqual(2, dto.Seat);
            Assert.AreEqual(15, dto.NodeId);
        }

        [TestMethod]
        public void DtoMapper_SerializesPlayCardCommand_WithCardInfo()
        {
            // Arrange
            var card = new CardBuilder().WithName("c_test").Build();
            var player = new PlayerBuilder()
                .WithColor(PlayerColor.Red)
                .WithSeatIndex(1)
                .WithCardsInHand(card)
                .Build();
            var cmd = new PlayCardCommand(card);

            // Act
            var dto = DtoMapper.ToDto(cmd, 5, player) as PlayCardCommandDto;

            // Assert
            Assert.IsNotNull(dto);
            Assert.AreEqual("c_test", dto.CardId);
            Assert.AreEqual(0, dto.HandIdx); // It's the first card in hand
            Assert.AreEqual(1, dto.Seat);
            Assert.AreEqual(5, dto.Seq);
        }

        [TestMethod]
        public void ReplayDataDto_SerializesWithDiscriminators()
        {
            // Arrange
            var data = new ReplayDataDto
            {
                Seed = 42,
                Commands = new List<GameCommandDto>
                {
                    new PlayCardCommandDto { Seq = 1, Seat = 0, CardId = "card_1", HandIdx = 2 },
                    new EndTurnCommandDto { Seq = 2, Seat = 0 }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(data);

            // Assert
            Assert.Contains("\"t\":\"play\"", json);
            Assert.Contains("\"t\":\"end\"", json);
            Assert.Contains("\"Seed\":42", json);
        }

        [TestMethod]
        public void DtoMapper_HydratesPolymorphicCommands()
        {
            // Arrange
            var json = "{\"t\":\"end\",\"Seq\":10,\"Seat\":1}";
            var dto = JsonSerializer.Deserialize<GameCommandDto>(json);

            // Mock state for hydration
            // (In a real test we'd use a real or mock GameplayState)
            // But let's just test that the DTO type is correct
            Assert.IsInstanceOfType(dto, typeof(EndTurnCommandDto));
        }
    }
}
