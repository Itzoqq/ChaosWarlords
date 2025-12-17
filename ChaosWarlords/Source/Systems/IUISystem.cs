using System;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Source.Systems
{
    public interface IUISystem
    {
        // Layout and Draw properties
        int ScreenWidth { get; }
        int ScreenHeight { get; }

        // --- Layout Data for Rendering ---
        Rectangle MarketButtonRect { get; }
        Rectangle AssassinateButtonRect { get; }
        Rectangle ReturnSpyButtonRect { get; }

        // Input Handling
        void Update(InputManager input);

        // Events
        event EventHandler OnMarketToggleRequest;
        event EventHandler OnAssassinateRequest;
        event EventHandler OnReturnSpyRequest;

        // Querying state (Hovering)
        bool IsMarketHovered { get; }
        bool IsAssassinateHovered { get; }
        bool IsReturnSpyHovered { get; }
    }
}