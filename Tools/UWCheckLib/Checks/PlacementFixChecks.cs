using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THINGS THE ORIGINAL'S DATA MISPLACES (UWPlacementFixes, under "Fix bugs of the original"): the second game's writing
    /// over the door on level 12, 28/33, is drawn at fine 3/4 (the lintel's west side, facing 2) instead of 7/4 (per user 2026-11-20); only
    /// that thing with the original's values moves, and the deviation switch follows the Game option.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPlacementFixes()
        {
            Check.Group("Misplaced things drawn where they were meant to be (UWPlacementFixes)");

            UWObject lOWriting = new UWObject(0x166) { XPos = 7, YPos = 4, Heading = 2 };
            bool lbFound = UWPlacementFixes.TryGet(UWGame.Uw2, 12, 28, 33, lOWriting, out int liX, out int liY, out int liHeading, out float lfNudge);

            Check.Str("placement: UW2 level 12, the writing on 28/33 over the door -> fine, facing and the nudge to the lintel",
                lbFound + " " + liX + "/" + liY + " " + liHeading + " " + lfNudge.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), "True 3/4 2 0.5");
            Check.Bool("placement: ... not in the first game", UWPlacementFixes.TryGet(UWGame.Uw1, 12, 28, 33, lOWriting, out _, out _, out _), false);
            Check.Bool("placement: ... not on another level", UWPlacementFixes.TryGet(UWGame.Uw2, 11, 28, 33, lOWriting, out _, out _, out _), false);
            Check.Bool("placement: ... not when it stands elsewhere in its tile",
                UWPlacementFixes.TryGet(UWGame.Uw2, 12, 28, 33, new UWObject(0x166) { XPos = 6, YPos = 4, Heading = 2 }, out _, out _, out _), false);

            UWRulesDeviations lODeviations = new UWRulesDeviations();

            lODeviations.SetOriginalBugFixes(false);
            bool lbOff = lODeviations.MisplacedObjectsMoved;
            lODeviations.SetOriginalBugFixes(true);

            Check.Str("placement: the switch follows Fix bugs of the original (off, on)", lbOff + " " + lODeviations.MisplacedObjectsMoved, "False True");
        }
    }
}
