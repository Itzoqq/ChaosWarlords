using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using System.Diagnostics.CodeAnalysis;

namespace ChaosWarlords.Source.Rendering.UI
{
    [ExcludeFromCodeCoverage]
    public class ButtonManager : IButtonManager
    {
        // Logic Only - No Rendering Dependencies
        private readonly List<SimpleButton> _buttons;

        public ButtonManager()
        {
            _buttons = new List<SimpleButton>();
        }

        public void AddButton(SimpleButton button)
        {
            _buttons.Add(button);
        }

        public void Clear()
        {
            _buttons.Clear();
        }

        public void Update(Point mousePosition, bool isMouseClicked)
        {
            // Reverse loop allows safe modification (like Clear() or Remove()) during iteration
            for (int i = _buttons.Count - 1; i >= 0; i--)
            {
                // Safety check: if clear happened triggered by a previous iteration's click?
                // Actually, if a click triggers Clear(), _buttons.Count becomes 0.
                // If we cache count, we might crash accessing index.
                // We should check Count every safe step or just realize that if we are iterating backwards:
                // If i >= Count (because Count dropped to 0), we stop? No, loop condition is i>=0.
                // But accessing _buttons[i] is dangerous if Count changed?
                // If Count becomes 0, i is still, say, 1. _buttons[1] -> ArgumentOutOfRange.

                // Better approach: Iterate over a COPY for interactions to ensure safety
                // OR check bounds.

                if (i >= _buttons.Count) continue;

                var button = _buttons[i];
                button.Update(mousePosition);

                if (button.IsHovered && isMouseClicked)
                {
                    button.OnClick?.Invoke();

                    // If OnClick cleared the list, we must break to avoid invalid access next loop
                    // or if we rely on reverse loop, the next i-- might still be out of bounds if ALL were removed?
                    // Example: Handled index 1. Click clears all. Count=0. Next i=0. Access _buttons[0] -> Fails if cleared.
                    if (_buttons.Count == 0) return;
                }
            }
        }

        public IEnumerable<SimpleButton> GetButtons()
        {
            return _buttons;
        }

        // Texture disposal should be handled if the manager is destroyed, but usually it lasts for the state.
        // If needed we can implement IDisposable.
    }
}


