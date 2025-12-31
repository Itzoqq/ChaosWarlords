using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;

namespace ChaosWarlords.Source.Core.Interfaces.Rendering
{
    public interface IVictoryView : IDisposable
    {
        Rectangle MainMenuButtonRect { get; }
        bool IsMainMenuHovered { get; set; }
        void Draw(SpriteBatch spriteBatch);
    }
}
