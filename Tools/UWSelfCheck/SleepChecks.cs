using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE NIGHT (player_sleep, SKILLS.C; 2026-10-15, reference audit round 2): what an undisturbed
    /// night does in which order, what passing out does on water and lava, how long the night's
    /// black lasts.
    /// </summary>
    public static partial class Program
    {
        /// <summary>A sleep host without a scene: logs what the night asks of it.</summary>
        private sealed class LoggingSleepHost : IUWSleepHost
        {
            public readonly List<string> Log = new List<string>();

            public bool InWater;

            public bool OnLava;

            public int Damage;

            public float NightBlack = -1f;

            public bool CanSleepHere => true;

            public int CollectCreatures(int piTileRadius) { return 0; }

            public UWCritterRecord CreatureRecord(int piIndex) { return null; }

            public int CreatureTravelRange(int piIndex) { return 0; }

            public bool TryGetCreaturePath(int piIndex, UWTilePos pOTo, int piToHeight, List<UWTilePos> pOPath) { return false; }

            public bool PlaceCreature(int piIndex, UWTilePos pOTile, int piSubX, int piSubY) { return false; }

            public UWTile.TileTypeEnum TileTypeAt(UWTilePos pOTile) { return UWTile.TileTypeEnum.open; }

            public UWTilePos PlayerTile => new UWTilePos(32, 32);

            public int PlayerFloorLevel => 0;

            public void PlaySleepTheme() { Log.Add("music"); }

            public void RunRespawner() { Log.Add("respawner"); }

            public void LetTimePassForCreatures() { Log.Add("creatures"); }

            public void BurnCarriedLights(int piTicks) { }

            public void AddGeneralMessage(int piIndex) { }

            public bool TryPlayDream(int piCutscene) { Log.Add("dream"); return true; }

            public void ShowNightBlack(float pfSeconds) { NightBlack = pfSeconds; Log.Add("black"); }

            public bool IsInWater => InWater;

            public bool IsOnLava => OnLava;

            public void DamagePlayer(int piDamage) { Damage += piDamage; Log.Add("damage"); }

            public void CloseDoorsOnLevel(int piChanceInTen) { Log.Add("doors"); }

            public int CollectGarbage(int piRange, int piLimit) { Log.Add("cull " + piRange + "/" + piLimit); return 0; }

            public bool IsOnGround => true;
        }

        private static void fCheckNight()
        {
            Check.Group("The night (player_sleep)");

            bool lbBuried = UWGameFlags.GaramonBuried;

            // No dream: Garamon is buried.
            UWGameFlags.GaramonBuried = true;

            try
            {
                LoggingSleepHost lOHost = new LoggingSleepHost();

                UWSleepRules.Sleep(fNewSleeper(), 1, lOHost, false, true);

                // SKILLS.C: update_all_critters_whilst_player_snoozes, then DoWanderingMonsters(0).
                int liCreatures = lOHost.Log.IndexOf("creatures");

                fExpectBool("night: time passes for the creatures on an undisturbed night", liCreatures >= 0, true);
                fExpectBool("night: ... before the wandering monsters", liCreatures >= 0 && liCreatures < lOHost.Log.IndexOf("respawner"), true);

                // Obj_GarbageCollect(1, 0x14) right after DoClosingDoors(0).
                fExpectInt("night: far objects culled with range 1, twenty at most, after the doors",
                    lOHost.Log.IndexOf("cull 1/20"), lOHost.Log.IndexOf("doors") + 1);

                // dream: 0x180 ticks of the 1/256-second clock when no cutscene plays.
                fExpectBool("night: no dream holds 1.5 s of black", Math.Abs(lOHost.NightBlack - 1.5f) < 0.001f, true);

                // drop_drunk_player: 255 when the motion state swims (water or shore) or is lava.
                lOHost = new LoggingSleepHost { OnLava = true };
                UWSleepRules.Sleep(fNewSleeper(), 1, lOHost, true, false);
                fExpectInt("passing out: on lava 255", lOHost.Damage, 255);

                lOHost = new LoggingSleepHost { InWater = true };
                UWSleepRules.Sleep(fNewSleeper(), 1, lOHost, true, false);
                fExpectInt("passing out: swimming (water or its edge) 255", lOHost.Damage, 255);

                lOHost = new LoggingSleepHost();
                UWSleepRules.Sleep(fNewSleeper(), 1, lOHost, true, false);
                fExpectInt("passing out: on dry ground nothing", lOHost.Damage, 0);
                fExpectBool("passing out: no dream and no hold of black", lOHost.Log.Contains("black") || lOHost.Log.Contains("dream"), false);
            }
            finally
            {
                UWGameFlags.GaramonBuried = lbBuried;
            }
        }

        /// <summary>Obj_GarbageCollect(1, 0x14) on level 1 with the lowest rolls: twenty objects go,
        /// none within nine tiles (taxicab) of the player.</summary>
        private static void fCheckNightCulling(UWDataImport.DataImport pOData)
        {
            Check.Group("The night's tidying (Obj_GarbageCollect)");

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null)
            {
                fExpectBool("night culling: level 1 loaded", false, true);
                return;
            }

            UWTilePos lOPlayer = new UWTilePos(32, 2);
            UWWorldEdit lOEdit = new UWWorldEdit(new UWTileQueries(lOLevel, pOData)) { PlayerTile = () => lOPlayer };
            int liNearBefore = fCountObjectsNear(lOLevel, lOPlayer, 9);

            // The group's seeded generator: the result is the same on every run.
            int liCulled = lOEdit.CollectGarbage(UWSleepRules.NightCullRange, UWSleepRules.NightCullLimit);

            fExpectBool("night culling: some objects went, twenty at most (" + liCulled + ")", liCulled > 0 && liCulled <= 20, true);
            fExpectInt("night culling: nothing within nine tiles", fCountObjectsNear(lOLevel, lOPlayer, 9), liNearBefore);
        }

        private static int fCountObjectsNear(UWLevel pOLevel, UWTilePos pOPlayer, int piDistance)
        {
            int liCount = 0;

            for (int liX = 0; liX < 64; liX++)
            {
                for (int liY = 0; liY < 64; liY++)
                {
                    UWTile lOTile = pOLevel.GetTile(liX, liY);

                    if (lOTile != null && lOTile.ObjectsInTile != null
                        && Math.Abs(liX - pOPlayer.X) + Math.Abs(liY - pOPlayer.Y) <= piDistance)
                        liCount += lOTile.ObjectsInTile.Count;
                }
            }

            return liCount;
        }

        private static UWPlayerVitals fNewSleeper()
        {
            UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            return lOVitals;
        }
    }
}
