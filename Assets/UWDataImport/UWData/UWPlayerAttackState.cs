using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PLAYER'S BLOW FROM THE PRESS TO THE HIT FRAME (cleanup step 5.14, 2026-10-22: ten fields of
	/// Interaction until then, now the AttackState of UWPlayerAttackInput): the press (where it went
	/// down, from the pad or not), the kind of blow and the clicked ninth, the charge counted after
	/// the wind-up (player_attack's play_pow), and what the release keeps for the hit frame - the
	/// charge, the kind and the ninth. The weapon's wind-up, the input and the clock are the Unity
	/// side's; nothing here reads the engine.
	///
	/// THE KIND OF BLOW is the weapon animation's AttackTypes: Hack the overhead bash (the original
	/// table's bash, Interaction.AttackEnum.Jab), Stab, Slash, and Ready for "none chosen yet"
	/// (Interaction.AttackEnum.Idle) - the original table knows exactly the three of UWCombat.
	/// </summary>
	public sealed class UWPlayerAttackState
	{
		/// <summary>The middle ninth, 5 - the slash in the middle column.</summary>
		public const int MiddleNinth = 5;

		/// <summary>Above this height in the view window lies the upper third, below
		/// LowerThirdFraction the lower one. 0 is the bottom.</summary>
		public const float UpperThirdFraction = 2f / 3f;

		public const float LowerThirdFraction = 1f / 3f;

		/// <summary>One charge step when the caller hands none - see ChargeStepMilliseconds.</summary>
		public const float DefaultChargeStepSeconds = 0.072f;

		/// <summary>One charge step after the wind-up, in milliseconds: the default of the rule set's
		/// combat.player.chargeStepMilliseconds (cleanup phase 10; UWSettings.AttackChargeStepSeconds
		/// until then). Fitted to the user's stopwatch in 2026-08 - see
		/// UWPlayerAttackInput.fGetChargeSpeed; the original's own step is not settled.</summary>
		public const int ChargeStepMilliseconds = 72;

		/// <summary>The fist's speed, for a weapon table that cannot be read.</summary>
		public const int FallbackChargeSpeed = 15;

		/// <summary>How far the view has to look up or down, in degrees, for a bash or a thrust
		/// (modern scheme).</summary>
		public const float ModernSwingPitch = 12f;

		/// <summary>
		/// Is attack in progress?
		/// </summary>
		public bool IsAttacking { get; set; }

		/// <summary>A strike is running and waiting for its hit frame.</summary>
		public bool BlowPending { get; set; }

		/// <summary>The current right-button press began in the viewport - only such a press charges
		/// the weapon (see UWPlayerAttackInput.UpdatePress).</summary>
		public bool PressInViewport { get; set; }

		/// <summary>The weapon button's press came from the gamepad's trigger (UWGamepad): it strikes
		/// at the crosshair whatever the mouse pointer lies on.</summary>
		public bool FromPad { get; set; }

		/// <summary>The kind chosen for the running blow; Ready before the first.</summary>
		public AttackTypes Kind { get; private set; } = AttackTypes.Ready;

		/// <summary>The clicked ninth of the view, 1 to 9 from the bottom left (player_attack's swing,
		/// resolve_attack's towhere) - chosen with the attack kind (UWPlayerAttackInput.fChooseAttackKind),
		/// kept for the hit frame like the kind.</summary>
		public int Ninth { get; private set; } = MiddleNinth;

		/// <summary>What the release kept for the hit frame: the charge (0 to 1), the kind and the
		/// ninth. The charge counter is passed on unchanged; the conversion to the weapon's damage
		/// factor only happens on the hit (UWCombat.MapCharge).</summary>
		public float PendingCharge { get; private set; }

		public AttackTypes PendingKind { get; private set; } = AttackTypes.Ready;

		public int PendingNinth { get; private set; } = MiddleNinth;

		/// <summary>The charge of the blow, 0 to 100 - player_attack's play_pow (see StepCharge).</summary>
		public int PlayPow { get; private set; }

		/// <summary>Seconds gathered towards the next charge step; below 0 until the first frame
		/// after the wind-up (player_attack's elapsed of -1).</summary>
		public float ChargeElapsed { get; private set; } = -1f;

		/// <summary>What one charge step adds - the melee table's speed of the weapon in hand.</summary>
		public int ChargeSpeed { get; private set; }

		/// <summary>The charge from 0 (none) to 1 (max): play_pow / 100 (Interaction.AttackCharge).</summary>
		public float Charge => PlayPow / (float)UWCombat.FullCharge;

		/// <summary>play_pow 0 and the step clock stopped (player_attack at the swing's start).</summary>
		public void ClearCharge()
		{
			PlayPow = 0;
			ChargeElapsed = -1f;
		}

		/// <summary>
		/// Begins a strike: the wind-up starts, the charge waits for it (StepCharge, by the
		/// weapon's speed). The kind is chosen before (SetKind, SetColumn).
		/// </summary>
		public void Begin(int piChargeSpeed)
		{
			ClearCharge();
			ChargeSpeed = piChargeSpeed;
			IsAttacking = true;
		}

		/// <summary>
		/// player_attack's charge while the button is held, once the wind-up is through (the caller
		/// asks the weapon): on the first frame after it the clock starts (elapsed -1 to 0), then
		/// every pfStepSeconds (combat.player.chargeStepMilliseconds; 0 or less takes the default) the
		/// weapon's speed is added, capped at 100. A launcher never charges.
		/// </summary>
		public void StepCharge(float pfDeltaSeconds, float pfStepSeconds)
		{
			if (ChargeElapsed < 0f)
			{
				ChargeElapsed = 0f;

				return;
			}

			float lfStep = pfStepSeconds;

			if (lfStep <= 0f)
				lfStep = DefaultChargeStepSeconds;

			ChargeElapsed += pfDeltaSeconds;

			while (ChargeElapsed > lfStep)
			{
				PlayPow = Math.Min(UWCombat.FullCharge, PlayPow + ChargeSpeed);
				ChargeElapsed -= lfStep;
			}
		}

		/// <summary>
		/// The release keeps the charge, the kind and the ninth for the hit frame. Whether the
		/// release becomes a strike depends solely on the windup: if it has completed, the strike
		/// happens, otherwise it plays backwards and the weapon returns to ready (checked by user in
		/// the original, 2026-09-01). The minimum charge does NOT decide this - it is the lower
		/// bound of the damage factor.
		/// </summary>
		public void Release(bool pbWindupComplete)
		{
			PendingCharge = Charge;
			PendingKind = Kind;
			PendingNinth = Ninth;
			BlowPending = pbWindupComplete;
		}

		/// <summary>The hit frame has come: true once if a blow was waiting for it.</summary>
		public bool TakePendingBlow()
		{
			if (!BlowPending)
				return false;

			BlowPending = false;

			return true;
		}

		/// <summary>
		/// Forgets a charge and a strike still waiting for its hit frame - whenever the weapon goes
		/// away or comes out (Interaction.fResetAttackState).
		/// </summary>
		public void Reset()
		{
			ClearCharge();
			IsAttacking = false;
			BlowPending = false;
		}

		/// <summary>Sets the kind (Hack, Stab, anything else a Slash) and the middle column of the
		/// kind's row; the classic scheme sets the clicked column after (SetColumn).</summary>
		public void SetKind(AttackTypes peKind)
		{
			switch (peKind)
			{
				case AttackTypes.Hack: Kind = AttackTypes.Hack; break;
				case AttackTypes.Stab: Kind = AttackTypes.Stab; break;
				default: Kind = AttackTypes.Slash; break;
			}

			Ninth = (SwingRow * 3) + 2;
		}

		/// <summary>The clicked column, 0 to 2 from the left, in the kind's row.</summary>
		public void SetColumn(int piColumn)
		{
			Ninth = (SwingRow * 3) + piColumn + 1;
		}

		/// <summary>The row of the ninths for the current kind: 0 the stab (bottom), 1 the slash, 2
		/// the overhead bash (top) - swing_kind of COMBAT.C read backwards.</summary>
		public int SwingRow
		{
			get
			{
				switch (Kind)
				{
					case AttackTypes.Stab: return 0;
					case AttackTypes.Slash: return 1;
					default: return 2;
				}
			}
		}

		/// <summary>The kind by the third of the view the pointer is in, 0 the bottom: upper third
		/// overhead strike, lower third stab, in between the sideways slash (per user, 2026-08-30).</summary>
		public static AttackTypes KindOfThird(float pfFraction)
		{
			if (pfFraction >= UpperThirdFraction)
				return AttackTypes.Hack;

			if (pfFraction <= LowerThirdFraction)
				return AttackTypes.Stab;

			return AttackTypes.Slash;
		}

		/// <summary>The modern scheme's kind by the view's height (per user, 2026-10-03): looking up
		/// an overhead bash, down a thrust, straight a slash.</summary>
		public static AttackTypes KindOfPitch(float pfPitchUp)
		{
			if (pfPitchUp >= ModernSwingPitch)
				return AttackTypes.Hack;

			if (pfPitchUp <= -ModernSwingPitch)
				return AttackTypes.Stab;

			return AttackTypes.Slash;
		}

		/// <summary>The column by the third across the view, 0 the left.</summary>
		public static int ColumnOfThird(float pfAcross)
		{
			return pfAcross >= UpperThirdFraction ? 2 : pfAcross <= LowerThirdFraction ? 0 : 1;
		}

		/// <summary>The original table's kind of a blow: the overhead strike (and none chosen) is
		/// the bash attack of the original table.</summary>
		public static UWCombat.AttackKind CombatKindOf(AttackTypes peKind)
		{
			switch (peKind)
			{
				case AttackTypes.Slash:
					return UWCombat.AttackKind.Slash;

				case AttackTypes.Stab:
					return UWCombat.AttackKind.Stab;

				default:
					// Jab corresponds to the bash attack of the original table.
					return UWCombat.AttackKind.Bash;
			}
		}
	}
}
