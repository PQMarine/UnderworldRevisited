using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Spells read out of the disassembly: Detect Monsters, the poison, Summon Monster's choice
    /// and the damage a blast does to objects.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckDetectMonsters()
        {
            Check.Group("Detect Monsters");

            // DetectMonster_seg038_134E: range 10 (strictly below), skill 0x2D, difficulty 0xF
            // minus the stealth nibble, and the second message above min(most, 3).
            fExpectInt("detect: range is ten", UWMiscSpellRules.DetectRange, 10);
            fExpectInt("track: range is eight (ovr143_0 hands 8 to the same search)", UWMiscSpellRules.TrackRange, 8);
            fExpectInt("detect: stealth 5 gives difficulty 10", UWMiscSpellRules.GetDetectDifficulty(5), 10);
            fExpectInt("detect: no second message for 3 and 3", UWMiscSpellRules.GetSecondDirection(new[] { 3, 0, 3, 0, 0, 0, 0, 0 }, 0, 0), -1);
            fExpectInt("detect: a second message for 4 and 4", UWMiscSpellRules.GetSecondDirection(new[] { 4, 0, 0, 4, 0, 0, 0, 0 }, 0, 0), 3);
            fExpectInt("detect: a second from the random start on", UWMiscSpellRules.GetSecondDirection(new[] { 6, 5, 0, 0, 0, 7, 0, 0 }, 5, 3), 0);
            fExpectInt("detect: never the first direction again", UWMiscSpellRules.GetSecondDirection(new[] { 9, 0, 0, 0, 0, 0, 0, 0 }, 0, 0), -1);
            fExpectInt("detect: one east and three north is north-east, as in the original", UWShrineRules.GetCardinalDirection(0, 0, 1, 3), 1);
            fExpectInt("detect: one step east counts as north-east (integer halves)", UWShrineRules.GetCardinalDirection(0, 0, 1, 0), 1);
        }

        private static void fCheckPoisonSpell()
        {
            Check.Group("Poison spell");

            // Poison_seg038_3307_834: 5 dice of 4, type 0x13, no lasting poison.
            // The distance cull: frozen only when far from the player AND from the view.
            fExpectBool("cull: far from both freezes", UWCritterRules.IsFarFromBoth(11, 0, 0, 11), true);
            fExpectBool("cull: near the camera trap's view stays awake", UWCritterRules.IsFarFromBoth(20, 20, 3, 0), false);
            fExpectBool("cull: near the player stays awake", UWCritterRules.IsFarFromBoth(6, 8, 30, 30), false);

            fExpectInt("poison: damage type is poison and magic", UWTargetSpellRules.PoisonDamageType, 0x13);

            // The bounds straight from the lowest and the highest roll of every die. Until
            // 2026-09-26 this rolled 5000 times and waited for 5 and 20 to show up; each needs all
            // five dice at one end (1 in 1024), so a run missed one with about 1.5 % and failed.
            UWRandom.Use(new FixedRandomSource(false));
            int liMin = UWTargetSpellRules.RollPoisonDamage();
            UWRandom.Use(new FixedRandomSource(true));
            int liMax = UWTargetSpellRules.RollPoisonDamage();
            UWRandom.Use(null);

            fExpectInt("poison: at least 5", liMin, 5);
            fExpectInt("poison: at most 20", liMax, 20);
            fExpectInt("poison: a poison-proof creature without magic resistance takes nothing",
                UWDamageTypes.Scale(UWDamageTypes.Poison, 12, UWTargetSpellRules.PoisonDamageType), 0);
            fExpectInt("poison: an ordinary creature takes it all",
                UWDamageTypes.Scale(0, 12, UWTargetSpellRules.PoisonDamageType), 12);
        }

        /// <summary>
        /// SUMMON MONSTER'S CHOICE (Class8Spells_seg038_E45, read 2026-09-27): id = 0x40 + level +
        /// RNG % level - the upper half -, never a passive kind, a swimmer, an empty row or 0x7B/0x7C.
        /// </summary>
        private static void fCheckSummonRoll(DataImport pOData)
        {
            Check.Group("Summon Monster");

            if (pOData.ObjectClassProperties == null)
            {
                fFail("summon", "no creature table");
                return;
            }

            bool lbInRange = true;
            bool lbFits = true;

            for (int liAt = 0; liAt < 400; liAt++)
            {
                int liId = UWSummonSpellRules.RollMonster(12, pOData.ObjectClassProperties);

                if (liId < 0)
                    continue;

                lbInRange &= liId >= 0x40 + 12 && liId < 0x40 + 24;

                UWObjectClassProperties.Critter lOKind;

                lbFits &= pOData.ObjectClassProperties.TryGetCritter(liId, out lOKind)
                    && lOKind.Vitality != 0 && (lOKind.Passiveness & 0x42) == 0 && liId != 0x7B && liId != 0x7C;
            }

            fExpectBool("summon: casting 12 draws from 0x4C to 0x57", lbInRange, true);
            fExpectBool("summon: never a passive kind, a swimmer or an empty row", lbFits, true);
            fExpectBool("summon: casting 12 finds something", UWSummonSpellRules.RollMonster(12, pOData.ObjectClassProperties) >= 0, true);

            // A creature made at runtime takes a slot from the level's free list at once, below
            // 256 (UWLevel.TryPlaceInFreeMobileSlot). The creature stays in the level; the runner
            // reloads the world after this group (MutatesData), so the object-limit count that
            // runs later sees fresh levels.
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 8 ? pOData.Levels[8] : null;
            UWNpc lONew = new UWNpc(70);
            int liSlot;
            bool lbPlaced = lOLevel != null && lOLevel.TryPlaceInFreeMobileSlot(lONew, out liSlot) && liSlot >= 2 && liSlot < 256
                && lOLevel.Masterlist[liSlot] == lONew;

            fExpectBool("summon: a new creature gets a mobile slot from the free list", lbPlaced, true);

            // ...and it is on the list of active critters in the saved block, else the original
            // never moves it (per user 2026-10-12: a goblin summoned in ours stood frozen there).
            string lsArk = pOData.DataPath != null ? Path.Combine(pOData.DataPath, "LEV.ARK") : null;

            if (!lbPlaced || lsArk == null || !File.Exists(lsArk))
            {
                fFail("summon: a creature made in play is on the active list after saving", "no slot or no LEV.ARK");
                return;
            }

            byte[] lyArk = File.ReadAllBytes(lsArk);
            uint luOffset = BitConverter.ToUInt32(lyArk, 2 + (8 * 4));
            byte[] lyOriginal = new byte[UWLevArkLayout.BlockSize];

            Array.Copy(lyArk, (int)luOffset, lyOriginal, 0, lyOriginal.Length);

            UWTile lOTile = lOLevel.GetTile(32, 32);

            if (lOTile.ObjectsInTile == null)
                lOTile.ObjectsInTile = new List<UWObject>();

            lOTile.ObjectsInTile.Add(lONew);

            byte[] lyBuilt = UWLevelWriter.BuildBlock(lOLevel, lyOriginal);
            int liPlacedSlot = lOLevel.Masterlist.IndexOf(lONew);

            fExpectBool("summon: a creature made in play is on the active list after saving",
                UWLevArkCodec.ReadActiveMobiles(lyBuilt, 0).Contains(liPlacedSlot), true);
        }

        /// <summary>
        /// DAMAGE TO OBJECTS (DamageObjectAndDoors, DamageObject_Debris, DetonateProjectile;
        /// read 2026-09-27): the blast dice of fireball and lightning bolt, the wear by quality
        /// class and bit 13, and the remains - fire takes a pile of debris away, smoke one time
        /// in four.
        /// </summary>
        private static void fCheckObjectDamage(string psDataPath)
        {
            Check.Group("Damage to objects");

            fExpectBool("blast: the fireball explodes",
                UWObjectDamageRules.TryGetBlast(0x14, out int liCount, out int liRange, out int liType), true);
            fExpectInt("blast: fireball 10 dice", liCount, 10);
            fExpectInt("blast: ... of 6", liRange, 6);
            fExpectInt("blast: ... fire 0x0B", liType, 0x0B);
            UWObjectDamageRules.TryGetBlast(0x15, out liCount, out liRange, out liType);
            fExpectInt("blast: lightning bolt 6 dice", liCount, 6);
            fExpectInt("blast: ... of 5", liRange, 5);
            fExpectInt("blast: ... magic 3", liType, 3);
            fExpectBool("blast: the magic missile does not explode",
                UWObjectDamageRules.TryGetBlast(0x17, out _, out _, out _), false);

            fExpectBool("wear: class 0 loses it all", UWObjectDamageRules.Wear(40, 35, 0, false, out int liQuality), false);
            fExpectInt("wear: 40 - 35", liQuality, 5);
            UWObjectDamageRules.Wear(40, 35, 2, false, out liQuality);
            fExpectInt("wear: class 2 a quarter (35 >> 2 = 8)", liQuality, 32);
            fExpectBool("wear: class 3 immune", UWObjectDamageRules.Wear(40, 60, 3, false, out liQuality), false);
            fExpectInt("wear: class 3 unchanged", liQuality, 40);
            fExpectBool("wear: bit 13 protects", UWObjectDamageRules.Wear(1, 60, 0, true, out liQuality), false);
            fExpectBool("wear: at 0 destroyed", UWObjectDamageRules.Wear(20, 20, 0, false, out liQuality), true);
            fExpectInt("wear: never below 0", liQuality, 0);
            fExpectBool("wear: class 1 halves 1 to nothing", UWObjectDamageRules.Wear(1, 1, 1, false, out _), false);

            UWRandom.Use(new FixedRandomSource(false));
            fExpectBool("remains: fire and a zero roll raise smoke",
                UWObjectDamageRules.DecideRemains(0x05, UWDamageTypes.Fire) == UWObjectDamageRules.Remains.DebrisWithSmoke, true);
            fExpectBool("remains: fire takes a pile of debris away",
                UWObjectDamageRules.DecideRemains(0xD6, UWDamageTypes.Fire) == UWObjectDamageRules.Remains.Gone, true);
            fExpectBool("remains: lightning leaves plain debris",
                UWObjectDamageRules.DecideRemains(0x05, UWDamageTypes.Magic) == UWObjectDamageRules.Remains.Debris, true);
            fExpectInt("remains: lowest roll 0xD5", UWObjectDamageRules.RollDebrisObjectId(), 0xD5);
            fExpectInt("smoke: at least 6 units, 1.5 s (in tenths)",
                (int)Math.Round(UWObjectDamageRules.RollSmokeSeconds() * 10f), 15);
            UWRandom.Use(new FixedRandomSource(true));
            fExpectBool("remains: fire and 0x7FFF (& 3 = 3) no smoke",
                UWObjectDamageRules.DecideRemains(0x05, UWDamageTypes.Fire) == UWObjectDamageRules.Remains.Debris, true);
            fExpectInt("remains: highest roll 0xD6", UWObjectDamageRules.RollDebrisObjectId(), 0xD6);
            fExpectInt("smoke: at most 60 units, 15 s (in tenths)",
                (int)Math.Round(UWObjectDamageRules.RollSmokeSeconds() * 10f), 150);
            UWRandom.Use(null);

            fExpectBool("remains: a bag spills", UWObjectDamageRules.SpillsWhenDestroyed(0x80), true);
            fExpectBool("remains: a sword does not", UWObjectDamageRules.SpillsWhenDestroyed(0x05), false);

            UWHealth lODoor = new UWHealth();
            lODoor.Initialise(40, 0, 0, 0, 0, -1, 0);
            lODoor.IsProtected = true;
            fExpectBool("door: word 0 bit 13 takes no blow", lODoor.CanTakeDamage, false);
            lODoor.IsProtected = false;
            fExpectBool("door: without the bit a class 0 door takes one", lODoor.CanTakeDamage, true);

            fExpectInt("hallucination: the normal 64 mask reads row 5, column 7 as texel 5*64+7",
                UWHallucinationState.ScrambledTexel((5 * 64) + 13, 7, UWHallucinationState.NormalMask64, 64), (5 * 64) + 7);
            fExpectInt("hallucination: the normal 32 mask drops the row's fraction",
                UWHallucinationState.ScrambledTexel((3 * 32) + 31, 4, UWHallucinationState.NormalMask32, 32), (3 * 32) + 4);
            fExpectInt("hallucination: a random mask lets the fraction into the column",
                UWHallucinationState.ScrambledTexel((3 * 32) + 5, 4, 0x3FF, 32), (3 * 32) + 9);
            UWHallucinationState lOHallucination = new UWHallucinationState();
            fExpectInt("hallucination: masks are the normal ones while off", lOHallucination.ScrambleMasks[4], 0xFC0);
            byte[] lyLightTable = UWHallucinationState.BuildLightTableEffect();
            int liOther = UWHallucinationState.OtherPart * 16 * 256;
            int liFloor = UWHallucinationState.FloorPart * 16 * 256;
            int liWall = UWHallucinationState.WallPart * 16 * 256;

            // Counts the non-black entries of one part over a range of levels.
            Func<int, int, int, int> fLit = (piPart, piFirst, piLast) =>
            {
                int liLit = 0;

                for (int liLevel = piFirst; liLevel <= piLast; liLevel++)
                    for (int liIndex = 0; liIndex < 256; liIndex++)
                        liLit += lyLightTable[piPart + (liLevel * 256) + liIndex] != 0 ? 1 : 0;

                return liLit;
            };

            fExpectBool("hallucination: a sparse green scatter on the floor right in front", fLit(liFloor, 3, 3) > 10 && fLit(liFloor, 3, 3) < 40, true);
            fExpectInt("hallucination: walls black right in front", fLit(liWall, 0, 3), 0);
            fExpectInt("hallucination: 4 to 8 all black", fLit(liOther, 4, 8) + fLit(liFloor, 4, 8) + fLit(liWall, 4, 8), 0);
            fExpectInt("hallucination: 9 to 11 floors black", fLit(liFloor, 9, 11), 0);
            fExpectInt("hallucination: 9 to 11 walls grey", fLit(liWall, 10, 10), 128);
            fExpectInt("hallucination: 12 and 13 walls black", fLit(liWall, 12, 13), 0);
            fExpectInt("hallucination: 12 and 13 floors grey", fLit(liFloor, 12, 12), 128);
            fExpectBool("hallucination: 14 a sparse purple scatter on walls", fLit(liWall, 14, 14) > 25 && fLit(liWall, 14, 14) < 60, true);
            fExpectInt("hallucination: 14 floors and the rest black, 15 all black",
                fLit(liFloor, 14, 15) + fLit(liOther, 9, 15) + fLit(liWall, 15, 15), 0);

            string lsExe = DataPath.FindExe(psDataPath);

            if (lsExe != null)
            {
                // The grey band is the file's row 2 of segment 51 (GOG UW.EXE), entries 0 and 1 aside.
                byte[] lyExe = File.ReadAllBytes(lsExe);
                int liDiffer = 0;

                for (int liIndex = 2; liIndex < 256; liIndex++)
                {
                    int liFile = lyExe.Length > 0x4A430 + 0x300 ? lyExe[0x4A430 + 0x200 + liIndex] : -1;
                    int liOurs = lyLightTable[liFloor + (12 * 256) + liIndex];

                    liDiffer += (liFile == 255 ? 255 : 0) != liOurs ? 1 : 0;
                }

                fExpectInt("hallucination: the grey on floors is the file's row 2", liDiffer, 0);
            }
        }
    }
}
