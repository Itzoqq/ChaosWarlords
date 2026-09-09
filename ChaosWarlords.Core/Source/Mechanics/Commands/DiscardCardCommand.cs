using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Discards a named card from a SPECIFIC player's hand - not implicitly
    /// context.TurnManager.ActivePlayer, unlike every other command in this codebase. This is
    /// deliberate: Insane Outcast discards from its own owner's hand (who IS the active
    /// player when it resolves), but Neogi's "each opponent must discard a card" forces
    /// OTHER players to discard during the active player's End Turn - the target player must
    /// stay explicit and independent of whoever ActivePlayer resolves to at Execute() time.
    /// </summary>
    public class DiscardCardCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.DiscardCard;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.DiscardCardCommandDto
            {
                PlayerColor = TargetPlayerColor.ToString(),
                CardId = CardId
            };
        }

        public PlayerColor TargetPlayerColor { get; }
        public string? CardId { get; }

        public DiscardCardCommand(PlayerColor targetPlayerColor, string? cardId)
        {
            TargetPlayerColor = targetPlayerColor;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var player = context.TurnManager.GetPlayerByColor(TargetPlayerColor);
            if (player == null) return context.RejectValidation(nameof(DiscardCardCommand), $"no player with color {TargetPlayerColor}.");

            // Must be the player currently expected to discard - context.TurnManager.ActivePlayer
            // correctly resolves to ForcedActingPlayer during Cranium Rats'/Neogi's forced
            // sequences, or the real active player for Insane Outcast's own-hand discard.
            // Without this, an unrelated player's legitimately-owned card would validate fine
            // and get consumed to satisfy someone else's pending forced discard. See
            // planning.txt/RESOLVED.txt (council-review 2026-09-01).
            if (player != context.TurnManager.ActivePlayer)
            {
                return context.RejectValidation(nameof(DiscardCardCommand), $"{TargetPlayerColor} is not the player currently expected to discard (ActivePlayer is {context.TurnManager.ActivePlayer.Color}).");
            }

            var card = player.Hand.FirstOrDefault(c => c.Id == CardId);
            if (card == null)
            {
                return context.RejectValidation(nameof(DiscardCardCommand), $"card '{CardId}' not found in {TargetPlayerColor}'s hand.");
            }
            return true;
        }

        public void Execute(MatchContext context)
        {
            var player = context.TurnManager.GetPlayerByColor(TargetPlayerColor);
            var card = player?.Hand.FirstOrDefault(c => c.Id == CardId);

            if (player == null || card == null)
            {
                return;
            }

            // "An opponent's effect caused this discard" (as opposed to the card's own owner
            // choosing to discard it, e.g. Insane Outcast's own-hand cost) - the same
            // distinction ReactiveDiscardEffect's card text keys off (e.g. Grimlock: "If an
            // opponent causes you to discard this, draw 2 cards"). ForcedActingPlayer being
            // set to THIS discarding player is the correct, general signal for that - true for
            // BOTH of the two independent ways a shipped card can force someone else to
            // discard: Neogi's cross-player queue (MatchManager.AdvanceOpponentDiscard calls
            // BeginForcedActingPlayer directly, no ExecutionStack involved) and Cranium Rats'
            // SelectOpponent -> OnSuccess: DiscardCard chain (BeginForcedActingPlayer via
            // SelectOpponentCommand, released later by ActionSystem's own ClearState() once the
            // chain resolves - see ReleaseForcedActingPlayerIfOwnedByExecutionStack). Checking
            // MatchManager.IsResolvingOpponentDiscard alone only covers the Neogi case and
            // silently misses Cranium Rats forcing a discard of a ReactiveDiscardEffect card -
            // read before either branch below can release it.
            bool forcedByOpponent = context.TurnManager.ForcedActingPlayer == player;

            // Captured BEFORE applying card.ReactiveDiscardEffect below, deliberately: Umber
            // Hulk's ReactiveDiscardEffect (EffectType.ForceCausingOpponentDiscard) can itself
            // enqueue a NEW entry into this same MatchManager queue via EnqueueReactiveDiscard.
            // Deciding the branch below off a POST-enqueue read would misattribute that queue
            // becoming non-empty to THIS discard being part of an opponent-discard sequence it
            // was never actually part of, wrongly dequeuing the just-added entry instead of
            // completing this discard's own chain.
            bool wasResolvingOpponentDiscard = context.MatchManager.IsResolvingOpponentDiscard;

            context.PlayerStateManager.DiscardCard(player, card);
            context.RecordAction("DiscardCard", $"{player.DisplayName} discarded {card.Name}.");

            if (forcedByOpponent && card.ReactiveDiscardEffect != null)
            {
                Mechanics.Rules.CardEffectProcessor.ApplyEffect(card.ReactiveDiscardEffect, card, context, context.Logger);
            }

            if (wasResolvingOpponentDiscard)
            {
                // A cross-player forced-discard sequence is in progress - Neogi's end-of-turn
                // one, a mid-turn reactive one (Umber Hulk), or both merged into the same queue -
                // this discard has NOTHING on ActionSystem's ExecutionStack (whatever queued it -
                // MarkOpponentDiscardAtEndOfTurn or EnqueueReactiveDiscard - already resolved
                // earlier), so ActionSystem.CompleteAction() would hit its no-stack-context
                // fallback and incorrectly reset CurrentState to Normal after just one player.
                // Advance the sequence instead - MatchManager.ResolveOpponentDiscard moves to
                // the next player or completes/stops per _discardPhaseEndsTurn.
                context.MatchManager.ResolveOpponentDiscard(card);
            }
            else
            {
                // Normal chain-continuation path (e.g. Insane Outcast's own "discard -> devour
                // self" chain, or Cranium Rats' SelectOpponent -> DiscardCard chain) - the
                // DiscardCard EffectContext is genuinely sitting on ExecutionStack, so
                // CompleteAction() resolves it and pushes its OnSuccess. If this discard was
                // part of a forced-actor mid-turn chain (e.g. Cranium Rats' chosen opponent)
                // and the whole chain has now fully resolved back to Normal, ActionSystem's own
                // ClearState()-driven release (see ReleaseForcedActingPlayerIfOwnedByExecutionStack)
                // reverts ActivePlayer to the real active player - generically, for any
                // OnSuccess shape a future SelectOpponent-based card might chain into, not just
                // this one.
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
