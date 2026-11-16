using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>The second game's oil flask, rock hammer and fountain (UWToolRulesUw2, G4 rest part 2, 2026-11-16) against
    /// UseOilOn, UseRockHammerOn and UseMagic (uw2 obj/USEITEMS.C).</summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2Tools()
        {
            Check.Group("UW2 oil flask, rock hammer, key gem and fountain: UseOilOn, UseRockHammerOn, UseKeyGem, UseMagic");

            string fOil(int piId, int piQuality)
            {
                UWToolRulesUw2.OilResult lOResult = UWToolRulesUw2.Oil(piId, piQuality);

                return string.Format("{0:X}:{1}{2}", lOResult.Message - 1, lOResult.Changes ? string.Format("->{0:X}/{1:X}", lOResult.NewId, lOResult.NewQuality) : "",
                    lOResult.FlaskUsed ? "+" : "");
            }

            Check.Str("oil: wood -> torch 0x28, lantern +0x20, torch capped at 0x3F, full, lit, other",
                string.Join(" ", fOil(0xCC, 10), fOil(0x90, 0x10), fOil(0x91, 0x30), fOil(0x90, 0x3F), fOil(0x95, 5), fOil(0x10, 40)),
                "C4:->91/28+ C2:->90/30+ C6:->91/3F+ C3: C5: C0:");

            UWRandom.Use(new ScriptedAreaRandom(1, 0, 1, 1, 0, 1, 0, 2));

            try
            {
                // Large 0x153: draw 1 + 2 + 1 = 4 pieces, sizes by draws 0, 1, 1, 0 -> 0x154, 0x155, 0x155, 0x154.
                List<UWToolRulesUw2.RockPiece> lOLarge = UWToolRulesUw2.BreakRock(0x153, out bool lbLarge);
                // Small 0x156: draw 1 - 1 + 1 = 1 piece, size draw 0 -> 0x157, below the small boulder: a pile of 2 % 6 + 3 = 5 sling stones.
                List<UWToolRulesUw2.RockPiece> lOSmall = UWToolRulesUw2.BreakRock(0x156, out bool lbSmall);

                string fPieces(List<UWToolRulesUw2.RockPiece> pOPieces)
                {
                    List<string> lOText = new List<string>();

                    foreach (UWToolRulesUw2.RockPiece lOPiece in pOPieces)
                        lOText.Add(lOPiece.ObjectId.ToString("X") + (lOPiece.Quantity > 0 ? "x" + lOPiece.Quantity : ""));

                    return string.Join(",", lOText);
                }

                UWToolRulesUw2.BreakRock(0x10, out bool lbOther);
                Check.Str("rock hammer: large boulder in 4, small boulder a pile of sling stones, else nothing",
                    lbLarge + " " + fPieces(lOLarge) + " | " + lbSmall + " " + fPieces(lOSmall) + " | " + lbOther,
                    "True 154,155,155,154 | True 10x5 | False");
            }
            finally
            {
                UWRandom.Use(null);
            }

            // UseKeyGem: gem 7 (0x11E) treated, on the large gem in the world: the castle clock stays, the gems' clock 1 and its
            // line, gem 7's bit is bit 5 (6 and 7 swap), vars[6] 5. Untreated, carried or on anything else: inert.
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck" });
            UWObject lOGem = new UWObject(0x11E) { Owner = 1 };
            UWToolRulesUw2.KeyGemResult lOFused = UWToolRulesUw2.UseKeyGem(lOGem, 0x168, false, lOFile);
            string lsLines = string.Join(",", lOFused.Lines.ConvertAll(piLine => (piLine - 1).ToString("X")));

            Check.Str("key gem: fuses on the large gem - lines, X clocks, QB_GEMS_USED bit, vars[6]; inert otherwise",
                lOFused.Fused + " " + lsLines + " shake " + lOFused.ShakeRounds + " castle " + UWPlayerRecordUw2.XClock(lOFile, 1) + " gems " + UWPlayerRecordUw2.XClock(lOFile, 2)
                + " used " + UWPlayerRecordUw2.GetNumbered(lOFile, UWTrapRulesUw2.FirstQuestByte + UWTrapRulesUw2.GemsUsedQuestByte).ToString("X")
                + " facet " + UWPlayerRecordUw2.GetNumbered(lOFile, UWTrapRulesUw2.GemFacetVariable)
                + " | " + UWToolRulesUw2.UseKeyGem(new UWObject(0x11E), 0x168, false, lOFile).Fused
                + " " + UWToolRulesUw2.UseKeyGem(lOGem, 0x168, true, lOFile).Fused + " " + UWToolRulesUw2.UseKeyGem(lOGem, 0x12E, false, lOFile).Fused,
                "True 152,153 shake 0 castle " + UWPlayerRecordUw2.XClock(UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter { Name = "x" }), 1)
                + " gems 1 used 20 facet 5 | False False False");

            // The moonstones (part 3b): a new character's lie on levels 3 and 45; taking one on 3 clears its place, putting it
            // down on 7 takes the first free one, taking on 45 clears the second, a bag holding it on 9 takes it.
            UWPlayerFile lOStones = UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck" });
            UWObject lOStone = new UWObject(UWMoonstoneRules.MoonstoneId);
            UWObject lOBag = new UWObject(0x80);
            List<string> lOSteps = new List<string>();
            string fStones() => UWPlayerRecordUw2.Moonstone(lOStones, 0) + "/" + UWPlayerRecordUw2.Moonstone(lOStones, 1);

            lOBag.SetContents(new List<UWObject> { lOStone });
            lOSteps.Add(fStones());
            UWMoonstoneRules.OnTakenFromWorldSecondGame(lOStones, lOStone, 3);
            lOSteps.Add(fStones());
            UWMoonstoneRules.OnPutIntoWorldSecondGame(lOStones, lOStone, 7);
            lOSteps.Add(fStones());
            UWMoonstoneRules.OnTakenFromWorldSecondGame(lOStones, lOStone, 0x2D);
            lOSteps.Add(fStones());
            UWMoonstoneRules.OnPutIntoWorldSecondGame(lOStones, lOBag, 9);
            lOSteps.Add(fStones());
            UWMoonstoneRules.OnPutIntoWorldSecondGame(lOStones, new UWObject(0x80), 11);
            lOSteps.Add(fStones());
            Check.Str("moonstones: two places, taken clears this level's, put down takes the first free, a bag holding one counts",
                string.Join(" ", lOSteps), "3/45 0/45 7/45 7/0 7/9 7/9");

            Check.Str("vending: go_vend's items - fish, meat, ale, leeches, water, dagger (0x03, not the battle axe 0x01), lockpick, torch",
                string.Join(",", System.Array.ConvertAll(UWTrapRulesUw2.VendItems, piId => piId.ToString("X"))), "B6,B0,BB,125,BC,3,101,91");
            Check.Str("vending: the price is (value + 1) / 2 + 1", UWTrapRulesUw2.VendPrice(0) + " " + UWTrapRulesUw2.VendPrice(5) + " " + UWTrapRulesUw2.VendPrice(20),
                "1 4 11");

            UWObject lODry = new UWObject(UWToolRulesUw2.Fountain) { Quality = 0, HasQuantity = true, Quantity = 1 };
            UWObject lOWet = new UWObject(UWToolRulesUw2.Fountain) { Quality = 40, HasQuantity = true, Quantity = 1 };
            Check.Str("fountain: quality 0 dry, else the water refreshes (no shipped fountain carries a spell)",
                (UWToolRulesUw2.FountainLine(lODry, null, out bool _, out int _, out int _) - 1).ToString("X") + " "
                + (UWToolRulesUw2.FountainLine(lOWet, null, out bool lbSpell, out int _, out int _) - 1).ToString("X") + " " + lbSpell,
                "93 FC False");
        }
    }
}
