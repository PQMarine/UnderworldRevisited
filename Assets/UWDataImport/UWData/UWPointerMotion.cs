namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GAME'S OWN POINTER, ENGINE-FREE (cleanup step 7.7B, 2026-10-25; Docs/CLEANUP-PLAN.md 8.5,
	/// the goal decided 2026-10-23): in the running game the system pointer is locked and hidden and
	/// the pointer is ours - kept from the mouse's counts as the DOS mouse driver kept it, drawn by us
	/// (UWPointer). This is its arithmetic, in screen pixels with y up:
	///
	///   - CAPTURED, it moves by the mouse's counts times the speed (UWUserData.PointerSpeed: 1 is one
	///     count per screen pixel, what Windows gives at its default speed without acceleration; no
	///     acceleration here either), held inside the window (0 to width - 1, as the pad's pointer was);
	///     the pad, the slot of the modern bags and the classic walk's clamp put it somewhere (Place,
	///     Clamp).
	///   - THE CAPTURE BEGINS where the system pointer stands (Capture).
	///   - THE CAPTURE ENDS with the system pointer put once where ours was (Release with a warp). Until
	///     the system reports it there, ours stays; where the platform refuses the warp (Wayland,
	///     XWayland) the system's position wins after WarpFrames frames (FollowSystem). A capture lost
	///     with the focus warps nothing: the pointer is the system's at once.
	///
	/// Nothing here reads the engine; the caller passes the window's size, the counts and the
	/// system's position.
	/// </summary>
	public sealed class UWPointerMotion
	{
		/// <summary>The system pointer counts as arrived where it was warped within this many pixels.</summary>
		public const float ArrivedWithin = 2f;

		/// <summary>Frames the own position waits for the warped system pointer before the system's
		/// position is taken (the warp refused).</summary>
		public const int WarpFrames = 5;

		/// <summary>The position, screen pixels from the left.</summary>
		public float X { get; private set; }

		/// <summary>The position, screen pixels from the bottom.</summary>
		public float Y { get; private set; }

		/// <summary>The system pointer is locked and hidden; this one is the pointer.</summary>
		public bool IsCaptured { get; private set; }

		private int miWarpFramesLeft;

		/// <summary>The capture has ended with a warp and the system pointer has not reported it yet.</summary>
		public bool IsWarpPending => miWarpFramesLeft > 0;

		/// <summary>The capture begins: the pointer starts where the system's stands.</summary>
		public void Capture(float pfSystemX, float pfSystemY, float pfWidth, float pfHeight)
		{
			IsCaptured = true;
			miWarpFramesLeft = 0;
			fSet(pfSystemX, pfSystemY, pfWidth, pfHeight);
		}

		/// <summary>The capture ends. pbWarp: the caller puts the system pointer at X, Y, and the
		/// position stays until it arrives; false (the focus lost) takes the system's at once.</summary>
		public void Release(bool pbWarp)
		{
			IsCaptured = false;
			miWarpFramesLeft = pbWarp ? WarpFrames : 0;
		}

		/// <summary>Once a frame while free: the system pointer's position - taken, unless a warp is on
		/// its way and this is not yet where it went.</summary>
		public void FollowSystem(float pfSystemX, float pfSystemY)
		{
			if (miWarpFramesLeft > 0)
			{
				if (fNear(pfSystemX, X) && fNear(pfSystemY, Y))
					miWarpFramesLeft = 0;
				else if (--miWarpFramesLeft > 0)
					return;
			}

			X = pfSystemX;
			Y = pfSystemY;
		}

		/// <summary>Once a frame while captured: the mouse's counts times the speed, inside the window.</summary>
		public void Move(float pfCountsX, float pfCountsY, float pfSpeed, float pfWidth, float pfHeight)
		{
			fSet(X + (pfCountsX * pfSpeed), Y + (pfCountsY * pfSpeed), pfWidth, pfHeight);
		}

		/// <summary>Put somewhere - by the pad, onto a slot, into the middle - inside the window.</summary>
		public void Place(float pfX, float pfY, float pfWidth, float pfHeight)
		{
			fSet(pfX, pfY, pfWidth, pfHeight);
		}

		/// <summary>Held inside a rectangle, its borders included (the classic walk's view window);
		/// whether it had to move.</summary>
		public bool Clamp(float pfMinX, float pfMinY, float pfMaxX, float pfMaxY)
		{
			float lfX = fClamp(X, pfMinX, pfMaxX);
			float lfY = fClamp(Y, pfMinY, pfMaxY);
			bool lbMoved = lfX != X || lfY != Y;

			X = lfX;
			Y = lfY;

			return lbMoved;
		}

		private void fSet(float pfX, float pfY, float pfWidth, float pfHeight)
		{
			X = fClamp(pfX, 0f, pfWidth - 1f);
			Y = fClamp(pfY, 0f, pfHeight - 1f);
		}

		private static bool fNear(float pfA, float pfB)
		{
			float lfDifference = pfA - pfB;

			return lfDifference <= ArrivedWithin && lfDifference >= -ArrivedWithin;
		}

		/// <summary>Within the bounds; a range turned over (a window narrower than one pixel) gives the
		/// lower bound.</summary>
		private static float fClamp(float pfValue, float pfMin, float pfMax)
		{
			if (pfValue > pfMax)
				pfValue = pfMax;

			return pfValue < pfMin ? pfMin : pfValue;
		}
	}

	/// <summary>
	/// THE CLICK THAT GIVES THE FOCUS BACK does not click in the game (cleanup step 7.7B, 2026-10-25,
	/// per user): after the focus comes back - the capture is taken up again, the pointer where it was -
	/// a mouse button that goes down at once is the click on the window, not one for the game. Its
	/// buttons are taken out of the mouse's events (UWPointer) until an event with every button up
	/// comes; so neither its press nor its release reaches anyone, the actions included.
	///
	/// "At once": from the focus coming back to WindowSeconds after the first frame that followed it
	/// (Tick) - the first frame after an Alt+Tab into fullscreen can take long, and the click's events
	/// arrive in it. Time is the caller's clock in seconds.
	/// </summary>
	public sealed class UWFocusClick
	{
		/// <summary>How long after the first frame back a pressed button still counts as the focus's click.</summary>
		public const double WindowSeconds = 0.25;

		private bool mbArmed;

		private double mdOpenUntil = double.MinValue;

		private bool mbEating;

		/// <summary>The focus came back.</summary>
		public void FocusGained()
		{
			mbArmed = true;
			mbEating = false;
		}

		/// <summary>The focus went: nothing pending.</summary>
		public void FocusLost()
		{
			mbArmed = false;
			mbEating = false;
			mdOpenUntil = double.MinValue;
		}

		/// <summary>Once a frame: the first frame after the focus came back opens the window, a later one
		/// past its end closes it.</summary>
		public void Tick(double pdNow)
		{
			if (mbArmed)
			{
				mbArmed = false;
				mdOpenUntil = pdNow + WindowSeconds;
			}
			else if (!mbEating && mdOpenUntil != double.MinValue && pdNow > mdOpenUntil)
			{
				mdOpenUntil = double.MinValue;
			}
		}

		/// <summary>A button of the focus's click is being taken out.</summary>
		public bool IsEating => mbEating;

		/// <summary>Whether the mouse's events need to be looked at at all (the focus came back lately).</summary>
		public bool IsWatching => mbArmed || mbEating || mdOpenUntil != double.MinValue;

		/// <summary>A mouse event with a button down or not (pbAnyDown) at pdNow: whether its buttons
		/// are taken out.</summary>
		public bool Swallows(bool pbAnyDown, double pdNow)
		{
			if (mbEating)
			{
				if (pbAnyDown)
					return true;

				// Every button up: the click is over, and so is the window.
				mbEating = false;
				mbArmed = false;
				mdOpenUntil = double.MinValue;

				return false;
			}

			if (pbAnyDown && (mbArmed || pdNow <= mdOpenUntil))
			{
				mbEating = true;
				return true;
			}

			return false;
		}
	}
}
