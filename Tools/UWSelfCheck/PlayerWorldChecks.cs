using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The reference audit of 2026-10-14, the player and the world (fixes of 2026-10-15): the
    /// full stops the original prints after the compass time, the mantra results and Track or
    /// Detect Monsters.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckPlayerWorld(DataImport pOData)
        {
            Check.Group("Player and world, audit 2026-10-14");

            fCheckReachTerrain();

            fCheckFullStops(pOData);
            fCheckMapNotes();
            fCheckSwimView();
            fCheckArrival(pOData);
        }

        /// <summary>find_good_x_and_y (event/WORLDEV.C:76-167) on a made-up map: every tile open
        /// unless named, a tile fits when it is in the set of free tiles.</summary>
        /// <summary>BlockingTerrain (UWReachRules.IsBlockedByTerrain) on the level 1 channel of
        /// 2026-10-17: floor 9 at 46/5 and 48/5, water at floor 1 on 47/5 between.</summary>
        private static void fCheckReachTerrain()
        {
            System.Func<int, int, int> lfFloor = (piX, piY) => piX == 47 ? 1 : 9;
            System.Func<int, int, int> lfTerrain = (piX, piY) => piX == 47 ? 1 : 0;
            System.Func<int, int, int> lfNoBridge = (piX, piY) => -1;
            System.Func<int, int, int> lfBridge = (piX, piY) => piX == 47 ? 72 : -1;

            fExpectBool("reach: across the water channel from 46/5 is blocked",
                UWReachRules.IsBlockedByTerrain(46, 5, 72, 48, 5, 72, UWReachRules.UseDistanceSquared, false,
                    lfFloor, lfTerrain, lfNoBridge), true);
            fExpectBool("reach: ... with the centre over the channel's tile nothing lies between",
                UWReachRules.IsBlockedByTerrain(47, 5, 72, 48, 5, 72, UWReachRules.UseDistanceSquared, false,
                    lfFloor, lfTerrain, lfNoBridge), false);
            fExpectBool("reach: ... a bridge at the right height carries the line",
                UWReachRules.IsBlockedByTerrain(46, 5, 72, 48, 5, 72, UWReachRules.UseDistanceSquared, false,
                    lfFloor, lfTerrain, lfBridge), false);
            fExpectBool("reach: ... Telekinesis is never blocked",
                UWReachRules.IsBlockedByTerrain(46, 5, 72, 48, 5, 72, UWReachRules.UnlimitedDistance, false,
                    lfFloor, lfTerrain, lfNoBridge), false);
            fExpectBool("reach: ... a thing on the player's own tile is never blocked",
                UWReachRules.IsBlockedByTerrain(48, 5, 72, 48, 5, 72, UWReachRules.UseDistanceSquared, false,
                    lfFloor, lfTerrain, lfNoBridge), false);
        }

        private static void fCheckArrival(DataImport pOData)
        {
            Dictionary<int, UWTile.TileTypeEnum> lOTypes = new Dictionary<int, UWTile.TileTypeEnum>();
            HashSet<int> lOFree = new HashSet<int>();
            List<string> lOTried = new List<string>();

            System.Func<int, int, UWTile.TileTypeEnum> lfType = (piX, piY) =>
                lOTypes.TryGetValue((piX << 8) | piY, out UWTile.TileTypeEnum leType) ? leType : UWTile.TileTypeEnum.open;
            System.Func<int, int, bool> lfFits = (piX, piY) =>
            {
                lOTried.Add(piX + "/" + piY);

                return lOFree.Contains((piX << 8) | piY);
            };

            int liX;
            int liY;

            lOFree.Add((20 << 8) | 20);
            fExpectBool("arrival: a target with room is taken",
                UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY) && liX == 20 && liY == 20, true);

            lOFree.Clear();
            lOFree.Add((20 << 8) | 21);
            lOFree.Add((19 << 8) | 20);
            lOTried.Clear();
            UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY);
            Check.Str("arrival: the first ring in the order -x, -y, +x, +y", liX + "/" + liY + " after " + string.Join(" ", lOTried),
                "19/20 after 20/20 19/20");

            lOFree.Clear();
            lOFree.Add((21 << 8) | 20);
            lOTypes[(20 << 8) | 20] = UWTile.TileTypeEnum.diagonal_ne;
            lOTried.Clear();
            UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY);
            Check.Str("arrival: a diagonal passes on only through its open sides (NE: +x, +y)",
                liX + "/" + liY + " after " + string.Join(" ", lOTried), "21/20 after 20/20 21/20");

            lOTypes.Clear();
            lOFree.Clear();
            lOFree.Add((25 << 8) | 20);
            fExpectBool("arrival: five tiles to the high side are inside the box",
                UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY), true);

            lOFree.Clear();
            lOFree.Add((26 << 8) | 20);
            fExpectBool("arrival: six are not", UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY), false);

            lOFree.Clear();
            lOFree.Add((16 << 8) | 20);
            fExpectBool("arrival: four to the low side are inside",
                UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY), true);

            lOFree.Clear();
            lOFree.Add((15 << 8) | 20);
            fExpectBool("arrival: five are not", UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY), false);

            lOFree.Clear();
            lOFree.Add((0 << 8) | 2);
            fExpectBool("arrival: the box never reaches column 0", UWArrivalRules.FindGoodXAndY(2, 2, lfType, lfFits, null, out liX, out liY), false);

            lOFree.Clear();
            lOTypes[(21 << 8) | 20] = UWTile.TileTypeEnum.solid;
            lOFree.Add((22 << 8) | 20);
            lOTried.Clear();
            UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, null, out liX, out liY);
            fExpectBool("arrival: a wall is tried but passes nothing on (22/20 is reached round it)",
                liX == 22 && liY == 20 && lOTried.Contains("21/20"), true);

            lOTypes.Clear();
            lOFree.Clear();
            List<string> lOCleared = new List<string>();
            fExpectBool("arrival: the second pass clears each tile before it asks",
                UWArrivalRules.FindGoodXAndY(20, 20, lfType, lfFits, (piX, piY) =>
                {
                    lOCleared.Add(piX + "/" + piY);
                    lOFree.Add((piX << 8) | piY);
                }, out liX, out liY) && liX == 20 && liY == 20 && lOCleared.Count == 1, true);

            UWTile lOTile = new UWTile(0, (5u << 4) | (uint)UWTile.TileTypeEnum.open, new ushort[64]);

            fExpectInt("arrival: asked at the floor, height 5 is z 40", UWArrivalRules.GetTestZ(lOTile), 40);

            lOTile.TileType = UWTile.TileTypeEnum.slope_e;
            fExpectInt("arrival: on a slope half a step higher", UWArrivalRules.GetTestZ(lOTile), 44);

            // can_place on the small map of the motion checks (open, floor 2): free, then with a
            // goblin standing on the tile.
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWMotionCore lOCore = new UWMotionCore(lOWorld, pOData.CommonObjectProperties);
            UWTile lOFloorTwo = new UWTile(0, (2u << 4) | (uint)UWTile.TileTypeEnum.open, new ushort[64]);

            fExpectBool("arrival: the adventurer fits on an empty tile",
                UWArrivalRules.AdventurerFits(lOCore, lOFloorTwo, 10, 10), true);

            lOWorld.AddBody(10, 10, new UWMotionBody { Index = 5, ItemId = 0x40, XPos = 4, YPos = 4, ZPos = 16,
                IsMobile = true, IsCreature = true });
            fExpectBool("arrival: ... not with a creature standing there",
                UWArrivalRules.AdventurerFits(lOCore, lOFloorTwo, 10, 10), false);
            fExpectBool("arrival: ... but on the tile beside it",
                UWArrivalRules.AdventurerFits(lOCore, lOFloorTwo, 12, 10), true);

            fExpectBool("arrival: the clearing takes a creature",
                UWArrivalRules.IsClearedForArrival(new UWNpc(0x40), pOData.CommonObjectProperties), true);

            int liTall = -1;
            int liFlat = -1;

            for (int liId = 0; liId < 0x200 && (liTall < 0 || liFlat < 0); liId++)
            {
                if (!pOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOEntry))
                    continue;

                if (lOEntry.Height != 0 && liTall < 0)
                    liTall = liId;
                else if (lOEntry.Height == 0 && liFlat < 0)
                    liFlat = liId;
            }

            fExpectBool("arrival: ... a thing with a height",
                liTall >= 0 && UWArrivalRules.IsClearedForArrival(new UWObject((ushort)liTall), pOData.CommonObjectProperties), true);
            fExpectBool("arrival: ... but not a flat one",
                liFlat >= 0 && !UWArrivalRules.IsClearedForArrival(new UWObject((ushort)liFlat), pOData.CommonObjectProperties), true);
        }

        /// <summary>parse_effect's swimming branch (motion/PLAYMOVE.C:527-544).</summary>
        private static void fCheckSwimView()
        {
            fExpectInt("swim view: amplitude at full speed", UWSwimViewRules.GetAmplitude(100f, 100f), 5);
            fExpectInt("swim view: ... at three quarters", UWSwimViewRules.GetAmplitude(75f, 100f), 3);
            fExpectInt("swim view: ... at half speed the floor of 1", UWSwimViewRules.GetAmplitude(50f, 100f), 1);
            fExpectInt("swim view: ... faster than full counts as full", UWSwimViewRules.GetAmplitude(150f, 100f), 5);

            RecordingRandom lORandom = new RecordingRandom(127, 0);
            UWSwimViewRules.Offsets lOView = UWSwimViewRules.GetOriginal(0x60, 0x40, 100f, 100f, false, lORandom.Next);

            Check.Str("swim view: full speed, phase 4: height, bank, heading, pitch [draws]",
                lOView.HeightEighths + " " + lOView.Bank + " " + lOView.Heading + " " + lOView.Pitch
                    + " [" + string.Join(" ", lORandom.Ranges) + "]",
                "20 0 315 -320 [128 128]");

            lORandom = new RecordingRandom(0, 64, 64);
            lOView = UWSwimViewRules.GetOriginal(0x60, 0, 0f, 100f, true, lORandom.Next);

            Check.Str("swim view: standing, phase 0: the bank drawn first, amplitude 1",
                lOView.HeightEighths + " " + lOView.Bank + " " + lOView.Heading + " " + lOView.Pitch
                    + " [" + string.Join(" ", lORandom.Ranges) + "]",
                "-4 -256 0 0 [512 128 128]");

            lORandom = new RecordingRandom(5);
            lOView = UWSwimViewRules.GetOriginal(0x50, 0x80, 100f, 100f, false, lORandom.Next);

            Check.Str("swim view: at a counter of 0x50 nothing moves and nothing is drawn",
                lOView.HeightEighths + " " + lOView.Bank + " " + lOView.Heading + " " + lOView.Pitch
                    + " [" + string.Join(" ", lORandom.Ranges) + "]",
                "0 0 0 0 []");

            lOView = UWSwimViewRules.GetOriginal(0x60, 0x80, 100f, 100f, false, new RecordingRandom(64, 64).Next);
            fExpectInt("swim view: full speed, phase 8: the bank 5 * 4 << 6", lOView.Bank, 1280);
        }

        /// <summary>ManageDungeonMap and DetermineBetterHit (ui/AUTOMAP.C:376-547), with every
        /// character four pixels wide.</summary>
        private static void fCheckMapNotes()
        {
            System.Func<string, int> lfWidth = psText => psText.Length * 4;
            string lsText;

            fExpectBool("map notes: 99 on the page, one more may start", UWMapNoteRules.CanAddNote(99), true);
            fExpectBool("map notes: 100 fill the page", UWMapNoteRules.CanAddNote(100), false);
            fExpectBool("map notes: 'z' (0x7A) is taken, upper-cased",
                UWMapNoteRules.TryAppend("A", 'z', 10, lfWidth, out lsText) && lsText == "AZ", true);
            fExpectBool("map notes: '{' (0x7B) is not", UWMapNoteRules.TryAppend("A", '{', 10, lfWidth, out lsText), false);
            fExpectBool("map notes: '~' is not", UWMapNoteRules.TryAppend("A", '~', 10, lfWidth, out lsText), false);
            fExpectBool("map notes: a control code is not", UWMapNoteRules.TryAppend("A", '\t', 10, lfWidth, out lsText), false);
            fExpectBool("map notes: ending exactly on x 0x13B fits",
                UWMapNoteRules.TryAppend("AB", 'C', UWMapNoteRules.RightEdge - 12, lfWidth, out lsText), true);
            fExpectBool("map notes: one pixel further does not",
                UWMapNoteRules.TryAppend("AB", 'C', UWMapNoteRules.RightEdge - 11, lfWidth, out lsText), false);
            fExpectBool("map notes: the 46th character fits",
                UWMapNoteRules.TryAppend(new string('A', 45), 'B', 0, psText => 0, out lsText), true);
            fExpectBool("map notes: the 47th does not",
                UWMapNoteRules.TryAppend(new string('A', 46), 'B', 0, psText => 0, out lsText), false);

            // Two overlapping notes, both under the pointer at (20, 100): their middles are at
            // y 98 and 99 (two below the top), at x 24 and 30.
            List<UWLevel.MapNote> lONotes = new List<UWLevel.MapNote>
            {
                new UWLevel.MapNote { Text = "AB", X = 20, Y = 100 },
                new UWLevel.MapNote { Text = "ABCDE", X = 20, Y = 101 }
            };

            fExpectInt("map notes: the eraser takes the vertically nearer middle (the later note)",
                UWMapNoteRules.FindNoteToErase(lONotes, 20, 100, lfWidth), 1);

            lONotes[1] = new UWLevel.MapNote { Text = "A", X = 18, Y = 97 };
            fExpectInt("map notes: farther vertically but nearer horizontally wins too (DetermineBetterHit)",
                UWMapNoteRules.FindNoteToErase(lONotes, 20, 97, lfWidth), 1);

            lONotes[1] = new UWLevel.MapNote { Text = "ABCDEFGH", X = 20, Y = 100 };
            fExpectInt("map notes: equal vertically and farther horizontally keeps the first",
                UWMapNoteRules.FindNoteToErase(lONotes, 20, 98, lfWidth), 0);
            fExpectInt("map notes: below a note's five rows nothing is hit",
                UWMapNoteRules.FindNoteToErase(lONotes, 20, 94, lfWidth), -1);
        }

        /// <summary>print_info (ui/INTERACT.C:130), great_advance and report_advance
        /// (game/SKILLS.C:230, 255), print_path_to (sys/MISCUTIL.C:108).</summary>
        private static void fCheckFullStops(DataImport pOData)
        {
            Check.Str("full stop: the compass time", UWStatusReport.GetTimeLine(pOData, 0), "You guess that it is currently night.");
            Check.Str("full stop: a skill mantra", UWShrineRules.GetAdvancedGreatlyLine(pOData, 3), "You have advanced greatly in Sword.");
            Check.Str("full stop: a group mantra",
                UWShrineRules.GetAdvancedLine(pOData, new List<string> { "Sword", "Axe", "Mace" }), "You have advanced in Sword, Axe and Mace.");
            Check.Str("full stop: Detect Monsters", UWMiscSpellRules.GetDetectionLine(pOData.GetGeneralMessage, 2, 3),
                "You detect a few creatures to the East.");
        }
    }
}
