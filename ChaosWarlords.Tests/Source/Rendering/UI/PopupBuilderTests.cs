using ChaosWarlords.Source.Rendering.UI;

namespace ChaosWarlords.Tests.Source.Rendering.UI
{
    [TestClass]
    [TestCategory("Unit")]
    public class PopupBuilderTests
    {
        [TestMethod]
        public void Build_WithDefaults_CreatesPopupWithCloseButton()
        {
            var builder = new PopupBuilder();
            var popup = builder.Build();

            Assert.AreEqual("Popup", popup.Title);
            Assert.AreEqual("", popup.Message);
#pragma warning disable MSTEST0037 // Use Assert.HasCount
            Assert.AreEqual(1, popup.Buttons.Count);
#pragma warning restore MSTEST0037
            Assert.AreEqual("OK", popup.Buttons[0].Text);
            Assert.IsTrue(popup.Buttons[0].IsDefault);
        }

        [TestMethod]
        public void Build_WithCustomData_CreatesCorrectPopup()
        {
            var popup = new PopupBuilder()
                .WithTitle("Test Title")
                .WithMessage("Test Message")
                .AddButton("Yes", () => { }, true)
                .AddButton("No", () => { })
                .Build();

            Assert.AreEqual("Test Title", popup.Title);
            Assert.AreEqual("Test Message", popup.Message);
#pragma warning disable MSTEST0037 // Use Assert.HasCount
            Assert.AreEqual(2, popup.Buttons.Count);
#pragma warning restore MSTEST0037
            Assert.AreEqual("Yes", popup.Buttons[0].Text);
            Assert.IsTrue(popup.Buttons[0].IsDefault);
            Assert.AreEqual("No", popup.Buttons[1].Text);
            Assert.IsFalse(popup.Buttons[1].IsDefault);
        }

        [TestMethod]
        public void InvokeDefaultAction_InvokesDefaultButton()
        {
            bool invoked = false;
            var popup = new PopupBuilder()
                .AddButton("Action", () => { invoked = true; }, true)
                .Build();

            popup.InvokeDefaultAction();

            Assert.IsTrue(invoked);
            Assert.IsFalse(popup.IsVisible); // Default action closes popup
        }
    }
}
