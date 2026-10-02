namespace UWDataImport.UWData
{
	/// <summary>
	/// How far the hand reaches. The reach check of the original (in the reference
	/// uimanager_interaction.CanReach), rebuilt on 2026-09-18 after the user measured it in the
	/// original down to the eighth of a tile.
	///
	/// EVERYTHING COUNTS ON THE TILE GRID, not in world coordinates: both positions become
	/// eighths of a tile (tile times eight plus the sub-tile spot), the horizontal distance is
	/// compared squared against a fixed number, and the height is a window of its own. Until then
	/// we measured the distance of the ray from the eye to the collider, which is neither the same
	/// quantisation nor the same shape.
	///
	/// THERE IS NO LINE OF SIGHT IN IT. Through the bars of a closed portcullis one reaches the
	/// switch behind it - that is the point of the pole puzzle on level 1. What keeps you from a
	/// thing is where you can stand, not what stands in between.
	///
	/// MEASURED (per user in the original, save 4, level 2): standing on tile 36/16 at fine
	/// position 128, so sub-tile 4, the sign at 37/16 sub-tile 7 can be used - eleven eighths
	/// away, 11 * 11 + 1 * 1 = 122. One step back the fine position falls to 127 and with it the
	/// sub-tile to 3, twelve eighths, 144 + 1 = 145, and the original answers "You are unable to
	/// use that from here.".
	///
	/// LOOKING has no check at all (the reference calls look.LookAt without CanReach) - only the
	/// light limits what can be seen.
	/// </summary>
	public static class UWReachRules
	{
		/// <summary>The squared distance in eighths of a tile the bare hand reaches: 0x90 = 144,
		/// so twelve eighths or one and a half tiles.</summary>
		public const int UseDistanceSquared = 0x90;

		/// <summary>With a pole in hand: 0x190 = 400, twenty eighths or two and a half tiles.</summary>
		public const int PoleDistanceSquared = 0x190;

		/// <summary>Telekinesis: the original passes zero, and its check reads that as "always
		/// reachable" - no distance at all, not a larger one.</summary>
		public const int UnlimitedDistance = 0;

		/// <summary>How far above the feet a thing may be: 24 zpos steps, three floor height
		/// levels.</summary>
		public const int ReachAbove = 24;

		/// <summary>... and how far below: 0xC = 12 zpos steps, one and a half levels. One reaches
		/// higher up than down.</summary>
		public const int ReachBelow = 0xC;

		/// <summary>From this swim counter on one hangs deeper in the water than the feet position
		/// says, and the height window moves with it (see UWPlayerTerrain.SwimCounter).</summary>
		public const int SwimCounterThreshold = 0x50;

		/// <summary>String block 1: "You are unable to use that from here."</summary>
		public const int UnableToUseMessage = 186;

		/// <summary>String block 1: "That is too far away to take." - the same reach, its own
		/// sentence (per user on the original, 2026-09-18).</summary>
		public const int TooFarToTakeMessage = 94;

		/// <summary>Taking something reaches exactly as far as the bare hand; the pole does not
		/// help, only Telekinesis lifts the limit (the reference keeps PickupDistance next to
		/// UseDistance with the same 0x90).</summary>
		public static int GetPickupDistanceSquared(bool pbHasTelekinesis)
		{
			return pbHasTelekinesis ? UnlimitedDistance : UseDistanceSquared;
		}

		/// <summary>A position in eighths of a tile, the unit this check counts in.</summary>
		public static int ToEighths(int piTile, int piSubPosition)
		{
			return (piTile << 3) + piSubPosition;
		}

		/// <summary>The squared distance that applies right now. The pole beats Telekinesis, as in
		/// the original, which asks for the pole first.</summary>
		public static int GetDistanceSquared(bool pbUsingPole, bool pbHasTelekinesis)
		{
			if (pbUsingPole)
				return PoleDistanceSquared;

			return pbHasTelekinesis ? UnlimitedDistance : UseDistanceSquared;
		}

		/// <summary>
		/// Is the thing within reach? Positions in eighths of a tile, heights in zpos steps
		/// (0 to 127); the player's height is that of the feet.
		/// </summary>
		public static bool CanReach(int piPlayerX, int piPlayerY, int piPlayerZ,
			int piTargetX, int piTargetY, int piTargetZ,
			int piDistanceSquared, bool pbUsingPole, int piSwimCounter)
		{
			if (piDistanceSquared == UnlimitedDistance)
				return true;

			int liDx = piTargetX - piPlayerX;
			int liDy = piTargetY - piPlayerY;

			if ((liDx * liDx) + (liDy * liDy) > piDistanceSquared)
				return false;

			int liHeight = piPlayerZ - piTargetZ;

			if (piSwimCounter > SwimCounterThreshold)
				liHeight -= piSwimCounter >> 3;

			// The pole doubles the window, as it doubles the distance.
			int liFactor = pbUsingPole ? 2 : 1;

			return liHeight <= liFactor * ReachBelow && liHeight >= -(liFactor * ReachAbove);
		}
	}
}
