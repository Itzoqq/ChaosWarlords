using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.States.Input;
using ChaosWarlords.Source.Utilities; // For ActionState

namespace ChaosWarlords.Source.States
{
    // Extends IState (assuming it defines LoadContent, UnloadContent, Update, Draw)
    public interface IGameplayState : IState
    {
        // ------------------------------------------------------------------
        // Public Access to Core Systems/Managers (needed by Commands and InputModes)
        // ------------------------------------------------------------------
        InputManager InputManager { get; }
        IUISystem UIManager { get; }
        IMapManager MapManager { get; }
        IMarketManager MarketManager { get; }
        IActionSystem ActionSystem { get; }
        TurnManager TurnManager { get; }

        // ------------------------------------------------------------------
        // State Properties (for reading/setting game state)
        // ------------------------------------------------------------------
        IInputMode InputMode { get; set; }

        /// <summary>Represents the open/closed state of the Market UI.</summary>
        bool IsMarketOpen { get; set; }

        /// <summary>The screen Y coordinate for cards in the player's hand.</summary>
        int HandY { get; }

        /// <summary>The screen Y coordinate for cards moved to the played pile.</summary>
        int PlayedY { get; }

        // ------------------------------------------------------------------
        // State Transition / Game Logic Methods (called by Commands or InputModes)
        // ------------------------------------------------------------------

        /// <summary>Ends the current player's turn, passes control, and handles cleanup.</summary>
        void EndTurn();

        /// <summary>Opens the market UI.</summary>
        void ToggleMarket();

        /// <summary>Closes the market UI and switches back to normal input mode.</summary>
        void CloseMarket();

        /// <summary>Switches the state's input mode to handle map targeting.</summary>
        void SwitchToTargetingMode();

        /// <summary>Switches the state's input mode back to normal gameplay.</summary>
        void SwitchToNormalMode();

        /// <summary>Plays a card, potentially starting a targeting action or applying immediate effects.</summary>
        void PlayCard(Card card);

        /// <summary>Applies the non-targeting effects of a card (e.g., resource gain).</summary>
        void ResolveCardEffects(Card card);

        /// <summary>Moves a card from the player's Hand to their PlayedCards pile and updates its visual position.</summary>
        void MoveCardToPlayed(Card card);

        /// <summary>Calculates the visual position for all cards in the active player's hand.</summary>
        void ArrangeHandVisuals();

        /// <summary>Retrieves the text hint for the current targeting state.</summary>
        string GetTargetingText(ActionState state);
    }
}