using NSubstitute;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Rendering.UI;

namespace ChaosWarlords.Tests.Integration.GameStates
{
    [TestClass]

    [TestCategory("Integration")]
    public class MainMenuStateTests
    {
        [TestInitialize]
        public void Setup()
        {
            ChaosWarlords.Tests.Utilities.TestLogger.Initialize();
        }

        [TestMethod]
        public void Constructor_InitializesCorrectly()
        {
            // Arrange
            var mockGame = Substitute.For<Game1>(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var mockInput = Substitute.For<IInputProvider>();

            // Act
            var state = new MainMenuState(mockGame, mockInput, Substitute.For<IStateManager>(), Substitute.For<ICardDatabase>(), Substitute.For<IReplayManager>(), ChaosWarlords.Tests.Utilities.TestLogger.Instance, null!, null!);

            // Assert
            Assert.IsNotNull(state);
        }

        /*
        [TestMethod]
        public void LoadContent_CreatesView_WhenNull_AndDeviceAvailable()
        {
             // Skipped: Cannot easily mock sealed GraphicsAdapter/GraphicsDevice with NSubstitute.
             // Manual verification confirmed the fix.
        }
        */

        [TestMethod]
        public void Update_StartBoundsClick_TriggersGameStart()
        {
            // Arrange
            var mockGame = Substitute.For<Game1>(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var mockInput = Substitute.For<IInputProvider>();
            var mockButtonManager = Substitute.For<IButtonManager>();
            var mockStateManager = Substitute.For<IStateManager>();
            var mockCardDb = Substitute.For<ICardDatabase>();

            // Capture the buttons added to the manager
            SimpleButton? startButton = null;
            mockButtonManager.When(x => x.AddButton(Arg.Any<SimpleButton>()))
                             .Do(x =>
                             {
                                 var btn = x.Arg<SimpleButton>();
                                 if (btn.Text == "Start Game") startButton = btn;
                             });

            // Standard Constructor Injection
            var state = new MainMenuState(mockGame, mockInput, mockStateManager, mockCardDb, Substitute.For<IReplayManager>(), ChaosWarlords.Tests.Utilities.TestLogger.Instance, null!, mockButtonManager);
            state.LoadContent();

            Assert.IsNotNull(startButton, "Start Button was not added");

            // Act
            startButton.OnClick?.Invoke();

            // Assert
            mockStateManager.Received(1).ChangeState(Arg.Any<GameplayState>());
        }

        [TestMethod]
        public void Update_ExitBoundsClick_TriggersExit()
        {
            // Arrange
            var mockGame = Substitute.For<Game1>(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var mockInput = Substitute.For<IInputProvider>();
            var mockButtonManager = Substitute.For<IButtonManager>();
            var mockStateManager = Substitute.For<IStateManager>();
            var mockCardDb = Substitute.For<ICardDatabase>();

            SimpleButton? exitButton = null;
            mockButtonManager.When(x => x.AddButton(Arg.Any<SimpleButton>()))
                             .Do(x =>
                             {
                                 var btn = x.Arg<SimpleButton>();
                                 if (btn.Text == "Exit") exitButton = btn;
                             });

            var state = new MainMenuState(mockGame, mockInput, mockStateManager, mockCardDb, Substitute.For<IReplayManager>(), ChaosWarlords.Tests.Utilities.TestLogger.Instance, null!, mockButtonManager);
            state.LoadContent();

            Assert.IsNotNull(exitButton, "Exit Button was not added");

            // Act
            exitButton.OnClick?.Invoke();

            // Assert
            mockGame.Received(1).Exit();
        }

        [TestMethod]
        public void Update_DelegatesToButtonManager()
        {
            // Arrange
            var mockGame = Substitute.For<Game1>(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var mockInput = Substitute.For<IInputProvider>();
            var mockButtonManager = Substitute.For<IButtonManager>();
            var mockStateManager = Substitute.For<IStateManager>();
            var mockCardDb = Substitute.For<ICardDatabase>();

            var state = new MainMenuState(mockGame, mockInput, mockStateManager, mockCardDb, Substitute.For<IReplayManager>(), ChaosWarlords.Tests.Utilities.TestLogger.Instance, null!, mockButtonManager);

            // Act
            state.Update(new GameTime());

            // Assert
            mockButtonManager.Received(1).Update(Arg.Any<Point>(), Arg.Any<bool>());
        }
        [TestMethod]
        public void Update_WaitReleaseLogic_PreventsDragThroughClick()
        {
            // Arrange
            var mockGame = Substitute.For<Game1>(ChaosWarlords.Tests.Utilities.TestLogger.Instance);
            var mockInput = Substitute.For<IInputProvider>();
            var mockButtonManager = Substitute.For<IButtonManager>();
            var mockStateManager = Substitute.For<IStateManager>();
            var mockCardDb = Substitute.For<ICardDatabase>();

            var state = new MainMenuState(mockGame, mockInput, mockStateManager, mockCardDb, Substitute.For<IReplayManager>(), ChaosWarlords.Tests.Utilities.TestLogger.Instance, null!, mockButtonManager);

            // 1. Initial Load - Button is PRESSED (e.g. from previous screen click)
            mockInput.GetMouseState().Returns(new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            state.LoadContent(); // Captures initial state

            // 2. First Update - Still Pressed (User holding button)
            // Should be ignored (locked)
            mockInput.GetMouseState().Returns(new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            state.Update(new GameTime());

            // Assert: ButtonManager NOT called while locked
            mockButtonManager.DidNotReceive().Update(Arg.Any<Point>(), Arg.Any<bool>());

            // 3. Second Update - Released (User lets go)
            // Should unlock, but NOT trigger click
            mockInput.GetMouseState().Returns(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            state.Update(new GameTime());

            // Assert: ButtonManager called, but isClick should be FALSE
            mockButtonManager.Received(1).Update(Arg.Any<Point>(), false);

            // 4. Third Update - Pressed again (Start of new click)
            mockInput.GetMouseState().Returns(new MouseState(100, 100, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            mockButtonManager.ClearReceivedCalls(); // Clear previous calls
            state.Update(new GameTime());

            // Assert: ButtonManager called, isClick FALSE (press only)
            mockButtonManager.Received(1).Update(new Point(100, 100), false);

            // 5. Fourth Update - Released again (End of new click)
            // Should trigger valid click
            mockInput.GetMouseState().Returns(new MouseState(100, 100, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            mockButtonManager.ClearReceivedCalls();
            state.Update(new GameTime());

            // Assert: ButtonManager called, isClick TRUE
            mockButtonManager.Received(1).Update(new Point(100, 100), true);
        }
    }
}


