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
using System.Linq;
using ChaosWarlords.Source.States.Input;

namespace ChaosWarlords.Tests.Commands
{
    // --- Mock Classes to Track Interactions (Required for Test Setup) ---

    public class MockInputProvider : IInputProvider
    {
        public MouseState GetMouseState() => default;
        public KeyboardState GetKeyboardState() => default;
        public void Reset() { }
    }

    public class MockCardDatabase : ICardDatabase
    {
        private readonly Card _dummyCard = new Card("dummy", "Dummy Card", 0, CardAspect.Neutral, 0, 0);

        public List<Card> GetAllMarketCards() => new List<Card>();
        public Card GetCardById(string id) => default!;

        /// <summary>Provides a clone of a generic card object for deck seeding.</summary>
        public Card GetTestCard() => _dummyCard.Clone();
    }

    public class MockMapManager : IMapManager
    {
        // FIX: The interface uses IReadOnlyList, and List<T> implements it. This is correct.
        public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
        public IReadOnlyList<Site> Sites { get; } = new List<Site>();

        public bool TryDeployCalled { get; private set; }
        public MapNode? TryDeployNode { get; private set; }

        public MapNode GetNodeAt(Vector2 position) => null!;
        public Site GetSiteAt(Vector2 position) => null!;

        public bool TryDeploy(Player player, MapNode node) { TryDeployCalled = true; TryDeployNode = node; return true; }
        public void CenterMap(int screenWidth, int screenHeight) { }

        // FIX: Public implementation should use List<PlayerColor> to satisfy the interface.
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer) => new List<PlayerColor>();

        public void DistributeControlRewards(Player activePlayer) { }
        public Site GetSiteForNode(MapNode node) { throw new NotImplementedException(); }

        // REMOVED: All redundant explicit interface implementations that caused CS0539/CS0102
    }

    public class MockMarketManager : IMarketManager
    {
        // FIX: Public property must match the interface type: List<Card>.
        public List<Card> MarketRow { get; } = new List<Card>();

        public bool TryBuyCardCalled { get; private set; }
        public Card? LastCardToBuy { get; private set; }

        // REMOVED: All redundant explicit interface implementations that caused CS0539/CS0102

        public bool TryBuyCard(Player player, Card card) { TryBuyCardCalled = true; LastCardToBuy = card; return true; }
        public void Update(Vector2 mousePosition) { }
    }

    public class MockActionSystem : IActionSystem
    {
        public ActionState CurrentState { get; set; } = ActionState.Normal;
        public Card? PendingCard { get; set; }
        public Site? PendingSite { get; set; }
        public bool TryStartAssassinateCalled { get; private set; }
        public bool TryStartReturnSpyCalled { get; private set; }
        public bool FinalizeSpyReturnCalled { get; private set; }
        public bool CancelTargetingCalled { get; private set; }
        public bool IsTargeting() => CurrentState != ActionState.Normal;
        public void SetCurrentPlayer(Player player) { }
        public void TryStartAssassinate()
        {
            TryStartAssassinateCalled = true;
            CurrentState = ActionState.TargetingAssassinate;
        }

        public void TryStartReturnSpy()
        {
            TryStartReturnSpyCalled = true;
            CurrentState = ActionState.TargetingReturnSpy;
        }

        public void CancelTargeting()
        {
            CancelTargetingCalled = true;
            CurrentState = ActionState.Normal;
        }
        public bool FinalizeSpyReturn(PlayerColor spyColor) { FinalizeSpyReturnCalled = true; return true; }
        public void StartTargeting(ActionState state, Card card) { }
        public bool HandleTargetClick(MapNode? targetNode, Site? targetSite) => true;
    }

    public class MockGameplayState : GameplayState
    {
        public new MockMapManager MapManager => (MockMapManager)base.MapManager;
        public new MockMarketManager MarketManager => (MockMarketManager)base.MarketManager;
        public new MockActionSystem ActionSystem => (MockActionSystem)base.ActionSystem;
        public new TurnManager TurnManager => (TurnManager)base.TurnManager;

        public MockGameplayState(Game game, IInputProvider inputProvider, ICardDatabase cardDatabase,
                                 MockMapManager mapManager, MockMarketManager marketManager, MockActionSystem actionSystem, TurnManager turnManager)
            : base(game, inputProvider, cardDatabase)
        {
            InjectDependencies(new InputManager(inputProvider), new UIManager(100, 100), mapManager, marketManager, actionSystem, turnManager);
        }
    }


    [TestClass]
    public class GameCommandsTests
    {
        private MockMapManager _mockMapManager = default!;
        private MockMarketManager _mockMarketManager = default!;
        private MockActionSystem _mockActionSystem = default!;
        private TurnManager _turnManager = default!;
        private MockGameplayState _mockState = default!;
        private Card _mockCard = default!;
        private MockCardDatabase _mockCardDatabase = default!;

        [TestInitialize]
        public void Setup()
        {
            // Base mocks for Systems
            _mockMapManager = new MockMapManager();
            _mockMarketManager = new MockMarketManager();
            _mockActionSystem = new MockActionSystem();
            _mockCardDatabase = new MockCardDatabase();

            // 1. Initialize TurnManager with TWO players
            var playerRed = new Player(PlayerColor.Red);
            var playerBlue = new Player(PlayerColor.Blue);

            // FIX 1: SEED BOTH PLAYERS' DECKS with cards (for EndTurn test to pass)
            for (int i = 0; i < 25; i++)
            {
                playerRed.Deck.Add(_mockCardDatabase.GetTestCard());
                playerBlue.Deck.Add(_mockCardDatabase.GetTestCard());
            }

            _turnManager = new TurnManager(new List<Player> { playerRed, playerBlue });

            // Mock Card
            _mockCard = new Card("test", "Test Card", 5, CardAspect.Shadow, 2, 1);

            // Mock Game context
            _mockState = new MockGameplayState(null!, new MockInputProvider(), _mockCardDatabase,
                                               _mockMapManager, _mockMarketManager, _mockActionSystem, _turnManager);

            // FIX 2: Manually draw the hand for the STARTING player (for EndTurn test's pre-assertion)
            _turnManager.ActivePlayer.DrawCards(5);
        }

        [TestMethod]
        public void BuyCardCommand_CallsMarketManagerTryBuyCard()
        {
            // Arrange
            var command = new BuyCardCommand(_mockCard);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockMarketManager.TryBuyCardCalled, "BuyCardCommand must call TryBuyCard.");
            Assert.AreEqual(_mockCard, _mockMarketManager.LastCardToBuy, "BuyCardCommand must pass the correct card to TryBuyCard.");
        }

        [TestMethod]
        public void DeployTroopCommand_CallsMapManagerTryDeploy()
        {
            // Arrange
            var mockNode = new MapNode(0, new Vector2(0, 0));
            var command = new DeployTroopCommand(mockNode);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockMapManager.TryDeployCalled, "DeployTroopCommand must call TryDeploy.");
            Assert.AreEqual(mockNode, _mockMapManager.TryDeployNode, "DeployTroopCommand must pass the correct node to TryDeploy.");
        }

        [TestMethod]
        public void ToggleMarketCommand_TogglesMarketState_ToOpen()
        {
            // Arrange
            _mockState.IsMarketOpen = false;
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockState.IsMarketOpen, "ToggleMarketCommand should open the market if it was closed.");
        }

        [TestMethod]
        public void ToggleMarketCommand_TogglesMarketState_ToClosed()
        {
            // Arrange
            _mockState.IsMarketOpen = true;
            var command = new ToggleMarketCommand();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsFalse(_mockState.IsMarketOpen, "ToggleMarketCommand should close the market if it was open.");
        }

        [TestMethod]
        public void PlayCardCommand_CallsStatePlayCard()
        {
            // Arrange
            var command = new PlayCardCommand(_mockCard);
            _mockState.TurnManager.ActivePlayer.Hand.Add(_mockCard);

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.DoesNotContain(_mockCard, _mockState.TurnManager.ActivePlayer.Hand, "PlayCardCommand should remove the card from hand.");
            Assert.Contains(_mockCard, _mockState.TurnManager.ActivePlayer.PlayedCards, "PlayCardCommand should add the card to played cards.");
        }

        [TestMethod]
        public void EndTurnCommand_CallsStateEndTurn()
        {
            // Arrange
            var command = new EndTurnCommand();
            var startingPlayer = _mockState.TurnManager.ActivePlayer;

            // Assert starting condition (Should be 5 from Setup())
            Assert.HasCount(5, startingPlayer.Hand, "Starting player must have a full hand before ending turn.");

            // Act
            command.Execute(_mockState);

            // Assert
            // 1. Player Switch Check
            Assert.AreNotEqual(startingPlayer, _mockState.TurnManager.ActivePlayer, "EndTurnCommand should change the ActivePlayer.");

            // 2. New Player Draws Check
            Assert.HasCount(5, _mockState.TurnManager.ActivePlayer.Hand, "EndTurnCommand should cause the new player to draw 5 cards.");
        }

        [TestMethod]
        public void StartAssassinateCommand_SwitchesToTargetingInput()
        {
            // Arrange
            var command = new StartAssassinateCommand();
            _mockState.SwitchToNormalMode(); // Ensure we start in Normal

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.TryStartAssassinateCalled, "Backend system must be notified.");
            Assert.IsInstanceOfType(_mockState.InputMode, typeof(TargetingInputMode),
                "Command must switch InputMode to Targeting so user can click victims.");
        }

        [TestMethod]
        public void StartReturnSpyCommand_SwitchesToTargetingInput()
        {
            // Arrange
            var command = new StartReturnSpyCommand();
            _mockState.SwitchToNormalMode();

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.TryStartReturnSpyCalled, "Backend system must be notified.");
            Assert.IsInstanceOfType(_mockState.InputMode, typeof(TargetingInputMode),
                "Command must switch InputMode to Targeting so user can select a spy.");
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
        public void CancelActionCommand_ResetsToNormalInput()
        {
            // Arrange
            var command = new CancelActionCommand();

            // 1. Create a dummy UIManager for the test.
            // Since UIManager is logic-only (math), we can safely instantiate it without a GraphicsDevice.
            var dummyUI = new UIManager(800, 600);

            // 2. Force the mock into a "Targeting" state.
            // We instantiate TargetingInputMode with the new 6-argument constructor.
            _mockState.InputMode = new TargetingInputMode(
                _mockState,
                new InputManager(new MockInputProvider()),
                dummyUI,
                _mockMapManager,
                _turnManager,
                _mockActionSystem
            );

            // Act
            command.Execute(_mockState);

            // Assert
            Assert.IsTrue(_mockActionSystem.CancelTargetingCalled, "Backend targeting must be cancelled.");
            Assert.IsInstanceOfType(_mockState.InputMode, typeof(NormalPlayInputMode),
                "Command must restore Normal Input Mode so the game continues.");
        }
    }
}