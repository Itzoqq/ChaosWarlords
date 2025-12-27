using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;


namespace ChaosWarlords.Source.States
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
            PopState();
            PushState(state);
        }

        public IState GetCurrentState()
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

