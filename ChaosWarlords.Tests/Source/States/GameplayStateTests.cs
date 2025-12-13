using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting; // Ensure this is present

namespace ChaosWarlords.Tests.States
{
    // Define the Mock Provider inside the Test project
    public class MockInputProvider : IInputProvider
    {
        public MouseState MouseState;
        public KeyboardState KeyboardState;

        public MouseState GetMouseState() => MouseState;
        public KeyboardState GetKeyboardState() => KeyboardState;
    }

    [TestClass]
    public class GameplayStateTests
    {
        private GameplayState _state = null!;
        private InputManager _input = null!;
        private MockInputProvider _mockInputProvider = null!;
        private Player _player = null!;
        private MapManager _mapManager = null!;
        private MarketManager _marketManager = null!;
        private ActionSystem _actionSystem = null!;

        [TestInitialize]
        public void Setup()
        {
            // 1. Create the Mock
            _mockInputProvider = new MockInputProvider();
            // 2. Inject Mock into Manager
            _input = new InputManager(_mockInputProvider);
            _player = new Player(PlayerColor.Red);
            _mapManager = new MapManager(new List<MapNode>(), new List<Site>());
            _marketManager = new MarketManager();
            _actionSystem = new ActionSystem(_player, _mapManager);

            // Pass null Game to avoid graphics initialization
            _state = new GameplayState(null!);

            // Inject our test dependencies
            _state.InjectDependencies(_input, null!, _mapManager, _marketManager, _actionSystem, _player);
        }

        [TestMethod]
        public void EndTurn_ResetsResources_AndDrawsCards()
        {
            // Arrange
            _player.Power = 5;
            _player.Influence = 5;
            _player.Deck.Add(new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0));
            _player.Hand.Clear();

            // Act
            _state.EndTurn();

            // Assert
            Assert.AreEqual(0, _player.Power, "Power should reset at end of turn.");
            Assert.AreEqual(0, _player.Influence, "Influence should reset at end of turn.");
            Assert.HasCount(1, _player.Hand, "Should draw cards (1 available in deck).");
        }

        [TestMethod]
        public void HandleGlobalInput_EnterKey_EndsTurn()
        {
            // Step 1: Ensure Key is UP (Previous Frame)
            _mockInputProvider.KeyboardState = new KeyboardState();
            _input.Update();

            // Step 2: Press Key DOWN (Current Frame)
            _mockInputProvider.KeyboardState = new KeyboardState(Keys.Enter);
            _input.Update();

            // Act
            _player.Power = 10;
            _state.HandleGlobalInput();

            // Assert
            Assert.AreEqual(0, _player.Power, "Power should be 0 after EndTurn triggered by Enter key.");
        }

        [TestMethod]
        public void PlayCard_ResolvesResourceEffects()
        {
            var card = new Card("test", "Resource Card", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 3, ResourceType.Power));
            _player.Hand.Add(card);

            _state.PlayCard(card);

            Assert.AreEqual(3, _player.Power);
            Assert.Contains(card, _player.PlayedCards);
            Assert.DoesNotContain(card, _player.Hand);
        }

        [TestMethod]
        public void PlayCard_TriggersTargeting_ForAssassinate()
        {
            var card = new Card("kill", "Assassin", 0, CardAspect.Shadow, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            _state.PlayCard(card);

            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);
            Assert.AreEqual(card, _actionSystem.PendingCard);
        }

        [TestMethod]
        public void UpdateMarketLogic_ClosesMarket_WhenClickedOutside()
        {
            _state._isMarketOpen = true;

            // Frame 1: Released
            _mockInputProvider.MouseState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();

            // Frame 2: Clicked at (0,0) - assuming no card is there
            _mockInputProvider.MouseState = new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();

            _state.UpdateMarketLogic();

            Assert.IsFalse(_state._isMarketOpen);
        }

        [TestMethod]
        public void UpdateTargetingLogic_CancelsTargeting_OnRightClick()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate);
            Assert.IsTrue(_actionSystem.IsTargeting());

            // Simulate Right Click
            // Frame 1: Up
            _mockInputProvider.MouseState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();
            // Frame 2: Down (Right Button)
            _mockInputProvider.MouseState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            _input.Update();

            // Act
            _state.UpdateTargetingLogic();

            // Assert
            Assert.IsFalse(_actionSystem.IsTargeting(), "Right-click should cancel targeting mode.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void UpdateMarketLogic_BuysCard_WhenClickedAndAffordable()
        {
            // Arrange
            _state._isMarketOpen = true;
            _player.Influence = 10;

            // Setup a card in the market at specific coordinates
            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0);
            cardToBuy.Position = new Vector2(100, 100); // Set position so we can click it
                                                        // Card.Width is 150, Height is 200 (defined in Card.cs constants)

            _marketManager.MarketRow.Clear();
            _marketManager.MarketRow.Add(cardToBuy);

            // Simulate Left Click ON the card (110, 110 is inside 100,100 -> 250,300)
            // Frame 1: Up
            _mockInputProvider.MouseState = new MouseState(110, 110, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();
            // Frame 2: Down
            _mockInputProvider.MouseState = new MouseState(110, 110, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();

            // Act
            // This calls the logic that includes the "TryBuyCard" check we just fixed
            _state.UpdateMarketLogic();

            // Assert
            Assert.AreEqual(7, _player.Influence, "Influence should decrease by cost (10 - 3 = 7).");
            Assert.Contains(cardToBuy, _player.DiscardPile, "Card should be moved to discard pile.");
            Assert.DoesNotContain(cardToBuy, _marketManager.MarketRow, "Card should be removed from market row.");
            Assert.IsTrue(_state._isMarketOpen, "Market should remain open after a valid purchase.");
        }
    }
}