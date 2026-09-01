using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Utilities;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Utilities
{
  [TestClass]

  [TestCategory("Unit")]
  public class LocalizationManagerTests
  {
    private const string MockBundleJson = @"
        {
          ""wight_name"": ""Wight"",
          ""wight_description"": ""Choose one: Gain 2 Power. Or, devour a card in your hand to Supplant a troop.""
        }";

    [TestMethod]
    public void GetString_ReturnsValue_ForKnownKey()
    {
      var loc = new LocalizationManager();
      loc.LoadFromJson(MockBundleJson);

      Assert.AreEqual("Wight", loc.GetString("wight_name"));
      Assert.AreEqual("Choose one: Gain 2 Power. Or, devour a card in your hand to Supplant a troop.", loc.GetString("wight_description"));
    }

    [TestMethod]
    public void GetString_ReturnsMissingPlaceholder_ForUnknownKey()
    {
      // Never a crash or a silent blank string - a missing key should fail loudly during
      // content authoring, not ship a blank card name (see ILocalizationService).
      var loc = new LocalizationManager();
      loc.LoadFromJson(MockBundleJson);

      Assert.AreEqual("[MISSING:nonexistent_key]", loc.GetString("nonexistent_key"));
    }

    [TestMethod]
    public void GetString_ReturnsMissingPlaceholder_WhenNothingLoaded()
    {
      var loc = new LocalizationManager();

      Assert.AreEqual("[MISSING:wight_name]", loc.GetString("wight_name"));
    }

    [TestMethod]
    public void GetString_ForMissingKey_LogsAWarning()
    {
      var logger = Substitute.For<IGameLogger>();
      var loc = new LocalizationManager(logger);
      loc.LoadFromJson(MockBundleJson);

      loc.GetString("nonexistent_key");

      logger.Received(1).Log(Arg.Is<string>(s => s.Contains("nonexistent_key")), LogChannel.Warning);
    }

    [TestMethod]
    public void GetString_ForKnownKey_DoesNotLog()
    {
      var logger = Substitute.For<IGameLogger>();
      var loc = new LocalizationManager(logger);
      loc.LoadFromJson(MockBundleJson);

      loc.GetString("wight_name");

      logger.DidNotReceiveWithAnyArgs().Log(default(string)!, default);
    }

    [TestMethod]
    public void Load_ReadsFromStream_AndPopulatesBundle()
    {
      var loc = new LocalizationManager();
      using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(MockBundleJson)))
      {
        loc.Load(stream);
      }

      Assert.AreEqual("Wight", loc.GetString("wight_name"));
    }

    [TestMethod]
    public void Load_ReplacesAnyPreviouslyLoadedBundle()
    {
      var loc = new LocalizationManager();
      loc.LoadFromJson(MockBundleJson);
      Assert.AreEqual("Wight", loc.GetString("wight_name"));

      loc.LoadFromJson(@"{ ""noble_name"": ""Noble"" }");

      // The new bundle replaced the old one entirely - the previous key is gone.
      Assert.AreEqual("[MISSING:wight_name]", loc.GetString("wight_name"));
      Assert.AreEqual("Noble", loc.GetString("noble_name"));
    }
  }
}
