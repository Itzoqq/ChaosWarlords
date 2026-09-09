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

        // Composition of the trophy hall (which PlayerColor - Neutral/"white" included - each
        // captured troop was) as well as the total count - e.g. Mummy Lord's "take a white
        // troop from any trophy hall," Black Dragon's "1 VP per 3 white troops in trophy hall"
        // (planning.txt's TROPHY-HALL-AS-TROOP-RESERVOIR). Never touched directly - always via
        // AddTrophy/RemoveTrophy/SetTrophyHall below (same internal-mutation-method convention
        // as Hand/Deck/InnerCircle: AddToHand/RemoveFromHand/etc.).
        private readonly Dictionary<PlayerColor, int> _trophyHallByColor = new();

        /// <summary>
        /// Read-only view of the trophy hall's composition by captured troop color.
        /// </summary>
        public IReadOnlyDictionary<PlayerColor, int> TrophyHallByColor => _trophyHallByColor;

        /// <summary>
        /// Total trophy count across all colors - every existing reader (VictoryManager's VP
        /// scoring, EffectCondition.TrophyHallCount, UI display) keeps working against this
        /// exact same int contract, computed fresh from TrophyHallByColor rather than tracked
        /// as a separately-settable field.
        /// </summary>
        public int TrophyHall => _trophyHallByColor.Values.Sum();

        /// <summary>
        /// Records one captured troop of troopColor into the trophy hall. Called by
        /// IPlayerStateManager.AddTrophy for every Assassinate/Supplant (see CombatResolver) -
        /// the color must be captured by the caller BEFORE the map node's Occupant is cleared.
        /// </summary>
        internal void AddTrophy(PlayerColor troopColor)
        {
            _trophyHallByColor[troopColor] = _trophyHallByColor.GetValueOrDefault(troopColor) + 1;
        }

        /// <summary>
        /// Removes one captured troop of troopColor from the trophy hall, if present - e.g.
        /// Mummy Lord's "take a white troop from any trophy hall." Returns false (no-op) if none
        /// of that color remain; the caller (TrophyHallRuleEngine) is expected to have already
        /// confirmed eligibility before calling this, so a false here indicates a stale/forged
        /// command rather than an expected outcome.
        /// </summary>
        internal bool RemoveTrophy(PlayerColor troopColor)
        {
            if (!_trophyHallByColor.TryGetValue(troopColor, out var count) || count <= 0)
            {
                return false;
            }

            if (count == 1)
            {
                _trophyHallByColor.Remove(troopColor);
            }
            else
            {
                _trophyHallByColor[troopColor] = count - 1;
            }

            return true;
        }

        /// <summary>
        /// Replaces the ENTIRE trophy hall composition with snapshot (clear then repopulate,
        /// same "clear then repopulate, tolerate whatever's given" shape as ClearHand()+
        /// AddToHand()) - used by StateRestorer (rollback/replay) and directly by tests that
        /// need a specific composition set up without dispatching real commands. Passing an
        /// empty/null snapshot just clears.
        /// </summary>
        internal void SetTrophyHall(IReadOnlyDictionary<PlayerColor, int>? snapshot)
        {
            _trophyHallByColor.Clear();
            if (snapshot == null)
            {
                return;
            }

            foreach (var (color, count) in snapshot)
            {
                if (count > 0)
                {
                    _trophyHallByColor[color] = count;
                }
            }
        }

        /// <summary>
        /// Convenience single-color overload of SetTrophyHall, for tests that only care about
        /// the TOTAL count (e.g. VictoryManager VP-scoring/EffectCondition.TrophyHallCount
        /// threshold tests) and don't need a specific composition - defaults to Neutral since
        /// that's this project's most common trophy-hall-composition test scenario ("white"
        /// troops).
        /// </summary>
        internal void SetTrophyHall(int count, PlayerColor color = PlayerColor.Neutral)
        {
            SetTrophyHall(count > 0 ? new Dictionary<PlayerColor, int> { [color] = count } : null);
        }

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
        /// Moves the entire draw pile into the discard pile (e.g. Matron Mother's "Put your
        /// deck into your discard pile").
        /// </summary>
        internal void MoveDeckToDiscard() => _deckManager.MoveAllToDiscard();

        /// <summary>
        /// Promotes the top card of THIS player's own deck straight to the Inner Circle (e.g.
        /// Hezrou/Nalfeshnee/Elder Brain's "Promote the top card of your deck") - there's no
        /// player choice at all, unlike TryPromoteCard's Hand/Played/Discard search. Reuses
        /// _deckManager.Draw(1, ...) rather than a dedicated "peek" method - it already handles
        /// the empty-deck-reshuffles-discard case identically to a normal draw; the drawn card
        /// is redirected straight to the Inner Circle below instead of ever touching Hand.
        /// </summary>
        internal bool TryPromoteTopOfDeck(IGameRandom random, out string errorMessage)
        {
            var drawn = _deckManager.Draw(1, random);
            if (drawn.Count == 0)
            {
                errorMessage = "No cards left in deck or discard pile to promote.";
                return false;
            }

            var card = drawn[0];
            if (card.RedirectsToSupplyOnDevourOrPromote)
            {
                // e.g. Insane Outcast: "If [this] would be devoured or promoted, return it to
                // the supply instead." Not actually promoted - same rule TryPromoteCard applies.
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

        /// <summary>
        /// Attempts to promote a card from Hand, PlayedCards, or the discard pile to the Inner
        /// Circle. The discard-pile search exists for EffectType.PromoteFromPile (e.g. Matron
        /// Mother, Necromancer) - unconditional/safe to leave widened for every caller since the
        /// legacy deferred-credit UI (PromoteInputMode) only ever offers Hand/Played cards as
        /// click targets, so it can never actually pass a discard-pile card's id through here.
        /// </summary>
        /// <param name="card">The card to promote.</param>
        /// <param name="errorMessage">Error message if promotion fails.</param>
        /// <returns>True if promotion succeeded, false otherwise.</returns>
        internal bool TryPromoteCard(Card card, out string errorMessage)
        {
            if (card is null)
            {
                errorMessage = "Card cannot be null";
                return false;
            }

            if (!TryRemoveFromHandPlayedOrDiscard(card.RuntimeId))
            {
                errorMessage = $"Card '{card.Name}' (ID: {card.RuntimeId}) not found in Hand, Played, or Discard area";
                return false;
            }

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

        /// <summary>
        /// Removes the card matching <paramref name="runtimeId"/> from whichever of Hand,
        /// PlayedCards, or the discard pile currently holds it (checked in that order - the
        /// discard-pile search exists for EffectType.PromoteFromPile, see TryPromoteCard's own
        /// doc comment). Returns false if none of the three piles holds a match.
        /// </summary>
        private bool TryRemoveFromHandPlayedOrDiscard(Guid runtimeId)
        {
            return TryRemoveMatch(Hand, runtimeId, _hand.Remove)
                || TryRemoveMatch(PlayedCards, runtimeId, _playedCards.Remove)
                || TryRemoveMatch(DiscardPile, runtimeId, _deckManager.RemoveFromDiscard);
        }

        private static bool TryRemoveMatch(IEnumerable<Card> pile, Guid runtimeId, Func<Card, bool> remove)
        {
            var match = pile.FirstOrDefault(c => c.RuntimeId == runtimeId);
            return match != null && remove(match);
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

