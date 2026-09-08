using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Takes one troopColor troop from sourcePlayerColor's trophy hall and deploys it as the
    /// ACTIVE player's OWN troop at node - EffectType.DeployFromTrophyHall (Mummy Lord: "take a
    /// white troop from any trophy hall and deploy it anywhere on the board" -
    /// TROPHY-HALL-AS-TROOP-RESERVOIR, planning.txt). SourcePlayerColor/TroopColor are explicit
    /// (mirroring ReturnAnySpyCommand's own explicit SpyColor), even though the ONLY way a real
    /// click can produce this command today is via the sole-eligible-source ActionSystem already
    /// resolved before targeting opened (ActionSystem.PendingTrophyHallSourceColor) - Validate()
    /// re-confirms both fields anyway rather than trusting the caller, since this is the real
    /// defense once a client can dispatch commands directly.
    /// </summary>
    public class DeployFromTrophyHallCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.DeployFromTrophyHall;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.DeployFromTrophyHallCommandDto
            {
                NodeId = TargetNodeId,
                SourcePlayerColor = SourcePlayerColor.ToString(),
                TroopColor = TroopColor.ToString(),
                CardId = CardId
            };
        }

        public int TargetNodeId { get; }
        public PlayerColor SourcePlayerColor { get; }
        public PlayerColor TroopColor { get; }
        public string? CardId { get; }

        public DeployFromTrophyHallCommand(int targetNodeId, PlayerColor sourcePlayerColor, PlayerColor troopColor, string? cardId = null)
        {
            TargetNodeId = targetNodeId;
            SourcePlayerColor = sourcePlayerColor;
            TroopColor = troopColor;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node == null) return context.RejectValidation(nameof(DeployFromTrophyHallCommand), $"target node {TargetNodeId} not found.");

            if (!context.MapManager.CanMoveDestination(node))
            {
                return context.RejectValidation(nameof(DeployFromTrophyHallCommand), $"node {TargetNodeId} is not empty.");
            }

            if (!MatchesRequiredTroopColor(context))
            {
                return context.RejectValidation(nameof(DeployFromTrophyHallCommand), $"card requires a Neutral troop, but {TroopColor} was requested.");
            }

            if (!IsResolvedSource(context))
            {
                return context.RejectValidation(nameof(DeployFromTrophyHallCommand), $"{SourcePlayerColor} is not the resolved trophy hall source ({context.ActionSystem.PendingTrophyHallSourceColor}).");
            }

            if (!SourcePlayerStillHasTheTroop(context))
            {
                return context.RejectValidation(nameof(DeployFromTrophyHallCommand), $"{SourcePlayerColor}'s trophy hall has no {TroopColor} troop to take.");
            }

            return true;
        }

        // Re-derives the neutral-only restriction from the currently pending CardEffect rather
        // than trusting the caller, matching AssassinateCommand/SupplantCommand's own
        // RequiresNeutralTarget pattern.
        private bool MatchesRequiredTroopColor(MatchContext context)
        {
            var pendingEffect = context.ActionSystem.CurrentSourceEffect;
            bool requireNeutral = pendingEffect != null && pendingEffect.Type == EffectType.DeployFromTrophyHall && pendingEffect.TargetNeutralTroopOnly;
            return !requireNeutral || TroopColor == PlayerColor.Neutral;
        }

        // Site-scoped-equivalent guard: the source player must match whichever one
        // ActionSystem/TrophyHallRuleEngine already resolved when targeting opened - see
        // AssassinateCommand.IsAtRequiredSite for the identical "must match what was resolved
        // when this chain/step began" shape (there, a Site; here, a PlayerColor).
        private bool IsResolvedSource(MatchContext context) =>
            context.ActionSystem.PendingTrophyHallSourceColor == SourcePlayerColor;

        private bool SourcePlayerStillHasTheTroop(MatchContext context)
        {
            var sourcePlayer = context.TurnManager.GetPlayerByColor(SourcePlayerColor);
            return sourcePlayer != null && sourcePlayer.TrophyHallByColor.GetValueOrDefault(TroopColor) > 0;
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(TargetNodeId);
            if (node != null)
            {
                context.ActionSystem.PerformDeployFromTrophyHall(node, SourcePlayerColor, TroopColor, CardId);
            }
        }
    }
}
