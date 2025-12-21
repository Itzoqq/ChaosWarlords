using System;
using System.Linq;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Systems
{
    public class MatchController
    {
        private readonly MatchContext _context;

        public MatchController(MatchContext context)
        {
            _context = context;
        }

        public void PlayCard(Card card)
        {
            // --- 1. PRE-CALCULATION (SNAPSHOT) ---
            int currentCount = _context.TurnManager.CurrentTurnContext.GetAspectCount(card.Aspect);
            bool playedAnother = currentCount > 0;
            bool canRevealFromHand = _context.ActivePlayer.Hand.Any(c => c.Aspect == card.Aspect && c != card);
            bool hasFocus = playedAnother || canRevealFromHand;

            // --- 2. STATE MUTATION ---

            // FIX: Remove from Hand if present
            if (_context.ActivePlayer.Hand.Contains(card))
            {
                _context.ActivePlayer.Hand.Remove(card);
            }

            // Add to PlayedCards
            _context.ActivePlayer.PlayedCards.Add(card);

            // Update Stats
            _context.TurnManager.PlayCard(card);

            // Resolve effects
            ResolveCardEffects(card, hasFocus);
        }

        public void ResolveCardEffects(Card card, bool hasFocus)
        {
            foreach (var effect in card.Effects)
            {
                int finalAmount = effect.Amount;
                // Future: Apply Focus bonus logic here if needed

                switch (effect.Type)
                {
                    case EffectType.GainResource:
                        if (effect.TargetResource == ResourceType.Power)
                            _context.ActivePlayer.Power += finalAmount;
                        else if (effect.TargetResource == ResourceType.Influence)
                            _context.ActivePlayer.Influence += finalAmount;
                        break;

                    case EffectType.DrawCard:
                        _context.ActivePlayer.DrawCards(effect.Amount);
                        break;

                    case EffectType.Promote:
                        // --- CHANGED: Pass 'card' as the source ---
                        _context.TurnManager.CurrentTurnContext.AddPromotionCredit(card, finalAmount);
                        GameLogger.Log($"Promotion pending! Added {finalAmount} point(s) from {card.Name}.", LogChannel.Info);
                        break;

                    case EffectType.Devour:
                        break;
                }
            }
        }

        public void MoveCardToPlayed(Card card)
        {
            if (_context.ActivePlayer.Hand.Contains(card))
            {
                _context.ActivePlayer.Hand.Remove(card);
                _context.ActivePlayer.PlayedCards.Add(card);
            }
        }

        public bool CanEndTurn(out string reason)
        {
            if (_context.ActivePlayer.Hand.Count > 0)
            {
                reason = "You must play all cards in your hand before ending your turn.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public void EndTurn()
        {
            // 1. Map Rewards
            _context.MapManager.DistributeControlRewards(_context.ActivePlayer);

            // 2. Cleanup: Move Hand + Played -> Discard
            _context.ActivePlayer.CleanUpTurn();

            // 3. Draw New Hand
            _context.ActivePlayer.DrawCards(5);

            // 4. Switch Player
            _context.TurnManager.EndTurn();
        }
    }
}