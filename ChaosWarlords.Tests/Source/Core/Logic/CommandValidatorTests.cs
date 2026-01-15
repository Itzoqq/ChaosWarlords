using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Utilities;

namespace ChaosWarlords.Tests.Source.Core.Logic
{
    [TestClass]

    [TestCategory("Unit")]
    public class CommandValidatorTests
    {
        // Mock Command
        public class TestCommand : IGameCommand
        {
            public ChaosWarlords.Source.Core.Data.Enums.CommandType Type => ChaosWarlords.Source.Core.Data.Enums.CommandType.None;

            public ChaosWarlords.Source.Core.Data.Dtos.GameCommandDto ToDto() => new ChaosWarlords.Source.Core.Data.Dtos.ActionCompletedCommandDto();

            public bool Validate(ChaosWarlords.Source.Contexts.MatchContext context) => true;
            public void Execute(ChaosWarlords.Source.Contexts.MatchContext context) { }
        }

        // Mock Validator
        public class TestCommandValidator : ICommandValidator<TestCommand>
        {
            public bool ShouldFail { get; set; }

            public ValidationResult Validate(TestCommand command, ChaosWarlords.Source.Contexts.MatchContext context)
            {
                if (ShouldFail)
                {
                    return ValidationResult.Failure("Failed by design", ValidationFailureReason.RuleViolation);
                }
                return ValidationResult.Success();
            }
        }

        [TestMethod]
        public void Validate_Success_ReturnsIsValidTrue()
        {
            // Arrange
            var validator = new TestCommandValidator { ShouldFail = false };
            var command = new TestCommand();
            // We need a dummy MatchContext, which is hard to mock due to concrete dependencies.
            // But validation here ignores the context. We can pass null context?
            // The interface requires it. Let's try to mock it or pass null if logic allows.
            // Given the test double logic "return Success", null should work for runtime, 
            // but for type safety it expects MatchContext.
            // Let's create a minimal context or pass null!.
            ChaosWarlords.Source.Contexts.MatchContext context = null!;

            // Act
            var result = validator.Validate(command, context);

            // Assert
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(ValidationFailureReason.None, result.Reason);
        }

        [TestMethod]
        public void Validate_Failure_ReturnsIsValidFalseAndReason()
        {
            // Arrange
            var validator = new TestCommandValidator { ShouldFail = true };
            var command = new TestCommand();
            ChaosWarlords.Source.Contexts.MatchContext context = null!;

            // Act
            var result = validator.Validate(command, context);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Failed by design", result.Message);
            Assert.AreEqual(ValidationFailureReason.RuleViolation, result.Reason);
        }
    }
}
