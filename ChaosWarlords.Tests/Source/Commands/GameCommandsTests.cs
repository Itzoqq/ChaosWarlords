using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class GameCommandsTests
    {
        [TestMethod]
        public void BuyCardCommand_ExecutesTryBuyCard()
        {
            // 1. Arrange
            var stateSub = Substitute.For<IGameplayState>();
            var marketSub = Substitute.For<IMarketManager>();

            // Setup Dependencies
            var player = new Player(PlayerColor.Red);
            var turnManager = new TurnManager(new List<Player> { player });

            stateSub.MarketManager.Returns(marketSub);
            stateSub.TurnManager.Returns(turnManager);

            var card = new Card("test", "Test Card", 3, CardAspect.Neutral, 0, 0);
            var command = new BuyCardCommand(card);

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert
            marketSub.Received(1).TryBuyCard(player, card);
        }

        [TestMethod]
        public void DeployTroopCommand_ExecutesTryDeploy()
        {
            // 1. Arrange
            var stateSub = Substitute.For<IGameplayState>();
            var mapSub = Substitute.For<IMapManager>();

            var player = new Player(PlayerColor.Red);
            var turnManager = new TurnManager(new List<Player> { player });

            stateSub.MapManager.Returns(mapSub);
            stateSub.TurnManager.Returns(turnManager);

            var node = new MapNode(1, Vector2.Zero);
            var command = new DeployTroopCommand(node);

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert
            mapSub.Received(1).TryDeploy(player, node);
        }

        [TestMethod]
        public void ToggleMarketCommand_OpensMarket_WhenClosed()
        {
            // 1. Arrange
            var stateSub = Substitute.For<IGameplayState>();
            stateSub.IsMarketOpen.Returns(false);

            var command = new ToggleMarketCommand();

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert
            // Assuming the command calls ToggleMarket() or sets IsMarketOpen = true
            stateSub.Received(1).ToggleMarket();
        }

        [TestMethod]
        public void ToggleMarketCommand_ClosesMarket_WhenOpen()
        {
            // 1. Arrange
            var stateSub = Substitute.For<IGameplayState>();
            stateSub.IsMarketOpen.Returns(true);

            var command = new ToggleMarketCommand();

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert
            // The command likely calls CloseMarket() specifically when open to handle mode switching
            stateSub.Received(1).CloseMarket();
        }

        [TestMethod]
        public void ResolveSpyCommand_FinalizesSpyReturn()
        {
            // 1. Arrange
            var stateSub = Substitute.For<IGameplayState>();
            var actionSub = Substitute.For<IActionSystem>();

            stateSub.ActionSystem.Returns(actionSub);

            var command = new ResolveSpyCommand(PlayerColor.Blue);

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert
            actionSub.Received(1).FinalizeSpyReturn(PlayerColor.Blue);
        }

        [TestMethod]
        public void CancelActionCommand_CancelsTargeting_AndSwitchesMode()
        {
            // 1. Arrange
            var stateSub = Substitute.For<IGameplayState>();
            var actionSub = Substitute.For<IActionSystem>();

            stateSub.ActionSystem.Returns(actionSub);

            var command = new CancelActionCommand();

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert
            actionSub.Received(1).CancelTargeting();
            stateSub.Received(1).SwitchToNormalMode();
        }

        [TestMethod]
        public void SwitchToNormalModeCommand_ExecutesSwitch()
        {
            // 1. Arrange
            var stateSub = Substitute.For<IGameplayState>();
            var command = new SwitchToNormalModeCommand();

            // 2. Act
            command.Execute(stateSub);

            // 3. Assert
            stateSub.Received(1).SwitchToNormalMode();
        }
    }
}