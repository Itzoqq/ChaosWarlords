using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests
{
    // ------------------------------------------------------------------------
    // SHARED MOCKS
    // These are public so they can be used by integration-style tests.
    // ------------------------------------------------------------------------

    public class MockActionSystem : IActionSystem
    {
        public ActionState CurrentState { get; set; } = ActionState.Normal;
        public Card? PendingCard { get; set; }
        public Site? PendingSite { get; set; }
        public bool HandleTargetClickCalled { get; private set; }
        public MapNode? ClickedNode { get; private set; }
        public Site? ClickedSite { get; private set; }

        public event EventHandler? OnActionCompleted;
        public event EventHandler<string>? OnActionFailed;

        // Helper to simulate events in tests
        public void SimulateActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
        public void SimulateActionFailed(string reason) => OnActionFailed?.Invoke(this, reason);

        // Interface Implementation
        public void CancelTargeting() { CurrentState = ActionState.Normal; }
        public void FinalizeSpyReturn(PlayerColor spyColor) { }
        public void HandleTargetClick(MapNode node, Site site)
        {
            HandleTargetClickCalled = true;
            ClickedNode = node;
            ClickedSite = site;
        }
        public bool IsTargeting() => CurrentState != ActionState.Normal;
        public void SetCurrentPlayer(Player p) { }
        public void StartTargeting(ActionState state, Card card) { CurrentState = state; }
        public void TryStartAssassinate() { }
        public void TryStartReturnSpy() { }
        public void Reset() { CurrentState = ActionState.Normal; PendingCard = null; PendingSite = null; }
    }

    public class MockMapManager : IMapManager
    {
        public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
        public IReadOnlyList<Site> Sites { get; } = new List<Site>();
        public MapNode? NodeToReturn { get; set; }
        public Site? SiteToReturn { get; set; }
        public List<PlayerColor> SpiesToReturn { get; set; } = new List<PlayerColor>();
        public void CenterMap(int width, int height) { }
        public void DistributeControlRewards(Player activePlayer) { }
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer) => SpiesToReturn;
        public MapNode GetNodeAt(Vector2 position) => NodeToReturn!;
        public Site GetSiteAt(Vector2 position) => SiteToReturn!;
        public Site GetSiteForNode(MapNode node) => null!;
        public bool TryDeploy(Player currentPlayer, MapNode targetNode)
        {
            return true;
        }
        public void RecalculateSiteState(Site site, Player activePlayer) { }
        public bool TryBuyCard(Player player, Card card) { return true; }
        // Ensure all interface methods are implemented (stubbed)
        public bool HasPresence(MapNode targetNode, PlayerColor player) => true;
        public bool CanAssassinate(MapNode target, Player attacker) => true;
        public bool CanDeployAt(MapNode targetNode, PlayerColor player) => true;
        public void Assassinate(MapNode node, Player attacker) { }
        public void ReturnTroop(MapNode node, Player requestingPlayer) { }
        public void PlaceSpy(Site site, Player player) { }
        public bool ReturnSpy(Site site, Player activePlayer) => true;
        public void Supplant(MapNode node, Player attacker) { }
        public bool ReturnSpecificSpy(Site site, Player activePlayer, PlayerColor targetSpyColor) => true;
    }

    public class MockMarketManager : IMarketManager
    {
        public List<Card> MarketRow { get; } = new List<Card>();
        public bool UpdateCalled { get; private set; }
        public bool TryBuyCardCalled { get; private set; }
        public void BuyCard(Player p, Card c) { }
        public void InitializeDeck(List<Card> allCards) { }
        public void RefillMarket() { }
        public bool TryBuyCard(Player player, Card card) { return true; }
        public void Update(Vector2 mousePos)
        {
            UpdateCalled = true;
            foreach (var card in MarketRow)
            {
                if (card.Bounds.Contains(mousePos))
                    card.IsHovered = true;
                else
                    card.IsHovered = false;
            }
        }
    }

    public class MockCardDatabase : ICardDatabase
    {
        public List<Card> MarketDeckToReturn { get; set; } = new List<Card>();

        public void Load(Stream stream) { } // Stub
        public List<Card> GetAllMarketCards()
        {
            return MarketDeckToReturn;
        }

        public Card? GetCardById(string id)
        {
            return null;
        }
    }

    public class MockInputProvider : IInputProvider
    {
        // Backing fields
        public MouseState MouseState { get; private set; }
        public KeyboardState KeyboardState { get; private set; }

        public MockInputProvider()
        {
            // Initialize with default (Released, 0,0) states
            MouseState = new MouseState();
            KeyboardState = new KeyboardState();
        }

        // Interface Implementation
        public MouseState GetMouseState() => MouseState;
        public KeyboardState GetKeyboardState() => KeyboardState;

        // --- Helper Methods for Tests ---

        public void QueueRightClick()
        {
            MouseState = new MouseState(
                MouseState.X,
                MouseState.Y,
                MouseState.ScrollWheelValue,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Pressed, // Right Click
                ButtonState.Released,
                ButtonState.Released
            );
        }

        public void QueueLeftClick()
        {
            MouseState = new MouseState(
                MouseState.X,
                MouseState.Y,
                MouseState.ScrollWheelValue,
                ButtonState.Pressed, // Left Click
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released
            );
        }

        public void SetMousePosition(int x, int y)
        {
            // Preserve button state if needed, but for simple moves, reset works
            MouseState = new MouseState(
                x,
                y,
                MouseState.ScrollWheelValue,
                MouseState.LeftButton,
                MouseState.MiddleButton,
                MouseState.RightButton,
                MouseState.XButton1,
                MouseState.XButton2
            );
        }

        public void Reset()
        {
            MouseState = new MouseState();
            KeyboardState = new KeyboardState();
        }

        public void SetKeyboardState(params Keys[] keys)
        {
            KeyboardState = new KeyboardState(keys);
        }

        public void SetMouseState(MouseState state)
        {
            MouseState = state;
        }
    }

    public class MockUISystem : IUISystem
    {
        public bool IsMarketHovered { get; set; } = false;
        public bool IsAssassinateHovered { get; set; } = false;
        public bool IsReturnSpyHovered { get; set; } = false;
        public int ScreenWidth { get; } = 800;
        public int ScreenHeight { get; } = 600;
        public Rectangle MarketButtonRect { get; } = Rectangle.Empty;
        public Rectangle AssassinateButtonRect { get; } = Rectangle.Empty;
        public Rectangle ReturnSpyButtonRect { get; } = Rectangle.Empty;

        public event EventHandler? OnMarketToggleRequest;
        public event EventHandler? OnAssassinateRequest;
        public event EventHandler? OnReturnSpyRequest;

        public void RaiseMarketToggle() => OnMarketToggleRequest?.Invoke(this, EventArgs.Empty);
        public void RaiseAssassinateRequest() => OnAssassinateRequest?.Invoke(this, EventArgs.Empty);
        public void RaiseReturnSpyRequest() => OnReturnSpyRequest?.Invoke(this, EventArgs.Empty);
        public void Update(InputManager input) { }
    }
}