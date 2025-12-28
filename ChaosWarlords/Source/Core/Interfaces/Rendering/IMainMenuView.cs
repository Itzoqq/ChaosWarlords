using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Source.Core.Interfaces.Rendering
{
    public interface IMainMenuView
    {
        void LoadContent();
        void UnloadContent();
        void Update(Microsoft.Xna.Framework.GameTime gameTime);
        void Draw(SpriteBatch spriteBatch);
    }
}



