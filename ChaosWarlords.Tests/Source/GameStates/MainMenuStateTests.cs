using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Core.Interfaces;
using ChaosWarlords.Source.Interfaces;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Rendering.UI;

namespace ChaosWarlords.Tests.GameStates
{
    [TestClass]
    public class MainMenuStateTests
    {
        [TestInitialize]
        public void Setup()
        {
            GameLogger.Initialize();
        }

        [TestMethod]
        public void Constructor_InitializesCorrectly()
        {
            // Arrange
            var mockGame = Substitute.For<Game1>();
            var mockInput = Substitute.For<IInputProvider>();

            // Act
            var state = new MainMenuState(mockGame, mockInput, Substitute.For<IStateManager>(), Substitute.For<ICardDatabase>(), null, null);

            // Assert
            Assert.IsNotNull(state);
        }

        [TestMethod]
        public void Update_StartBoundsClick_TriggersGameStart()
        {
            // Arrange
            var mockGame = Substitute.For<Game1>();
            var mockInput = Substitute.For<IInputProvider>();
            var mockButtonManager = Substitute.For<IButtonManager>();
            var mockStateManager = Substitute.For<IStateManager>();
            var mockCardDb = Substitute.For<ICardDatabase>();

            // Capture the buttons added to the manager
            SimpleButton startButton = null;
            mockButtonManager.When(x => x.AddButton(Arg.Any<SimpleButton>()))
                             .Do(x => 
                             {
                                 var btn = x.Arg<SimpleButton>();
                                 if (btn.Text == "Start Game") startButton = btn;
                             });

            // Standard Constructor Injection
            var state = new MainMenuState(mockGame, mockInput, mockStateManager, mockCardDb, null, mockButtonManager);
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
            var mockGame = Substitute.For<Game1>();
            var mockInput = Substitute.For<IInputProvider>();
            var mockButtonManager = Substitute.For<IButtonManager>();
            var mockStateManager = Substitute.For<IStateManager>();
            var mockCardDb = Substitute.For<ICardDatabase>();
            
            SimpleButton exitButton = null;
            mockButtonManager.When(x => x.AddButton(Arg.Any<SimpleButton>()))
                             .Do(x => 
                             {
                                 var btn = x.Arg<SimpleButton>();
                                 if (btn.Text == "Exit") exitButton = btn;
                             });

            var state = new MainMenuState(mockGame, mockInput, mockStateManager, mockCardDb, null, mockButtonManager);
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
            var mockGame = Substitute.For<Game1>();
            var mockInput = Substitute.For<IInputProvider>();
            var mockButtonManager = Substitute.For<IButtonManager>();
            var mockStateManager = Substitute.For<IStateManager>();
            var mockCardDb = Substitute.For<ICardDatabase>();

            var state = new MainMenuState(mockGame, mockInput, mockStateManager, mockCardDb, null, mockButtonManager);
            
            // Act
            state.Update(new GameTime());

            // Assert
            mockButtonManager.Received(1).Update(Arg.Any<Point>(), Arg.Any<bool>());
        }
    }
}
