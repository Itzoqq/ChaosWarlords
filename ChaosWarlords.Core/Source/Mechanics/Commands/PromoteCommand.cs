using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

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
                CardRuntimeId = CardRuntimeId,
                IsChainedEffect = IsChainedEffect
            };
        }

        public string? CardId { get; }

        /// <summary>
        /// Identifies the specific physical copy to promote, disambiguating duplicate copies of
        /// the same card definition (Card.Id alone can't) - the same problem PlayCardCommand.
        /// CardRuntimeId already solves for playing a card. Null for the legacy 1-/2-arg string
        /// constructor (matches every pre-existing call site) and for hydrating replay data
        /// where it wasn't recorded - ResolveCard falls back to a plain CardId lookup in both
        /// cases. That fallback isn't just a one-time legacy-data affordance: Card.RuntimeId is
        /// a fresh, non-deterministic Guid.NewGuid() on every card instantiation (unlike Card.
        /// Id's seeded-RNG-derived suffix), so a from-scratch replay run will always generate
        /// different RuntimeIds than the original recording - the CardId fallback is what makes
        /// EVERY replay resolve correctly, not a one-off compatibility shim for old data.
        /// </summary>
        public System.Guid? CardRuntimeId { get; }

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

        public PromoteCommand(string? cardId, bool isChainedEffect = false, System.Guid? cardRuntimeId = null)
        {
            CardId = cardId;
            IsChainedEffect = isChainedEffect;
            CardRuntimeId = cardRuntimeId;
        }

        /// <summary>
        /// Preferred construction path whenever an actual Card object is already in hand at the
        /// call site (e.g. ActionSystem.HandlePromoteFromPileSelection, resolving a real click) -
        /// captures CardRuntimeId alongside CardId so ResolveCard can disambiguate 2 copies of
        /// the same card definition, unlike the string-only constructor above.
        /// </summary>
        public PromoteCommand(Card card, bool isChainedEffect = false)
            : this(card.Id, isChainedEffect, card.RuntimeId)
        {
        }

        /// <summary>
        /// Shared lookup for Validate()/Execute(). When CardRuntimeId is set, resolution is
        /// BY RUNTIMEID ONLY - unambiguous even when the player holds 2 copies of the same card
        /// definition, and deliberately NOT allowed to fall back to a CardId match if that exact
        /// physical copy can't be found (e.g. it was already promoted by an earlier dispatch of
        /// this same command) - falling back there would silently re-target a different,
        /// same-Id sibling copy instead of correctly resolving to "nothing left to do," exactly
        /// the ambiguity CardRuntimeId exists to prevent. The CardId-only fallback path is used
        /// SOLELY when CardRuntimeId is null: the legacy string-only construction path, and every
        /// hydrated replay (see CardRuntimeId's own doc comment for why that's not just an old-
        /// data affordance). Searches Hand/PlayedCards, plus Discard only for the immediate
        /// PromoteFromPile flow (IsChainedEffect == true) - the legacy deferred end-of-turn
        /// promotion-credit flow (IsChainedEffect == false) must never be able to promote from
        /// Discard, enforced here rather than merely by the UI never offering a discard card as
        /// a click target. Must stay a pure read - CommandDispatcher calls Validate() then
        /// Execute() on the same instance, so mutating here would leave Execute()'s own
        /// promotion call with nothing left to find.
        /// </summary>
        private Card? ResolveCard(MatchContext context)
        {
            var player = context.TurnManager.ActivePlayer;

            if (CardRuntimeId is System.Guid runtimeId)
            {
                return FindInEligiblePiles(player, c => c.RuntimeId == runtimeId);
            }

            return FindInEligiblePiles(player, c => c.Id == CardId);
        }

        /// <summary>
        /// Searches Hand/PlayedCards, plus Discard only for the immediate PromoteFromPile flow
        /// (IsChainedEffect == true) - shared by both ResolveCard lookup passes (RuntimeId-first,
        /// then CardId-fallback) so the "which piles are eligible" rule lives in exactly one
        /// place.
        /// </summary>
        private Card? FindInEligiblePiles(Player player, System.Func<Card, bool> predicate)
        {
            return player.Hand.FirstOrDefault(predicate) ??
                   player.PlayedCards.FirstOrDefault(predicate) ??
                   (IsChainedEffect ? player.DiscardPile.FirstOrDefault(predicate) : null);
        }

        public bool Validate(MatchContext context)
        {
            var card = ResolveCard(context);

            if (card == null)
            {
                return context.RejectValidation(nameof(PromoteCommand), $"card '{CardId}' (RuntimeId {CardRuntimeId}) not found in Hand/PlayedCards{(IsChainedEffect ? "/Discard" : "")}.");
            }
            return true;
        }

        public void Execute(MatchContext context)
        {
            var player = context.TurnManager.ActivePlayer;
            var card = ResolveCard(context);

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
