using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Core.Interfaces.Data;

namespace ChaosWarlords.Tests.Source.Doubles.State
{
    public class TestGameplayState : IGameplayState
    {
        public List<IGameCommand> ExecutedCommands { get; } = new();

        public IInputManager InputManager { get; set; } = NSubstitute.Substitute.For<IInputManager>();
        public IGameLogger Logger { get; set; }
        public IUIManager UIManager { get; set; } = NSubstitute.Substitute.For<IUIManager>();
        public IGameplayView? View { get; set; }
        public IMapManager MapManager { get; set; } = NSubstitute.Substitute.For<IMapManager>();
        public IMarketManager MarketManager { get; set; } = NSubstitute.Substitute.For<IMarketManager, IMarketStateManager>();
        public IActionSystem ActionSystem { get; set; } = NSubstitute.Substitute.For<IActionSystem>();
        public ITurnManager TurnManager { get; set; } = NSubstitute.Substitute.For<ITurnManager>();
        public MatchContext MatchContext { get; set; } = null!; // Concrete class, keep null or manual
        public IMatchManager MatchManager { get; set; } = NSubstitute.Substitute.For<IMatchManager>();

        public IInputMode InputMode { get; set; } = null!;
        public bool IsMarketOpen => MarketStateManager.IsOpen;
        public bool IsConfirmationPopupOpen { get; set; }
        public bool IsPauseMenuOpen { get; set; }
        public bool IsOptionalEffectPopupOpen { get; set; }
        public int HandY { get; set; }
        public int PlayedY { get; set; }

        // Test Helpers
        public Card? HoveredHandCard { get; set; }
        public Card? HoveredPlayedCard { get; set; }
        public Card? HoveredMarketCard { get; set; }

        public TestGameplayState()
        {
            // Defaults to avoid null refs if not set
            Logger = Tests.Utilities.TestLogger.Instance;

            // Auto-wire MatchContext with current mocks
            // Note: If tests replace mocks later, they might need to update MatchContext or we use lazy properties
            // But usually tests set mocks BEFORE usage.
            // For now, let's just initialize it with current properties.
            // However, properties like TurnManager are initialized inline.
            InitializeMatchContext();
        }

        public void InitializeMatchContext()
        {
            // Create a dummy PlayerStateManager if needed because it's not in the property list of TestGameplayState explicitly as an interface?
            // Actually IGameplayState doesn't have PlayerStateManager anymore (it was removed).
            // But MatchContext needs one.
            var playerState = new PlayerStateManager(Logger);

            MatchContext = new MatchContext(
                TurnManager,
                MapManager,
                MarketManager,
                ActionSystem,
                NSubstitute.Substitute.For<ICardDatabase>(),
                playerState,
                NSubstitute.Substitute.For<ChaosWarlords.Source.Core.Interfaces.Services.IUIEventMediator>(),
                Logger
            );
            MatchContext.MatchManager = MatchManager;
        }

        public void RecordAndExecuteCommand(IGameCommand command)
        {
            ExecutedCommands.Add(command);
            command.Execute(MatchContext);
        }

        // --- Interaction Helpers ---
        // --- Interaction Helpers ---
        public Card? GetHoveredHandCard() => HoveredHandCard;
        public Card? GetHoveredPlayedCard() => HoveredPlayedCard;
        public Card? GetHoveredMarketCard() => HoveredMarketCard;
        public Card? GetHoveredBrowserCard() => HoveredBrowserCard;

        public Card? HoveredBrowserCard { get; set; }

        // --- Unused / NotImplemented for basic tests ---
        public void LoadContent() { }
        public bool TestCanEndTurnResult { get; set; } = true;
        public bool EndTurnCalled { get; private set; }

        public bool CanEndTurn(out string reason)
        {
            reason = "";
            return TestCanEndTurnResult;
        }

        public void EndTurn() { EndTurnCalled = true; }

        public IMarketStateManager MarketStateManager { get; set; } = new MarketStateManager(Tests.Utilities.TestLogger.Instance);

        public string ActiveModeName { get; set; } = "None"; // For testing mode switches
        public void SwitchToTargetingMode() { ActiveModeName = "Targeting"; }
        public void SwitchToNormalMode() { ActiveModeName = "Normal"; }
        public void SwitchToPromoteMode(int amount) { ActiveModeName = "Promote"; }
        public bool EscapeHandled { get; private set; }
        public bool EndTurnRequested { get; private set; }

        public void HandleEscapeKeyPress() { EscapeHandled = true; }
        public void HandleEndTurnKeyPress() { EndTurnRequested = true; }
        public void PlayCard(Card card)
        {
            // For State-Based Verification
            // 1. Delegate to MatchManager if it exists (mimicking real state)
            MatchManager?.PlayCard(card);

            // 2. Or just track it locally if MatchManager isn't critical for the specific test
            // But usually PlayCard involves moving card from Hand to Played
            MoveCardToPlayed(card);
        }

        public void MoveCardToPlayed(Card card)
        {
            // Verify this method is called by PlayCard
        }
        public bool HasViableTargets(Card card) => true;
        public string GetTargetingText(ActionState state) => "Test Targeting";

        // IState Implementation
        public void UnloadContent() { }
        public void Update(GameTime gameTime) { }
        public void Draw() { } // Optional, not in interface but harmless
    }
}
