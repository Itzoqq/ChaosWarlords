using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.States.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.Contexts;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Views;

namespace ChaosWarlords.Tests.States
{
    [TestClass]
    public class GameplayStateTests
    {
        private IInputProvider _inputProvider = null!; // Mock
        private ICardDatabase _cardDatabase = null!; // Mock
        private IMapManager _mapManager = null!; // Mock
        private IMarketManager _marketManager = null!; // Mock
        private IActionSystem _actionSystem = null!; // Mock

        [TestInitialize]
        public void Setup()
        {
            _inputProvider = Substitute.For<IInputProvider>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();
        }

        [TestMethod]
        public void SwitchToTargetingMode_SetsCorrectInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.SwitchToTargetingMode();

            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode));
        }

        [TestMethod]
        public void SwitchToNormalMode_SetsCorrectInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            state.SwitchToNormalMode();

            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void ToggleMarket_SwitchesInputModesAndFlags()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.SwitchToNormalMode();

            state.ToggleMarket();

            Assert.IsTrue(state.IsMarketOpen);
            Assert.IsInstanceOfType(state.InputMode, typeof(MarketInputMode));

            state.ToggleMarket();
            Assert.IsFalse(state.IsMarketOpen);
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void Update_EnterKey_TriggersEndTurn()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Capture initial active player to check if turn switched
            var initialPlayer = state.MatchContext.ActivePlayer;

            // Mock Input: Simulate "Enter Key Just Pressed"
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(Keys.Enter));

            // Act
            state.Update(new GameTime());

            // Assert
            Assert.AreNotEqual(initialPlayer, state.MatchContext.ActivePlayer);
        }

        [TestMethod]
        public void Update_RightClick_CancelsMarket()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.IsMarketOpen = true;

            // Mock Input: Simulate "Right Click Just Pressed"
            var pressedState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _inputProvider.GetMouseState().Returns(pressedState);

            // Act
            state.Update(new GameTime());

            // Assert
            Assert.IsFalse(state.IsMarketOpen);
        }

        // --- Helper Class ---
        // Exposes internals for testing and handles Manual Initialization
        // In GameplayStateTests.cs -> internal class TestableGameplayState
        internal class TestableGameplayState : GameplayState
        {
            private readonly IInputProvider _testInput;
            private readonly ICardDatabase _testDb;

            public TestableGameplayState(Game game, IInputProvider input, ICardDatabase db)
                : base(game, input, db)
            {
                _testInput = input;
                _testDb = db;
            }

            public void InitializeTestEnvironment(IMapManager map, IMarketManager market, IActionSystem action)
            {
                _inputManagerBacking = new InputManager(_testInput);
                _uiManagerBacking = new UIManager(800, 600);

                var p1 = new Player(PlayerColor.Red);
                var p2 = new Player(PlayerColor.Blue);
                var tm = new TurnManager(new List<Player> { p1, p2 });

                _matchContext = new MatchContext(tm, map, market, action, _testDb);

                // FIX 1: Initialize the Controller (Was causing NRE in EndTurn)
                _matchController = new MatchController(_matchContext);

                // FIX 2: Initialize Event Subscriptions (Was causing Assertion Fail in Event test)
                InitializeEventSubscriptions();

                // FIX 3: Set Default Input Mode (Was causing NRE in Update)
                SwitchToNormalMode();

                // Initialize View for Visual tests
                if (_view == null)
                {
                    // We assume GameplayView can handle null GraphicsDevice for list testing 
                    // or we would need a more complex Mock. 
                    // Since the previous error wasn't about View creation but Logic, we leave this as is or strictly mock lists if needed.
                    // For now, let's just instantiate it if your constructor allows, or mock the internal lists if you made them writable.
                    // Since GameplayView requires GraphicsDevice, we might strictly need to rely on the null checks in State.
                    // If Visual tests fail on View creation, we might need a dummy GraphicsDevice.
                }
            }

            public new MatchContext MatchContext => base.MatchContext;

            // Access the internal View directly
            public List<CardViewModel> HandViewModels => _view?.HandViewModels ?? new List<CardViewModel>();
            public List<CardViewModel> PlayedViewModels => _view?.PlayedViewModels ?? new List<CardViewModel>();
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.SwitchToNormalMode();
            state.IsMarketOpen = true;

            // Setup Data
            var player = state.MatchContext.ActivePlayer;
            player.Influence = 10;
            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0, 0);

            // Mock Market
            _marketManager.MarketRow.Returns(new List<Card> { cardToBuy });
            _marketManager.TryBuyCard(Arg.Any<Player>(), Arg.Any<Card>()).Returns(true);

            // Act
            var command = new BuyCardCommand(cardToBuy);
            command.Execute(state);

            // Assert
            _marketManager.Received(1).TryBuyCard(player, cardToBuy);
        }

        [TestMethod]
        public void Command_DeployTroop_DeploysTroop()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);
            state.SwitchToNormalMode();

            var node = new MapNode(1, Vector2.Zero);
            _mapManager.TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>()).Returns(true);

            // Act
            var command = new DeployTroopCommand(node);
            command.Execute(state);

            // Assert
            _mapManager.Received(1).TryDeploy(state.MatchContext.ActivePlayer, node);
        }

        // In GameplayStateTests.cs

        [TestMethod]
        public void PlayCard_TriggersTargeting_SwitchInputMode()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Create a card with a Targeting Effect (Essential!)
            var card = new Card("kill", "Assassin", 0, CardAspect.Shadow, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            // Act
            // Calling PlayCard() triggers the logic: "If targeting effect -> SwitchToTargetingMode()"
            state.PlayCard(card);

            // Assert
            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode));
        }

        [TestMethod]
        public void Update_RightClick_CancelsTargeting_AndResetsInputMode()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Set to Targeting Mode manually
            _actionSystem.IsTargeting().Returns(true);
            state.SwitchToTargetingMode();

            // Mock Input: Right Click
            var rightClick = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _inputProvider.GetMouseState().Returns(rightClick);

            // Act
            state.Update(new GameTime());

            // Assert
            _actionSystem.Received(1).CancelTargeting();
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void SubscribesToEvents_OnActionCompleted_ResetsInputMode()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InitializeTestEnvironment(_mapManager, _marketManager, _actionSystem);

            // Start in Targeting
            state.SwitchToTargetingMode();

            // Act: Raise ActionCompleted event from the mock
            _actionSystem.OnActionCompleted += Raise.Event();

            // Assert
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }
    }
}