﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using datx02_rally.GameLogic;
using Microsoft.Xna.Framework;
using datx02_rally.Menus;
using datx02_rally.EventTrigger;

namespace datx02_rally.GameplayModes
{
    class MultiplayerRaceMode : SimpleRaceMode
    {
        public MultiplayerRaceMode(Game1 gameInstance, int laps, int noOfCheckpoints, RaceTrack raceTrack, Car localCar)
            : base(gameInstance, laps, noOfCheckpoints, raceTrack, localCar)
        {
            this.Mode = Mode.Multiplayer;
            players.AddRange(gameInstance.GetService<ServerClient>().Players.Values);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void PrepareStatistics()
        {
            base.PrepareStatistics();
        }

        public override void Update(GameTime gameTime, GamePlayView gamePlay)
        {
            if (TotalRaceTime != TimeSpan.Zero && allStatesFinished)
                gameInstance.GetService<ServerClient>().SendRaceTime(TotalRaceTime);
            if (Statistics != null) {
                var playerHeading = Statistics.CategorizedItems.First(h => h.Updateable);
                playerHeading.Items.Clear();
                int place = 1;
                foreach (var player in players.OrderBy(p => p.RaceTime))
                    playerHeading.Items[place++ + ". " + player.PlayerName] = player.RaceTime == TimeSpan.MaxValue ? "" : player.RaceTime.ToString(@"m\:ss\:ff");
            }
            base.Update(gameTime, gamePlay);
        }
    }
}
