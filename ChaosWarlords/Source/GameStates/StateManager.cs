using ChaosWarlords.Source.Core.Interfaces.State;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.GameStates
{
    public class StateManager : IStateManager
    {
        // Internal: Allows Unit Tests to verify "Did the state actually pop?"
        internal Stack<IState> _states;

        private readonly Game _game;

        public StateManager(Game game)
        {
            _game = game;
            _states = new Stack<IState>();
        }

        public void PushState(IState state)
        {
            state.LoadContent();
            _states.Push(state);
        }

        public void PopState()
        {
            if (_states.Count > 0)
            {
                var state = _states.Pop();
                state.UnloadContent();
            }
        }

        public void ChangeState(IState state)
        {
            // Load the NEW state's content BEFORE touching the old one. If LoadContent() throws
            // (e.g. a missing font/texture), the old state must stay exactly as it was - still on
            // the stack, still functional - rather than _states ending up empty: Update()/Draw()
            // both no-op on an empty stack (see below), which would otherwise turn a load failure
            // into a permanent, silent blank-screen freeze with no path back to a working state
            // or to Program.cs's top-level crash handler.
            state.LoadContent();

            if (_states.Count > 0)
            {
                var oldState = _states.Pop();
                oldState.UnloadContent();
            }

            _states.Push(state);
        }

        public IState? GetCurrentState()
        {
            return _states.Count > 0 ? _states.Peek() : null;
        }

        public void Update(GameTime gameTime)
        {
            if (_states.Count > 0)
                _states.Peek().Update(gameTime);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_states.Count > 0 && _states.Peek() is IDrawableState drawableState)
                drawableState.Draw(spriteBatch);
        }
    }
}

