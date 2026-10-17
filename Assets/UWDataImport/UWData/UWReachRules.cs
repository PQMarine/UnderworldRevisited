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

		/// <summary>A close look: 0x48 = 72, eight eighths, about one tile. player_3dlook
		/// (UnderworldExhumed ui/INTERACT.C) looks with detail 1 only at a thing that cannot be
		/// taken and lies within this reach - a window then shows its picture (RectLook); from
		/// farther away only its words (2026-10-15, reference audit round 2, A46).</summary>
		public const int LookDetailDistanceSquared = 0x48;

		/// <summary>Telekinesis: the original passes zero, and its check reads that as "always
		/// reachable" - no distance at all, not a larger one.</summary>
		public const int UnlimitedDistance = 0;

		/// <summary>How far above the feet a thing may be: 24 zpos steps, three floor height
		/// levels.</summary>
		public const int ReachAbove = 24;

		/// <summary>... and how far below: 0xC = 12 zpos steps, one and a half levels. One reaches
		/// higher up than down.</summary>
		public const int ReachBelow = 0xC;

		/// <summary>String block 1: "You are unable to use that from here." - the plain use's
		/// refusal (player_3duse, UnderworldExhumed INTERACT.C).</summary>
		public const int UnableToUseMessage = 186;

		/// <summary>String block 1: "You cannot reach that." - the target mode's refusal
		/// (mous_in_3d, game_sprint 0x5E): an item used on something (UseThing) and a target spell
		/// alike.</summary>
		public const int CannotReachMessage = 95;

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
		/// Is the thing within reach (InPickRange, UnderworldExhumed ui/INTERACT.C)? Positions in
		/// eighths of a tile, heights in zpos steps (0 to 127); the player's height is that of
		/// the feet. A SWIMMER REACHES AS ANYONE: UW1's InPickRange has no swimming term (the file
		/// lists "no swimming reach" among UW1's differences from UW2); until 2026-10-15 ours
		/// lowered the window by the swim counter / 8 (reference audit round 2, A52).
		/// The original's second test, the terrain between, is IsBlockedByTerrain.
		/// </summary>
		public static bool CanReach(int piPlayerX, int piPlayerY, int piPlayerZ,
			int piTargetX, int piTargetY, int piTargetZ,
			int piDistanceSquared, bool pbUsingPole)
		{
			if (piDistanceSquared == UnlimitedDistance)
				return true;

			int liDx = piTargetX - piPlayerX;
			int liDy = piTargetY - piPlayerY;

			if ((liDx * liDx) + (liDy * liDy) > piDistanceSquared)
				return false;

			int liHeight = piPlayerZ - piTargetZ;

			// The pole doubles the window, as it doubles the distance.
			int liFactor = pbUsingPole ? 2 : 1;

			return liHeight <= liFactor * ReachBelow && liHeight >= -(liFactor * ReachAbove);
		}

		/// <summary>
		/// THE TERRAIN BETWEEN (BlockingTerrain, UnderworldExhumed ui/INTERACT.C), the second test
		/// next to CanReach for use, take and the target mode. Only for a thing off the player's
		/// own tile: its height must lie in [pz, pz + 32] with pz the feet less 8 (doubled with
		/// the pole); then the tiles from the player's tile to the thing's are walked, a step on
		/// each axis at a time, the thing's tile included - a tile whose floor lies two or more
		/// steps below pz blocks (bare hand only), and a tile between of another terrain class
		/// than the player's (water, lava) blocks unless a bridge at a height between carries the
		/// line (not with the pole). Per user on the original 2026-10-17 (SAVE4 level 1, an apple
		/// across the one-tile water channel at 47/5): "You cannot reach that." from the edge of
		/// 46/5, and taken one step closer - once the player's centre lies over the channel's
		/// tile, nothing lies between any more. Until then ours had no such test.
		/// The player's tile is his centre's; heights in zpos steps; the delegates read the
		/// level: floor height nibble, terrain class (0 normal, 1 water, 2 lava), the highest
		/// bridge's z or -1.
		/// </summary>
		public static bool IsBlockedByTerrain(int piPlayerTileX, int piPlayerTileY, int piPlayerZ,
			int piTargetTileX, int piTargetTileY, int piTargetZ, int piDistanceSquared, bool pbUsingPole,
			System.Func<int, int, int> pFFloorHeight, System.Func<int, int, int> pFTerrain,
			System.Func<int, int, int> pFBridgeZ)
		{
			if (piDistanceSquared == UnlimitedDistance
				|| (piTargetTileX == piPlayerTileX && piTargetTileY == piPlayerTileY))
				return false;

			int liPlayerTerrain = pFTerrain(piPlayerTileX, piPlayerTileY);
			int liStepX = System.Math.Sign(piTargetTileX - piPlayerTileX);
			int liStepY = System.Math.Sign(piTargetTileY - piPlayerTileY);
			int liFactor = pbUsingPole ? 2 : 1;
			int liReach = liFactor << 3;
			int liHigh = liFactor << 5;
			int liPz = piPlayerZ > liReach ? piPlayerZ - liReach : 0;

			if (piTargetZ < liPz || liPz + liHigh < piTargetZ)
				return true;

			int liX = piPlayerTileX;
			int liY = piPlayerTileY;

			for (;;)
			{
				liX += liStepX;

				if ((liStepX < 0 && liX < piTargetTileX) || (liStepX > 0 && liX > piTargetTileX))
					liX = piTargetTileX;

				liY += liStepY;

				if ((liStepY < 0 && liY < piTargetTileY) || (liStepY > 0 && liY > piTargetTileY))
					liY = piTargetTileY;

				int liBridge = pFBridgeZ(liX, liY);
				bool lbCarried = liBridge >= 0 && liBridge <= piTargetZ && liBridge >= liPz;

				if (!lbCarried && (liPz >> 3) > pFFloorHeight(liX, liY) && piDistanceSquared == UseDistanceSquared)
					return true;

				if (liX == piTargetTileX && liY == piTargetTileY)
					return false;

				int liTerrain = pFTerrain(liX, liY);

				if (piDistanceSquared <= UseDistanceSquared && !lbCarried && liTerrain != 0 && liTerrain != liPlayerTerrain)
					return true;
			}
		}
	}
}
