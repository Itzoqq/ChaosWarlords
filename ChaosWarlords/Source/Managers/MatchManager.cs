using ChaosWarlords.Source.Core.Interfaces.Services;
using System.Linq;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Mechanics.Rules;

namespace ChaosWarlords.Source.Managers
{
    public class MatchManager : IMatchManager
    {
        private readonly MatchContext _context;
        // 1. Add the Processor to handle the logic
        private readonly CardEffectProcessor _effectProcessor;

        public MatchManager(MatchContext context)
        {
            _context = context;
            _effectProcessor = new CardEffectProcessor();
        }

        public void PlayCard(Card card)
        {
            // --- 1. PRE-CALCULATION (SNAPSHOT) ---
            // We must calculate Focus BEFORE moving the card to 'Played' or modifying the turn stats.
            // Focus Condition: Played another card of same aspect OR Reveal one from hand.

            int currentCount = _context.TurnManager.CurrentTurnContext.GetAspectCount(card.Aspect);
            bool playedAnother = currentCount > 0;

            // Check hand for a DIFFERENT card of the same aspect
            bool canRevealFromHand = _context.ActivePlayer.Hand.Any(c => c.Aspect == card.Aspect && c != card);

            bool hasFocus = playedAnother || canRevealFromHand;

            // --- 2. STATE MUTATION ---

            // Verify Ownership: Cannot play a card that isn't in your hand!
            if (!_context.ActivePlayer.Hand.Contains(card))
            {
                GameLogger.Log($"Attempted to play card {card.Name} which is NOT in active player's hand.", LogChannel.Error);
                return;
            }

            // Use PlayerStateManager for centralized mutation
            _context.PlayerStateManager.PlayCard(_context.ActivePlayer, card);

            // --- 3. RESOLVE EFFECTS (The Missing Link) ---
            // Now that the card is "played", we trigger its game logic.
            // We pass the 'hasFocus' snapshot we calculated earlier.
            CardEffectProcessor.ResolveEffects(card, _context, hasFocus);

            // --- 4. UPDATE STATS ---
            // Finally, register the card with the turn manager to update Aspect counts for future Focus checks.
            _context.TurnManager.PlayCard(card);
        }

        public void DevourCard(Card card)
        {
            _context.PlayerStateManager.DevourCard(_context.ActivePlayer, card);

            // If the card was successfully moved to Void location by the state manager,
            // we track it in the global VoidPile for match history.
            if (card.Location == CardLocation.Void)
            {
                _context.VoidPile.Add(card);
            }
        }

        public void MoveCardToPlayed(Card card)
        {
            _context.PlayerStateManager.PlayCard(_context.ActivePlayer, card);
        }

        public bool CanEndTurn(out string reason)
        {
            if (_context.TurnManager.CurrentTurnContext.PendingPromotionsCount > 0)
            {
                // Optional: Could block here if strictly enforcing cleanup
            }

            reason = string.Empty;
            return true;
        }

        public void EndTurn()
        {
            // 1. Map Rewards - REMOVED (Now Start of Turn)

            // 2. Cleanup: Move Hand + Played -> Discard
            _context.PlayerStateManager.CleanUpTurn(_context.ActivePlayer);

            // 3. Draw New Hand
            _context.PlayerStateManager.DrawCards(_context.ActivePlayer, GameConstants.HandSize, _context.Random);

            // 4. Switch Player
            _context.TurnManager.EndTurn();

            // 5. START OF TURN Actions for the NEW active player

            // Phase Check: Transition from Setup to Playing?
            // Phase Check: Transition from Setup to Playing?
            if (_context.CurrentPhase == MatchPhase.Setup)
            {
                // Check if ALL players have placed their initial troop
                // (Assuming 1 troop per player for Setup)
                bool allDeployed = _context.TurnManager.Players.All(p =>
                    _context.MapManager.Nodes.Any(n => n.Occupant == p.Color));

                // SAFEGUARD: If any player has cards in Discard Pile, the game has clearly started (Setup phase doesn't use cards).
                // This prevents getting stuck in Setup if a player is wiped or deployment logic fails.
                bool gameHasProgressed = _context.TurnManager.Players.Any(p => p.DiscardPile.Count > 0);

                if (allDeployed || gameHasProgressed)
                {
                    GameLogger.Log("All armies deployed (or game in progress). The War Begins! (Entering Playing Phase)", LogChannel.General);
                    _context.CurrentPhase = MatchPhase.Playing;
                    _context.MapManager.SetPhase(MatchPhase.Playing);
                }
            }

            _context.MapManager.DistributeStartOfTurnRewards(_context.ActivePlayer);
        }
    }
}


