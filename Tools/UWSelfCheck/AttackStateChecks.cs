using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.14 (2026-10-22): the player's blow from the press to the hit frame as
    /// UWPlayerAttackState - the charge after the wind-up (player_attack's play_pow: the clock starts
    /// on the first frame after it, then the weapon's speed per step, capped at 100), the kind by
    /// the third of the view or the view's height, the clicked ninth, what the release keeps and the
    /// reset when the weapon goes away. Data-free.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckAttackState()
        {
            Check.Group("Attack state (UWPlayerAttackState - cleanup step 5.14)");

            UWPlayerAttackState lOState = new UWPlayerAttackState();

            Check.Bool("attack: no kind before the first blow", lOState.Kind == AttackTypes.Ready, true);
            Check.Int("attack: the middle ninth before the first blow", lOState.Ninth, UWPlayerAttackState.MiddleNinth);

            // The charge: a dagger's speed 30, steps of 0.072 s.
            lOState.SetKind(AttackTypes.Slash);
            lOState.Begin(30);
            Check.Bool("charge: attacking", lOState.IsAttacking, true);
            Check.Int("charge: nothing at the start", lOState.PlayPow, 0);
            lOState.StepCharge(0.5f, 0.072f);
            Check.Int("charge: the first frame after the wind-up only starts the clock", lOState.PlayPow, 0);
            lOState.StepCharge(0.07f, 0.072f);
            Check.Int("charge: below a step nothing yet", lOState.PlayPow, 0);
            lOState.StepCharge(0.01f, 0.072f);
            Check.Int("charge: one step adds the speed", lOState.PlayPow, 30);
            lOState.StepCharge(0.3f, 0.072f);
            Check.Int("charge: several steps in one frame, capped at 100", lOState.PlayPow, 100);
            Check.Bool("charge: as a fraction", System.Math.Abs(lOState.Charge - 1f) < 0.0001f, true);
            lOState.StepCharge(1f, 0f);
            Check.Int("charge: a step of 0 takes the default and stays capped", lOState.PlayPow, 100);

            // The release keeps the blow for the hit frame.
            lOState.Release(true);
            Check.Bool("release: the blow waits for its hit frame", lOState.BlowPending, true);
            Check.Bool("release: the charge kept", System.Math.Abs(lOState.PendingCharge - 1f) < 0.0001f, true);
            Check.Bool("release: the kind kept", lOState.PendingKind == AttackTypes.Slash, true);
            Check.Bool("hit frame: takes the blow once", lOState.TakePendingBlow(), true);
            Check.Bool("hit frame: and not twice", lOState.TakePendingBlow(), false);
            lOState.Release(false);
            Check.Bool("release before the wind-up: no blow", lOState.BlowPending, false);

            // Reset: water mid-strike (per user, 2026-10-03).
            lOState.Begin(30);
            lOState.Release(true);
            lOState.Reset();
            Check.Bool("reset: no blow waits", lOState.BlowPending, false);
            Check.Bool("reset: not attacking", lOState.IsAttacking, false);
            Check.Int("reset: no charge", lOState.PlayPow, 0);

            // The kind by the thirds and by the view's height.
            Check.Bool("kind: upper third an overhead bash", UWPlayerAttackState.KindOfThird(0.9f) == AttackTypes.Hack, true);
            Check.Bool("kind: lower third a stab", UWPlayerAttackState.KindOfThird(0.1f) == AttackTypes.Stab, true);
            Check.Bool("kind: the middle a slash", UWPlayerAttackState.KindOfThird(0.5f) == AttackTypes.Slash, true);
            Check.Bool("kind: on the upper edge a bash",
                UWPlayerAttackState.KindOfThird(UWPlayerAttackState.UpperThirdFraction) == AttackTypes.Hack, true);
            Check.Bool("kind: looking up 12 degrees a bash", UWPlayerAttackState.KindOfPitch(12f) == AttackTypes.Hack, true);
            Check.Bool("kind: looking down 12 degrees a stab", UWPlayerAttackState.KindOfPitch(-12f) == AttackTypes.Stab, true);
            Check.Bool("kind: straight a slash", UWPlayerAttackState.KindOfPitch(5f) == AttackTypes.Slash, true);

            // The ninths: middle column by kind, the classic scheme's clicked column after.
            lOState.SetKind(AttackTypes.Stab);
            Check.Int("ninth: stab in the middle column", lOState.Ninth, 2);
            lOState.SetKind(AttackTypes.Slash);
            Check.Int("ninth: slash in the middle column", lOState.Ninth, 5);
            lOState.SetKind(AttackTypes.Hack);
            Check.Int("ninth: bash in the middle column", lOState.Ninth, 8);
            lOState.SetColumn(UWPlayerAttackState.ColumnOfThird(0.9f));
            Check.Int("ninth: bash in the right column", lOState.Ninth, 9);
            lOState.SetKind(AttackTypes.Stab);
            lOState.SetColumn(UWPlayerAttackState.ColumnOfThird(0.1f));
            Check.Int("ninth: stab in the left column", lOState.Ninth, 1);

            // The original table's kinds.
            Check.Bool("table: bash for the overhead strike",
                UWPlayerAttackState.CombatKindOf(AttackTypes.Hack) == UWCombat.AttackKind.Bash, true);
            Check.Bool("table: bash with no kind",
                UWPlayerAttackState.CombatKindOf(AttackTypes.Ready) == UWCombat.AttackKind.Bash, true);
            Check.Bool("table: stab", UWPlayerAttackState.CombatKindOf(AttackTypes.Stab) == UWCombat.AttackKind.Stab, true);
            Check.Bool("table: slash", UWPlayerAttackState.CombatKindOf(AttackTypes.Slash) == UWCombat.AttackKind.Slash, true);
        }
    }
}
