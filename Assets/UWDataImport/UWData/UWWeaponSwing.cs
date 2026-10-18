using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What the weapon hand reads from the player's input (cleanup step 4.6, 2026-10-18): the combat
	/// mode, the held button, the charge and the aiming cursor - Interaction's values, read at the
	/// moment the swing needs them, as UWCharacter.Update read them until then.
	/// </summary>
	public interface IUWWeaponHand
	{
		bool IsCombatModeActive { get; }

		bool IsAttacking { get; }

		/// <summary>The charge from 0 to 1 - see Interaction.AttackCharge.</summary>
		float AttackCharge { get; }

		bool IsRangedTargeting { get; }
	}

	/// <summary>
	/// The weapon hand in the view window: drawing and sheathing, the wind-up, the blow and the
	/// retraction, the weapon switch in two motions, and which frame of the weapon picture and of
	/// the power gem is shown.
	///
	/// ENGINE-FREE SINCE CLEANUP STEP 4.6 (2026-10-18): the state machine lived in UWCharacter.Update
	/// until then. The host (UWWeaponView) hands in the elapsed frame time and the hand's input,
	/// turns Frame and GemPicture into textures and answers the events (music, noise, the hit
	/// check). The frame times (0.125 s per weapon frame, the draw in half a second) are
	/// presentation timing, as they were.
	/// </summary>
	public sealed class UWWeaponSwing
	{
		public enum AttackStatusEnum
		{
			Seathed = 0,
			Seathing = 1,
			Readying = 2,
			Ready = 3,
			PowerUp = 4,
			PoweUpComplete = 5,
			Release = 6,

			/// <summary>Released too early: the wind-up motion runs backwards again without a
			/// blow being struck.</summary>
			PowerDown = 7
		}

		private const int numPowerUpFrames = 4;
		private const int numReleaseFrames = 6;

		/// <summary>How many frames before the end the blow connects. See fGetConnectFrame.</summary>
		public const int DefaultFramesFromEnd = 2;

		/// <summary>Seconds per frame of the wind-up, the blow and the retraction.</summary>
		private const float FrameSeconds = .125f;

		/// <summary>The weapon whose pictures are shown (UWCharacter.CurrentWeaponType until 4.6).</summary>
		public WeaponTypes WeaponType { get; private set; }

		/// <summary>The swing of the next blow - set by Interaction every frame.</summary>
		public AttackTypes AttackType = AttackTypes.Slash;

		/// <summary>
		/// A ranged weapon is NOT held up into view.
		///
		/// In the original, with a bow or sling in hand you see nothing at all in the
		/// view window - no image, no wind-up motion (per user, 2026-09-10). Ours raised the
		/// axe instead, because a ranged weapon is missing from the melee table and the
		/// display therefore simply kept the previous weapon.
		///
		/// This is kept up to date continuously while combat mode is active, see
		/// Interaction.fUpdateWeaponType.
		/// </summary>
		public bool HasRangedWeapon { get; set; }

		private bool mIsCombatModeActive;

		public bool DrawWWeapon
		{
			get { return !HasRangedWeapon
				&& (mIsCombatModeActive || meAttackStatus == AttackStatusEnum.Seathing); }
		}

		private UWWeaponAnimations mOWeapons;
		private AttackStatusEnum meAttackStatus;

		public AttackStatusEnum State => meAttackStatus;

		/// <summary>The button was released; the state decides what comes of it.</summary>
		private bool mbReleaseRequested;

		/// <summary>
		/// Reports that the button was released.
		///
		/// During the wind-up this leads to a retraction from the current frame, afterwards to
		/// the blow. Whether the blow deals damage depends on the minimum charge and is decided
		/// in Interaction - the motion runs in any case.
		/// </summary>
		public void ReleaseAttack()
		{
			mbReleaseRequested = true;
		}

		/// <summary>Weapon to switch to after sheathing.</summary>
		private WeaponTypes mePendingWeapon;

		private bool mbHasPendingWeapon;

		/// <summary>UWSettings.AttackConnectFramesFromEnd, handed in by the host.</summary>
		public int ConnectFramesFromEnd = DefaultFramesFromEnd;

		/// <summary>
		/// The frame at which the blow connects. It is counted from the END of the animation, whose
		/// length is in the weapon data: an axe chop has four frames, a thrust six (the
		/// coordinate table ends with zero pairs, see UWWeaponAnimation.GetReleaseFrameCount).
		///
		/// A fixed number therefore does not work - for the axe chop, frame 5 is already past the
		/// end of the motion, the blow would connect too late.
		/// </summary>
		private int fGetConnectFrame()
		{
			int liFromEnd = ConnectFramesFromEnd;

			int liLength = mCurrentWeaponAnimation != null
				? mCurrentWeaponAnimation.GetReleaseFrameCount(AttackType)
				: numReleaseFrames;

			int liFrame = liLength - liFromEnd;

			return liFrame < 1 ? 1 : liFrame;
		}

		/// <summary>Reports the moment the blow connects. See the attack animation
		/// further below.</summary>
		public event Action AttackConnected;

		/// <summary>The weapon is drawn on entering combat mode (the music hears of it).</summary>
		public event Action CombatModeEntered;

		/// <summary>Combat mode was left, the weapon is being sheathed.</summary>
		public event Action CombatModeLeft;

		/// <summary>The blow is struck - it is loud (reference: combat_input.AttackTarget).</summary>
		public event Action Struck;

		/// <summary>How far the weapon is drawn, 0 to 1 - the picture slides up by it.</summary>
		public float CurrentReadyWeaponTime = 0f;

		private int mCurrentWeaponFrameIndex;
		private float mCurrentAttackAnimationDuration;
		private UWTexture mCurrentWeaponFrame;
		private UWWeaponAnimation mCurrentWeaponAnimation;
		private WeaponCoordinate mCurrentWeaponCoodinate;
		private UWPowerGem mPowerGem;
		private float mMaxAttackPowerAnimationTime;

		/// <summary>The weapon picture to show - see FrameVersion.</summary>
		public UWTexture Frame => mCurrentWeaponFrame;

		/// <summary>Counts every time the weapon picture was chosen anew, even the same one: the
		/// host converts it then (and only then, as UWCharacter.fUpdateWeapon did).</summary>
		public int FrameVersion { get; private set; }

		/// <summary>The power gem's picture; null until one was chosen.</summary>
		public UWTexture GemPicture { get; private set; }

		public WeaponCoordinate GetWeaponOffset()
		{
			return mCurrentWeaponCoodinate;
		}

		/// <summary>The weapon pictures and the gem from the data: the start weapon (the
		/// Inspector's on UWCharacter) sheathed, the gem inactive.</summary>
		public void Init(UWWeaponAnimations pOWeapons, UWPowerGem pOGem, WeaponTypes peStartWeapon)
		{
			WeaponType = peStartWeapon;
			mOWeapons = pOWeapons;
			mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(WeaponType);
			meAttackStatus = AttackStatusEnum.Seathed;
			fUpdateWeapon();

			mIsCombatModeActive = false;

			mPowerGem = pOGem;
			fUpdatePowerGemTeture(mPowerGem.Inactive);
		}

		/// <summary>
		/// Changes the weapon visible in the hand. The field alone is not enough - the frame
		/// sequence must be fetched again, otherwise the player keeps striking with the old one.
		/// </summary>
		public void SetWeaponType(WeaponTypes peWeapon, bool pbNothingVisible = false)
		{
			if (mOWeapons == null || peWeapon == (mbHasPendingWeapon ? mePendingWeapon : WeaponType))
				return;

			// Outside combat mode the weapon can be swapped silently - none is visible
			// anyway. THE SAME HOLDS WITH A RANGED WEAPON IN HAND: nothing is held up into view
			// then (HasRangedWeapon), so there is nothing to sheathe. Laying the bow down in
			// combat mode played the AXE'S sheathing motion, because the weapon type had kept
			// whatever was last in hand while the bow hid it (per user, 2026-09-20).
			if (!mIsCombatModeActive || pbNothingVisible)
			{
				WeaponType = peWeapon;
				mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(peWeapon);

				// NOTHING TO SHEATHE, BUT THE NEW WEAPON IS STILL DRAWN. Laying the bow down in
				// combat mode had the fist there at once; the original plays the readying motion
				// for it (per user, 2026-09-20). Outside combat mode nothing is visible either
				// way, so the motion only starts while it is on.
				if (pbNothingVisible && mIsCombatModeActive)
				{
					meAttackStatus = AttackStatusEnum.Readying;
					CurrentReadyWeaponTime = 0f;
					mCurrentWeaponFrameIndex = 0;
				}

				fUpdateWeapon();

				return;
			}

			// As long as the weapon has not been drawn at all, it is swapped silently. Otherwise
			// on the FIRST entry into combat mode the player would first sheathe the weapon he
			// did not even have in hand yet (per user, 2026-08-30).
			if (meAttackStatus != AttackStatusEnum.Ready && meAttackStatus != AttackStatusEnum.PowerUp
				&& meAttackStatus != AttackStatusEnum.PoweUpComplete && meAttackStatus != AttackStatusEnum.Release)
			{
				WeaponType = peWeapon;
				mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(peWeapon);

				fUpdateWeapon();

				return;
			}

			// In combat the switch takes two motions, as observed in the original
			// (user, 2026-08-30): first sheathe the old weapon, then draw the new one. No attack
			// is possible before the draw motion ends.
			mePendingWeapon = peWeapon;
			mbHasPendingWeapon = true;

			meAttackStatus = AttackStatusEnum.Seathing;
			CurrentReadyWeaponTime = 1f;
		}

		/// <summary>Whether a blow can be struck now. Not while sheathing or drawing.</summary>
		public bool CanAttack
		{
			get { return meAttackStatus == AttackStatusEnum.Ready; }
		}

		/// <summary>
		/// Whether the wind-up motion has run through. In the original, exactly this decides
		/// whether releasing becomes a blow: once the animation is through, the blow is struck -
		/// regardless of how far the charge counter got (per user, 2026-09-01). The
		/// minimum charge does not decide WHETHER a blow is struck, only how much
		/// damage comes out of it.
		/// </summary>
		public bool IsWindupComplete
		{
			get { return meAttackStatus == AttackStatusEnum.PoweUpComplete; }
		}

		/// <summary>Winding up a blow is loud (reference: CombatStages.Charging).</summary>
		public bool IsChargingAttack => meAttackStatus == AttackStatusEnum.PowerUp
			|| meAttackStatus == AttackStatusEnum.PoweUpComplete;

		/// <summary>
		/// One frame of the weapon hand: entering and leaving combat mode, drawing, sheathing and
		/// the weapon switch, the wind-up, the gem while charging, the blow and the retraction.
		/// Moved from UWCharacter.Update in cleanup step 4.6 (2026-10-18), in the same order.
		/// </summary>
		public void Tick(float pfDeltaSeconds, IUWWeaponHand pOHand)
		{
			if (!mIsCombatModeActive && pOHand.IsCombatModeActive) //Enter combat mode
			{
				mIsCombatModeActive = true;
				meAttackStatus = AttackStatusEnum.Readying;
				CurrentReadyWeaponTime = 0f;
				mCurrentWeaponFrameIndex = 0;
				fUpdateWeapon();
				fUpdatePowerGemTeture(mPowerGem.Unpowered);

				// Weapon drawn: the "Armed" theme, unless combat music plays (UWMusicSelector.OnWeaponDrawn).
				CombatModeEntered?.Invoke();
			}
			else if (mIsCombatModeActive && !pOHand.IsCombatModeActive) //Exit combat mode
			{
				mIsCombatModeActive = false;
				meAttackStatus = AttackStatusEnum.Seathing;
				CurrentReadyWeaponTime = 1f;
				fUpdatePowerGemTeture(mPowerGem.Inactive);

				CombatModeLeft?.Invoke();
			}
			else if (mIsCombatModeActive && meAttackStatus == AttackStatusEnum.Readying) //Readying weapon
			{
				CurrentReadyWeaponTime += pfDeltaSeconds * 2f;
				if (CurrentReadyWeaponTime >= 1f)
				{
					CurrentReadyWeaponTime = 1f;
					meAttackStatus = AttackStatusEnum.Ready;
				}
			}
			else if (meAttackStatus == AttackStatusEnum.Seathing) //Seathing weapon
			{
				CurrentReadyWeaponTime -= pfDeltaSeconds * 2f;
				if (CurrentReadyWeaponTime <= 0f)
				{
					CurrentReadyWeaponTime = 0f;

					// Weapon switch in combat: the old weapon is sheathed, now the new one
					// is drawn. Without a pending switch it stays sheathed.
					if (mbHasPendingWeapon)
					{
						mbHasPendingWeapon = false;

						WeaponType = mePendingWeapon;
						mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(mePendingWeapon);

						meAttackStatus = AttackStatusEnum.Readying;
						mCurrentWeaponFrameIndex = 0;
						fUpdateWeapon();
					}
					else
					{
						meAttackStatus = AttackStatusEnum.Seathed;
					}
				}
			}
			else if (mIsCombatModeActive && (meAttackStatus == AttackStatusEnum.Ready || meAttackStatus == AttackStatusEnum.PowerUp))
			{
				// The press starts the wind-up; the charge waits for it to finish (Interaction.
				// AttackCharge, since 2026-10-12 - until then the charge rising started the wind-up).
				// The gem shows frame 1 from the press (player_attack's set_screen_frame(3, 1)).
				if (pOHand.IsAttacking && meAttackStatus == AttackStatusEnum.Ready) //Starting attack
				{
					meAttackStatus = AttackStatusEnum.PowerUp;
					mCurrentWeaponFrameIndex = 0;
					mCurrentAttackAnimationDuration = 0f;
					fUpdateWeapon();
					fUpdatePowerGemTeture(mPowerGem.Unpowered);
				}
			}

			if (!mIsCombatModeActive) return;

			if (meAttackStatus == AttackStatusEnum.PowerUp)
			{
				// Released during the wind-up: the motion runs back from EXACTLY THIS frame,
				// not from the last one (per user, 2026-08-30).
				if (mbReleaseRequested)
				{
					mbReleaseRequested = false;

					meAttackStatus = AttackStatusEnum.PowerDown;
					mCurrentAttackAnimationDuration = 0f;
				}
				else if (mCurrentWeaponFrameIndex < numPowerUpFrames)
				{
					mCurrentAttackAnimationDuration += pfDeltaSeconds;

					if (mCurrentAttackAnimationDuration > FrameSeconds)
					{
						mCurrentWeaponFrameIndex++;
						mCurrentAttackAnimationDuration = 0f;
						fUpdateWeapon();
					}
				}
				else
				{
					meAttackStatus = AttackStatusEnum.PoweUpComplete;
					mCurrentAttackAnimationDuration = 0f;
					mCurrentWeaponFrameIndex = 0;
				}
			}
			else if(meAttackStatus == AttackStatusEnum.PoweUpComplete)
			{
				if (pOHand.AttackCharge < 1f)
				{
					// The orb depends solely on the charge counter, not on the minimum charge: the
					// original computes frame = 1 + charge/12 over the full range of 0 to 100.
					// Frame 1 is uncharged, 2 to 8 are the yellow levels, from 9 on green begins.
					//
					// Previously we squeezed the seven yellow frames into the range between
					// minimum charge and full. For the fist (minimum charge 90) that was
					// a tenth of the charge time - the orb practically jumped from red to green
					// (per user, 2026-09-01). In the original yellow is clearly visible, and with
					// this computation it already starts at charge 12.
					//
					// Read from the image data: frames 2 to 8 contain not a single
					// green pixel, their yellow ones grow from 11 to 59. A transition from
					// yellow to green does not occur in them - the change only happens on the
					// jump to the green frames.
					//
					// THE CHARGE STARTS AT 0 HERE, after the wind-up (by the user's decision of
					// 2026-10-12 to follow the original, see Interaction.AttackCharge): the gem
					// starts red at frame 1 and climbs from there. Until then it started mid-yellow,
					// because the charge counted from the press.
					int liCharge = GetChargeForGem(pOHand.AttackCharge);
					int liGemFrame = UWPowerGem.GetFrame(liCharge);

					// A RANGED WEAPON KNOWS NO YELLOW: in the original the sling jumps straight
					// from red to green, exactly when the targeting cursor appears (per user,
					// 2026-09-10). SINCE 2026-09-22 WE KNOW WHY, and it is not a computation at
					// all - the charge path is skipped for a ranged weapon and the gem is set to
					// frame 9 in one go, at the end of the wind-up (2026-10-12, C2). See
					// UWPowerGem.RangedFrame.
					if (HasRangedWeapon)
						liGemFrame = UWPowerGem.GetRangedFrame(pOHand.IsRangedTargeting);

					if (liGemFrame <= 1)
						fUpdatePowerGemTeture(mPowerGem.Unpowered);
					else if (liGemFrame - 2 < mPowerGem.Powering.Count)
						fUpdatePowerGemTeture(mPowerGem.Powering[liGemFrame - 2]);
					else
						fUpdatePowerGemTeture(mPowerGem.FullyPowered[0]);

					mMaxAttackPowerAnimationTime = 0f;
				}
				else
				{
					mMaxAttackPowerAnimationTime += pfDeltaSeconds * 5f;
					if (mMaxAttackPowerAnimationTime >= 5f)
						mMaxAttackPowerAnimationTime = 0f;

					fUpdatePowerGemTeture(mPowerGem.FullyPowered[(int)mMaxAttackPowerAnimationTime]);
				}

				// Once the wind-up motion has run through, a blow is ALWAYS struck - even if
				// the minimum charge has not been reached yet. The blow then just hits nothing
				// (per user, 2026-08-30: with some weapons the orb is still pale red at this
				// point). Whether it deals damage is decided by Interaction via the
				// minimum charge; the motion runs in any case.
				if (mbReleaseRequested)
				{
					mbReleaseRequested = false;

					meAttackStatus = AttackStatusEnum.Release;
					mCurrentWeaponFrameIndex = 0;
					mCurrentAttackAnimationDuration = 0f;

					// The blow is loud (reference: combat_input.AttackTarget).
					Struck?.Invoke();

					fUpdateWeapon();
				}
			}

			if (meAttackStatus == AttackStatusEnum.PowerDown)
			{
				// Backwards through the wind-up frames, then ready again. No blow, no
				// hit (per user, 2026-08-30: an attack released too early is not
				// carried out).
				mCurrentAttackAnimationDuration += pfDeltaSeconds;

				if (mCurrentAttackAnimationDuration > FrameSeconds)
				{
					mCurrentAttackAnimationDuration = 0f;

					if (mCurrentWeaponFrameIndex > 0)
					{
						mCurrentWeaponFrameIndex--;
						fUpdateWeapon();
					}
					else
					{
						meAttackStatus = AttackStatusEnum.Ready;
						fUpdateWeapon();
						fUpdatePowerGemTeture(mPowerGem.Unpowered);
					}
				}
			}

			if (meAttackStatus == AttackStatusEnum.Release)
			{
				if (mCurrentWeaponFrameIndex < numReleaseFrames)
				{
					mCurrentAttackAnimationDuration += pfDeltaSeconds;

					if (mCurrentAttackAnimationDuration > FrameSeconds)
					{
						mCurrentWeaponFrameIndex++;
						mCurrentAttackAnimationDuration = 0f;
						fUpdateWeapon();

						// The blow connects: the hit check hangs on THIS frame, not on
						// releasing the button. In the original you can start a blow out of
						// range and still hit if you are close enough at the right
						// moment (per user, 2026-08-30).
						int liConnectFrame = fGetConnectFrame();

						if (mCurrentWeaponFrameIndex == liConnectFrame && AttackConnected != null)
							AttackConnected();
					}
				}
				else
				{
					meAttackStatus = AttackStatusEnum.Ready;
					fUpdateWeapon();
					fUpdatePowerGemTeture(mPowerGem.Unpowered);
				}
			}
		}

		/// <summary>The charge as the gem counts it, 0 to UWCombat.FullCharge: the fraction
		/// clamped and rounded to the nearest whole (to even on a half, as Mathf.RoundToInt did).
		/// </summary>
		public static int GetChargeForGem(float pfCharge)
		{
			float lfClamped = pfCharge < 0f ? 0f : (pfCharge > 1f ? 1f : pfCharge);

			return (int)Math.Round(lfClamped * UWCombat.FullCharge);
		}

		private void fUpdateWeapon()
		{
			switch (meAttackStatus)
			{
				// PowerDown runs through the same frames as PowerUp, only backwards - without
				// this case it ended up in default and showed the ready frame, so the
				// retraction was not visible (per user, 2026-08-30).
				case AttackStatusEnum.PowerUp:
				case AttackStatusEnum.PowerDown:
					mCurrentWeaponFrame = mCurrentWeaponAnimation.GetPowerupFrame(AttackType, mCurrentWeaponFrameIndex);
					mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetPowerupCoord(AttackType, mCurrentWeaponFrameIndex);
					break;
				case AttackStatusEnum.PoweUpComplete:
					mCurrentWeaponFrame = mCurrentWeaponAnimation.GetPowerupFrame(AttackType, 3);
					mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetPowerupCoord(AttackType, 3);
					break;
				case AttackStatusEnum.Release:
					mCurrentWeaponFrame = mCurrentWeaponAnimation.GetReleaseFrame(AttackType, mCurrentWeaponFrameIndex);
					mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetReleaseCoord(AttackType, mCurrentWeaponFrameIndex);
					break;
				default:
					mCurrentWeaponFrame = mCurrentWeaponAnimation.GetReadyFrame();
					mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetReadyCoord();
					break;
			}

			FrameVersion++;
		}

		/// <summary>Chooses the gem's picture; null keeps the one shown (the host converts each
		/// picture once, see UWWeaponView).</summary>
		private void fUpdatePowerGemTeture(UWTexture pTexture)
		{
			if (pTexture == null)
				return;

			GemPicture = pTexture;
		}
	}
}
