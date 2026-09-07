using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Composition;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Mechanics.Actions;
using ChaosWarlords.Source.Rendering;
using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Tests.Source.Doubles.Input;
using ChaosWarlords.Tests.Source.Doubles.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Tests.Source.Functional
{
    /// <summary>
    /// Reusable functional/scenario test harness for the CLIENT input-dispatch layer - the
    /// input-layer sibling of MatchScenario (see that class's own doc comment). Wires the REAL
    /// InputManager, GameplayInputCoordinator, PlayerController, UIManager, UIEventMediator and
    /// ReplayController together, against a REAL (MatchFactory-built, non-mocked)
    /// ActionSystem/MatchContext (via an internally-held MatchScenario) - the same subscription
    /// order production's own composition root (GameplayState.InitializeSystems) uses. The only
    /// two things this harness fakes are the literal hardware boundary (IInputProvider - nothing
    /// can move a real mouse in a test process) and the rendering boundary (IGameplayView -
    /// rendering itself is deliberately out of scope for this project's test suite, see
    /// planning.txt), via FakeInputProvider/FakeGameplayView. Everything else is production code.
    ///
    /// This exists to close a gap MatchScenario cannot: MatchScenario proves a CARD's effect
    /// resolves correctly once PlayCardCommand reaches CommandDispatcher, but several real,
    /// shipped bugs (the global Escape/Right-click/Enter bypass family, Return Spy silently
    /// non-functional with 2+ enemy spy colors, players permanently stuck in TargetingInputMode
    /// after a cancel) were never card bugs at all - they were bugs in HOW A CLICK OR KEYPRESS
    /// GETS ROUTED, one layer before any card's effect logic runs, and only reproduce when 2+
    /// real classes are wired together reacting to the SAME shared InputManager.OnInputEvent
    /// stream. Isolated unit tests mocking each class's collaborators (correct, standard
    /// practice) cannot catch this class of bug by construction - see planning.txt's TEST
    /// INFRASTRUCTURE section for the full writeup and InputPipelineScenarioRegressionTests.cs
    /// for these bugs pinned as regression tests through this harness.
    ///
    /// Standing policy: any new class that subscribes to a shared/multicast event (InputManager.
    /// OnInputEvent today), or any new IInputMode, needs at least one test through this harness
    /// before it ships - not just its own isolated unit test with mocked collaborators.
    ///
    /// Usage sketch:
    ///   var scenario = InputPipelineScenario.Build();
    ///   var red = scenario.AsActivePlayer(PlayerColor.Red);
    ///   var card = scenario.GiveCard(PlayerColor.Red, "core_house_guard");
    ///   scenario.ClickHandCard(card);
    ///   ... assert on scenario.Context / scenario.CurrentMode ...
    /// </summary>
    public sealed class InputPipelineScenario
    {
        public const int ScreenWidth = 1920;
        public const int ScreenHeight = 1080;

        /// <summary>
        /// A click position guaranteed to miss every real UI button rect (all anchored to
        /// screen edges within [0, ScreenWidth] x [0, ScreenHeight]) and every hover-based
        /// ViewModel lookup this scenario doesn't itself populate - used for clicks whose
        /// target is resolved by hover state (ClickHandCard etc.), not by screen position.
        /// </summary>
        private const int OffScreen = -10000;

        private readonly FakeInputProvider _inputProvider;
        private readonly FakeGameplayView _view;
        private readonly ScenarioGameplayState _state;

        public MatchScenario Match { get; }
        public MatchContext Context => Match.Context;
        public IGameplayState State => _state;
        public IInputMode CurrentMode => _state.InputMode;
        public IUIManager UIManager => _state.UIManager;
        public bool IsMarketOpen => _state.IsMarketOpen;
        public bool IsPauseMenuOpen => _state.IsPauseMenuOpen;
        public bool IsConfirmationPopupOpen => _state.IsConfirmationPopupOpen;
        public FakeGameplayView View => _view;

        private InputPipelineScenario(int? seed, IReadOnlyList<PlayerColor>? playerColors)
        {
            Match = MatchScenario.Build(seed, playerColors);
            _inputProvider = new FakeInputProvider();
            _view = new FakeGameplayView();

            var logger = ChaosWarlords.Tests.Utilities.TestLogger.Instance;
            _state = new ScenarioGameplayState(_inputProvider, _view, Match.ReplayManager, Match.CardDatabase, logger);
            _state.WireToRealMatch(Match, logger);
        }

        public static InputPipelineScenario Build(int? seed = 20260907, IReadOnlyList<PlayerColor>? playerColors = null) =>
            new(seed, playerColors);

        public Player Player(PlayerColor color) => Match.Player(color);
        public Player AsActivePlayer(PlayerColor color) => Match.AsActivePlayer(color);
        public Card GiveCard(PlayerColor color, string cardId) => Match.GiveCard(color, cardId);
        public void Dispatch(IGameCommand command) => Match.Dispatch(command);

        // ---- Frame simulation ----

        /// <summary>
        /// Advances <paramref name="frames"/> real game-loop frames via the actual
        /// GameplayState.Update(GameTime) - the same method Game1 calls every frame in
        /// production, reused as-is rather than reimplemented, so cooldown counters
        /// (DevourInputMode/MarketInputMode/PromoteFromPileInputMode's own CooldownFrames) and
        /// popup-blocking behave identically to a real session.
        /// </summary>
        public void Tick(int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                _state.Update(new GameTime());
            }
        }

        // ---- Raw input simulation ----

        public void Click(Vector2 position) => Click((int)position.X, (int)position.Y);

        public void Click(int x, int y)
        {
            _inputProvider.SetMouse(x, y, leftDown: true);
            Tick();
            _inputProvider.SetMouse(x, y, leftDown: false);
            Tick();
        }

        public void RightClick(int x, int y)
        {
            _inputProvider.SetMouse(x, y, rightDown: true);
            Tick();
            _inputProvider.SetMouse(x, y, rightDown: false);
            Tick();
        }

        public void PressKey(Keys key)
        {
            _inputProvider.SetKey(key, down: true);
            Tick();
            _inputProvider.SetKey(key, down: false);
            Tick();
        }

        // ---- Hover-based card clicks (position is irrelevant - real IInputMode
        // implementations resolve these via IGameplayState.GetHoveredXCard(), not screen
        // position - see NormalPlayInputMode.HandleCardClick) ----

        public void ClickHandCard(Card card)
        {
            _view.HandViewModels.Clear();
            _view.HandViewModels.Add(new CardViewModel(card) { IsHovered = true });
            Click(OffScreen, OffScreen);
        }

        public void ClickMarketCard(Card card)
        {
            _view.MarketViewModels.Clear();
            _view.MarketViewModels.Add(new CardViewModel(card) { IsHovered = true });
            Click(OffScreen, OffScreen);
        }

        public void ClickBrowserCard(Card card)
        {
            _view.BrowserViewModels.Clear();
            _view.BrowserViewModels.Add(new CardViewModel(card) { IsHovered = true });
            Click(OffScreen, OffScreen);
        }

        /// <summary>Plays a card through the real click path (ClickHandCard), not a shortcut.</summary>
        public void PlayCard(Card card) => ClickHandCard(card);

        // ---- Map clicks (real screen-position hit-testing, via the real IMapManager) ----

        public void ClickNode(MapNode node) => Click(node.Position.ToVector2());

        public void ClickSite(Site site)
        {
            var rect = site.Bounds.ToRectangle();
            Click(rect.Center.X, rect.Center.Y);
        }

        // ---- Main Game UI button clicks (real rects, read straight off the real UIManager) ----

        public void ClickMarketButton() => ClickCenterOf(_state.UIManager.MarketButtonRect);
        public void ClickAssassinateButton() => ClickCenterOf(_state.UIManager.AssassinateButtonRect);
        public void ClickReturnSpyButton() => ClickCenterOf(_state.UIManager.ReturnSpyButtonRect);
        public void ClickEndTurnButton() => ClickCenterOf(_state.UIManager.EndTurnButtonRect);
        public void ClickDeclineRepeatButton() => ClickCenterOf(_state.UIManager.DeclineRepeatButtonRect);

        private void ClickCenterOf(Rectangle rect) => Click(rect.Center.X, rect.Center.Y);

        // ---- Spy-return / opponent-select button clicks ----

        /// <summary>
        /// Clicks the on-screen button for returning <paramref name="spyColor"/>'s spy at
        /// <paramref name="site"/> - the real end-to-end path (PlayerController's own
        /// InteractionMapper-based hit-test, not ActionSystem.FinalizeSpyReturn called
        /// directly). Requires ActionSystem.CurrentState == SelectingSpyToReturn and
        /// PendingSite == site already (see ClickReturnSpyButton).
        /// </summary>
        public void ClickSpyReturnButton(PlayerColor spyColor, Site site)
        {
            int index = site.Spies.ToList().IndexOf(spyColor);
            if (index < 0)
            {
                throw new InvalidOperationException($"{spyColor} has no spy at site '{site.Name}'.");
            }
            ClickCenterOf(ComputeSideButtonRect(index));
        }

        /// <summary>
        /// Clicks the on-screen button for selecting <paramref name="opponentColor"/> - mirrors
        /// PlayerController.HandleOpponentSelectionInput's own InteractionMapper-based hit-test,
        /// including its eligibility gate (PendingCard's SelectOpponent effect Amount as the
        /// hand-size threshold - see GetSelectOpponentThreshold) - a click on a real but
        /// ineligible opponent's button is a legitimate real no-op production also produces, so
        /// this throws rather than silently clicking through it, the same way ClickSpyReturnButton
        /// throws for a color with no spy at the site. Requires ActionSystem.CurrentState ==
        /// TargetingOpponentSelect already.
        /// </summary>
        public void ClickOpponentSelectButton(PlayerColor opponentColor)
        {
            var activePlayer = Context.ActivePlayer;
            var eligible = Context.TurnManager.Players.Where(p => p != activePlayer).ToList();
            int index = eligible.FindIndex(p => p.Color == opponentColor);
            if (index < 0)
            {
                throw new InvalidOperationException($"{opponentColor} is not a player in this match.");
            }

            int threshold = GetSelectOpponentThreshold(Context.ActionSystem.PendingCard);
            if (eligible[index].Hand.Count <= threshold)
            {
                throw new InvalidOperationException(
                    $"{opponentColor} does not meet the eligibility threshold (Hand.Count={eligible[index].Hand.Count} <= {threshold}) - " +
                    "PlayerController.HandleOpponentSelectionInput would reject this click as a real no-op.");
            }

            ClickCenterOf(ComputeSideButtonRect(index));
        }

        /// <summary>Mirrors PlayerController.GetSelectOpponentThreshold exactly.</summary>
        private static int GetSelectOpponentThreshold(Card? sourceCard)
        {
            if (sourceCard == null) return 0;
            var effect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.SelectOpponent);
            return effect?.Amount ?? 0;
        }

        /// <summary>
        /// Mirrors InteractionMapper.GetClickedSpyReturnButton/GetClickedOpponentSelectButton's
        /// geometry exactly - both must stay in sync with the real drawing code (see their own
        /// doc comments), duplicated here because InteractionMapper only exposes a hit-test
        /// function taking a point, not the rects themselves. NOTE: like the production
        /// geometry it mirrors, this is anchored near screen-center (x ~ (ScreenWidth-200)/2,
        /// y >= 240) - avoid placing a real map node there in a test that also relies on
        /// ClickNode/ClickSite, or the two could collide (see planning.txt's LATENT UIManager/
        /// geometry finding for the general shape of this risk).
        /// </summary>
        private Rectangle ComputeSideButtonRect(int index)
        {
            int screenWidth = _state.UIManager.ScreenWidth;
            const float headerWidth = 200;
            int drawX = (int)((screenWidth - headerWidth) / 2);
            const int startY = 200;
            int yOffset = 40 + (40 * index);
            return new Rectangle(drawX, startY + yOffset, 200, 30);
        }

        /// <summary>
        /// Real GameplayState subclass wired against an internally-held MatchScenario's REAL
        /// MatchContext/CommandDispatcher, in the exact same construction/subscription order as
        /// GameplayState.InitializeSystems (Coordinator first, then UIManager via
        /// BindInputManager, then PlayerController, then ReplayController) - several real bugs
        /// (see InputPipelineScenario's own doc comment) depended on this exact order.
        /// </summary>
        private sealed class ScenarioGameplayState : GameplayState
        {
            public ScenarioGameplayState(FakeInputProvider inputProvider, FakeGameplayView view, IReplayManager replayManager, ICardDatabase cardDatabase, IGameLogger logger)
                : base(new GameDependencies
                {
                    Game = null,
                    InputManager = new InputManager(inputProvider),
                    CardDatabase = cardDatabase,
                    Logger = logger,
                    UIManager = new UIManager(ScreenWidth, ScreenHeight, logger),
                    ReplayManager = replayManager,
                    View = view,
                    ViewportWidth = ScreenWidth,
                    ViewportHeight = ScreenHeight,
                })
            {
            }

            public void WireToRealMatch(MatchScenario match, IGameLogger logger)
            {
                _matchContext = match.Context;
                _matchManager = match.Context.MatchManager;
                _matchContext.ActionSystem.OnAutoExecuteCommand += RecordAndExecuteCommand;

                // Real screen-space node/site positions, matching GameplayState.InitializeMatch's
                // own call - without this, ClickNode/ClickSite operate on un-centered raw map
                // geometry a real session never actually shows, which could hide (or falsely
                // flag) a click-collision with the button rects below at the wrong coordinates.
                _matchContext.MapManager.CenterMap(ScreenWidth, ScreenHeight);

                _marketStateManager = new MarketStateManager(logger);
                _matchContext.ActionSystem.SetMarketStateManager(_marketStateManager);

                _uiEventMediator = new UIEventMediator(this, _uiManagerBacking, _matchContext.ActionSystem, logger, null);

                _interactionMapper = new InteractionMapper(_view!);

                // Subscriber #1 on InputManager.OnInputEvent.
                _inputCoordinator = new GameplayInputCoordinator(this, _inputManagerBacking, _matchContext);

                _commandDispatcher = match.Dispatcher;
                _cardPlaySystem = new CardPlaySystem(_matchContext, _matchManager, _replayManager, () => SwitchToTargetingMode(), logger);

                _uiEventMediator.Initialize();
                // Subscriber #2 on InputManager.OnInputEvent.
                _uiManagerBacking.BindInputManager(_inputManagerBacking);

                // Subscriber #3 on InputManager.OnInputEvent.
                _playerController = new PlayerController(this, _inputManagerBacking, _inputCoordinator, _interactionMapper);

                // Subscriber #4 on InputManager.OnInputEvent.
                _replayController = new ReplayController(this, _replayManager, _inputManagerBacking, logger, () => { });
            }
        }
    }
}
