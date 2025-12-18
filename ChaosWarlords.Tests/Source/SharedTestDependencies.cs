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
    // These are public so they can be used by GameplayStateTests.cs 
    // and other integration-style tests.
    // ------------------------------------------------------------------------

    public class MockActionSystem : IActionSystem
    {
        public ActionState CurrentState { get; set; } = ActionState.Normal;
        public Card? PendingCard { get; set; }
        public Site? PendingSite { get; set; }
        public bool CancelTargetingCalled { get; private set; }

        public event EventHandler? OnActionCompleted;
        public event EventHandler<string>? OnActionFailed;

        // Helper to simulate events in tests
        public void SimulateActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
        public void SimulateActionFailed(string reason) => OnActionFailed?.Invoke(this, reason);

        // Interface Implementation
        public void CancelTargeting() { CurrentState = ActionState.Normal; CancelTargetingCalled = true; }
        public void FinalizeSpyReturn(PlayerColor spyColor) { }
        public void HandleTargetClick(MapNode node, Site site) { }
        public bool IsTargeting() => CurrentState != ActionState.Normal;
        public void SetCurrentPlayer(Player p) { }
        public void StartTargeting(ActionState state, Card card) { CurrentState = state; }
        public void TryStartAssassinate() { }
        public void TryStartReturnSpy() { }
        public void Reset() { CurrentState = ActionState.Normal; PendingCard = null; PendingSite = null; CancelTargetingCalled = false; }
    }

    public class MockMapManager : IMapManager
    {
        public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
        public IReadOnlyList<Site> Sites { get; } = new List<Site>();

        public void CenterMap(int width, int height) { }
        public void DistributeControlRewards(Player activePlayer) { }
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer) => new List<PlayerColor>();
        public MapNode GetNodeAt(Vector2 position) => null!;
        public Site GetSiteAt(Vector2 position) => null!;
        public Site GetSiteForNode(MapNode node) => null!;
        public bool TryDeploy(Player currentPlayer, MapNode targetNode) => true;
    }

    public class MockMarketManager : IMarketManager
    {
        public List<Card> MarketRow { get; } = new List<Card>();
        public void BuyCard(Player p, Card c) { }
        public void RefillMarket(List<Card> deck) { }
        public bool TryBuyCard(Player player, Card card) => true;
        public void Update(Vector2 mousePos) { }
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
        public InputManager InputManager => null!;
        public IUISystem UIManager => null!;
        public IMapManager MapManager => null!;
        public IMarketManager MarketManager => null!;
        public TurnManager TurnManager => null!;
        public IInputMode InputMode { get; set; } = null!;
        public bool IsMarketOpen { get; set; }
        public int HandY => 0;
        public int PlayedY => 0;
        public void PlayCard(Card card) { }
        public void SwitchToTargetingMode() { }
        public void ToggleMarket() { }
        public void CloseMarket() { }
        public void EndTurn() { }
        public void ArrangeHandVisuals() { }
        public string GetTargetingText(ActionState state) => "";
        public void LoadContent() { }
        public void UnloadContent() { }
        public void Update(GameTime gameTime) { }
        public void Draw(SpriteBatch spriteBatch) { }
    }
}