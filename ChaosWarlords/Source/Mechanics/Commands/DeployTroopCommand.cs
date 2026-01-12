using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Commands
{
    public class DeployTroopCommand : IGameCommand
    {
        public ChaosWarlords.Source.Core.Data.Enums.CommandType Type => ChaosWarlords.Source.Core.Data.Enums.CommandType.DeployTroop;

        public ChaosWarlords.Source.Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new ChaosWarlords.Source.Core.Data.Dtos.DeployTroopCommandDto
            {
                NodeId = Node.Id
            };
        }
        public MapNode Node { get; }
        public Player? Player { get; }
        
        // Constructor for normal gameplay (uses ActivePlayer)
        public DeployTroopCommand(MapNode node) 
        { 
            Node = node;
            Player = null; // Will use ActivePlayer during execution
        }
        
        // Constructor for replay (uses specific player)
        public DeployTroopCommand(MapNode node, Player player)
        {
            Node = node;
            Player = player;
        }

        public bool Validate(MatchContext context)
        {
            var p = Player ?? context.TurnManager.ActivePlayer;
            return context.MapManager.CanDeployAt(Node, p.Color);
        }

        public void Execute(MatchContext context)
        {
            var p = Player ?? context.TurnManager.ActivePlayer;
            context.MapManager.TryDeploy(p, Node);
        }
    }
}

