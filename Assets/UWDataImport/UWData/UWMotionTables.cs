namespace UWDataImport.UWData
{
	/// <summary>
	/// THE TABLES OF THE ORIGINAL'S MOTION CORE (UW.EXE, read 2026-10-05 for the motion rework,
	/// stage 1): the sine table the headings are turned into directions with, the wall headings,
	/// the 3x3 tile offsets, the corner tables of the wall octant and the tile traverse flags.
	/// All values are the binary's own (the sine words copied from seg063 at 0x4E0 - they are
	/// neither rounded nor truncated sines of the usual kind, 220 of the 321 differ from a rounded
	/// table by one).
	/// </summary>
	public static class UWMotionTables
	{
		/// <summary>
		/// seg063:04E0, 321 words: sin(n * 360 / 256) * 0x7FFF for n = 0..255, then 65 more so the
		/// cosine (the same table 64 entries on) and the interpolation (one entry on) can read past
		/// the end. Heading 0 = +y, 0x4000 (index 64) = +x.
		/// </summary>
		private static readonly short[] msSine =
		{
			     0,    804,   1608,   2411,   3212,   4011,   4808,   5602,   6393,   7180,   7962,   8740,   9512,  10279,  11039,  11793,
			 12540,  13279,  14010,  14733,  15447,  16151,  16846,  17531,  18205,  18868,  19520,  20160,  20788,  21403,  22006,  22595,
			 23170,  23732,  24279,  24812,  25330,  25833,  26320,  26791,  27246,  27684,  28106,  28511,  28899,  29269,  29622,  29957,
			 30274,  30572,  30853,  31114,  31357,  31581,  31786,  31972,  32138,  32286,  32413,  32522,  32610,  32679,  32729,  32758,
			 32767,  32758,  32729,  32679,  32610,  32522,  32413,  32286,  32138,  31972,  31786,  31581,  31357,  31114,  30853,  30572,
			 30274,  29957,  29622,  29269,  28899,  28511,  28106,  27684,  27246,  26791,  26320,  25833,  25330,  24812,  24279,  23732,
			 23170,  22595,  22006,  21403,  20788,  20160,  19520,  18868,  18205,  17531,  16846,  16151,  15447,  14733,  14010,  13279,
			 12540,  11793,  11039,  10279,   9512,   8740,   7962,   7180,   6393,   5602,   4808,   4011,   3212,   2411,   1608,    804,
			     0,   -804,  -1608,  -2411,  -3212,  -4011,  -4808,  -5602,  -6393,  -7180,  -7962,  -8740,  -9512, -10279, -11039, -11793,
			-12540, -13279, -14010, -14733, -15447, -16151, -16846, -17531, -18205, -18868, -19520, -20160, -20788, -21403, -22006, -22595,
			-23170, -23732, -24279, -24812, -25330, -25833, -26320, -26791, -27246, -27684, -28106, -28511, -28899, -29269, -29622, -29957,
			-30274, -30572, -30853, -31114, -31357, -31581, -31786, -31972, -32138, -32286, -32413, -32522, -32610, -32679, -32729, -32758,
			-32767, -32758, -32729, -32679, -32610, -32522, -32413, -32286, -32138, -31972, -31786, -31581, -31357, -31114, -30853, -30572,
			-30274, -29957, -29622, -29269, -28899, -28511, -28106, -27684, -27246, -26791, -26320, -25833, -25330, -24812, -24279, -23732,
			-23170, -22595, -22006, -21403, -20788, -20160, -19520, -18868, -18205, -17531, -16846, -16151, -15447, -14733, -14010, -13279,
			-12540, -11793, -11039, -10279,  -9512,  -8740,  -7962,  -7180,  -6393,  -5602,  -4808,  -4011,  -3212,  -2411,  -1608,   -804,
			     0,    804,   1608,   2411,   3212,   4011,   4808,   5602,   6393,   7180,   7962,   8740,   9512,  10279,  11039,  11793,
			 12540,  13279,  14010,  14733,  15447,  16151,  16846,  17531,  18205,  18868,  19520,  20160,  20788,  21403,  22006,  22595,
			 23170,  23732,  24279,  24812,  25330,  25833,  26320,  26791,  27246,  27684,  28106,  28511,  28899,  29269,  29622,  29957,
			 30274,  30572,  30853,  31114,  31357,  31581,  31786,  31972,  32138,  32286,  32413,  32522,  32610,  32679,  32729,  32758,
			 32767
		};

		/// <summary>
		/// seg019_EAE -> seg019_A38: sine and cosine of a 16-bit heading, interpolated between the
		/// table entries by the heading's low byte: s = tab[i] + (((tab[i + 1] - tab[i]) * lo) >> 8),
		/// the product 32 bits, its bits 8..23 taken as the 16-bit step (al = ah, ah = dl). The
		/// cosine reads the same table 64 entries on.
		/// </summary>
		public static void SinCos(int piHeading, out int piSin, out int piCos)
		{
			int liIndex = (piHeading >> 8) & 0xFF;
			int liFraction = piHeading & 0xFF;

			piSin = (short)(msSine[liIndex] + (short)(((msSine[liIndex + 1] - msSine[liIndex]) * liFraction) >> 8));
			piCos = (short)(msSine[liIndex + 64] + (short)(((msSine[liIndex + 65] - msSine[liIndex + 64]) * liFraction) >> 8));
		}

		/// <summary>seg019_E63 -> seg019_A69: the table entry of the heading's high byte alone, no
		/// interpolation (GetCoordinateInDirection).</summary>
		public static void SinCosCoarse(int piHeading, out int piSin, out int piCos)
		{
			int liIndex = (piHeading >> 8) & 0xFF;

			piSin = msSine[liIndex];
			piCos = msSine[liIndex + 64];
		}

		/// <summary>dseg_433: the heading along a wall per wall octant 0..7, and a ninth entry.</summary>
		public static readonly int[] WallHeading = { 0, 0xE000, 0xC000, 0xA000, 0x8000, 0x6000, 0x4000, 0x2000, 0x0100 };

		/// <summary>dseg_2BA: the 3x3 tile offsets in the 64-wide map, t = 4 + dx + 3 * dy.</summary>
		public static readonly int[] NeighbourOffset = { -65, -64, -63, -1, 0, 1, 63, 64, 65 };

		/// <summary>dseg_2C3: the edge bits tested against TileTraverseFlags in the diagonal edge
		/// test of ProcessMotionTileHeights (five of the six are copied).</summary>
		public static readonly int[] EdgeBits = { 4, 0x10, 2, 8, 4, 1 };

		/// <summary>dseg_2C8: the corner signs of the wall octant (see seg026_7F6 in UWMotionCore).</summary>
		public static readonly int[] CornerSign = { 1, -1, -1, 1 };

		/// <summary>dseg_2CC..2D4, indexed 2D0 + n for n in -4..4: the 3x3 sign sum to a wall
		/// octant, 9 = none.</summary>
		public static readonly int[] OctantBySum = { 5, 4, 3, 6, 9, 2, 7, 0, 1 };

		/// <summary>TileTraverseFlags_dseg_1D8A per tile type 0..9.</summary>
		public static readonly int[] TileTraverseFlags = { 0x1E, 0, 0x13, 0x15, 0x0B, 0x0D, 0x20, 0x20, 0x20, 0x20 };

		/// <summary>
		/// Log2_dseg_3FA: the one-bit contact state (params +0x25) to the 3-bit field of the
		/// object's byte +0x0A: [1] 0 ground, [2] 1 water, [4] 2 lava, [8] 3 terrain 3, [0x10] 4
		/// airborne. The other entries are not read in the flows transcribed (GUESS 0).
		/// </summary>
		public static readonly int[] ContactStateIndex = { 0, 0, 1, 0, 2, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 4 };

		/// <summary>The contact state bits of params +0x25.</summary>
		public const int ContactGround = 1;

		public const int ContactWater = 2;

		public const int ContactLava = 4;

		public const int ContactTerrain3 = 8;

		public const int ContactAirborne = 0x10;

		/// <summary>The 3-bit field back to the one-bit state: 1 &lt;&lt; field.</summary>
		public static int ContactStateFromIndex(int piIndex)
		{
			return 1 << (piIndex & 7);
		}
	}
}
