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

        [TestMethod]
        public void MoveCardToPlayed_PreservesXPosition_AndPopsUpY()
        {
            // Arrange: Setup the "Screen" logic manually since we have no GraphicsDevice
            _state._handY = 500;
            _state._playedY = 400; // The "Pop Up" target

            // Create 2 cards in hand
            var card1 = new Card("c1", "Left Card", 0, CardAspect.Neutral, 0, 0);
            var card2 = new Card("c2", "Right Card", 0, CardAspect.Neutral, 0, 0);

            // Manually position them like they would be on screen
            card1.Position = new Vector2(100, 500); // X=100, Y=HandY
            card2.Position = new Vector2(250, 500); // X=250, Y=HandY

            _player.Hand.Add(card1);
            _player.Hand.Add(card2);

            // Act: Play the first card
            _state.MoveCardToPlayed(card1);

            // Assert 1: Card1 should have moved UP (Y changes) but NOT sideways (X stays 100)
            Assert.AreEqual(100, card1.Position.X, "Played card should maintain its X position (Visual Gap).");
            Assert.AreEqual(400, card1.Position.Y, "Played card should move to the Played Y position.");

            // Assert 2: Card2 should NOT have moved at all (Prevents the 'sliding' bug)
            Assert.AreEqual(250, card2.Position.X, "Remaining cards should not slide over.");
            Assert.AreEqual(500, card2.Position.Y, "Remaining cards should stay in the hand row.");

            // Assert 3: Lists are correct
            Assert.Contains(card1, _player.PlayedCards);
            Assert.DoesNotContain(card1, _player.Hand);
        }
    }
}