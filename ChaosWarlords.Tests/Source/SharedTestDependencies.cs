using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.States.Input;
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
        public bool CancelTargetingCalled { get; private set; }
        public bool FinalizeSpyReturnCalled { get; private set; }
        public PlayerColor? LastFinalizedSpyColor { get; private set; }
        public bool HandleTargetClickCalled { get; private set; }
        public MapNode? ClickedNode { get; private set; }
        public Site? ClickedSite { get; private set; }

        public event EventHandler? OnActionCompleted;
        public event EventHandler<string>? OnActionFailed;

        // Helper to simulate events in tests
        public void SimulateActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
        public void SimulateActionFailed(string reason) => OnActionFailed?.Invoke(this, reason);

        // Interface Implementation
        public void CancelTargeting() { CurrentState = ActionState.Normal; CancelTargetingCalled = true; }
        public void FinalizeSpyReturn(PlayerColor spyColor) { FinalizeSpyReturnCalled = true; LastFinalizedSpyColor = spyColor; }
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
        public void Reset() { CurrentState = ActionState.Normal; PendingCard = null; PendingSite = null; CancelTargetingCalled = false; FinalizeSpyReturnCalled = false; LastFinalizedSpyColor = null; }
    }

    public class MockMapManager : IMapManager
    {
        public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
        public IReadOnlyList<Site> Sites { get; } = new List<Site>();

        public MapNode? NodeToReturn { get; set; }
        public Site? SiteToReturn { get; set; }
        public List<PlayerColor> SpiesToReturn { get; set; } = new List<PlayerColor>();

        public bool TryDeployCalled { get; private set; }
        public MapNode? LastDeployTarget { get; private set; }

        public void CenterMap(int width, int height) { }
        public void DistributeControlRewards(Player activePlayer) { }
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer) => SpiesToReturn;
        public MapNode GetNodeAt(Vector2 position) => NodeToReturn!;
        public Site GetSiteAt(Vector2 position) => SiteToReturn!;
        public Site GetSiteForNode(MapNode node) => null!;
        public bool TryDeploy(Player currentPlayer, MapNode targetNode)
        {
            TryDeployCalled = true;
            LastDeployTarget = targetNode;
            return true;
        }
    }

    public class MockMarketManager : IMarketManager
    {
        public List<Card> MarketRow { get; } = new List<Card>();
        public bool UpdateCalled { get; private set; }
        public bool TryBuyCardCalled { get; private set; }
        public Card? LastCardBought { get; private set; }

        public void BuyCard(Player p, Card c) { }
        public void RefillMarket(List<Card> deck) { }
        public bool TryBuyCard(Player player, Card card) { TryBuyCardCalled = true; LastCardBought = card; return true; }
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
        public List<Card> GetAllMarketCards()
        {
            return new List<Card>(); // Return empty list for tests
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

    public class MockGameplayState : IGameplayState
    {
        public IActionSystem ActionSystem { get; }

        // Verification Flags
        public bool ResolveCardEffectsCalled { get; private set; }
        public bool MoveCardToPlayedCalled { get; private set; }
        public bool SwitchToNormalModeCalled { get; private set; }
        public bool ToggleMarketCalled { get; private set; }
        public bool CloseMarketCalled { get; private set; }
        public bool SwitchToTargetingModeCalled { get; private set; }
        public Card? LastResolvedCard { get; private set; }
        public Card? LastMovedCard { get; private set; }

        public MockGameplayState(IActionSystem actionSystem)
        {
            ActionSystem = actionSystem;
        }

        public void ResolveCardEffects(Card card)
        {
            ResolveCardEffectsCalled = true;
            LastResolvedCard = card;
        }

        public void MoveCardToPlayed(Card card)
        {
            MoveCardToPlayedCalled = true;
            LastMovedCard = card;
        }

        public void SwitchToNormalMode()
        {
            SwitchToNormalModeCalled = true;
        }

        // Stubs
        public InputManager InputManager { get; set; } = null!;
        public IUISystem UIManager { get; set; } = null!;
        public IMapManager MapManager { get; set; } = null!;
        public IMarketManager MarketManager { get; set; } = null!;
        public TurnManager TurnManager { get; set; } = null!;
        public IInputMode InputMode { get; set; } = null!;
        public bool IsMarketOpen { get; set; }
        public int HandY => 0;
        public int PlayedY => 0;
        public void PlayCard(Card card) { }
        public void SwitchToTargetingMode() { SwitchToTargetingModeCalled = true; }
        public void ToggleMarket() { ToggleMarketCalled = true; IsMarketOpen = !IsMarketOpen; }
        public void CloseMarket() { CloseMarketCalled = true; IsMarketOpen = false; }
        public void EndTurn() { }
        public void ArrangeHandVisuals() { }
        public string GetTargetingText(ActionState state) => "";
        public void LoadContent() { }
        public void UnloadContent() { }
        public void Update(GameTime gameTime) { }
        public void Draw(SpriteBatch spriteBatch) { }
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