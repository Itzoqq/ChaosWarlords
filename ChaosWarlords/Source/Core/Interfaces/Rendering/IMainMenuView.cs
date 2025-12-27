using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
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



