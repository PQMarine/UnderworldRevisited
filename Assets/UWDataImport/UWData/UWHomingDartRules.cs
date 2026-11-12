using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE HOMING DART OF DEADLY SEEKER (UW2 plan G1 part C2, 2026-11-12; Underworld Exhumed uw2 critter/PATHFIND.C
	/// check_homing, store_targ, merge_angles, sys/MISCUTIL.C get_theta, combat/SPELLS.C process_area). After every step
	/// that keeps it in flight (move_me_joe) the dart looks for the nearest creature in a box ahead of it - the player
	/// counts, its shooter (last_hit) does not - and turns towards it, harder the closer it is, and pitches towards its
	/// height; with none it weaves at random. It trails a sparkle half the time and loses a hit point one step in four,
	/// which ends its flight; once it has struck a creature (the loner bit) it ends at once. Only the second game has
	/// the dart (item 0x1B); the first game's object there is none.
	/// </summary>
	public static class UWHomingDartRules
	{
		/// <summary>ITEM_HOMING_DART.</summary>
		public const int ItemId = 0x1B;

		/// <summary>put_effect's class 14: the sparkle behind the dart, three frames.</summary>
		public const int TrailEffectId = UWObjectClassProperties.AnimationFirstId + 14;

		public const int TrailFrames = 3;

		/// <summary>The map's size in tiles (process_area's clipping).</summary>
		private const int MapSize = 64;

		/// <summary>No target found (htarget -1, hdist 0x7FFF).</summary>
		private const int NoDistance = 0x7FFF;

		/// <summary>One steering step on a dart that is still in flight (move_me_joe's check_homing).</summary>
		public static void Steer(UWMobileRecord pORecord, IUWMotionWorld pIWorld, IUWMobileObjectHost pIHost)
		{
			if (pORecord == null || pIWorld == null)
				return;

			int liX8 = (pORecord.TileX << 3) + (pORecord.XPos & 7);
			int liY8 = (pORecord.TileY << 3) + (pORecord.YPos & 7);

			if (FindTarget(pORecord, pIWorld, liX8, liY8, out int liTargetX8, out int liTargetY8, out int liTargetZ, out int liDistance))
			{
				int liVector = GetTheta(liX8, liY8, liTargetX8, liTargetY8);
				int liStep = liDistance < 12 ? 2 : liDistance < 32 ? 4 : 6;

				pORecord.HeadingByte = MergeAngles(pORecord.HeadingByte & 0xFF, (liVector >> 8) & 0xFF, (liStep + 8) << 2);

				int liPitch = pORecord.VzField & 0x1F;

				// unsigned in the original: a target below gives a huge difference, divided as unsigned.
				int liHeightDiff = (liTargetZ - pORecord.ZPos) & 0xFFFF;
				int liDesired = (liHeightDiff / 24) + 16;
				int liNew = ((liPitch * (liStep + 4)) + (liDesired * (8 - liStep))) / 12;

				pORecord.VzField = liNew & 0x1F;
			}
			else
			{
				pORecord.HeadingByte = (pORecord.HeadingByte + (pIWorld.Random() & 7) - 3) & 0xFF;

				int liPitch = pORecord.VzField & 0x1F;

				if ((pIWorld.Random() & 4) == 0)
				{
					if (liPitch < 14)
						liPitch++;
					else if (liPitch > 18)
						liPitch--;
					else
						liPitch = liPitch + (pIWorld.Random() & 3) - 1;
				}

				pORecord.VzField = liPitch & 0x1F;
			}

			if ((pIWorld.Random() & 2) == 0 && pIHost != null)
				pIHost.ShowEffectAtObject(TrailEffectId, pORecord, TrailFrames);

			if (pORecord.Loner)
				pORecord.Hp = 0;
			else if ((pIWorld.Random() & 3) == 0 && pORecord.Hp != 0)
				pORecord.Hp--;
		}

		/// <summary>
		/// check_homing's box and process_area(1, last_hit, store_targ, 0, ...): three tiles by three around the dart's
		/// tile, one more ahead along its octant's quarter; process_area walks x0..x0+w and y0..y0+h both inclusive
		/// (so four by five in fact), clipped to the map; of the creatures there but the shooter the nearest by fine
		/// Manhattan distance, the first found on a tie.
		/// </summary>
		public static bool FindTarget(UWMobileRecord pORecord, IUWMotionWorld pIWorld, int piX8, int piY8,
			out int piTargetX8, out int piTargetY8, out int piTargetZ, out int piDistance)
		{
			piTargetX8 = 0;
			piTargetY8 = 0;
			piTargetZ = 0;
			piDistance = NoDistance;

			bool lbFound = false;

			int liX0 = pORecord.TileX - 1;
			int liY0 = pORecord.TileY - 1;
			int liWidth = 3;
			int liHeight = 3;
			int liCardinal = ((pORecord.Octant + 1) & 7) >> 1;

			if (liCardinal > 1)
			{
				if (liCardinal == 3)
					liX0--;
				else
					liY0--;
			}

			if ((liCardinal & 1) != 0)
				liWidth++;
			else
				liHeight++;

			if (liX0 >= MapSize || liX0 + liWidth < 0 || liY0 >= MapSize || liY0 + liHeight < 0)
				return false;

			if (liX0 < 0)
			{
				liWidth -= -liX0;
				liX0 = 0;
			}
			else if (liX0 + liWidth >= MapSize)
				liWidth -= liX0 + liWidth - MapSize;

			if (liY0 < 0)
			{
				liHeight -= -liY0;
				liY0 = 0;
			}
			else if (liY0 + liHeight > MapSize)
				liHeight -= liY0 + liHeight - MapSize;

			if (liWidth <= 0 || liHeight <= 0)
				return false;

			List<UWMotionBody> lOBodies = new List<UWMotionBody>();

			for (int liX = liX0; liX <= liX0 + liWidth; liX++)
			{
				for (int liY = liY0; liY <= liY0 + liHeight; liY++)
				{
					if (liX < 0 || liX >= MapSize || liY < 0 || liY >= MapSize)
						continue;

					lOBodies.Clear();
					pIWorld.GetBodiesInTile(liX, liY, lOBodies);

					foreach (UWMotionBody lOBody in lOBodies)
					{
						if (!lOBody.IsCreature || lOBody.Index == pORecord.Owner || lOBody.Index == pORecord.Index)
							continue;

						int liBodyX8 = (liX << 3) + (lOBody.XPos & 7);
						int liBodyY8 = (liY << 3) + (lOBody.YPos & 7);
						int liDistance = System.Math.Abs(liBodyX8 - piX8) + System.Math.Abs(liBodyY8 - piY8);

						if (liDistance < piDistance)
						{
							piTargetX8 = liBodyX8;
							piTargetY8 = liBodyY8;
							piTargetZ = lOBody.ZPos;
							piDistance = liDistance;
							lbFound = true;
						}
					}
				}
			}

			return lbFound;
		}

		/// <summary>get_theta: the angle from (piSx, piSy) to (piX, piY), 0 to 0xFFFF, 0 along +y and 0x4000 along +x: the
		/// vector scaled to length 0x7FFF by the original's root (cSqRt), then cAtan2 of it; 0 for no vector.</summary>
		public static int GetTheta(int piSx, int piSy, int piX, int piY)
		{
			int liDx = piX - piSx;
			int liDy = piY - piSy;
			int liDist = UWVanillaMath.Sqrt((liDx * liDx) + (liDy * liDy));

			if (liDist == 0)
				return 0;

			int liVx = (int)((long)liDx * 0x7FFF / liDist);
			int liVy = (int)((long)liDy * 0x7FFF / liDist);

			return UWVanillaMath.Atan2(liVx, liVy) & 0xFFFF;
		}

		/// <summary>merge_angles: piSrc turned piMagnitude / 64 of the way towards piDst (0 to 255), the short way round.</summary>
		public static int MergeAngles(int piSrc, int piDst, int piMagnitude)
		{
			if (piDst - piSrc > 128)
			{
				piSrc += 256;
				piMagnitude = 64 - piMagnitude;

				int liTmp = piSrc;

				piSrc = piDst;
				piDst = liTmp;
			}
			else if (piDst - piSrc < -128)
				piDst += 256;

			return 0xFF & ((piMagnitude * (piDst - piSrc) / 64) + piSrc);
		}
	}
}
