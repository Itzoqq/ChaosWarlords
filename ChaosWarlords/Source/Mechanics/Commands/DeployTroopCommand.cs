using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Entities.Actors;

namespace ChaosWarlords.Source.Commands
{
    public class DeployTroopCommand : IGameCommand
    {
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

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("Deploy", $"Deployed troop at Node {Node.Id}");
            
            // Use the stored player if available (replay), otherwise use ActivePlayer (normal gameplay)
            var playerToUse = Player ?? state.TurnManager.ActivePlayer;
            state.MapManager.TryDeploy(playerToUse, Node);
        }
    }
}

