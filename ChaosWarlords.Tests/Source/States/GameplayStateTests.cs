using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands; // [NEW] Required for Commands
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosWarlords.Tests.States
{
    // Define the Mock Provider inside the Test project
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
            // 1. Create the Input Mock
            _mockInputProvider = new MockInputProvider();

            // 2. Inject Input Mock into InputManager
            _input = new InputManager(_mockInputProvider);

            // 3. Setup Player with Cards
            _player = new Player(PlayerColor.Red);
            for (int i = 0; i < 10; i++)
            {
                _player.Deck.Add(CardFactory.CreateSoldier());
            }
            _player.DrawCards(5);

            // 4. Setup other Systems
            _mapManager = new MapManager(new List<MapNode>(), new List<Site>());
            _marketManager = new MarketManager();
            _actionSystem = new ActionSystem(_player, _mapManager);

            // --- FIX START --- 
            // We must create a real UIManager for tests, otherwise _uiManager.HandleInput throws NullReference
            var testUiManager = new UIManager(800, 600);
            // --- FIX END ---

            // Create the Mock DB
            var mockDb = new MockCardDatabase();

            // Pass it to the constructor
            _state = new GameplayState(null!, _mockInputProvider, mockDb);

            // Pass testUiManager instead of null!
            _state.InjectDependencies(_input, testUiManager, _mapManager, _marketManager, _actionSystem, _player);
        }

        [TestMethod]
        public void EndTurn_ResetsResources_AndDrawsCards()
        {
            // 1. Arrange
            _player.Hand.Clear();
            _player.Deck.Clear();
            _player.DiscardPile.Clear();
            _player.PlayedCards.Clear();

            // Add 5 cards so the logic can actually find 5 to draw
            for (int i = 0; i < 5; i++)
            {
                _player.Deck.Add(CardFactory.CreateSoldier());
            }

            _player.Power = 5;

            // 2. Act
            _state.EndTurn();

            // 3. Assert
            Assert.AreEqual(0, _player.Power, "Power should reset to 0.");
            // Temporary 5 card draw since when you end turn in testing phase
            // You immediately start a new turn and draw a new hand (5 cards).
            Assert.HasCount(5, _player.Hand, "Should draw exactly 5 cards.");
        }

        [TestMethod]
        public void HandleGlobalInput_EnterKey_EndsTurn()
        {
            // Step 1: Ensure Key is UP (Previous Frame)
            _mockInputProvider.SetKeyboardState();
            _input.Update();

            // Step 2: Press Key DOWN (Current Frame)
            _mockInputProvider.SetKeyboardState(Keys.Enter);
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
            // This tests that if we click but NO command is generated (clicked empty space), market closes.
            _state._isMarketOpen = true;

            // Simulate a click that hits nothing (UIManager will return null)
            _mockInputProvider.Reset();
            _input.Update();
            _mockInputProvider.QueueLeftClick();
            _input.Update();

            // Act
            // We call UpdateMarketLogic directly as per the original test structure
            _state.UpdateMarketLogic();

            // Assert
            Assert.IsFalse(_state._isMarketOpen, "Market should close if clicked outside/no command executed.");
        }

        [TestMethod]
        public void UpdateTargetingLogic_CancelsTargeting_OnRightClick()
        {
            // Arrange
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate);

            // Step 1: Ensure button starts RELEASED (Frame 1)
            _mockInputProvider.Reset();
            _input.Update();

            // Step 2: Press button DOWN (Frame 2)
            _mockInputProvider.QueueRightClick();
            _input.Update();

            // Act
            _state.HandleGlobalInput();

            // Assert
            Assert.IsFalse(_actionSystem.IsTargeting(), "Right-click (Press) should cancel targeting mode.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void Command_BuyCard_BuysCard_WhenAffordable()
        {
            // [REFACTORED] Uses Command Pattern instead of Mouse Clicks
            // Was: UpdateMarketLogic_BuysCard_WhenClickedAndAffordable

            // Arrange
            _state._isMarketOpen = true;
            _player.Influence = 10;

            var cardToBuy = new Card("market_card", "Buy Me", 3, CardAspect.Sorcery, 1, 0);
            _marketManager.MarketRow.Clear();
            _marketManager.MarketRow.Add(cardToBuy);

            // Act: Execute the command directly
            var command = new BuyCardCommand(cardToBuy);
            command.Execute(_state);

            // Assert
            Assert.AreEqual(7, _player.Influence, "Influence should decrease by cost (10 - 3 = 7).");
            Assert.Contains(cardToBuy, _player.DiscardPile, "Card should be moved to discard pile.");
            Assert.DoesNotContain(cardToBuy, _marketManager.MarketRow, "Card should be removed from market row.");
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

        [TestMethod]
        public void Update_RightClick_CancelsTargetingState()
        {
            // [REDUNDANT but preserved] Similar to UpdateTargetingLogic_CancelsTargeting_OnRightClick
            // 1. Arrange: Put the game into a "Targeting" state
            _actionSystem.StartTargeting(ActionState.TargetingAssassinate, CardFactory.CreateSoldier());

            // 2. Act: Simulate a Right Click
            _mockInputProvider.QueueRightClick();
            _state.Update(new GameTime());

            // 3. Assert
            Assert.IsFalse(_actionSystem.IsTargeting(), "Right-click should have cancelled the targeting state.");
            Assert.AreEqual(ActionState.Normal, _actionSystem.CurrentState);
        }

        [TestMethod]
        public void Command_ToggleMarket_TogglesState()
        {
            // [REFACTORED] Uses Command Pattern
            // Was: Update_ClickingMarketButton_TogglesMarket

            // 1. Arrange
            _state._isMarketOpen = true;

            // 2. Act: Execute Command
            var command = new ToggleMarketCommand();
            command.Execute(_state);

            // 3. Assert
            Assert.IsFalse(_state._isMarketOpen, "Toggle command should close the market if open.");
        }

        [TestMethod]
        public void PlayCard_Directly_PlaysCard()
        {
            // [REFACTORED] Bypassing mouse click to test logic directly
            // Was: Update_ClickingCard_PlaysIt

            // 1. Arrange
            var card = _player.Hand[_player.Hand.Count - 1];

            // 2. Act: Call Logic Directly
            _state.PlayCard(card);

            // 3. Assert
            Assert.DoesNotContain(card, _player.Hand, "The clicked card should be removed from Hand.");
            Assert.Contains(card, _player.PlayedCards, "The clicked card should be in PlayedCards.");
        }

        [TestMethod]
        public void Update_PressingEnter_EndsTurn()
        {
            // 1. Arrange
            int initialDeckCount = _player.Deck.Count;
            // Play a card so we have something to clean up
            var card = _player.Hand[0];
            _state.PlayCard(card);

            // 2. Act: Press Enter
            _mockInputProvider.SetKeyboardState(Keys.Enter);
            _state.Update(new GameTime());

            // 3. Assert
            Assert.IsEmpty(_player.PlayedCards, "Played cards should be discarded/cleaned up.");
            Assert.HasCount(5, _player.Hand, "Player should have drawn a new hand of 5.");
        }

        [TestMethod]
        public void Command_DeployTroop_DeploysTroop()
        {
            // [REFACTORED] Uses Command Pattern
            // Was: Update_ClickingMapNode_DeploysTroop

            // 1. Arrange
            var node = new MapNode(1, new Vector2(500, 500));
            var baseNode = new MapNode(2, new Vector2(550, 550));
            baseNode.Occupant = PlayerColor.Red;

            node.Neighbors.Add(baseNode);
            baseNode.Neighbors.Add(node);

            _mapManager.Nodes.Add(node);
            _mapManager.Nodes.Add(baseNode);

            _player.TroopsInBarracks = 1;
            _player.Power = 1;

            // 2. Act: Execute Command
            var command = new DeployTroopCommand(node);
            command.Execute(_state);

            // 3. Assert
            Assert.AreEqual(PlayerColor.Red, node.Occupant, "Command should deploy a Red troop.");
        }

        [TestMethod]
        public void PlayCard_GainsResources()
        {
            // 1. Arrange
            _player.Influence = 0;
            _player.Power = 0;

            var richNoble = new Card("rich_noble", "Rich Noble", 0, CardAspect.Order, 0, 0);
            richNoble.Effects.Add(new CardEffect(EffectType.GainResource, 3, ResourceType.Influence));

            _player.Hand.Add(richNoble);

            // 2. Act
            _state.PlayCard(richNoble);

            // 3. Assert
            Assert.AreEqual(3, _player.Influence, "Playing the card should grant 3 Influence.");
        }

        [TestMethod]
        public void Logic_AssassinateTarget_KillsEnemy()
        {
            // [REFACTORED] Tests Logic directly via ActionSystem
            // Was: Update_AssassinateTarget_KillsEnemy

            // 1. Arrange
            var enemyNode = new MapNode(99, new Vector2(600, 600));
            enemyNode.Occupant = PlayerColor.Blue;

            var myNode = new MapNode(100, new Vector2(650, 650));
            myNode.Occupant = PlayerColor.Red;

            enemyNode.Neighbors.Add(myNode);
            myNode.Neighbors.Add(enemyNode);

            _mapManager.Nodes.Add(enemyNode);
            _mapManager.Nodes.Add(myNode);

            var assassin = new Card("assassin", "Assassin", 0, CardAspect.Shadow, 0, 0);
            assassin.Effects.Add(new CardEffect(EffectType.Assassinate, 0));
            _player.Hand.Add(assassin);

            // 2. Act
            _state.PlayCard(assassin);
            Assert.AreEqual(ActionState.TargetingAssassinate, _actionSystem.CurrentState);

            // Logic: Target the enemy
            bool success = _actionSystem.HandleTargetClick(enemyNode, null);

            // 3. Assert
            Assert.IsTrue(success, "Action should succeed.");
            Assert.AreEqual(PlayerColor.None, enemyNode.Occupant, "Enemy should be removed (Empty) after assassination.");
            // We don't check state resetting here because GameplayState handles the reset after success is returned
        }
    }
}