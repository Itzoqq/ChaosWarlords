using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Data;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using NSubstitute;

namespace ChaosWarlords.Tests
{
    // ------------------------------------------------------------------------
    // TEST HELPERS
    // Industry-standard test utilities for input simulation
    // ------------------------------------------------------------------------

    /// <summary>
    /// Test helper for simulating input states.
    /// This is NOT a mock - it's a test builder/helper for stateful input simulation.
    /// Industry precedent: Unity's InputTestFixture, Unreal's FAutomationTestBase
    /// </summary>
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

        public void SetMouseState(MouseState state)
        {
            MouseState = state;
        }

        public void SetKeyboardState(KeyboardState state)
        {
            KeyboardState = state;
        }
    }

    // ------------------------------------------------------------------------
    // INPUT SIMULATION HELPERS
    // Static utilities to reduce code duplication across test suites
    // ------------------------------------------------------------------------

    /// <summary>
    /// Static helper methods for simulating input in tests.
    /// Reduces repetitive 4-line patterns to single method calls.
    /// </summary>
    public static class InputTestHelpers
    {
        /// <summary>
        /// Simulates a left mouse button click at the specified position.
        /// This is the most common input pattern in tests (15+ usages).
        /// </summary>
        /// <param name="input">The mock input provider</param>
        /// <param name="manager">The input manager to update</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void SimulateLeftClick(MockInputProvider input, IInputManager manager, int x, int y)
        {
            // Set released state first
            input.SetMouseState(CreateReleasedMouseState(x, y));
            manager.Update();

            // Then set pressed state
            input.SetMouseState(CreateMouseState(x, y, left: ButtonState.Pressed));
            manager.Update();
        }

        /// <summary>
        /// Simulates a right mouse button click at the specified position.
        /// Commonly used for cancel/context menu operations.
        /// </summary>
        /// <param name="input">The mock input provider</param>
        /// <param name="manager">The input manager to update</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void SimulateRightClick(MockInputProvider input, IInputManager manager, int x, int y)
        {
            // Set released state first
            input.SetMouseState(CreateReleasedMouseState(x, y));
            manager.Update();

            // Then set right button pressed
            input.SetMouseState(CreateMouseState(x, y, right: ButtonState.Pressed));
            manager.Update();
        }

        /// <summary>
        /// Simulates moving the mouse to a position without clicking.
        /// Useful for hover state tests.
        /// </summary>
        /// <param name="input">The mock input provider</param>
        /// <param name="manager">The input manager to update</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        public static void SimulateMouseMove(MockInputProvider input, IInputManager manager, int x, int y)
        {
            input.SetMouseState(CreateReleasedMouseState(x, y));
            manager.Update();
        }

        /// <summary>
        /// Creates a MouseState with all buttons released at the specified position.
        /// This is the most common mouse state configuration.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <returns>MouseState with all buttons released</returns>
        public static MouseState CreateReleasedMouseState(int x, int y)
        {
            return new MouseState(
                x, y, 0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released
            );
        }

        /// <summary>
        /// Creates a MouseState with configurable button states.
        /// Reduces verbose constructor calls with sensible defaults.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="scrollWheel">Scroll wheel value (default: 0)</param>
        /// <param name="left">Left button state (default: Released)</param>
        /// <param name="middle">Middle button state (default: Released)</param>
        /// <param name="right">Right button state (default: Released)</param>
        /// <param name="xButton1">XButton1 state (default: Released)</param>
        /// <param name="xButton2">XButton2 state (default: Released)</param>
        /// <returns>Configured MouseState</returns>
        public static MouseState CreateMouseState(
            int x,
            int y,
            int scrollWheel = 0,
            ButtonState left = ButtonState.Released,
            ButtonState middle = ButtonState.Released,
            ButtonState right = ButtonState.Released,
            ButtonState xButton1 = ButtonState.Released,
            ButtonState xButton2 = ButtonState.Released)
        {
            return new MouseState(x, y, scrollWheel, left, middle, right, xButton1, xButton2);
        }
    }

    // ------------------------------------------------------------------------
    // TEST DATA BUILDERS
    // Fluent API for creating test objects with sensible defaults
    // ------------------------------------------------------------------------

    /// <summary>
    /// Fluent builder for creating Card instances in tests.
    /// Provides sensible defaults and chainable methods for customization.
    /// </summary>
    public class CardBuilder
    {
        private string _name = "Test Card";
        private string _description = "Test Description";
        private int _cost = 0;
        private CardAspect _aspect = CardAspect.Warlord;
        private int _power = 0;
        private int _influence = 0;
        private int _vp = 0;
        private CardLocation _location = CardLocation.None;
        private List<CardEffect> _effects = new List<CardEffect>();

        public CardBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public CardBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        public CardBuilder WithCost(int cost)
        {
            _cost = cost;
            return this;
        }

        public CardBuilder WithAspect(CardAspect aspect)
        {
            _aspect = aspect;
            return this;
        }

        public CardBuilder WithPower(int power)
        {
            _power = power;
            return this;
        }

        public CardBuilder WithInfluence(int influence)
        {
            _influence = influence;
            return this;
        }

        public CardBuilder WithVP(int vp)
        {
            _vp = vp;
            return this;
        }

        public CardBuilder WithEffect(EffectType type, int magnitude, ResourceType? targetResource = null)
        {
            var effect = new CardEffect(type, magnitude);
            if (targetResource.HasValue)
                effect.TargetResource = targetResource.Value;
            _effects.Add(effect);
            return this;
        }

        public CardBuilder WithFocusEffect(EffectType type, int magnitude, ResourceType? targetResource = null)
        {
            var effect = new CardEffect(type, magnitude) { RequiresFocus = true };
            if (targetResource.HasValue)
                effect.TargetResource = targetResource.Value;
            _effects.Add(effect);
            return this;
        }

        public CardBuilder InHand()
        {
            _location = CardLocation.Hand;
            return this;
        }

        public CardBuilder InDeck()
        {
            _location = CardLocation.Deck;
            return this;
        }

        public CardBuilder InDiscard()
        {
            _location = CardLocation.DiscardPile;
            return this;
        }

        public CardBuilder InInnerCircle()
        {
            _location = CardLocation.InnerCircle;
            return this;
        }

        public CardBuilder InPlayed()
        {
            _location = CardLocation.Played;
            return this;
        }

        public Card Build()
        {
            // Card constructor: (string id, string name, int cost, ...)
            // We use _name as the id and _description as the name for backwards compatibility
            var card = new Card(_name, _description, _cost, _aspect, _power, _influence, _vp);
            card.Location = _location;
            foreach (var effect in _effects)
            {
                card.Effects.Add(effect);
            }
            return card;
        }
    }

    /// <summary>
    /// Fluent builder for creating Player instances in tests.
    /// Provides sensible defaults and chainable methods for customization.
    /// </summary>
    public class PlayerBuilder
    {
        private PlayerColor _color = PlayerColor.Red;
        private int _seatIndex = 0;
        private string _displayName = "Test Player";
        private int _power = 0;
        private int _influence = 0;
        private int _vp = 0;
        private int _troops = 0;
        private int _spies = 0;
        private readonly List<Card> _handCards = [];
        private readonly List<Card> _deckCards = [];
        private readonly List<Card> _discardCards = [];
        private readonly List<Card> _innerCircleCards = [];

        public PlayerBuilder WithColor(PlayerColor color)
        {
            _color = color;
            return this;
        }

        public PlayerBuilder WithSeatIndex(int seatIndex)
        {
            _seatIndex = seatIndex;
            return this;
        }

        public PlayerBuilder WithName(string name)
        {
            _displayName = name;
            return this;
        }

        public PlayerBuilder WithPower(int power)
        {
            _power = power;
            return this;
        }

        public PlayerBuilder WithInfluence(int influence)
        {
            _influence = influence;
            return this;
        }

        public PlayerBuilder WithVP(int vp)
        {
            _vp = vp;
            return this;
        }

        public PlayerBuilder WithTroops(int troops)
        {
            _troops = troops;
            return this;
        }

        public PlayerBuilder WithSpies(int spies)
        {
            _spies = spies;
            return this;
        }

        public PlayerBuilder WithCardsInHand(params Card[] cards)
        {
            _handCards.AddRange(cards);
            return this;
        }

        public PlayerBuilder WithCardsInDeck(params Card[] cards)
        {
            _deckCards.AddRange(cards);
            return this;
        }

        public PlayerBuilder WithCardsInDiscard(params Card[] cards)
        {
            _discardCards.AddRange(cards);
            return this;
        }

        public PlayerBuilder WithCardsInInnerCircle(params Card[] cards)
        {
            _innerCircleCards.AddRange(cards);
            return this;
        }

        public Player Build()
        {
            var player = new Player(_color)
            {
                SeatIndex = _seatIndex,
                DisplayName = _displayName,
                VictoryPoints = _vp,
                TroopsInBarracks = _troops,
                SpiesInBarracks = _spies
            };
            player.AddPower(_power);
            player.AddInfluence(_influence);

            foreach (var card in _handCards)
            {
                card.Location = CardLocation.Hand;
                player.AddToHand(card);
            }

            foreach (var card in _deckCards)
            {
                card.Location = CardLocation.Deck;
                player.DeckManager.AddToTop(card);
            }

            foreach (var card in _discardCards)
            {
                card.Location = CardLocation.DiscardPile;
                player.DeckManager.AddToDiscard(card);
            }

            foreach (var card in _innerCircleCards)
            {
                card.Location = CardLocation.InnerCircle;
                player.AddToInnerCircle(card);
            }

            return player;
        }
    }

    /// <summary>
    /// Fluent builder for creating MapNode instances in tests.
    /// Provides sensible defaults and chainable methods for customization.
    /// </summary>
    public class MapNodeBuilder
    {
        private LogicVector2 _position = LogicVector2.Zero;
        private int _id = 0;
        private PlayerColor _occupant = PlayerColor.None;
        private readonly List<MapNode> _neighbors = [];

        public MapNodeBuilder At(float x, float y)
        {
            _position = new LogicVector2(
                (int)Math.Round(x * LogicVector2.ScaleFactor),
                (int)Math.Round(y * LogicVector2.ScaleFactor));
            return this;
        }

        public MapNodeBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public MapNodeBuilder OccupiedBy(PlayerColor color)
        {
            _occupant = color;
            return this;
        }

        public MapNodeBuilder ConnectedTo(params MapNode[] neighbors)
        {
            _neighbors.AddRange(neighbors);
            return this;
        }

        public MapNode Build()
        {
            var node = new MapNode(_id, _position)
            {
                Occupant = _occupant
            };

            foreach (var neighbor in _neighbors)
            {
                node.AddNeighbor(neighbor);
            }

            return node;
        }
    }

    /// <summary>
    /// Fluent builder for creating MatchContext instances in tests. MatchContext is the
    /// single most-constructed object across the test suite (~40 call sites hand-rolled the
    /// same 8-positional-argument constructor call before this existed) and the most
    /// fragile-to-extend one - every new dependency added to its constructor meant editing
    /// all ~40 of them, each a chance to put the wrong value in the wrong positional slot
    /// with no compiler help. Mirrors CardBuilder/PlayerBuilder/MapNodeBuilder's own style:
    /// every dependency defaults to a fresh NSubstitute mock (matching what most call sites
    /// already did), overridable one at a time via With...() for the tests that need a real
    /// implementation or a specifically-configured mock for one particular dependency. See
    /// planning.txt.
    /// </summary>
    public class MatchContextBuilder
    {
        private ITurnManager _turnManager = Substitute.For<ITurnManager>();
        private IMapManager _mapManager = Substitute.For<IMapManager>();
        private IMarketManager _marketManager = Substitute.For<IMarketManager>();
        private IActionSystem _actionSystem = Substitute.For<IActionSystem>();
        private ICardDatabase _cardDatabase = Substitute.For<ICardDatabase>();
        private IPlayerStateManager _playerStateManager = Substitute.For<IPlayerStateManager>();
        private IGameLogger _logger = Tests.Utilities.TestLogger.Instance;
        private int? _seed = 12345;

        public MatchContextBuilder WithTurnManager(ITurnManager turnManager)
        {
            _turnManager = turnManager;
            return this;
        }

        public MatchContextBuilder WithMapManager(IMapManager mapManager)
        {
            _mapManager = mapManager;
            return this;
        }

        public MatchContextBuilder WithMarketManager(IMarketManager marketManager)
        {
            _marketManager = marketManager;
            return this;
        }

        public MatchContextBuilder WithActionSystem(IActionSystem actionSystem)
        {
            _actionSystem = actionSystem;
            return this;
        }

        public MatchContextBuilder WithCardDatabase(ICardDatabase cardDatabase)
        {
            _cardDatabase = cardDatabase;
            return this;
        }

        public MatchContextBuilder WithPlayerStateManager(IPlayerStateManager playerStateManager)
        {
            _playerStateManager = playerStateManager;
            return this;
        }

        public MatchContextBuilder WithLogger(IGameLogger logger)
        {
            _logger = logger;
            return this;
        }

        public MatchContextBuilder WithSeed(int? seed)
        {
            _seed = seed;
            return this;
        }

        public MatchContext Build()
        {
            return new MatchContext(_turnManager, _mapManager, _marketManager, _actionSystem, _cardDatabase, _playerStateManager, _logger, _seed);
        }
    }

    /// <summary>
    /// Minimal ILocalizationService test double for CardDatabase/CardFactory tests that don't
    /// need the real Content/data/localization/en_US.json bundle (see MatchScenario's
    /// ResolveLocalizationJsonPath for the tests that DO load it, real content and all).
    /// Seed known keys via the constructor; any other key falls back to the same
    /// "[MISSING:key]" placeholder the real LocalizationManager returns, so a test that
    /// forgets to seed a key it depends on fails loudly rather than silently.
    /// </summary>
    public class TestLocalizationService : ILocalizationService
    {
        private readonly Dictionary<string, string> _strings;

        public TestLocalizationService(Dictionary<string, string>? strings = null)
        {
            _strings = strings ?? new Dictionary<string, string>();
        }

        public void Set(string key, string value) => _strings[key] = value;

        public void Load(Stream stream) => throw new NotSupportedException(
            "TestLocalizationService is seeded via its constructor/Set, not a JSON stream - use LocalizationManager for real-bundle tests.");

        public string GetString(string key) =>
            _strings.TryGetValue(key, out var value) ? value : $"[MISSING:{key}]";
    }
}
