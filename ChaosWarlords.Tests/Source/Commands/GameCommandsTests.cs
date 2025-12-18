using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class GameCommandsTests
    {
        // ------------------------------------------------------------------------
        // 2. TEST SETUP
        // ------------------------------------------------------------------------

        private MockGameplayState _mockState = null!;
        private MockMapManager _mockMap = null!;
        private MockMarketManager _mockMarket = null!;
        private MockActionSystem _mockAction = null!;
        private TurnManager _turnManager = null!;
        private Player _activePlayer = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockMap = new MockMapManager();
            _mockMarket = new MockMarketManager();
            _mockAction = new MockActionSystem();

            _activePlayer = new Player(PlayerColor.Red);
            _turnManager = new TurnManager(new List<Player> { _activePlayer });

            _mockState = new MockGameplayState(_mockAction)
            {
                MapManager = _mockMap,
                MarketManager = _mockMarket,
                TurnManager = _turnManager
            };
        }

        // ------------------------------------------------------------------------
        // 3. UNIT TESTS
        // ------------------------------------------------------------------------

        [TestMethod]
        public void BuyCardCommand_ExecutesTryBuyCard()
        {
            // Arrange
            var card = new Card("test", "Test Card", 3, CardAspect.Neutral, 0, 0);
            var command = new BuyCardCommand(card);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockMarket.TryBuyCardCalled, "Command must delegate to MarketManager.");
            Assert.AreEqual(card, _mockMarket.LastCardBought, "Command must pass the correct card.");
        }

        [TestMethod]
        public void DeployTroopCommand_ExecutesTryDeploy()
        {
            // Arrange
            var node = new MapNode(1, Vector2.Zero);
            var command = new DeployTroopCommand(node);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockMap.TryDeployCalled, "Command must delegate to MapManager.");
            Assert.AreEqual(node, _mockMap.LastDeployTarget, "Command must pass the correct node.");
        }

        [TestMethod]
        public void ToggleMarketCommand_OpensMarket_WhenClosed()
        {
            // Arrange
            _mockState.IsMarketOpen = false;
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockState.ToggleMarketCalled, "Command should call ToggleMarket.");
            Assert.IsFalse(_mockState.CloseMarketCalled, "Command should NOT call CloseMarket when opening.");
            Assert.IsTrue(_mockState.IsMarketOpen, "Market state should be toggled to Open.");
        }

        [TestMethod]
        public void ToggleMarketCommand_ClosesMarket_WhenOpen()
        {
            // Arrange
            _mockState.IsMarketOpen = true;
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockState.CloseMarketCalled, "Command should call CloseMarket logic (which handles mode switching).");
            Assert.IsFalse(_mockState.IsMarketOpen, "Market state should be closed.");
        }

        [TestMethod]
        public void ResolveSpyCommand_FinalizesSpyReturn()
        {
            // Arrange
            var command = new ResolveSpyCommand(PlayerColor.Blue);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockAction.FinalizeSpyReturnCalled, "Command must delegate to ActionSystem.");
            Assert.AreEqual(PlayerColor.Blue, _mockAction.LastFinalizedSpyColor, "Command must pass correct spy color.");
        }

        [TestMethod]
        public void CancelActionCommand_CancelsTargeting_AndSwitchesMode()
        {
            // Arrange
            var command = new CancelActionCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockAction.CancelTargetingCalled, "Command must cancel the backend action.");
            Assert.IsTrue(_mockState.SwitchToNormalModeCalled, "Command must switch input state to Normal.");
        }

        [TestMethod]
        public void SwitchToNormalModeCommand_ExecutesSwitch()
        {
            // Arrange
            var command = new SwitchToNormalModeCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockState.SwitchToNormalModeCalled, "Command must call SwitchToNormalMode on state.");
        }
    }
}