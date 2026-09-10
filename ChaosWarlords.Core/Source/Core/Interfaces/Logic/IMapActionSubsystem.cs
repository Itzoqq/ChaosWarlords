using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Mechanics.Actions.Subsystems
{
    /// <summary>
    /// The basic map-action executors (Assassinate/Supplant/ReturnTroop/DeployTroop/
    /// DeployFromTrophyHall/MoveTroop) - real logic (resource spend, timing-sensitive Pending*
    /// state capture), not thin wrappers, split out of ActionSystem into its own composed
    /// subsystem 2026-09-10 the same way Devour/Spy already were. See ActionSystem's own
    /// PerformX methods, which delegate straight through to this.
    /// </summary>
    public interface IMapActionSubsystem
    {
        void PerformAssassinate(MapNode node, string? cardId, string? devourCardId = null);
        void PerformReturnTroop(MapNode node, string? cardId);
        void PerformSupplant(MapNode node, string? cardId, string? devourCardId = null);
        void PerformDeployFromTrophyHall(MapNode node, PlayerColor sourcePlayerColor, PlayerColor troopColor, string? cardId);
        void PerformDeployTroop(MapNode node, string? cardId);
        void PerformMoveTroop(MapNode source, MapNode dest, string? cardId);

        // MatchManager stays setter-injected - same genuine circular dependency as
        // IDevourSubsystem's own SetMatchManager (arrives later, from the client layer, only
        // after MatchContext/MatchManager exist, which themselves need ActionSystem - and
        // therefore this subsystem - to already exist first). Needed only for
        // ConsumePendingDevour's transactional Devour-then-Assassinate/Supplant handling.
        void SetMatchManager(IMatchManager matchManager);
    }
}
