using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.States
{
    public class StateManager
    {
        private readonly Stack<IState> _states = new Stack<IState>();
        private readonly Game _game;

        // We pass the Game reference so states can access GraphicsDevice, Content, etc.
        public StateManager(Game game)
        {
            _game = game;
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
            while (_states.Count > 0)
            {
                PopState();
            }
            PushState(state);
        }

        public void Update(GameTime gameTime)
        {
            if (_states.Count > 0)
                _states.Peek().Update(gameTime);
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_states.Count > 0)
                _states.Peek().Draw(spriteBatch);
        }
    }
}