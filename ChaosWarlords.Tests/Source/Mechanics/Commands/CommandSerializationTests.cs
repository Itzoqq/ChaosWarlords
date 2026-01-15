using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Data.Enums;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Tests.Source.Mechanics.Commands
{
    [TestClass]
    public class CommandSerializationTests
    {
        private Card CreateDummyCard(string id)
        {
            return new Card(id, "Dummy", 2, ChaosWarlords.Source.Utilities.CardAspect.Neutral, 1, 1, 0);
        }

        [TestMethod]
        public void Verify_AllCommands_HaveUniqueTypes()
        {
            // Just a sanity check that I assigned unique enums
            var commands = new List<ChaosWarlords.Source.Core.Interfaces.Logic.IGameCommand>
            {
                new PlayCardCommand(CreateDummyCard("c1")),
                new BuyCardCommand(CreateDummyCard("c2")),
                new DeployTroopCommand(new MapNode(1, Microsoft.Xna.Framework.Vector2.Zero)),
                new EndTurnCommand(),
                new CancelActionCommand(),
                new ActionCompletedCommand(),
                new PromoteCommand("c1"),
                new MoveTroopCommand(1, 2, "c1"),
                new PlaceSpyCommand(5, "c1"),
                new ReturnTroopCommand(1, "c1"),
                new AssassinateCommand(1, "c1"),
                new SupplantCommand(1, "c1", "c2"),
                new StartAssassinateCommand(),
                new StartReturnSpyCommand(),
                new ResolveSpyCommand(1, ChaosWarlords.Source.Utilities.PlayerColor.Red, "c1")
            };

            var types = new HashSet<CommandType>();
            foreach (var cmd in commands)
            {
                Assert.AreNotEqual(CommandType.None, cmd.Type, $"Command {cmd.GetType().Name} has None type.");
                Assert.IsTrue(types.Add(cmd.Type), $"Duplicate CommandType {cmd.Type} for {cmd.GetType().Name}.");
            }
        }

        [TestMethod]
        public void Verify_PlayCardCommand_Serialization()
        {
            var card = CreateDummyCard("test_card_1");
            var cmd = new PlayCardCommand(card);

            var dto = (PlayCardCommandDto)cmd.ToDto();

            Assert.AreEqual("test_card_1", dto.CardId);
            Assert.AreEqual(-1, dto.HandIdx); // Default behavior
            Assert.AreEqual(CommandType.PlayCard, cmd.Type);
        }

        [TestMethod]
        public void Verify_DeployTroopCommand_Serialization()
        {
            var node = new MapNode(99, Microsoft.Xna.Framework.Vector2.Zero);
            var cmd = new DeployTroopCommand(node);

            var dto = (DeployTroopCommandDto)cmd.ToDto();

            Assert.AreEqual(99, dto.NodeId);
            Assert.AreEqual(CommandType.DeployTroop, cmd.Type);
        }

        [TestMethod]
        public void Verify_AssassinateCommand_Serialization()
        {
            var cmd = new AssassinateCommand(42, "KillCard", "FeedingCard");
            var dto = (AssassinateCommandDto)cmd.ToDto();

            Assert.AreEqual(42, dto.NodeId);
            Assert.AreEqual("KillCard", dto.CardId);
            Assert.AreEqual("FeedingCard", dto.DevourCardId);
        }

        [TestMethod]
        public void Verify_DtoMapper_Enrichment()
        {
            // Test that DtoMapper correctly enriches the DTO with hand index
            var player = new Player(ChaosWarlords.Source.Utilities.PlayerColor.Red) { SeatIndex = 0 };
            var card = CreateDummyCard("hand_card");
            player.Hand.Add(card);

            var cmd = new PlayCardCommand(card);

            // Should produce enriched DTO
            var dto = (PlayCardCommandDto)DtoMapper.ToDto(cmd, 1, player)!;

            Assert.AreEqual("hand_card", dto.CardId);
            Assert.AreEqual(0, dto.HandIdx, "DtoMapper should have found the card at index 0.");
        }
    }
}
