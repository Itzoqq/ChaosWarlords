using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Utilities;
using System;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Manages the turn-based lifecycle of a match, including tracking active players and phases.
    /// </summary>
    public interface ITurnManager
    {
        /// <summary>
        /// Gets the list of all players participating in the current match.
        /// </summary>
        List<Player> Players { get; }

        /// <summary>
        /// Gets the player whose turn it currently is. Resolves to ForcedActingPlayer when
        /// set, otherwise CurrentTurnContext.ActivePlayer.
        /// </summary>
        Player ActivePlayer { get; }

        /// <summary>
        /// When set, overrides what ActivePlayer resolves to, WITHOUT touching
        /// CurrentTurnContext (aspect-focus counts, promotion credits, action history all
        /// stay attributed to the real turn owner). Used ONLY for MatchManager's
        /// cross-player forced-discard sequencing (Neogi) - the window during which
        /// rendering/input routing/resource checks need to act on a specific OTHER player,
        /// synchronously, before the real end-of-turn player-switch happens. Not a
        /// general-purpose "impersonate a player" primitive - do not use it outside that
        /// exact call pattern.
        /// </summary>
        Player? ForcedActingPlayer { get; }

        /// <summary>
        /// Begins overriding ActivePlayer to resolve to the given player. See ForcedActingPlayer.
        /// </summary>
        void BeginForcedActingPlayer(Player player);

        /// <summary>
        /// Clears the ActivePlayer override, reverting to CurrentTurnContext.ActivePlayer.
        /// </summary>
        void EndForcedActingPlayer();

        /// <summary>
        /// Gets the context object containing data for the current turn.
        /// </summary>
        TurnContext CurrentTurnContext { get; }

        /// <summary>
        /// Executes a card play action for the current turn.
        /// </summary>
        /// <param name="card">The card to be played.</param>
        void PlayCard(Card card);

        /// <summary>
        /// Concludes the current turn and advances the game state to the next player or phase.
        /// </summary>
        void EndTurn();

    /// <summary>
    /// Event fired when the turn control passes to a new player.
    /// </summary>
    event EventHandler<Player> OnTurnChanged;

    /// <summary>
    /// Gets a player by their color.
    /// </summary>
    /// <param name="color">The player color to search for.</param>
    /// <returns>The player with the specified color, or null if not found.</returns>
    Player? GetPlayerByColor(PlayerColor color);
    }
}



