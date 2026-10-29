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
                && fThrowsNotSupported(() => lOUw2.Magic) && fThrowsNotSupported(() => lOUw2.TrapKinds), true);
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
        }

        /// <summary>
        /// The save folder rule on made-up installations (2026-10-29): an older one keeps its saves in
        /// UNDEROMn; GOG's current installers in cloud_saves/UNDEROMn, which DOSBox lays over drive C:
        /// (it need not exist yet); an installation serves only the game whose UNDEROMn it has; the second
        /// game's saves are also found in its own product's installation beside the first's.
        /// </summary>
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
        }

        /// <summary>A player variant whose steps give themselves away.</summary>
        private sealed class SpyPlayer : IUWPlayerVariant
        {
            public UWExperienceGateEnum ExperienceGate(UWRulesExperience pORules, int piExperience, int piGain) => UWExperienceGateEnum.CountWithoutLevels;

            public int ExperienceGain(int piGain, int piDungeonLevel, int piCharacterLevel) => piGain * 10;

            public bool MaySleepAt(UWRules pORules, int piDungeonLevel) => false;

            public int TrapDetectTarget() => 99;

            public int TrapDisarmTarget() => 98;
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

            public int RollDebris() => 0x1FF;
        }

        private sealed class SpyMagic : IUWMagicVariant
        {
            public bool Allow;

            public int Asked;

            private readonly IUWMagicVariant mIUw1 = new UWMagicVariantUw1();

            public bool MayCast(IUWSpellHost pIHost) => mIUw1.MayCast(pIHost);

            public bool MayCastHere(IUWSpellHost pIHost, int piCircle)
            {
                Asked++;

                return Allow;
            }

            public bool PaysOnRelease(UWRunicMagic.Spell pOSpell) => mIUw1.PaysOnRelease(pOSpell);

            public void TooSoon(IUWSpellHost pIHost) => mIUw1.TooSoon(pIHost);
        }
    }
}
