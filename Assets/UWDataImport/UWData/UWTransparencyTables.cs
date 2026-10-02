using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The colour translation tables from "xfer.dat" (uw-formats.txt 3.9). Certain colour indices
	/// do NOT overwrite the background, but tint it instead - that is how fog and
	/// ghosts become translucent and fire glows through.
	///
	/// The method: the colour index of the pixel being drawn selects the table, the colour index
	/// of the existing background pixel is the index into it, and the value stored there
	/// replaces the background pixel.
	///
	/// LOCATION OF THE TABLES, measured on the file (2026-08-30): the docs name five tables
	/// of 0x80 bytes starting at 0x80 - that does not fit. XFER.DAT is 1536 bytes, so exactly
	/// SIX tables of 256 bytes starting at 0. Both readings were checked by applying each to palette
	/// 0 and comparing the mean target colour with the mean source colour.
	/// Six times 256 yields six clearly distinct directions, five times 128
	/// on the other hand mush (two practically identical tables, several without any identity mapping):
	///
	///   Table 0 @0x000  (106,94,74) -> ( 64, 55, 44)   darker    -> black
	///   Table 1 @0x100  (106,94,74) -> (135,124, 97)   brighter  -> white
	///   Table 2 @0x200  (106,94,74) -> (124, 70, 49)   towards red
	///   Table 3 @0x300  (106,94,74) -> ( 76,114, 50)   towards green
	///   Table 4 @0x400  (106,94,74) -> ( 68, 72,102)   towards blue
	///   Table 5 @0x500  (106,94,74) -> (100, 24, 20)   towards dark red
	///
	/// WHICH COLOUR INDEX TRIGGERS WHICH TABLE is now established by measurement in the game and
	/// no longer taken from the docs. Their statement (0xF0 red, 0xF4 blue, 0xF8 green, white and
	/// black "????") did not survive any check: 0xF0 to 0xFA are ordinary dark colours in
	/// palette 0, and NO creature image of the game uses them. The count was made
	/// over the auxiliary palettes of all CR??PAGE files (2026-09-07); of the high indices
	/// exactly two occur there, 0xFB and 0xFC, and both are conspicuous marker colours
	/// in the palette (100/88/168 and 112/28/64).
	///
	/// The two pieces of evidence come from the game:
	///
	///   0xFC -> table 1: the fog cloud (object 449) consists only of this index and is
	///           grey-white in the original (per user, level 3 at tile 41/51, 2026-08-30).
	///           The same applies to the ordinary ghost (object 97, auxiliary palette 0), which
	///           is correct in our port (per user, 2026-09-07).
	///
	///   0xFB -> table 0: the dread spirit (object 113, auxiliary palette 3). We drew it
	///           light violet, because 0xFB was not treated as a blend colour here at all and
	///           so the plain palette colour 100/88/168 came out. In the original it is
	///           DARKER than the ordinary ghost (per user, 2026-09-07) - and table 0
	///           is the only one that darkens the background.
	///
	/// Together that is an ascending sequence from 0xFB. 0xFD (garish green 12/252/12,
	/// also a marker colour) would then be table 2 - not evidenced anywhere and therefore
	/// not entered either: a pixel that is wrongly translucent stands out, one that is
	/// wrongly opaque does not.
	///
	/// ABOUT THE REFERENCE: for uw1, UnderworldGodot maps 0xFB to row 1 and 0xFC to row 2
	/// (viewport_combination.gdshader), i.e. shifted by one against our measurement. Its
	/// own comment in the same file, however, says "0 = shadows(251)" and
	/// thereby contradicts its own code. We stick with what can be seen in the game.
	/// </summary>
	public class UWTransparencyTables
	{
		public enum FadeTable
		{
			Black = 0,
			White = 1,
			Red = 2,
			Green = 3,
			Blue = 4,
			DarkRed = 5
		}

		/// <summary>Evidenced in the game: the dread spirit consists only of this index and
		/// darkens in the original.</summary>
		public const int FadeToShadowIndex = 0xFB;

		/// <summary>Evidenced in the game: the fog cloud consists only of this index and is
		/// grey-white in the original.</summary>
		public const int FadeToWhiteIndex = 0xFC;

		private const int TableSize = 256;

		private const int FirstTableOffset = 0;

		private const int TableCount = 6;

		private readonly byte[][] myTables = new byte[TableCount][];

		public bool IsLoaded { get; private set; }

		public UWTransparencyTables(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "XFER.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyData = File.ReadAllBytes(lsFile);

			if (lyData.Length < FirstTableOffset + (TableCount * TableSize))
				return;

			for (int liTable = 0; liTable < TableCount; liTable++)
			{
				myTables[liTable] = new byte[TableSize];

				System.Array.Copy(lyData, FirstTableOffset + (liTable * TableSize),
					myTables[liTable], 0, TableSize);
			}

			IsLoaded = true;
		}

		/// <summary>Whether this colour index tints the background instead of
		/// overwriting it.</summary>
		public static bool IsFadeIndex(int piColourIndex)
		{
			FadeTable leTable;

			return TryGetFadeTable(piColourIndex, out leTable);
		}

		public static bool TryGetFadeTable(int piColourIndex, out FadeTable peTable)
		{
			switch (piColourIndex)
			{
				case FadeToShadowIndex:
					peTable = FadeTable.Black;
					return true;

				case FadeToWhiteIndex:
					peTable = FadeTable.White;
					return true;

				default:
					peTable = FadeTable.White;
					return false;
			}
		}

		/// <summary>Replacement colour: peTable selects the table, piBackgroundColourIndex is
		/// the index into it.</summary>
		public byte Apply(FadeTable peTable, int piBackgroundColourIndex)
		{
			int liTable = (int)peTable;

			if (!IsLoaded || liTable < 0 || liTable >= TableCount
				|| piBackgroundColourIndex < 0 || piBackgroundColourIndex >= TableSize)
				return (byte)piBackgroundColourIndex;

			return myTables[liTable][piBackgroundColourIndex];
		}

		/// <summary>How many tables the file holds.</summary>
		public static int Count => TableCount;

		/// <summary>Cells per colour axis of the inverse lookup: the VGA's six bits, the precision
		/// the palette itself has.</summary>
		public const int InverseLookupSize = 64;

		/// <summary>
		/// THE PICTURE BACK TO ITS INDICES, for applying a table the way the original does
		/// (2026-09-27, audit row 12; per user: the smoke, made of 0xFC only, is better to see in
		/// the original). The original draws into an 8-bit frame: the pixel behind a translucent
		/// one IS a palette index, and the table replaces it. A modern renderer holds colours
		/// there - but in the palette renderer every one of them is some palette colour, so the
		/// index can be recovered: this cube maps every colour (six bits per channel, red along
		/// x, green along y, blue along z) to the nearest of pOColours, index 0 left out (it is
		/// the transparency marker; black is index 1). The screen buffer may hold the colour with
		/// less precision than eight bits, which is why the cube holds the NEAREST index for
		/// every cell and not only the exact ones.
		/// </summary>
		public static byte[] BuildInverseLookup(UWColor32[] pOColours)
		{
			int liSize = InverseLookupSize;
			byte[] lyCube = new byte[liSize * liSize * liSize];

			if (pOColours == null || pOColours.Length < 2)
				return lyCube;

			int liCount = System.Math.Min(pOColours.Length, TableSize);

			// The palette in cell units (0..63), scaled by 4 to stay in integers.
			int[] liR = new int[liCount];
			int[] liG = new int[liCount];
			int[] liB = new int[liCount];

			for (int i = 1; i < liCount; i++)
			{
				liR[i] = (pOColours[i].R * (liSize - 1) * 4 + 127) / 255;
				liG[i] = (pOColours[i].G * (liSize - 1) * 4 + 127) / 255;
				liB[i] = (pOColours[i].B * (liSize - 1) * 4 + 127) / 255;
			}

			for (int liZ = 0; liZ < liSize; liZ++)
			{
				for (int liY = 0; liY < liSize; liY++)
				{
					for (int liX = 0; liX < liSize; liX++)
					{
						int liBest = 1;
						int liBestDistance = int.MaxValue;

						for (int i = 1; i < liCount; i++)
						{
							int liDr = liR[i] - (liX * 4);
							int liDg = liG[i] - (liY * 4);
							int liDb = liB[i] - (liZ * 4);
							int liDistance = (liDr * liDr) + (liDg * liDg) + (liDb * liDb);

							if (liDistance < liBestDistance)
							{
								liBestDistance = liDistance;
								liBest = i;
							}
						}

						lyCube[liX + (liY * liSize) + (liZ * liSize * liSize)] = (byte)liBest;
					}
				}
			}

			return lyCube;
		}

		/// <summary>
		/// Expresses a table as an ordinary alpha blend:
		///
		///   result = layer colour * opacity + background * (1 - opacity)
		///
		/// This is exactly the approach uw-formats.txt suggests for modern systems that no
		/// longer draw through a palette. It is computed as a linear least-squares fit
		/// over all 256 entries: plotting the target colour against the source colour
		/// per colour channel, the slope is (1 - opacity) and the intercept is
		/// layer colour * opacity. Together they give the values sought, without
		/// anything having to be guessed.
		/// </summary>
		public bool TryGetBlend(FadeTable peTable, UWPalette pOPalette, out UWColor32 pOColour, out float pfAlpha)
		{
			pOColour = default;
			pfAlpha = 0f;

			int liTable = (int)peTable;

			if (!IsLoaded || pOPalette == null || liTable < 0 || liTable >= TableCount)
				return false;

			double[] ldSlope = new double[3];
			double[] ldIntercept = new double[3];

			for (int liChannel = 0; liChannel < 3; liChannel++)
			{
				double ldSumX = 0, ldSumY = 0, ldSumXX = 0, ldSumXY = 0;

				for (int i = 0; i < TableSize; i++)
				{
					double ldX = fChannel(pOPalette.GetUWColor(i), liChannel);
					double ldY = fChannel(pOPalette.GetUWColor(myTables[liTable][i]), liChannel);

					ldSumX += ldX;
					ldSumY += ldY;
					ldSumXX += ldX * ldX;
					ldSumXY += ldX * ldY;
				}

				double ldDenominator = (TableSize * ldSumXX) - (ldSumX * ldSumX);

				if (System.Math.Abs(ldDenominator) < 1e-6)
					return false;

				ldSlope[liChannel] = ((TableSize * ldSumXY) - (ldSumX * ldSumY)) / ldDenominator;
				ldIntercept[liChannel] = (ldSumY - (ldSlope[liChannel] * ldSumX)) / TableSize;
			}

			// One opacity for all three channels: the layer is one colour with one
			// opacity, not three separate ones.
			double ldAlpha = 1.0 - ((ldSlope[0] + ldSlope[1] + ldSlope[2]) / 3.0);

			if (ldAlpha <= 0.001)
				return false;

			pfAlpha = (float)System.Math.Min(1.0, ldAlpha);

			pOColour = new UWColor32
			{
				R = fClampByte(ldIntercept[0] / ldAlpha),
				G = fClampByte(ldIntercept[1] / ldAlpha),
				B = fClampByte(ldIntercept[2] / ldAlpha),
				A = 255
			};

			return true;
		}

		private static double fChannel(UWColor32 pOColour, int piChannel)
		{
			if (piChannel == 0)
				return pOColour.R;

			return piChannel == 1 ? pOColour.G : pOColour.B;
		}

		private static byte fClampByte(double pdValue)
		{
			if (pdValue < 0)
				return 0;

			if (pdValue > 255)
				return 255;

			return (byte)pdValue;
		}
	}
}
