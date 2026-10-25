using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The reference audit of 2026-10-12, vitals and player data: the rules of UWPlayerVitals
    /// and UWPlayerData that were brought to the original (UnderworldExhumed) that day, each with
    /// the function it follows.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckVitals(UWDataImport.DataImport pOData)
        {
            Check.Group("Vitals and player data (reference audit 2026-10-12)");

            fCheckNameLength(pOData);

            fCheckSpellDuration();
            fCheckPlayerDataRoundTrip();
            fCheckCurseFloor();
            fCheckFlaskSteps();
            fCheckTrainerMana();
            fCheckPlayerCompute();
            fCheckLoreReset();
            fCheckLeeches(pOData);
            fCheckMotionStateNoise();
            fCheckPoisonMinute();
            fCheckStatusPassEvents();
            fCheckCombatShake();

            Check.Group("Vitals: what they tell, as events (cleanup step 3.6)");

            fCheckVitalsEvents();
        }

        /// <summary>
        /// The callbacks of IUWVitalsHost became events on UWPlayerVitals (cleanup step 3.6,
        /// 2026-10-17): each is raised where the callback was called, in the same order, and
        /// nobody listening is what no host was.
        /// </summary>
        private static void fCheckVitalsEvents()
        {
            System.Collections.Generic.List<string> lOTold = new System.Collections.Generic.List<string>();
            UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            lOVitals.VitalsChanged += () => lOTold.Add("vitals");
            lOVitals.DamageRecorded += () => lOTold.Add("damage");
            lOVitals.MessageDue += psLine => lOTold.Add("message");
            lOVitals.UnsteadyShakeDue += piTicks => lOTold.Add("unsteady " + piTicks);
            lOVitals.StatusPassDue += () => lOTold.Add("status pass");
            lOVitals.ActiveSpellsChanged += (pbHaste, pbRoaming, pbFrozen, pbTelekinesis) => lOTold.Add("spells");

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Plain(3, UWDamageTypes.None));
            fExpectInt("vitals events: a hit is recorded, then the flasks change",
                fIndexOfSequence(lOTold, "damage", "vitals"), 0);

            lOTold.Clear();
            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Plain(0, UWDamageTypes.None));
            fExpectInt("vitals events: no damage, nothing told", lOTold.Count, 0);

            int liLevel = lOVitals.Level;

            lOVitals.ChangeExperience(100000, 1);
            fExpectBool("vitals events: a level-up is a message (level " + liLevel + " -> " + lOVitals.Level + ")",
                lOVitals.Level > liLevel && lOTold.Contains("message"), true);

            lOTold.Clear();
            lOVitals.ShakeAfterPassingOut();
            fExpectBool("vitals events: waking from a drink shakes 10 plus intoxication / 6",
                lOTold.Count == 1 && lOTold[0] == "unsteady " + (10 + (lOVitals.Intoxication / 6)), true);

            lOTold.Clear();
            lOVitals.RequestStatusPass();
            fExpectBool("vitals events: the status pass is asked for", lOTold.Contains("status pass"), true);

            lOTold.Clear();
            lOVitals.EndAllSpells();
            fExpectBool("vitals events: clearing the spells tells the active spells", lOTold.Contains("spells"), true);

            // Nobody listening: nothing is told and nothing breaks.
            UWPlayerVitals lOAlone = new UWPlayerVitals(null);

            lOAlone.Initialise(null, UWPlayerVitals.StartValues.Default);
            UWDamagePipeline.Apply(lOAlone, UWDamageRequest.Plain(3, UWDamageTypes.None));
            lOAlone.ShakeAfterPassingOut();
            lOAlone.ChangeExperience(100000, 1);
            fExpectBool("vitals events: without listeners the vitals still change", lOAlone.CurrentHP < lOAlone.MaxHP, true);
        }

        /// <summary>COMBAT.C do_damage: only a blow or a missile shakes, level from the damage
        /// before the Easy halving / 4, at most 3, none when nothing was dealt (C5, 2026-10-12).</summary>
        private static void fCheckCombatShake()
        {
            CountingVitalsHost lOHost = new CountingVitalsHost();
            UWPlayerVitals lOVitals = lOHost.Watch(new UWPlayerVitals(lOHost));

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Plain(12, UWDamageTypes.Physical));
            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Plain(8, UWDamageTypes.Poison));
            fExpectInt("shake: plain damage (fall, poison, lava, trap) does not shake", lOHost.Shakes, 0);

            // The blows through the damage pipeline since cleanup step 4.2 (2026-10-17), on Easy.
            UWPlayerVitals.StartValues lOEasy = UWPlayerVitals.StartValues.Default;

            lOEasy.EasyDifficulty = true;

            UWPlayerVitals lOEasyVitals = lOHost.Watch(new UWPlayerVitals(lOHost));

            lOEasyVitals.Initialise(null, lOEasy);

            UWDamagePipeline.Apply(lOEasyVitals, UWDamageRequest.CreatureBlow(14, 0, false));
            fExpectInt("shake: a blow of 14, 7 on Easy, shakes at level 3", lOHost.LastShakeLevel, 3);

            UWDamagePipeline.Apply(lOEasyVitals, UWDamageRequest.CreatureBlow(5, 0, false));
            fExpectInt("shake: a blow of 5 sets level 1 (overwriting)", lOHost.LastShakeLevel, 1);

            UWDamagePipeline.Apply(lOEasyVitals, UWDamageRequest.CreatureBlow(1, 0, false));
            fExpectInt("shake: nothing dealt, no shake", lOHost.Shakes, 2);
        }

        /// <summary>
        /// PLAYTIME.C duration_check and set_curmagic, USEITEMS.C UseFood: the status pass where a
        /// worn curse strikes comes when a spell begins or ends or the mushroom changes - never
        /// on a quiet tick (C4, the user's decision of 2026-10-12).
        /// </summary>
        private static void fCheckStatusPassEvents()
        {
            CountingVitalsHost lOHost = new CountingVitalsHost();
            UWPlayerVitals lOVitals = lOHost.Watch(new UWPlayerVitals(lOHost));

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);
            lOHost.StatusPasses = 0;

            for (int liTick = 0; liTick < 30; liTick++)
                lOVitals.RunGameTick(0, 0);

            fExpectInt("status pass: thirty quiet ticks run none (no curse while standing still)", lOHost.StatusPasses, 0);

            lOVitals.TryAddActiveSpell(3, 1, 2);
            fExpectInt("status pass: a spell begins (set_curmagic)", lOHost.StatusPasses, 1);

            lOVitals.RunGameTick(0, 0);
            fExpectInt("status pass: a tick that only counts down runs none", lOHost.StatusPasses, 1);

            lOVitals.RunGameTick(0, 0);
            fExpectInt("status pass: the spell ends (duration_check)", lOHost.StatusPasses, 2);

            lOVitals.AddHallucination();
            fExpectInt("status pass: the mushroom eaten (UseFood)", lOHost.StatusPasses, 3);

            lOVitals.RunGameTick(0, 0);
            fExpectInt("status pass: the mushroom wearing off", lOHost.StatusPasses, 4);
        }

        /// <summary>PLAYTIME.C duration_check: the poison works on the global plyregen[1] % 3.</summary>
        private static void fCheckPoisonMinute()
        {
            UWPlayerVitals lOVitals = fWoundedVitals(new FakeVitalsHost());

            lOVitals.RunGameTick(0, 0);
            lOVitals.ApplyPoison(5);
            lOVitals.RunGameTick(0, 0);
            fExpectInt("poison: poisoned after tick 1, nothing on tick 2", (int)lOVitals.CurrentHP, 10);
            lOVitals.RunGameTick(0, 0);
            fExpectInt("poison: tick 3 is the minute and hurts 5", (int)lOVitals.CurrentHP, 5);
            fExpectInt("poison: and drops to 4", lOVitals.Poison, 4);
        }

        /// <summary>PLAYMOVE.C set_sound: four more for any motion_state bit, also standing.</summary>
        private static void fCheckMotionStateNoise()
        {
            CountingVitalsHost lOHost = new CountingVitalsHost { IsInMotionState = true };
            UWPlayerVitals lOVitals = lOHost.Watch(new UWPlayerVitals(lOHost));

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);
            lOVitals.TickQuietness(1f);
            fExpectInt("noise: standing in a motion state (floating, lava, shaking) is 4", lOVitals.Quietness,
                UWCritterRules.PlayerNoiseWater);

            CountingVitalsHost lOQuiet = new CountingVitalsHost();
            UWPlayerVitals lOStill = new UWPlayerVitals(lOQuiet);

            lOStill.Initialise(null, UWPlayerVitals.StartValues.Default);
            lOStill.TickQuietness(1f);
            fExpectInt("noise: standing without one is 0", lOStill.Quietness, 0);
        }

        /// <summary>USEITEMS.C UseMagic, the leeches: poison cleared, backfire(2), and line 0xE0
        /// (our 225) every time.</summary>
        private static void fCheckLeeches(UWDataImport.DataImport pOData)
        {
            UWInventoryModel lOInventory = new UWInventoryModel(() => pOData, () => null);
            UWObject lOLeeches = new UWObject((ushort)UWItemUse.LeechObjectId);
            BookHost lOHost = new BookHost();
            UWPlayerVitals lOVitals = fWoundedVitals(new FakeVitalsHost());

            lOInventory.Backpack[2] = lOLeeches;

            fExpectBool("leeches: used", UWItemUse.TryUseImmediately(lOLeeches, pOData, lOVitals, lOInventory, lOHost), true);
            fExpectBool("leeches: the line comes without poison too", lOHost.GeneralMessages.Contains(225), true);
            fExpectBool("leeches: they bite, at least 3 left", lOVitals.CurrentHP >= 3 && lOVitals.CurrentHP < 10, true);
            fExpectBool("leeches: used up", lOHost.Consumed, true);

            UWPlayerVitals lOWeak = fWoundedVitals(new FakeVitalsHost());
            BookHost lOWeakHost = new BookHost();
            UWObject lOMore = new UWObject((ushort)UWItemUse.LeechObjectId);

            UWDamagePipeline.Apply(lOWeak, UWDamageRequest.Plain(7, UWDamageTypes.None));
            lOWeak.ApplyPoison(5);
            lOInventory.Backpack[2] = lOMore;
            UWItemUse.TryUseImmediately(lOMore, pOData, lOWeak, lOInventory, lOWeakHost);
            fExpectInt("leeches: at 3 hit points they do not bite", (int)lOWeak.CurrentHP, 3);
            fExpectInt("leeches: and still take the poison", lOWeak.Poison, 0);
        }

        /// <summary>SKILLS.C get_skill clears the lore tries for Lore whatever the result;
        /// WORLDEV.C clear_loretry leaves the mobile objects themselves alone.</summary>
        private static void fCheckLoreReset()
        {
            CountingVitalsHost lOHost = new CountingVitalsHost();
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.Intelligence = 1;

            UWPlayerVitals lOVitals = lOHost.Watch(new UWPlayerVitals(lOHost));

            lOVitals.Initialise(null, lOStart);

            int liLore = UWPlayerVitals.FirstNamedSkillNumber + (int)UWPlayerData.Skill.Lore;

            lOVitals.SetSkillByNumber(liLore, 5);
            fExpectBool("lore: a raise above twice Intelligence fails", lOVitals.TryIncreaseSkill(liLore), false);
            fExpectInt("lore: and still clears the lore tries", lOHost.Resets, 1);
            fExpectInt("lore: and notes the skill for level 1 (player->lore[1], 0xC3)", lOVitals.LoreByLevel[1], 5);
            fExpectInt("lore: other levels untouched", lOVitals.LoreByLevel[2], 0);

            System.Collections.Generic.List<UWObject> lOList = new System.Collections.Generic.List<UWObject>();

            for (int liSlot = 0; liSlot < UWLevArkLayout.MobileCount + 2; liSlot++)
                lOList.Add(new UWObject(1) { Heading = 6 });

            UWLoreCheck.ResetAttempts(lOList, null);
            fExpectInt("lore: a mobile slot keeps its heading (a creature's facing)", lOList[5].Heading, 6);
            fExpectInt("lore: a static slot forgets the attempt", lOList[UWLevArkLayout.MobileCount + 1].Heading, 2);
        }

        /// <summary>player.h name[0x1E], CHARGEN.C pickopt: 29 characters survive the player
        /// record, and the input stops at the box's edge - the widest name it takes still fits the
        /// creation screen's 120-pixel name label (UWCharacterCreationScreen.LabelWidth).</summary>
        private static void fCheckNameLength(UWDataImport.DataImport pOData)
        {
            string lsLong = "Bartholomew Smith of Britain1";

            fExpectInt("name: 29 characters", UWCharacterGeneration.MaxNameLength, 29);
            fExpectBool("name: 29 characters survive the player record",
                UWPlayerData.FromPlain(UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter { Name = lsLong })).Name == lsLong, true);
            fExpectBool("name: a 30th is cut",
                UWPlayerData.FromPlain(UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter { Name = lsLong + "X" })).Name == lsLong, true);

            UWFont lOFont = pOData != null && pOData.Fonts != null ? pOData.Fonts.Get(UWFonts.FontType.CharacterGeneration) : null;

            if (lOFont == null || !lOFont.IsLoaded)
            {
                // The GOG archive the check unpacks carries no FONT*.SYS.
                Check.Pass("name: the widest name fits the label", "FONTCHAR.SYS not in this data folder, not measured");

                return;
            }

            const string lsPrompt = "Name: ";
            int liSpacing = UWTextLayout.CharacterSpacing;
            char lcWidest = 'A';

            for (char lcChar = ' '; lcChar <= '~'; lcChar++)
            {
                if (lOFont.MeasureText(lcChar.ToString(), 0, liSpacing) > lOFont.MeasureText(lcWidest.ToString(), 0, liSpacing))
                    lcWidest = lcChar;
            }

            string lsTyped = string.Empty;

            while (UWCharacterGeneration.AcceptsNameCharacter(lsTyped.Length, lOFont.MeasureText(lsPrompt + lsTyped, 0, liSpacing)))
                lsTyped += lcWidest;

            int liWidth = lOFont.MeasureText(lsTyped, 0, liSpacing);

            Check.Bool("name: the widest name the input takes fits the 120-pixel label ("
                + lsTyped.Length + " x '" + lcWidest + "', " + liWidth + " px)", liWidth <= 120, true);
        }

        /// <summary>SKILLS.C get_skill does not call player_compute: a raise leaves the maximum
        /// mana; the shrine's player_compute(0) sets it.</summary>
        private static void fCheckTrainerMana()
        {
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.Intelligence = 24;

            UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost { DungeonLevelIndex = 0 });

            lOVitals.Initialise(null, lOStart);
            lOVitals.SetMana(20f, 5f);

            fExpectBool("trainer: a Mana raise succeeds", lOVitals.TryIncreaseSkill(UWPlayerVitals.ManaSkillNumber), true);
            fExpectInt("trainer: the maximum mana stays until the shrine or the next level", (int)lOVitals.MaxMana, 20);

            lOVitals.RecomputeMaxima();
            fExpectInt("trainer: player_compute then sets it from skill and Intelligence", (int)lOVitals.MaxMana,
                UWExperience.GetMaximumMana(lOVitals.GetSkill(UWPlayerData.Skill.Mana), 24));
            fExpectInt("trainer: and leaves the current mana", (int)lOVitals.CurrentMana, 5);
        }

        /// <summary>SKILLS.C player_compute(0) after a shrine mantra: the maximum vitality becomes
        /// 30 + level * strength / 5, the current vitality is left above it.</summary>
        private static void fCheckPlayerCompute()
        {
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.Strength = 20;
            lOStart.Level = 5;
            lOStart.FallbackMaxHP = 90f;

            UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost { DungeonLevelIndex = 0 });

            lOVitals.Initialise(null, lOStart);
            fExpectInt("player_compute: the loaded maximum stays until then", (int)lOVitals.MaxHP, 90);

            lOVitals.RecomputeMaxima();
            fExpectInt("player_compute: maximum vitality 30 + 5 * 20 / 5", (int)lOVitals.MaxHP, 50);
            fExpectInt("player_compute: the current vitality is not cut", (int)lOVitals.CurrentHP, 90);
        }

        /// <summary>PANELS.C set_screen_frame and adjust_flasks: twelve steps, value * 12 / max in
        /// whole numbers, bubbles only at steps 1 to 7 and from the second picture on.</summary>
        private static void fCheckFlaskSteps()
        {
            fExpectInt("flasks: twelve steps", UWFlasks.FillLevels, 12);
            fExpectInt("flasks: full is step 12", UWFlasks.GetLevel(30, 30), 12);
            fExpectInt("flasks: above the maximum stays 12", UWFlasks.GetLevel(113, 90), 12);
            fExpectInt("flasks: 10 of 30 is step 4", UWFlasks.GetLevel(10, 30), 4);
            fExpectInt("flasks: 1 of 13 shows nothing", UWFlasks.GetLevel(1, 13), 0);
            fExpectInt("flasks: no maximum shows nothing", UWFlasks.GetLevel(5, 0), 0);
            fExpectBool("flasks: bubbles at steps 1 to 7",
                UWFlaskAnimation.LowestLevel == 1 && UWFlaskAnimation.HighestLevel == 7, true);
            fExpectInt("flasks: a bubbling starts on the second picture", UWFlaskAnimation.FirstShownFrame, 1);
        }

        /// <summary>SPELLS.C backfire, which the curse and Bronus' book call: at three hit points
        /// or less nothing happens (no flash), above it at least three remain.</summary>
        private static void fCheckCurseFloor()
        {
            UWPlayerVitals lOVitals = fWoundedVitals(new FakeVitalsHost());

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Plain(8, UWDamageTypes.None));
            fExpectBool("curse: at 2 hit points it does not strike",
                UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Curse(5)).Landed, false);
            fExpectInt("curse: and the 2 stay 2 (no lift to 3)", (int)lOVitals.CurrentHP, 2);

            UWPlayerVitals lOStrong = fWoundedVitals(new FakeVitalsHost());

            fExpectBool("curse: at 10 hit points eight dice strike",
                UWDamagePipeline.Apply(lOStrong, UWDamageRequest.Curse(8)).Landed, true);
            fExpectInt("curse: and leaves at least 3", (int)lOStrong.CurrentHP, 3);
            fExpectBool("curse: no dice, no strike", UWDamagePipeline.Apply(lOStrong, UWDamageRequest.Curse(0)).Landed, false);
        }

        /// <summary>
        /// PLAYER.DAT through our save writer (UWPlayerData.WritePosition) and back, in a scratch
        /// folder: the poison (bits 2-5 of word 0x5F, read_player_data loads it) survives.
        /// </summary>
        private static void fCheckPlayerDataRoundTrip()
        {
            string lsFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "UWSelfCheck-vitals-" + System.Guid.NewGuid().ToString("N"));

            try
            {
                System.IO.Directory.CreateDirectory(lsFolder);

                byte[] lyPlain = UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter
                {
                    Name = "Selfcheck",
                    Strength = 20,
                    Dexterity = 20,
                    Intelligence = 20,
                    MaxVitality = 34,
                    MaxMana = 10
                });

                System.IO.File.WriteAllBytes(System.IO.Path.Combine(lsFolder, "PLAYER.DAT"), UWPlayerData.Encrypt(lyPlain, 0x2A));

                int[] liFlags = new int[UWPlayerData.QuestFlagCount];

                liFlags[UWPlayerData.TalismanQuestFlag] = 8;
                liFlags[UWPlayerData.DreamQuestFlag] = 0x301;

                string lsError = UWPlayerData.WritePosition(lsFolder, 1, 32 << 8, 2 << 8, 96 * 8, 0,
                    new UWPlayerData.SavedState
                    {
                        Hunger = 0x80, CurrentVitality = 20, MaxVitality = 34, Poison = 7, QuestFlags = liFlags,
                        LoreByLevel = new byte[] { 0, 3, 0, 0, 0, 0, 0, 0, 9 }
                    });

                fExpectBool("player data: the writer accepts the scratch save", lsError == null, true);

                UWPlayerData lOLoaded = new UWPlayerData(lsFolder);

                fExpectInt("player data: poison 7 is read back from PLAYER.DAT", lOLoaded.Poison, 7);
                fExpectInt("player data: the hit points beside it are unchanged", lOLoaded.CurrentVitality, 20);
                fExpectInt("player data: the dream word keeps dreams 8 and 9 (0x6F)",
                    lOLoaded.GetQuestFlag(UWPlayerData.DreamQuestFlag), 0x301);
                fExpectInt("player data: the talismans beside it stay a byte",
                    lOLoaded.GetQuestFlag(UWPlayerData.TalismanQuestFlag), 8);
                fExpectInt("player data: the first game variable after the dreams is untouched",
                    lOLoaded.GetGameVariable(0), 0);
                fExpectInt("player data: a fresh character is not poisoned", UWPlayerData.FromPlain(lyPlain).Poison, 0);
                fExpectInt("player data: Lore of level 1 at 0xC3", lOLoaded.LoreByLevel[1], 3);
                fExpectInt("player data: Lore of level 8 at 0xCA, past the eight", lOLoaded.LoreByLevel[8], 9);
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

        /// <summary>PLAYTIME.C duration_check and dispel_spell: a spell of stability S lasts S
        /// ticks, the last spell moves into a freed slot, Levitate and Fly become Slow Fall for
        /// one more tick.</summary>
        private static void fCheckSpellDuration()
        {
            UWPlayerVitals lOVitals = fWoundedVitals(new FakeVitalsHost());

            lOVitals.TryAddActiveSpell(3, 1, 3);

            lOVitals.RunGameTick(0, 0);
            lOVitals.RunGameTick(0, 0);
            fExpectInt("spell duration: stability 3 still runs after two ticks", lOVitals.ActiveSpells.Count, 1);
            fExpectInt("spell duration: and stands at one", lOVitals.ActiveSpells[0].Stability, 1);

            lOVitals.RunGameTick(0, 0);
            fExpectInt("spell duration: the third tick ends it", lOVitals.ActiveSpells.Count, 0);

            // Three spells, the first ends: the last moves into its slot (C, B, not B, C).
            lOVitals.TryAddActiveSpell(3, 1, 1);
            lOVitals.TryAddActiveSpell(3, 2, 20);
            lOVitals.TryAddActiveSpell(3, 3, 20);
            lOVitals.RunGameTick(0, 0);
            fExpectBool("spell slots: the last spell takes the freed first slot",
                lOVitals.ActiveSpells.Count == 2 && lOVitals.ActiveSpells[0].MinorClass == 3
                && lOVitals.ActiveSpells[1].MinorClass == 2, true);
            fExpectBool("spell slots: the moved spell is counted down in the same pass",
                lOVitals.ActiveSpells[0].Stability == 19 && lOVitals.ActiveSpells[1].Stability == 19, true);

            lOVitals.CancelActiveSpell(0);
            fExpectBool("spell slots: cancelling keeps the other", lOVitals.ActiveSpells.Count == 1
                && lOVitals.ActiveSpells[0].MinorClass == 2, true);
            lOVitals.CancelActiveSpell(0);

            // Fly at its end: Slow Fall with stability one, gone on the next tick.
            lOVitals.TryAddActiveSpell(1, 5, 1);
            lOVitals.RunGameTick(0, 0);
            fExpectBool("spell duration: Fly turns into Slow Fall with stability one",
                lOVitals.ActiveSpells.Count == 1 && lOVitals.ActiveSpells[0].MinorClass == 2
                && lOVitals.ActiveSpells[0].Stability == 1, true);
            lOVitals.RunGameTick(0, 0);
            fExpectInt("spell duration: the Slow Fall ends on the next tick", lOVitals.ActiveSpells.Count, 0);

            // A fresh Fly cancelled by its icon: the same dispel, Slow Fall "nearly done" (B3, per
            // user on the original 2026-10-14).
            lOVitals.TryAddActiveSpell(1, 5, 20);
            lOVitals.CancelActiveSpell(0);
            fExpectBool("spell icon: a cancelled fresh Fly becomes Slow Fall with stability one",
                lOVitals.ActiveSpells.Count == 1 && lOVitals.ActiveSpells[0].MinorClass == 2
                && lOVitals.ActiveSpells[0].Stability == 1, true);
            lOVitals.CancelActiveSpell(0);
        }
    }
}
