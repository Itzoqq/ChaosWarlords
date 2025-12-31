using ChaosWarlords.Source.Rendering.ViewModels;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Core.Interfaces.Rendering
{
    public interface IGameplayView
    {
        int HandY { get; }
        int PlayedY { get; }

        List<CardViewModel> HandViewModels { get; }
        List<CardViewModel> PlayedViewModels { get; }
        List<CardViewModel> MarketViewModels { get; }

        void LoadContent(ContentManager content);
        void Update(MatchContext context, InputManager inputManager, bool isMarketOpen);
        void Draw(SpriteBatch spriteBatch, MatchContext context, InputManager inputManager, IUIManager uiManager, bool isMarketOpen, string targetingText, bool isPopupOpen, bool isPauseMenuOpen, bool isReplaying, ChaosWarlords.Source.Core.Data.Dtos.VictoryDto? victoryResult);
        void DrawSetupPhaseOverlay(SpriteBatch spriteBatch, Player activePlayer);
    }
}



