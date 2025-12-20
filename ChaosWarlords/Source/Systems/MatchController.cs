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
            // 1. Notify TurnManager (Tracks stats like Focus inside CurrentTurnContext)
            _context.TurnManager.PlayCard(card);

            // 2. Execute Immediate Effects
            ResolveCardEffects(card);

            // 3. Move from Hand to Played
            MoveCardToPlayed(card);
        }

        public void ResolveCardEffects(Card card)
        {
            // --- FOCUS LOGIC START ---

            // Get the count from our new Context
            int playedCount = _context.TurnManager.CurrentTurnContext.GetAspectCount(card.Aspect);

            // Condition 1: Have we played ANOTHER card of this aspect?
            // (Count > 1 because the current card was recorded in Step 1)
            bool playedAnother = playedCount > 1;

            // Condition 2: Can we reveal a card of this aspect from hand?
            // (The current card is technically still in 'Hand' list until Step 3, so we exclude it)
            bool canRevealFromHand = _context.ActivePlayer.Hand.Any(c => c.Aspect == card.Aspect && c != card);

            bool hasFocus = playedAnother || canRevealFromHand;
            // --- FOCUS LOGIC END ---

            foreach (var effect in card.Effects)
            {
                // Skip conditional effects if Focus requirements aren't met
                if (effect.RequiresFocus && !hasFocus)
                {
                    continue;
                }

                switch (effect.Type)
                {
                    case EffectType.GainResource:
                        if (effect.TargetResource == ResourceType.Power)
                            _context.ActivePlayer.Power += effect.Amount;
                        else if (effect.TargetResource == ResourceType.Influence)
                            _context.ActivePlayer.Influence += effect.Amount;
                        else if (effect.TargetResource == ResourceType.VictoryPoints)
                            _context.ActivePlayer.VictoryPoints += effect.Amount;
                        break;

                    case EffectType.DrawCard:
                        _context.ActivePlayer.DrawCards(effect.Amount);
                        break;

                    case EffectType.Devour:
                        // Auto-devour logic would go here
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

            // 4. Switch Player (This now resets TurnContext internally)
            _context.TurnManager.EndTurn();

            // 5. Update ActionSystem target
            _context.ActionSystem.SetCurrentPlayer(_context.ActivePlayer);
        }
    }
}