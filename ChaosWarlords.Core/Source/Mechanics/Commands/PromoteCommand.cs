using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class PromoteCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.Promote;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.PromoteCommandDto
            {
                CardId = CardId,
                IsChainedEffect = IsChainedEffect
            };
        }

        public string? CardId { get; }

        /// <summary>
        /// True when this command resolves an active blocking effect on ActionSystem's
        /// ExecutionStack (EffectType.PromoteFromPile's immediate flow - e.g. Matron Mother,
        /// Necromancer), as opposed to the legacy deferred end-of-turn promotion-credit flow
        /// (EffectType.Promote, redeemed via PromoteInputMode), where by the time this command
        /// runs the ExecutionStack is already empty (see PromoteInputMode's manual
        /// CancelTargeting()/EndTurnCommand handling). Defaults to false so every existing call
        /// site and recorded replay is completely unaffected - calling CompleteAction() when
        /// there's no blocking effect to resolve would incorrectly pop/complete an unrelated
        /// stack entry (or hit the "no stack context" fallback and fire OnActionCompleted
        /// prematurely). Only the new PromoteFromPile flow (ActionSystem.
        /// HandlePromoteFromPileSelection) ever passes true.
        /// </summary>
        public bool IsChainedEffect { get; }

        public PromoteCommand(string? cardId, bool isChainedEffect = false)
        {
            CardId = cardId;
            IsChainedEffect = isChainedEffect;
        }

        public bool Validate(MatchContext context)
        {
            var player = context.TurnManager.ActivePlayer;
            // Check if card exists in Hand/Played, plus Discard only for the immediate
            // PromoteFromPile flow (IsChainedEffect == true). The legacy deferred
            // end-of-turn promotion-credit flow (IsChainedEffect == false) must never be able
            // to promote from Discard - that must be enforced here, not merely by the UI
            // never offering a discard card as a click target. This must stay a pure read -
            // CommandDispatcher calls Validate() then Execute() on the same instance, so
            // actually promoting here (as this used to do via TryPromoteCard) removes the card
            // from Hand/Played/Discard as a side effect of "checking", leaving Execute()'s own
            // TryPromoteCard call to find nothing.
            var card = player.Hand.FirstOrDefault(c => c.Id == CardId) ??
                       player.PlayedCards.FirstOrDefault(c => c.Id == CardId) ??
                       (IsChainedEffect ? player.DiscardPile.FirstOrDefault(c => c.Id == CardId) : null);

            return card != null;
        }

        public void Execute(MatchContext context)
        {
            var player = context.TurnManager.ActivePlayer;
            // Check if card exists in Hand/Played, plus Discard only for the immediate
            // PromoteFromPile flow (IsChainedEffect == true). See Validate() above.
            var card = player.Hand.FirstOrDefault(c => c.Id == CardId) ??
                       player.PlayedCards.FirstOrDefault(c => c.Id == CardId) ??
                       (IsChainedEffect ? player.DiscardPile.FirstOrDefault(c => c.Id == CardId) : null);

            if (card != null)
            {
                if (context.PlayerStateManager.TryPromoteCard(player, card, out var error))
                {
                    context.RecordAction("Promote", $"Promoted {card.Name} to Inner Circle.");
                }
                // else: card vanished from Hand/Played/Discard between Validate() and Execute()
                // (e.g. a chained effect moved it) - nothing to promote, so this is a silent
                // no-op.
            }

            if (IsChainedEffect)
            {
                context.ActionSystem.CompleteAction();
            }
        }
    }
}
