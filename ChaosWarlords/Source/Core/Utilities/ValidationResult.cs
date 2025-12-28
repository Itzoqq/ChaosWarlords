namespace ChaosWarlords.Source.Core.Utilities
{
    /// <summary>
    /// Categorizes the reason for a validation failure.
    /// </summary>
    public enum ValidationFailureReason
    {
        None,
        InvalidState,
        InsufficientResources,
        InvalidTarget,
        RuleViolation,
        Other
    }

    /// <summary>
    /// Represents the result of a validation operation.
    /// </summary>
    public record ValidationResult(bool IsValid, string Message = "", ValidationFailureReason Reason = ValidationFailureReason.None)
    {
        public static ValidationResult Success() => new ValidationResult(true);
        public static ValidationResult Failure(string message, ValidationFailureReason reason = ValidationFailureReason.Other) 
            => new ValidationResult(false, message, reason);
    }
}
