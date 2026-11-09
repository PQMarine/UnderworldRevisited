using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// A DOCKED PANEL'S HANDLE MOVED ALONG ITS EDGE (per user, 2026-11-08): UWHudLayout keeps the
    /// handle's height alone, anchored by thirds, the part still docked (anchor X -2 in the text); the
    /// text reads back the same, a later free move or a reset drops it, a size keeps it, and an entry
    /// a version before wrote is unchanged.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckHudLayoutDock()
        {
            Check.Group("Layout: a docked panel's handle moved along its edge (UWHudLayout)");

            const float ScreenWidth = 1920f;
            const float ScreenHeight = 1080f;
            const float PixelScale = 3f;
            const float HandleHeight = 40f * PixelScale;
            UWHudLayout lOLayout = new UWHudLayout(9);

            // The handle dragged low on the right edge (the character panel, element 7).
            lOLayout.MoveDock(7, new UWRect(1893f, 200f, 27f, HandleHeight), ScreenHeight, PixelScale);
            lOLayout.TryPlaceDock(7, HandleHeight, ScreenHeight, PixelScale, 12f, ScreenHeight - 162f, out float lfBottom);

            Check.Str("dock: docked, not placed, the handle where it was dragged",
                lOLayout.Get(7).Docked + " " + lOLayout.Get(7).Placed + " " + lfBottom, "True False 200");

            string lsText = lOLayout.ToString();
            UWHudLayout lOAgain = UWHudLayout.Parse(lsText, 9);

            lOAgain.TryPlaceDock(7, HandleHeight, ScreenHeight, PixelScale, 12f, ScreenHeight - 162f, out float lfAgain);
            Check.Str("dock: the text (" + lsText + ") reads back the same", lOAgain.ToString() + " " + lfAgain, lsText + " 200");

            // Another screen height: anchored to the bottom third, it keeps its distance from the bottom.
            lOAgain.TryPlaceDock(7, HandleHeight, 1440f, PixelScale, 12f, 1440f - 162f, out float lfTaller);
            Check.Str("dock: on a taller screen the low handle keeps its distance from the bottom", lfTaller.ToString(), "200");

            // Kept on the screen.
            lOAgain.TryPlaceDock(7, HandleHeight, ScreenHeight, PixelScale, 300f, ScreenHeight - 162f, out float lfKept);
            Check.Str("dock: kept above the floor given (the message box for the rune panel)", lfKept.ToString(), "300");

            // A size keeps it docked; a free move drops it; a reset too.
            lOAgain.SetPercent(7, 120, new UWRect(1700f, 100f, 200f, 500f), ScreenWidth, ScreenHeight, PixelScale);
            Check.Str("dock: a new size keeps the handle's height", lOAgain.Get(7).Docked + " " + lOAgain.Get(7).Placed + " " + lOAgain.Get(7).Percent, "True False 120");

            lOAgain.Move(7, new UWRect(800f, 300f, 200f, 500f), ScreenWidth, ScreenHeight, PixelScale);
            Check.Str("dock: moved freely it is a window, no longer docked", lOAgain.Get(7).Docked + " " + lOAgain.Get(7).Placed, "False True");

            lOLayout.Reset(7);
            Check.Bool("dock: a reset puts the default height back", lOLayout.TryPlaceDock(7, HandleHeight, ScreenHeight, PixelScale, 0f, ScreenHeight, out float _), false);

            // A version before: "only sized" and moved parts read as they did.
            UWHudLayout lOOld = UWHudLayout.Parse("7:-1:0:0:0:120;1:1:1:-4:-4:100;", 9);

            Check.Str("dock: an older text reads as before (sized, moved, nothing docked)",
                lOOld.Get(7).Docked + " " + lOOld.Get(7).Placed + " " + lOOld.Get(7).Percent + " " + lOOld.Get(1).Placed + " " + lOOld.ToString(),
                "False False 120 True 1:1:1:-4:-4:100;7:-1:0:0:0:120;");
        }
    }
}
