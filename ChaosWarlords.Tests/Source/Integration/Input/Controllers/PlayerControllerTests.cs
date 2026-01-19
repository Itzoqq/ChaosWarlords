using ChaosWarlords.Source.Input.Controllers;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.State; 
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Input; 
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using NSubstitute;
using ChaosWarlords.Tests.Source.Doubles.State;
using ChaosWarlords.Source.Core.Events;
using System;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities; // Added for Enums
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosWarlords.Tests.Integration.Input.Controllers
{
    [TestClass]
    [TestCategory("Integration")]
    public class PlayerControllerTests
    {
        private PlayerController _controller = null!;
        private TestGameplayState _stateFake = null!;
        private IInputManager _mockInputManager = null!;
        private IActionSystem _mockActionSystem = null!;
        private IInteractionMapper _mockMapper = null!; 
        private IGameplayInputCoordinator _mockCoordinator = null!; 

        [TestInitialize]
        public void Setup()
        {
            _mockInputManager = Substitute.For<IInputManager>();
            _mockActionSystem = Substitute.For<IActionSystem>();
            _mockMapper = Substitute.For<IInteractionMapper>();
            _mockCoordinator = Substitute.For<IGameplayInputCoordinator>();

            _stateFake = new TestGameplayState
            {
                InputManager = _mockInputManager,
                ActionSystem = _mockActionSystem
            };
            
            // Fix Constructor: Pass Coordinator
            _controller = new PlayerController(_stateFake, _mockInputManager, _mockCoordinator, _mockMapper);
        }

        [TestMethod]
        public void Update_DelegatesToInputManagerUpdate()
        {
            // Act
            _controller.Update();

            // Assert
            _mockCoordinator.Received(1).HandleInput();
        }
        
        [TestMethod]
        public void HandleEscapeKey_CallsGameStateEscapeHandler()
        {
            // Arrange
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Escape);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsTrue(_stateFake.EscapeHandled, "State should acknowledge Escape key press.");
        }

        [TestMethod]
        public void HandleEnterKey_EndsTurn_WhenAllowed()
        {
            // Arrange
            _stateFake.IsPauseMenuOpen = false;
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Enter);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsTrue(_stateFake.EndTurnRequested, "State should have received EndTurn request.");
        }

        [TestMethod]
        public void Update_WhenPaused_DoesNotProcessGameplayInput()
        {
            // Arrange
            _stateFake.IsPauseMenuOpen = true;
            // Enter key usually ends turn, but if paused it shouldn't.
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Enter);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsFalse(_stateFake.EndTurnRequested, "Should not end turn while paused.");
        }
        
        [TestMethod]
        public void HandleOneKey_SelectsCard_Index0()
        {
             // Arrange
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.D1);
            
            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);
            
            // Assert
            // Verify state interaction or UI interaction
        }

        [TestMethod]
        public void HandleEnterKey_DoesNotEndTurn_WhenPauseMenuOpen()
        {
            // Arrange
            _stateFake.IsPauseMenuOpen = true;
            var evt = new InputEventArgs(InputEventType.KeyDown, Vector2.Zero, Keys.Enter);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsFalse(_stateFake.EndTurnRequested, "Should NOT request EndTurn when Pause Menu is open.");
        }

        [TestMethod]
        public void HandleRightClick_ClosesMarket_WhenOpen()
        {
            // Arrange
            _stateFake.MarketStateManager.OpenForBrowsing();
            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            Assert.IsFalse(_stateFake.IsMarketOpen, "Market should be closed by right click.");
        }

        [TestMethod]
        public void HandleRightClick_CancelsTargeting_WhenTargeting()
        {
            // Arrange
            _stateFake.MarketStateManager.Close();

            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.IsTargeting().Returns(true);
            _stateFake.ActionSystem = mockActionSystem; // Inject mock into fake
            _stateFake.InitializeMatchContext(); // Update MatchContext to use new mock

            var evt = new InputEventArgs(InputEventType.RightClick, Vector2.Zero);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            mockActionSystem.Received(1).CancelTargeting();

            // Verify state logic (PlayerController calls SwitchToNormalMode on state)
            Assert.AreEqual("Normal", _stateFake.ActiveModeName, "Should switch to Normal mode.");
        }

        [TestMethod]
        public void HandleSpySelectionInput_FinalizesSpyReturn()
        {
            // Arrange
            var mockActionSystem = Substitute.For<IActionSystem>();
            mockActionSystem.CurrentState.Returns(ActionState.SelectingSpyToReturn);
            var mockSite = TestData.Sites.CitySite();
            mockActionSystem.PendingSite.Returns(mockSite);

            _stateFake.ActionSystem = mockActionSystem;
            _stateFake.InitializeMatchContext();

            var evt = new InputEventArgs(InputEventType.LeftClick, new Vector2(100, 100));

            var mockUIManager = Substitute.For<IUIManager>();
            mockUIManager.ScreenWidth.Returns(800);
            _stateFake.UIManager = mockUIManager;

            _mockMapper.GetClickedSpyReturnButton(Arg.Any<Point>(), mockSite, 800)
                .Returns(PlayerColor.Blue);

            // Act
            _mockInputManager.OnInputEvent += Raise.Event<EventHandler<InputEventArgs>>(_mockInputManager, evt);

            // Assert
            mockActionSystem.Received(1).FinalizeSpyReturn(PlayerColor.Blue);
        }
    }
}
