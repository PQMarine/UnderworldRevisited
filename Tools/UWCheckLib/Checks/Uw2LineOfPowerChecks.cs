using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>A spell host with the second game's player record, for the spells that read or write it.</summary>
        private sealed class FakeSpellHostUw2 : FakeSpellHost, IUWSpellHostUw2
        {
            public FakeSpellHostUw2(FakeWorld pOWorld, UWCastContext pOCast, UWPlayerFile pORecord) : base(pOWorld, pOCast)
            {
                Record = pORecord;
            }

            public UWPlayerFile Record { get; }

            public UWPlayerVitals Vitals => null;

            public bool PlayerFitsAt(UWTilePos pOTile) { return true; }

            public void MovePlayer(int piLevel, UWTilePos pOTile) { Fake.Log.Add("move " + piLevel + " " + pOTile.X + "/" + pOTile.Y); }

            public void SwingView(int piSteps) { Fake.Log.Add("swing " + piSteps); }
        }

        /// <summary>
        /// CUTTING A LINE OF POWER (per user 2026-11-22: Altara's wand in the ice caverns cut the line but showed nothing).
        /// check_Guardian_magic_marker (uw2 SPELLS2.C:820) on a first cut: the spells' sparkle on the marker before it
        /// goes (put_effect(obj, 7, 4, 0, 7, x, y)), the quest bits, the tremor do_sfx(4, 0xF), then the two sounds; a
        /// marker of a line already cut goes without any of it, with "The spell has no noticeable effect."
        /// </summary>
        private static void fCheckUw2LineOfPower(CheckContext pOContext)
        {
            Check.Group("Cutting a line of power (UW2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("line of power", "no second game here: skipped");

                return;
            }

            UWGameSession lOBefore = UWGameSession.Current;

            try
            {
                foreach (bool lbAlready in new[] { false, true })
                {
                    DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);

                    UWGameSession.MakeCurrent(lOImport.Session);

                    // The ice caverns' first level (26, world 3) and its marker: the hidden Guardian signet ring with id bit 13.
                    UWLevel lOLevel = lOImport.Levels[25];
                    UWTilePos lOAt = new UWTilePos(-1, -1);

                    UWAreaWalk.Process(lOLevel, 1, -1, UWAreaWalk.Objects, 0, 0, 63, 63, (pOTile, pOObject) =>
                    {
                        if (pOObject == null || pOObject.ID != UWSpecialSpellRulesUw2.GuardianSignetRing || !pOObject.DoorDirection
                            || !pOObject.IsHidden)
                            return false;

                        lOAt = new UWTilePos(pOObject.TileX, pOObject.TileY);

                        return true;
                    });

                    if (lOAt.X < 0)
                    {
                        Check.Bool("line of power: level 26 holds a marker", false, true);

                        return;
                    }

                    UWPlayerFile lOFile = UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck" });

                    if (lbAlready)
                        UWPlayerRecordUw2.SetQuest(lOFile, UWPlayerRecordUw2.LinesOfPowerQuest, 1 << 2);

                    FakeWorld lOWorld = new FakeWorld { Level = lOLevel, RemovesFromTiles = true };

                    lOWorld.Body.Floor = new UWSpot(lOAt, 3, 3, 0);

                    FakeSpellHostUw2 lOHost = new FakeSpellHostUw2(lOWorld, UWCastContext.ThePlayer, lOFile) { Data = lOImport, PlayerLevel = 26 };
                    bool lbTook = UWSpecialSpellRulesUw2.Cast(UWSpecialSpellRulesUw2.LinesOfPowerSpell, lOHost);
                    List<string> lOAll = new List<string>(lOWorld.Log);

                    lOAll.AddRange(lOWorld.Shown.Log);

                    string lsLog = string.Join(", ", lOAll);
                    string lsEffect = "effect " + UWAreaSpellRules.SpellEffectObjectId;
                    int liLines = UWPlayerRecordUw2.GetQuest(lOFile, UWPlayerRecordUw2.LinesOfPowerQuest);

                    if (!lbAlready)
                        Check.Bool("line of power: a first cut sparkles on the marker (" + lOAt.X + "/" + lOAt.Y + "), takes it, shakes the view and sounds (" + lsLog + ")",
                            lbTook && lsLog.Contains(lsEffect) && lsLog.IndexOf(lsEffect) < lsLog.IndexOf("remove " + UWSpecialSpellRulesUw2.GuardianSignetRing)
                            && lsLog.Contains("shake " + UWSpecialSpellRulesUw2.CutShake) && lsLog.Contains("sound " + UWSpecialSpellRulesUw2.CutSoundA)
                            && liLines == (1 << 2), true);
                    else
                        Check.Bool("line of power: a line already cut takes the marker without sparkle, shake or sounds (" + lsLog + ")",
                            lbTook && lsLog.Contains("remove " + UWSpecialSpellRulesUw2.GuardianSignetRing) && !lsLog.Contains(lsEffect)
                            && !lsLog.Contains("shake") && !lsLog.Contains("sound ") && lsLog.Contains("general " + UWExtraSpellRulesUw2.NoEffectString), true);
                }

                // EVERY WORLD'S MARKER (per user 2026-11-22: Miranda named only the Prison Tower, the cuts in the ice
                // caverns and on Talorus were made too): cast from one tile west and two north of it, as the user's
                // SAVE3 stood at 57/6 beside Talorus' 58/4, the world's bit goes into quest 128.
                List<string> lOWorlds = new List<string>();
                DataImport lOAll2 = new DataImport(lsData, null, null, UWGameProfiles.Uw2);

                UWGameSession.MakeCurrent(lOAll2.Session);

                for (int liLevel = 0; liLevel < lOAll2.Levels.Count; liLevel++)
                {
                    UWLevel lOLevel = lOAll2.Levels[liLevel];
                    UWTilePos lOAt = new UWTilePos(-1, -1);

                    if (lOLevel == null || lOLevel.TileData == null)
                        continue;

                    UWAreaWalk.Process(lOLevel, 1, -1, UWAreaWalk.Objects, 0, 0, 63, 63, (pOTile, pOObject) =>
                    {
                        if (pOObject == null || pOObject.ID != UWSpecialSpellRulesUw2.GuardianSignetRing || !pOObject.DoorDirection
                            || !pOObject.IsHidden)
                            return false;

                        lOAt = new UWTilePos(pOObject.TileX, pOObject.TileY);

                        return true;
                    });

                    if (lOAt.X < 0)
                        continue;

                    UWPlayerFile lOFile = UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck" });
                    FakeWorld lOWorld = new FakeWorld { Level = lOLevel, RemovesFromTiles = true };

                    lOWorld.Body.Floor = new UWSpot(new UWTilePos(lOAt.X - 1, lOAt.Y + 2), 3, 3, 0);

                    FakeSpellHostUw2 lOHost = new FakeSpellHostUw2(lOWorld, UWCastContext.ThePlayer, lOFile) { Data = lOAll2, PlayerLevel = liLevel + 1 };

                    UWSpecialSpellRulesUw2.Cast(UWSpecialSpellRulesUw2.LinesOfPowerSpell, lOHost);
                    lOWorlds.Add((liLevel + 1) + ":" + UWPlayerRecordUw2.GetQuest(lOFile, UWPlayerRecordUw2.LinesOfPowerQuest));
                }

                Check.Str("line of power: each world's marker cut from beside it sets its own bit (level:quest 128)", string.Join(" ", lOWorlds),
                    "16:1 18:2 26:4 33:8 48:16 49:32 57:64 69:128");
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }
    }
}
