using Microsoft.Xna.Framework;


namespace ChaosWarlords.Source.Core.Interfaces.State
{
    public interface IState
    {
        void LoadContent();
        void UnloadContent();
        void Update(GameTime gameTime);
    }
}



