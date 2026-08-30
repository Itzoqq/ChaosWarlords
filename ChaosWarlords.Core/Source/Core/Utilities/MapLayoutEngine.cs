using ChaosWarlords.Source.Entities.Map;

namespace ChaosWarlords.Source.Utilities
{
    public class MapLayoutEngine
    {
        private int _nodeIdCounter = 1;

        public (List<MapNode> Nodes, List<Site> Sites, List<Route> Routes) GenerateMap(MapGenerationConfig config)
        {
            List<MapNode> nodes = [];
            List<Site> sites = [];
            List<Route> routes = [];

            GenerateSites(config, nodes, sites);
            GenerateRoutes(config, nodes, sites, routes);

            return (nodes, sites, routes);
        }

        private void GenerateSites(MapGenerationConfig config, List<MapNode> nodes, List<Site> sites)
        {
            foreach (var siteConfig in config.Sites)
            {
                // Create Site Object
                Site site = CreateSiteFromConfig(siteConfig);
                site.EndGameVictoryPoints = siteConfig.EndGameVP;

                // Generate nodes for the site
                var siteNodes = GenerateSiteNodes(siteConfig.Position, siteConfig.NodeCount, siteConfig.IsCity);

                // Interconnect all site nodes (Fully Connected Mesh)
                ConnectSiteNodes(siteNodes);

                foreach (var node in siteNodes)
                {
                    site.AddNode(node);
                    nodes.Add(node);
                }

                site.Id = sites.Count + 1;
                sites.Add(site);
            }
        }

        private static Site CreateSiteFromConfig(SiteConfig siteConfig)
        {
            if (siteConfig.IsCity)
            {
                return new CitySite(siteConfig.Name, siteConfig.ControlResource, siteConfig.ControlAmount, siteConfig.TotalControlResource, siteConfig.TotalControlAmount);
            }
            else if (siteConfig.IsStartingSite)
            {
                return new StartingSite(siteConfig.Name, siteConfig.ControlResource, siteConfig.ControlAmount, siteConfig.TotalControlResource, siteConfig.TotalControlAmount);
            }
            else
            {
                return new NonCitySite(siteConfig.Name, siteConfig.ControlResource, siteConfig.ControlAmount, siteConfig.TotalControlResource, siteConfig.TotalControlAmount);
            }
        }

        private static void ConnectSiteNodes(List<MapNode> siteNodes)
        {
            for (int i = 0; i < siteNodes.Count; i++)
            {
                for (int j = i + 1; j < siteNodes.Count; j++)
                {
                    siteNodes[i].AddNeighbor(siteNodes[j]);
                    siteNodes[j].AddNeighbor(siteNodes[i]);
                }
            }
        }

        private void GenerateRoutes(MapGenerationConfig config, List<MapNode> nodes, List<Site> sites, List<Route> routes)
        {
            foreach (var routeConfig in config.Routes)
            {
                var fromSite = sites.FirstOrDefault(s => s.Name == routeConfig.FromSiteName);
                var toSite = sites.FirstOrDefault(s => s.Name == routeConfig.ToSiteName);

                if (fromSite is not null && toSite is not null)
                {
                    var route = new Route(fromSite, toSite);

                    // Find closest nodes between the two sites to connect
                    var connection = FindBestConnectionPoints(fromSite, toSite);

                    if (connection.StartNode is not null && connection.EndNode is not null)
                    {
                        var routeNodes = GenerateRouteNodes(connection.StartNode!, connection.EndNode!, routeConfig.NodeCount);

                        ConnectRouteNodes((connection.StartNode!, connection.EndNode!), routeNodes, nodes, route);
                    }

                    routes.Add(route);
                }
            }
        }

        private static void ConnectRouteNodes(
            (MapNode StartNode, MapNode EndNode) connection,
            List<MapNode> routeNodes,
            List<MapNode> allNodes,
            Route route)
        {
            // Link start node to first route node
            if (routeNodes.Count > 0)
            {
                connection.StartNode.AddNeighbor(routeNodes[0]);
            }
            else
            {
                // Direct connection if no route nodes
                connection.StartNode.AddNeighbor(connection.EndNode);
            }

            // Link intermediate route nodes
            for (int i = 0; i < routeNodes.Count; i++)
            {
                allNodes.Add(routeNodes[i]);
                route.AddNode(routeNodes[i]);

                if (i < routeNodes.Count - 1)
                {
                    routeNodes[i].AddNeighbor(routeNodes[i + 1]);
                }
            }

            // Link last route node to end node
            if (routeNodes.Count > 0)
            {
                routeNodes[routeNodes.Count - 1].AddNeighbor(connection.EndNode);
            }
        }

        private List<MapNode> GenerateSiteNodes(Core.Data.LogicVector2 center, int count, bool isCity)
        {
            List<MapNode> results = [];
            if (count <= 0) return results;

            int maxCols = 3;
            // Spacing 50f -> 50000 in Logic Space
            int spacing = 50 * Core.Data.LogicVector2.ScaleFactor;

            int totalRows = (int)Math.Ceiling((double)count / maxCols);
            int totalHeight = (totalRows - 1) * spacing;
            int startY = center.Y - (totalHeight / 2);

            int nodesCreated = 0;
            for (int r = 0; r < totalRows; r++)
            {
                int itemsInThisRow = Math.Min(maxCols, count - nodesCreated);
                int rowWidth = (itemsInThisRow - 1) * spacing;
                int startX = center.X - (rowWidth / 2);
                int y = startY + (r * spacing);

                for (int c = 0; c < itemsInThisRow; c++)
                {
                    int x = startX + (c * spacing);
                    results.Add(new MapNode(_nodeIdCounter++, new Core.Data.LogicVector2(x, y)));
                    nodesCreated++;
                }
            }

            return results;
        }

        private List<MapNode> GenerateRouteNodes(MapNode start, MapNode end, int count)
        {
            List<MapNode> results = [];
            if (count <= 0) return results;

            // Linear interpolation
            for (int i = 1; i <= count; i++)
            {
                // Lerp using deterministic math
                var pos = Core.Data.LogicVector2.Lerp(start.LogicPosition, end.LogicPosition, i, count + 1);
                results.Add(new MapNode(_nodeIdCounter++, pos));
            }

            return results;
        }

        private static (MapNode? StartNode, MapNode? EndNode) FindBestConnectionPoints(Site from, Site to)
        {
            // Simple approach: find the pair of nodes (one from each site) with the minimum distance
            MapNode? bestStart = null;
            MapNode? bestEnd = null;
            long minDstSq = long.MaxValue;

            foreach (var n1 in from.NodesInternal)
            {
                foreach (var n2 in to.NodesInternal)
                {
                    long dst = Core.Data.LogicVector2.DistanceSquared(n1.LogicPosition, n2.LogicPosition);
                    if (dst < minDstSq)
                    {
                        minDstSq = dst;
                        bestStart = n1;
                        bestEnd = n2;
                    }
                }
            }

            return (bestStart, bestEnd);
        }
    }
}



