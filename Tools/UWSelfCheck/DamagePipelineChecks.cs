using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.2 (2026-10-17): UWDamagePipeline, the one road of damage to the player.
    /// Pinned: each site's row of the table in section 7.5 of Docs/CLEANUP-PLAN.md as its
    /// UWDamageRequest carries it, and what the pipeline makes of it - the armour of the part, the
    /// easy halving, the proof, the shake, the record and the dragons' count, the backfire's dice
    /// and floor, the Void's drain - and the decisions of B5 taken from the original.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckDamagePipeline()
        {
            Check.Group("Damage pipeline (UWDamagePipeline - cleanup step 4.2)");

            fCheckDamageRequests();
            fCheckDamageBlows();
            fCheckDamageProof();
            fCheckDamageBackfire();
            fCheckDamageDrain();
        }

        /// <summary>Vitals at 30 of 30 hit points over a host whose armour and proof the check sets,
        /// with every event the pipeline raises written into pOTold.</summary>
        private static UWPlayerVitals fPipelineVitals(FakeVitalsHost pOHost, List<string> pOTold, bool pbEasy = false)
        {
            UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

            lOStart.EasyDifficulty = pbEasy;

            UWPlayerVitals lOVitals = new UWPlayerVitals(pOHost);

            lOVitals.Initialise(null, lOStart);

            if (pOTold != null)
            {
                lOVitals.DamageRecorded += () => pOTold.Add("recorded");
                lOVitals.VitalsChanged += () => pOTold.Add("vitals");
                lOVitals.CombatShakeDue += piLevel => pOTold.Add("shake " + piLevel);
                lOVitals.Damaged += (pORequest, pOOutcome) => pOTold.Add("damaged " + pORequest.Source
                    + (pOOutcome.Landed ? " landed" : "") + (pORequest.TellsMusic ? " music" : "")
                    + (pOOutcome.Landed && pORequest.FlashColour != UWDamageRequest.NoFlash ? " flash " + pORequest.FlashColour : ""));
            }

            return lOVitals;
        }

        /// <summary>The table of section 7.5, as the factories carry it.</summary>
        private static void fCheckDamageRequests()
        {
            UWDamageRequest lOBlow = UWDamageRequest.CreatureBlow(10, 2, false);

            fExpectBool("request: a creature's blow is a bash with the part's armour, halved on Easy, shaking, telling the music",
                lOBlow.Type == UWDamageTypes.Physical && lOBlow.BodyPart == 2 && lOBlow.SubtractArmour && lOBlow.HalveOnEasy
                && lOBlow.Shakes && lOBlow.TellsMusic && lOBlow.FlashColour == UWDamageRequest.NoFlash, true);
            fExpectInt("request: a critical blow flashes red (0xB8)", UWDamageRequest.CreatureBlow(10, 2, true).FlashColour, 0xB8);
            fExpectBool("request: a miss lands nothing and tells the music",
                UWDamageRequest.CreatureMiss().Rule == UWDamageRule.Miss && UWDamageRequest.CreatureMiss().TellsMusic, true);

            UWDamageRequest lOArrow = UWDamageRequest.Missile(6, UWDamageTypes.Missile, 1, true, false);
            UWDamageRequest lOTrapBolt = UWDamageRequest.Missile(6, UWDamageTypes.Missile, 1, false, false);
            UWDamageRequest lOOwn = UWDamageRequest.Missile(6, UWDamageTypes.Missile, 1, false, true);

            fExpectBool("request: every missile takes the armour and the halving",
                lOArrow.SubtractArmour && lOArrow.HalveOnEasy && lOTrapBolt.SubtractArmour && lOTrapBolt.HalveOnEasy
                && lOOwn.SubtractArmour && lOOwn.HalveOnEasy, true);
            fExpectBool("request: a creature's missile shakes and tells the music", lOArrow.Shakes && lOArrow.TellsMusic, true);
            fExpectBool("request: a missile nobody fired neither shakes nor tells", lOTrapBolt.Shakes || lOTrapBolt.TellsMusic, false);
            fExpectBool("request: his own rebounding missile shakes, the music stays", lOOwn.Shakes && !lOOwn.TellsMusic, true);

            UWDamageRequest[] lOPlain =
            {
                UWDamageRequest.Blast(5, UWDamageTypes.Fire), UWDamageRequest.Lava(1), UWDamageRequest.Drowning(4),
                UWDamageRequest.Fall(6), UWDamageRequest.Trap(3), UWDamageRequest.SleepSurface(0xFF),
                UWDamageRequest.PotionTrap(5), UWDamageRequest.SleepPoison(6, 0), UWDamageRequest.Starvation(2),
                UWDamageRequest.PoisonTick(3, 0), UWDamageRequest.ArrivalNowhere(0x7FFF)
            };

            bool lbPlain = true;

            foreach (UWDamageRequest lORequest in lOPlain)
                lbPlain &= lORequest.Rule == UWDamageRule.Hit && !lORequest.SubtractArmour && !lORequest.HalveOnEasy
                    && !lORequest.Shakes && !lORequest.TellsMusic;

            fExpectBool("request: blast, lava, drowning, fall, traps, the sleep's three, the poison tick and the arrival are plain",
                lbPlain, true);

            fExpectInt("request: lava is plain fire", UWDamageRequest.Lava(1).Type, UWDamageTypes.PlainFire);
            fExpectInt("request: a blast keeps its type", UWDamageRequest.Blast(5, UWDamageTypes.Fire).Type, UWDamageTypes.Fire);
            fExpectInt("request: the fall is typed as a blow (ours since the motion core)", UWDamageRequest.Fall(6).Type,
                UWDamageTypes.Physical);
            fExpectInt("request: drowning untyped (PLAYTIME.C sink_sink_sink, B5)", UWDamageRequest.Drowning(4).Type,
                UWDamageTypes.None);
            fExpectInt("request: drowning flashes blue (0xC6)", UWDamageRequest.Drowning(4).FlashColour, 0xC6);
            fExpectInt("request: the damage trap is type 4 on the player too (WORLDEV.C whack_thing, B5)",
                UWDamageRequest.Trap(3).Type, UWDamageTypes.Physical);
            fExpectInt("request: the sleeper's water and lava untyped (SKILLS.C drop_drunk_player, B5)",
                UWDamageRequest.SleepSurface(0xFF).Type, UWDamageTypes.None);
            fExpectInt("request: the night's poison is poison (SKILLS.C player_sleep, B5)", UWDamageRequest.SleepPoison(6, 0).Type,
                UWDamageTypes.Poison);
            fExpectInt("request: starvation untyped", UWDamageRequest.Starvation(2).Type, UWDamageTypes.None);

            fExpectBool("request: the curse, the backfire and the leeches are backfires that flash 0xA8",
                UWDamageRequest.Curse(5).Rule == UWDamageRule.Backfire && UWDamageRequest.Backfire(2).Rule == UWDamageRule.Backfire
                && UWDamageRequest.Leeches().Rule == UWDamageRule.Backfire && UWDamageRequest.Curse(5).FlashColour == 0xA8
                && UWDamageRequest.Backfire(2).FlashColour == 0xA8 && UWDamageRequest.Leeches().FlashColour == 0xA8, true);
            fExpectInt("request: the leeches roll two dice", UWDamageRequest.Leeches().Amount, 2);
            fExpectBool("request: the Void drains, without a flash of its own",
                UWDamageRequest.VoidDrain(3).Rule == UWDamageRule.Drain
                && UWDamageRequest.VoidDrain(3).FlashColour == UWDamageRequest.NoFlash, true);
        }

        /// <summary>COMBAT.C do_damage: the armour, the halving, the shake from the value before it,
        /// then damage_item; nothing left, nothing further.</summary>
        private static void fCheckDamageBlows()
        {
            List<string> lOTold = new List<string>();
            FakeVitalsHost lOHost = new FakeVitalsHost();

            lOHost.Armour[2] = 3;

            UWPlayerVitals lOVitals = fPipelineVitals(lOHost, lOTold);
            UWDamageOutcome lOOutcome = UWDamagePipeline.Apply(lOVitals, UWDamageRequest.CreatureBlow(10, 2, false));

            fExpectInt("blow: 10 less the part's armour 3", lOOutcome.Applied, 7);
            fExpectInt("blow: off the hit points", (int)lOVitals.CurrentHP, 23);
            fExpectInt("blow: shakes at 7 / 4", lOOutcome.ShakeLevel, 1);
            fExpectInt("blow: recorded, the flasks, the shake, then the word to the host",
                fIndexOfSequence(lOTold, "recorded", "vitals", "shake 1", "damaged CreatureBlow landed music"), 0);
            fExpectInt("blow: recorded for F1 as a bash", lOVitals.LastDamageType, UWDamageTypes.Physical);
            fExpectInt("blow: counted for the dragons", lOVitals.TakeDamageThisTick(), 7);

            lOTold.Clear();
            lOOutcome = UWDamagePipeline.Apply(lOVitals, UWDamageRequest.CreatureBlow(3, 2, true));
            fExpectBool("blow: the armour swallows 3 - no damage, no shake",
                lOOutcome.Applied == 0 && lOOutcome.ShakeLevel < 0 && (int)lOVitals.CurrentHP == 23, true);
            fExpectBool("blow: but the critical still flashes and the music hears of it",
                lOTold.Count == 1 && lOTold[0] == "damaged CreatureBlow landed music flash 184", true);

            lOTold.Clear();
            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.CreatureMiss());
            fExpectBool("blow: a miss only tells the music", lOTold.Count == 1 && lOTold[0] == "damaged CreatureBlow music"
                && (int)lOVitals.CurrentHP == 23, true);

            List<string> lOEasyTold = new List<string>();
            UWPlayerVitals lOEasy = fPipelineVitals(lOHost, lOEasyTold, true);

            lOOutcome = UWDamagePipeline.Apply(lOEasy, UWDamageRequest.CreatureBlow(17, 2, false));
            fExpectBool("blow on Easy: 17 less 3 is 14, halved 7, shaking at 14 / 4 = 3",
                lOOutcome.AfterArmour == 14 && lOOutcome.AfterEasy == 7 && lOOutcome.Applied == 7 && lOOutcome.ShakeLevel == 3, true);

            lOEasyTold.Clear();
            lOOutcome = UWDamagePipeline.Apply(lOEasy, UWDamageRequest.CreatureBlow(4, 2, false));
            fExpectBool("blow on Easy: 4 less 3 is 1, halved 0 - no damage and no shake",
                lOOutcome.Applied == 0 && lOOutcome.ShakeLevel < 0 && !lOEasyTold.Contains("recorded"), true);

            lOOutcome = UWDamagePipeline.Apply(lOEasy, UWDamageRequest.Missile(9, UWDamageTypes.Missile, 2, false, false));
            fExpectBool("missile nobody fired on Easy: 9 less 3, halved 3, no shake",
                lOOutcome.Applied == 3 && lOOutcome.ShakeLevel < 0, true);

            // A magic missile the armour swallows does not reach the proof: no draw (the missile
            // site never called the damage then).
            FakeVitalsHost lOWarded = new FakeVitalsHost { DamageTypeProof = UWDamageTypes.Magic };

            lOWarded.Armour[1] = 20;

            RecordingRandom lORandom = new RecordingRandom(0);

            UWRandom.Use(lORandom);

            try
            {
                UWDamagePipeline.Apply(fPipelineVitals(lOWarded, null), UWDamageRequest.Missile(9, UWDamageTypes.Magic, 1, true, false));
            }
            finally
            {
                UWRandom.Use(null);
            }

            fExpectInt("missile: swallowed by the armour, the magic proof draws nothing", lORandom.Ranges.Count, 0);
        }

        /// <summary>DAMAGE.C check_res against the player's resist byte: Flameproof, Poison
        /// Resistance; untyped damage passes whatever the proof.</summary>
        private static void fCheckDamageProof()
        {
            FakeVitalsHost lOFlameproof = new FakeVitalsHost { DamageTypeProof = UWDamageTypes.PlainFire };
            UWPlayerVitals lOVitals = fPipelineVitals(lOFlameproof, null);
            UWDamageOutcome lOOutcome = UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Lava(1));

            fExpectBool("proof: Flameproof wards off the lava whole", lOOutcome.Resisted && (int)lOVitals.CurrentHP == 30, true);
            fExpectInt("proof: and the record stays empty", lOVitals.LastDamage, 0);

            UWPlayerVitals lOBare = fPipelineVitals(new FakeVitalsHost(), null);

            UWDamagePipeline.Apply(lOBare, UWDamageRequest.Lava(1));
            fExpectInt("proof: without it the lava burns a point", (int)lOBare.CurrentHP, 29);

            // Every bit the player can ward off (PLAYDATA.C damage_protection_flags).
            FakeVitalsHost lOAll = new FakeVitalsHost { DamageTypeProof = 0x40 | 0x08 | 0x10 | 0x01 | 0x02 };
            UWPlayerVitals lOProof = fPipelineVitals(lOAll, null);

            UWDamagePipeline.Apply(lOProof, UWDamageRequest.Trap(3));
            UWDamagePipeline.Apply(lOProof, UWDamageRequest.Drowning(4));
            UWDamagePipeline.Apply(lOProof, UWDamageRequest.SleepSurface(5));
            UWDamagePipeline.Apply(lOProof, UWDamageRequest.Fall(2));
            fExpectInt("proof: the trap's type 4, drowning, the sleeper's lava and the fall pass any proof", (int)lOProof.CurrentHP,
                30 - 3 - 4 - 5 - 2);

            UWPlayerVitals lOSleeper = fPipelineVitals(new FakeVitalsHost(), null);

            UWDamagePipeline.Apply(lOSleeper, UWDamageRequest.SleepPoison(6, UWDamageTypes.Poison));
            fExpectInt("proof: the night's poison is warded off by the Poison Resistance it is handed (B5)",
                (int)lOSleeper.CurrentHP, 30);

            UWDamagePipeline.Apply(lOSleeper, UWDamageRequest.SleepPoison(6, 0));
            fExpectInt("proof: and lands without it", (int)lOSleeper.CurrentHP, 24);

            FakeVitalsHost lOResistant = new FakeVitalsHost { DamageTypeProof = UWDamageTypes.Poison };
            UWPlayerVitals lOTicked = fPipelineVitals(lOResistant, null);

            UWDamagePipeline.Apply(lOTicked, UWDamageRequest.PoisonTick(4, 0));
            fExpectInt("proof: a request's own proof beats the host's", (int)lOTicked.CurrentHP, 26);

            UWDamagePipeline.Apply(lOTicked, UWDamageRequest.Lava(1).WithProof(UWDamageRequest.HostProof));
            fExpectInt("proof: HostProof asks the host", (int)lOTicked.CurrentHP, 25);
        }

        /// <summary>SPELLS.C backfire: the dice first, nothing at three or fewer, at least three
        /// left, recorded but not counted for the dragons; the flash only when it struck.</summary>
        private static void fCheckDamageBackfire()
        {
            List<string> lOTold = new List<string>();
            UWPlayerVitals lOVitals = fPipelineVitals(new FakeVitalsHost(), lOTold);
            UWDamageOutcome lOOutcome = UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Curse(8));

            fExpectBool("backfire: eight dice at 30 strike and leave at least 3",
                lOOutcome.Landed && lOVitals.CurrentHP >= 3 && lOVitals.CurrentHP <= 22, true);
            fExpectBool("backfire: recorded untyped, flashing", lOVitals.LastDamageType == UWDamageTypes.None
                && lOTold.Contains("damaged Curse landed flash 168"), true);
            fExpectInt("backfire: not counted for the dragons", lOVitals.TakeDamageThisTick(), 0);

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Curse(30));
            fExpectInt("backfire: never below 3", (int)lOVitals.CurrentHP, 3);

            RecordingRandom lORandom = new RecordingRandom(1, 1);

            lOTold.Clear();
            UWRandom.Use(lORandom);

            try
            {
                lOOutcome = UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Backfire(2));
                fExpectBool("backfire: at 3 it rolls its dice and does nothing", !lOOutcome.Landed && lORandom.Ranges.Count == 2
                    && (int)lOVitals.CurrentHP == 3, true);
                fExpectBool("backfire: and does not flash", lOTold.Count == 1 && lOTold[0] == "damaged Backfire", true);

                UWDamagePipeline.Apply(lOVitals, UWDamageRequest.Curse(0));
                fExpectInt("curse: no dice, no roll at all", lORandom.Ranges.Count, 2);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>The Void sets the hit points directly: no record, no count, never below zero.</summary>
        private static void fCheckDamageDrain()
        {
            UWPlayerVitals lOVitals = fPipelineVitals(new FakeVitalsHost(), null);
            UWDamageOutcome lOOutcome = UWDamagePipeline.Apply(lOVitals, UWDamageRequest.VoidDrain(4));

            fExpectBool("drain: four off, not recorded, not counted", lOOutcome.Landed && (int)lOVitals.CurrentHP == 26
                && lOVitals.LastDamage == 0 && lOVitals.TakeDamageThisTick() == 0, true);

            UWDamagePipeline.Apply(lOVitals, UWDamageRequest.VoidDrain(100));
            fExpectInt("drain: never below zero", (int)lOVitals.CurrentHP, 0);

            lOOutcome = UWDamagePipeline.Apply(fPipelineVitals(new FakeVitalsHost(), null), UWDamageRequest.VoidDrain(0));
            fExpectBool("drain: nothing, nothing landed", lOOutcome.Landed, false);

            lOOutcome = UWDamagePipeline.Apply(fPipelineVitals(new FakeVitalsHost(), null), UWDamageRequest.ArrivalNowhere(0x7FFF));
            fExpectBool("arrival nowhere: the hit points cross to zero", lOOutcome.Died, true);
        }
    }
}
