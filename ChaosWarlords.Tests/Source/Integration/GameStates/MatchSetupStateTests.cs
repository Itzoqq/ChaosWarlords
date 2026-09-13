using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Rendering.UI;
using ChaosWarlords.Source.Utilities;
using NSubstitute;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Tests.Integration.GameStates
{
    [TestClass]
    [TestCategory("Integration")]
    public class MatchSetupStateTests
    {
        [TestMethod]
        public void LoadContent_CyclesSelectedHalfDeckWithoutAllowingDuplicates()
        {
            var buttons = Substitute.For<IButtonManager>();
            SimpleButton? firstHalfDeckButton = null;
            buttons.When(manager => manager.AddButton(Arg.Any<SimpleButton>())).Do(call =>
            {
                var button = call.Arg<SimpleButton>();
                if (button.Text.StartsWith("First half-deck:")) firstHalfDeckButton = button;
            });
            var state = CreateState(buttons, Substitute.For<IStateManager>());

            state.LoadContent();
            firstHalfDeckButton!.OnClick();

            // Default is Drow+Dragons - cycling First skips Dragons (the other selected half-
            // deck) and lands on Elemental, the next MarketHalfDeck enum value.
            Assert.AreEqual(MarketHalfDeck.Elemental, state.MarketDeckSelection.First);
            Assert.AreNotEqual(state.MarketDeckSelection.First, state.MarketDeckSelection.Second);
            Assert.AreEqual("First half-deck: Elemental", firstHalfDeckButton.Text);
        }

        [TestMethod]
        public void StartMatch_UsesConfirmedHalfDeckSelectionToCreateGameplayState()
        {
            var buttons = Substitute.For<IButtonManager>();
            SimpleButton? startButton = null;
            buttons.When(manager => manager.AddButton(Arg.Any<SimpleButton>())).Do(call =>
            {
                var button = call.Arg<SimpleButton>();
                if (button.Text == "Start Match") startButton = button;
            });
            var stateManager = Substitute.For<IStateManager>();
            var state = CreateState(buttons, stateManager);

            state.LoadContent();
            startButton!.OnClick();

            stateManager.Received(1).ChangeState(Arg.Any<GameplayState>());
        }

        [TestMethod]
        public void Update_WaitsForInitialReleaseThenRoutesFreshClicksToButtons()
        {
            var buttons = Substitute.For<IButtonManager>();
            var input = Substitute.For<IInputProvider>();
            input.GetMouseState().Returns(
                new MouseState(0, 0, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new MouseState(10, 20, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new MouseState(10, 20, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            var state = new MatchSetupState(Substitute.For<Game1>(Utilities.TestLogger.Instance), input, Substitute.For<IStateManager>(),
                Substitute.For<ICardDatabase>(), Substitute.For<IReplayManager>(), Utilities.TestLogger.Instance, null, buttons);

            state.LoadContent();
            state.Update(new GameTime());
            state.Update(new GameTime());
            state.Update(new GameTime());
            state.Update(new GameTime());

            buttons.Received().Update(new Point(10, 20), true);
        }

        private static MatchSetupState CreateState(IButtonManager buttons, IStateManager stateManager) => new(
            Substitute.For<Game1>(Utilities.TestLogger.Instance), Substitute.For<IInputProvider>(), stateManager,
            Substitute.For<ICardDatabase>(), Substitute.For<IReplayManager>(), Utilities.TestLogger.Instance, null, buttons);
    }
}
