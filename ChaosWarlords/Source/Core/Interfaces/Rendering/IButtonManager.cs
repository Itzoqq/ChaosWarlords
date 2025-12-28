using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Rendering.UI;

namespace ChaosWarlords.Source.Core.Interfaces.Rendering
{
    public interface IButtonManager
    {
        void AddButton(SimpleButton button);
        void Update(Point mousePosition, bool isMouseClicked);
        void Clear();
        System.Collections.Generic.IEnumerable<SimpleButton> GetButtons();
    }
}



