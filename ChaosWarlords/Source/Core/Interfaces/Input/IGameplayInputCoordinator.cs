namespace ChaosWarlords.Source.Core.Interfaces.Input
{
    /// <summary>
    /// Interface for coordinating gameplay input across different input modes.
    /// Extracted to enable unit testing with NSubstitute.
    /// </summary>
    public interface IGameplayInputCoordinator
    {
        /// <summary>
        /// Gets the current input mode.
        /// </summary>
        IInputMode CurrentMode { get; }

        /// <summary>
        /// Handles input for the current frame and executes resulting commands.
        /// </summary>
        void HandleInput();

        /// <summary>
        /// Switches to normal play input mode.
        /// </summary>
        void SwitchToNormalMode();

        /// <summary>
        /// Switches to targeting input mode (assassinate, return spy, etc.).
        /// </summary>
        void SwitchToTargetingMode();

        /// <summary>
        /// Sets the market input mode.
        /// </summary>
        void SetMarketMode(bool isOpen);
    }
}



