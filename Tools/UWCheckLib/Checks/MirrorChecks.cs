using System.Globalization;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The mirror hygiene of cleanup phase 10 (wave 3, 2026-10-26): the engine-free core as the
    /// original's integers, tables and tick rates, so that a C++ mirror computes the same. Each
    /// group records a set of situations; a change that moves one of them says why in its commit.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// HIT POINTS AND MANA THROUGH THE SITUATIONS OF A GAME: damage, healing over time, the
        /// regeneration ticks, mana spent and regained, a night, a potion, a conversation's values,
        /// Tybal's orb, and several save and load cycles through PLAYER.DAT. Recorded on the float
        /// vitals (cleanup 10.26) before they became the original's integers (PLAYER.DAT 0x35-0x38
        /// are bytes; 10.27), and the integers give the same numbers: nothing in the game ever made
        /// a fraction.
        /// </summary>
        private static void fCheckIntegerVitals(UWDataImport.DataImport pOData)
        {
            Check.Group("Vitals as the original's integers (cleanup phase 10, wave 3)");

            // Damage: a plain hit, a blow on Easy (halved), a curse down to its floor, a drain.
            UWPlayerVitals lOVitals = fMirrorVitals(new FakeVitalsHost(), 30, 20, -1, -1, false);

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Plain(7, UWDamageTypes.None));
            Check.Str("situations: a plain hit of 7", fVitalsLine(lOVitals), "23/30 20/20");
            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Fall(5));
            Check.Str("situations: a fall of 5", fVitalsLine(lOVitals), "18/30 20/20");
            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Curse(8));
            Check.Str("situations: a curse of eight dice stops at three", fVitalsLine(lOVitals), "3/30 20/20");
            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.VoidDrain(5));
            Check.Str("situations: the Void's drain stops at zero", fVitalsLine(lOVitals), "0/30 20/20");

            UWPlayerVitals lOEasy = fMirrorVitals(new FakeVitalsHost(), 30, 20, -1, -1, true);

            UWDamagePipeline.Apply(lOEasy, UWDamageRequest.CreatureBlow(9, 0, false));
            UWDamagePipeline.Apply(lOEasy, UWDamageRequest.CreatureBlow(1, 0, false));
            Check.Str("situations: blows of 9 and 1 on Easy, halved", fVitalsLine(lOEasy), "26/30 20/20");

            // Healing over time: 48 player ticks wounded (two five-minute blocks with the Strength
            // check, sixteen minutes of mana), then a Ring of Regeneration and Mana Regeneration.
            FakeVitalsHost lOHost = new FakeVitalsHost();
            UWPlayerVitals lOHealing = fMirrorVitals(lOHost, 30, 20, 10, 5, false);

            for (int liTick = 0; liTick < 48; liTick++)
                lOHealing.RunGameTick(UWGameClock.UnitsPerTick, 0);

            Check.Str("situations: 48 ticks of healing and mana", fVitalsLine(lOHealing), "10/30 8/20");

            lOHost.Regeneration = UWWornRegeneration.HealthBit | UWWornRegeneration.ManaBit;

            for (int liTick = 0; liTick < 5; liTick++)
                lOHealing.RunGameTick(UWGameClock.UnitsPerTick, 0);

            Check.Str("situations: five ticks with both regeneration rings", fVitalsLine(lOHealing), "15/30 13/20");

            for (int liTick = 0; liTick < 30; liTick++)
                lOHealing.RunGameTick(UWGameClock.UnitsPerTick, 0);

            Check.Str("situations: the rings stop at the maxima", fVitalsLine(lOHealing), "30/30 20/20");

            // Poison over the minutes.
            UWPlayerVitals lOPoisoned = fMirrorVitals(new FakeVitalsHost(), 30, 20, -1, -1, false);

            lOPoisoned.ApplyPoison(4);

            for (int liTick = 0; liTick < 15; liTick++)
                lOPoisoned.RunGameTick(UWGameClock.UnitsPerTick, 0);

            Check.Str("situations: a poison of 4 over fifteen ticks", fVitalsLine(lOPoisoned) + " poison " + lOPoisoned.Poison,
                "20/30 20/20 poison 0");

            // Mana spent and regained: a cast's cost, the mana spells, the mushroom, a night.
            UWPlayerVitals lOMana = fMirrorVitals(new FakeVitalsHost(), 30, 40, 12, 40, false);

            lOMana.SpendMana(27);
            Check.Str("situations: 27 mana spent", fVitalsLine(lOMana), "12/30 13/40");
            lOMana.SpendMana(20);
            Check.Str("situations: more than is left stops at zero", fVitalsLine(lOMana), "12/30 0/40");
            lOMana.RestoreManaFromSpell(3);
            Check.Str("situations: restore mana of subclass 3", fVitalsLine(lOMana), "12/30 13/40");
            lOMana.AddMana(2);
            Check.Str("situations: the mushroom's 2", fVitalsLine(lOMana), "12/30 15/40");
            lOMana.RegenerateOnSleep(2, 1);
            Check.Str("situations: a night (hit points 2, mana 1)", fVitalsLine(lOMana), "20/30 27/40");
            lOMana.RegenerateOnSleep(-1, -1);
            Check.Str("situations: a night without the shares (one point each, six more mana)", fVitalsLine(lOMana), "21/30 34/40");

            // A potion, Greater Heal and the conversation's values.
            UWPlayerVitals lOPotion = fMirrorVitals(new FakeVitalsHost(), 30, 20, 4, 3, false);

            lOPotion.Heal(9);
            Check.Str("situations: a healing potion of 9", fVitalsLine(lOPotion), "13/30 3/20");
            lOPotion.Heal(40);
            Check.Str("situations: more than the maximum stops at it", fVitalsLine(lOPotion), "30/30 3/20");
            UWDamagePipeline.Apply(lOPotion, UWDamageRequest.Plain(20, UWDamageTypes.None));
            lOPotion.ApplyConversationValues(lOPotion.Hunger, 25, 17, lOPotion.Poison);
            Check.Str("situations: a conversation sets 25 and 17", fVitalsLine(lOPotion), "25/30 17/20");
            lOPotion.ApplyConversationValues(lOPotion.Hunger, 99, 99, lOPotion.Poison);
            // Stored as it is, above the maximum, as the original's CONVVARS.C (2026-10-26; it was cut
            // to the maximum until then).
            Check.Str("situations: a conversation's value above the maximum stays", fVitalsLine(lOPotion), "99/30 99/20");
            UWDamagePipeline.Apply(lOPotion, UWDamageRequest.Plain(29, UWDamageTypes.None));
            lOPotion.HealFully();
            Check.Str("situations: Greater Heal (the mana above the maximum stays)", fVitalsLine(lOPotion), "30/30 99/20");

            // A level-up recomputes the maxima and leaves the current values (player_compute).
            UWPlayerVitals.StartValues lOStrong = UWPlayerVitals.StartValues.Default;

            lOStrong.Strength = 23;
            lOStrong.Intelligence = 19;
            lOStrong.ManaSkill = 11;
            lOStrong.Level = 6;
            lOStrong.FallbackMaxHP = 50;
            lOStrong.FallbackCurrentHP = 41;

            UWPlayerVitals lOLevel = new UWPlayerVitals(new FakeVitalsHost { DungeonLevelIndex = 0 });

            lOLevel.Initialise(null, lOStrong);
            lOLevel.SpendMana(5);
            lOLevel.RecomputeMaxima();
            Check.Str("situations: the maxima of a shrine's mantra", fVitalsLine(lOLevel),
                "41/" + (30 + (6 * 23 / 5)) + " " + (UWExperience.GetMaximumMana(11, 19) - 5) + "/" + UWExperience.GetMaximumMana(11, 19));

            // Tybal's orb takes the mana in his lair and gives it back.
            UWPlayerVitals lOOrb = fMirrorVitals(new FakeVitalsHost(), 30, 20, 30, 11, false);

            lOOrb.SetMana(0, 0);
            Check.Str("situations: the orb takes the mana", fVitalsLine(lOOrb), "30/30 0/0");
            lOOrb.SetMana(20, 20);
            Check.Str("situations: and gives it back", fVitalsLine(lOOrb), "30/30 20/20");

            fCheckVitalsSaveCycles(pOData);
        }

        /// <summary>Three save and load cycles through PLAYER.DAT (our writer, then the reader the
        /// game loads with): the numbers between come back as they went in, and a wounded,
        /// half-spent character keeps healing from where it was.</summary>
        private static void fCheckVitalsSaveCycles(UWDataImport.DataImport pOData)
        {
            string lsFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "UWSelfCheck-mirror-" + System.Guid.NewGuid().ToString("N"));

            try
            {
                System.IO.Directory.CreateDirectory(lsFolder);

                byte[] lyPlain = UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter
                {
                    Name = "Selfcheck", Strength = 20, Dexterity = 20, Intelligence = 20, MaxVitality = 34, MaxMana = 18
                });

                System.IO.File.WriteAllBytes(System.IO.Path.Combine(lsFolder, "PLAYER.DAT"), UWPlayerData.Encrypt(lyPlain, 0x2A));

                pOData.ReplaceInitialPlayer(new UWPlayerData(lsFolder));

                FakeVitalsHost lOHost = new FakeVitalsHost();
                UWPlayerVitals lOVitals = new UWPlayerVitals(lOHost);

                lOVitals.Initialise(pOData, UWPlayerVitals.StartValues.Default);

                string lsCycles = fVitalsLine(lOVitals);

                for (int liCycle = 0; liCycle < 3; liCycle++)
                {
                    UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Plain(5 + liCycle, UWDamageTypes.None));
                    lOVitals.SpendMana(4);

                    for (int liTick = 0; liTick < 24; liTick++)
                        lOVitals.RunGameTick(UWGameClock.UnitsPerTick, 0);

                    string lsBefore = fVitalsLine(lOVitals);

                    UWPlayerData.WritePosition(lsFolder, 1, 32 << 8, 2 << 8, 96 * 8, 0, new UWPlayerData.SavedState
                    {
                        Hunger = lOVitals.Hunger,
                        CurrentVitality = lOVitals.CurrentHP,
                        MaxVitality = lOVitals.MaxHP,
                        CurrentMana = lOVitals.CurrentMana,
                        MaxMana = lOVitals.MaxMana,
                        ClockValue = lOVitals.ClockValue
                    });

                    pOData.ReplaceInitialPlayer(new UWPlayerData(lsFolder));
                    lOVitals = new UWPlayerVitals(lOHost);
                    lOVitals.Initialise(pOData, UWPlayerVitals.StartValues.Default);

                    Check.Str("save cycles: cycle " + (liCycle + 1) + " loads what it saved", fVitalsLine(lOVitals), lsBefore);

                    lsCycles += " | " + lsBefore;
                }

                Check.Str("save cycles: three cycles of a hit, a cast and two five-minute blocks", lsCycles,
                    "34/34 18/18 | 30/34 16/18 | 24/34 15/18 | 18/34 14/18");
            }
            finally
            {
                try
                {
                    System.IO.Directory.Delete(lsFolder, true);
                }
                catch (System.IO.IOException)
                {
                }
            }
        }

        /// <summary>
        /// THE ORIGINAL'S SQUARE ROOT (cSqRt, five Newton rounds; UWVanillaMath.Sqrt), which the
        /// creature brain takes since cleanup phase 10 (2026-10-26) for the follower's jump and the
        /// aim's pitch - an exact root rounded down until then (UWCritterBrain.IntSqrt). The roots
        /// of every value the brain can ask for (up to 2 * 512 squared eighths) are pinned in
        /// Pins/vanilla-sqrt.txt.
        /// </summary>
        private static void fCheckVanillaSqrt(CheckContext pOContext)
        {
            Check.Group("The original's square root in the brain (cleanup phase 10, wave 3)");

            fExpectInt("sqrt: 255 gives 16 - five rounds still swing above 15", UWVanillaMath.Sqrt(255), 16);
            fExpectInt("sqrt: 3 gives 2", UWVanillaMath.Sqrt(3), 2);
            fExpectInt("sqrt: 2 gives 1", UWVanillaMath.Sqrt(2), 1);
            fExpectInt("sqrt: 256 gives 16", UWVanillaMath.Sqrt(256), 16);
            fExpectInt("sqrt: 288 gives 17 (from 0x40)", UWVanillaMath.Sqrt(288), 17);
            fExpectInt("sqrt: 70000 gives 264 (from 0x400)", UWVanillaMath.Sqrt(70000), 264);
            fExpectInt("sqrt: 524288, a level's diagonal in eighths squared, gives 724", UWVanillaMath.Sqrt(524288), 724);
            fExpectInt("sqrt: 0x1000000 gives 4096 (from 0x4000)", UWVanillaMath.Sqrt(0x1000000), 4096);
            fExpectInt("sqrt: 28800, the darkening's largest, gives 169", UWVanillaMath.Sqrt(28800), 169);

            int liNotFloor = 0;
            string lsFirst = string.Empty;

            for (int liValue = 0; liValue <= 0xFFFF; liValue++)
            {
                if (UWVanillaMath.Sqrt(liValue) == (int)System.Math.Sqrt(liValue))
                    continue;

                if (liNotFloor < 8)
                    lsFirst += (lsFirst.Length > 0 ? " " : string.Empty) + liValue + ":" + UWVanillaMath.Sqrt(liValue);

                liNotFloor++;
            }

            Check.Str("sqrt: of 0 to 0xFFFF this many are not the root rounded down, the first", liNotFloor + " - " + lsFirst,
                "120 - 3:2 8:3 24:5 35:6 120:11 143:12 168:13 195:14");

            System.Collections.Generic.SortedDictionary<string, string> lONow =
                new System.Collections.Generic.SortedDictionary<string, string>(System.StringComparer.Ordinal);

            fPinSqrtRange(lONow, 0, 0xFF);
            fPinSqrtRange(lONow, 0x100, 0xFFFF);
            fPinSqrtRange(lONow, 0x10000, 0x80000);

            fComparePins(pOContext, "vanilla-sqrt.txt", "vanilla sqrt", lONow, lsKey => "ranges");
        }

        /// <summary>
        /// THE FINE HEADING BY THE ORIGINAL'S TABLE ARCTANGENT (UWCritterBrain.FineHeadingOf as
        /// turn_real_fine computes it, UWVanillaMath.Atan2; Math.Atan2 rounded until cleanup phase
        /// 10, 2026-10-26): the two tables against their formula and UW.EXE, a few vectors by
        /// name, how many headings moved, and the headings of every vector within the casting
        /// range pinned in Pins/fine-headings.txt.
        /// </summary>
        private static void fCheckFineHeadings(CheckContext pOContext)
        {
            Check.Group("The fine heading by the original's table arctangent (cleanup phase 10, wave 3)");

            int liFormulaMisses = 0;

            for (int liAt = 0; liAt <= 128; liAt++)
            {
                double ldRatio = liAt / 128.0;

                if (UWVanillaMath.ArcSineTable[liAt] != (int)System.Math.Floor(System.Math.Asin(ldRatio) * 0x8000 / System.Math.PI)
                    || UWVanillaMath.ArcCosineTable[liAt] != (int)System.Math.Floor(System.Math.Acos(ldRatio) * 0x8000 / System.Math.PI))
                    liFormulaMisses++;
            }

            fExpectInt("tables: 129 entries each", UWVanillaMath.ArcSineTable.Length + UWVanillaMath.ArcCosineTable.Length, 258);
            fExpectInt("tables: every entry the formula floor(asin or acos(i / 128) * 0x8000 / pi)", liFormulaMisses, 0);
            fCheckArcTablesInExe(pOContext);

            Check.Str("atan2: the axes and the diagonals of a unit vector",
                UWVanillaMath.Atan2(0, 0x7FFF) + " " + UWVanillaMath.Atan2(0x7FFF, 0) + " " + UWVanillaMath.Atan2(0, -0x8000) + " "
                + UWVanillaMath.Atan2(-0x8000, 0) + " " + UWVanillaMath.Atan2(0x5A82, 0x5A82) + " " + UWVanillaMath.Atan2(-0x5A82, -0x5A82),
                "0 16384 32768 49152 8191 40959");

            Check.Str("headings: +Y, +X, -Y, -X", UWCritterBrain.FineHeadingOf(0, 5) + " " + UWCritterBrain.FineHeadingOf(5, 0) + " "
                + UWCritterBrain.FineHeadingOf(0, -5) + " " + UWCritterBrain.FineHeadingOf(-5, 0), "0 64 128 192");
            Check.Str("headings: the diagonals 8/8, 8/-8, -8/-8, -8/8 (the root of 128 is 11; atan2 gave 32 96 160 224)",
                UWCritterBrain.FineHeadingOf(8, 8) + " " + UWCritterBrain.FineHeadingOf(8, -8) + " "
                + UWCritterBrain.FineHeadingOf(-8, -8) + " " + UWCritterBrain.FineHeadingOf(-8, 8), "34 95 162 223");
            Check.Str("headings: the shortest diagonals 1/1 and -1/1 lie on the x axis (length 1, both components 1.0)",
                UWCritterBrain.FineHeadingOf(1, 1) + " " + UWCritterBrain.FineHeadingOf(-1, 1), "64 192");
            fExpectInt("headings: the zero vector", UWCritterBrain.FineHeadingOf(0, 0), 0);

            // Against Math.Atan2 rounded to the nearest, what FineHeadingOf was until 2026-10-26.
            System.Collections.Generic.SortedDictionary<int, int> lOMoved = new System.Collections.Generic.SortedDictionary<int, int>();
            int liVectors = 0;
            int[] liAll = new int[127 * 127];

            for (int liDy = -63; liDy <= 63; liDy++)
            {
                for (int liDx = -63; liDx <= 63; liDx++)
                {
                    int liHeading = UWCritterBrain.FineHeadingOf(liDx, liDy);

                    liAll[((liDy + 63) * 127) + liDx + 63] = liHeading;

                    if (liDx == 0 && liDy == 0)
                        continue;

                    liVectors++;

                    int liBefore = (int)System.Math.Round(System.Math.Atan2(liDx, liDy) * 128.0 / System.Math.PI) & 0xFF;
                    int liMoved = ((liHeading - liBefore + 0x80) & 0xFF) - 0x80;

                    if (liMoved != 0)
                    {
                        lOMoved.TryGetValue(liMoved, out int liCount);
                        lOMoved[liMoved] = liCount + 1;
                    }
                }
            }

            string lsMoved = string.Empty;

            foreach (System.Collections.Generic.KeyValuePair<int, int> lOEntry in lOMoved)
                lsMoved += (lsMoved.Length > 0 ? " " : string.Empty) + lOEntry.Key.ToString("+0;-0", CultureInfo.InvariantCulture) + ":" + lOEntry.Value;

            Check.Str("headings: of the " + liVectors + " vectors up to 63 eighths either way, moved against the rounded atan2", lsMoved,
                "-32:1 -31:1 -5:8 -4:8 -2:54 -1:212 +1:7608 +2:198 +3:54 +5:8 +6:8 +32:2");

            System.Collections.Generic.SortedDictionary<string, string> lONow =
                new System.Collections.Generic.SortedDictionary<string, string>(System.StringComparer.Ordinal);

            for (int liDy = -16; liDy <= 16; liDy++)
            {
                string lsRow = string.Empty;

                for (int liDx = -16; liDx <= 16; liDx++)
                    lsRow += (liDx > -16 ? " " : string.Empty) + UWCritterBrain.FineHeadingOf(liDx, liDy);

                lONow[string.Format(CultureInfo.InvariantCulture, "dy {0:+00;-00; 00}, dx -16..16", liDy)] = lsRow;
            }

            lONow["all of -63..63 squared, row by row"] = string.Format(CultureInfo.InvariantCulture, "{0} {1:X8}", liAll.Length, fCrc32(liAll));

            fComparePins(pOContext, "fine-headings.txt", "fine headings", lONow, lsKey => "rows");
        }

        /// <summary>The two tables against UW.EXE beside the data folder, where it is the GOG build
        /// (547,248 bytes): seg063 at paragraph 0x5624 of the load image, the arcsine at +0x762,
        /// the arccosine at +0x864.</summary>
        private static void fCheckArcTablesInExe(CheckContext pOContext)
        {
            string lsExe = null;

            foreach (string lsCandidate in new[]
            {
                System.IO.Path.Combine(pOContext.DataPath ?? string.Empty, "UW.EXE"),
                System.IO.Path.Combine(pOContext.DataPath ?? string.Empty, "..", "UW.EXE")
            })
            {
                if (System.IO.File.Exists(lsCandidate))
                {
                    lsExe = lsCandidate;

                    break;
                }
            }

            byte[] lyExe = lsExe != null ? System.IO.File.ReadAllBytes(lsExe) : null;

            if (lyExe == null || lyExe.Length != 547248)
            {
                Check.Pass("tables: as in UW.EXE", lyExe == null ? "no UW.EXE beside the data folder, not compared" : "not the GOG build, not compared");

                return;
            }

            int liSegment = ((lyExe[8] | (lyExe[9] << 8)) * 16) + (0x5624 * 16);
            int liMisses = 0;

            for (int liAt = 0; liAt <= 128; liAt++)
            {
                if ((short)(lyExe[liSegment + 0x762 + (liAt * 2)] | (lyExe[liSegment + 0x763 + (liAt * 2)] << 8)) != UWVanillaMath.ArcSineTable[liAt]
                    || (short)(lyExe[liSegment + 0x864 + (liAt * 2)] | (lyExe[liSegment + 0x865 + (liAt * 2)] << 8)) != UWVanillaMath.ArcCosineTable[liAt])
                    liMisses++;
            }

            fExpectInt("tables: every entry as in UW.EXE", liMisses, 0);
        }

        /// <summary>
        /// THE ROUNDINGS BY NAME (UWRounding, cleanup phase 10): what each of the three does at the
        /// halves and below zero, and the rule sites that went through them that day keeping what
        /// they did - the eighth of a yaw on a half (to even), the heading byte (truncated, the
        /// original's shift), the noise of a walk (truncated, the original's division).
        /// </summary>
        private static void fCheckRounding()
        {
            Check.Group("Rounding by name (cleanup phase 10, wave 3)");

            Check.Str("half to even: 0.5 1.5 2.5 -0.5 -1.5 -2.5 2.4999",
                UWRounding.HalfToEven(0.5) + " " + UWRounding.HalfToEven(1.5) + " " + UWRounding.HalfToEven(2.5) + " "
                + UWRounding.HalfToEven(-0.5) + " " + UWRounding.HalfToEven(-1.5) + " " + UWRounding.HalfToEven(-2.5) + " "
                + UWRounding.HalfToEven(2.4999), "0 2 2 0 -2 -2 2");
            Check.Str("floor: 2.7 -2.3 -3", UWRounding.Floor(2.7) + " " + UWRounding.Floor(-2.3) + " " + UWRounding.Floor(-3.0), "2 -3 -3");
            Check.Str("truncate: 2.7 -2.7", UWRounding.Truncate(2.7) + " " + UWRounding.Truncate(-2.7), "2 -2");
            Check.Str("a float widens exactly: 0.1f * 30 truncated and 0.5f * 5 rounded as the casts did",
                UWRounding.Truncate(0.1f * 30f) + " " + UWRounding.HalfToEven(0.5f * 5f), (int)(0.1f * 30f) + " " + (int)System.Math.Round(0.5f * 5f));
            Check.Str("sites: the eighth of 22.5, 67.5 and 112.5 degrees (halves to even)",
                UWHeadings.DegreesToEighth(22.5f) + " " + UWHeadings.DegreesToEighth(67.5f) + " " + UWHeadings.DegreesToEighth(112.5f), "0 2 2");
            Check.Str("sites: the heading byte of 1.4 and 359.99 degrees (truncated)",
                UWHeadings.DegreesToHeadingByte(1.4f) + " " + UWHeadings.DegreesToHeadingByte(359.99f), "0 255");
            Check.Str("sites: the noise of a walk at 0.35 and 0.99 of the speed (truncated)",
                UWPlayerVitals.GetMovingNoise(0.35f) + " " + UWPlayerVitals.GetMovingNoise(0.99f), "3 9");
        }

        /// <summary>
        /// THE CAST PAUSE AND THE NOISE DECAY, the two rules of the player that ran on seconds or
        /// frames: try_cast's spell_delay against the game clock (RUNES.C), and the sneak score's
        /// one step down per eight calls of the frame loop (ApplyPlayerSneakScore_seg034_2F89_898).
        /// </summary>
        private static void fCheckTickRates()
        {
            Check.Group("The cast pause and the noise decay on the game's clock (cleanup phase 10, wave 3)");

            // A level-1 caster's In Lor sets (2 * 1 - 1) * 4 + 0x40 = 68 clock units.
            FakeSpellHost lOHost = new FakeSpellHost(new FakeWorld(), UWCastContext.ThePlayer) { PlayerLevel = 1 };
            string lsCasts = string.Empty;

            UWSpellCasting.ResetCastPause();

            foreach (long llAt in new[] { 256000L, 256000L + 67, 256000L + 68, 256000L + 256 })
            {
                lOHost.GameTicks = llAt;
                lsCasts += (lsCasts.Length > 0 ? " " : string.Empty) + UWSpellCasting.CastRunes(8, 11, UWRunicMagic.EmptyRune, lOHost);
            }

            Check.Str("cast pause: In Lor at 0, 67, 68 and 256 units after a level-1 cast", lsCasts, "Cast TooSoon Cast Cast");

            // Noise: walking at full speed (15), then standing - one step down per eight calls, a
            // call per 8 PIT ticks of game time (12 calls a second of the frames' seconds until
            // 10.33: 15 13 12 10 9 7 6 4 3 1 0). The frames only hand the ticks in.
            string lsAt60 = fNoiseTimeline(60);

            Check.Str("noise: walking, then each second of standing (60 frames a second)", lsAt60, "15 11 7 3 0 0 0 0 0 0 0");
            Check.Str("noise: the same at 144 and at 7 frames a second", fNoiseTimeline(144) + " | " + fNoiseTimeline(7),
                lsAt60 + " | " + lsAt60);
        }

        /// <summary>The noise after half a second of walking at full speed and then after each of
        /// ten seconds of standing, the game time handed in by frames of the given rate.</summary>
        private static string fNoiseTimeline(int piFramesPerSecond)
        {
            NoiseHost lOHost = new NoiseHost { IsMoving = true, MovementFraction = 1f };
            UWPlayerVitals lOVitals = new UWPlayerVitals(lOHost);

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);
            lOVitals.TickQuietness(UWPlayerTick.PitTicksPerSecond / 2);

            string lsTimeline = lOVitals.Quietness.ToString(CultureInfo.InvariantCulture);
            int liHandedIn = 0;

            lOHost.IsMoving = false;

            for (int liFrame = 1; liFrame <= piFramesPerSecond * 10; liFrame++)
            {
                int liByNow = liFrame * UWPlayerTick.PitTicksPerSecond / piFramesPerSecond;

                lOVitals.TickQuietness(liByNow - liHandedIn);
                liHandedIn = liByNow;

                if (liFrame % piFramesPerSecond == 0)
                    lsTimeline += " " + lOVitals.Quietness.ToString(CultureInfo.InvariantCulture);
            }

            return lsTimeline;
        }

        /// <summary>The surroundings of the noise rule: walking or standing, nothing else.</summary>
        private sealed class NoiseHost : IUWVitalsHost
        {
            public bool IsMoving { get; set; }

            public float MovementFraction { get; set; }

            public int DungeonLevelIndex => 0;

            public int WornRegeneration => 0;

            public bool IsInMotionState => false;

            public bool IsChargingAttack => false;

            public int GetArmourAt(int piPart) => 0;

            public int DamageTypeProof => 0;
        }

        /// <summary>The roots of a range as one pin line: count, CRC and sum.</summary>
        private static void fPinSqrtRange(System.Collections.Generic.SortedDictionary<string, string> pONow, int piFrom, int piTo)
        {
            int[] liRoots = new int[piTo - piFrom + 1];
            long llSum = 0;

            for (int liValue = piFrom; liValue <= piTo; liValue++)
            {
                liRoots[liValue - piFrom] = UWVanillaMath.Sqrt(liValue);
                llSum += liRoots[liValue - piFrom];
            }

            pONow[string.Format(CultureInfo.InvariantCulture, "sqrt 0x{0:X6}-0x{1:X6}", piFrom, piTo)] =
                string.Format(CultureInfo.InvariantCulture, "{0} {1:X8} sum {2}", liRoots.Length, fCrc32(liRoots), llSum);
        }

        /// <summary>Vitals of a given state; minus one for full.</summary>
        private static UWPlayerVitals fMirrorVitals(IUWVitalsHost pIHost, int piMaxHp, int piMaxMana, int piHp, int piMana,
            bool pbEasy)
        {
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.FallbackMaxHP = piMaxHp;
            lOStart.FallbackMaxMana = piMaxMana;
            lOStart.FallbackCurrentHP = piHp;
            lOStart.FallbackCurrentMana = piMana;
            lOStart.EasyDifficulty = pbEasy;

            UWPlayerVitals lOVitals = new UWPlayerVitals(pIHost);

            lOVitals.Initialise(null, lOStart);

            return lOVitals;
        }

        /// <summary>"hp/max mana/max", as the numbers stand.</summary>
        private static string fVitalsLine(UWPlayerVitals pOVitals)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1} {2}/{3}", pOVitals.CurrentHP, pOVitals.MaxHP,
                pOVitals.CurrentMana, pOVitals.MaxMana);
        }
    }
}
