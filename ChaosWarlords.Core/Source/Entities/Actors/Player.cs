using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Entities.Cards;

namespace ChaosWarlords.Source.Entities.Actors
{
    /// <summary>
    /// Represents a participant in the game session.
    /// Manages resources, card collections (Deck/Hand/Discard), and military assets.
    /// </summary>
    public class Player
    {
        // --- Identity ---

        /// <summary>
        /// Unique identifier for this player across all matches.
        /// Used for player tracking, statistics, and reconnection in multiplayer.
        /// </summary>
        public Guid PlayerId { get; private set; }

        /// <summary>
        /// The seat index of the player in the current match (0, 1, 2, ...).
        /// Used for deterministic lookups and smaller network packets.
        /// </summary>
        public int SeatIndex { get; internal set; }

        /// <summary>
        /// Display name for this player (for UI purposes).
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The faction color this player is using for the current match.
        /// </summary>
        public PlayerColor Color { get; private set; }

        // --- Economy ---
        private int _power;
        private int _influence;

        /// <summary>
        /// Gets the current Power resource amount.
        /// Power is primarily used for deploying troops and assassinating spies.
        /// </summary>
        public int Power => _power;

        /// <summary>
        /// Gets the current Influence resource amount.
        /// Influence is primarily used for purchasing cards and placing spies.
        /// </summary>
        public int Influence => _influence;

        // --- Resource Management ---

        /// <summary>
        /// Adds Power to the player's pool.
        /// </summary>
        public void AddPower(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Cannot add negative power.");
            _power += amount;
        }

        /// <summary>
        /// Attempts to spend Power.
        /// </summary>
        /// <returns>True if successful, false if insufficient funds.</returns>
        public bool SpendPower(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Cannot spend negative power.");
            if (_power >= amount)
            {
                _power -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets Power to an absolute value, bypassing Add/Spend validation.
        /// Internal access for state restoration (e.g. transactional rollback) only.
        /// </summary>
        internal void SetPower(int amount) => _power = amount;

        /// <summary>
        /// Sets Influence to an absolute value, bypassing Add/Spend validation.
        /// Internal access for state restoration (e.g. transactional rollback) only.
        /// </summary>
        internal void SetInfluence(int amount) => _influence = amount;

        /// <summary>
        /// Adds Influence to the player's pool.
        /// </summary>
        public void AddInfluence(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Cannot add negative influence.");
            _influence += amount;
        }

        /// <summary>
        /// Attempts to spend Influence.
        /// </summary>
        /// <returns>True if successful, false if insufficient funds.</returns>
        public bool SpendInfluence(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Cannot spend negative influence.");
            if (_influence >= amount)
            {
                _influence -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the current Victory Points accumulated by the player.
        /// </summary>
        public int VictoryPoints { get; internal set; }

        // --- Military ---

        /// <summary>
        /// Troops available in the barracks (reserve) ready for deployment.
        /// </summary>
        public int TroopsInBarracks { get; internal set; } = GameConstants.StartingTroops;

        /// <summary>
        /// Spies available in the barracks (reserve) ready for placement.
        /// </summary>
        public int SpiesInBarracks { get; internal set; } = GameConstants.StartingSpies;

        /// <summary>
        /// Troops granted by card effects this turn that can be deployed for free.
        /// Resets at end of turn if not used.
        /// </summary>
        public int PendingFreeTroops { get; internal set; }

        /// <summary>
        /// Count of trophies collected (e.g. from assassinations).
        /// </summary>
        public int TrophyHall { get; internal set; }

        // --- Card Piles ---

        // Encapsulated Deck Manager
        private readonly Deck _deckManager = new();

        // Standard Collections
        private readonly List<Card> _hand = new();
        private readonly List<Card> _playedCards = new();
        private readonly List<Card> _innerCircle = new();

        public IReadOnlyList<Card> Hand => _hand;
        public IReadOnlyList<Card> PlayedCards => _playedCards;
        public IReadOnlyList<Card> InnerCircle => _innerCircle;

        // Expose via read-only lists
        /// <summary>
        /// Read-only view of the cards currently in the draw pile.
        /// </summary>
        public IReadOnlyList<Card> Deck => _deckManager.DrawPile;

        /// <summary>
        /// Read-only view of the cards currently in the discard pile.
        /// </summary>
        public IReadOnlyList<Card> DiscardPile => _deckManager.DiscardPile;

        internal Deck DeckManager => _deckManager; // Internal access for Factory/Tests only

        // --- Internal State Management (Exposed to PlayerStateManager) ---
        internal void AddToHand(Card card) => _hand.Add(card);
        internal bool RemoveFromHand(Card card) => _hand.Remove(card);
        internal void AddToPlayed(Card card) => _playedCards.Add(card);
        internal bool RemoveFromPlayed(Card card) => _playedCards.Remove(card);
        internal void AddToInnerCircle(Card card) => _innerCircle.Add(card);
        internal bool RemoveFromInnerCircle(Card card) => _innerCircle.Remove(card);

        internal void ClearHand() => _hand.Clear();
        internal void ClearPlayed() => _playedCards.Clear();
        internal void ClearInnerCircle() => _innerCircle.Clear();

        /// <summary>
        /// Creates a new player with the specified color and optional identity.
        /// </summary>
        /// <param name="color">The faction color for this player.</param>
        /// <param name="playerId">Optional unique identifier. If null, a new GUID will be generated.</param>
        /// <param name="displayName">Optional display name. If empty, defaults to "Player {color}".</param>
        public Player(PlayerColor color, Guid? playerId = null, string displayName = "")
        {
            PlayerId = playerId ?? Guid.NewGuid();
            Color = color;
            DisplayName = string.IsNullOrEmpty(displayName) ? $"Player {color}" : displayName;
        }



        // --- Deck Management ---

        /// <summary>
        /// Draws the specified number of cards from the deck.
        /// </summary>
        /// <param name="count">Number of cards to draw.</param>
        /// <param name="random">Random number generator for shuffling if needed.</param>
        internal void DrawCards(int count, IGameRandom random)
        {
            var drawn = _deckManager.Draw(count, random);
            foreach (var card in drawn)
            {
                card.Location = CardLocation.Hand;
                _hand.Add(card);
            }
        }

        /// <summary>
        /// Attempts to promote a card from Hand or PlayedCards to the Inner Circle.
        /// </summary>
        /// <param name="card">The card to promote.</param>
        /// <param name="errorMessage">Error message if promotion fails.</param>
        /// <returns>True if promotion succeeded, false otherwise.</returns>
        internal bool TryPromoteCard(Card card, out string errorMessage)
        {
            // Guard clause: null check
            if (card is null)
            {
                errorMessage = "Card cannot be null";
                return false;
            }

            // Try to remove from Hand first, then PlayedCards
            // Key Fix: Use RuntimeId to ensure we remove the EXACT instance requested
            // Use RemoveAll with count check or Find + Remove

            // Check Hand
            var handMatch = Hand.FirstOrDefault(c => c.RuntimeId == card.RuntimeId);
            bool removed = false;
            if (handMatch != null)
            {
                removed = _hand.Remove(handMatch);
            }

            // Check PlayedCards if not found in Hand
            if (!removed)
            {
                var playedMatch = PlayedCards.FirstOrDefault(c => c.RuntimeId == card.RuntimeId);
                if (playedMatch != null)
                {
                    removed = _playedCards.Remove(playedMatch);
                }
            }

            // Guard clause: card not found
            if (!removed)
            {
                // Card not found in Hand or PlayedCards
                // Note: Promotion from Discard pile is not currently supported
                errorMessage = $"Card '{card.Name}' (ID: {card.RuntimeId}) not found in Hand or Played area";
                return false;
            }

            // Success path
            if (card.RedirectsToSupplyOnDevourOrPromote)
            {
                // e.g. Insane Outcast: "If [this] would be devoured or promoted, return it to
                // the supply instead." Not actually promoted.
                card.Location = CardLocation.Supply;
            }
            else
            {
                card.Location = CardLocation.InnerCircle;
                _innerCircle.Add(card);
            }
            errorMessage = string.Empty;
            return true;
        }

        internal void CleanUpTurn()
        {
            // Move Played Cards to Discard
            _deckManager.AddToDiscard(_playedCards);
            _playedCards.Clear();

            // Move Hand to Discard
            _deckManager.AddToDiscard(_hand);
            _hand.Clear();

            _power = 0;
            _influence = 0;
        }
    }
}

