using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChaosWarlords.Source.Rendering.UI;

namespace ChaosWarlords.Source.Core.Interfaces
{
    public interface IButtonManager
    {
        void AddButton(SimpleButton button);
        void Update(Point mousePosition, bool isMouseClicked);
        void Draw(SpriteBatch spriteBatch);
        void Clear();
    }
}
