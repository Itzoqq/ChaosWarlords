using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Commands
{
    /// <summary>
    /// Discards a named card from a SPECIFIC player's hand - not implicitly
    /// context.TurnManager.ActivePlayer, unlike every other command in this codebase. This is
    /// deliberate: Insane Outcast discards from its own owner's hand (who IS the active
    /// player when it resolves), but Neogi's "each opponent must discard a card" forces
    /// OTHER players to discard during the active player's End Turn - the target player must
    /// stay explicit and independent of whoever ActivePlayer resolves to at Execute() time.
    /// </summary>
    public class DiscardCardCommand : IGameCommand
    {
        public Core.Data.Enums.CommandType Type => Core.Data.Enums.CommandType.DiscardCard;

        public Core.Data.Dtos.GameCommandDto ToDto()
        {
            return new Core.Data.Dtos.DiscardCardCommandDto
            {
                PlayerColor = TargetPlayerColor.ToString(),
                CardId = CardId
            };
        }

        public PlayerColor TargetPlayerColor { get; }
        public string? CardId { get; }

        public DiscardCardCommand(PlayerColor targetPlayerColor, string? cardId)
        {
            TargetPlayerColor = targetPlayerColor;
            CardId = cardId;
        }

        public bool Validate(MatchContext context)
        {
            var player = context.TurnManager.GetPlayerByColor(TargetPlayerColor);
            if (player == null) return false;

            var card = player.Hand.FirstOrDefault(c => c.Id == CardId);
            return card != null;
        }

        public void Execute(MatchContext context)
        {
            var player = context.TurnManager.GetPlayerByColor(TargetPlayerColor);
            var card = player?.Hand.FirstOrDefault(c => c.Id == CardId);

            if (player == null || card == null)
            {
                return;
            }

            context.PlayerStateManager.DiscardCard(player, card);
            context.RecordAction("DiscardCard", $"{player.DisplayName} discarded {card.Name}.");

            // Neogi's cross-player forced-discard sequencing (MatchManager.ResolveOpponentDiscard)
            // needs to know a discard just resolved so it can advance to the next opponent -
            // it reacts to CompleteAction() the same way every other blocking targeting
            // command signals "done" (see ReturnTroopCommand etc.).
            context.ActionSystem.CompleteAction();
        }
    }
}
