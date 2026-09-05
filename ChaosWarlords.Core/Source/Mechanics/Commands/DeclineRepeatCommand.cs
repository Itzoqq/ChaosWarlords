using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

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
            if (effect?.SourceEffect == null || !effect.SourceEffect.AllowPartialRepeat) return false;

            // ... on a strategy that actually opted into repeats in the first place. Without
            // this, a future card that mistakenly (or maliciously) sets AllowPartialRepeat on
            // a non-repeat-capable effect type (e.g. PlaceSpy) would have RemainingRepeats
            // stuck at its default of 1 - CurrentState == effect.EffectType would already be
            // true at the very entry state, before any real target was ever picked, letting
            // this command "resolve" a mandatory effect as a success with zero targets chosen.
            if (!context.CardRuleEngine.GetStrategy(effect.SourceEffect.Type).SupportsRepeat) return false;

            // ... belong to the card this command claims (defense against a stale/forged
            // command referencing a sequence that has since resolved and moved on) ...
            if (effect.SourceCard.Id != CardId) return false;

            // ... and be at a genuine repeat boundary, not mid-way through a multi-click
            // sub-target (e.g. MoveUnit's source-chosen-but-destination-not-yet-picked step) -
            // CurrentState only equals the effect's own entry state at the very start of the
            // sequence or between two repeats, never partway through one.
            return context.ActionSystem.CurrentState == effect.EffectType;
        }

        public void Execute(MatchContext context)
        {
            context.ActionSystem.DeclineRemainingRepeats();
        }
    }
}
