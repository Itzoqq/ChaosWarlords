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

namespace ChaosWarlords.Tests.States
{
    [TestClass]
    public class GameplayStateTests
    {
        private InputManager _input = null!;
        private IInputProvider _inputProvider = null!; // Mock

        // Systems
        private TurnManager _turnManager = null!; // Concrete Logic
        private IMapManager _mapManager = null!; // Mock
        private IMarketManager _marketManager = null!; // Mock
        private UIManager _uiManager = null!; // Concrete Logic
        private IActionSystem _actionSystem = null!; // Mock
        private ICardDatabase _cardDatabase = null!; // Mock

        [TestInitialize]
        public void Setup()
        {
            // 1. Create NSubstitute Mocks for external dependencies
            _inputProvider = Substitute.For<IInputProvider>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();

            // 2. Setup Input Manager Defaults
            _input = new InputManager(_inputProvider);
            _inputProvider.GetMouseState().Returns(new MouseState());
            _inputProvider.GetKeyboardState().Returns(new KeyboardState());

            // 3. Setup Concrete Managers & Player Data
            var p1 = new Player(PlayerColor.Red);
            var p2 = new Player(PlayerColor.Blue);

            // FIX: Populate Deck so DrawCards(5) doesn't fail in tests
            for (int i = 0; i < 10; i++) p1.Deck.Add(CardFactory.CreateSoldier());
            p1.DrawCards(5);

            _turnManager = new TurnManager(new List<Player> { p1, p2 });
            _uiManager = new UIManager(800, 600);

            // 4. Configure Default Mock Behaviors (Restored from old file)
            _marketManager.MarketRow.Returns(new List<Card>());
            _actionSystem.CurrentState.Returns(ActionState.Normal);
            _actionSystem.IsTargeting().Returns(false);

            // Allow Try methods to succeed by default to unblock commands
            _marketManager.TryBuyCard(Arg.Any<Player>(), Arg.Any<Card>()).Returns(true);
            _mapManager.TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>()).Returns(true);
        }

        // --- Helpers for Input (Restored) ---
        private void SetKeyboard(Keys key)
        {
            _inputProvider.GetKeyboardState().Returns(new KeyboardState(key));
        }

        private void QueueRightClick()
        {
            // Simulate a right click in the current frame
            var mouse = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _inputProvider.GetMouseState().Returns(mouse);
        }

        /// <summary>
        /// Helper method to manually inject dependencies since we removed InjectDependencies().
        /// This mimics what happens in LoadContent() but with our Mocks.
        /// </summary>
        private void ConfigureStateWithMocks(GameplayState state)
        {
            // 1. Create the Context with our Mocks
            var context = new MatchContext(
                _turnManager,
                _mapManager,
                _marketManager,
                _actionSystem,
                _cardDatabase
            );

            // 2. Inject directly into internal fields (allowed via InternalsVisibleTo)
            state._matchContext = context;
            state._inputManagerBacking = _input;
            state._uiManagerBacking = _uiManager;

            // 3. Perform the necessary initialization steps that usually happen in LoadContent
            state._matchContext.ActionSystem.SetCurrentPlayer(state._matchContext.ActivePlayer);
            state.InitializeEventSubscriptions();

            // FIX: Initialize InputMode to prevent NullReferenceException during Update() in tests
            state.SwitchToNormalMode();
        }

        // --- TESTS ---

        [TestMethod]
        public void Constructor_Initialization_ShouldNotFail()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            Assert.IsNotNull(state);
        }

        [TestMethod]
        public void EndTurn_ResetsResources_AndDrawsCards()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            var activePlayer = _turnManager.ActivePlayer;
            activePlayer.Hand.Clear();
            activePlayer.Deck.Clear();
            activePlayer.DiscardPile.Clear();
            activePlayer.PlayedCards.Clear();

            // Add cards to deck so we can draw
            for (int i = 0; i < 5; i++) activePlayer.Deck.Add(CardFactory.CreateSoldier());
            activePlayer.Power = 5;

            // Act
            state.EndTurn();

            // Assert
            Assert.AreEqual(0, activePlayer.Power, "Power should reset to 0.");
            Assert.HasCount(5, activePlayer.Hand, "Should draw exactly 5 cards.");
        }

        [TestMethod]
        public void HandleGlobalInput_EnterKey_EndsTurn()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);
            _turnManager.ActivePlayer.Power = 10;

            // Step 1: Key Down
            SetKeyboard(Keys.Enter);
            _input.Update();

            // Act
            state.HandleGlobalInput();

            // Assert
            Assert.AreEqual(0, _turnManager.ActivePlayer.Power, "Power should be 0 after EndTurn triggered by Enter key.");
        }

        [TestMethod]
        public void PlayCard_ResolvesResourceEffects()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            var card = new Card("test", "Resource Card", 0, CardAspect.Neutral, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 3, ResourceType.Power));
            _turnManager.ActivePlayer.Hand.Add(card);

            state.PlayCard(card);

            Assert.AreEqual(3, _turnManager.ActivePlayer.Power);
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards);
            Assert.DoesNotContain(card, _turnManager.ActivePlayer.Hand);
        }

        [TestMethod]
        public void PlayCard_TriggersTargeting_SwitchInputMode()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            var card = new Card("kill", "Assassin", 0, CardAspect.Shadow, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            // Update Mock to reflect state change when called
            _actionSystem.When(x => x.StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>()))
                         .Do(x => _actionSystem.CurrentState.Returns(x.Arg<ActionState>()));

            // Act
            state.PlayCard(card);

            // Assert
            _actionSystem.Received(1).StartTargeting(ActionState.TargetingAssassinate, card);
            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode), "Playing a targeting card must switch the Input Mode.");
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);
            state.IsMarketOpen = true;
            _turnManager.ActivePlayer.Influence = 10;

            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0, 0);
            var marketList = new List<Card> { cardToBuy };
            _marketManager.MarketRow.Returns(marketList);

            // Act
            var command = new BuyCardCommand(cardToBuy);
            command.Execute(state);

            // Assert
            _marketManager.Received(1).TryBuyCard(_turnManager.ActivePlayer, cardToBuy);
        }

        [TestMethod]
        public void MoveCardToPlayed_MovesToPlayedArea_AndPopsUpY()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            // Manually set layout positions for test
            state._handYBacking = 500;
            state._playedYBacking = 400;
            var activePlayer = _turnManager.ActivePlayer;

            var card1 = new Card("c1", "Left Card", 0, CardAspect.Neutral, 0, 0, 0);
            var card2 = new Card("c2", "Right Card", 0, CardAspect.Neutral, 0, 0, 0);

            activePlayer.Hand.Add(card1);
            activePlayer.Hand.Add(card2);

            // Act
            // 1. Initial Sync
            state.Update(new GameTime());

            // 2. Perform Move
            state.MoveCardToPlayed(card1);

            // 3. Second Sync
            state.Update(new GameTime());

            // Assert
            var playedVM = state.PlayedViewModels.FirstOrDefault(vm => vm.Model == card1);
            var handVM = state.HandViewModels.FirstOrDefault(vm => vm.Model == card2);

            Assert.IsNotNull(playedVM, "Card1 should be in Played list");
            Assert.IsNotNull(handVM, "Card2 should be in Hand list");
            Assert.AreEqual(400, playedVM.Position.Y, "Played card should be at PlayedY");
            Assert.AreEqual(500, handVM.Position.Y, "Hand card should be at HandY");
        }

        [TestMethod]
        public void Update_RightClick_CancelsTargeting_AndResetsInputMode()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            _actionSystem.IsTargeting().Returns(true);
            _actionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);
            state.InputMode = new TargetingInputMode(state, _input, _uiManager, _mapManager, _turnManager, _actionSystem);

            // Act
            QueueRightClick();
            state.Update(new GameTime());

            // Assert
            _actionSystem.Received(1).CancelTargeting();
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void Command_ToggleMarket_TogglesState()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);
            state.IsMarketOpen = true;

            var command = new ToggleMarketCommand();
            command.Execute(state);

            Assert.IsFalse(state.IsMarketOpen);
        }

        [TestMethod]
        public void PlayCard_Directly_PlaysCard()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            // Ensure hand has cards (populated in Setup)
            var card = _turnManager.ActivePlayer.Hand[0];
            int initialCount = _turnManager.ActivePlayer.Hand.Count;

            state.PlayCard(card);

            Assert.HasCount(initialCount - 1, _turnManager.ActivePlayer.Hand);
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards);
        }

        [TestMethod]
        public void Update_PressingEnter_EndsTurn()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);
            var activePlayer = _turnManager.ActivePlayer;

            // Play a card so we have state to clear
            state.PlayCard(activePlayer.Hand[0]);

            // Act
            SetKeyboard(Keys.Enter);
            state.Update(new GameTime());

            // Assert
            Assert.IsEmpty(activePlayer.PlayedCards);
            Assert.HasCount(5, activePlayer.Hand);
        }

        [TestMethod]
        public void Command_DeployTroop_DeploysTroop()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);
            var node = new MapNode(1, Vector2.Zero);

            // Act
            var command = new DeployTroopCommand(node);
            command.Execute(state);

            // Assert
            _mapManager.Received(1).TryDeploy(_turnManager.ActivePlayer, node);
        }

        [TestMethod]
        public void PlayCard_GainsResources()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            var activePlayer = _turnManager.ActivePlayer;
            activePlayer.Influence = 0;

            var richNoble = new Card("rich_noble", "Rich Noble", 0, CardAspect.Order, 0, 0, 0);
            richNoble.Effects.Add(new CardEffect(EffectType.GainResource, 3, ResourceType.Influence));
            activePlayer.Hand.Add(richNoble);

            state.PlayCard(richNoble);

            Assert.AreEqual(3, activePlayer.Influence);
        }

        [TestMethod]
        public void Logic_PlayTargetedActionCard_SetsActionStateCorrectly()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);

            var assassin = new Card("assassin", "Assassin", 0, CardAspect.Shadow, 0, 0, 0);
            assassin.Effects.Add(new CardEffect(EffectType.Assassinate, 0));
            _turnManager.ActivePlayer.Hand.Add(assassin);

            _actionSystem.When(x => x.StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>()))
                         .Do(x => _actionSystem.CurrentState.Returns(x.Arg<ActionState>()));

            // Act
            state.PlayCard(assassin);

            // Assert
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void ToggleMarket_SwitchesInputMode()
        {
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);
            state.SwitchToNormalMode();

            state.ToggleMarket();

            Assert.IsTrue(state.IsMarketOpen);
            Assert.IsInstanceOfType(state.InputMode, typeof(MarketInputMode));

            state.ToggleMarket();
            Assert.IsFalse(state.IsMarketOpen);
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode));
        }

        [TestMethod]
        public void LoadContent_SubscribesToEvents()
        {
            // Arrange
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            ConfigureStateWithMocks(state);
            state.SwitchToNormalMode();

            // Act: Switch to targeting, then fire event
            state.SwitchToTargetingMode();
            Assert.IsInstanceOfType(state.InputMode, typeof(TargetingInputMode));

            _actionSystem.OnActionCompleted += Raise.Event();

            // Assert: Should be back to Normal
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode), "GameplayState did not react to ActionSystem.OnActionCompleted event.");
        }

        // --- Helper Class ---
        internal class TestableGameplayState : GameplayState
        {
            public TestableGameplayState(Game game, IInputProvider input, ICardDatabase db)
                : base(game, input, db) { }
        }
    }
}