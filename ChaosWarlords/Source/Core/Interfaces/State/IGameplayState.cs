#nullable enable
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Interfaces.Input;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Contexts;


namespace ChaosWarlords.Source.Core.Interfaces.State
{
    public interface IGameplayState : IState
    {
        // --- Managers ---
        IInputManager InputManager { get; }
        IUIManager UIManager { get; }
        IMapManager MapManager { get; }
        IMarketManager MarketManager { get; }
        IActionSystem ActionSystem { get; }
        ITurnManager TurnManager { get; }
        MatchContext MatchContext { get; }
        IMatchManager MatchManager { get; }

        // --- State Properties ---
        // Removed 'set' because this is now managed by the InputCoordinator
        IInputMode InputMode { get; }
        bool IsMarketOpen { get; set; }
        bool IsConfirmationPopupOpen { get; }
        bool IsPauseMenuOpen { get; }

        // --- Layout ---
        int HandY { get; }
        int PlayedY { get; }

        // --- Flow Control ---
        bool CanEndTurn(out string reason);
        void EndTurn();
        void ToggleMarket();
        void CloseMarket();

        void SwitchToTargetingMode();
        void SwitchToNormalMode();
        void SwitchToPromoteMode(int amount);

        // --- Input Delegation (for PlayerController) ---
        void HandleEscapeKeyPress();
        void HandleEndTurnKeyPress();

        // --- Gameplay Actions ---
        void PlayCard(Card card);
        // REMOVED: void ResolveCardEffects(Card card); -> Logic moved to CardEffectProcessor
        void MoveCardToPlayed(Card card);

        // --- Query Methods ---
        bool HasViableTargets(Card card);
        string GetTargetingText(ActionState state);

        // --- Interaction Helpers ---
        // These delegates are required for InputModes to interact with the View
        // Does not require 'set' because this is typically a read-only query from the View/InteractionMapper
        Card? GetHoveredHandCard();
        Card? GetHoveredPlayedCard();
        Card? GetHoveredMarketCard();
    }
}



