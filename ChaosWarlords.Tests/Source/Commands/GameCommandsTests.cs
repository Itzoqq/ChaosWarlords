using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Tests.Commands
{
    // --- Mock Classes to Track Interactions ---

    // FIX 1: Added MockInputProvider, necessary for GameplayState's construction
    public class MockInputProvider : IInputProvider
    {
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;
    }

    // FIX 2: Added MockCardDatabase, necessary for GameplayState's construction
    public class MockCardDatabase : ICardDatabase
    {
        public List<Card> GetAllMarketCards() => new List<Card>();
        public Card GetCardById(string id) => default!;
    }

    // Now implements the IMapManager interface
    public class MockMapManager : IMapManager
    {
        // IReadOnlyList Properties (Satisfies IMapManager.Nodes and IMapManager.Sites)
        public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
        public IReadOnlyList<Site> Sites { get; } = new List<Site>();

        // FIX 3: Added field to track if TryDeploy was called
        public bool TryDeployCalled { get; private set; }

        // Setup Methods
        public void CenterMap(int screenWidth, int screenHeight) { }

        // Query Methods
        public MapNode GetNodeAt(Vector2 position) => default!;
        public Site GetSiteAt(Vector2 position) => default!;
        public Site GetSiteForNode(MapNode node) => default!;

        // Logic Methods
        public bool TryDeploy(Player currentPlayer, MapNode targetNode)
        {
            TryDeployCalled = true; // FIX 4: Set the flag
            return true;
        }

        public void DistributeControlRewards(Player activePlayer) { }

        // List<PlayerColor> implementation
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer) => new List<PlayerColor>();
    }

    // Now implements the IMarketManager interface
    public class MockMarketManager : IMarketManager
    {
        public bool TryBuyCardCalled { get; private set; }
        // Implement interface properties/methods used by consumers
        public List<Card> MarketRow { get; } = new List<Card>();
        public void Update(Microsoft.Xna.Framework.Vector2 cursorPosition) { }

        // Matches the signature of the interface method
        public bool TryBuyCard(Player player, Card card)
        {
            TryBuyCardCalled = true;
            return true;
        }
    }

    // Now implements the IActionSystem interface
    public class MockActionSystem : IActionSystem
    {
        public bool TryStartAssassinateCalled { get; private set; }
        public bool TryStartReturnSpyCalled { get; private set; }
        public bool CancelTargetingCalled { get; private set; }
        public bool FinalizeSpyReturnCalled { get; private set; }
        public bool HandleTargetClickCalled { get; private set; }
        public Card PendingCardOnStart { get; private set; } = null!;

        public ActionState CurrentState { get; private set; } = ActionState.Normal;
        public Card PendingCard { get; } = null!;
        public Site PendingSite { get; } = null!;

        public MockActionSystem(Player initialPlayer, MapManager mapManager) { }

        // Matches the signature of the interface method
        public void TryStartAssassinate() { TryStartAssassinateCalled = true; }
        public void TryStartReturnSpy() { TryStartReturnSpyCalled = true; }
        public void CancelTargeting() { CancelTargetingCalled = true; CurrentState = ActionState.Normal; }

        public bool FinalizeSpyReturn(PlayerColor selectedSpyColor)
        {
            FinalizeSpyReturnCalled = true;
            return true;
        }

        public bool HandleTargetClick(MapNode targetNode, Site targetSite)
        {
            HandleTargetClickCalled = true;
            return true; // Simulate successful action completion
        }

        public void StartTargeting(ActionState state, Card pendingCard = null!)
        {
            CurrentState = state;
            PendingCardOnStart = pendingCard!;
        }

        public bool IsTargeting() => CurrentState != ActionState.Normal;

        // Implement the rest of the IActionSystem methods to satisfy the interface
        public void SetCurrentPlayer(Player newPlayer) { }
    }

    [TestClass]
    public class GameCommandsTests
    {
        // ... existing fields ...
        private GameplayState _mockState = null!;
        private Player _testPlayer = null!;
        private MockMapManager _mockMapManager = null!;
        private MockMarketManager _mockMarketManager = null!;
        private MockActionSystem _mockActionSystem = null!;
        private TurnManager _turnManager = null!;

        [TestInitialize]
        public void Setup()
        {
            // Set up test dependencies
            _testPlayer = new Player(PlayerColor.Red);
            _turnManager = new TurnManager(new List<Player> { _testPlayer });
            // Initialize mocks directly
            _mockMapManager = new MockMapManager();
            _mockMarketManager = new MockMarketManager();
            // FIX: The second argument should be null in the mock constructor.
            _mockActionSystem = new MockActionSystem(_testPlayer, null!);

            // Mock a GameplayState instance and inject the mocks
            // FIX: Pass the defined MockProvider and MockCardDatabase
            _mockState = new GameplayState(null!, new MockInputProvider(), new MockCardDatabase());
            _mockState.InjectDependencies(
                new InputManager(new MockInputProvider()),
                new UIManager(800, 600),
                _mockMapManager, // Inject IMapManager
                _mockMarketManager, // Inject IMarketManager
                _mockActionSystem, // Inject IActionSystem
                _turnManager
            );
        }

        [TestMethod]
        public void BuyCardCommand_CallsMarketManager()
        {
            // Arrange
            var card = CardFactory.CreateNoble();
            var command = new BuyCardCommand(card);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockMarketManager.TryBuyCardCalled, "BuyCardCommand must call TryBuyCard on the MarketManager.");
        }

        [TestMethod]
        public void DeployTroopCommand_CallsMapManager()
        {
            // Arrange
            var node = new MapNode(1, Microsoft.Xna.Framework.Vector2.Zero);
            var command = new DeployTroopCommand(node);

            // Act
            command.Execute(_mockState);

            // Assert
            // FIX: Use the correct flag in MockMapManager
            Assert.IsTrue(_mockMapManager.TryDeployCalled, "DeployTroopCommand must call TryDeploy on the MapManager.");
        }

        [TestMethod]
        public void ToggleMarketCommand_TogglesMarketState()
        {
            // Arrange
            _mockState._isMarketOpen = false;
            var command = new ToggleMarketCommand();

            // Act 1: Open
            command.Execute(_mockState);
            Assert.IsTrue(_mockState._isMarketOpen, "ToggleMarketCommand should open the market.");

            // Act 2: Close
            command.Execute(_mockState);
            Assert.IsFalse(_mockState._isMarketOpen, "ToggleMarketCommand should close the market.");
        }

        [TestMethod]
        public void PlayCardCommand_CallsPlayCardOnState()
        {
            // Arrange
            var card = CardFactory.CreateSoldier();
            _turnManager.ActivePlayer.Hand.Add(card);
            var command = new PlayCardCommand(card);
            int initialHandCount = _turnManager.ActivePlayer.Hand.Count;

            // Act
            command.Execute(_mockState);

            // Assert: Verify that the card was processed (moved from Hand to PlayedCards)
            Assert.HasCount(initialHandCount - 1, _turnManager.ActivePlayer.Hand, "PlayCardCommand must result in the card being played.");
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards, "Played card must be moved to PlayedCards.");
        }

        [TestMethod]
        public void MapNodeClickedCommand_WhenTargeting_CallsHandleTargetClick()
        {
            // Arrange
            var node = new MapNode(1, Microsoft.Xna.Framework.Vector2.Zero);
            var command = new MapNodeClickedCommand(node);

            // Set targeting mode
            _mockActionSystem.StartTargeting(ActionState.TargetingAssassinate);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.HandleTargetClickCalled, "MapNodeClickedCommand must call HandleTargetClick when in targeting mode.");
        }

        [TestMethod]
        public void MapNodeClickedCommand_WhenNotTargeting_CallsTryDeploy()
        {
            // Arrange
            var node = new MapNode(1, Microsoft.Xna.Framework.Vector2.Zero);
            var command = new MapNodeClickedCommand(node);

            // Ensure not targeting
            _mockActionSystem.CancelTargeting();

            // Act
            command.Execute(_mockState);

            // Assert
            // FIX: Use the correct flag in MockMapManager
            Assert.IsTrue(_mockMapManager.TryDeployCalled, "MapNodeClickedCommand must call TryDeploy when not in targeting mode.");
        }

        [TestMethod]
        public void SiteClickedCommand_CallsHandleTargetClick()
        {
            // Arrange
            // FIX: Corrected constructor usage for Site, which only takes data arguments now.
            var site = new Site("test", ResourceType.Power, 1, ResourceType.VictoryPoints, 1);
            var command = new SiteClickedCommand(site);

            // Set targeting mode
            _mockActionSystem.StartTargeting(ActionState.TargetingPlaceSpy);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.HandleTargetClickCalled, "SiteClickedCommand must call HandleTargetClick when in targeting mode.");
        }

        [TestMethod]
        public void StartAssassinateCommand_CallsActionSystem()
        {
            // Arrange
            var command = new StartAssassinateCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.TryStartAssassinateCalled, "StartAssassinateCommand must call TryStartAssassinate.");
        }

        [TestMethod]
        public void StartReturnSpyCommand_CallsActionSystem()
        {
            // Arrange
            var command = new StartReturnSpyCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.TryStartReturnSpyCalled, "StartReturnSpyCommand must call TryStartReturnSpy.");
        }

        [TestMethod]
        public void ResolveSpyCommand_CallsFinalizeSpyReturn()
        {
            // Arrange
            var command = new ResolveSpyCommand(PlayerColor.Blue);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.FinalizeSpyReturnCalled, "ResolveSpyCommand must call FinalizeSpyReturn.");
        }

        [TestMethod]
        public void CancelActionCommand_CallsActionSystemCancelTargeting()
        {
            // Arrange
            var command = new CancelActionCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.CancelTargetingCalled, "CancelActionCommand must call CancelTargeting.");
        }
    }
}