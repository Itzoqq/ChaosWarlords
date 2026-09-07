using System.Linq;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Voluntarily stops a "for up to N" repeat targeting effect (CardEffect.
    /// AllowPartialRepeat - e.g. Council Member: "Move up to 2 enemy troops") before all N
    /// repeats are used, keeping whatever repeats already resolved instead of undoing them.
    /// Distinct from ActionSystem.CancelTargeting(), which reverts the ENTIRE card play
    /// (including any already-resolved repeats) via a full state snapshot restore and is
    /// never dispatched through CommandDispatcher - this command IS, since declining early is
    /// a genuine, replay-significant player choice, not a pure client-side UI revert.
    ///
    /// Dispatched from 2 different UI gestures with 2 different narrower gates: right-click AT
    /// a genuine repeat boundary (TargetingInputMode.IsAtADeclinableRepeatBoundary - narrower,
    /// only the effect's own entry state) and the explicit "I'm done" button (visible/clickable
    /// from ANY of the effect's owned states, not just the entry one). This command's own
    /// Validate() below is intentionally as permissive as it's safe to be (any owned state, not
    /// just the entry one) - it's the single authoritative gate both call sites share, not
    /// the place either UI gesture's own narrower policy lives.
    /// </summary>
    public class DeclineRepeatCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.DeclineRepeat;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.DeclineRepeatCommandDto
            {
                CardId = CardId
            };
        }

        public string? CardId { get; }

        public DeclineRepeatCommand(string? cardId)
        {
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var effect = context.ActionSystem.CurrentEffect;

            // Must actually be a repeat-optional effect (Council Member's "up to 2", not
            // Deathblade's mandatory "exactly 2") ...
            if (effect?.SourceEffect == null || !effect.SourceEffect.AllowPartialRepeat)
            {
                return context.RejectValidation(nameof(DeclineRepeatCommand), "no repeat-optional effect (AllowPartialRepeat) is currently active.");
            }

            // ... on a strategy that actually opted into repeats in the first place. Without
            // this, a future card that mistakenly (or maliciously) sets AllowPartialRepeat on
            // a non-repeat-capable effect type (e.g. PlaceSpy) would have RemainingRepeats
            // stuck at its default of 1 - CurrentState == effect.EffectType would already be
            // true at the very entry state, before any real target was ever picked, letting
            // this command "resolve" a mandatory effect as a success with zero targets chosen.
            var strategy = context.CardRuleEngine.GetStrategy(effect.SourceEffect.Type);
            if (!strategy.SupportsRepeat)
            {
                return context.RejectValidation(nameof(DeclineRepeatCommand), $"the active effect's strategy ({effect.SourceEffect.Type}) doesn't support repeats.");
            }

            // ... belong to the card this command claims (defense against a stale/forged
            // command referencing a sequence that has since resolved and moved on) ...
            if (effect.SourceCard.Id != CardId)
            {
                return context.RejectValidation(nameof(DeclineRepeatCommand), $"CardId mismatch (command claims '{CardId}', active effect belongs to '{effect.SourceCard.Id}').");
            }

            // ... and be SOMEWHERE within this effect's own targeting sub-flow - not
            // necessarily its literal entry state anymore (see IEffectStrategy.
            // GetOwnedActionStates's doc comment). A not-yet-committed sub-pick (e.g. MoveUnit's
            // source-chosen-but-destination-not-yet-picked step) is always safe to discard
            // regardless of which owned state it's in, since nothing has actually been
            // dispatched for it yet - ActionExecutionEngine.DeclineRemainingRepeats calls the
            // strategy's own ResetInProgressSelection to discard it defensively either way.
            if (!strategy.GetOwnedActionStates(effect.SourceEffect).Contains(context.ActionSystem.CurrentState))
            {
                return context.RejectValidation(nameof(DeclineRepeatCommand), $"CurrentState ({context.ActionSystem.CurrentState}) is outside this effect's own targeting sub-flow.");
            }
            return true;
        }

        public void Execute(MatchContext context)
        {
            context.ActionSystem.DeclineRemainingRepeats();
        }
    }
}
