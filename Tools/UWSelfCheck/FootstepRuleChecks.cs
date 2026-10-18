using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.7 (2026-10-18): UWFootstepRules, the footsteps' formula out of
    /// UWPlayerMovement. Pinned: the interval 0x40 + 0x1770 / (1 + momentum / 4) PIT ticks capped
    /// at 0xC8 (89 ticks at a full run, 114 at half, the cap when sneaking), the note offset 0xF +
    /// momentum / 32, and the momentum from the measured speed, clamped to 0..0x3AC.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckFootstepRules()
        {
            Check.Group("Footsteps (UWFootstepRules - cleanup step 4.7)");

            fExpectInt("footsteps: a full run steps every 89 ticks (0.35 s)", UWFootstepRules.IntervalTicks(UWFootstepRules.FullMomentum), 89);
            fExpectInt("footsteps: half the momentum every 114 (0.45 s)", UWFootstepRules.IntervalTicks(UWFootstepRules.FullMomentum / 2), 114);
            fExpectInt("footsteps: sneaking waits the most, 0xC8 (0.78 s)", UWFootstepRules.IntervalTicks(0x60), UWFootstepRules.MaxIntervalTicks);
            fExpectInt("footsteps: ... and standing too", UWFootstepRules.IntervalTicks(0), UWFootstepRules.MaxIntervalTicks);

            fExpectInt("footsteps: the note offset at a full run is 0xF + 0x3AC / 32", UWFootstepRules.NoteOffset(UWFootstepRules.FullMomentum), 0xF + 29);
            fExpectInt("footsteps: ... standing 0xF", UWFootstepRules.NoteOffset(0), 0xF);

            fExpectInt("footsteps: the full speed is the full momentum", UWFootstepRules.Momentum(3.2f, 3.2f), UWFootstepRules.FullMomentum);
            fExpectInt("footsteps: half the speed is half of it", UWFootstepRules.Momentum(1.6f, 3.2f), UWFootstepRules.FullMomentum / 2);
            fExpectInt("footsteps: faster than full stays at full", UWFootstepRules.Momentum(6.4f, 3.2f), UWFootstepRules.FullMomentum);
            fExpectInt("footsteps: standing is none", UWFootstepRules.Momentum(0f, 3.2f), 0);
            fExpectInt("footsteps: the momentum rounds a half to even (as Mathf.RoundToInt)", UWFootstepRules.Momentum(0.5f, UWFootstepRules.FullMomentum), 0);
        }
    }
}
