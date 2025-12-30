using ChaosWarlords.Source.Core.Interfaces.State;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Needed for the Spy Selection Popup
    /// </summary>
    public class ResolveSpyCommand : IGameCommand
    {
        public int SiteId { get; }
        public PlayerColor SpyColor { get; }
        public string? CardId { get; }

        public ResolveSpyCommand(int siteId, PlayerColor spyColor, string? cardId = null)
        {
            SiteId = siteId;
            SpyColor = spyColor;
            CardId = cardId;
        }

        public void Execute(IGameplayState state)
        {
            state.MatchContext?.RecordAction("ResolveSpy", $"Selected {SpyColor} spy to return from Site {SiteId}");
            
            var site = state.MapManager.Sites.FirstOrDefault(s => s.Id == SiteId);
            if (site != null)
            {
                // Execute logic directly with the stored CardId (works for Replay and Normal)
                state.ActionSystem.PerformSpyReturn(site, SpyColor, CardId);
                
                // Replay Consistency: Ensure card is played if it wasn't by ActionSystem
                if (!string.IsNullOrEmpty(CardId))
                {
                    if (state.ActionSystem.PendingCard == null)
                    {
                         var player = state.TurnManager.ActivePlayer;
                         var card = player.Hand.FirstOrDefault(c => c.Id == CardId);
                         if (card != null)
                         {
                             state.MatchManager.PlayCard(card);
                         }
                    }
                }
            }
    }
}
}



