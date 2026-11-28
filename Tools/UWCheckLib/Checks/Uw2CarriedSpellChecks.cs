using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S CARRIED WANDS AND POTIONS (G4, 2026-11-16): UWObjectSpellUw2 against the original's UseWand,
    /// checkSpell and useNSpellCharges (uw2 obj/OBJUSE.C:229, :353, :492) and UseFood's potions (USEITEMS.C:186).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2CarriedSpells()
        {
            Check.Group("UW2 carried wands and potions: UseWand, checkSpell, useNSpellCharges");

            List<string> lOCasts = new List<string>();
            bool fCast(int piMajor, int piEffect)
            {
                lOCasts.Add(piMajor + "/" + piEffect);

                return true;
            }

            // A wand with two charges of spell 17 (link 17: major bits 0 -> -1, cast by number).
            UWObject lOSpell = new UWObject(UWEnchantSpellRulesUw2.SpellObjectId) { Quality = 2, Quantity = 17, Flags = 0x4 };
            UWObject lOWand = new UWObject(UWCarriedSpellRulesUw2.FirstWand) { Quantity = 1 };
            lOWand.SetContents(new List<UWObject> { lOSpell });

            UWRandom.Use(new ScriptedAreaRandom(9, 9, 9, 0));

            try
            {
                UWCarriedSpellRulesUw2.ClearWait();

                const int Clock = 100000;
                List<string> lOResults = new List<string>();

                lOResults.Add(UWCarriedSpellRulesUw2.UseWand(lOWand, null, Clock, fCast, out bool lbCracked1) + ":" + lOSpell.Quality + (lbCracked1 ? "!" : ""));
                lOResults.Add(UWCarriedSpellRulesUw2.UseWand(lOWand, null, Clock + UWCarriedSpellRulesUw2.CastWait - 1, fCast, out bool lbCracked2) + ":" + lOSpell.Quality + (lbCracked2 ? "!" : ""));
                lOResults.Add(UWCarriedSpellRulesUw2.UseWand(lOWand, null, Clock + UWCarriedSpellRulesUw2.CastWait, fCast, out bool lbCracked3) + ":" + lOSpell.Quality + (lbCracked3 ? "!" : ""));

                // Quality 0: decode draws 9 (not missed), the spend past 0 draws 9 (kept); next decode 9, the spend 0 (deleted).
                lOResults.Add(UWCarriedSpellRulesUw2.UseWand(lOWand, null, Clock + (2 * UWCarriedSpellRulesUw2.CastWait), fCast, out bool lbCracked4) + ":" + lOSpell.Quality + (lbCracked4 ? "!" : ""));
                lOResults.Add(UWCarriedSpellRulesUw2.UseWand(lOWand, null, Clock + (3 * UWCarriedSpellRulesUw2.CastWait), fCast, out bool lbCracked5) + (lbCracked5 ? "!" : "") + ":0x" + lOWand.ID.ToString("X2"));

                Check.Str("wand: two charges, the wait of 0x2FD, empty at 0, deleted 4 in 10 past it, then the crack (id + 4)",
                    string.Join(" ", lOResults), "Cast:1 NotReady:1 Cast:0 Cast:0 Cast!:0x9C");
                Check.Str("wand: cast by number (major -1) with the link's low six bits", string.Join(" ", lOCasts), "-1/17 -1/17 -1/17 -1/17");

                // A broken wand does nothing.
                lOCasts.Clear();
                Check.Str("broken wand: nothing", UWCarriedSpellRulesUw2.UseWand(lOWand, null, Clock + (9 * UWCarriedSpellRulesUw2.CastWait), fCast, out bool _) + " " + lOCasts.Count,
                    "None 0");

                // A potion (red, 0xE5) with its spell in itself: a quantity bit, the enchanted flag, 512 + 17. UseFood clears the wait.
                lOCasts.Clear();
                UWObject lOPotion = new UWObject(0xE5) { HasQuantity = true, Quantity = 512 + 17, Flags = 0x4 | 0x8 };
                int liAt = Clock + (20 * UWCarriedSpellRulesUw2.CastWait);
                string lsFirst = UWCarriedSpellRulesUw2.CheckSpell(lOPotion, null, liAt, fCast).ToString();
                UWCarriedSpellRulesUw2.ClearWait();
                Check.Str("potion: drunk on use, its spell in itself, and again at once after the wait is cleared",
                    UWFoodUw2.IsEatenOnUse(0xE5) + " " + lsFirst + " " + UWCarriedSpellRulesUw2.CheckSpell(lOPotion, null, liAt + 1, fCast) + " "
                    + string.Join(" ", lOCasts), "True Cast Cast -1/17 -1/17");
            }
            finally
            {
                UWRandom.Use(null);
                UWCarriedSpellRulesUw2.ClearWait();
            }

            fCheckThingSpellWait();
        }

        /// <summary>
        /// THE WAIT ON THE SESSION'S CLOCK (cleanup plan 2, K11, 2026-11-28; UWThingSpellWait, both games): nextSpellTime =
        /// game_clock + 0x2FD (uw1 obj/USEITEMS.C:1059-1063), the clock the scheduler's running time plus 0x40 for every
        /// easy step (PLAYMOVE.C:304) - twelve easy steps (0x300) end the wait at once -, and a new session starts without
        /// a wait (reset_times, UWEDIT.C:375).
        /// </summary>
        private static void fCheckThingSpellWait()
        {
            UWGameSession lOBefore = UWGameSession.Current;

            try
            {
                UWGameSession lOSession = new UWGameSession(null);
                UWGameSession.MakeCurrent(lOSession);

                lOSession.Schedule.Advance(1000, true);
                UWThingSpellWait.Start();

                bool lbAtOnce = UWThingSpellWait.IsWaiting;

                lOSession.Schedule.Advance(UWThingSpellWait.Wait - 1, true);

                bool lbJustBefore = UWThingSpellWait.IsWaiting;

                lOSession.Schedule.Advance(1, true);

                bool lbAtTheWait = UWThingSpellWait.IsWaiting;

                UWThingSpellWait.Start();

                for (int liStep = 0; liStep < 11; liStep++)
                    UWThingSpellWait.AddEasyStep();

                bool lbElevenSteps = UWThingSpellWait.IsWaiting;

                UWThingSpellWait.AddEasyStep();

                bool lbTwelveSteps = UWThingSpellWait.IsWaiting;

                UWThingSpellWait.Start();
                UWGameSession.MakeCurrent(new UWGameSession(null));

                Check.Str("thing spell wait: after a cast, 0x2FD - 1 ticks on, 0x2FD; 11 and 12 easy steps; a new session",
                    lbAtOnce + " " + lbJustBefore + " " + lbAtTheWait + " " + lbElevenSteps + " " + lbTwelveSteps + " "
                    + UWThingSpellWait.IsWaiting, "True True False True False False");
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }
    }
}
