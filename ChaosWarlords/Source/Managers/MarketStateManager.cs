using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Manages the state and mode of market interactions.
    /// Centralizes all market-related state transitions and provides events for UI updates.
    /// </summary>
    public class MarketStateManager : IMarketStateManager
    {
        private readonly IGameLogger _logger;
        private MarketMode _currentMode = MarketMode.Closed;
        private Func<Card, Core.Interfaces.Logic.IGameCommand?>? _devourCallback;

        public MarketMode CurrentMode => _currentMode;
        public bool IsOpen => _currentMode != MarketMode.Closed;
        public Func<Card, Core.Interfaces.Logic.IGameCommand?>? DevourCallback => _devourCallback;

        public event EventHandler<MarketMode>? ModeChanged;

        public MarketStateManager(IGameLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void OpenForBrowsing()
        {
            _logger.Log("MarketStateManager: Opening market for browsing", LogChannel.Info);
            _currentMode = MarketMode.Browse;
            _devourCallback = null;
            ModeChanged?.Invoke(this, _currentMode);
        }

        public void OpenForDevour(Func<Card, Core.Interfaces.Logic.IGameCommand?> onDevourCallback)
        {
            ArgumentNullException.ThrowIfNull(onDevourCallback);

            _logger.Log("MarketStateManager: Opening market for devour targeting", LogChannel.Info);
            _currentMode = MarketMode.DevourTarget;
            _devourCallback = onDevourCallback;
            ModeChanged?.Invoke(this, _currentMode);
        }

        public void Close()
        {
            _logger.Log($"MarketStateManager: Closing market (was {_currentMode})", LogChannel.Info);
            _currentMode = MarketMode.Closed;
            _devourCallback = null;
            ModeChanged?.Invoke(this, _currentMode);
        }
    }
}
