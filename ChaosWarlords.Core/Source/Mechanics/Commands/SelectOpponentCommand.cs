using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Resolves EffectType.SelectOpponent - the active player chooses one opponent (matching
    /// the eligibility threshold, see SelectOpponentStrategy) to become
    /// TurnManager.ForcedActingPlayer for whatever OnSuccess chains off it (e.g. Cranium Rats'
    /// "choose one opponent with more than 3 cards to discard a card"). Unlike
    /// DiscardCardCommand, there is no separate target-player field carried on top of "acting
    /// player" - the acting player for THIS command is always whoever
    /// context.TurnManager.ActivePlayer currently resolves to (matches the
    /// AssassinateCommand/SupplantCommand convention of trusting ActivePlayer rather than
    /// carrying an explicit actor field), since choosing WHO to target is always the real
    /// active player's own decision (nothing has been force-switched yet at this step).
    /// </summary>
    public class SelectOpponentCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.SelectOpponent;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.SelectOpponentCommandDto
            {
                TargetPlayerColor = TargetPlayerColor.ToString()
            };
        }

        public PlayerColor TargetPlayerColor { get; }

        public SelectOpponentCommand(PlayerColor targetPlayerColor)
        {
            TargetPlayerColor = targetPlayerColor;
        }

        public bool Validate(MatchContext context)
        {
            if (context.ActionSystem.CurrentState != ActionState.TargetingOpponentSelect) return false;

            var active = context.TurnManager.ActivePlayer;
            var target = context.TurnManager.GetPlayerByColor(TargetPlayerColor);
            if (target == null || target == active) return false;

            int threshold = FindThreshold(context.ActionSystem.PendingCard);
            return target.Hand.Count > threshold;
        }

        public void Execute(MatchContext context)
        {
            var target = context.TurnManager.GetPlayerByColor(TargetPlayerColor);
            if (target == null) return;

            // Order matters: BeginForcedActingPlayer BEFORE CompleteAction(), so that when
            // CompleteAction() resolves this effect's OnSuccess chain (e.g. Cranium Rats'
            // DiscardCard), CardEffectProcessor's PushEffectContext reads context.ActivePlayer
            // fresh at that moment and it already resolves to the chosen opponent - see
            // MatchContext.ActivePlayer => TurnManager.ActivePlayer => ForcedActingPlayer ??
            // normal rotation.
            context.TurnManager.BeginForcedActingPlayer(target);
            context.RecordAction("SelectOpponent", $"{target.DisplayName} was chosen as the target.");
            context.ActionSystem.CompleteAction();
        }

        private static int FindThreshold(Card? sourceCard)
        {
            if (sourceCard == null) return 0;
            var effect = sourceCard.Effects.FirstOrDefault(e => e.Type == EffectType.SelectOpponent);
            return effect?.Amount ?? 0;
        }
    }
}
