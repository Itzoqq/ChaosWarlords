using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Managers;

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
        Card? GetHoveredHandCard();

        /// <summary>
        /// Gets the card currently hovered in the market.
        /// </summary>
        Card? GetHoveredMarketCard();

        /// <summary>
        /// Gets the card currently hovered in the played cards area.
        /// </summary>
        Card? GetHoveredPlayedCard(IInputManager input);

        /// <summary>
        /// Gets the card currently hovered in the browser area.
        /// </summary>
        Card? GetHoveredBrowserCard();

        /// <summary>
        /// Gets the spy color clicked in the spy return UI.
        /// </summary>
        PlayerColor? GetClickedSpyReturnButton(Point mousePos, Site site, int screenWidth);

        /// <summary>
        /// Gets the opponent color clicked in the opponent-selection UI (e.g. Cranium Rats'
        /// "choose one opponent"). Iterates all players except <paramref name="activePlayer"/>
        /// (can't target yourself) - clicking a row for a player whose hand doesn't exceed
        /// <paramref name="eligibilityThreshold"/> returns null (ineligible rows are
        /// grayed-out/unclickable, matching DrawOpponentSelectionUI).
        /// </summary>
        PlayerColor? GetClickedOpponentSelectButton(Point mousePos, IReadOnlyList<Player> allPlayers, Player activePlayer, int eligibilityThreshold, int screenWidth);
    }
}

