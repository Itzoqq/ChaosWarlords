using System;
using System.Collections.Generic;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Mechanics.Actions;
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
        public bool TryExecutePreTarget(
            Card card, 
            ActionState state, 
            Func<MapNode?, Site?, IGameCommand?> handleTargetClick,
            Action<Card?> handleDevourSelection,
            Action<IGameCommand> onAutoExecuteCommand)
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
            ExecutePreTargetByType(target, state, handleTargetClick, handleDevourSelection, onAutoExecuteCommand);

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
            Action<Card?> handleDevourSelection,
            Action<IGameCommand> onAutoExecuteCommand)
        {
            // Special case: Devour targeting
            if (state == ActionState.TargetingDevourHand)
            {
                ExecuteDevourPreTarget(target, handleDevourSelection);
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

        private void ExecuteDevourPreTarget(object target, Action<Card?> handleDevourSelection)
        {
            if (target is Card card)
            {
                handleDevourSelection(card);
            }
            else if (target == ActionSystem.SkippedTarget)
            {
                handleDevourSelection(null);
            }
            else
            {
                _logger.Log($"PreTargetHandler: Invalid devour target type", LogChannel.Warning);
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
