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
                CardId = CardId,
                PromoteInsteadOfDiscard = PromoteInsteadOfDiscard
            };
        }

        public PlayerColor TargetPlayerColor { get; }
        public string? CardId { get; }

        /// <summary>
        /// The player's choice on an EffectType.PromoteInsteadOfDiscard card (Ambassador) - "you
        /// may promote it instead" of the discard this command would otherwise perform. Always
        /// false for every other card (including a plain declined choice), and rejected by
        /// Validate() unless the card actually carries that reactive effect AND this discard is
        /// genuinely opponent-caused - see PromoteInsteadOfDiscard's own doc comment.
        /// </summary>
        public bool PromoteInsteadOfDiscard { get; }

        public DiscardCardCommand(PlayerColor targetPlayerColor, string? cardId, bool promoteInsteadOfDiscard = false)
        {
            TargetPlayerColor = targetPlayerColor;
            CardId = cardId;
            PromoteInsteadOfDiscard = promoteInsteadOfDiscard;
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

            if (PromoteInsteadOfDiscard)
            {
                if (card.ReactiveDiscardEffect?.Type != EffectType.PromoteInsteadOfDiscard)
                {
                    return context.RejectValidation(nameof(DiscardCardCommand), $"'{CardId}' has no PromoteInsteadOfDiscard reactive effect - cannot promote it instead of discarding.");
                }

                if (context.TurnManager.ForcedActingPlayer != player)
                {
                    return context.RejectValidation(nameof(DiscardCardCommand), $"'{CardId}' can only be promoted instead of discarded when an opponent caused this discard.");
                }
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
            // discard: Neogi's cross-player queue (TurnLifecycleSubsystem.AdvanceOpponentDiscard calls
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

            ApplyDiscardOrPromoteInstead(context, player, card, forcedByOpponent);
            AdvanceSequence(context, card, wasResolvingOpponentDiscard);
        }

        /// <summary>
        /// Either promotes <paramref name="card"/> (Ambassador's "you may promote it instead")
        /// or discards it normally, then - only in the normal-discard branch - dispatches any
        /// ReactiveDiscardEffect the card carries. Validate() already confirmed
        /// PromoteInsteadOfDiscard is only ever true here when the card actually carries
        /// EffectType.PromoteInsteadOfDiscard AND this discard is genuinely opponent-caused, so
        /// that branch REPLACES the discard entirely rather than adding to it - there's nothing
        /// left to apply afterward; the reactive effect WAS the promote-instead choice itself.
        /// </summary>
        private void ApplyDiscardOrPromoteInstead(MatchContext context, Entities.Actors.Player player, Entities.Cards.Card card, bool forcedByOpponent)
        {
            if (PromoteInsteadOfDiscard)
            {
                // Mirrors PromoteCommand.Execute's own success check - only log the action if
                // the card was actually still there to promote (it's re-fetched from Hand
                // immediately before this call with nothing intervening that could move it
                // today, so failure isn't currently reachable, but a silently-false log entry
                // claiming a promotion that didn't happen would be worse than a quiet no-op).
                if (context.PlayerStateManager.TryPromoteCard(player, card, out _))
                {
                    context.RecordAction("PromoteInsteadOfDiscard", $"{player.DisplayName} promoted {card.Name} instead of discarding it.");
                }
                return;
            }

            context.PlayerStateManager.DiscardCard(player, card);
            context.RecordAction("DiscardCard", $"{player.DisplayName} discarded {card.Name}.");

            if (forcedByOpponent && card.ReactiveDiscardEffect != null && card.ReactiveDiscardEffect.Type != EffectType.PromoteInsteadOfDiscard)
            {
                Mechanics.Rules.CardEffectApplier.ApplyEffect(card.ReactiveDiscardEffect, card, context, context.Logger);
            }
        }

        /// <summary>
        /// Resumes whichever flow this discard belongs to - a cross-player forced-discard queue
        /// (Neogi/Umber Hulk) or a normal ExecutionStack chain (Insane Outcast/Cranium Rats) -
        /// see each branch's own doc comment below for why they can't be handled the same way.
        /// </summary>
        private static void AdvanceSequence(MatchContext context, Entities.Cards.Card card, bool wasResolvingOpponentDiscard)
        {
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
                return;
            }

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
