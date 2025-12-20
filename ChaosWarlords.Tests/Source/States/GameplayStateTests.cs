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
using System.Collections.Generic;
using System.Linq;

namespace ChaosWarlords.Tests.States
{
    [TestClass]
    public class GameplayStateTests
    {
        private GameplayState _state = null!;
        private InputManager _input = null!;
        private IInputProvider _inputProvider = null!; // Mock

        private TurnManager _turnManager = null!; // Concrete (Logic is self-contained)
        private IMapManager _mapManager = null!; // Mock
        private IMarketManager _marketManager = null!; // Mock
        private UIManager _uiManager = null!; // Concrete
        private IActionSystem _actionSystem = null!; // Mock
        private ICardDatabase _cardDatabase = null!; // Mock

        [TestInitialize]
        public void Setup()
        {
            // 1. Create NSubstitute Mocks
            _inputProvider = Substitute.For<IInputProvider>();
            _mapManager = Substitute.For<IMapManager>();
            _marketManager = Substitute.For<IMarketManager>();
            _actionSystem = Substitute.For<IActionSystem>();
            _cardDatabase = Substitute.For<ICardDatabase>();

            // 2. Setup Input Manager
            _input = new InputManager(_inputProvider);
            // Default Input: No buttons pressed
            _inputProvider.GetMouseState().Returns(new MouseState());
            _inputProvider.GetKeyboardState().Returns(new KeyboardState());

            // 3. Setup Player & TurnManager
            // We keep these concrete because Player logic is simple data holding
            var player = new Player(PlayerColor.Red);
            for (int i = 0; i < 10; i++) player.Deck.Add(CardFactory.CreateSoldier());
            player.DrawCards(5);
            _turnManager = new TurnManager(new List<Player> { player });

            // 4. Setup UI Manager (Concrete is fine here, or could be mocked)
            _uiManager = new UIManager(800, 600);

            // 5. Configure Default Mock Behaviors
            _marketManager.MarketRow.Returns(new List<Card>());
            _actionSystem.CurrentState.Returns(ActionState.Normal);
            _actionSystem.IsTargeting().Returns(false);

            // Allow Try methods to succeed by default to unblock commands
            _marketManager.TryBuyCard(Arg.Any<Player>(), Arg.Any<Card>()).Returns(true);
            _mapManager.TryDeploy(Arg.Any<Player>(), Arg.Any<MapNode>()).Returns(true);

            // 6. Initialize GameplayState
            _state = new GameplayState(null!, _inputProvider, _cardDatabase);
            _state.InjectDependencies(_input, _uiManager, _mapManager, _marketManager, _actionSystem, _turnManager);

            // FIX: Initialize InputMode to prevent NullReferenceException during Update()
            _state.InputMode = Substitute.For<IInputMode>();
        }

        // --- Helpers for Input ---
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

        // --- TESTS ---

        [TestMethod]
        public void EndTurn_ResetsResources_AndDrawsCards()
        {
            var activePlayer = _turnManager.ActivePlayer;
            activePlayer.Hand.Clear();
            activePlayer.Deck.Clear();
            activePlayer.DiscardPile.Clear();
            activePlayer.PlayedCards.Clear();

            // Add cards to deck so we can draw
            for (int i = 0; i < 5; i++) activePlayer.Deck.Add(CardFactory.CreateSoldier());
            activePlayer.Power = 5;

            // Act
            _state.EndTurn();

            // Assert
            Assert.AreEqual(0, activePlayer.Power, "Power should reset to 0.");
            Assert.HasCount(5, activePlayer.Hand, "Should draw exactly 5 cards.");
        }

        [TestMethod]
        public void HandleGlobalInput_EnterKey_EndsTurn()
        {
            // Arrange
            _turnManager.ActivePlayer.Power = 10;

            // Step 1: Key Down
            SetKeyboard(Keys.Enter);
            _input.Update(); // Process input

            // Act
            _state.HandleGlobalInput();

            // Assert
            Assert.AreEqual(0, _turnManager.ActivePlayer.Power, "Power should be 0 after EndTurn triggered by Enter key.");
        }

        [TestMethod]
        public void PlayCard_ResolvesResourceEffects()
        {
            var card = new Card("test", "Resource Card", 0, CardAspect.Neutral, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 3, ResourceType.Power));
            _turnManager.ActivePlayer.Hand.Add(card);

            _state.PlayCard(card);

            Assert.AreEqual(3, _turnManager.ActivePlayer.Power);
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards);
            Assert.DoesNotContain(card, _turnManager.ActivePlayer.Hand);
        }

        [TestMethod]
        public void PlayCard_TriggersTargeting_SwitchInputMode()
        {
            // Arrange
            var card = new Card("kill", "Assassin", 0, CardAspect.Shadow, 0, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));
            _state.SwitchToNormalMode();

            // Update Mock to reflect state change when called
            _actionSystem.When(x => x.StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>()))
                         .Do(x => _actionSystem.CurrentState.Returns(x.Arg<ActionState>()));

            // Act
            _state.PlayCard(card);

            // Assert
            _actionSystem.Received(1).StartTargeting(ActionState.TargetingAssassinate, card);

            // Verify Input Mode Switch
            Assert.IsInstanceOfType(_state.InputMode, typeof(TargetingInputMode), "Playing a targeting card must switch the Input Mode.");
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            // Arrange
            _state.IsMarketOpen = true;
            _turnManager.ActivePlayer.Influence = 10;
            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0, 0);

            // Setup Mock Market
            var marketList = new List<Card> { cardToBuy };
            _marketManager.MarketRow.Returns(marketList);
            _marketManager.TryBuyCard(Arg.Any<Player>(), cardToBuy).Returns(true);

            // Act
            var command = new BuyCardCommand(cardToBuy);
            command.Execute(_state);

            // Assert
            _marketManager.Received(1).TryBuyCard(_turnManager.ActivePlayer, cardToBuy);
        }

        [TestMethod]
        public void MoveCardToPlayed_MovesToPlayedArea_AndPopsUpY()
        {
            // Arrange
            _state._handYBacking = 500;
            _state._playedYBacking = 400;
            var activePlayer = _turnManager.ActivePlayer;

            // Create cards (No Position in constructor anymore)
            var card1 = new Card("c1", "Left Card", 0, CardAspect.Neutral, 0, 0, 0);
            var card2 = new Card("c2", "Right Card", 0, CardAspect.Neutral, 0, 0, 0);

            activePlayer.Hand.Add(card1);
            activePlayer.Hand.Add(card2);

            // Act
            // 1. Initial Sync (Create Hand ViewModels)
            _state.Update(new GameTime());

            // 2. Perform the Move Logic
            _state.MoveCardToPlayed(card1);

            // 3. Second Sync (Update ViewModels for new state)
            _state.Update(new GameTime());

            // Assert
            // Retrieve the ViewModels generated by GameplayState
            var playedVM = _state.PlayedViewModels.FirstOrDefault(vm => vm.Model == card1);
            var handVM = _state.HandViewModels.FirstOrDefault(vm => vm.Model == card2);

            Assert.IsNotNull(playedVM, "Card1 should have a matching ViewModel in Played list");
            Assert.IsNotNull(handVM, "Card2 should have a matching ViewModel in Hand list");

            // Check Y Positions
            Assert.AreEqual(400, playedVM.Position.Y, "Played card should be at PlayedY (400)");
            Assert.AreEqual(500, handVM.Position.Y, "Hand card should stay at HandY (500)");
        }

        [TestMethod]
        public void Update_RightClick_CancelsTargeting_AndResetsInputMode()
        {
            // 1. ARRANGE
            // Setup the mock to look like we are targeting
            _actionSystem.IsTargeting().Returns(true);
            _actionSystem.CurrentState.Returns(ActionState.TargetingAssassinate);

            // Initialize Targeting Mode
            _state.InputMode = new TargetingInputMode(_state, _input, _uiManager, _mapManager, _turnManager, _actionSystem);

            // 2. ACT
            QueueRightClick();
            _state.Update(new GameTime());

            // 3. ASSERT
            // Verify CancelTargeting was called on the mock
            _actionSystem.Received(1).CancelTargeting();

            // Verify the state switched back to Normal
            Assert.IsInstanceOfType(_state.InputMode, typeof(NormalPlayInputMode), "Right-clicking should switch the Input Processor back to Normal Mode.");
        }

        [TestMethod]
        public void Command_ToggleMarket_TogglesState()
        {
            _state.IsMarketOpen = true;
            var command = new ToggleMarketCommand();
            command.Execute(_state);
            Assert.IsFalse(_state.IsMarketOpen);
        }

        [TestMethod]
        public void PlayCard_Directly_PlaysCard()
        {
            var card = _turnManager.ActivePlayer.Hand[0];
            int initialCount = _turnManager.ActivePlayer.Hand.Count;

            _state.PlayCard(card);

            Assert.HasCount(initialCount - 1, _turnManager.ActivePlayer.Hand);
            Assert.Contains(card, _turnManager.ActivePlayer.PlayedCards);
        }

        [TestMethod]
        public void Update_PressingEnter_EndsTurn()
        {
            // Arrange
            var activePlayer = _turnManager.ActivePlayer;
            var card = activePlayer.Hand[0];
            _state.PlayCard(card);

            // Act
            SetKeyboard(Keys.Enter);
            _state.Update(new GameTime());

            // Assert
            Assert.IsEmpty(activePlayer.PlayedCards);
            Assert.HasCount(5, activePlayer.Hand);
        }

        [TestMethod]
        public void Command_DeployTroop_DeploysTroop()
        {
            // Arrange
            var node = new MapNode(1, Vector2.Zero);
            var activePlayer = _turnManager.ActivePlayer;

            // Act
            var command = new DeployTroopCommand(node);
            command.Execute(_state);

            // Assert
            // Verify delegation to MapManager
            _mapManager.Received(1).TryDeploy(activePlayer, node);
        }

        [TestMethod]
        public void PlayCard_GainsResources()
        {
            var activePlayer = _turnManager.ActivePlayer;
            activePlayer.Influence = 0;
            var richNoble = new Card("rich_noble", "Rich Noble", 0, CardAspect.Order, 0, 0, 0);
            richNoble.Effects.Add(new CardEffect(EffectType.GainResource, 3, ResourceType.Influence));
            activePlayer.Hand.Add(richNoble);

            _state.PlayCard(richNoble);

            Assert.AreEqual(3, activePlayer.Influence);
        }

        [TestMethod]
        public void Logic_PlayTargetedActionCard_SetsActionStateCorrectly()
        {
            // Arrange
            var assassin = new Card("assassin", "Assassin", 0, CardAspect.Shadow, 0, 0, 0);
            assassin.Effects.Add(new CardEffect(EffectType.Assassinate, 0));
            _turnManager.ActivePlayer.Hand.Add(assassin);

            // Setup Mock to update property when method called
            _actionSystem.When(x => x.StartTargeting(Arg.Any<ActionState>(), Arg.Any<Card>()))
                         .Do(x => _actionSystem.CurrentState.Returns(x.Arg<ActionState>()));

            // Act
            _state.PlayCard(assassin);

            // Assert
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void ToggleMarket_SwitchesInputMode()
        {
            _state.SwitchToNormalMode();

            _state.ToggleMarket();

            Assert.IsTrue(_state.IsMarketOpen);
            Assert.IsInstanceOfType(_state.InputMode, typeof(MarketInputMode));
        }

        [TestMethod]
        public void LoadContent_SubscribesToEvents()
        {
            // Arrange
            // We need a fresh state to test LoadContent subscription logic
            // Use the dependencies we already created in Setup
            var state = new TestableGameplayState(null!, _inputProvider, _cardDatabase);
            state.InjectDependencies(_input, _uiManager, _mapManager, _marketManager, _actionSystem, _turnManager);

            // Pre-condition
            state.SwitchToNormalMode();

            // Act: Trigger the event on the MOCK
            _actionSystem.OnActionCompleted += Raise.Event();

            // Assert
            // If the event was hooked up, the handler (OnActionCompleted) would call SwitchToNormalMode.
            // But we are ALREADY in NormalMode. 
            // To prove it works, let's put it in TargetingMode first.
            state.InputMode = new TargetingInputMode(state, _input, _uiManager, _mapManager, _turnManager, _actionSystem);

            // Trigger again
            _actionSystem.OnActionCompleted += Raise.Event();

            // Should be back to Normal
            Assert.IsInstanceOfType(state.InputMode, typeof(NormalPlayInputMode), "GameplayState did not react to ActionSystem.OnActionCompleted event.");
        }

        // --- Helper Class ---
        private class TestableGameplayState : GameplayState
        {
            public TestableGameplayState(Game game, IInputProvider input, ICardDatabase db)
                : base(game, input, db) { }
        }
    }
}