using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Core.Utilities;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Tests;

namespace ChaosWarlords.Tests.Replay
{
    [TestClass]
    public class ReplayScenarioTests
    {
        private IGameLogger _logger = new NullLogger();

        [TestMethod]
        public void Scenario_PlayMultipleCards_RecordsCorrectly()
        {
            // Arrange
            var replayManager = new ReplayManager(_logger);
            var player = new PlayerBuilder().WithSeatIndex(0).Build();

            var card1 = new CardBuilder().WithName("c1").Build();
            var card2 = new CardBuilder().WithName("c2").Build();
            player.Hand.Add(card1);
            player.Hand.Add(card2);

            var cmd1 = new PlayCardCommand(card1);
            var cmd2 = new PlayCardCommand(card2);

            // Act
            replayManager.RecordCommand(cmd1, player, 1);
            replayManager.RecordCommand(cmd2, player, 2);
            var json = replayManager.GetRecordingJson();

            // Assert
            Assert.Contains("c1", json, "Should contain first card ID");
            Assert.Contains("c2", json, "Should contain second card ID");
            Assert.Contains("\"Seq\":1", json, "Should contain sequence 1");
            Assert.Contains("\"Seq\":2", json, "Should contain sequence 2");
        }

        [TestMethod]
        public void Scenario_FullTurnCycle_RecordsSequence()
        {
            // Arrange
            var replayManager = new ReplayManager(_logger);
            var p1 = new PlayerBuilder().WithSeatIndex(0).Build();
            var p2 = new PlayerBuilder().WithSeatIndex(1).Build();
            var node = new MapNodeBuilder().WithId(99).Build();

            var cmds = new List<(ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand, Player)>
            {
                (new PlayCardCommand(new CardBuilder().WithName("p1_c1").Build()), p1),
                (new DeployTroopCommand(node, p1), p1),
                (new EndTurnCommand(), p1),
                (new PlayCardCommand(new CardBuilder().WithName("p2_c1").Build()), p2),
                (new EndTurnCommand(), p2)
            };

            // Act
            int seq = 1;
            foreach (var (cmd, player) in cmds)
            {
                replayManager.RecordCommand(cmd, player, seq++);
            }
            var json = replayManager.GetRecordingJson();

            // Assert
            Assert.Contains("p1_c1", json);
            Assert.Contains("p2_c1", json);
            // 5 commands total
            Assert.Contains("\"Seq\":5", json);
        }
    }
}
