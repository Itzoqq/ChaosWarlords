using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Data;
using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords.Source.Core.Interfaces.Input
{
    /// <summary>
    /// Interface for mapping mouse interactions to game entities.
    /// Extracted to enable unit testing with NSubstitute.
    /// </summary>
    public interface IInteractionMapper
    {
        /// <summary>
        /// Gets the card currently hovered in the player's hand.
        /// </summary>
        Card GetHoveredHandCard();

        /// <summary>
        /// Gets the card currently hovered in the market.
        /// </summary>
        Card GetHoveredMarketCard();

        /// <summary>
        /// Gets the card currently hovered in the played cards area.
        /// </summary>
        Card GetHoveredPlayedCard(InputManager input);

        /// <summary>
        /// Gets the spy color clicked in the spy return UI.
        /// </summary>
        PlayerColor? GetClickedSpyReturnButton(Point mousePos, Site site, int screenWidth);
    }
}




