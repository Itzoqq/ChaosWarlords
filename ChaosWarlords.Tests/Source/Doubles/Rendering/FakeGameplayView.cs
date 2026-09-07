using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Rendering.ViewModels;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChaosWarlords.Tests.Source.Doubles.Rendering
{
    /// <summary>
    /// Minimal, fully-controllable IGameplayView for InputPipelineScenario. Actual rendering is
    /// deliberately out of scope for this project's test suite (see planning.txt) - but the
    /// hover/ViewModel CONTRACT this interface exposes to InteractionMapper and the IInputMode
    /// implementations (GetHoveredHandCard etc.) is real production surface that click routing
    /// depends on, so it's faked here (settable ViewModel lists) rather than mocked away with a
    /// blanket Substitute.
    /// </summary>
    public class FakeGameplayView : IGameplayView
    {
        public int HandY { get; set; }
        public int PlayedY { get; set; }

        public List<CardViewModel> HandViewModels { get; } = new();
        public List<CardViewModel> PlayedViewModels { get; } = new();
        public List<CardViewModel> MarketViewModels { get; } = new();
        public List<CardViewModel> BrowserViewModels { get; } = new();

        public bool IsOptionalEffectPopupOpen { get; set; }

        public int OptionalEffectClickCount { get; private set; }
        public (int X, int Y)? LastOptionalEffectClickPosition { get; private set; }

        public void LoadContent(ContentManager content)
        {
        }

        public void Update(MatchContext context, IInputManager inputManager, bool isMarketOpen, bool isOptionalPopupOpen)
        {
        }

        public void Draw(SpriteBatch spriteBatch, MatchContext context, IInputManager inputManager, IUIManager uiManager, bool isMarketOpen, string targetingText, bool isPopupOpen, bool isPauseMenuOpen, bool isReplaying, IMatchManager matchManager)
        {
        }

        public void DrawSetupPhaseOverlay(SpriteBatch spriteBatch, Player activePlayer)
        {
        }

        public void HandleOptionalEffectClick(int mouseX, int mouseY)
        {
            OptionalEffectClickCount++;
            LastOptionalEffectClickPosition = (mouseX, mouseY);
        }

        public bool HandleOptionalEffectAccept() => false;
    }
}
