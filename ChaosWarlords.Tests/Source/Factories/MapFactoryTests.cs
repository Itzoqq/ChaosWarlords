using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;

namespace ChaosWarlords.Tests.Source.Utilities
{
  [TestClass]

  [TestCategory("Integration")]
  public class MapFactoryTests
  {
    // A mock JSON string representing a small, simple map.
    // This avoids needing a physical file for the test.
    private const string MockMapJson = @"
        {
          ""nodes"": [
            { ""id"": 1, ""x"": 10, ""y"": 10 },
            { ""id"": 2, ""x"": 20, ""y"": 10 },
            { ""id"": 3, ""x"": 30, ""y"": 10 }
          ],
          ""sites"": [
            {
              ""name"": ""Test Site"",
              ""isCity"": true,
              ""nodeIds"": [ 2, 3 ],
              ""controlResource"": ""Power"", 
              ""controlAmount"": 1,
              ""totalControlResource"": ""VictoryPoints"", 
              ""totalControlAmount"": 1
            }
          ],
          ""routes"": [
            { ""from"": 1, ""to"": 2 }
          ]
        }";

    [TestMethod]
    public void LoadFromData_CreatesCorrectNodesAndConnections()
    {
      // ARRANGE & ACT
      // Call the actual internal method that takes JSON data directly.
      var (nodes, sites, _) = MapFactory.LoadFromData(MockMapJson);

      // ASSERT - Nodes
      Assert.HasCount(3, nodes, "Should load all 3 nodes from the JSON.");
      var node1 = nodes.FirstOrDefault(n => n.Id == 1);
      var node2 = nodes.FirstOrDefault(n => n.Id == 2);
      Assert.IsNotNull(node1);
      Assert.IsNotNull(node2);
      Assert.AreEqual(new Vector2(10, 10), node1.Position);

      // ASSERT - Routes (Connections)
      Assert.Contains(node2, node1.Neighbors, "Node 1 should be connected to Node 2.");
      Assert.Contains(node1, node2.Neighbors, "Connections should be two-way.");
      Assert.HasCount(1, node1.Neighbors, "Node 1 should only have one neighbor.");
    }

    [TestMethod]
    public void LoadFromData_CreatesCorrectSitesAndAssignsNodes()
    {
      // ARRANGE & ACT
      var (nodes, sites, _) = MapFactory.LoadFromData(MockMapJson);

      // ASSERT - Sites
      Assert.HasCount(1, sites, "Should load the single site from JSON.");
      var site = sites[0];
      Assert.AreEqual("Test Site", site.Name);
      Assert.IsTrue(site.IsCity);
      Assert.AreEqual(ResourceType.Power, site.ControlResource);
      Assert.AreEqual(1, site.ControlAmount);
      Assert.AreEqual(ResourceType.VictoryPoints, site.TotalControlResource);
      Assert.AreEqual(1, site.TotalControlAmount);

      // ASSERT - Site Node Membership
      var node2 = nodes.FirstOrDefault(n => n.Id == 2);
      var node3 = nodes.FirstOrDefault(n => n.Id == 3);
      Assert.IsNotNull(node2);
      Assert.IsNotNull(node3);

      // Changed 'site.Nodes' to 'site.NodesInternal'
      Assert.HasCount(2, site.NodesInternal, "Site should contain 2 nodes.");
      Assert.Contains(node2, site.NodesInternal, "Site should contain Node 2.");
      Assert.Contains(node3, site.NodesInternal, "Site should contain Node 3.");
    }
    [TestMethod]
    public void LoadFromStream_ParsesDataCorrectly()
    {
      // Arrange
      using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(MockMapJson)))
      {
        // Act
        var (nodes, sites, _) = MapFactory.LoadFromStream(stream);

        // Assert
        Assert.HasCount(3, nodes);
        Assert.HasCount(1, sites);
      }
    }

    [TestMethod]
    public void LoadFromStream_ReturnsTestMap_OnError()
    {
      // Arrange - Invalid JSON
      var invalidJson = "{ invalid_json }";
      using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson)))
      {
        // Act
        var (nodes, sites, _) = MapFactory.LoadFromStream(stream);

        // Assert - Should return Test Map (3 nodes, no sites)
        Assert.HasCount(25, nodes);
        Assert.HasCount(5, sites);
      }
    }

    [TestMethod]
    public void CreateTestMap_ReturnsValidDefaultMap()
    {
      // Act
      var (nodes, sites, _) = MapFactory.CreateTestMap();

      // Assert
      Assert.HasCount(25, nodes);
      Assert.HasCount(5, sites);

      var n1 = nodes.FirstOrDefault(n => n.Id == 1);
      var n2 = nodes.FirstOrDefault(n => n.Id == 2);

      Assert.IsNotNull(n1);
      Assert.IsNotNull(n2);
      Assert.Contains(n2, n1.Neighbors);
    }
  }
}

