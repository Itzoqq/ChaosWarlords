using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Interfaces.Logic;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Core.Interfaces.State;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Core.Logic
{
    [TestClass]
    public class CommandValidatorTests
    {
        // Mock Command
        public class TestCommand : IGameCommand
        {
            public void Execute(IGameplayState state) { }
        }

        // Mock Validator
        public class TestCommandValidator : ICommandValidator<TestCommand>
        {
            public bool ShouldFail { get; set; }

            public ValidationResult Validate(TestCommand command, IGameplayState state)
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
            var state = Substitute.For<IGameplayState>();

            // Act
            var result = validator.Validate(command, state);

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
            var state = Substitute.For<IGameplayState>();

            // Act
            var result = validator.Validate(command, state);

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Failed by design", result.Message);
            Assert.AreEqual(ValidationFailureReason.RuleViolation, result.Reason);
        }
    }
}
