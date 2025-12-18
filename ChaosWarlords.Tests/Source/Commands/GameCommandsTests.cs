using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using ChaosWarlords.Source.States.Input;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Tests.Source.Commands
{
    [TestClass]
    public class GameCommandsTests
    {
        // ------------------------------------------------------------------------
        // 1. DEDICATED MOCKS (Tailored for GameCommand logic)
        // ------------------------------------------------------------------------

        private class MockMapManager : IMapManager
        {
            // Verification Flags
            public bool TryDeployCalled { get; private set; }
            public MapNode? LastDeployTarget { get; private set; }

            // Implementation
            public bool TryDeploy(Player currentPlayer, MapNode targetNode)
            {
                TryDeployCalled = true;
                LastDeployTarget = targetNode;
                return true; // Simulate success
            }

            // Stubs
            public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
            public IReadOnlyList<Site> Sites { get; } = new List<Site>();
            public void CenterMap(int w, int h) { }
            public Site GetSiteForNode(MapNode n) => null!;
            public MapNode GetNodeAt(Vector2 p) => null!;
            public Site GetSiteAt(Vector2 p) => null!;
            public void DistributeControlRewards(Player p) { }
            public List<PlayerColor> GetEnemySpiesAtSite(Site s, Player p) => new List<PlayerColor>();
        }

        private class MockMarketManager : IMarketManager
        {
            // Verification Flags
            public bool TryBuyCardCalled { get; private set; }
            public Card? LastCardBought { get; private set; }

            // Implementation
            public bool TryBuyCard(Player player, Card card)
            {
                TryBuyCardCalled = true;
                LastCardBought = card;
                return true;
            }

            // Stubs
            public List<Card> MarketRow { get; } = new List<Card>();
            public void Update(Vector2 mousePos) { }
            public void BuyCard(Player p, Card c) { }
            public void RefillMarket(List<Card> deck) { }
        }

        private class MockActionSystem : IActionSystem
        {
            // Verification Flags
            public bool FinalizeSpyReturnCalled { get; private set; }
            public bool CancelTargetingCalled { get; private set; }
            public PlayerColor? LastFinalizedSpyColor { get; private set; }

            // Implementation
            public void FinalizeSpyReturn(PlayerColor spyColor)
            {
                FinalizeSpyReturnCalled = true;
                LastFinalizedSpyColor = spyColor;
            }

            public void CancelTargeting()
            {
                CancelTargetingCalled = true;
            }

            // Stubs
            public ActionState CurrentState { get; set; } = ActionState.Normal;
            public Card? PendingCard { get; }
            public Site? PendingSite { get; }
            public event EventHandler? OnActionCompleted;
            public event EventHandler<string>? OnActionFailed;
            public void HandleTargetClick(MapNode n, Site s) { }
            public bool IsTargeting() => false;
            public void SetCurrentPlayer(Player p) { }
            public void StartTargeting(ActionState s, Card c) { }
            public void TryStartAssassinate() { }
            public void TryStartReturnSpy() { }

            // Helper to satisfy unused event warnings
            public void RaiseActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
            public void RaiseActionFailed(string s) => OnActionFailed?.Invoke(this, s);
        }

        private class MockGameplayState : IGameplayState
        {
            // Dependencies
            public IMapManager MapManager { get; }
            public IMarketManager MarketManager { get; }
            public IActionSystem ActionSystem { get; }
            public TurnManager TurnManager { get; }

            // State Flags for Verification
            public bool ToggleMarketCalled { get; private set; }
            public bool CloseMarketCalled { get; private set; }
            public bool SwitchToNormalModeCalled { get; private set; }
            public bool SwitchToTargetingModeCalled { get; private set; }

            // Properties
            public bool IsMarketOpen { get; set; }

            public MockGameplayState(IMapManager map, IMarketManager market, IActionSystem action, TurnManager turn)
            {
                MapManager = map;
                MarketManager = market;
                ActionSystem = action;
                TurnManager = turn;
            }

            // Command Logic Implementations
            public void ToggleMarket()
            {
                ToggleMarketCalled = true;
                IsMarketOpen = !IsMarketOpen;
            }

            public void CloseMarket()
            {
                CloseMarketCalled = true;
                IsMarketOpen = false;
            }

            public void SwitchToNormalMode()
            {
                SwitchToNormalModeCalled = true;
            }

            public void SwitchToTargetingMode()
            {
                SwitchToTargetingModeCalled = true;
            }

            // Stubs (Unused by these specific commands)
            public InputManager InputManager => null!;
            public IUISystem UIManager => null!;
            public IInputMode InputMode { get; set; } = null!;
            public int HandY => 0;
            public int PlayedY => 0;
            public void EndTurn() { }
            public void PlayCard(Card card) { }
            public void ResolveCardEffects(Card card) { }
            public void MoveCardToPlayed(Card card) { }
            public void ArrangeHandVisuals() { }
            public string GetTargetingText(ActionState state) => "";
            public void LoadContent() { }
            public void UnloadContent() { }
            public void Update(GameTime gameTime) { }
            public void Draw(object spriteBatch) { } // Object to avoid strict SpriteBatch dependency if needed

            public void Draw(SpriteBatch spriteBatch)
            {
                throw new NotImplementedException();
            }
        }

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

            _mockState = new MockGameplayState(
                _mockMap,
                _mockMarket,
                _mockAction,
                _turnManager
            );
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