using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Core.Data.Dtos
{
    /// <summary>
    /// Represents a complete snapshot of the game state at a specific point in time.
    /// Root object for Serialization (Save Game / Checkpoints).
    /// </summary>
    public class GameStateDto
    {
        // Meta
        public int Seed { get; set; }
        public int TurnNumber { get; set; }
        public MatchPhase Phase { get; set; }

        // Entities
        public List<PlayerDto> Players { get; set; } = [];
        public MapDto Map { get; set; } = new MapDto();

        // Market (Row of cards)
        public List<CardDto> Market { get; set; } = [];

        // Market's face-down draw pile behind the visible row (IMarketManager.MarketDeck).
        // Without this, a rollback after a command that bought/removed a market card (which
        // RefillMarket immediately backfills from this deck) restores MarketRow to its
        // pre-command contents but leaves the deck already short the card it drew - that card
        // ends up in neither the row, the deck, any player's hand/discard/void, permanently
        // deleted from the game. Not currently part of MatchContext.GetStateHash (see
        // planning.txt's existing hash-granularity-vs-cost bucket) - this field only closes the
        // DTO/restore gap, not the hash one.
        public List<CardDto> MarketDeck { get; set; } = [];

        // Void (Removed cards)
        public List<CardDto> VoidPile { get; set; } = [];

        // Transient State (Cards pending destruction at end of turn)
        public List<string> MarkedForTurnEndDevourCardIds { get; set; } = [];

        // Transient State (Cards played this turn that force each opponent to discard at end
        // of turn, e.g. Neogi - one entry per source card, stacks)
        public List<string> PendingOpponentDiscardTriggerCardIds { get; set; } = [];

        // Stack State (For mid-action recovery)
        public List<EffectContextDto> EffectStack { get; set; } = [];

        /// <summary>
        /// ActionSystem's targeting state machine at snapshot time - CurrentState plus its
        /// Pending* fields (PendingCard/PendingSite/PendingMoveSource/PendingDevourCard).
        /// Added alongside EffectStack so a rollback (StateRestorer, on a command that throws
        /// mid-execution) restores the FULL mid-action picture, not just the execution stack -
        /// before this, a failed command could roll the map/players/market back to before it
        /// ran while leaving ActionSystem still pointing at Pending* state from the failed
        /// attempt (or from whatever ran just before it). Card/Site/MapNode are stored as IDs,
        /// not embedded objects, and re-resolved on restore - cards via CardDatabase.GetCardById
        /// (matching RestoreEffect's existing SourceCard resolution, so a restored PendingCard
        /// is a fresh instance rather than the original reference, same known limitation as the
        /// rest of rollback's Card handling), sites/nodes via MapManager.Sites/Nodes lookup
        /// (matching RestoreMap, which mutates the existing Site/MapNode instances in place
        /// rather than recreating them, so these DO resolve to the same reference other restored
        /// state already points at). See planning.txt.
        /// </summary>
        public ActionState ActionSystemState { get; set; } = ActionState.Normal;
        public string? PendingCardId { get; set; }
        public int? PendingSiteId { get; set; }
        public int? PendingMoveSourceNodeId { get; set; }
        public string? PendingDevourCardId { get; set; }

        /// <summary>
        /// See IActionSystem.PendingAffectedPlayerColor's doc comment - the outcome-dependent
        /// targeting primitive's read side (e.g. Mindwitness). Unlike PendingSite/
        /// PendingMoveSource, this needs no id-lookup re-resolution on restore - PlayerColor is
        /// a plain value, not an entity reference.
        /// </summary>
        public PlayerColor? PendingAffectedPlayerColor { get; set; }

        public long SequenceNumber { get; set; }

        /// <summary>
        /// Deterministic hash of the full game state at snapshot time, for desync detection
        /// (e.g. comparing a server's and a client's state after the same sequence of
        /// commands). Populated by DtoMapper.ToGameStateDto from MatchContext.GetStateHash() -
        /// this DTO does NOT compute its own hash. It used to (a `CalculateChecksum()` method
        /// using StateHasher.ComputeHash directly), but that mixed only Seed/TurnNumber/
        /// Phase/SequenceNumber - no map, player, or market state - so it could not actually
        /// have caught a desync outside of those four fields, despite living on exactly the
        /// object that would travel over the wire for that purpose. See planning.txt.
        /// </summary>
        public string StateHash { get; set; } = string.Empty;

        public GameStateDto() { }
    }
}
