using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE PLAYER ARRIVES (reference audit 2026-10-14, A30, built 2026-10-15). Every move of
	/// the player to a tile - a level change by stairs or trap, a teleport on the same level, Gate
	/// Travel, the silver tree, the void - ends in new_player_pos (game/UWEDIT.C:450-484 of
	/// UnderworldExhumed), which does not simply put him on the tile: find_good_x_and_y
	/// (event/WORLDEV.C:76-167) searches outward from it for a tile where the adventurer fits
	/// (can_place), and only if none is found a second pass searches again, CLEARING every tile it
	/// tries of all objects with a height and all mobiles; if that finds nothing either the player
	/// dies (hp 0). A same-level teleport by a trap asks the first search already before it moves
	/// anyone (do_teleport, WORLDEV.C:175-200) and does nothing when it fails. Until 2026-10-15 ours
	/// put the player on the requested tile, into a creature standing there.
	///
	/// THE SEARCH: breadth first, ring by ring, from the target. A ring holds at most twenty tiles
	/// (more are dropped, not kept for later). The box is the target less four, clamped to 1 and
	/// 0x3A, to that plus nine, clamped to 0x3E - so ten tiles wide, four to the low side and five
	/// to the high. An open (or sloped) tile passes on to its four neighbours in the order -x, -y,
	/// +x, +y; a diagonal only to its two open sides; a wall to none. Each tile is asked as the
	/// tiles come: is there room for the adventurer at fine 3/3 of the tile, at its floor height
	/// (plus half a step on a slope), not as a flier, with a step allowance of 8.
	///
	/// TWO SLIPS OF THE ORIGINAL ARE NOT COPIED: it clears only nine bytes of its eighteen-byte
	/// visited array (the upper rows of the box start with whatever the stack held) and its tenth
	/// column writes one past the array. Ours starts with nothing visited and keeps all ten columns.
	/// </summary>
	public static class UWArrivalRules
	{
		/// <summary>The box starts this many tiles below the target...</summary>
		public const int BoxReachBelow = 4;

		/// <summary>...and reaches this many beyond its start.</summary>
		public const int BoxSpan = 9;

		/// <summary>The box's start is clamped to 1..0x3A, its end to 0x3E.</summary>
		public const int BoxLowest = 1;

		public const int BoxHighestStart = 0x3A;

		public const int BoxHighestEnd = 0x3E;

		/// <summary>A ring of the search holds at most this many tiles.</summary>
		public const int RingLimit = 20;

		/// <summary>can_place is asked at the tile's eighth 3 on both axes.</summary>
		public const int TestFineEighth = 3;

		/// <summary>A sloped tile (tile_walls bit 0x20) is asked half a step above its floor.</summary>
		public const int SlopeLift = 4;

		/// <summary>can_place's step allowance here.</summary>
		public const int FitStep = 8;

		/// <summary>The special coordinate do_teleport skips the search for (Gate Travel and the
		/// silver tree pass it and set the real tile later).</summary>
		public const int NoSearchCoordinate = 0x3F;

		/// <summary>
		/// find_good_x_and_y: the first tile from (piX, piY) outward where pfFits says the
		/// adventurer fits. pfType gives a tile's type (solid outside the map), pfClear - the second
		/// pass only - empties a tile before it is asked. False when no tile in the box fits.
		/// </summary>
		public static bool FindGoodXAndY(int piX, int piY, Func<int, int, UWTile.TileTypeEnum> pfType,
			Func<int, int, bool> pfFits, Action<int, int> pfClear, out int piFoundX, out int piFoundY)
		{
			piFoundX = piX;
			piFoundY = piY;

			if (pfType == null || pfFits == null)
				return false;

			int liXMin = Math.Min(Math.Max(BoxLowest, piX - BoxReachBelow), BoxHighestStart);
			int liYMin = Math.Min(Math.Max(BoxLowest, piY - BoxReachBelow), BoxHighestStart);
			int liXMax = Math.Min(liXMin + BoxSpan, BoxHighestEnd);
			int liYMax = Math.Min(liYMin + BoxSpan, BoxHighestEnd);

			HashSet<int> lOVisited = new HashSet<int> { fKey(piX, piY) };
			List<int> lOCurrent = new List<int> { fKey(piX, piY) };

			while (lOCurrent.Count > 0)
			{
				List<int> lONext = new List<int>();

				foreach (int liKey in lOCurrent)
				{
					int liX = liKey >> 8;
					int liY = liKey & 0xFF;

					if (pfClear != null)
						pfClear(liX, liY);

					if (pfFits(liX, liY))
					{
						piFoundX = liX;
						piFoundY = liY;

						return true;
					}

					switch (pfType(liX, liY))
					{
						case UWTile.TileTypeEnum.solid:
							break;

						case UWTile.TileTypeEnum.diagonal_se:
							fVisit(liX + 1, liY, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							fVisit(liX, liY - 1, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							break;

						case UWTile.TileTypeEnum.diagonal_sw:
							fVisit(liX - 1, liY, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							fVisit(liX, liY - 1, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							break;

						case UWTile.TileTypeEnum.diagonal_ne:
							fVisit(liX + 1, liY, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							fVisit(liX, liY + 1, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							break;

						case UWTile.TileTypeEnum.diagonal_nw:
							fVisit(liX - 1, liY, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							fVisit(liX, liY + 1, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							break;

						default:
							fVisit(liX - 1, liY, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							fVisit(liX, liY - 1, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							fVisit(liX + 1, liY, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							fVisit(liX, liY + 1, lONext, lOVisited, liXMin, liXMax, liYMin, liYMax);
							break;
					}
				}

				lOCurrent = lONext;
			}

			return false;
		}

		/// <summary>The z can_place is asked at: the tile's floor (its four-bit height times
		/// eight) plus half a step on a slope.</summary>
		public static int GetTestZ(UWTile pOTile)
		{
			if (pOTile == null)
				return 0;

			int liZ = (pOTile.FloorHeight >> 4) << 3;

			return pOTile.TileType >= UWTile.TileTypeEnum.slope_n ? liZ + SlopeLift : liZ;
		}

		/// <summary>
		/// The fit itself: can_place for the adventurer (0x7F, object 1, so the player's own body
		/// is not in its way) at fine 3/3 of the tile, at GetTestZ, not as a flier, step 8 -
		/// against the solid bodies pOCore's world reports.
		/// </summary>
		public static bool AdventurerFits(UWMotionCore pOCore, UWTile pOTile, int piTileX, int piTileY)
		{
			if (pOCore == null || pOTile == null || pOTile.TileType == UWTile.TileTypeEnum.solid)
				return false;

			int liZ = GetTestZ(pOTile);

			pOCore.Mover = new UWMotionBody
			{
				Index = 1,
				ItemId = UWObjectMechanics.AdventurerObjectId,
				XPos = TestFineEighth,
				YPos = TestFineEighth,
				ZPos = liZ,
				IsMobile = true,
				IsCreature = true
			};

			return pOCore.CheckIfItemFitsInTile(UWObjectMechanics.AdventurerObjectId, 1,
				(piTileX << 3) + TestFineEighth, (piTileY << 3) + TestFineEighth, liZ, false, FitStep,
				out int _, out int _);
		}

		/// <summary>Whether the second pass deletes an object from a tile it tries: anything with
		/// a height in COMOBJ, and every mobile (find_good_x_and_y's clear branch).</summary>
		public static bool IsClearedForArrival(UWObject pOObject, UWCommonObjectProperties pOProperties)
		{
			if (pOObject == null)
				return false;

			if (pOObject is UWNpc)
				return true;

			UWCommonObjectProperties.Entry lOEntry;

			return pOProperties != null && pOProperties.TryGet(pOObject.ID, out lOEntry) && lOEntry.Height != 0;
		}

		private static int fKey(int piX, int piY)
		{
			return (piX << 8) | (piY & 0xFF);
		}

		/// <summary>VISIT: a tile not yet seen, inside the box, while the ring has room.</summary>
		private static void fVisit(int piX, int piY, List<int> pONext, HashSet<int> pOVisited,
			int piXMin, int piXMax, int piYMin, int piYMax)
		{
			if (pONext.Count >= RingLimit || piX < piXMin || piX > piXMax || piY < piYMin || piY > piYMax)
				return;

			if (!pOVisited.Add(fKey(piX, piY)))
				return;

			pONext.Add(fKey(piX, piY));
		}
	}
}
