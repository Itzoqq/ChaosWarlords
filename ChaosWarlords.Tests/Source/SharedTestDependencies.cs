using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.States;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests
{
    // ------------------------------------------------------------------------
    // SHARED MOCKS
    // These are public so they can be used by GameplayStateTests.cs 
    // and other integration-style tests.
    // ------------------------------------------------------------------------

    public class MockActionSystem : IActionSystem
    {
        public ActionState CurrentState { get; set; } = ActionState.Normal;
        public Card? PendingCard { get; set; }
        public Site? PendingSite { get; set; }

        public event EventHandler? OnActionCompleted;
        public event EventHandler<string>? OnActionFailed;

        // Helper to simulate events in tests
        public void SimulateActionCompleted() => OnActionCompleted?.Invoke(this, EventArgs.Empty);
        public void SimulateActionFailed(string reason) => OnActionFailed?.Invoke(this, reason);

        // Interface Implementation
        public void CancelTargeting() { CurrentState = ActionState.Normal; }
        public void FinalizeSpyReturn(PlayerColor spyColor) { }
        public void HandleTargetClick(MapNode node, Site site) { }
        public bool IsTargeting() => CurrentState != ActionState.Normal;
        public void SetCurrentPlayer(Player p) { }
        public void StartTargeting(ActionState state, Card card) { CurrentState = state; }
        public void TryStartAssassinate() { }
        public void TryStartReturnSpy() { }
    }

    public class MockMapManager : IMapManager
    {
        public IReadOnlyList<MapNode> Nodes { get; } = new List<MapNode>();
        public IReadOnlyList<Site> Sites { get; } = new List<Site>();

        public void CenterMap(int width, int height) { }
        public void DistributeControlRewards(Player activePlayer) { }
        public List<PlayerColor> GetEnemySpiesAtSite(Site site, Player activePlayer) => new List<PlayerColor>();
        public MapNode GetNodeAt(Vector2 position) => null!;
        public Site GetSiteAt(Vector2 position) => null!;
        public Site GetSiteForNode(MapNode node) => null!;
        public bool TryDeploy(Player currentPlayer, MapNode targetNode) => true;
    }

    public class MockMarketManager : IMarketManager
    {
        public List<Card> MarketRow { get; } = new List<Card>();
        public void BuyCard(Player p, Card c) { }
        public void RefillMarket(List<Card> deck) { }
        public bool TryBuyCard(Player player, Card card) => true;
        public void Update(Vector2 mousePos) { }
    }
}