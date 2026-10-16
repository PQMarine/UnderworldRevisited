using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE STORED PATH (struct PathRec, UnderworldExhumed PATHFIND.C: store_path,
	/// set_next_square_on_path): the squares of a found path from the start, the jump flag of each,
	/// how many steps have been taken, and the square the creature heads for now. The original packs
	/// two direction bits and one jump bit per step; ours keeps the squares themselves, which come
	/// to the same squares in the same order. Since 2026-10-15 (audit round 2, A22 / KR D22).
	/// </summary>
	public sealed class UWCritterPath
	{
		private readonly List<UWTilePos> mOSquares = new List<UWTilePos>();

		private readonly List<bool> mOJumps = new List<bool>();

		/// <summary>The steps of the path (the original's pathlen, PathRec.index).</summary>
		public int Length => mOSquares.Count - 1;

		/// <summary>The steps taken (flag.bits.count).</summary>
		public int Count { get; private set; }

		/// <summary>The square the creature heads for now (PathRec x, y).</summary>
		public int X { get; private set; }

		public int Y { get; private set; }

		/// <summary>The step into the current square is a leap (flag.bits.slope).</summary>
		public bool JumpStep { get; private set; }

		/// <summary>store_path: the squares (start first, the goal last) and their jump flags; the
		/// creature stands on the start square, no step taken yet.</summary>
		public void Store(IReadOnlyList<UWTilePos> pOSquares, IReadOnlyList<bool> pOJumps)
		{
			mOSquares.Clear();
			mOJumps.Clear();

			for (int liAt = 0; liAt < pOSquares.Count; liAt++)
			{
				mOSquares.Add(pOSquares[liAt]);
				mOJumps.Add(pOJumps != null && liAt < pOJumps.Count && pOJumps[liAt]);
			}

			Count = 0;
			JumpStep = false;

			if (mOSquares.Count > 0)
			{
				X = mOSquares[0].X;
				Y = mOSquares[0].Y;
			}
		}

		/// <summary>set_next_square_on_path: the next square and its jump flag; false at the end of
		/// the path.</summary>
		public bool Advance()
		{
			if (Count >= Length)
				return false;

			Count++;
			X = mOSquares[Count].X;
			Y = mOSquares[Count].Y;
			JumpStep = mOJumps[Count];

			return true;
		}

		/// <summary>The square the step after the current one leads to - where a leap lands
		/// (do_that_jump_kinda_thing: the current square plus the direction of the next step).
		/// False at the end of the path, where the original reads past the stored directions.</summary>
		public bool TryGetSquareAfter(out int piX, out int piY)
		{
			if (Count + 1 > Length)
			{
				piX = X;
				piY = Y;

				return false;
			}

			piX = mOSquares[Count + 1].X;
			piY = mOSquares[Count + 1].Y;

			return true;
		}

		/// <summary>
		/// close_to_square (PATHFIND.C): does a creature on tile (piTileX, piTileY) at the fine
		/// position (piFineX, piFineY) count as being on the path square? Unless the step is a leap,
		/// within two eighths of the edge towards the square counts as there.
		/// </summary>
		public static bool CloseToSquare(bool pbJump, int piTileX, int piTileY, int piFineX, int piFineY,
			int piPathX, int piPathY)
		{
			if (!pbJump)
			{
				if (piFineX >= UWCritterRules.PathCornerCutFineHigh && piPathX > piTileX)
					piTileX++;
				else if (piFineX <= UWCritterRules.PathCornerCutFineLow && piPathX < piTileX)
					piTileX--;

				if (piFineY >= UWCritterRules.PathCornerCutFineHigh && piPathY > piTileY)
					piTileY++;
				else if (piFineY <= UWCritterRules.PathCornerCutFineLow && piPathY < piTileY)
					piTileY--;
			}

			return piTileX == piPathX && piTileY == piPathY;
		}
	}

	/// <summary>
	/// THE SIXTEEN PATH SLOTS of a level (paths[], freepaths, find_free_path and clear_paths,
	/// UnderworldExhumed PATHFIND.C): a creature that finds a path stores it in a free slot and
	/// follows it until it ends, it collides, its destination changes or its animation is neither
	/// walk nor stand; with all sixteen taken, find_free_path fails and the creature wanders.
	/// Cleared with every level build (init_level_creature_stuff). The session's, like the clock
	/// and the alarm. Until 2026-10-15 ours searched anew for the next tile every half second and
	/// had no limit (audit round 2, A22 / KR D22).
	/// </summary>
	public sealed class UWCritterPathSlots
	{
		public const int SlotCount = UWCritterRules.PathSlots;

		private readonly UWCritterPath[] mOPaths = new UWCritterPath[SlotCount];

		/// <summary>The creature (its mobile index) that stored the path of a slot, -1 for none.
		/// Ours only - see IsOwner.</summary>
		private readonly int[] miOwners = new int[SlotCount];

		/// <summary>One bit per free slot (freepaths, 0xFFFF when all are free).</summary>
		private int miFree = AllFree;

		private const int AllFree = (1 << SlotCount) - 1;

		public UWCritterPathSlots()
		{
			for (int liAt = 0; liAt < SlotCount; liAt++)
			{
				mOPaths[liAt] = new UWCritterPath();
				miOwners[liAt] = NoOwner;
			}
		}

		private const int NoOwner = -1;

		/// <summary>
		/// WHETHER THIS CREATURE STORED THE SLOT'S PATH. The original keeps no owner: the has-a-path
		/// bit (byte 0x15 bit 7) is also the "collided with a mobile" bit the physics sets, so a
		/// creature that touched the player or another creature follows - and later frees - the
		/// path of whatever slot its word 0x16 names, another creature's included. Ours lets such a
		/// creature neither follow nor free a path it did not store (the bit still lets it cross a
		/// water or lava edge, UWCreatureMotion); its next update searches its own (2026-10-15,
		/// DEVIATIONS candidate).
		/// </summary>
		public bool IsOwner(int piSlot, int piOwner)
		{
			int liSlot = piSlot & (SlotCount - 1);

			return (miFree & (1 << liSlot)) == 0 && miOwners[liSlot] == piOwner;
		}

		/// <summary>The path of a slot.</summary>
		public UWCritterPath this[int piSlot] => mOPaths[piSlot & (SlotCount - 1)];

		/// <summary>How many slots are free.</summary>
		public int FreeCount
		{
			get
			{
				int liCount = 0;

				for (int liAt = 0; liAt < SlotCount; liAt++)
				{
					if ((miFree & (1 << liAt)) != 0)
						liCount++;
				}

				return liCount;
			}
		}

		/// <summary>find_free_path: the lowest free slot, without taking it; false when all
		/// sixteen are taken.</summary>
		public bool TryFindFree(out int piSlot)
		{
			for (int liAt = 0; liAt < SlotCount; liAt++)
			{
				if ((miFree & (1 << liAt)) != 0)
				{
					piSlot = liAt;

					return true;
				}
			}

			piSlot = 0;

			return false;
		}

		/// <summary>freepaths &amp;= ~(1 &lt;&lt; slot): the slot is taken, by this creature.</summary>
		public void Take(int piSlot, int piOwner)
		{
			int liSlot = piSlot & (SlotCount - 1);

			miFree &= ~(1 << liSlot);
			miOwners[liSlot] = piOwner;
		}

		/// <summary>freepaths |= 1 &lt;&lt; slot: the slot is free again.</summary>
		public void Free(int piSlot)
		{
			int liSlot = piSlot & (SlotCount - 1);

			miFree |= 1 << liSlot;
			miOwners[liSlot] = NoOwner;
		}

		/// <summary>clear_paths: every slot free.</summary>
		public void Clear()
		{
			miFree = AllFree;

			for (int liAt = 0; liAt < SlotCount; liAt++)
				miOwners[liAt] = NoOwner;
		}
	}
}
