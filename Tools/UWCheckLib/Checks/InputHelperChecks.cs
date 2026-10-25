using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup phase 7, wave 1 (2026-10-25): the input's engine-free helpers. Decision 18's table - the
    /// easy steps take the keys of the walk and turn bindings they belong to, per scheme, as settings.json
    /// names those bindings (UWEasyMovement.FollowedKeys). Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckEasyStepKeys()
        {
            Check.Group("Easy step keys (UWEasyMovement.FollowedKeys - decision 18)");

            Check.Int("four steps follow a walk or turn key", UWEasyMovement.FollowedKeys.Length, 4);

            HashSet<string> lOSteps = new HashSet<string>();
            List<string> lsClassic = new List<string>();
            List<string> lsModern = new List<string>();

            foreach (UWEasyMovement.FollowedKey lOFollowed in UWEasyMovement.FollowedKeys)
            {
                lOSteps.Add(lOFollowed.EasyAction);

                // The first three fields of a settings.json rebinding line, "Map|Action|BindingIndex".
                lsClassic.Add(lOFollowed.EasyAction + " <- Player|" + lOFollowed.ClassicAction + "|" + lOFollowed.ClassicBinding);
                lsModern.Add(lOFollowed.EasyAction + " <- Player|" + lOFollowed.ModernAction + "|" + lOFollowed.ModernBinding);
            }

            Check.Int("each step once", lOSteps.Count, 4);
            Check.Bool("X keeps its own key", lOSteps.Contains(UWEasyMovement.OwnKeyAction), false);
            Check.Str("X is the step that does not go off a ledge", UWEasyMovement.OwnKeyAction, "EasyWalkForward");

            Check.Str("classic: forward and back on the walk, the turns on the turn keys", string.Join(", ", lsClassic),
                "EasyForward <- Player|Move|1, EasyBack <- Player|Move|2, EasyTurnLeft <- Player|Turn|1, EasyTurnRight <- Player|Turn|2");
            Check.Str("modern: forward and back on the walk, the turns on the keys beside it", string.Join(", ", lsModern),
                "EasyForward <- Player|Move|1, EasyBack <- Player|Move|2, EasyTurnLeft <- Player|Move|3, EasyTurnRight <- Player|Move|4");
        }

        /// <summary>
        /// Cleanup step 7.3: the one key repeat (UWRepeat) and the one tap and hold (UWHoldTap), with
        /// each place's numbers as they stood before (UWKitKeyNav 0.4 s then 0.06 s, the help's text area
        /// and reading mode 0.4 s then 0.05 s, the letter grid 0.35 s then 0.09 s, the counts 0.35 s then
        /// 0.07 s, the pad pointer's wheel 0.35 s then 0.22 to 0.08 s by the stick, the easy movement the
        /// setting both times; E 0.4 s, the pad's panel button 0.5 s, the cast trigger 0.35 s) and each
        /// place's rule. The times stay off the exact due moments, where float sums decide.
        /// </summary>
        private static void fCheckInputRepeat()
        {
            Check.Group("Key repeat and tap/hold (UWRepeat, UWHoldTap - cleanup step 7.3)");

            // The kit's arrow keys: the next due time accumulates, a missed one starts from now.
            UWRepeat lONav = new UWRepeat(0.4f, 0.06f, UWRepeat.RuleEnum.AccumulateOrNow);

            Check.Bool("nav: the press acts", lONav.Tick(1, true, true, 0f), true);
            Check.Int("nav: ... and is held", lONav.Held, 1);
            Check.Bool("nav: not before the delay", lONav.Tick(1, false, true, 0.39f), false);
            Check.Bool("nav: after the delay", lONav.Tick(1, false, true, 0.401f), true);
            Check.Bool("nav: not before every", lONav.Tick(1, false, true, 0.45f), false);
            Check.Bool("nav: every 0.06 after the last due time", lONav.Tick(1, false, true, 0.461f), true);
            Check.Bool("nav: a late frame acts once", lONav.Tick(1, false, true, 1f), true);
            Check.Bool("nav: ... and the next is every from now", lONav.Tick(1, false, true, 1.05f), false);
            Check.Bool("nav: ... 1.06", lONav.Tick(1, false, true, 1.061f), true);
            Check.Bool("nav: let go", lONav.Tick(1, false, false, 1.2f), false);
            Check.Int("nav: ... nothing held", lONav.Held, 0);
            Check.Bool("nav: the other direction's press", lONav.Tick(-1, true, true, 1.3f), true);
            Check.Bool("nav: a held key of another direction does nothing", lONav.Tick(1, false, true, 2f), false);

            // The help's text area and reading mode: one repeat for all keys, a missed due time is now.
            UWRepeat lOArea = new UWRepeat(0.4f, 0.05f, UWRepeat.RuleEnum.AccumulateOrCatchUp);

            Check.Bool("area: Backspace pressed", lOArea.Tick(8, true, true, 0f), true);
            Check.Bool("area: Delete held meanwhile does nothing", lOArea.Tick(71, false, true, 0.5f), false);
            Check.Bool("area: Backspace after the delay", lOArea.Tick(8, false, true, 0.401f), true);
            Check.Bool("area: a late frame acts", lOArea.Tick(8, false, true, 1f), true);
            Check.Bool("area: ... and once more at once (catching up)", lOArea.Tick(8, false, true, 1.01f), true);
            Check.Bool("area: ... then every 0.05 again", lOArea.Tick(8, false, true, 1.02f), false);
            Check.Bool("area: another key's press takes the repeat", lOArea.Tick(71, true, true, 1.03f), true);
            Check.Bool("area: ... the first key no more", lOArea.Tick(8, false, true, 1.5f), false);

            // The letter grid: the next repeat from the step before; another direction is a new push.
            UWRepeat lOGrid = new UWRepeat(0.35f, 0.09f, UWRepeat.RuleEnum.FromNow);

            Check.Bool("grid: the push steps", lOGrid.Hold(5, 0f), true);
            Check.Bool("grid: held, not before 0.35", lOGrid.Hold(5, 0.34f), false);
            Check.Bool("grid: held, at 0.35", lOGrid.Hold(5, 0.351f), true);
            Check.Bool("grid: a late step", lOGrid.Hold(5, 0.5f), true);
            Check.Bool("grid: ... the next 0.09 after it", lOGrid.Hold(5, 0.58f), false);
            Check.Bool("grid: ... 0.59", lOGrid.Hold(5, 0.591f), true);
            Check.Bool("grid: another direction steps at once", lOGrid.Hold(9, 0.6f), true);
            Check.Bool("grid: ... with the delay again", lOGrid.Hold(9, 0.8f), false);
            Check.Bool("grid: let go", lOGrid.Hold(0, 0.9f), false);
            Check.Int("grid: ... nothing held", lOGrid.Held, 0);

            // The counts' d-pad (UWCountInput): the press time for the fast step after two seconds.
            UWRepeat lOCount = new UWRepeat(UWCountInput.FirstRepeatSeconds, UWCountInput.RepeatSeconds, UWRepeat.RuleEnum.FromNow);

            lOCount.Hold(1, 3f);
            Check.Bool("count: held at 3 + 0.35", lOCount.Hold(1, 3.351f), true);
            Check.Bool("count: the press time is kept", lOCount.PressedAt == 3f, true);

            // The pad pointer's wheel: each repeat its own interval after the last due time, however late.
            UWRepeat lOWheel = new UWRepeat(0.35f, 0.08f, UWRepeat.RuleEnum.Accumulate);

            lOWheel.Every = 0.22f;
            Check.Bool("wheel: a notch on the push", lOWheel.Tick(1, lOWheel.Held == 0, true, 0f), true);
            Check.Bool("wheel: the first repeat after 0.35", lOWheel.Tick(1, lOWheel.Held == 0, true, 0.351f), true);
            Check.Bool("wheel: a late frame", lOWheel.Tick(1, lOWheel.Held == 0, true, 0.6f), true);
            Check.Bool("wheel: ... the next due 0.79, not from now", lOWheel.Tick(1, lOWheel.Held == 0, true, 0.78f), false);
            Check.Bool("wheel: ... 0.79", lOWheel.Tick(1, lOWheel.Held == 0, true, 0.791f), true);

            // The easy movement: the setting both as the delay and between the steps.
            UWRepeat lOEasy = new UWRepeat(UWEasyMovement.DefaultRepeatSeconds, UWEasyMovement.DefaultRepeatSeconds, UWRepeat.RuleEnum.FromNow);

            Check.Bool("easy: the step at once", lOEasy.Hold(12, 0f), true);
            Check.Bool("easy: not before the interval", lOEasy.Hold(12, 0.24f), false);
            Check.Bool("easy: after it", lOEasy.Hold(12, 0.251f), true);
            Check.Bool("easy: another command at once", lOEasy.Hold(10, 0.3f), true);

            // E: the hold goes on to the release, which taps only before the hold and only as a release.
            UWHoldTap lOUse = new UWHoldTap(0.4f, UWHoldTap.UseKey);

            Check.Bool("use: no press, no time held", lOUse.HeldSeconds == -1f, true);
            lOUse.Press(10f);
            Check.Bool("use: before the hold nothing", lOUse.Update(true, false, 10.39f) == UWHoldTap.ResultEnum.None, true);
            Check.Bool("use: the time held by the clock", System.Math.Abs(lOUse.HeldSeconds - 0.39f) < 0.001f, true);
            Check.Bool("use: the hold at 0.4", lOUse.Update(true, false, 10.401f) == UWHoldTap.ResultEnum.Hold, true);
            Check.Bool("use: ... the press goes on", lOUse.IsPressed, true);
            Check.Bool("use: ... the hold once", lOUse.Update(true, false, 11f) == UWHoldTap.ResultEnum.None, true);
            Check.Bool("use: the release after the hold taps nothing", lOUse.Update(false, true, 11.1f) == UWHoldTap.ResultEnum.None, true);
            Check.Bool("use: ... the press is over", lOUse.IsPressed, false);
            lOUse.Press(20f);
            Check.Bool("use: a release before the hold taps", lOUse.Update(false, true, 20.2f) == UWHoldTap.ResultEnum.Tap, true);
            lOUse.Press(30f);
            Check.Bool("use: up without a release (lost) taps nothing", lOUse.Update(false, false, 30.2f) == UWHoldTap.ResultEnum.None, true);
            lOUse.Press(40f);
            lOUse.Cancel();
            Check.Bool("use: a cancelled press taps nothing", lOUse.Update(false, true, 40.2f) == UWHoldTap.ResultEnum.None, true);

            // The pad's panel button and cast trigger: the hold ends the press; up before it taps.
            UWHoldTap lOPanel = new UWHoldTap(0.5f, UWHoldTap.PadButton);

            lOPanel.Press(0f);
            Check.Bool("pad: before the hold nothing", lOPanel.Update(true, false, 0.49f) == UWHoldTap.ResultEnum.None, true);
            Check.Bool("pad: the hold at 0.5", lOPanel.Update(true, false, 0.501f) == UWHoldTap.ResultEnum.Hold, true);
            Check.Bool("pad: ... ends the press", lOPanel.IsPressed, false);
            Check.Bool("pad: ... the release does nothing", lOPanel.Update(false, true, 0.6f) == UWHoldTap.ResultEnum.None, true);
            lOPanel.Press(1f);
            Check.Bool("pad: up taps, released or not", lOPanel.Update(false, false, 1.1f) == UWHoldTap.ResultEnum.Tap, true);

            UWHoldTap lOCast = new UWHoldTap(0.35f, UWHoldTap.PadButton);

            lOCast.Press(0f);
            Check.Bool("cast: the hold at 0.35", lOCast.Update(true, false, 0.351f) == UWHoldTap.ResultEnum.Hold, true);
            lOCast.Press(1f);
            Check.Bool("cast: a tap", lOCast.Update(false, true, 1.2f) == UWHoldTap.ResultEnum.Tap, true);
        }
    }
}
