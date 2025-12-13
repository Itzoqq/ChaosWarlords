using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace ChaosWarlords.Tests.States
{

    // Define the Mock Provider inside the Test project
    public class MockInputProvider : IInputProvider
    {
        // Public fields so we can manipulate them easily in tests
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
        private ActionSystem _actionSystem = null!;

        // Mock dependencies
        // (Note: In a real environment, we'd mock UIManager specifically to return true/false on button hover)
        // Since we can't easily mock UIManager without Moq/Interfaces, we'll null it out and test logic that doesn't depend on it
        // OR we test the internal logic methods directly.

        [TestInitialize]
        public void Setup()
        {
            // 1. Create the Mock
            _mockInputProvider = new MockInputProvider();
            // 2. Inject Mock into Manager
            _input = new InputManager(_mockInputProvider);
            _player = new Player(PlayerColor.Red);
            _mapManager = new MapManager(new List<MapNode>(), new List<Site>());
            _actionSystem = new ActionSystem(_player, _mapManager);

            // Pass null Game to avoid graphics initialization
            _state = new GameplayState(null!);

            // Inject our test dependencies
            _state.InjectDependencies(_input, null!, _mapManager, new MarketManager(), _actionSystem, _player);
        }

        [TestMethod]
        public void EndTurn_ResetsResources_AndDrawsCards()
        {
            // Arrange
            _player.Power = 5;
            _player.Influence = 5;
            _player.Deck.Add(new Card("c1", "Test", 0, CardAspect.Neutral, 0, 0));
            _player.Hand.Clear(); // Ensure hand starts empty

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
            _input.Update(); // InputManager reads "Up"

            // Step 2: Press Key DOWN (Current Frame)
            _mockInputProvider.KeyboardState = new KeyboardState(Keys.Enter);
            _input.Update(); // InputManager reads "Down", so IsKeyJustPressed = true

            // Act
            _player.Power = 10; // Give power to verify it gets reset

            // We call the method that uses IsKeyJustPressed
            _state.HandleGlobalInput();

            // Assert
            Assert.AreEqual(0, _player.Power, "Power should be 0 after EndTurn triggered by Enter key.");
        }

        [TestMethod]
        public void PlayCard_ResolvesResourceEffects()
        {
            // Arrange
            var card = new Card("test", "Resource Card", 0, CardAspect.Neutral, 0, 0);
            card.AddEffect(new CardEffect(EffectType.GainResource, 3, ResourceType.Power));
            _player.Hand.Add(card);

            // Act
            _state.PlayCard(card);

            // Assert
            Assert.AreEqual(3, _player.Power);
            Assert.Contains(card, _player.PlayedCards);
            Assert.DoesNotContain(card, _player.Hand);
        }

        [TestMethod]
        public void PlayCard_TriggersTargeting_ForAssassinate()
        {
            // Arrange
            var card = new Card("kill", "Assassin", 0, CardAspect.Shadow, 0, 0);
            card.AddEffect(new CardEffect(EffectType.Assassinate, 1));

            // Act
            _state.PlayCard(card);

            // Assert
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);
            Assert.AreEqual(card, _actionSystem.PendingCard);
        }

        [TestMethod]
        public void UpdateMarketLogic_ClosesMarket_WhenClickedOutside()
        {
            // Arrange
            _state._isMarketOpen = true;

            // Simulate a sequence of inputs
            // Frame 1: Mouse Released
            _mockInputProvider.MouseState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();

            // Frame 2: Mouse Pressed (Click!)
            _mockInputProvider.MouseState = new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _input.Update();

            // Act
            _state.UpdateMarketLogic();

            // Assert
            Assert.IsFalse(_state._isMarketOpen);
        }
    }
}