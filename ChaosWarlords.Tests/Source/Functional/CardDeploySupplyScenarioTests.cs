using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Contexts;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;

namespace ChaosWarlords.Tests.Source.Functional
{
    [TestClass]
    [TestCategory("Integration")]
    public class CardDeploySupplyScenarioTests
    {
        private static MatchScenario BuildPlayingScenario()
        {
            var scenario = MatchScenario.Build();
            scenario.Context.CurrentPhase = MatchPhase.Playing;
            scenario.Context.MapManager.SetPhase(MatchPhase.Playing);
            return scenario;
        }

        private static Site Fortress(MatchScenario scenario) =>
            scenario.Context.MapManager.Sites.First(s => s.Name == "Obsidian Fortress");

        private static Card GrantTwoDeploys(MatchScenario scenario)
        {
            var card = scenario.GiveCard(scenario.Context.ActivePlayer.Color, "skeletal_horde");
            scenario.PlayCard(card);
            scenario.RespondToLatestInteraction(accept: false);
            return card;
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void DeployTroop_CardCredits_ConsumeSupplyThenAwardVP(int supply)
        {
            var scenario = BuildPlayingScenario();
            var player = scenario.Context.ActivePlayer;
            player.TroopsInBarracks = supply;
            int initialVP = player.VictoryPoints;
            int initialPower = player.Power;
            var nodes = Fortress(scenario).NodesInternal;
            GrantTwoDeploys(scenario);
            Assert.AreEqual(supply, player.TroopsInBarracks, "Granting a credit must not reserve a troop.");

            for (int i = 0; i < 2; i++)
            {
                var hydrated = CommandHydrator.HydrateCommand(new DeployTroopCommand(nodes[i]).ToDto(), scenario.Context);
                Assert.IsNotNull(hydrated);
                scenario.Dispatch(hydrated);
                Assert.AreEqual(Math.Max(0, supply - i - 1), player.TroopsInBarracks);
                Assert.AreEqual(1 - i, player.PendingFreeTroops);
                Assert.AreEqual(i < supply ? player.Color : PlayerColor.None, nodes[i].Occupant);
            }

            Assert.AreEqual(initialVP + 2 - supply, player.VictoryPoints);
            Assert.AreEqual(initialPower, player.Power);
        }

        [TestMethod]
        public void DeployTroop_InvalidAndRepeatedTargets_DoNotSpendExtraCreditsOrSupply()
        {
            var scenario = BuildPlayingScenario();
            var player = scenario.Context.ActivePlayer;
            player.TroopsInBarracks = 3;
            GrantTwoDeploys(scenario);
            var node = Fortress(scenario).NodesInternal[0];

            scenario.AssertRejected(new DeployTroopCommand(int.MaxValue));
            Assert.AreEqual(2, player.PendingFreeTroops);
            scenario.DispatchTwice(new DeployTroopCommand(node));

            Assert.AreEqual(2, player.TroopsInBarracks);
            Assert.AreEqual(1, player.PendingFreeTroops);
            Assert.AreEqual(player.Color, node.Occupant);
        }

        [TestMethod]
        public void TryDeploy_NoCreditAndNoPower_DoesNotMutateSupplyOrVP()
        {
            var scenario = BuildPlayingScenario();
            var player = scenario.Context.ActivePlayer;
            player.TroopsInBarracks = 0;
            player.SetPower(0);
            var node = Fortress(scenario).NodesInternal[0];
            int initialVP = player.VictoryPoints;

            Assert.IsFalse(scenario.Context.MapManager.TryDeploy(player, node));

            Assert.AreEqual(initialVP, player.VictoryPoints);
            Assert.AreEqual(0, player.TroopsInBarracks);
            Assert.AreEqual(PlayerColor.None, node.Occupant);
        }

        [TestMethod]
        public void CancelTargeting_AfterImmediateDeploy_RestoresSupplyCreditsAndBoard()
        {
            var scenario = BuildPlayingScenario();
            var player = scenario.Context.ActivePlayer;
            player.TroopsInBarracks = 3;
            player.PendingFreeTroops = 2;
            var node = Fortress(scenario).NodesInternal[0];
            var card = scenario.GiveCard(player.Color, "gibbering_mouther");
            scenario.PlayCard(card);
            scenario.ClickTarget(node, null);
            Assert.AreEqual(2, player.TroopsInBarracks);

            scenario.Context.ActionSystem.CancelTargeting();

            Assert.AreEqual(3, player.TroopsInBarracks);
            Assert.AreEqual(2, player.PendingFreeTroops);
            Assert.AreEqual(PlayerColor.None, scenario.Context.MapManager.GetNodeById(node.Id)!.Occupant);
            Assert.IsEmpty(scenario.Context.ActionSystem.PendingDeployedNodes);
            Assert.IsTrue(player.Hand.Any(c => c.RuntimeId == card.RuntimeId));
        }

        [TestMethod]
        public void PlaySkeletalHorde_WrongPlayer_DoesNotGrantCreditsOrReserveSupply()
        {
            var scenario = BuildPlayingScenario();
            var player = scenario.Context.ActivePlayer;
            var other = scenario.Context.TurnManager.Players.First(p => p != player);
            var card = scenario.GiveCard(other.Color, "skeletal_horde");
            int supply = other.TroopsInBarracks;

            scenario.AssertRejected(new PlayCardCommand(card));

            Assert.AreEqual(0, player.PendingFreeTroops);
            Assert.AreEqual(0, other.PendingFreeTroops);
            Assert.AreEqual(supply, other.TroopsInBarracks);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void PlayGibberingMouther_InsufficientSupply_TracksOnlyActualDeployments(int supply)
        {
            var scenario = BuildPlayingScenario();
            var player = scenario.Context.ActivePlayer;
            var other = scenario.Context.TurnManager.Players.First(p => p != player);
            var nodes = Fortress(scenario).NodesInternal;
            nodes[2].Occupant = other.Color;
            player.TroopsInBarracks = supply;
            player.PendingFreeTroops = 3;
            int initialVP = player.VictoryPoints;
            var card = scenario.GiveCard(player.Color, "gibbering_mouther");
            scenario.PlayCard(card);

            scenario.ClickTarget(nodes[0], null);
            Assert.HasCount(supply, scenario.Context.ActionSystem.PendingDeployedNodes);
            scenario.ClickTarget(nodes[1], null);

            Assert.AreEqual(0, player.TroopsInBarracks);
            Assert.AreEqual(initialVP + 2 - supply, player.VictoryPoints);
            Assert.AreEqual(3, player.PendingFreeTroops, "The immediate effect must preserve pre-existing credits.");
            Assert.AreEqual(PlayerColor.None, nodes[1].Occupant);
            if (supply == 0)
            {
                Assert.AreEqual(ActionState.Normal, scenario.Context.ActionSystem.CurrentState);
                Assert.IsEmpty(scenario.Context.ActionSystem.PendingDeployedNodes);
                scenario.AssertRejected(new SelectOpponentCommand(other.Color));
            }
            else
            {
                Assert.AreEqual(ActionState.TargetingOpponentSelect, scenario.Context.ActionSystem.CurrentState);
                Assert.HasCount(1, scenario.Context.ActionSystem.PendingDeployedNodes);
                scenario.Dispatch(new SelectOpponentCommand(other.Color));
                Assert.IsTrue(other.DiscardPile.Any(c => c.DefinitionId == "insane_outcast"));
            }
            scenario.AssertRejected(new DeployTroopCommand(nodes[1], card.Id));
            Assert.AreEqual(initialVP + 2 - supply, player.VictoryPoints);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DeployTroop_LastBarracksTroop_EndsGameOnlyAfterRemainingSeatsFinish(bool immediate)
        {
            var scenario = BuildPlayingScenario();
            var first = scenario.Context.TurnManager.Players[0];
            scenario.AsActivePlayer(first.Color);
            first.TroopsInBarracks = immediate ? 2 : 1;
            var nodes = Fortress(scenario).NodesInternal;
            if (immediate)
            {
                scenario.PlayCard(scenario.GiveCard(first.Color, "gibbering_mouther"));
                scenario.ClickTarget(nodes[0], null);
                scenario.ClickTarget(nodes[1], null);
            }
            else
            {
                GrantTwoDeploys(scenario);
                scenario.Dispatch(new DeployTroopCommand(nodes[0]));
            }

            Assert.AreEqual(0, first.TroopsInBarracks);
            Assert.IsFalse(scenario.Context.MatchManager.IsGameOver());
            scenario.Dispatch(new EndTurnCommand());
            Assert.IsFalse(scenario.Context.MatchManager.IsGameOver(), "The second seat must get its final turn.");
            scenario.Dispatch(new EndTurnCommand());
            Assert.IsTrue(scenario.Context.MatchManager.IsGameOver());
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void PlayOgreZombie_WithDeployCredits_PreservesCreditsAndUsesRealSupply(int supply)
        {
            var scenario = BuildPlayingScenario();
            var player = scenario.Context.ActivePlayer;
            player.TroopsInBarracks = supply;
            GrantTwoDeploys(scenario);
            int initialVP = player.VictoryPoints;
            int initialTrophies = player.TrophyHall;
            var target = scenario.Context.MapManager.Nodes.First(n => n.Occupant == PlayerColor.Neutral);

            scenario.PlayCard(scenario.GiveCard(player.Color, "ogre_zombie"));
            scenario.ClickTarget(target, null);

            Assert.AreEqual(0, player.TroopsInBarracks);
            Assert.AreEqual(2, player.PendingFreeTroops);
            Assert.AreEqual(initialTrophies + 1, player.TrophyHall);
            Assert.AreEqual(initialVP + 1 - supply, player.VictoryPoints);
            Assert.AreEqual(supply == 0 ? PlayerColor.None : player.Color, target.Occupant);
        }
    }
}
