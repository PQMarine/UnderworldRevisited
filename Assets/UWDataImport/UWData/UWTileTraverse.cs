using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE TRIPLE TEST of the creature's tile routes, TraverseMultipleTiles_seg006_1477_6F6
	/// (read 2026-10-05 for stage 2 of the motion rework): is the middle tile B passable from A
	/// towards C? Three entry cases - no A (the start tile: the B-C edge and a step up of at most
	/// one level, nothing about B itself), no C (the goal: the drop into B against the budget and
	/// B's terrain, unless an object carries the creature), and the full triple (the B-C edge,
	/// closed doors in B by their orientation, the heights with a one-level step up, drops of two
	/// or more costing their depth less one, B's impassable terrain, and the two jumps of a
	/// creature with table byte 0x0A bit 5: over a pit and over impassable terrain). Fliers get
	/// the walls and doors only. Reading aid: the private notes motion-path.md section 3.
	///
	/// The per-creature context (the handler's masks, the COMOBJ height, the jump bit) is set
	/// before a route runs; the budget is the route's (UWTileRoute.Budget).
	/// </summary>
	public sealed class UWTileTraverse
	{
		/// <summary>The wall bits of TileTraverseFlags as the routine tests them: 2 the -x side
		/// (west), 4 the +x side (east), 8 the -y side (south), 0x10 the +y side (north).</summary>
		public const int WallWest = 2;

		public const int WallEast = 4;

		public const int WallSouth = 8;

		public const int WallNorth = 0x10;

		/// <summary>The "bound to the floor" bit of the impassable mask (land 0x1010, swimmer
		/// 0x10A8; a flier's 0x80 lacks it): without it only walls and doors are tested.</summary>
		public const int FloorBoundBit = 0x1000;

		/// <summary>The handler masks per kind: +4 impassable terrain, +6 cost terrain
		/// (InitVariablesAndDelegates_seg006_1477_28E, see UWCreatureMotion for the blocks).</summary>
		private static readonly int[] ImpassableByKind = { 0x1010, 0x80, 0x10A8 };

		private static readonly int[] CostByKind = { 0x20, 0, 0 };

		/// <summary>Table byte 0x0A bit 5: the creature may jump (a pit, impassable terrain).</summary>
		public const int JumpRowBit = 0x20;

		/// <summary>The slope type that rises in a step direction: north (+y) 6, east (+x) 8,
		/// south (-y) 7, west (-x) 9 (tables dseg_BA and dseg_BF).</summary>
		private static int fRisingSlope(int piDx, int piDy)
		{
			if (piDy > 0)
				return 6;

			if (piDx > 0)
				return 8;

			if (piDy < 0)
				return 7;

			return 9;
		}

		private readonly IUWMotionWorld mOWorld;

		private readonly UWCommonObjectProperties mOProperties;

		private readonly UWTileRoute mORoute;

		private readonly List<UWMotionBody> mOBodies = new List<UWMotionBody>();

		/// <summary>The handler's impassable terrain mask (+4).</summary>
		public int ImpassableMask;

		/// <summary>The handler's cost terrain mask (+6).</summary>
		public int CostMask;

		/// <summary>dseg_246C: the creature's COMOBJ height.</summary>
		public int CreatureHeight;

		/// <summary>Table byte 0x0A bit 5.</summary>
		public bool CanJump;

		public UWTileTraverse(IUWMotionWorld pOWorld, UWCommonObjectProperties pOProperties, UWTileRoute pORoute)
		{
			mOWorld = pOWorld;
			mOProperties = pOProperties;
			mORoute = pORoute;
		}

		/// <summary>The context of one creature: the masks of its kind - the lava-proof quirk of
		/// UWCreatureMotion drops the lava bit from the kind's cost mask for the session - its
		/// height and its jump bit.</summary>
		public void SetCreature(UWCreatureMotion.Kind peKind, int piCreatureHeight, bool pbCanJump)
		{
			int liKind = (int)peKind;

			ImpassableMask = ImpassableByKind[liKind];
			CostMask = CostByKind[liKind] & (UWCreatureMotion.LavaFlagDropped(peKind) ? ~0x20 : ~0);
			CreatureHeight = piCreatureHeight;
			CanJump = pbCanJump;
		}

		private UWCommonObjectProperties.Entry fEntry(int piItemId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			if (mOProperties != null && mOProperties.TryGet(piItemId, out lOEntry))
				return lOEntry;

			return default;
		}

		/// <summary>The tile's type, floor nibble and terrain; off the map solid, floor 0.</summary>
		private void fTile(int piX, int piY, out int piType, out int piFloor, out int piTerrain)
		{
			if (!mOWorld.TryGetTile(piX, piY, out piType, out piFloor, out piTerrain))
			{
				piType = 0;
				piFloor = 0;
				piTerrain = 0;
			}
		}

		private static int fWall(int piType)
		{
			return piType >= 0 && piType < UWMotionTables.TileTraverseFlags.Length ? UWMotionTables.TileTraverseFlags[piType] : 0;
		}

		private static bool fIsClosedDoor(int piItemId)
		{
			return (piItemId >> 4) == 0x14 && (piItemId & 0xF) < 8;
		}

		/// <summary>
		/// The triple test (UWTileRoute.TripleDelegate). A.x == 0: no previous tile; C.x == 0: no
		/// next tile. piHeightIn is the level the creature arrives with; piHeightOut the level
		/// it leaves with; piCost accumulates against the route's budget; pbClimb is dseg_244F,
		/// set on a jump.
		/// </summary>
		public bool Triple(int piAx, int piAy, int piBx, int piBy, int piCx, int piCy,
			int piHeightIn, ref int piHeightOut, ref int piCost, out bool pbClimb)
		{
			pbClimb = false;

			int liTypeA, liFloorA, liTerrainA;
			int liTypeB, liFloorB, liTerrainB;
			int liTypeC, liFloorC, liTerrainC;

			fTile(piAx, piAy, out liTypeA, out liFloorA, out liTerrainA);
			fTile(piBx, piBy, out liTypeB, out liFloorB, out liTerrainB);
			fTile(piCx, piCy, out liTypeC, out liFloorC, out liTerrainC);

			int liBudget = mORoute != null ? mORoute.Budget : 0;
			bool lbFloorBound = (ImpassableMask & FloorBoundBit) != 0;

			// ---- case 1: no previous tile (lines 41836-41997).
			if (piAx == 0)
			{
				piHeightOut = piHeightIn;

				if (!fEdgeOpen(liTypeB, liTypeC, piBx, piBy, piCx, piCy))
					return false;

				if (!lbFloorBound)
					return true;

				int liHc = liFloorC;

				if (liTypeC >= 6 && liTypeC <= 9 && fRisingSlope(piCx - piBx, piCy - piBy) != liTypeC)
					liHc++;

				return liHc <= piHeightIn + 1;
			}

			// ---- case 2: no next tile, the goal (lines 42014-42179).
			if (piCx == 0)
			{
				piHeightOut = piHeightIn;

				if (!lbFloorBound)
					return true;

				int liTop = fFirstStandableTop(piBx, piBy);
				int liHb = liFloorB;
				bool lbOnObject = false;

				if (liTop > liHb)
				{
					liHb = liTop;
					lbOnObject = true;
				}

				if (liFloorA > liHb + 1)
				{
					piHeightOut = liHb;
					piCost = (piCost + (piHeightIn - piHeightOut) - 1) & 0xFF;

					if (piCost > liBudget)
						return false;
				}

				if (!lbOnObject)
				{
					if ((ImpassableMask & (8 << liTerrainB)) != 0)
						return false;

					if ((CostMask & (8 << liTerrainB)) != 0)
					{
						piCost = (piCost + 2) & 0xFF;

						if (piCost > liBudget)
							return false;
					}
				}

				return true;
			}

			// ---- case 3: the full triple (lines 42184-43070).
			piHeightOut = piHeightIn;

			if (!fEdgeOpen(liTypeB, liTypeC, piBx, piBy, piCx, piCy))
				return false;

			int liObjectTop;

			if (!fWalkObjects(piAx, piAy, piBx, piBy, piCx, piCy, out liObjectTop))
				return false;

			if (!lbFloorBound)
			{
				// GUESS what the value means: a flying level from the creature's height.
				piHeightOut = 0x10 - ((CreatureHeight + 3) >> 2);

				return true;
			}

			int liHab = liFloorB > liFloorA ? liFloorB : liFloorA;
			int liHbc0 = liFloorB > liFloorC ? liFloorB : liFloorC;
			int liHbc = liHbc0;
			bool lbDrop = false;
			bool lbOnObj = false;

			if (piHeightIn > liHab)
				liHab = piHeightIn;

			if (liTypeC >= 6 && liTypeC <= 9 && fRisingSlope(piCx - piBx, piCy - piBy) != liTypeC)
				liHbc++;

			if (((liHab > liHbc ? liHab : liHbc) * 8) + CreatureHeight > 0x7F)
				return false;

			if (liHab > liHbc + 1)
			{
				if (liHab > liObjectTop + 1)
				{
					// A drop of two or more levels: d - 1 against the budget.
					lbDrop = true;
					piHeightOut = liHbc > liObjectTop ? liHbc : liObjectTop;
					piCost = (piCost + (liHab - piHeightOut) - 1) & 0xFF;

					if (piCost > liBudget)
						return false;
				}
				else
				{
					// Landing on the object in B.
					liHbc = liObjectTop;
					liHbc0 = liObjectTop;
					lbOnObj = true;
				}
			}
			else if (liObjectTop != 0 && liHab >= liObjectTop && liHab <= liObjectTop + 1)
				lbOnObj = true;

			if (liHbc > liHab + 1)
				return false;

			if (liHab > liFloorB + 1 && !lbDrop && !lbOnObj)
			{
				// B is a pit below the level of A and C.
				if ((ImpassableMask & (8 << liTerrainB)) != 0)
					return false;

				if ((CostMask & (8 << liTerrainC)) != 0)
				{
					piCost = (piCost + 2) & 0xFF;

					if (piCost >= liBudget)
						return false;
				}

				if (liHab > liObjectTop + 1)
				{
					piHeightOut = liHab;

					if (liHab < liHbc)
						return false;

					if (!CanJump)
						return false;

					piCost = (piCost + 1) & 0xFF;

					if (piCost >= liBudget)
						return false;

					pbClimb = true;

					return true;
				}

				piHeightOut = liHbc0;

				return true;
			}

			// The normal path.
			if ((ImpassableMask & (8 << liTerrainB)) == 0 || lbOnObj)
			{
				piHeightOut = liHbc0;

				return true;
			}

			// B's terrain is impassable and nothing carries the creature: the jump over B.
			if ((ImpassableMask & (8 << liTerrainC)) != 0)
				return false;

			if ((CostMask & (8 << liTerrainC)) != 0)
			{
				piCost = (piCost + 2) & 0xFF;

				if (piCost > liBudget)
					return false;
			}

			if (!CanJump)
				return false;

			piCost = (piCost + 1) & 0xFF;

			if (piCost >= liBudget)
				return false;

			pbClimb = true;

			return true;
		}

		/// <summary>The B-C edge on both tiles: C closed on its near side, or B closed on its
		/// far side, blocks (lines 41845-41954 and 42188-42270; the first case runs all eight
		/// tests, only the matching ones can fire).</summary>
		private static bool fEdgeOpen(int piTypeB, int piTypeC, int piBx, int piBy, int piCx, int piCy)
		{
			int liWallB = fWall(piTypeB);
			int liWallC = fWall(piTypeC);

			if (piCx > piBx)
				return (liWallC & WallWest) == 0 && (liWallB & WallEast) == 0;

			if (piCx < piBx)
				return (liWallC & WallEast) == 0 && (liWallB & WallWest) == 0;

			if (piCy > piBy)
				return (liWallC & WallSouth) == 0 && (liWallB & WallNorth) == 0;

			if (piCy < piBy)
				return (liWallC & WallNorth) == 0 && (liWallB & WallSouth) == 0;

			return true;
		}

		/// <summary>Case 2: the first object of B that can be stood on (COMOBJ flags bit 1) with a
		/// non-zero top, (zpos + height) &gt;&gt; 3; 0 when none.</summary>
		private int fFirstStandableTop(int piBx, int piBy)
		{
			mOBodies.Clear();
			mOWorld.GetBodiesInTile(piBx, piBy, mOBodies);

			foreach (UWMotionBody lOBody in mOBodies)
			{
				UWCommonObjectProperties.Entry lOEntry = fEntry(lOBody.ItemId);

				if (!lOEntry.Is3DModel)
					continue;

				int liTop = (lOBody.ZPos + lOEntry.Height) >> 3;

				if (liTop != 0)
					return liTop;
			}

			return 0;
		}

		/// <summary>
		/// Case 3's walk of B's object list (lines 42272-42636): a closed door 0x140-0x147 blocks
		/// by its orientation against the way the creature passes through B; the first object
		/// that can be stood on gives the top the heights use, and ends the walk. False when a
		/// door blocks.
		/// </summary>
		private bool fWalkObjects(int piAx, int piAy, int piBx, int piBy, int piCx, int piCy, out int piTop)
		{
			piTop = 0;
			mOBodies.Clear();
			mOWorld.GetBodiesInTile(piBx, piBy, mOBodies);

			int liCase = -1;

			foreach (UWMotionBody lOBody in mOBodies)
			{
				int liItemId = lOBody.ItemId & 0x1FF;

				if (fIsClosedDoor(liItemId))
				{
					if (liCase < 0)
						liCase = fPassCase(piAx, piAy, piBx, piBy, piCx, piCy);

					if (fDoorBlocks(liCase, lOBody.Direction & 3, lOBody.XPos, lOBody.YPos))
						return false;

					continue;
				}

				UWCommonObjectProperties.Entry lOEntry = fEntry(liItemId);

				if (!lOEntry.Is3DModel)
					continue;

				piTop = (lOBody.ZPos + lOEntry.Height) >> 3;

				if (piTop != 0)
					break;
			}

			return true;
		}

		/// <summary>The way through B: the edge entered from A and the edge left towards C
		/// (lines 42343-42424): 0 W-N, 1 W-E, 2 W-S, 3 E-N, 4 E-W, 5 E-S, 6 S-W, 7 S-N, 8 S-E,
		/// 9 N-W, 10 N-S, 11 N-E.</summary>
		private static int fPassCase(int piAx, int piAy, int piBx, int piBy, int piCx, int piCy)
		{
			if (piAx < piBx)
				return piCy > piBy ? 0 : piCy == piBy ? 1 : 2;

			if (piAx > piBx)
				return piCy > piBy ? 3 : piCy == piBy ? 4 : 5;

			if (piAy < piBy)
				return piCx < piBx ? 6 : piCx == piBx ? 7 : 8;

			return piCx < piBx ? 9 : piCx == piBx ? 10 : 11;
		}

		/// <summary>
		/// The door table (the jump table at 43087, 12 cases on 6 handlers): a door with heading
		/// bits 0 sits on the north or south edge (its ypos says which), one with heading 2 on
		/// the west or east edge (its xpos); a straight pass needs the door parallel to it, a turn
		/// is blocked by a door on either edge it uses, the diagonal headings 1 and 3 go by the
		/// turn alone. The lock state is not consulted.
		/// </summary>
		private static bool fDoorBlocks(int piCase, int piDirection, int piXPos, int piYPos)
		{
			switch (piCase)
			{
				case 1:
				case 4:
					// W-E straight (handler C36): only a door with heading 0 lets it pass.
					return piDirection != 0;

				case 7:
				case 10:
					// S-N straight (C29): only heading 2 passes.
					return piDirection != 2;

				case 0:
				case 9:
					// W and N (C42).
					return piDirection == 0 ? piYPos > 3 : piDirection == 2 ? piXPos < 4 : piDirection == 1;

				case 3:
				case 11:
					// E and N (CAC).
					return piDirection == 0 ? piYPos > 3 : piDirection == 2 ? piXPos > 3 : piDirection == 3;

				case 2:
				case 6:
					// W and S (C77).
					return piDirection == 0 ? piYPos < 4 : piDirection == 2 ? piXPos < 4 : piDirection == 3;

				default:
					// 5, 8: E and S (CDA).
					return piDirection == 0 ? piYPos < 4 : piDirection == 2 ? piXPos > 3 : piDirection == 1;
			}
		}
	}
}
