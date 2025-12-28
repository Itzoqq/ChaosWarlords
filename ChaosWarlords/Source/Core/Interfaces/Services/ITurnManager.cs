using System.Collections.Generic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Actors;
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
        /// Gets the player whose turn it currently is.
        /// Convenience property for CurrentTurnContext.ActivePlayer.
        /// </summary>
        Player ActivePlayer { get; }

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
    }
}



