using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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



