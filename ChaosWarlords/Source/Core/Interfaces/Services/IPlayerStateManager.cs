using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Core.Interfaces.Services
{
    /// <summary>
    /// Defines the contract for muting player state (Resources, Hand, Deck, etc.).
    /// Centralizes all state changes to facilitate logging, networking, and replay systems.
    /// </summary>
    public interface IPlayerStateManager
    {
        // --- Resources ---

        /// <summary>
        /// Adds power to the player's resource pool.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The amount of power to add.</param>
        void AddPower(Player player, int amount);

        /// <summary>
        /// Attempts to spend power for a player.
        /// </summary>
        /// <param name="player">The purchasing player.</param>
        /// <param name="amount">The cost in power.</param>
        /// <returns>True if the player had enough power and it was deducted; otherwise, false.</returns>
        bool TrySpendPower(Player player, int amount);

        /// <summary>
        /// Adds influence to the player's resource pool.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The amount of influence to add.</param>
        void AddInfluence(Player player, int amount);

        /// <summary>
        /// Attempts to spend influence for a player.
        /// </summary>
        /// <param name="player">The purchasing player.</param>
        /// <param name="amount">The cost in influence.</param>
        /// <returns>True if the player had enough influence and it was deducted; otherwise, false.</returns>
        bool TrySpendInfluence(Player player, int amount);

        /// <summary>
        /// Awards victory points to a player.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The number of points to award.</param>
        void AddVictoryPoints(Player player, int amount);

        // --- Military ---

        /// <summary>
        /// Adds troops to the player's reserve barracks.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The number of troops to add.</param>
        void AddTroops(Player player, int amount);

        /// <summary>
        /// Removes troops from the player's reserve barracks.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The number of troops to remove.</param>
        void RemoveTroops(Player player, int amount);

        /// <summary>
        /// Adds spies to the player's available supply.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The number of spies to add.</param>
        void AddSpies(Player player, int amount);

        /// <summary>
        /// Removes spies from the player's available supply.
        /// </summary>
        /// <param name="player">The target player.</param>
        /// <param name="amount">The number of spies to remove.</param>
        void RemoveSpies(Player player, int amount);

        /// <summary>
        /// Awards a generic trophy to the player (unused placeholder).
        /// </summary>
        /// <param name="player">The target player.</param>
        void AddTrophy(Player player);

        // --- Card Management ---

        /// <summary>
        /// Draws cards from the player's deck into their hand.
        /// </summary>
        /// <param name="player">The player drawing cards.</param>
        /// <param name="count">The number of cards to draw.</param>
        /// <param name="random">The RNG source for shuffles.</param>
        void DrawCards(Player player, int count, IGameRandom random);

        /// <summary>
        /// Moves a card from the Hand to the Played area.
        /// </summary>
        /// <param name="player">The player playing the card.</param>
        /// <param name="card">The card being played.</param>
        void PlayCard(Player player, Card card);

        /// <summary>
        /// Adds a newly acquired card (e.g. from market) to the player's discard pile.
        /// </summary>
        /// <param name="player">The acquiring player.</param>
        /// <param name="card">The new card.</param>
        void AcquireCard(Player player, Card card);

        /// <summary>
        /// Attempts to promote a card to the Inner Circle.
        /// </summary>
        /// <param name="player">The player promoting the card.</param>
        /// <param name="card">The card to promote.</param>
        /// <param name="errorMessage">Output error if promotion fails.</param>
        /// <returns>True if successful; otherwise, false.</returns>
        bool TryPromoteCard(Player player, Card card, out string errorMessage);

        /// <summary>
        /// Permanently removes a card from the player's ownership.
        /// </summary>
        /// <param name="player">The owner of the card.</param>
        /// <param name="card">The card to devour.</param>
        void DevourCard(Player player, Card card);

        // --- Turn Management ---

        /// <summary>
        /// Performs end-of-turn cleanup for the player, discarding hand and played cards.
        /// </summary>
        /// <param name="player">The player ending their turn.</param>
        void CleanUpTurn(Player player);
    }
}
