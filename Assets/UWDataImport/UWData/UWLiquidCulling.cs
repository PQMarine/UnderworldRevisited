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
	/// THE RANGE IS 10 TO 12, AND A THROWN OBJECT IS TESTED TWICE IN WATER (settled 2026-10-03,
	/// per user: "let us tackle the water culling"). The roll is (RNG * 3) / 0x8000, 0 to 2, on
	/// the argument 0x0A that all six callers push. RNG_seg005_DE7 (see UWRandom) takes no
	/// arguments - the pushed 0 / 0x8000 are the divisor of the division after it - and the
	/// priority really is byte 9 (the table lies at 5B6E behind the file's two header bytes).
	/// What makes thrown things sink more often than one test at 10 to 12 would is the landing
	/// itself: in
	/// ApplyProjectileMotion_seg029_29EE_61A a projectile coming to rest goes first through
	/// ObjectHitsFloorTileDestroyTalismans_seg029_C6F, which over water sets the break chance to
	/// 8 of 8 and culls (label DED), and then through PlacedObjectCollison_seg029_104D at the same
	/// spot, whose terrain value 5 (water) calls RemoveObject_seg027_2861_6DD with flag 0 - which
	/// culls again, with a range rolled anew. Two tests, either of which takes the object.
	/// Something only put down (DropOrThrowByPlayer's placing, a spill) passes the collision alone.
	///
	/// The measurements in the original, all per user, fit that:
	///
	///   ten wands (priority 10)                     ten sank       - always, 10 never exceeds 10
	///   ten emeralds (priority 11)                  ten sank       - 1/9 to stay (1/3 per test)
	///   27 keys (priority 12)                       eight stayed   - 4/9 to stay, about 12
	///   2 x 8 stacks of five emeralds (11 + 2)      all stayed     - 13 exceeds every range
	///   2026-08-31: priority 15 stayed, 8 and 6 went
	///
	/// The base of 11 the port used from 2026-09-21 matched the keys with one test, but it would
	/// have let a third of those stacks sink, and the sixteen of 2026-10-03 rule it out (the odds
	/// of all staying under it are (2/3)^16, about one in 650).
	///
	/// WHERE NOTHING MORE IS, gone through on 2026-09-22 and again 2026-10-03:
	///
	///   - the test survives on GREATER, not on equal (label 216, jle means culled)
	///   - there IS A SECOND ROLL after the value test fails (label 2BA): (RNG * 0x0A) / 0x8000,
	///     so 0 to 9, and the object survives when it reaches the range. At a range of ten or
	///     more it can never fire - see SecondChanceDice
	///   - InsertObjectToList_seg027_561, which puts the landed copy into the tile, only links
	///   - CullObjects_seg027_2861_329 (see UWObjectLimitRules) runs only when the free list is
	///     low and when sleeping, with ranges of 3 and 1
	///
	/// A CONTAINER and everything in it are tested against the SAME range
	/// (RunCodeOnObjectChain_seg027_117 with the one GlobalCullingRange); it survives when
	/// anything in it survives.
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
		/// <summary>The lowest culling range: the argument 0x0A of every caller, see the class
		/// comment.</summary>
		public const int RangeBase = 0x0A;

		/// <summary>How many times a THROWN object is tested when it comes to rest in water: the
		/// landing and the collision after it - see the class comment.</summary>
		public const int ThrownIntoWaterTests = 2;

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

		/// <summary>Whether this priority is swallowed whatever the roll gives - the wand of the
		/// measurement. For a check, where no roll belongs.</summary>
		public static bool AlwaysSwallows(int piCullingPriority)
		{
			return piCullingPriority <= RangeBase;
		}

		/// <summary>Whether this priority survives whatever the roll gives - the heavy things
		/// that stayed in the user's throwing test of 2026-08-31, and the stacks of five emeralds
		/// of 2026-10-03.</summary>
		public static bool NeverSwallows(int piCullingPriority)
		{
			return piCullingPriority > RangeBase + RangeDice - 1;
		}
	}
}
