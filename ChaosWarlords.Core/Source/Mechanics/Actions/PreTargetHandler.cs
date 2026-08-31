using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Source.Managers
{
    /// <summary>
    /// Internal helper class responsible for handling pre-target auto-execution logic.
    /// Extracted from ActionSystem to reduce cyclomatic complexity.
    /// </summary>
    internal class PreTargetHandler
    {
        private readonly IGameLogger _logger;
        private readonly Dictionary<Card, Dictionary<ActionState, object>> _preSelectedTargets;

        public PreTargetHandler(IGameLogger logger, Dictionary<Card, Dictionary<ActionState, object>> preSelectedTargets)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _preSelectedTargets = preSelectedTargets ?? throw new ArgumentNullException(nameof(preSelectedTargets));
        }

        /// <summary>
        /// Attempts to execute a pre-selected target if one exists for the given card and state.
        /// Returns true if a pre-target was found and executed, false otherwise.
        /// </summary>
        /// <summary>
        /// Attempts to execute a pre-selected target if one exists for the given card and state.
        /// Returns true if a pre-target was found and executed, false otherwise.
        /// </summary>
        public bool TryExecutePreTarget(
            Card card,
            ActionState state,
            Func<MapNode?, Site?, IGameCommand?> handleTargetClick,
            Func<Card?, IGameCommand?> handleDevourSelection,
            Action<IGameCommand> onAutoExecuteCommand,
            Action onSkipped)
        {
            if (!_preSelectedTargets.TryGetValue(card, out var stateTargets))
            {
                return false;
            }

            if (!stateTargets.TryGetValue(state, out var target))
            {
                return false;
            }

            _logger.Log($"PreTargetHandler: Pre-Target found for {state}. Auto-executing...", LogChannel.Info);

            // Consume the target immediately to prevent "zombie" targets
            ConsumePreTarget(card, state, stateTargets);

            // Execute based on target type
            ExecutePreTargetByType(target, state, handleTargetClick, handleDevourSelection, onAutoExecuteCommand, onSkipped);

            return true;
        }

        private void ConsumePreTarget(Card card, ActionState state, Dictionary<ActionState, object> stateTargets)
        {
            stateTargets.Remove(state);
            if (stateTargets.Count == 0)
            {
                _preSelectedTargets.Remove(card);
            }
        }

        private void ExecutePreTargetByType(
            object target,
            ActionState state,
            Func<MapNode?, Site?, IGameCommand?> handleTargetClick,
            Func<Card?, IGameCommand?> handleDevourSelection,
            Action<IGameCommand> onAutoExecuteCommand,
            Action onSkipped)
        {
            // Special case: Devour targeting
            if (state == ActionState.TargetingDevourHand)
            {
                ExecuteDevourPreTarget(target, handleDevourSelection, onAutoExecuteCommand, onSkipped);
                return;
            }

            // MapNode targeting
            if (target is MapNode node)
            {
                ExecuteMapNodePreTarget(node, handleTargetClick, onAutoExecuteCommand);
                return;
            }

            // Site targeting
            if (target is Site site)
            {
                ExecuteSitePreTarget(site, handleTargetClick, onAutoExecuteCommand);
                return;
            }

            _logger.Log($"PreTargetHandler: Unknown target type {target.GetType().Name}", LogChannel.Warning);
        }

        private void ExecuteDevourPreTarget(
            object target,
            Func<Card?, IGameCommand?> handleDevourSelection,
            Action<IGameCommand> onAutoExecuteCommand,
            Action onSkipped)
        {
            if (target == ActionSystem.SkippedTarget)
            {
                // A skip has no command to dispatch - handleDevourSelection(null) always
                // returns null for it (DevourSubsystem.HandleDevourSelection's null guard),
                // so there is nothing for onAutoExecuteCommand to run. Without resolving the
                // effect here explicitly, the pending Devour EffectContext was left stuck on
                // ExecutionStack forever (CurrentState frozen at TargetingDevourHand) - the
                // pre-target was already consumed above, so nothing would ever retry it. See
                // planning.txt.
                onSkipped();
                return;
            }

            IGameCommand? cmd = null;

            if (target is Card card)
            {
                cmd = handleDevourSelection(card);
            }
            else
            {
                _logger.Log($"PreTargetHandler: Invalid devour target type", LogChannel.Warning);
            }

            if (cmd != null)
            {
                onAutoExecuteCommand(cmd);
            }
        }

        private static void ExecuteMapNodePreTarget(
            MapNode node,
            Func<MapNode?, Site?, IGameCommand?> handleTargetClick,
            Action<IGameCommand> onAutoExecuteCommand)
        {
            var cmd = handleTargetClick(node, null);
            if (cmd != null)
            {
                onAutoExecuteCommand(cmd);
            }
        }

        private static void ExecuteSitePreTarget(
            Site site,
            Func<MapNode?, Site?, IGameCommand?> handleTargetClick,
            Action<IGameCommand> onAutoExecuteCommand)
        {
            var cmd = handleTargetClick(null, site);
            if (cmd != null)
            {
                onAutoExecuteCommand(cmd);
            }
        }
    }
}
