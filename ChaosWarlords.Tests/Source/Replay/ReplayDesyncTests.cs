using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaosWarlords.Source.Core.Data.Dtos;
using ChaosWarlords.Source.GameStates;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Entities.Actors;
using ChaosWarlords.Source.Factories;
using ChaosWarlords.Source.Entities.Map;
using System.Collections.Generic;
using System.Linq;
using ChaosWarlords.Source.Core.Interfaces.Services;
using ChaosWarlords.Source.Core.Utilities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Commands;
using ChaosWarlords.Source.Core.Interfaces.State;
using NSubstitute;
using ChaosWarlords.Source.Contexts;

namespace ChaosWarlords.Tests.Source.Replay
{
    [TestClass]
    public class ReplayDesyncTests
    {
        [TestMethod]
        public void Verify_RNG_Consistency_Turn1_Draw()
        {
            // 1. Setup
            int seed = 442000625;
            var logger = Substitute.For<IGameLogger>();
            var cardDb = new MockCardDatabase(); 
            
            var matchFactory = new MatchFactory(cardDb, logger);
            var replayManager = new ReplayManager(logger);
            
            // ---------------------------------------------------------
            // RUN 1: Live Game (Recorded)
            // ---------------------------------------------------------
            cardDb.ReturnReverseOrder = false;
            var worldLive = matchFactory.Build(replayManager, seed);
            var seededRandomLive = (SeededGameRandom)worldLive.GameRandom;
            int countLiveInit = seededRandomLive.CallCount;
            
            var redLive = worldLive.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
            var blueLive = worldLive.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
            
            // Draw Initial Hands
            redLive.DrawCards(5, worldLive.GameRandom);
            blueLive.DrawCards(5, worldLive.GameRandom);
            
            var countLiveInitAfterDraw = seededRandomLive.CallCount;

            // ---------------------------------------------------------
            // RED TURN
            // ---------------------------------------------------------
            // 1. Play Soldier (+1 Power)
            var soldierRed = redLive.Hand.First(c => c.Name == "Soldier");
            var playCmd = new PlayCardCommand(soldierRed);
            replayManager.RecordCommand(playCmd, redLive, 1);
            
            // Execute PlayCard manually (mocking command execution)
            redLive.Hand.Remove(soldierRed);
            redLive.PlayedCards.Add(soldierRed);
            redLive.Power += 1; // Effect
            
            // 2. Deploy
            var cmd1 = new DeployTroopCommand(worldLive.MapManager.NodesInternal.First(n => n.Id == 1), redLive);
            replayManager.RecordCommand(cmd1, redLive, 2);
            worldLive.MapManager.TryDeploy(redLive, cmd1.Node);
            
            redLive.CleanUpTurn();
            redLive.DrawCards(5, worldLive.GameRandom);
            
            int countLiveEndRed = seededRandomLive.CallCount;

            worldLive.TurnManager.EndTurn();
            
            // ---------------------------------------------------------
            // BLUE TURN
            // ---------------------------------------------------------
            var cmd2 = new DeployTroopCommand(worldLive.MapManager.NodesInternal.First(n => n.Id == 7), blueLive);
            replayManager.RecordCommand(cmd2, blueLive, 3);
            worldLive.MapManager.TryDeploy(blueLive, cmd2.Node);
            
            blueLive.CleanUpTurn();
            blueLive.DrawCards(5, worldLive.GameRandom);
            
            int countLiveEndBlue = seededRandomLive.CallCount;

            // Generate Replay JSON
            string replayJson = replayManager.GetRecordingJson();
            replayManager.StartReplay(replayJson);

            // ---------------------------------------------------------
            // RUN 2: Replay (Executed via ReplayManager)
            // ---------------------------------------------------------
            var worldReplay = matchFactory.Build(replayManager, seed);
            var seededRandomReplay = (SeededGameRandom)worldReplay.GameRandom;
            int countReplayInit = seededRandomReplay.CallCount;
            
            var redReplay = worldReplay.TurnManager.Players.First(p => p.Color == PlayerColor.Red);
            var blueReplay = worldReplay.TurnManager.Players.First(p => p.Color == PlayerColor.Blue);
            
            // Replay Initial Draw
            redReplay.DrawCards(5, worldReplay.GameRandom);
            blueReplay.DrawCards(5, worldReplay.GameRandom);
            
            var countReplayInitAfterDraw = seededRandomReplay.CallCount;

            var mockState = Substitute.For<IGameplayState>();
            mockState.TurnManager.Returns(worldReplay.TurnManager);
            mockState.MapManager.Returns(worldReplay.MapManager);
            try
            {
                var ctx = new MatchContext(
                    worldReplay.TurnManager, worldReplay.MapManager, worldReplay.MarketManager, 
                    worldReplay.ActionSystem, cardDb, worldReplay.PlayerStateManager, null, logger, seed);
                mockState.MatchContext.Returns(ctx);
            }
            catch(System.Exception ex)
            {
                Assert.Fail($"MatchContext Error: {ex}");
            }

            // Exec 1 (RED PlayCard)
            var fetchedCmd1 = replayManager.GetNextCommand(mockState);
            Assert.IsNotNull(fetchedCmd1, "Failed to switch PlayCard Command");
            if (fetchedCmd1 is PlayCardCommand playCmdReplay)
            {
                 // Simulate Execution of PlayCard (Manually, since PlayCardCommand.Execute is complex and mocked state might need more args)
                 // But wait! PlayCardCommand.Execute needs PlayerStateManager.
                 // mockState.MatchContext.PlayerStateManager... implementation?
                 // Let's do manual logic for robustness of test setup OR check if Hydrated command has Card.
                 
                 var card = playCmdReplay.Card;
                 Assert.IsNotNull(card, "PlayCardCommand Card is null!");
                 
                 // Apply Logic
                 redReplay.Hand.Remove(card);
                 redReplay.PlayedCards.Add(card);
                 redReplay.Power += 1;
            }

            // Exec 2 (RED Deploy)
            var fetchedCmd2 = replayManager.GetNextCommand(mockState);
            Assert.IsNotNull(fetchedCmd2, "Failed to switch Deploy Command (Red)");
            if (fetchedCmd2 is DeployTroopCommand deployCmd1)
            {
                 worldReplay.MapManager.TryDeploy(deployCmd1.Player!, deployCmd1.Node);
            }
            
            redReplay.CleanUpTurn();
            redReplay.DrawCards(5, worldReplay.GameRandom);
            int countReplayEndRed = seededRandomReplay.CallCount;

            // Switch Turn
            worldReplay.TurnManager.EndTurn();
            
            // Exec 3 (BLUE)
            var fetchedCmd3 = replayManager.GetNextCommand(mockState);
            Assert.IsNotNull(fetchedCmd3, "Failed to switch Deploy Command (Blue)");
             
            if (fetchedCmd3 is DeployTroopCommand deployCmd2)
            {
                 worldReplay.MapManager.TryDeploy(deployCmd2.Player!, deployCmd2.Node);
            }
            blueReplay.CleanUpTurn();
            blueReplay.DrawCards(5, worldReplay.GameRandom);
            
            int countReplayEndBlue = seededRandomReplay.CallCount;
            
            // ---------------------------------------------------------
            // ASSERT
            // ---------------------------------------------------------
            
            Assert.AreEqual(countLiveInit, countReplayInit, "RNG Consumption during Init diverged!");
            Assert.AreEqual(countLiveEndRed, countReplayEndRed, "RNG Consumption during Red Turn diverged!");
            Assert.AreEqual(countLiveEndBlue, countReplayEndBlue, "RNG Consumption during Blue Turn diverged!");
            
            var handLiveRed = redLive.Hand.Select(c => c.Id).ToList();
            var handReplayRed = redReplay.Hand.Select(c => c.Id).ToList();
            Assert.IsTrue(handLiveRed.SequenceEqual(handReplayRed), "Red Hand IDs desynchronized!");

            var handLiveBlue = blueLive.Hand.Select(c => c.Id).ToList();
            var handReplayBlue = blueReplay.Hand.Select(c => c.Id).ToList();
            Assert.IsTrue(handLiveBlue.SequenceEqual(handReplayBlue), "Blue Hand IDs desynchronized!");
        }
    }
    
    public class MockCardDatabase : ChaosWarlords.Source.Core.Interfaces.Data.ICardDatabase
    {
         public bool ReturnReverseOrder = false;

         public System.Collections.Generic.List<ChaosWarlords.Source.Entities.Cards.Card> GetAllMarketCards(IGameRandom? random = null)
         {
             var listData = new System.Collections.Generic.List<ChaosWarlords.Source.Utilities.CardData>();
             listData.Add(new ChaosWarlords.Source.Utilities.CardData { Id="Soldier", Name="Soldier", Description="", Aspect="Neutral", Cost=0, Effects=new()});
             listData.Add(new ChaosWarlords.Source.Utilities.CardData { Id="Noble", Name="Noble", Description="", Aspect="Neutral", Cost=0, Effects=new()});
             
             if (ReturnReverseOrder) listData.Reverse();
             
             // This mimics the JsonCardDatabase logic.
             // If CardDatabase.cs is FIXED (OrderBy), it shouldn't matter what order we return here?
             // WAIT. JsonCardDatabase performs sorting internally. 
             // MockCardDatabase needs to implement GetAllMarketCards. 
             // IF I am testing MatchFactory calling CardDatabase.GetAllMarketCards, 
             // MatchFactory expects CardDatabase to behave nicely.
             // If I want to test IF Sort Fix Works, I should test CardDatabase directly.
             
             // But here I'm testing MatchFactory/System integrity.
             // If my fix in CardDatabase works, then MatchFactory won't see diff.
             
             // Wait, MatchFactory calls GetAllMarketCards.
             // CardDatabase.GetAllMarketCards does the sorting.
             // MockCardDatabase implements GetAllMarketCards but DOES NOT SORT.
             // So this test proves that IF the DB returns different order, desync happens.
             // It does NOT prove that CardDatabase.cs fix works.
             
             // To prove CardDatabase.cs fix works, I'd need to use Real CardDatabase or test CardDatabase isolation.
             // But the User Issue is persistent desync.
             
             var list = new System.Collections.Generic.List<ChaosWarlords.Source.Entities.Cards.Card>();
             foreach(var data in listData)
             {
                 list.Add(CardFactory.CreateFromData(data, random));
             }
             return list;
         }
         public ChaosWarlords.Source.Entities.Cards.Card? GetCardById(string id, IGameRandom? random = null) => null;
         public void Load(System.IO.Stream stream) { }
    }
}
