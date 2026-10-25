using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 7.7B (2026-10-25): the game's own pointer, engine-free (UWPointerMotion) - the move by
    /// the mouse's counts times the Pointer speed (1 = one count per screen pixel, no acceleration), held
    /// in the window and in the classic walk's view window; the capture starting at the system pointer;
    /// the release with its warp, kept until the system pointer arrives or the platform refuses it; the
    /// focus's click taken out (UWFocusClick); and the setting's range (UWUserSettingsTable.PointerSpeed).
    /// Data-free.
    /// </summary>
    public static partial class Program
    {
        private static void fExpectAt(string psName, UWPointerMotion pOMotion, float pfX, float pfY)
        {
            Check.Near(psName + ": x", pOMotion.X, pfX, 0.0001);
            Check.Near(psName + ": y", pOMotion.Y, pfY, 0.0001);
        }

        private static void fCheckPointerMotion()
        {
            Check.Group("Pointer of our own (UWPointerMotion, UWFocusClick - cleanup 7.7B)");

            UWPointerMotion lOMotion = new UWPointerMotion();

            // The capture starts where the system pointer stands.
            lOMotion.Capture(400f, 300f, 1920f, 1080f);
            Check.Bool("captured", lOMotion.IsCaptured, true);
            fExpectAt("the capture starts at the system pointer", lOMotion, 400f, 300f);

            // One count per pixel at 1, a quarter at 0.25, four at 4; no acceleration (a fast move is the
            // sum of slow ones).
            lOMotion.Move(10f, -5f, 1f, 1920f, 1080f);
            fExpectAt("speed 1: a count is a pixel", lOMotion, 410f, 295f);
            lOMotion.Move(8f, 4f, 0.25f, 1920f, 1080f);
            fExpectAt("speed 0.25: four counts a pixel", lOMotion, 412f, 296f);
            lOMotion.Move(-3f, 2f, 4f, 1920f, 1080f);
            fExpectAt("speed 4: a count is four pixels", lOMotion, 400f, 304f);
            lOMotion.Move(100f, 0f, 1f, 1920f, 1080f);
            lOMotion.Move(-100f, 0f, 1f, 1920f, 1080f);
            fExpectAt("no acceleration: there and back again is where it was", lOMotion, 400f, 304f);

            // Held in the window: 0 to width - 1, 0 to height - 1.
            lOMotion.Move(-5000f, -5000f, 1f, 1920f, 1080f);
            fExpectAt("held at the bottom-left corner", lOMotion, 0f, 0f);
            lOMotion.Move(9000f, 9000f, 1f, 1920f, 1080f);
            fExpectAt("held at the top-right corner", lOMotion, 1919f, 1079f);
            lOMotion.Place(2500f, 500f, 1280f, 720f);
            fExpectAt("placed outside a smaller window: held in it", lOMotion, 1279f, 500f);

            // The classic walk's view window holds it, borders included.
            lOMotion.Place(100f, 100f, 1920f, 1080f);
            Check.Bool("inside the view window it stays", lOMotion.Clamp(50f, 50f, 300f, 200f), false);
            fExpectAt("... where it was", lOMotion, 100f, 100f);
            lOMotion.Move(500f, -80f, 1f, 1920f, 1080f);
            Check.Bool("past the view window's corner it is held", lOMotion.Clamp(50f, 50f, 300f, 200f), true);
            fExpectAt("... on its border", lOMotion, 300f, 50f);

            // The release warps the system pointer to ours, and ours stays until it is there.
            lOMotion.Release(true);
            Check.Bool("released", lOMotion.IsCaptured, false);
            Check.Bool("the warp is on its way", lOMotion.IsWarpPending, true);
            lOMotion.FollowSystem(960f, 540f);
            fExpectAt("the system still reports the middle: ours stays", lOMotion, 300f, 50f);
            lOMotion.FollowSystem(301f, 51f);
            Check.Bool("arrived within two pixels: the warp is done", lOMotion.IsWarpPending, false);
            fExpectAt("... and the system's position counts", lOMotion, 301f, 51f);
            lOMotion.FollowSystem(700f, 20f);
            fExpectAt("free, it follows the system pointer", lOMotion, 700f, 20f);

            // A warp the platform refuses (Wayland, XWayland): the system's position after WarpFrames frames.
            lOMotion.Capture(700f, 20f, 1920f, 1080f);
            lOMotion.Release(true);

            for (int liFrame = 1; liFrame < UWPointerMotion.WarpFrames; liFrame++)
                lOMotion.FollowSystem(5f, 5f);

            fExpectAt("refused: ours holds for WarpFrames - 1 frames", lOMotion, 700f, 20f);
            lOMotion.FollowSystem(5f, 5f);
            Check.Bool("refused: the warp is given up", lOMotion.IsWarpPending, false);
            fExpectAt("... and the system's position counts", lOMotion, 5f, 5f);

            // Lost with the focus: no warp, the system's position at once.
            lOMotion.Capture(5f, 5f, 1920f, 1080f);
            lOMotion.Release(false);
            Check.Bool("released with the focus: no warp waited for", lOMotion.IsWarpPending, false);
            lOMotion.FollowSystem(900f, 900f);
            fExpectAt("... the system's position at once", lOMotion, 900f, 900f);

            // The click that gives the focus back.
            UWFocusClick lOFocus = new UWFocusClick();

            Check.Bool("no focus change: nothing watched", lOFocus.IsWatching, false);
            Check.Bool("no focus change: a press goes through", lOFocus.Swallows(true, 10.0), false);

            lOFocus.FocusGained();
            Check.Bool("focus back, before the first frame: a press is the focus's click", lOFocus.Swallows(true, 50.0), true);
            Check.Bool("... held: still taken out", lOFocus.Swallows(true, 99.0), true);
            Check.Bool("... every button up: passes, the click is over", lOFocus.Swallows(false, 99.1), false);
            Check.Bool("... and the next press is the player's", lOFocus.Swallows(true, 99.2), false);
            Check.Bool("... nothing watched any more", lOFocus.IsWatching, false);

            lOFocus.FocusGained();
            lOFocus.Tick(20.0);
            Check.Bool("a press within the window after the first frame is the focus's", lOFocus.Swallows(true, 20.0 + UWFocusClick.WindowSeconds - 0.01), true);
            lOFocus.Swallows(false, 20.5);

            lOFocus.FocusGained();
            lOFocus.Tick(30.0);
            Check.Bool("a mouse moved without a button does not end the window", lOFocus.Swallows(false, 30.1), false);
            Check.Bool("... a press after it within the window is still the focus's", lOFocus.Swallows(true, 30.2), true);
            lOFocus.Swallows(false, 30.3);

            lOFocus.FocusGained();
            lOFocus.Tick(40.0);
            lOFocus.Tick(40.0 + UWFocusClick.WindowSeconds + 0.01);
            Check.Bool("past the window: nothing watched", lOFocus.IsWatching, false);
            Check.Bool("... a press is the player's", lOFocus.Swallows(true, 40.5), false);

            lOFocus.FocusGained();
            lOFocus.FocusLost();
            Check.Bool("the focus lost again: nothing watched", lOFocus.IsWatching, false);

            // The setting: 0.25 to 4, 1 when missing or not positive, in five per cent steps.
            UWFloatSettingDef lODef = UWUserSettingsTable.PointerSpeed;
            UWUserData lOData = new UWUserData();

            Check.Near("a new file reads 1", lODef.Read(lOData), 1.0, 0.0);
            lOData.PointerSpeed = 0f;
            Check.Near("0 reads 1", lODef.Read(lOData), 1.0, 0.0);
            lOData.PointerSpeed = 0.1f;
            Check.Near("0.1 reads the least, 0.25", lODef.Read(lOData), 0.25, 0.0);
            lOData.PointerSpeed = 9f;
            Check.Near("9 reads the most, 4", lODef.Read(lOData), 4.0, 0.0);
            lODef.Write(lOData, 6f);
            Check.Near("written within the range", lOData.PointerSpeed, 4.0, 0.0);
            Check.Near("the step: five per cent", lODef.Step, 0.05, 0.000001);
            Check.Str("the row's label", lODef.LabelId, "setting.PointerSpeed");
            Check.Str("the row's hint", lODef.HintId, "setting.PointerSpeed.hint");
        }
    }
}
