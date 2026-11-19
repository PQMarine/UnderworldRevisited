using System.Collections.Generic;
using UWDataImport;
using UWDataImport.Audio;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S MUSIC (UWMusicSelectorUw2, UW2 plan phase 5 part A): the walking themes by world, the menu's
    /// theme followed by 10, the walk's first theme, a level change within a world and into another, the armed theme,
    /// the victory theme holding off requests, the combat timeout and the eight seconds between combat themes; with
    /// the data, every theme's two pieces.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A music output that writes down what it plays; a piece plays until the check ends it.</summary>
        private sealed class LoggingMusicOutput : IUWMusicOutput
        {
            public readonly List<string> Log = new List<string>();

            public bool IsReady => true;

            public bool IsPlaying { get; set; }

            public void Play(int piTheme)
            {
                Log.Add(piTheme.ToString());
                IsPlaying = true;
            }

            public void Stop()
            {
                Log.Add("stop");
                IsPlaying = false;
            }

            public string Take()
            {
                string lsLog = string.Join(" ", Log);

                Log.Clear();

                return lsLog;
            }
        }

        private static void fCheckUw2Music(CheckContext pOContext)
        {
            Check.Group("UW2 music: walking themes by world, the menu, the walk, the armed and victory themes, combat");

            // The worlds: (level - 1) / 8, nine; each its three walking themes.
            Check.Str("the worlds of levels 1, 8, 9, 17, 69, 80 (kept in the table) | world 0's and 8's walking themes",
                UWMusicSelectorUw2.WorldOf(1) + " " + UWMusicSelectorUw2.WorldOf(8) + " " + UWMusicSelectorUw2.WorldOf(9) + " "
                + UWMusicSelectorUw2.WorldOf(17) + " " + UWMusicSelectorUw2.WorldOf(69) + " " + UWMusicSelectorUw2.WorldOf(80) + " | "
                + UWMusicSelectorUw2.WalkingTheme(0, 0) + " " + UWMusicSelectorUw2.WalkingTheme(0, 1) + " " + UWMusicSelectorUw2.WalkingTheme(0, 2) + " "
                + UWMusicSelectorUw2.WalkingTheme(8, 0) + " " + UWMusicSelectorUw2.WalkingTheme(8, 1) + " " + UWMusicSelectorUw2.WalkingTheme(8, 2),
                "0 0 1 2 8 8 | 10 12 14 8 12 9");

            // The menu: theme 1, then 10 when it ends, and 10 again.
            LoggingMusicOutput lOOut = new LoggingMusicOutput();
            UWMusicSelectorUw2 lOMusic = new UWMusicSelectorUw2(lOOut);

            lOMusic.SetNewMusic(UWMusicSelectorUw2.MenuTheme);
            lOMusic.ChangeMusicMaybe(false, false, 1, 0f);
            lOMusic.LoopMusicMaybe();
            string lsMenu = lOOut.Take();
            lOOut.IsPlaying = false;
            lOMusic.LoopMusicMaybe();
            lOOut.IsPlaying = false;
            lOMusic.LoopMusicMaybe();
            lsMenu += " | " + lOOut.Take();

            Check.Str("the menu: theme 1 (a piece playing stays) | over: 10, and 10 again", lsMenu, "1 | 10 10");

            // The walk on level 1: the world's first theme at once, over the menu's; a level within the world keeps it,
            // the next world (level 9) its first; the weapon drawn: 5; sheathed: one of the world's three.
            lOMusic = new UWMusicSelectorUw2(lOOut);
            lOMusic.LoadNewMusic(UWMusicSelectorUw2.MenuTheme);
            lOOut.Take();
            lOMusic.SetRandomWalkingMusic(UWMusicSelectorUw2.PickFirst, 1);
            lOMusic.ChangeMusicMaybe(false, true, 1, 0f);
            lOMusic.SetRandomWalkingMusic(UWMusicSelectorUw2.PickKeep, 3);
            lOMusic.ChangeMusicMaybe(false, true, 3, 1f);
            lOMusic.SetRandomWalkingMusic(UWMusicSelectorUw2.PickKeep, 9);
            lOMusic.ChangeMusicMaybe(false, true, 9, 2f);
            lOMusic.OnWeaponDrawn();
            lOMusic.ChangeMusicMaybe(true, true, 9, 3f);
            string lsWalk = lOOut.Take();
            lOMusic.OnWeaponSheathed(9);
            lOMusic.ChangeMusicMaybe(false, true, 9, 4f);
            int liSheathed = lOMusic.CurrentTheme;

            Check.Str("the walk: the world's first (10), kept on level 3, level 9's first (9), drawn 5 | sheathed: one of 9 10 15",
                lsWalk + " | " + (liSheathed == 9 || liSheathed == 10 || liSheathed == 15), "10 9 5 | True");

            // A piece over in the walk: a walking theme repeats or another of the world; the weapon drawn after a walking
            // theme: 5; the armed theme over, weapon still drawn: 5 again.
            lOOut.IsPlaying = false;
            lOMusic.ChangeMusicMaybe(true, true, 9, 5f);
            lOOut.IsPlaying = false;
            lOMusic.ChangeMusicMaybe(true, true, 9, 6f);
            string lsOver = lOOut.Take();

            Check.Str("a piece over with the weapon drawn: the armed theme, and again", lsOver.Substring(lsOver.IndexOf(' ') + 1), "5 5");

            // The victory theme: a request is dropped while it plays (set_new_music), the walking choice is not; over: on
            // the game screen a walking theme follows.
            lOMusic = new UWMusicSelectorUw2(lOOut);
            lOMusic.LoadNewMusic(UWMusicSelectorUw2.VictoryTheme);
            lOOut.Take();
            lOMusic.SetNewMusic(UWMusicSelectorUw2.CombatTheme);
            int liAsked = lOMusic.NewTheme;
            lOMusic.ChangeMusicMaybe(false, true, 1, 0f);
            string lsVictory = liAsked + " " + lOOut.Take();
            lOOut.IsPlaying = false;
            lOMusic.ChangeMusicMaybe(false, true, 1, 1f);
            int liAfter = lOMusic.CurrentTheme;

            Check.Str("victory playing: a request dropped (0), nothing loads | over: a walking theme of world 0",
                lsVictory + " | " + (liAfter == 10 || liAfter == 12 || liAfter == 14), "0  | True");

            // Combat: ten seconds after the last blow the theme gives way (drawn: 5); one combat theme replaces another
            // at most every eight seconds, and a refused one counts as a change (theme_changed set again).
            lOMusic = new UWMusicSelectorUw2(lOOut);
            lOOut.IsPlaying = true;
            lOMusic.LoadNewMusic(UWMusicSelectorUw2.ArmedTheme);
            lOOut.Take();
            lOMusic.SetNewMusic(UWMusicSelectorUw2.CombatTheme);
            lOMusic.MarkCombat(100f);
            lOMusic.ChangeMusicMaybe(true, true, 1, 100f);
            lOMusic.SetNewMusic(UWMusicSelectorUw2.DangerTheme);
            lOMusic.MarkCombat(101f);
            lOMusic.ChangeMusicMaybe(true, true, 1, 101f);
            lOMusic.SetNewMusic(UWMusicSelectorUw2.FoeHurtTheme);
            lOMusic.MarkCombat(105f);
            lOMusic.ChangeMusicMaybe(true, true, 1, 105f);
            lOMusic.SetNewMusic(UWMusicSelectorUw2.FoeHurtTheme);
            lOMusic.MarkCombat(112f);
            lOMusic.ChangeMusicMaybe(true, true, 1, 112f);
            string lsCombat = lOOut.Take();
            lOMusic.SetNewMusic(UWMusicSelectorUw2.FoeHurtTheme);
            lOMusic.MarkCombat(114f);
            lOMusic.ChangeMusicMaybe(true, true, 1, 114f);
            lsCombat += " | " + lOOut.Take();
            lOMusic.SetNewMusic(UWMusicSelectorUw2.FoeHurtTheme);
            lOMusic.MarkCombat(123f);
            lOMusic.ChangeMusicMaybe(true, true, 1, 123f);
            lsCombat += " | " + lOOut.Take();
            lOMusic.ChangeMusicMaybe(true, true, 1, 130f);
            lOMusic.ChangeMusicMaybe(true, true, 1, 133.5f);
            lsCombat += " | " + lOOut.Take();

            Check.Str("combat: 3 over armed, 4 (8 s after none) | 2 refused at 105, 112, 114 (each refusal a change) | 2 at 123 | 10 s after: drawn 5",
                lsCombat, "3 4 |  | 2 | 5");

            // Switched on with nothing loaded: a walking theme of the world.
            lOMusic = new UWMusicSelectorUw2(lOOut);
            lOMusic.TurnOn(17);
            int liOn = lOMusic.CurrentTheme;

            Check.Str("turned on with nothing loaded on level 17: world 2's first", liOn.ToString(), "11");

            // Part B: the creatures' numbers by game, the swing that leaves a combat theme alone, the player's damage.
            IUWCritterVariant lIOne = UWGameProfiles.Uw1.Critter;
            IUWCritterVariant lITwo = UWGameProfiles.Uw2.Critter;

            Check.Str("combat and foe-hurt themes UW1 | UW2; keeps: UW1 6 over 5, UW2 a swing over 2, UW2 a blow's 3 over 2, UW2 a swing over 5",
                lIOne.CombatTheme + " " + lIOne.FoeHurtTheme + " | " + lITwo.CombatTheme + " " + lITwo.FoeHurtTheme + "; "
                + lIOne.KeepsPlayingTheme(6, 5, false) + " " + lITwo.KeepsPlayingTheme(3, 2, true) + " " + lITwo.KeepsPlayingTheme(3, 2, false)
                + " " + lITwo.KeepsPlayingTheme(3, 5, true),
                "6 5 | 3 2; True True False False");

            Check.Str("the player damaged: 31 of 120 -> combat, 30 -> danger (30 * 64 / 121 is 15), 0 of 1 -> danger",
                UWMusicSelectorUw2.PlayerDamagedTheme(31, 120) + " " + UWMusicSelectorUw2.PlayerDamagedTheme(30, 120) + " "
                + UWMusicSelectorUw2.PlayerDamagedTheme(0, 1), "3 4 4");

            // The death cries by kind (crit_die): 0 none, 2/3/4 their effects, the rest 6.
            UWObjectClassProperties.Critter lORow = new UWObjectClassProperties.Critter();
            string lsCries = string.Empty;

            for (byte lyKind = 0; lyKind < 8; lyKind++)
            {
                lORow.DeathSound = lyKind;
                lsCries += (lsCries.Length > 0 ? " " : string.Empty) + lITwo.DeathCry(lORow);
            }

            Check.Str("UW2 death cries for kinds 0-7", lsCries, "-1 6 34 35 36 6 6 6");

            // The footsteps by category on frames 1 and 3 (AI.C:1179): the first game's table, the second game's.
            string lsSteps = string.Empty;

            for (int liCategory = 1; liCategory <= 7; liCategory++)
                lsSteps += (liCategory > 1 ? " " : string.Empty) + lIOne.FootstepSound(liCategory, 1) + "/" + lIOne.FootstepSound(liCategory, 3)
                    + ":" + lITwo.FootstepSound(liCategory, 1) + "/" + lITwo.FootstepSound(liCategory, 3);

            Check.Str("footsteps by category, UW1:UW2 on frames 1/3 (the lurker's 3: 5 a splash sample in UW1's table, 0x27 in UW2's)",
                lsSteps, "1/2:90/91 23/23:23/23 5/5:39/39 14/14:14/14 13/13:13/13 -1/-1:47/48 -1/-1:29/29");

            // The save's switches (word 0x302): written as 1 or 0 beside the detail bits, read back.
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewWalker();
            int liDetail = lOFile.Record[UWPlayerRecordUw2.DetailByteOffset] >> UWPlayerRecordUw2.DetailShift;
            UWPlayerRecordUw2.WriteSoundAndMusic(lOFile, false, true);
            string lsSwitches = UWPlayerRecordUw2.SoundOn(lOFile) + " " + UWPlayerRecordUw2.MusicOn(lOFile) + " "
                + lOFile.Record[UWPlayerRecordUw2.DetailByteOffset].ToString("X2");
            UWPlayerRecordUw2.WriteSoundAndMusic(lOFile, true, false);
            lsSwitches += " | " + UWPlayerRecordUw2.SoundOn(lOFile) + " " + UWPlayerRecordUw2.MusicOn(lOFile) + " "
                + (lOFile.Record[UWPlayerRecordUw2.DetailByteOffset] >> UWPlayerRecordUw2.DetailShift == liDetail);

            Check.Str("the save's sound and music switches: sound off, music on (byte) | sound on, music off, detail kept",
                lsSwitches, "False True " + ((liDetail << 4) | 4).ToString("X2") + " | True False True");

            fCheckUw2MusicPieces(pOContext);
        }

        /// <summary>Every theme the second game asks for has its AdLib and MT-32 piece (UWAnn, UWRnn).</summary>
        private static void fCheckUw2MusicPieces(CheckContext pOContext)
        {
            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
                return;

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            UWSound lOSound = lOImport.Sound;
            List<string> lsMissing = new List<string>();
            int liFound = 0;

            for (int liTheme = 1; liTheme <= 0x19; liTheme++)
            {
                if (liTheme > UWMusicSelectorUw2.LastWalkingTheme && liTheme < UWMusicSelectorUw2.IntroTheme)
                    continue;

                foreach (bool lbAdlib in new[] { true, false })
                {
                    UWXmi lOXmi = lOSound != null ? lOSound.GetMusic(UWMusicSelector.GetFileNumber(liTheme), lbAdlib) : null;

                    if (lOXmi != null && lOXmi.IsLoaded && lOXmi.Sequences.Count > 0)
                        liFound++;
                    else
                        lsMissing.Add(UWSoundsDat.MusicName(UWGame.Uw2, liTheme, !lbAdlib));
                }
            }

            Check.Str("the pieces of themes 1-15, 0x18, 0x19, UWA and UWR: found | missing", liFound + " | " + string.Join(" ", lsMissing), "34 | ");

            // The death kinds the shipped creatures carry, and that every cry is in SOUNDS.DAT.
            SortedDictionary<int, int> lOKinds = new SortedDictionary<int, int>();
            bool lbCriesKnown = true;

            for (int liId = 0x40; liId < 0x80; liId++)
            {
                if (!lOImport.ObjectClassProperties.TryGetCritter(liId, out UWObjectClassProperties.Critter lORow))
                    continue;

                lOKinds.TryGetValue(lORow.DeathSound, out int liCount);
                lOKinds[lORow.DeathSound] = liCount + 1;

                int liCry = UWGameProfiles.Uw2.Critter.DeathCry(lORow);

                if (liCry >= 0 && (lOSound == null || liCry >= lOSound.Effects.Count))
                    lbCriesKnown = false;
            }

            string lsKinds = string.Empty;

            foreach (KeyValuePair<int, int> lOKind in lOKinds)
                lsKinds += (lsKinds.Length > 0 ? " " : string.Empty) + lOKind.Key + ":" + lOKind.Value;

            Check.Str("UW2 creatures by death kind (kind:count)", lsKinds, "0:13 1:24 2:16 3:7 4:4");
            Check.Str("every UW2 death cry has a SOUNDS.DAT entry", lbCriesKnown.ToString(), "True");
        }
    }
}
