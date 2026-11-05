using System.Collections.Generic;
using System.IO;
using UWDataImport.Install;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE GAME PROFILE (Docs/UW2-PLAN.md phase 0 step 1, 2026-10-28): the first game's profile holds
    /// what the code held as literals before - the image folder, the program with its size and hash,
    /// the cache, save and game folders - with the same values; the install code and the game folder
    /// read them from it; the game data and the session carry the profile, and a session without
    /// data is the first game's. With the image at hand: the profile's folder and program are in it.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckGameProfile(CheckContext pOContext)
        {
            Check.Group("The game profile (UW2 plan, phase 0 step 1)");

            IUWGameProfile lOUw1 = UWGameProfiles.Uw1;

            Check.Str("profile: the first game's values", lOUw1.Game + "|" + lOUw1.ImageFolder + "|" + lOUw1.Executable + "|" + lOUw1.ExecutableSize + "|"
                + lOUw1.ExecutableSha1 + "|" + lOUw1.CacheFolder + "|" + lOUw1.SaveFolder + "|" + lOUw1.GameFolder,
                "Uw1|UW|UW.EXE|547248|226fed4dba9718a7c972de5451b520fcd16546c0|UW1|UNDEROM1|UW1");
            IUWGameProfile lOUw2 = UWGameProfiles.Uw2;

            Check.Str("profile: the second game's values (phase 0 step 4)", lOUw2.Game + "|" + lOUw2.ImageFolder + "|" + lOUw2.Executable + "|" + lOUw2.ExecutableSize + "|"
                + lOUw2.ExecutableSha1 + "|" + lOUw2.CacheFolder + "|" + lOUw2.SaveFolder + "|" + lOUw2.GameFolder,
                "Uw2|UW2|UW2.EXE|675184|41a23de0fa85c375920ae5d0dbd2ec5f4657c758|UW2|UNDEROM2|UW2");
            Check.Bool("profile: the second game's rule set exists (phase 2 step 4), its other variants not yet (asking throws)",
                lOUw2.CreateOriginalRules() != null && lOUw2.Motion != null
                && fThrowsNotSupported(() => lOUw2.TrapKinds), true);
            Check.Str("profile: the first game is playable, the second not yet (phase 0 step 5)", lOUw1.IsPlayable + " " + lOUw2.IsPlayable, "True False");

            // The second game's compression: three literals, then the ring's 4078 for three ("abcabc");
            // a reference into the spaces the ring starts with; a stream cut short stops where it ends.
            byte[] lyStream = { 0x07, (byte)'a', (byte)'b', (byte)'c', 0xEE, 0xF0, 0x00, 0x00 };

            Check.Str("lzss: literals and a reference back", System.Text.Encoding.ASCII.GetString(UWLzss.Decode(lyStream, 0, 6, 6)), "abcabc");
            Check.Str("lzss: a reference into the starting spaces, then a cut stream", "[" + System.Text.Encoding.ASCII.GetString(UWLzss.Decode(new byte[] { 0x00, 0x00, 0x01, 0x05 }, 0, 4, 4)) + "]",
                "[" + new string(' ', 4) + "]");
            Check.Str("profile: the game folder is the profile's", UWGameFolder.Under("root", lOUw1), Path.Combine("root", UWGameFolder.Uw1));
            Check.Bool("profile: one instance per game, both listed, the first first", UWGameProfiles.Of(UWGame.Uw1) == lOUw1 && UWGameProfiles.Of(UWGame.Uw2) == lOUw2
                && UWGameProfiles.All.Length == 2 && UWGameProfiles.All[0] == lOUw1 && UWGameProfiles.All[1] == lOUw2, true);
            Check.Bool("profile: a session without data plays the first game", new UWGameSession(null, 1).Game == lOUw1, true);

            if (pOContext.HasData)
            {
                Check.Bool("profile: the game data carries the first game's profile", pOContext.Data.GameData.Profile == lOUw1, true);
                Check.Bool("profile: ... and a session on it plays that game", new UWGameSession(pOContext.Data.GameData, 1).Game == lOUw1, true);
            }

            if (!string.IsNullOrEmpty(pOContext.ImagePath) && File.Exists(pOContext.ImagePath))
            {
                Check.Bool("profile: the image has the profile's folder (a file of its DATA read through it)", UWGogInstall.ReadDataFile(pOContext.ImagePath, lOUw1, "PALS.DAT") != null, true);
                fCheckBothGamesInImage(pOContext.ImagePath);
            }

            // Phase 0 step 2: the session's rule set is its game's; the rules ask the game's variants.
            Check.Bool("profile: a session's rule set is its game's original (a new one per session)",
                new UWGameSession(null, 1).Rules != new UWGameSession(null, 1).Rules, true);

            if (pOContext.HasData)
                fCheckMagicVariantAsked(pOContext);
        }

        /// <summary>Whether asking throws NotSupportedException.</summary>
        private static bool fThrowsNotSupported(System.Func<object> pFAsk)
        {
            try
            {
                pFAsk();

                return false;
            }
            catch (System.NotSupportedException)
            {
                return true;
            }
        }

        /// <summary>
        /// Both games in the one game.gog (phase 0 step 4): the image holds each game's folder with
        /// its supported program and not a program of another build; the second game extracts into
        /// a cache folder of its own (the tools' cache, beside the first game's) with its DATA and
        /// UW2.EXE as the first game's has UW.EXE; each game's saves are looked for in its own
        /// folder beside the image.
        /// </summary>
        private static void fCheckBothGamesInImage(string psImage)
        {
            Check.Str("install: the image holds both games (first, second, another build of the second)",
                UWGogInstall.HasGame(psImage, UWGameProfiles.Uw1) + " " + UWGogInstall.HasGame(psImage, UWGameProfiles.Uw2) + " "
                + UWGogInstall.HasGame(psImage, new OtherBuildProfile()), "True True False");
            Check.Bool("install: the second game's DATA read straight from the image (LEV.ARK, T64.TR)",
                UWGogInstall.ReadDataFile(psImage, UWGameProfiles.Uw2, "LEV.ARK") != null && UWGogInstall.ReadDataFile(psImage, UWGameProfiles.Uw2, "T64.TR") != null
                && UWGogInstall.ReadDataFile(psImage, UWGameProfiles.Uw1, "T64.TR") == null, true);

            string lsCacheRoot = Path.Combine(Path.GetTempPath(), "UnderworldRevisited");
            string lsData = UWGogInstall.EnsureExtracted(psImage, lsCacheRoot, UWGameProfiles.Uw2, out string lsError);
            string lsGameFolder = lsData == null ? null : Path.GetDirectoryName(lsData);

            Check.Str("install: the second game extracts into its own cache folder", lsData == null ? "failed: " + lsError
                : Path.GetFileName(lsGameFolder) + "/" + Path.GetFileName(lsData), "UW2/DATA");

            if (lsData != null)
            {
                string lsExe = Path.Combine(lsGameFolder, UWGameProfiles.Uw2.Executable);

                Check.Bool("install: ... with LEV.ARK, CRIT, CUTS and SOUND, and UW2.EXE the supported build (not the first game's)",
                    File.Exists(Path.Combine(lsData, "LEV.ARK")) && Directory.Exists(Path.Combine(lsGameFolder, "CRIT"))
                    && Directory.Exists(Path.Combine(lsGameFolder, "CUTS")) && Directory.Exists(Path.Combine(lsGameFolder, "SOUND"))
                    && UWGogInstall.IsSupportedExe(lsExe, UWGameProfiles.Uw2) && !UWGogInstall.IsSupportedExe(lsExe, UWGameProfiles.Uw1), true);

                fCheckSecondGameScreens(lsData);
            }

            // Where this machine's saves are found (machine-specific, so reported, not pinned).
            foreach (IUWGameProfile lOGame in UWGameProfiles.All)
                Check.Pass("install: the " + lOGame.Game + " saves for this image", UWGogInstall.GetSavegameFolder(psImage, lOGame) ?? "none");

            fCheckSaveFolderRule();
            fCheckSteamInstall();
            fCheckGameDocuments();
            fCheckValuableRoll();
            fCheckPlayerUw2();
            fCheckDeathUw2();
            fCheckFoodUw2();
            fCheckSleepUw2();
            fCheckMagicPartAUw2();
            fCheckStatusUw2();
            fCheckCritterUw2();
            fCheckCombatUw2();
        }

        /// <summary>The second game's combat steps (uw2 COMBAT.C, DAMAGE.C).</summary>
        private static void fCheckCombatUw2()
        {
            IUWCombatVariant lICombat = UWGameProfiles.Uw2.Combat;
            string lsGot = string.Empty;

            foreach (int liMinor in new[] { 0, 3, 4, 7, 8 })
            {
                lICombat.WeaponEnchantment(0xC, liMinor, out int liAccuracy, out int liDamage);
                lsGot += liAccuracy + "/" + liDamage + " ";
            }

            Check.Str("uw2 combat: a weapon's enchantment, attack score / damage, effect 0 3 4 7 8", lsGot.Trim(), "0/1 0/7 1/0 7/0 0/0");
            Check.Str("uw2 combat: fire on cold resistance doubled, cold on fire, not on both, 200 goes to 255",
                lICombat.Unresisted(0x20, 10, 8) + " " + lICombat.Unresisted(8, 10, 0x20) + " " + lICombat.Unresisted(0x28, 10, 0x20) + " " + lICombat.Unresisted(0x20, 200, 8),
                "20 20 10 255");
            Check.Str("uw2 combat: doors take damage, the bit and the protected class spare the rest",
                lICombat.DoorsTakeDamage + " " + lICombat.ShrugsOff(true, 0) + " " + lICombat.ShrugsOff(false, UWObjectDamageRules.ImmuneQualityClass) + " " + lICombat.ShrugsOff(false, 0),
                "True True True False");

            lICombat.WeaponEnchantment(0xC, 15, out int liLastAccuracy, out int liLastDamage);
            lICombat.WeaponEnchantment(0xC, 8, out int liFirstAccuracy, out int liFirstDamage);

            Check.Str("uw2 combat: special weapons add nothing but effect 15's damage", liFirstAccuracy + "/" + liFirstDamage + " " + liLastAccuracy + "/" + liLastDamage, "0/0 0/5");
            Check.Str("uw2 combat: the kind of a dagger, a cudgel, a jewelled dagger, a sword, the fist, a bow",
                lICombat.PlayerWeaponKind(3) + " " + lICombat.PlayerWeaponKind(7) + " " + lICombat.PlayerWeaponKind(0xA) + " " + lICombat.PlayerWeaponKind(0) + " "
                + lICombat.PlayerWeaponKind(UWCombat.FistObjectId) + " " + lICombat.PlayerWeaponKind(0x19), "2 1 2 2 0 0");
            Check.Str("uw2 combat: the hit's sound of fist, blunt, sharp", lICombat.MeleeHitSound(0) + " " + lICombat.MeleeHitSound(1) + " " + lICombat.MeleeHitSound(2), "4 4 27");
            Check.Str("uw2 combat: the wall's sound of fist, blunt, sharp", lICombat.WallSound(0) + " " + lICombat.WallSound(1) + " " + lICombat.WallSound(2), "8 7 7");
            Check.Str("uw2 combat: blocked on metal: fist, blunt, sharp; sharp on leather",
                lICombat.BlockedSound(0, true) + " " + lICombat.BlockedSound(1, true) + " " + lICombat.BlockedSound(2, true) + " " + lICombat.BlockedSound(2, false), "8 7 7 8");
            Check.Str("uw2 combat: debris of a chair by class, a dagger, fire on a dagger, a stone", lICombat.DebrisOf(0x150, 4, null) + " " + lICombat.DebrisOf(3, 4, null) + " "
                + lICombat.DebrisOf(3, 8, null) + " " + lICombat.DebrisOf(0xA0, 4, null), "220 199 214 214");
            IUWObjectVariant lIObjects = UWGameProfiles.Uw2.Objects;

            Check.Str("uw2 objects: a light is used up at 1, burns out at 1, and its two messages",
                lIObjects.LightUsedUp(2) + " " + lIObjects.LightUsedUp(1) + " " + lIObjects.BurntOutQuality + " " + lIObjects.LightUsedUpString + " " + lIObjects.HandsFullString,
                "False True 1 139 262");
            IUWMagicVariant lIMagic = UWGameProfiles.Uw2.Magic;

            Check.Str("uw2 magic: the table, eight to a circle, In Lor as spell 5, the strings",
                lIMagic.Spells.Count + " " + lIMagic.SpellsPerCircle + " " + lIMagic.Spells[5].RuneSequence + "/" + lIMagic.Spells[5].MajorClass + "/" + lIMagic.Spells[5].MinorClass
                + " " + lIMagic.FailStringBase + " " + lIMagic.HealedString, "69 8 8568/0/131 226 293");
            Check.Str("uw2 magic: a missile and a target spell pay when released, others at once",
                lIMagic.PaysOnRelease(lIMagic.Spells[33]) + " " + lIMagic.PaysOnRelease(lIMagic.Spells[6]), "True False");
            Check.Str("uw2 objects: a burnt-out light has no condition, other things keep theirs",
                lIObjects.LookQuality(0x91, 1) + " " + lIObjects.LookQuality(0x95, 1) + " " + lIObjects.LookQuality(0x91, 2) + " " + lIObjects.LookQuality(0x10, 1),
                "0 0 2 1");
            Check.Str("uw2 objects: the look's strings: owners, nothing, spike, bones",
                lIObjects.LastOwnerRace + " " + lIObjects.NothingToSeeString + " " + lIObjects.DoorSpikedString + " " + lIObjects.BonesLeadString, "30 169 146 27");
            Check.Str("uw2 objects: a blackrock gem is cool, warm with an owner, others have no word",
                lIObjects.NameWordString(new UWObject(0x118)) + " " + lIObjects.NameWordString(new UWObject(0x11F) { Owner = 3 }) + " " + lIObjects.NameWordString(new UWObject(0x91)),
                "357 358 -1");
            Check.Str("uw2 combat: the launch sounds of a throw, a bow, a sling and the jeweled bow",
                lICombat.ThrowSound + " " + lICombat.ShotSound(0x19) + " " + lICombat.ShotSound(0x18) + " " + lICombat.ShotSound(0x1F), "28 10 10 28");
            Check.Str("uw2 combat: only a bow or crossbow that flew twangs",
                lICombat.Twangs(0x19, true) + " " + lICombat.Twangs(0x1A, true) + " " + lICombat.Twangs(0x18, true) + " " + lICombat.Twangs(0x19, false) + " " + lICombat.Twangs(0x1F, true),
                "True True False False False");
            Check.Str("uw2 combat: no room to fire, and no second sound on a missile's hit",
                lICombat.NoRoomSound + " " + lICombat.NoRoomToFireString + " " + lICombat.MissileDamageSounds, "45 271 False");
        }

        /// <summary>The second game's record holds sequences: the brain's slots go in and come back.</summary>
        private static void fCheckCritterUw2()
        {
            IUWCritterVariant lICritter = UWGameProfiles.Uw2.Critter;
            string lsGot = string.Empty;

            foreach (int liSlot in new[] { UWCritterBrain.AnimStanding, UWCritterBrain.AnimWalking, UWCritterBrain.AnimCombatIdle, UWCritterBrain.AnimBash,
                UWCritterBrain.AnimSpell, UWCritterBrain.AnimDying })
                lsGot += lICritter.RecordOfSlot(liSlot) + ":" + (lICritter.SlotOfRecord(lICritter.RecordOfSlot(liSlot)) == liSlot) + " ";

            Check.Str("uw2 critter: the brain's slots as the record's sequences, and back", lsGot.Trim(), "0:True 1:True 2:True 3:True 6:True 7:True");
        }

        /// <summary>uw2 print_info and pass_time's step, their strings as our block counts them.</summary>
        private static void fCheckStatusUw2()
        {
            Check.Str("uw2 status: hunger's words, starving and stuffed", UWStatusReportUw2.HungerWord(0) + " " + UWStatusReportUw2.HungerWord(255), "118 126");
            Check.Str("uw2 status: fatigue's words, wide awake and fatigued", UWStatusReportUw2.FatigueWord(0) + " " + UWStatusReportUw2.FatigueWord(255), "132 127");
            Check.Str("uw2 status: the new game's clock is in the third daypart", UWStatusReportUw2.DaypartWord(UWPlayerRecordUw2.NewGameClock).ToString(), "87");
            Check.Str("uw2 status: no world known, Britannia, an unknown world, a known one",
                UWStatusReportUw2.WorldWord(0, 1) + " " + UWStatusReportUw2.WorldWord(4, 1) + " " + UWStatusReportUw2.WorldWord(4, 9) + " " + UWStatusReportUw2.WorldWord(1, 9), "-1 76 75 77");
        }

        /// <summary>uw2 player_sleep: the lines of lying down (ours one above the game's), no dream of Garamon.</summary>
        private static void fCheckSleepUw2()
        {
            IUWPlayerVariant lIFirst = UWGameProfiles.Uw1.Player;
            IUWPlayerVariant lISecond = UWGameProfiles.Uw2.Player;

            Check.Str("sleep: uw1 lies down with one line", lIFirst.CampLine(0) + " " + lIFirst.CampLine(1) + " " + lIFirst.CampLine(2), "16 16 16");
            Check.Str("sleep: uw2 ground, bedroll, bed", lISecond.CampLine(0) + " " + lISecond.CampLine(1) + " " + lISecond.CampLine(2), "16 23 24");
            Check.Str("sleep: Garamon dreams in uw1 only", lIFirst.HasGaramonDreams + " " + lISecond.HasGaramonDreams, "True False");
            Check.Bool("sleep: uw2 may lie down on the Void's level", lISecond.MaySleepAt(UWGameProfiles.Uw2.CreateOriginalRules(), 65), true);
        }

        /// <summary>UW2 phase 4 magic stage 2 part A: the Academy's mana, Map Area, Portal, the Void's tremor, what is built.</summary>
        private static void fCheckMagicPartAUw2()
        {
            IUWPlayerVariant lIFirst = UWGameProfiles.Uw1.Player;
            IUWPlayerVariant lISecond = UWGameProfiles.Uw2.Player;

            Check.Str("uw2 mana: level 40, the Academy's 1st, 2nd, 8th at x 24 and 25, level 49; uw1 on 42",
                lISecond.MayRestoreMana(40, 0) + " " + lISecond.MayRestoreMana(41, 0) + " " + lISecond.MayRestoreMana(42, 30) + " " + lISecond.MayRestoreMana(48, 24) + " "
                + lISecond.MayRestoreMana(48, 25) + " " + lISecond.MayRestoreMana(49, 0) + " " + lIFirst.MayRestoreMana(42, 0), "True True False False True True True");
            Check.Str("uw2 map area: the radius at Casting 0, 10, 15, 16, 30",
                UWSpecialSpellRulesUw2.MapAreaRadius(0) + " " + UWSpecialSpellRulesUw2.MapAreaRadius(10) + " " + UWSpecialSpellRulesUw2.MapAreaRadius(15) + " "
                + UWSpecialSpellRulesUw2.MapAreaRadius(16) + " " + UWSpecialSpellRulesUw2.MapAreaRadius(30), "2 4 5 5 11");
            Check.Str("uw2 portal: height 15 at fine z 0, 2 at 0, 15 at 13, 15 at 12",
                UWExtraSpellRulesUw2.PortalAllows(15, 0) + " " + UWExtraSpellRulesUw2.PortalAllows(2, 0) + " " + UWExtraSpellRulesUw2.PortalAllows(15, 13) + " " + UWExtraSpellRulesUw2.PortalAllows(15, 12), "False True True False");

            string lsVoid = string.Empty;

            for (int liSlot = 0; liSlot < 6; liSlot++)
                lsVoid += UWExtraSpellRulesUw2.VoidThing(new UWTile(0, 1u | ((uint)liSlot << 10), new ushort[64])) + " ";

            Check.Str("uw2 tremor: the Void's thing by floor slot 0 to 5", lsVoid.Trim(), "194 185 182 297 160 -1");

            IUWMagicVariant lIMagic = UWGameProfiles.Uw2.Magic;
            string lsBuilt = string.Empty;

            foreach (int liSpell in new[] { 7, 15, 46, 58, 61, 63, 67, 0, 24, 33, 4, 65 })
                lsBuilt += lIMagic.IsBuilt(lIMagic.Spells[liSpell]) ? "1" : "0";

            Check.Str("uw2 magic built: Locate, Dispel Hunger, Map Area, Freeze Time, Roaming Sight, Armageddon, Mana Boost; Create Food, Frost, Fireball, Detect Trap, Acid",
                lsBuilt, "111101100000");

            UWTile[] lOTiles = new UWTile[0x1000];

            for (int liAt = 0; liAt < lOTiles.Length; liAt++)
                lOTiles[liAt] = new UWTile(liAt, 1u, new ushort[64]);

            UWLevel lOLevel = new UWLevel(9) { TileData = lOTiles };

            lOLevel.EnsureAutomap();
            UWSpecialSpellRulesUw2.MapCircle(lOLevel, null, 10, 10, 2);
            int liMiddle = fCountMapped(lOLevel);

            lOLevel = new UWLevel(9) { TileData = lOTiles };
            lOLevel.EnsureAutomap();
            UWSpecialSpellRulesUw2.MapCircle(lOLevel, null, 1, 1, 2);

            Check.Str("uw2 map area: radius 2 in the middle, and at the corner as automap_area clips it, the byte of an open tile",
                liMiddle + " " + fCountMapped(lOLevel) + " " + lOLevel.AutomapTiles[new UWTilePos(2, 2).Index] + " " + lOLevel.AutomapTiles[new UWTilePos(3, 2).Index], "13 6 1 0");
        }

        private static int fCountMapped(UWLevel pOLevel)
        {
            int liCount = 0;

            foreach (byte lyAt in pOLevel.AutomapTiles)
            {
                if (lyAt != 0)
                    liCount++;
            }

            return liCount;
        }

        /// <summary>uw2 UseFood: what is eaten and what it leaves.</summary>
        private static void fCheckFoodUw2()
        {
            Check.Str("uw2 food: eaten on use: meat, plant, dream plant, candle, sword",
                UWFoodUw2.IsEatenOnUse(0xB0) + " " + UWFoodUw2.IsEatenOnUse(0xCE) + " " + UWFoodUw2.IsEatenOnUse(0x114) + " " + UWFoodUw2.IsEatenOnUse(0x92) + " " + UWFoodUw2.IsEatenOnUse(4),
                "True True True False False");
            Check.Bool("uw2 food: the candle only from the head", UWFoodUw2.IsEatenFromHead(0x92), true);
            Check.Str("uw2 food: leftovers of a piece of meat, a stick's meat, a honeycomb, ale, an apple",
                UWFoodUw2.LeftoverOf(0xB0) + " " + UWFoodUw2.LeftoverOf(0xBE) + " " + UWFoodUw2.LeftoverOf(0xBA) + " " + UWFoodUw2.LeftoverOf(0xBB) + " " + UWFoodUw2.LeftoverOf(0xB2),
                "197 318 210 317 0");

            UWRules lORules = UWGameProfiles.Uw2.CreateOriginalRules();

            Check.Str("uw2 rules: a meal's sixths, an interrupted night's fatigue",
                lORules.Vitals.Meal.BlocksPerPoint + " " + lORules.Sleep.Relief.InterruptedFatigue, "6 24");
        }

        /// <summary>uw2 player_is_dead: the ninth, Britannia against the worlds below, and do_gem's place.</summary>
        private static void fCheckDeathUw2()
        {
            Check.Str("uw2 death: Britannia's last level, the next, a ninth of 1000",
                UWDeathRulesUw2.Decide(8) + " " + UWDeathRulesUw2.Decide(9) + " " + UWDeathRulesUw2.ExperienceLoss(1000), "Final Gem 111");

            int liX = UWDeathRulesUw2.GemTileX;
            int liY = UWDeathRulesUw2.GemTileY;

            UWDeathRulesUw2.MoveAlong(0x40, UWDeathRulesUw2.GemDistance, ref liX, ref liY);
            Check.Str("uw2 death: move_along at heading 0x40, the cosine's whole way, a gain of one", liX + " " + liY, "36 40");

            bool lbNear = true;

            for (int liHeading = 0; liHeading < 256; liHeading++)
            {
                int liAtX = UWDeathRulesUw2.GemTileX;
                int liAtY = UWDeathRulesUw2.GemTileY;

                UWDeathRulesUw2.MoveAlong(liHeading, UWDeathRulesUw2.GemDistance, ref liAtX, ref liAtY);
                lbNear &= System.Math.Abs(liAtX - UWDeathRulesUw2.GemTileX) <= 8 && System.Math.Abs(liAtY - UWDeathRulesUw2.GemTileY) <= 8;
            }

            Check.Bool("uw2 death: every heading stays within eight tiles", lbNear, true);
        }

        /// <summary>The second game's player steps (uw2 SKILLCHK.C, PLAYTIME.C): the gate, the halving by world,
        /// the long cycle and the skill point.</summary>
        private static void fCheckPlayerUw2()
        {
            IUWPlayerVariant lIPlayer = UWGameProfiles.Uw2.Player;
            UWRules lORules = UWGameProfiles.Uw2.CreateOriginalRules();

            Check.Str("uw2 player: the level's line, no orb's lair", lIPlayer.LevelUpString + " " + lIPlayer.HasOrbLair, "162 False");
            Check.Str("uw2 player: a mapped tile counts by the world (first of the world, last, next)",
                lIPlayer.ExplorationWeight(1) + " " + lIPlayer.ExplorationWeight(7) + " " + lIPlayer.ExplorationWeight(8), "1 1 2");
            Check.Str("uw2 player: the long cycle, the skill point, the gate below the cap, above it and past the ceiling",
                lIPlayer.LongCycleTicks + " " + lORules.Experience.PerSkillPoint + " " + lIPlayer.ExperienceGate(lORules.Experience, 1000, 10)
                + " " + lIPlayer.ExperienceGate(lORules.Experience, lORules.Experience.AwardCap + 1, 10) + " " + lIPlayer.ExperienceGate(lORules.Experience, UWPlayerVariantUw2.ExperienceCeiling, 1),
                "30 1500 Count CountWithoutLevels Ignore");

            bool lbHalved = true;
            bool lbTwice = true;

            for (int liRoll = 0; liRoll < 200; liRoll++)
            {
                int liGain = lIPlayer.ExperienceGain(11, 1, 1);
                int liEarly = lIPlayer.ExperienceGain(10, 1, 5);

                lbHalved &= liGain == 5 || liGain == 6;
                lbTwice &= liEarly == 3;
            }

            Check.Str("uw2 player: a gain halved at random, and again plus one in a world too early for the level",
                lbHalved + " " + lbTwice, "True True");
        }

        /// <summary>The second game's valuable (TREASURE.C:103-107): the same range in every world, the last
        /// six types at most, never type 1, and the first world mostly the coin.</summary>
        private static void fCheckValuableRoll()
        {
            IUWObjectVariant lIObjects = UWGameProfiles.Uw2.Objects;
            bool lbInRange = true;

            foreach (int liLevel in new[] { 1, 8, 9, 57, 65, 80 })
            {
                for (int liRoll = 0; liRoll < 2000; liRoll++)
                {
                    int liType = lIObjects.RollValuableType(liLevel);

                    lbInRange &= liType >= 0 && liType <= 6 && liType != 1;
                }
            }

            Check.Bool("loot: the second game's valuable stays 0 or 2 to 6 on every level", lbInRange, true);
        }

        /// <summary>
        /// The save folder rule on made-up installations (2026-10-29): an older one keeps its saves in
        /// UNDEROMn; GOG's current installers in cloud_saves/UNDEROMn, which DOSBox lays over drive C:
        /// (it need not exist yet); an installation serves only the game whose UNDEROMn it has; the second
        /// game's saves are also found in its own product's installation beside the first's.
        /// </summary>
        /// <summary>
        /// THE STEAM RELEASE (2026-11-05) on a made-up library: the image as game.bin, found from the folder and its
        /// UNDEROM1; each game in its own folder ("Ultima Underworld I", "II") with its own UNDEROMn and no
        /// cloud_saves; the first game's saves found from the second's image beside it; libraryfolders.vdf read,
        /// the old numbered form as well, an app's size not taken for a path.
        /// </summary>
        private static void fCheckSteamInstall()
        {
            string lsRoot = Path.Combine(Path.GetTempPath(), "UWCheckSteam_" + System.Guid.NewGuid().ToString("N"));
            string lsFirst = UWGogInstall.SteamGameFolder(lsRoot, 0);
            string lsSecond = UWGogInstall.SteamGameFolder(lsRoot, 1);
            IUWGameProfile lOUw1 = UWGameProfiles.Uw1;
            IUWGameProfile lOUw2 = UWGameProfiles.Uw2;

            try
            {
                Directory.CreateDirectory(Path.Combine(lsFirst, lOUw1.SaveFolder, "SAVE1"));
                Directory.CreateDirectory(Path.Combine(lsSecond, lOUw2.SaveFolder));
                File.WriteAllBytes(Path.Combine(lsFirst, UWGogInstall.SteamImageFileName), new byte[0]);
                File.WriteAllBytes(Path.Combine(lsSecond, UWGogInstall.SteamImageFileName), new byte[0]);
                File.WriteAllLines(Path.Combine(lsFirst, "dosboxULTIMA1_single.conf"), new[] { "[autoexec]", "mount C \"..\"", "#mount C \"..\\cloud_saves\" -t overlay" });

                string lsImage = UWGogInstall.FindImage(lsFirst);
                string lsSecondImage = UWGogInstall.FindImage(Path.Combine(lsSecond, lOUw2.SaveFolder));

                Check.Str("steam: game.bin found from the folder and from its UNDEROM2; the dialog takes it",
                    fRelative(lsRoot, lsImage) + " " + fRelative(lsRoot, lsSecondImage) + " " + UWGameFolderBrowser.HasImage(lsFirst),
                    "steamapps/common/Ultima Underworld I/game.bin steamapps/common/Ultima Underworld II/game.bin True");
                Check.Str("steam: each game's saves in its own UNDEROMn, without cloud_saves; the first's from the second's image",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw1)) + " " + fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsSecondImage, lOUw2)) + " "
                    + fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsSecondImage, lOUw1)),
                    "steamapps/common/Ultima Underworld I/UNDEROM1 steamapps/common/Ultima Underworld II/UNDEROM2 steamapps/common/Ultima Underworld I/UNDEROM1");

                string lsVdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"228980\"\t\t\"1234\"\n\t\t}\n\t}\n"
                    + "\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t}\n}\n";
                string lsOld = "\"LibraryFolders\"\n{\n\t\"TimeNextStatsReport\"\t\t\"1\"\n\t\"1\"\t\t\"E:\\\\Games\\\\Steam\"\n}\n";

                Check.Str("steam: libraryfolders.vdf's paths, escapes made single; the old numbered form",
                    string.Join("|", UWGogInstall.ParseLibraryFolders(lsVdf)) + " " + string.Join("|", UWGogInstall.ParseLibraryFolders(lsOld)),
                    @"C:\Program Files (x86)\Steam|D:\SteamLibrary E:\Games\Steam");

                string[] lsCandidates = UWGogInstall.GetCandidateFolders(false);
                int liGogFirst = System.Array.IndexOf(lsCandidates, @"D:\Ultima Underworld");
                int liGogSecond = System.Array.IndexOf(lsCandidates, @"D:\Ultima Underworld 2");
                int liSteamFirst = System.Array.FindIndex(lsCandidates, lsAt => lsAt.EndsWith(@"steamapps\common\Ultima Underworld I"));
                int liSteamSecond = System.Array.FindIndex(lsCandidates, lsAt => lsAt.EndsWith(@"steamapps\common\Ultima Underworld II"));

                Check.Bool("steam: the libraries' folders searched, GOG's first game, Steam's, GOG's second, Steam's",
                    liGogFirst >= 0 && liSteamFirst > liGogFirst && liGogSecond > liSteamFirst && liSteamSecond > liGogSecond, true);
            }
            finally
            {
                try
                {
                    Directory.Delete(lsRoot, true);
                }
                catch (System.Exception)
                {
                }
            }
        }

        /// <summary>The help's documents (2026-11-05): kind and game by the file name, GOG's manual beside game.gog and
        /// Steam's extras, one of each kind per game, in the help's order.</summary>
        private static void fCheckGameDocuments()
        {
            string lsGot = string.Empty;

            foreach (string lsName in new[] { "Ultima_Underworld-Manual.pdf", "Ultima_Underworld_II-Manual.pdf", "Ultima_Underworld_I_Map.pdf",
                "Ultima_Underworld_II_A_Safe_Passage_Through_Britannia.pdf", "Ultima_Underworld_I_Memoirs_of_Cabirus.pdf", "Ultima_Underworld_II_Reference_Card.pdf",
                "ultima underworld 2 cluebook.PDF", "Ultima_Underworld_I_Manual.txt", "Notes.pdf" })
            {
                lsGot += UWGameDocuments.Classify(lsName, out UWGameDocuments.KindEnum leKind, out int liGame) ? leKind + "/" + liGame + " " : "- ";
            }

            Check.Str("documents: kind and game by name; a text file and a stranger's PDF are none", lsGot.Trim(),
                "Manual/1 Manual/2 Map/1 SafePassage/2 Memoirs/1 ReferenceCard/2 Cluebook/2 - -");

            string lsRoot = Path.Combine(Path.GetTempPath(), "UWCheckDocs_" + System.Guid.NewGuid().ToString("N"));

            try
            {
                string lsGog = Path.Combine(lsRoot, "Ultima Underworld");
                string lsSteam = Path.Combine(lsRoot, "Ultima Underworld I");

                Directory.CreateDirectory(lsGog);
                Directory.CreateDirectory(Path.Combine(lsSteam, UWGameDocuments.ExtrasFolder));

                foreach (string lsName in new[] { "Ultima_Underworld-Manual.pdf", "Ultima_Underworld_II-Manual.pdf" })
                    File.WriteAllBytes(Path.Combine(lsGog, lsName), new byte[0]);

                foreach (string lsName in new[] { "Ultima_Underworld_I_Cluebook.pdf", "Ultima_Underworld_I_Manual.pdf", "Ultima_Underworld_I_Map.pdf",
                    "Ultima_Underworld_I_Memoirs_of_Cabirus.pdf", "Ultima_Underworld_I_Reference_Card.pdf" })
                    File.WriteAllBytes(Path.Combine(lsSteam, UWGameDocuments.ExtrasFolder, lsName), new byte[0]);

                string fList(List<UWGameDocuments.Document> pODocuments)
                {
                    string lsList = string.Empty;

                    foreach (UWGameDocuments.Document lODocument in pODocuments)
                        lsList += lODocument.Kind + "=" + fRelative(lsRoot, lODocument.Path) + (lODocument.IsSpoiler ? "!" : string.Empty) + " ";

                    return lsList.Trim();
                }

                Check.Str("documents: the first game's from GOG and Steam, one of each, GOG's manual first",
                    fList(UWGameDocuments.Find(new[] { lsGog, lsSteam }, 1)),
                    "Manual=Ultima Underworld/Ultima_Underworld-Manual.pdf ReferenceCard=Ultima Underworld I/Extras/Ultima_Underworld_I_Reference_Card.pdf "
                    + "Map=Ultima Underworld I/Extras/Ultima_Underworld_I_Map.pdf Memoirs=Ultima Underworld I/Extras/Ultima_Underworld_I_Memoirs_of_Cabirus.pdf "
                    + "Cluebook=Ultima Underworld I/Extras/Ultima_Underworld_I_Cluebook.pdf!");
                string lsGoodies = Path.Combine(lsRoot, "Gog", "Ultima_Underworld_1_2_cluebook");

                Directory.CreateDirectory(lsGoodies);
                File.WriteAllBytes(Path.Combine(lsGoodies, "Ultima Underworld - Cluebook .pdf"), new byte[0]);
                File.WriteAllBytes(Path.Combine(lsGoodies, "Ultima Underworld II - Cluebook .pdf"), new byte[0]);
                File.WriteAllBytes(Path.Combine(lsRoot, "Gog", "Ultima Underworld 2 - Quick Reference Card.pdf"), new byte[0]);
                File.WriteAllBytes(Path.Combine(lsRoot, "Gog", "Ultima Underworld - Map .pdf"), new byte[0]);

                Check.Str("documents: GOG's goodies as unpacked, the PDFs beside the image or in their ZIP's folder",
                    fList(UWGameDocuments.Find(new[] { Path.Combine(lsRoot, "Gog") }, 1)) + " | " + fList(UWGameDocuments.Find(new[] { Path.Combine(lsRoot, "Gog") }, 2)),
                    "Map=Gog/Ultima Underworld - Map .pdf Cluebook=Gog/Ultima_Underworld_1_2_cluebook/Ultima Underworld - Cluebook .pdf! | "
                    + "ReferenceCard=Gog/Ultima Underworld 2 - Quick Reference Card.pdf Cluebook=Gog/Ultima_Underworld_1_2_cluebook/Ultima Underworld II - Cluebook .pdf!");
                Check.Str("documents: the second game's, and Steam's alone",
                    fList(UWGameDocuments.Find(new[] { lsGog, lsSteam }, 2)) + " | " + fList(UWGameDocuments.Find(new[] { lsSteam }, 1).GetRange(0, 1)),
                    "Manual=Ultima Underworld/Ultima_Underworld_II-Manual.pdf | Manual=Ultima Underworld I/Extras/Ultima_Underworld_I_Manual.pdf");
            }
            finally
            {
                try
                {
                    Directory.Delete(lsRoot, true);
                }
                catch (System.Exception)
                {
                }
            }
        }

        private static void fCheckSaveFolderRule()
        {
            string lsRoot = Path.Combine(Path.GetTempPath(), "UWCheckSaves_" + System.Guid.NewGuid().ToString("N"));
            string lsFirst = Path.Combine(lsRoot, "Ultima Underworld");
            string lsSecond = Path.Combine(lsRoot, "Ultima Underworld 2");
            string lsImage = Path.Combine(lsFirst, UWGogInstall.ImageFileName);
            string lsSecondImage = Path.Combine(lsSecond, UWGogInstall.ImageFileName);
            IUWGameProfile lOUw1 = UWGameProfiles.Uw1;
            IUWGameProfile lOUw2 = UWGameProfiles.Uw2;

            try
            {
                Directory.CreateDirectory(Path.Combine(lsFirst, lOUw1.SaveFolder));
                File.WriteAllBytes(lsImage, new byte[0]);

                Check.Str("saves: an older installation keeps them in UNDEROM1; the second game has none there",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw1)) + " " + fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw2)),
                    "Ultima Underworld/UNDEROM1 none");

                Directory.CreateDirectory(Path.Combine(lsFirst, UWGogInstall.CloudSavesFolderName));

                // GOG's configuration with the overlay commented out (a DOSBox setup of the user's own,
                // 2026-10-29), then mounting it.
                string lsConf = Path.Combine(lsFirst, "dosboxULTIMA1_single.conf");

                File.WriteAllLines(lsConf, new[] { "[autoexec]", "mount C \"..\"", "#mount C \"..\\cloud_saves\" -t overlay" });

                Check.Str("saves: automatic without a save and with the overlay commented out stays in UNDEROM1",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw1)), "Ultima Underworld/UNDEROM1");

                File.WriteAllLines(lsConf, new[] { "[autoexec]", "mount C \"..\"", "mount c \"..\\cloud_saves\" -t overlay" });

                Check.Str("saves: a current installer's go to cloud_saves/UNDEROM1 (DOSBox's overlay; it need not exist yet)",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw1)), "Ultima Underworld/cloud_saves/UNDEROM1");

                // The newest save decides over the configuration; the two switches take their folder.
                Directory.CreateDirectory(Path.Combine(lsFirst, lOUw1.SaveFolder, "SAVE2"));
                File.WriteAllBytes(Path.Combine(lsFirst, lOUw1.SaveFolder, "SAVE2", "PLAYER.DAT"), new byte[1]);
                Directory.CreateDirectory(Path.Combine(lsFirst, UWGogInstall.CloudSavesFolderName, lOUw1.SaveFolder, "SAVE1"));
                File.WriteAllBytes(Path.Combine(lsFirst, UWGogInstall.CloudSavesFolderName, lOUw1.SaveFolder, "SAVE1", "PLAYER.DAT"), new byte[1]);
                File.SetLastWriteTimeUtc(Path.Combine(lsFirst, UWGogInstall.CloudSavesFolderName, lOUw1.SaveFolder, "SAVE1", "PLAYER.DAT"), new System.DateTime(2026, 1, 1));
                File.SetLastWriteTimeUtc(Path.Combine(lsFirst, lOUw1.SaveFolder, "SAVE2", "PLAYER.DAT"), new System.DateTime(2026, 2, 1));

                Check.Str("saves: automatic follows the newest save (UNDEROM1's), the switches cloud_saves and the installation; the modes by name",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw1)) + " "
                    + fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw1, UWGogInstall.SaveFolderModeEnum.CloudSaves)) + " "
                    + fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw1, UWGogInstall.SaveFolderModeEnum.Installation)) + " "
                    + UWGogInstall.ParseSaveFolderMode(string.Empty) + " " + UWGogInstall.ParseSaveFolderMode("installation"),
                    "Ultima Underworld/UNDEROM1 Ultima Underworld/cloud_saves/UNDEROM1 Ultima Underworld/UNDEROM1 Automatic Installation");

                Directory.Delete(Path.Combine(lsFirst, lOUw1.SaveFolder, "SAVE2"), true);
                Check.Str("saves: ... and the first game's installation holds no saves of the second (it has no UNDEROM2)",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw2)), "none");

                Directory.CreateDirectory(Path.Combine(lsSecond, lOUw2.SaveFolder));
                Directory.CreateDirectory(Path.Combine(lsSecond, UWGogInstall.CloudSavesFolderName));
                File.WriteAllBytes(lsSecondImage, new byte[0]);
                File.WriteAllLines(Path.Combine(lsSecond, "dosboxULTIMA2_single.conf"), new[] { "[autoexec]", "mount C \"..\\cloud_saves\" -t overlay" });

                Check.Str("saves: the second game's are found in its own product beside the first's, from either image",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw2)) + " " + fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsSecondImage, lOUw2)),
                    "Ultima Underworld 2/cloud_saves/UNDEROM2 Ultima Underworld 2/cloud_saves/UNDEROM2");

                Directory.CreateDirectory(Path.Combine(lsRoot, "Elsewhere", lOUw2.SaveFolder));
                File.WriteAllBytes(Path.Combine(lsRoot, "Elsewhere", UWGogInstall.ImageFileName), new byte[0]);

                Check.Str("saves: the folder chosen for the second game comes first, then the image's, then the others given",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw2, UWGogInstall.SaveFolderModeEnum.Automatic, Path.Combine(lsRoot, "Elsewhere"), null)) + " "
                    + fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsSecondImage, lOUw1, UWGogInstall.SaveFolderModeEnum.Automatic, string.Empty, null)),
                    "Elsewhere/UNDEROM2 Ultima Underworld/cloud_saves/UNDEROM1");

                Directory.Delete(lsSecond, true);
                Check.Str("saves: ... the others given are asked last",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsImage, lOUw2, UWGogInstall.SaveFolderModeEnum.Automatic, string.Empty, new[] { Path.Combine(lsRoot, "Nowhere"), Path.Combine(lsRoot, "Elsewhere") })),
                    "Elsewhere/UNDEROM2");

                string[] lsCandidates = UWGogInstall.GetCandidateFolders(false);

                Check.Bool("install: the second game's own folders are searched, after the first's",
                    System.Array.IndexOf(lsCandidates, @"D:\Ultima Underworld 2") > System.Array.IndexOf(lsCandidates, @"D:\Ultima Underworld")
                    && System.Array.IndexOf(lsCandidates, @"D:\Ultima Underworld") >= 0, true);
            }
            finally
            {
                try
                {
                    Directory.Delete(lsRoot, true);
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>A path below psRoot with forward slashes, "none" for null.</summary>
        private static string fRelative(string psRoot, string psPath)
        {
            return psPath == null ? "none" : Path.GetRelativePath(psRoot, psPath).Replace(Path.DirectorySeparatorChar, '/');
        }

        /// <summary>
        /// The second game's main menu screen (phase 0 step 5): BYT.ARK's 11 blocks with 3 and 10
        /// empty, block 5 a compressed full screen whose bytes are the ones an independent decoder (a
        /// scratch Python script after UE's FORMATS.md, 2026-10-28; the picture checked by eye: the
        /// title over grey stone) gave; the screen in palette 2, every pixel opaque.
        /// </summary>
        private static void fCheckSecondGameScreens(string psData)
        {
            UWArk lOArk = UWArk.Read(Path.Combine(psData, UWScreensUw2.ArchiveName), UWArk.LayoutEnum.Uw2);
            byte[] lyMenu = lOArk.ReadBlock(UWScreensUw2.MainMenuBlock);

            Check.Str("screens: BYT.ARK's blocks (count, 3 and 10 empty, the menu compressed, its length)",
                lOArk.BlockCount + " " + lOArk.ReadBlock(3).Length + " " + lOArk.ReadBlock(10).Length + " "
                + lOArk.IsCompressed(UWScreensUw2.MainMenuBlock) + " " + lyMenu.Length, "11 0 0 True 64000");
            Check.Str("screens: the main menu screen decodes to the independent decoder's bytes", fSha1(lyMenu),
                "d856857852124670470baa53f977dd2e6d96ab2a");

            UWPicture lOScreen = UWScreensUw2.MainMenu(psData);

            Check.Bool("screens: the main menu screen is 320 x 200, opaque", lOScreen != null && lOScreen.Width == 320 && lOScreen.Height == 200
                && System.Array.TrueForAll(lOScreen.Pixels, lOPixel => lOPixel.A == 255), true);
        }

        private static string fSha1(byte[] pyBytes)
        {
            using (System.Security.Cryptography.SHA1 lOSha = System.Security.Cryptography.SHA1.Create())
                return System.BitConverter.ToString(lOSha.ComputeHash(pyBytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>The second game with a program of another build: the image must not count as holding it.</summary>
        private sealed class OtherBuildProfile : IUWGameProfile
        {
            private readonly IUWGameProfile mOUw2 = UWGameProfiles.Uw2;

            public UWGame Game => mOUw2.Game;

            public string Title => mOUw2.Title;

            public string ImageFolder => mOUw2.ImageFolder;

            public string Executable => mOUw2.Executable;

            public long ExecutableSize => mOUw2.ExecutableSize;

            public string ExecutableSha1 => "0000000000000000000000000000000000000000";

            public string CacheFolder => mOUw2.CacheFolder;

            public string SaveFolder => mOUw2.SaveFolder;

            public string GameFolder => mOUw2.GameFolder;

            public bool IsPlayable => mOUw2.IsPlayable;

            public UWArk.LayoutEnum ArchiveLayout => mOUw2.ArchiveLayout;

            public UWLevArkFile LevArk => mOUw2.LevArk;

            public UW3DModelLayout Models => mOUw2.Models;

            public UWRules CreateOriginalRules() => mOUw2.CreateOriginalRules();

            public IUWMagicVariant Magic => mOUw2.Magic;

            public IUWCombatVariant Combat => mOUw2.Combat;

            public IUWPlayerVariant Player => mOUw2.Player;

            public IUWCritterVariant Critter => mOUw2.Critter;

            public IUWObjectVariant Objects => mOUw2.Objects;

            public IUWTrapVariant Traps => mOUw2.Traps;

            public IUWMotionVariant Motion => mOUw2.Motion;

            public IUWInventoryVariant Inventory => mOUw2.Inventory;

            public IUWConversationVariant Conversation => mOUw2.Conversation;

            public UWTrapTable TrapKinds => mOUw2.TrapKinds;

            public UWDoActionTable DoActions => mOUw2.DoActions;

            public UWConversationFunctionTable ConversationFunctions => mOUw2.ConversationFunctions;
        }

        /// <summary>The rune cast asks the game's magic variant (phase 0 step 2): a game whose variant
        /// refuses the place blocks the cast; one whose variant allows it casts, and the variant was
        /// asked; the first game's variant pays on release for projectiles only.</summary>
        private static void fCheckMagicVariantAsked(CheckContext pOContext)
        {
            UWGameSession lOBefore = UWGameSession.Current;
            TestProfile lOProfile = new TestProfile();

            try
            {
                UWGameData lOData = new UWGameData(pOContext.Data.GameData.DataPath, null, lOProfile);

                foreach (bool lbAllow in new[] { false, true })
                {
                    UWGameSession.MakeCurrent(new UWGameSession(lOData, 1));
                    lOProfile.Spy.Allow = lbAllow;
                    lOProfile.Spy.Asked = 0;

                    FakeSpellHost lOHost = new FakeSpellHost(new FakeWorld(), UWCastContext.ThePlayer) { PlayerLevel = 1, GameTicks = 256000L };

                    Check.Str("profile: In Lor where the game's magic " + (lbAllow ? "allows" : "refuses") + " the place",
                        UWSpellCasting.CastRunes(8, 11, UWRunicMagic.EmptyRune, lOHost) + ", asked " + (lOProfile.Spy.Asked > 0),
                        (lbAllow ? "Cast" : "Blocked") + ", asked True");
                }

                // Combat: each step answers what the test game's variant says.
                UWAttackerStats lOPoisoner = new UWAttackerStats(5, 40, 7, 128, 0, 0, false, 0x14);
                UWAttackOutcome lOHit = new UWAttackOutcome(UWSkillCheck.ResultEnum.Success, UWDefenderKind.Player, 0, 3, 3,
                    UWAttackResolution.NoSound, 5, false, UWDamageTypes.Physical, UWDamageRequest.CreatureBlow(3, 0, false));

                // The player: the experience asks the game's gate and gain.
                UWExperience.State lOState = new UWExperience.State { Level = 1 };
                int liLevels = UWExperience.Change(ref lOState, 100, 5);

                Check.Str("profile: the experience asks the game (gate without levels, gain times ten)", lOState.Experience + " " + liLevels, "1000 0");
                Check.Str("profile: the trap targets are the game's", UWGameSession.Current.Game.Player.TrapDetectTarget() + " " + UWGameSession.Current.Game.Player.TrapDisarmTarget(), "99 98");

                // Objects: a pick of skill 1 against height 2 opens because the test game says so.
                UWLockRules.PickResult lOPick = UWLockRules.PickLock(fMakeDoorLock(5, 2, true, true, out UWObject _), null, null, 1);

                Check.Str("profile: the pick asks the game's roll", lOPick.Unlocked + ", asked " + (((SpyObjects)lOProfile.Objects).Asked > 0), "True, asked True");

                // Conversations: a hostile creature attacking the player answers when the game says so.
                Check.Bool("profile: who answers asks the game (a hostile attacker)", UWClickRules.CreatureAnswers(5, 5, 1, UWNpc.AttitudeHostile, false), true);

                // Motion: a sure lava roll burns through the boots when the game says so.
                Check.Int("profile: the lava asks the game (boots on, a sure roll)", new UWPlayerTerrainRules().TickLava(0, true, true, 8, 1.0), 1);

                Check.Str("profile: the combat steps ask the game (unresisted, shrugs off, debris, poison)",
                    UWDamageTypes.Scale(0, 10, UWDamageTypes.Physical) + " " + UWObjectDamageRules.ShrugsOff(false, 0) + " "
                    + UWObjectDamageRules.RollDebrisObjectId() + " " + UWAttackResolution.PoisonNibble(lOPoisoner, lOHit, 0, 0),
                    "77 True 511 -1");

                // Phase 0 step 3: a fired trap runs the kind from its game's table (the test game's text
                // trap is a spy), a conversation function comes from its game's table.
                List<UWObject> lOMaster = pOContext.Data.Levels[0].Masterlist;
                UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId) { Quantity = (ushort)lOMaster.Count, Flags = 6 };

                lOMaster.Add(new UWObject((ushort)UWObjectMechanics.TextStringTrapId));

                try
                {
                    UWTrapRules.FireTraps(lOTrigger, null, new FakeTrapHost(pOContext.Data, 0));
                }
                finally
                {
                    lOMaster.RemoveAt(lOMaster.Count - 1);
                }

                Check.Int("profile: a fired trap runs its game's table (the text trap a spy)", lOProfile.TrapKindsRun, 1);

                UWConversationSession lOTalk = new UWConversationSession(pOContext.Data, null, 1, null, UWGameSession.Current);

                Check.Str("profile: a conversation function comes from its game's table (a spy, and setup_to_barter missing there)",
                    lOTalk.CallUnknown(SpyFunction, 0, new int[0], new int[0], null) + " " + lOTalk.MissingFunctions.Contains(SpyFunction) + " "
                    + lOTalk.CallUnknown("setup_to_barter", 0, new int[0], new int[0], null) + " " + lOTalk.MissingFunctions.Contains("setup_to_barter"),
                    "42 False 0 True");
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }

            IUWMagicVariant lIUw1 = UWGameProfiles.Uw1.Magic;
            int liProjectiles = 0;
            int liOthers = 0;

            foreach (UWRunicMagic.Spell lOSpell in UWRunicMagic.AllSpells)
            {
                if (lIUw1.PaysOnRelease(lOSpell) != (lOSpell.MajorClass == UWSpellCasting.ProjectileMajorClass))
                    liOthers++;
                else if (lOSpell.MajorClass == UWSpellCasting.ProjectileMajorClass)
                    liProjectiles++;
            }

            Check.Bool("profile: the first game pays on release for its projectiles only (" + liProjectiles + " of them)", liOthers == 0 && liProjectiles > 0, true);

            // The first game's weapon enchantment: class 0xC, (minor & 7) + 1 by bit 3; another class nothing.
            IUWCombatVariant lICombat = UWGameProfiles.Uw1.Combat;
            string lsEnchant = string.Empty;

            foreach (int[] liClass in new[] { new[] { 0xC, 3 }, new[] { 0xC, 0xB }, new[] { 5, 3 } })
            {
                lICombat.WeaponEnchantment(liClass[0], liClass[1], out int liAccuracy, out int liDamage);
                lsEnchant += (lsEnchant.Length > 0 ? " " : string.Empty) + liAccuracy + "/" + liDamage;
            }

            Check.Str("profile: the first game's weapon enchantments (0xC/3, 0xC/0xB, 5/3)", lsEnchant, "4/0 0/4 0/0");

            // The first game's player steps.
            IUWPlayerVariant lIPlayer = UWGameProfiles.Uw1.Player;
            UWRules lORules = UWRules.CreateOriginal();

            Check.Str("profile: the first game's experience gate below, at and above the cap",
                lIPlayer.ExperienceGate(lORules.Experience, lORules.Experience.AwardCap, 10) + " " + lIPlayer.ExperienceGate(lORules.Experience, lORules.Experience.AwardCap + 1, 10),
                "Count Ignore");
            Check.Str("profile: the first game's gain on level 2 for a level 6 and a level 7 character", lIPlayer.ExperienceGain(100, 2, 6) + " " + lIPlayer.ExperienceGain(100, 2, 7), "100 51");
            Check.Str("profile: the first game's sleep on level 8 and in the Void, its trap targets",
                lIPlayer.MaySleepAt(lORules, 8) + " " + lIPlayer.MaySleepAt(lORules, 9) + " " + lIPlayer.TrapDetectTarget() + " " + lIPlayer.TrapDisarmTarget(), "True False 8 8");

            // The creatures: the dying brain cries what its game says; the first game's steps.
            FakeCritterHost lOCritterHost = new FakeCritterHost { Game = new TestProfile() };
            UWCritterBrain lOBrain = fMakeBrain(fMakeRecord(5, 5, 0, 2, -1, -1, 3), fMakeRow(), 5, lOCritterHost);

            lOBrain.OnDamaged(lOCritterHost, 1, 5);
            Check.Bool("profile: a dying creature cries its game's death cry", lOCritterHost.HasEvent("sound 77"), true);

            IUWCritterVariant lICritter = UWGameProfiles.Uw1.Critter;
            UWObjectClassProperties.Critter lOCrier = fMakeRow();

            lOCrier.Row[8] = 0x09;

            Check.Str("profile: the first game's door (hostile, friendly) and death cry (byte 8 & 7 of 1, of 0)",
                lICritter.TriesDoor(UWNpc.AttitudeHostile) + " " + lICritter.TriesDoor(2) + " " + lICritter.DeathCry(lOCrier) + " " + lICritter.DeathCry(fMakeRow()),
                "True False " + UWCritterRules.DeathSound + " -1");

            // The first game's item and trap steps; its valuable roll and bite on a fixed seed.
            IUWObjectVariant lIObjects = UWGameProfiles.Uw1.Objects;
            IUWTrapVariant lITraps = UWGameProfiles.Uw1.Traps;

            Check.Str("profile: the first game's lights (quality 0, 1) and door trap lock (owner 0, 5)",
                lIObjects.LightUsedUp(0) + " " + lIObjects.LightUsedUp(1) + " " + lITraps.SwapsLock(0) + " " + lITraps.SwapsLock(5), "True False True True");

            string lsRolls = string.Empty;

            UWRandom.Use(new UWSessionRandom(7));

            try
            {
                for (int liLevel = 1; liLevel <= 9; liLevel++)
                    lsRolls += lIObjects.RollValuableType(liLevel) + " ";
            }
            finally
            {
                UWRandom.Use(null);
            }

            Check.Bool("profile: the first game's valuables lie within 0 to 6 on every level (" + lsRolls.Trim() + ")",
                System.Array.TrueForAll(lsRolls.Trim().Split(' '), lsRoll => int.Parse(lsRoll) >= 0 && int.Parse(lsRoll) <= 6), true);

            // The first game's motion steps: tenths (forward 100 swimming, on lava), the boots, the impact.
            IUWMotionVariant lIMotion = UWGameProfiles.Uw1.Motion;
            UWRulesMotion.SpeedRules lOSpeed = lORules.Motion.Speed;

            Check.Str("profile: the first game's speed shares (100 swimming, on lava), the boots (on, off), impact 0x100, 0x101, 0x3FF",
                lIMotion.ScaleSpeed(lOSpeed, 100, 1, 30) + " " + lIMotion.ScaleSpeed(lOSpeed, 100, 2, 30) + " " + lIMotion.LavaSpares(true) + " " + lIMotion.LavaSpares(false) + " "
                + lIMotion.ImpactDamage(0x100, false, 10) + " " + lIMotion.ImpactDamage(0x101, true, 10) + " " + lIMotion.ImpactDamage(0x3FF, false, 10),
                "30 50 True False 0 1 3");
            Check.Str("profile: the first game's turn in every state (swimming, lava), the view's follow 0x400, falls hurt on level 9",
                lIMotion.ScaleTurn(0x120, 1) + " " + lIMotion.ScaleTurn(0x120, 2) + " " + lIMotion.FacingFollow.ToString("X") + " " + lIMotion.FallHurts(9),
                "288 288 400 True");

            // The second game's motion (UW2 plan phase 2 step 4; Underworld Exhumed uw2 motion/PHYSICS.C
            // newFPS, :591, :604; OBJPHYS.C:299): twentieths, swimming by the skill, the turn's share below
            // levitation, 0x600, no fall damage on 65-72, lava for all, the impact above 0x180.
            IUWMotionVariant lIMotion2 = UWGameProfiles.Uw2.Motion;

            Check.Str("profile: the second game's speeds of 100 (foot, swimming at skill 0 and 30, lava, levitating, flying, slow fall)",
                lIMotion2.ScaleSpeed(lOSpeed, 100, 0, 0) + " " + lIMotion2.ScaleSpeed(lOSpeed, 100, 1, 0) + " " + lIMotion2.ScaleSpeed(lOSpeed, 100, 1, 30) + " "
                + lIMotion2.ScaleSpeed(lOSpeed, 100, 2, 0) + " " + lIMotion2.ScaleSpeed(lOSpeed, 100, 4, 0) + " " + lIMotion2.ScaleSpeed(lOSpeed, 100, 5, 0) + " "
                + lIMotion2.ScaleSpeed(lOSpeed, 100, 6, 0), "100 20 95 70 5 70 20");
            Check.Str("profile: the second game's turn of 100 (foot, swimming, lava, ice, levitating), the follow, falls on 64, 65, 72, 73",
                lIMotion2.ScaleTurn(100, 0) + " " + lIMotion2.ScaleTurn(100, 1) + " " + lIMotion2.ScaleTurn(100, 2) + " " + lIMotion2.ScaleTurn(100, 3) + " "
                + lIMotion2.ScaleTurn(100, 4) + " " + lIMotion2.FacingFollow.ToString("X") + " " + lIMotion2.FallHurts(64) + " " + lIMotion2.FallHurts(65) + " "
                + lIMotion2.FallHurts(72) + " " + lIMotion2.FallHurts(73), "100 30 70 100 100 600 True False False True");
            // munge_vectors (part C): no strength keeps the first; full ice strength takes the second (100
            // east as 15/16 of it, the table's 0x7FFF / 0x800 = 15: 93); a current of 0x8D east added to 100
            // north (vector 2115, 1500, a sixteenth 132, 93: speed 161, heading NE).
            UWMotionVariantUw2.MungeVectors(UWMotionVariantUw2.MungeIce, 0x1234, 50, 0x4000, 100, 0, out int liH0, out int liS0);
            UWMotionVariantUw2.MungeVectors(UWMotionVariantUw2.MungeIce, 0, 100, 0x4000, 100, 0x40, out int liH1, out int liS1);
            UWMotionVariantUw2.MungeVectors(UWMotionVariantUw2.MungeAdd, 0, 100, 0x4000, 0x8D, 0x20, out int liH2, out int liS2);

            Check.Str("profile: munge_vectors - none, full ice to east, a current east on a run north (heading speed)",
                liH0.ToString("X4") + " " + liS0 + " " + liH1.ToString("X4") + " " + liS1 + " " + liS2 + " " + (liH2 > 0x2400 && liH2 < 0x2A00),
                "1234 50 4000 93 161 True");

            Check.Str("profile: the second game's lava (boots on), impact 0x180, 0x200 object, creature, fragile object",
                lIMotion2.LavaSpares(true) + " " + lIMotion2.ImpactDamage(0x180, false, 40) + " " + lIMotion2.ImpactDamage(0x200, false, 40) + " "
                + lIMotion2.ImpactDamage(0x400, true, 40) + " " + lIMotion2.ImpactDamage(0x200, false, 0x1F), "False 0 2 1 8");

            // The first game's conversation steps.
            IUWConversationVariant lIConversation = UWGameProfiles.Uw1.Conversation;

            Check.Str("profile: the first game's talkers (0x16, 0x8E, 0xE7, 0x8C), demands (3 vs 2, 2 vs 2, 2 vs 2 ally), attitude (0, 1)",
                lIConversation.AlwaysAnswers(0x16) + " " + lIConversation.AlwaysAnswers(0x8E) + " " + lIConversation.AlwaysAnswers(0xE7) + " " + lIConversation.AlwaysAnswers(0x8C) + " "
                + lIConversation.DemandGivesIn(3, 2, false) + " " + lIConversation.DemandGivesIn(2, 2, false) + " " + lIConversation.DemandGivesIn(2, 2, true) + " "
                + lIConversation.GivingInLowersAttitude(0) + " " + lIConversation.GivingInLowersAttitude(1),
                "True True True False True False True False True");
            Check.Bool("profile: ... and a hostile attacker does not answer in the first game", UWClickRules.CreatureAnswers(5, 5, 1, UWNpc.AttitudeHostile, false), false);

            // The first game's tables: built once for its profile, sealed, and the spy is not in them.
            IUWGameProfile lOFirst = UWGameProfiles.Uw1;

            Check.Bool("profile: the first game's tables are built once (the same instance on every read)",
                lOFirst.TrapKinds == lOFirst.TrapKinds && lOFirst.DoActions == lOFirst.DoActions && lOFirst.ConversationFunctions == lOFirst.ConversationFunctions, true);
            Check.Str("profile: the first game's text trap, a_do 2 (the camera) and setup_to_barter are handled, the spy function is not",
                lOFirst.TrapKinds.IsHandled(UWObjectMechanics.TextStringTrapId) + " " + lOFirst.DoActions.IsHandled(UWObjectMechanics.DoTrapCameraAction) + " "
                + lOFirst.ConversationFunctions.Contains("setup_to_barter") + " " + lOFirst.ConversationFunctions.Contains(SpyFunction),
                "True True True False");
        }

        /// <summary>The test game's one conversation function.</summary>
        private const string SpyFunction = "spy_function";

        /// <summary>A game like the first whose magic is a spy that may refuse the place.</summary>
        private sealed class TestProfile : IUWGameProfile
        {
            public readonly SpyMagic Spy = new SpyMagic();

            /// <summary>How often the test game's own trap table ran a kind.</summary>
            public int TrapKindsRun;

            private readonly UWGame meGame;

            /// <summary>A test game like the first; as peGame (the second) it says so and has a game
            /// folder of its own.</summary>
            public TestProfile(UWGame peGame = UWGame.Uw1)
            {
                meGame = peGame;

                TrapKinds = new UWTrapTable().Register(UWObjectMechanics.TextStringTrapId, "spy", (pOTrap, pOTrigger, pOSwitch, pIHost) =>
                {
                    TrapKindsRun++;

                    return true;
                });
                TrapKinds.Seal();

                ConversationFunctions = new UWConversationFunctionTable()
                    .Register(SpyFunction, UWConversationFunctionTable.Provider.Session, (pOSession, pOCall) => 42);
                ConversationFunctions.Seal();
            }

            private readonly IUWGameProfile mOUw1 = UWGameProfiles.Uw1;

            public UWGame Game => meGame;

            public string Title => "test";

            public string ImageFolder => mOUw1.ImageFolder;

            public string Executable => mOUw1.Executable;

            public long ExecutableSize => mOUw1.ExecutableSize;

            public string ExecutableSha1 => mOUw1.ExecutableSha1;

            public string CacheFolder => mOUw1.CacheFolder;

            public string SaveFolder => mOUw1.SaveFolder;

            public string GameFolder => meGame == UWGame.Uw1 ? mOUw1.GameFolder : "UW2";

            public bool IsPlayable => true;

            public UWArk.LayoutEnum ArchiveLayout => meGame == UWGame.Uw1 ? UWArk.LayoutEnum.Uw1 : UWArk.LayoutEnum.Uw2;

            public UWLevArkFile LevArk => meGame == UWGame.Uw1 ? UWLevArkFile.Uw1 : UWLevArkFile.Uw2;

            public UW3DModelLayout Models => meGame == UWGame.Uw1 ? UW3DModelLayout.Uw1 : UW3DModelLayout.Uw2;

            public UWRules CreateOriginalRules() => mOUw1.CreateOriginalRules();

            public IUWMagicVariant Magic => Spy;

            public IUWCombatVariant Combat { get; } = new SpyCombat();

            public IUWPlayerVariant Player { get; } = new SpyPlayer();

            public IUWCritterVariant Critter { get; } = new SpyCritter();

            public IUWObjectVariant Objects { get; } = new SpyObjects();

            public IUWTrapVariant Traps { get; } = new SpyTraps();

            public IUWMotionVariant Motion { get; } = new SpyMotion();

            public IUWInventoryVariant Inventory { get; } = new UWInventoryVariantUw1();

            public IUWConversationVariant Conversation { get; } = new SpyConversation();

            public UWTrapTable TrapKinds { get; }

            public UWDoActionTable DoActions => mOUw1.DoActions;

            public UWConversationFunctionTable ConversationFunctions { get; }
        }

        /// <summary>A conversation variant whose steps give themselves away: everyone answers.</summary>
        private sealed class SpyConversation : IUWConversationVariant
        {
            public bool AlwaysAnswers(int piWhoami) => true;

            public bool DemandGivesIn(int piPlayerScore, int piNpcScore, bool pbAlly) => false;

            public bool GivingInLowersAttitude(int piAttitude) => false;
        }

        /// <summary>A motion variant whose steps give themselves away: the lava spares nobody.</summary>
        private sealed class SpyMotion : IUWMotionVariant
        {
            public int ScaleSpeed(UWRulesMotion.SpeedRules pORules, int piBase, int piState, int piSwimmingSkill) => piBase;

            public int ScaleTurn(int piTurnStep, int piState) => piTurnStep;

            public int FacingFollow => 0x400;

            public bool FallHurts(int piLevel) => true;

            public bool IceAndCurrents => false;

            public bool LavaSpares(bool pbDragonSkinBoots) => false;

            public int ImpactDamage(int piImpact, bool pbCreature, int piHitPoints) => 0;
        }

        /// <summary>An item variant whose steps give themselves away: every light is used up, the
        /// valuable is always the third, every pick succeeds.</summary>
        private sealed class SpyObjects : IUWObjectVariant
        {
            public int Asked;

            public bool LightUsedUp(int piQuality) => true;

            public int LookQuality(int piItemId, int piQuality) => piQuality;

            public int LastOwnerRace => 0;

            public int NameWordString(UWObject pOItem) => -1;

            public int NothingToSeeString => 0;

            public int DoorSpikedString => 0;

            public int BonesLeadString => 0;

            public int BurntOutQuality => 0;

            public int LightUsedUpString => 0;

            public int HandsFullString => 0;

            public int RollValuableType(int piDungeonLevel)
            {
                Asked++;

                return 2;
            }

            public UWSkillCheck.ResultEnum PickRoll(int piPicklock, int piLockHeight)
            {
                Asked++;

                return UWSkillCheck.ResultEnum.Success;
            }
        }

        /// <summary>A trap variant whose steps give themselves away.</summary>
        private sealed class SpyTraps : IUWTrapVariant
        {
            public bool Bites(int piTrackSkill) => piTrackSkill > 10;

            public bool SwapsLock(int piTrapOwner) => false;
        }

        /// <summary>A creature variant whose death cry gives itself away.</summary>
        private sealed class SpyCritter : IUWCritterVariant
        {
            public bool TriesDoor(int piAttitude) => true;

            public int DeathCry(UWObjectClassProperties.Critter pORow) => 77;

            public bool HasSpecialDeath(int piWhoami) => false;

            public int BloodObject(int piBloodIndex) => -1;

            public bool CorpseAlways(int piDungeonLevel) => false;

            public int SlotOfRecord(int piRaw) => piRaw;

            public int RecordOfSlot(int piSlot) => piSlot;
        }

        /// <summary>A player variant whose steps give themselves away.</summary>
        private sealed class SpyPlayer : IUWPlayerVariant
        {
            public int LevelUpString => 148;

            public int ExplorationWeight(int piDungeonLevel) => piDungeonLevel;

            public bool HasOrbLair => true;

            public UWExperienceGateEnum ExperienceGate(UWRulesExperience pORules, int piExperience, int piGain) => UWExperienceGateEnum.CountWithoutLevels;

            public int ExperienceGain(int piGain, int piDungeonLevel, int piCharacterLevel) => piGain * 10;

            public bool MaySleepAt(UWRules pORules, int piDungeonLevel) => false;

            public bool IsHeldAwake => false;

            public int CampLine(int piHow) => 0;

            public bool PassingOutKills => false;

            public bool HasGaramonDreams => true;

            public int LongCycleTicks => 24;

            public int TrapDetectTarget() => 99;

            public int TrapDisarmTarget() => 98;

            public bool MayRestoreMana(int piDungeonLevel, int piTileX) => true;
        }

        /// <summary>A combat variant whose every step gives itself away.</summary>
        private sealed class SpyCombat : IUWCombatVariant
        {
            public void WeaponEnchantment(int piMajor, int piMinor, out int piAccuracy, out int piDamage)
            {
                piAccuracy = 99;
                piDamage = 99;
            }

            public bool PoisonGetsThrough(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome) => false;

            public int Unresisted(int piResistances, int piDamage, int piDamageType) => 77;

            public bool ShrugsOff(bool pbProtected, int piQualityClass) => true;

            public bool DoorsTakeDamage => false;

            public int RollDebris() => 0x1FF;

            public int DebrisOf(int piItem, int piDamageType, UWDataImport.DataImport pOData) => 0x1FF;

            public int PlayerWeaponKind(int piWeaponId) => -1;

            public int MeleeHitSound(int piWeaponKind) => 0;

            public int WallSound(int piWeaponKind) => 0;

            public bool MissSoundsAtWall => true;

            public int BlockedSound(int piWeaponKind, bool pbArmourMetal) => 0;

            public int ThrowSound => 0;

            public int ShotSound(int piLauncherId) => 0;

            public bool Twangs(int piLauncherId, bool pbFired) => false;

            public int NoRoomSound => 0;

            public int NoRoomToFireString => 0;

            public bool MissileDamageSounds => false;
        }

        private sealed class SpyMagic : IUWMagicVariant
        {
            public bool Allow;

            public int Asked;

            private readonly IUWMagicVariant mIUw1 = new UWMagicVariantUw1();

            public System.Collections.Generic.IReadOnlyList<UWRunicMagic.Spell> Spells => mIUw1.Spells;

            public int SpellsPerCircle => mIUw1.SpellsPerCircle;

            public int FailStringBase => mIUw1.FailStringBase;

            public int HealedString => mIUw1.HealedString;

            public bool IsBuilt(UWRunicMagic.Spell pOSpell) => mIUw1.IsBuilt(pOSpell);

            public void PlayCastSound(IUWSpellHost pIHost, UWRunicMagic.Spell pOSpell) => mIUw1.PlayCastSound(pIHost, pOSpell);

            public bool MayCast(IUWSpellHost pIHost) => mIUw1.MayCast(pIHost);

            public bool MayCastHere(IUWSpellHost pIHost, int piCircle)
            {
                Asked++;

                return Allow;
            }

            public bool PaysOnRelease(UWRunicMagic.Spell pOSpell) => mIUw1.PaysOnRelease(pOSpell);

            public void TooSoon(IUWSpellHost pIHost) => mIUw1.TooSoon(pIHost);

            public bool TryApplyOwn(UWRunicMagic.Spell pOSpell, int piMinor, IUWSpellHost pIHost, out bool pbTook) => mIUw1.TryApplyOwn(pOSpell, piMinor, pIHost, out pbTook);
        }
    }
}
