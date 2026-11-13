using System.Linq;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE SECOND GAME'S TARGET SPELLS ON A THING (UWObjectSpellRulesUw2, UW2 plan G1 part D2, 2026-11-12): what Mending
        /// mends (mendable), Gate Travel's choice on the two moonstone records, the trap checks' targets by the world, and
        /// which minors are built.
        /// </summary>
        private static void fCheckObjectSpellsUw2()
        {
            Check.Group("UW2 object spells (UWObjectSpellRulesUw2 - part D2)");

            // mendable: a sword (0x04), a torch (0x91, misc minor 1 with the wands), a loaf (0xB5, misc minor 3, the food), a
            // door (0x140) - not a chest (0x80, misc minor 0), a key (0x100), a creature (0x44), rect minor 1 (0x150).
            string lsMend = string.Empty;

            foreach (int liId in new[] { 0x04, 0x91, 0xB5, 0x140, 0x80, 0x100, 0x44, 0x150 })
                lsMend += UWObjectSpellRulesUw2.Mendable(liId) ? "1" : "0";

            Check.Str("mending: weapons and armour, lights and wands, food, the doors; not a chest, key, creature or rect minor 1",
                lsMend, "11110000");

            int liLevel;
            string lsGate = UWObjectSpellRulesUw2.DecideGate(0, 0, 5, out liLevel) + " "
                + UWObjectSpellRulesUw2.DecideGate(5, 0, 5, out liLevel) + " "
                + UWObjectSpellRulesUw2.DecideGate(5, 5, 5, out liLevel) + " "
                + UWObjectSpellRulesUw2.DecideGate(3, 0x2D, 5, out liLevel) + ":" + liLevel + " "
                + UWObjectSpellRulesUw2.DecideGate(5, 0x2D, 5, out liLevel) + ":" + liLevel;

            Check.Str("gate travel: none placed, one here, both here, the first elsewhere wins, the other elsewhere", lsGate,
                "NotAvailable ThisLevel ThisLevel OtherLevel:3 OtherLevel:45");

            Check.Str("traps: detect 10 + 2 * world, disarm 8 + world (levels 1, 8, 9, 65)",
                UWObjectSpellRulesUw2.DetectTarget(1) + " " + UWObjectSpellRulesUw2.DetectTarget(8) + " " + UWObjectSpellRulesUw2.DetectTarget(9)
                + " " + UWObjectSpellRulesUw2.DetectTarget(65) + " / " + UWObjectSpellRulesUw2.DisarmTarget(1) + " " + UWObjectSpellRulesUw2.DisarmTarget(65),
                "10 10 12 26 / 8 16");

            string lsBuilt = string.Empty;

            for (int liMinor = 0; liMinor <= 15; liMinor++)
                lsBuilt += UWTargetSpellRulesUw2.IsBuilt(liMinor) || UWObjectSpellRulesUw2.IsBuilt(liMinor) ? "1" : "0";

            Check.Str("built: class 7 minors 0 to 15, Enchantment (14) since D2b", lsBuilt, "1111111111111111");

            // sp_enchant on made-up things (nothing carries contents, so no records are needed).
            UWObject lOSword = new UWObject(0x04) { Quality = 40 };
            UWObject lOHelm = new UWObject(0x2C) { Quality = 40 };
            UWObject lOKey = new UWObject(0x100) { Quality = 40 };
            string lsFirst = UWEnchantSpellRulesUw2.Enchant(lOSword, null, 15, 30, null, out _) + ":" + (lOSword.HasQuantity && lOSword.IsEnchanted)
                + ":" + ((lOSword.Quantity & 0x3F0) == 0x2C0 && ((lOSword.Quantity & 0xF) == 0 || (lOSword.Quantity & 0xF) == 4))
                + " " + UWEnchantSpellRulesUw2.Enchant(lOHelm, null, 15, 30, null, out _) + ":" + ((lOHelm.Quantity & 0xF) == 0 || (lOHelm.Quantity & 0xF) == 8)
                + " " + UWEnchantSpellRulesUw2.Enchant(lOKey, null, 15, 30, null, out _);

            Check.Str("enchant: a plain sword and helm get the lowest tier of one kind (0x2C0 + 0 or 4, 0 or 8); a key cannot",
                lsFirst, "Enchanted:True:True Enchanted:True CannotEnchant");

            // The next tier: a sword at tier 1 (0x2C1), level 15 Casting 30: reach (15 - 8) / 4 + 30 / 11 = 3, so to 2; tiers
            // 0 to 2 go one up, so 3 (0x2C3) is the top. A level 1 caster with Casting 0 (reach -1) destroys even a tier-0 sword.
            UWObject lOTiered = new UWObject(0x04) { HasQuantity = true, Quantity = 0x2C1, IsEnchanted = true };
            UWObject lOTop = new UWObject(0x04) { HasQuantity = true, Quantity = 0x2C3, IsEnchanted = true };
            UWObject lOWeak = new UWObject(0x04) { HasQuantity = true, Quantity = 0x2C0, IsEnchanted = true };
            string lsTiers = UWEnchantSpellRulesUw2.Enchant(lOTiered, null, 15, 30, null, out _) + ":" + lOTiered.Quantity.ToString("X")
                + " " + UWEnchantSpellRulesUw2.Enchant(lOTop, null, 15, 30, null, out _)
                + " " + UWEnchantSpellRulesUw2.Enchant(lOWeak, null, 1, 0, null, out _);

            Check.Str("enchant: tier 1 to 2 within reach, the top tier cannot go on, past the reach destroyed", lsTiers,
                "Enchanted:2C2 CannotEnchant Destroyed");

            Check.Str("enchant: chargeable not food, nor 7/14 and 13/12..15; debris of a dagger, a chair, a wand",
                (UWEnchantSpellRulesUw2.Chargeable(0x98, 1, 2) ? "1" : "0") + (UWEnchantSpellRulesUw2.Chargeable(0xB5, 1, 2) ? "1" : "0")
                + (UWEnchantSpellRulesUw2.Chargeable(0x98, 7, 0xE) ? "1" : "0") + (UWEnchantSpellRulesUw2.Chargeable(0x98, 0xD, 0xC) ? "1" : "0")
                + " " + UWEnchantSpellRulesUw2.DebrisOf(0x03, null).ToString("X") + " " + UWEnchantSpellRulesUw2.DebrisOf(0x150, null).ToString("X")
                + " " + UWEnchantSpellRulesUw2.DebrisOf(0x98, null).ToString("X"),
                "1000 C7 DC D6");

            // creat_spell's demons[(Casting + roll) / 12] (part E1): Casting 0 roll 0 and 29, Casting 30 roll 0 and 29,
            // Casting 12 roll 11, Casting 24 roll 12.
            Check.Str("summon demon: an imp, a hordling, a hordling, a destroyer, an imp, a despoiler",
                UWSummonSpellRules.DemonOf(0, 0).ToString("X") + " " + UWSummonSpellRules.DemonOf(0, 29).ToString("X") + " "
                + UWSummonSpellRules.DemonOf(30, 0).ToString("X") + " " + UWSummonSpellRules.DemonOf(30, 29).ToString("X") + " "
                + UWSummonSpellRules.DemonOf(12, 11).ToString("X") + " " + UWSummonSpellRules.DemonOf(24, 12).ToString("X"),
                "4B 5E 5E 68 4B 64");

            Check.Str("class 8 built on the second game: all but Summon Monster, a creature's there (minors 1 to 6)",
                string.Concat(new[] { 1, 2, 3, 4, 5, 6 }.Select(piMinor => UWSummonSpellRules.IsBuiltSecondGame(piMinor) ? "1" : "0")),
                "111011");

            // UseRune (part E2): the player 4 to 19, a creature 0x40 to 0x13C; the flam and tym runes and nothing else.
            Check.Str("runes: paralysis 4 and 19, hold 0x40 and 0x13C, runes 0x19E 0x19F not 0x19D",
                UWRuneRulesUw2.PlayerParalysis(0) + " " + UWRuneRulesUw2.PlayerParalysis(0x7FFF) + " "
                + UWRuneRulesUw2.CreatureHoldTime(0).ToString("X") + " " + UWRuneRulesUw2.CreatureHoldTime(0x7FFF).ToString("X") + " "
                + (UWRuneRulesUw2.IsRune(0x19E) ? "1" : "0") + (UWRuneRulesUw2.IsRune(0x19F) ? "1" : "0") + (UWRuneRulesUw2.IsRune(0x19D) ? "1" : "0"),
                "4 19 40 13C 110");

            // creat_spell's satellite (part E3): a quarter turn to either side, the floor + 0x12 (one more above 0x78).
            Check.Str("satellite: heading 0x10 to 0x50 or 0xCF, z 0x12, 0x79 + 1 and capped",
                UWSatelliteRulesUw2.StartHeading(0x10, 0).ToString("X") + " " + UWSatelliteRulesUw2.StartHeading(0x10, 1).ToString("X") + " "
                + UWSatelliteRulesUw2.StartZ(0).ToString("X") + " " + UWSatelliteRulesUw2.StartZ(0x67).ToString("X") + " " + UWSatelliteRulesUw2.StartZ(0x70).ToString("X"),
                "50 CF 12 7A 7F");
        }
    }
}
