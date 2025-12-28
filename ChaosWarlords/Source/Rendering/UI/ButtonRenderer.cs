using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System;

namespace ChaosWarlords.Source.Rendering.UI
{
    [ExcludeFromCodeCoverage]
    public class ButtonRenderer : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteFont _font;
        private Texture2D _pixelTexture;

        public ButtonRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
        {
            _graphicsDevice = graphicsDevice;
            _font = font;

            // Create a 1x1 white texture for drawing primitives
            _pixelTexture = new Texture2D(_graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        public void Draw(SpriteBatch spriteBatch, IEnumerable<SimpleButton> buttons)
        {
            foreach (var button in buttons)
            {
                button.Draw(spriteBatch, _pixelTexture, _font);
            }
        }
        public void Dispose()
        {
            _pixelTexture?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}


