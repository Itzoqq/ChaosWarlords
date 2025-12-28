using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ChaosWarlords.Source.Entities.Map;
using System.Linq;
using System;

namespace ChaosWarlords.Source.Utilities
{
    public class MapLayoutEngine
    {
        private int _nodeIdCounter = 1;

        public (List<MapNode> Nodes, List<Site> Sites, List<Route> Routes) GenerateMap(MapGenerationConfig config)
        {
            var nodes = new List<MapNode>();
            var sites = new List<Site>();
            var routes = new List<Route>();

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

                sites.Add(site);
            }
        }

        private Site CreateSiteFromConfig(SiteConfig siteConfig)
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

        private void ConnectSiteNodes(List<MapNode> siteNodes)
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

                if (fromSite != null && toSite != null)
                {
                    var route = new Route(fromSite, toSite);

                    // Find closest nodes between the two sites to connect
                    var connection = FindBestConnectionPoints(fromSite, toSite);

                    if (connection.StartNode != null && connection.EndNode != null)
                    {
                        var routeNodes = GenerateRouteNodes(connection.StartNode!, connection.EndNode!, routeConfig.NodeCount);

                        ConnectRouteNodes((connection.StartNode!, connection.EndNode!), routeNodes, nodes, route);
                    }

                    routes.Add(route);
                }
            }
        }

        private void ConnectRouteNodes(
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

        private List<MapNode> GenerateSiteNodes(Vector2 center, int count, bool isCity)
        {
            var results = new List<MapNode>();
            if (count <= 0) return results;

            int maxCols = 3;
            float spacing = 50f;

            int totalRows = (int)Math.Ceiling((double)count / maxCols);
            float totalHeight = (totalRows - 1) * spacing;
            float startY = center.Y - (totalHeight / 2);

            int nodesCreated = 0;
            for (int r = 0; r < totalRows; r++)
            {
                int itemsInThisRow = Math.Min(maxCols, count - nodesCreated);
                float rowWidth = (itemsInThisRow - 1) * spacing;
                float startX = center.X - (rowWidth / 2);
                float y = startY + (r * spacing);

                for (int c = 0; c < itemsInThisRow; c++)
                {
                    float x = startX + (c * spacing);
                    results.Add(new MapNode(_nodeIdCounter++, new Vector2(x, y)));
                    nodesCreated++;
                }
            }

            return results;
        }

        private List<MapNode> GenerateRouteNodes(MapNode start, MapNode end, int count)
        {
            var results = new List<MapNode>();
            if (count <= 0) return results;

            // Linear interpolation
            for (int i = 1; i <= count; i++)
            {
                float t = (float)i / (count + 1);
                Vector2 pos = Vector2.Lerp(start.Position, end.Position, t);
                results.Add(new MapNode(_nodeIdCounter++, pos));
            }

            return results;
        }

        private (MapNode? StartNode, MapNode? EndNode) FindBestConnectionPoints(Site from, Site to)
        {
            // Simple approach: find the pair of nodes (one from each site) with the minimum distance
            MapNode? bestStart = null;
            MapNode? bestEnd = null;
            float minDstSq = float.MaxValue;

            foreach (var n1 in from.NodesInternal)
            {
                foreach (var n2 in to.NodesInternal)
                {
                    float dst = Vector2.DistanceSquared(n1.Position, n2.Position);
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



