namespace UWDataImport.UWData
{
	/// <summary>
	/// What happens to an object that comes to rest in WATER: it is culled, unless it is
	/// valuable enough. This is the rule the old "byte 9 below 40" stood in for.
	///
	/// WHERE IT SITS IN UW.EXE. The landing of a flying object
	/// (ObjectHitsFloorTileDestroyTalismans_seg029_C6F, labels C96 to CC8) reads the object's
	/// tile state, byte 0x0A bits 4-6; a 1 is water. It then sets the break chance to 8 of 8,
	/// which always fires, and calls ObjectCulling_seg027_2861_226 with the priority 0x0A.
	/// That routine forms a CULLING RANGE from the argument plus a roll and puts it into
	/// GlobalCullingRange_dseg_5c99_2740, then asks ObjectCullingTest_seg027_2861_1A6
	/// (labels 1A6 to 223): the object survives when its CULLING PRIORITY - COMOBJ byte 9
	/// bits 2-5 - plus half the extra count of a stack is GREATER than the range. The test is
	/// run over the object's contents as well (RunCodeOnObjectChain_seg027_117), so a
	/// container that holds something valuable survives with it. Word 0 bit 13 protects
	/// outright; on an ordinary item that bit is not set.
	///
	/// WHY THE BASE IS 11 HERE AND 0x0A THERE. The roll is (RNG * 3) / 0x8000, which is 0 to 2,
	/// so the reading gives a range of 10 to 12 - and that does not survive the measurement.
	/// The user threw, in the original, on 2026-09-21:
	///
	///   ten wands (byte 9 = 40, priority 10)       ten sank
	///   ten emeralds (byte 9 = 44, priority 11)    ten sank
	///   27 keys (byte 9 = 48, priority 12)         eight stayed, so about a third
	///   earlier, 2026-08-31: byte 9 = 60 (priority 15) stayed, byte 9 of 32 and 24 went
	///
	/// A range of 10 to 12 would let a third of the emeralds and two thirds of the keys
	/// survive. A range of 11 to 13 gives exactly what was counted: nothing at 11, a third at
	/// 12, everything from 14 up. So the SHAPE comes from the disassembly and the BASE from
	/// the measurement.
	///
	/// WHERE THE EXTRA STEP IS NOT, gone through on 2026-09-22. It is still not found, but the
	/// ground is now covered and nobody has to walk it again:
	///
	///   - all SIX callers of ObjectCulling_seg027_2861_226 push 0x0A, the water landing among
	///     them (ObjectHitsFloorTileDestroyTalismans_seg029_C6F label DED, 101670-101680)
	///   - the roll is (RNG * 3) / 0x8000 with a divisor pushed before the call (labels 23E to
	///     25C), and RNG_seg005_DE7 (see UWOriginalRandom) masks its result with 0x7FFF, so the
	///     quotient truncates to 0, 1 or 2 and the range is 10, 11 or 12
	///   - the test survives on GREATER, not on equal (label 216, jle means culled)
	///   - there IS A SECOND ROLL after the value test fails (label 2BA): (RNG * 0x0A) / 0x8000,
	///     so 0 to 9, and the object survives when it reaches the range. At a range of ten or
	///     more it can never fire - see SecondChanceDice
	///   - the two other ways into the routine are not it either: MoveObjectToCoordinates culls
	///     when it places and its own flag is zero, RemoveObject culls and then removes only
	///     what the cull condemns. The water landing uses neither, it inserts into the tile
	///     itself (InsertObjectToList_seg027_561)
	///
	/// ONE THING THE READING DID CHANGE, about the measurement rather than the rule: a
	/// CONTAINER and everything in it are tested against the SAME range
	/// (RunCodeOnObjectChain_seg027_117 with the one GlobalCullingRange), so a bag of ten
	/// emeralds thrown in is ONE roll and not ten. The keys carry the conclusion on their own:
	/// priority 12, and about eight of twenty-seven stayed, which is the third that a base of
	/// 11 gives and not the two thirds that a base of 10 would.
	///
	/// LAVA IS THIS RULE WITH TWO EXCEPTIONS IN FRONT (read again 2026-09-24, after Tybal's
	/// keys vanished in the lava of his lair). The earlier note here, "eight points of fire
	/// damage", misread the arguments. Value 6 of PlacedObjectCollison_seg029_104D (label 123D)
	/// first spares an object whose QUALITY CLASS (COMOBJ byte 6 bits 2-3) is 3. Otherwise it
	/// asks ScaleDamageAgainstObject for one point of FIRE (type 8), which comes back 0 for a
	/// fire-resistant object (COMOBJ byte 8 bit 3) and spares it as well. Anything else goes to
	/// RemoveObject_seg027_2861_6DD with flag 0, which runs this very culling test (priority
	/// 0x0A) and removes only what it condemns. Water (value 5) goes straight to that removal.
	/// Keys, incense, strong thread and most tools are class 3 and stay in lava; scrolls are
	/// class 0 with priority 8 and burn (per user in the original, 2026-09-24). See
	/// SparedByLava.
	/// </summary>
	public static class UWLiquidCulling
	{
		/// <summary>The lowest culling range, see the class comment: pinned by measurement,
		/// the disassembly's own argument is 0x0A.</summary>
		public const int RangeBase = 11;

		/// <summary>How many values the range takes: base, base + 1, base + 2 - the roll of
		/// ObjectCulling_seg027_2861_226.</summary>
		public const int RangeDice = 3;

		/// <summary>
		/// The second roll of the same routine (label 2BA): 0 to 9, and whatever reaches the
		/// range survives after all. IT IS NOT BUILT because at our range it can never fire -
		/// see SecondChanceCouldSave, which the self-check pins. It would matter only where the
		/// routine is called with a base below ten, and no caller does that.
		/// </summary>
		public const int SecondChanceDice = 0x0A;

		/// <summary>Whether that second roll could save anything at this range at all.</summary>
		public static bool SecondChanceCouldSave(int piRange)
		{
			return SecondChanceDice - 1 >= piRange;
		}

		/// <summary>One landing rolls this once and uses it for the object and everything in
		/// it, as the original keeps it in GlobalCullingRange for the whole test.</summary>
		public static int RollRange()
		{
			return RangeBase + UWRandom.Next(RangeDice);
		}

		/// <summary>
		/// Whether the liquid swallows this object: its culling priority plus half the extra
		/// count of a stack must be GREATER than the range to survive.
		/// </summary>
		/// <param name="piCullingPriority">COMOBJ byte 9 bits 2-5.</param>
		/// <param name="piQuantity">The count of a stack, 1 for a single object.</param>
		/// <param name="piRange">The range of this landing, from RollRange.</param>
		public static bool Swallows(int piCullingPriority, int piQuantity, int piRange)
		{
			int liExtra = piQuantity > 1 ? piQuantity - 1 : 0;

			return piCullingPriority + (liExtra / 2) <= piRange;
		}

		/// <summary>The quality class lava never takes (COMOBJ byte 6 bits 2-3).</summary>
		public const int LavaProofQualityClass = 3;

		/// <summary>The fire bit of the resistances (COMOBJ byte 8), damage type 8.</summary>
		public const int FireResistanceBit = 0x08;

		/// <summary>Whether lava leaves this object alone before any culling - see the class
		/// comment. Only the object itself is asked, not what it holds.</summary>
		public static bool SparedByLava(int piQualityClass, int piResistances)
		{
			return piQualityClass == LavaProofQualityClass || (piResistances & FireResistanceBit) != 0;
		}

		/// <summary>Whether this priority is swallowed whatever the roll gives - the wand and
		/// the emerald of the measurement. For a check, where no roll belongs.</summary>
		public static bool AlwaysSwallows(int piCullingPriority)
		{
			return piCullingPriority <= RangeBase;
		}

		/// <summary>Whether this priority survives whatever the roll gives - the heavy things
		/// that stayed in the user's throwing test of 2026-08-31.</summary>
		public static bool NeverSwallows(int piCullingPriority)
		{
			return piCullingPriority > RangeBase + RangeDice - 1;
		}
	}
}
