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
            if (player == null) return false;

            // Must be the player currently expected to discard - context.TurnManager.ActivePlayer
            // correctly resolves to ForcedActingPlayer during Cranium Rats'/Neogi's forced
            // sequences, or the real active player for Insane Outcast's own-hand discard.
            // Without this, an unrelated player's legitimately-owned card would validate fine
            // and get consumed to satisfy someone else's pending forced discard. See
            // planning.txt/RESOLVED.txt (council-review 2026-09-01).
            if (player != context.TurnManager.ActivePlayer) return false;

            var card = player.Hand.FirstOrDefault(c => c.Id == CardId);
            return card != null;
        }

        public void Execute(MatchContext context)
        {
            var player = context.TurnManager.GetPlayerByColor(TargetPlayerColor);
            var card = player?.Hand.FirstOrDefault(c => c.Id == CardId);

            if (player == null || card == null)
            {
                return;
            }

            context.PlayerStateManager.DiscardCard(player, card);
            context.RecordAction("DiscardCard", $"{player.DisplayName} discarded {card.Name}.");

            if (context.MatchManager.IsResolvingOpponentDiscard)
            {
                // Neogi's cross-player forced-discard sequence is in progress - this discard
                // has NOTHING on ActionSystem's ExecutionStack (MarkOpponentDiscardAtEndOfTurn
                // already resolved, long ago, during Neogi's own play), so
                // ActionSystem.CompleteAction() would hit its no-stack-context fallback and
                // incorrectly reset CurrentState to Normal after just one opponent. Advance
                // the sequence instead - MatchManager.ResolveOpponentDiscard moves to the next
                // opponent or completes the deferred end-of-turn player-switch.
                context.MatchManager.ResolveOpponentDiscard(card);
            }
            else
            {
                // Normal chain-continuation path (e.g. Insane Outcast's own "discard -> devour
                // self" chain) - the DiscardCard EffectContext is genuinely sitting on
                // ExecutionStack, so CompleteAction() resolves it and pushes its OnSuccess.
                // If this discard was part of a forced-actor mid-turn chain (e.g. Cranium Rats'
                // chosen opponent) and the whole chain has now fully resolved back to Normal,
                // ActionSystem's own ClearState()-driven release (see
                // ReleaseForcedActingPlayerIfOwnedByExecutionStack) reverts ActivePlayer to the
                // real active player - generically, for any OnSuccess shape a future
                // SelectOpponent-based card might chain into, not just this one.
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
