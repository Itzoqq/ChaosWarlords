using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Source.Utilities
{
    /// <summary>
    /// Shared helper for IGameCommand.Validate() implementations to log WHY a command was
    /// rejected, not just that it was. CommandDispatcher.Dispatch's own rejection log only
    /// records the command's type name (e.g. "Validation failed for command
    /// AssassinateCommand") - it never records which of that command's own internal guard
    /// clauses actually produced the `false`, even though most commands have several
    /// independent ways to fail (a stale/nonexistent target, an unmet resource precondition, a
    /// wrong-state precondition, etc.), all otherwise indistinguishable from the log alone.
    /// Every Validate() early-return should go through this instead of a bare `return false;`,
    /// so a tester or an unattended AI-vs-AI batch run has a specific reason to look at instead
    /// of just "this action silently didn't happen."
    /// </summary>
    public static class CommandValidationLogging
    {
        public static bool RejectValidation(this MatchContext context, string commandName, string reason)
        {
            context.Logger.Log($"{commandName}.Validate() rejected: {reason}", LogChannel.Warning);
            return false;
        }
    }
}
