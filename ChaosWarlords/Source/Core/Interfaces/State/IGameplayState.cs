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
    /// <summary>
    /// The specific state interface for the main gameplay loop.
    /// Exposes necessary managers and state queries for controllers and views.
    /// </summary>
    public interface IGameplayState : IState
    {
        // --- Managers ---

        /// <summary>
        /// Gets the input manager responsible for polling hardware.
        /// </summary>
        IInputManager InputManager { get; }

        /// <summary>
        /// Gets the logger instance.
        /// </summary>
        IGameLogger Logger { get; }

        /// <summary>
        /// Gets the UI manager responsible for HUD and Menu rendering/interaction.
        /// </summary>
        IUIManager UIManager { get; }

        /// <summary>
        /// Gets the map manager for board state.
        /// </summary>
        IMapManager MapManager { get; }

        /// <summary>
        /// Gets the market manager for card purchasing.
        /// </summary>
        IMarketManager MarketManager { get; }

        /// <summary>
        /// Gets the action system for targeting and resolution.
        /// </summary>
        IActionSystem ActionSystem { get; }

        /// <summary>
        /// Gets the turn manager for phase control.
        /// </summary>
        ITurnManager TurnManager { get; }

        /// <summary>
        /// Gets the composite match context containing all shared data.
        /// </summary>
        MatchContext MatchContext { get; }

        /// <summary>
        /// Gets the match manager for high-level rules.
        /// </summary>
        IMatchManager MatchManager { get; }

        // --- State Properties ---

        /// <summary>
        /// Gets the current input mode (e.g., Normal, Targeting, Market).
        /// </summary>
        IInputMode InputMode { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the market overlay is open.
        /// </summary>
        bool IsMarketOpen { get; set; }

        /// <summary>
        /// Gets a value indicating whether a confirmation popup is currently blocking input.
        /// </summary>
        bool IsConfirmationPopupOpen { get; }

        /// <summary>
        /// Gets a value indicating whether the pause menu is active.
        /// </summary>
        bool IsPauseMenuOpen { get; }

        // --- Layout ---

        /// <summary>
        /// Y-coordinate for rendering the player's hand.
        /// </summary>
        int HandY { get; }

        /// <summary>
        /// Y-coordinate for rendering the played cards area.
        /// </summary>
        int PlayedY { get; }

        // --- Flow Control ---

        /// <summary>
        /// Checks if the turn can be ended.
        /// </summary>
        /// <param name="reason">The reason for failure, if any.</param>
        /// <returns>True if allowed.</returns>
        bool CanEndTurn(out string reason);

        /// <summary>
        /// Ends the current turn.
        /// </summary>
        void EndTurn();

        /// <summary>
        /// Toggles the visibility of the market overlay.
        /// </summary>
        void ToggleMarket();

        /// <summary>
        /// Closes the market overlay.
        /// </summary>
        void CloseMarket();

        /// <summary>
        /// Switches input context to Targeting mode (usually for card effects).
        /// </summary>
        void SwitchToTargetingMode();

        /// <summary>
        /// Switches input context back to Normal mode.
        /// </summary>
        void SwitchToNormalMode();

        /// <summary>
        /// Switches input context to Promote mode for card promotion.
        /// </summary>
        /// <param name="amount">The number of cards to promote (usually 1).</param>
        void SwitchToPromoteMode(int amount);

        // --- Input Delegation (for PlayerController) ---

        /// <summary>
        /// Handles the 'Escape' key press action (Pause/Back).
        /// </summary>
        void HandleEscapeKeyPress();

        /// <summary>
        /// Handles the key press designated for ending the turn.
        /// </summary>
        void HandleEndTurnKeyPress();

        // --- Gameplay Actions ---

        /// <summary>
        /// Initiates the play sequence for a specific card.
        /// </summary>
        /// <param name="card">The card to play.</param>
        void PlayCard(Card card);

        /// <summary>
        /// Moves a card physically to the played zone.
        /// </summary>
        /// <param name="card">The card to move.</param>
        void MoveCardToPlayed(Card card);

        // --- Query Methods ---

        /// <summary>
        /// Checks if a valid target exists for the given card's effects.
        /// </summary>
        /// <param name="card">The card to check.</param>
        /// <returns>True if at least one valid target exists.</returns>
        bool HasViableTargets(Card card);

        /// <summary>
        /// Retrieves the instructional text for the current targeting state.
        /// </summary>
        /// <param name="state">The action state to describe.</param>
        /// <returns>A localized or readable string instruction.</returns>
        string GetTargetingText(ActionState state);

        // --- Interaction Helpers ---

        /// <summary>
        /// Gets the card currently hovered in the hand, if any.
        /// </summary>
        /// <returns>The hovered card or null.</returns>
        Card? GetHoveredHandCard();

        /// <summary>
        /// Gets the card currently hovered in the played area, if any.
        /// </summary>
        /// <returns>The hovered card or null.</returns>
        Card? GetHoveredPlayedCard();

        /// <summary>
        /// Gets the card currently hovered in the market, if any.
        /// </summary>
        /// <returns>The hovered card or null.</returns>
        Card? GetHoveredMarketCard();
    }
}



