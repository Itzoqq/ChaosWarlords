using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class DeployTroopCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.DeployTroop;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.DeployTroopCommandDto
            {
                NodeId = NodeId
            };
        }

        public int NodeId { get; }

        public DeployTroopCommand(int nodeId)
        {
            NodeId = nodeId;
        }

        public DeployTroopCommand(Entities.Map.MapNode node) : this(node.Id) { }

        public bool Validate(MatchContext context)
        {
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == NodeId);
            if (node == null) return false;

            // Deploy is always the active player's own action (there's no "deploy for someone
            // else" in the rules), matching AssassinateCommand/SupplantCommand/etc.'s pattern.
            var player = context.TurnManager.ActivePlayer;
            return context.MapManager.CanDeployAt(node, player.Color);
        }

        public void Execute(MatchContext context)
        {
            var node = context.MapManager.Nodes.FirstOrDefault(n => n.Id == NodeId);
            if (node != null)
            {
                context.MapManager.TryDeploy(context.TurnManager.ActivePlayer, node);
            }
        }
    }
}
