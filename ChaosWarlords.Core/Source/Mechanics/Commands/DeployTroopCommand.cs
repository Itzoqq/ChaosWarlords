using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    public class DeployTroopCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.DeployTroop;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.DeployTroopCommandDto
            {
                NodeId = NodeId,
                CardId = CardId
            };
        }

        public int NodeId { get; }

        /// <summary>
        /// Set only when this Deploy is sourced from a card effect (EffectType.DeployTroop,
        /// Gibbering Mouther) rather than the paid basic action - toggles Execute() between
        /// ActionSystem.PerformDeployTroop (stack-integrated: funds the deploy for free,
        /// records the destination node, calls CompleteAction()) and the plain
        /// MapManager.TryDeploy fire-and-forget call the basic action has always used
        /// (untouched for every existing caller, which never sets this).
        /// </summary>
        public string? CardId { get; }

        public DeployTroopCommand(int nodeId, string? cardId = null)
        {
            NodeId = nodeId;
            CardId = cardId;
        }

        public DeployTroopCommand(Entities.Map.MapNode node, string? cardId = null) : this(node.Id, cardId) { }

        public bool Validate(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(NodeId);
            if (node == null) return context.RejectValidation(nameof(DeployTroopCommand), $"node {NodeId} not found.");

            // Deploy is always the active player's own action (there's no "deploy for someone
            // else" in the rules), matching AssassinateCommand/SupplantCommand/etc.'s pattern.
            var player = context.TurnManager.ActivePlayer;
            if (!context.MapManager.CanDeployAt(node, player.Color))
            {
                return context.RejectValidation(nameof(DeployTroopCommand), $"MapManager rejected deploying at node {NodeId} (occupied or no Presence).");
            }

            // Card-sourced Deploy is a stack-integrated targeting effect (see
            // ActionState.TargetingDeployTroop) - defense-in-depth against a forged command
            // bypassing ActionInputController's UI-layer state check, mirroring
            // SelectOpponentCommand.Validate()'s own CurrentState guard.
            if (!string.IsNullOrEmpty(CardId) && context.ActionSystem.CurrentState != ActionState.TargetingDeployTroop)
            {
                return context.RejectValidation(nameof(DeployTroopCommand), $"CurrentState is {context.ActionSystem.CurrentState}, not TargetingDeployTroop.");
            }
            return true;
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.GetNodeById(NodeId);
            if (node == null) return;

            if (!string.IsNullOrEmpty(CardId))
            {
                context.ActionSystem.PerformDeployTroop(node, CardId);
            }
            else
            {
                context.MapManager.TryDeploy(context.TurnManager.ActivePlayer, node);
            }
        }
    }
}
