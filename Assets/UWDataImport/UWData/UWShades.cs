using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Lighting and view distance settings from "shades.dat" (uw-formats.txt 3.10).
	/// Eight entries of twelve bytes each, which together determine from what distance
	/// things are darkened, how far one can see at all, and at which brightness level
	/// shading starts.
	///
	/// THE ENTRY INDEX IS THE PLAYER'S LIGHT LEVEL, 0 to 7 - i.e. the brightness of the
	/// carried light source or of the light spell, whichever is brighter.
	/// This fits the values: entry 0 is already darkened at the player's own position and
	/// reaches three tiles, entry 7 shows the original colours and reaches seven.
	///
	/// Remarkable and explicitly mentioned in the docs: if the file is renamed or
	/// deleted, the game runs permanently bright, without torches and light spells. So the file
	/// is the switch for the entire lighting system - for a remake the place
	/// where view distance and darkening come from, instead of inventing them.
	///
	/// Caution, the docs contradict themselves: chapter 9.3 says "12 entries of 8 bytes",
	/// chapter 3.10 "8 entries of 12 bytes". The file size of 96 bytes fits both.
	/// The byte pattern comparison decides for 3.10: with twelve bytes each
	/// entry splits into six clean 16-bit words whose values rise or fall evenly
	/// across the eight entries. With eight bytes no such pattern emerges.
	/// </summary>
	public class UWShades
	{
		public struct Entry
		{
			/// <summary>How fast things darken with distance - the larger, the
			/// faster. Falls from 56 at light level 0 to 18 at level 7.</summary>
			public byte Shading;

			/// <summary>The high byte of Shading. Always zero in UW1; the docs considered it
			/// a flag byte whose bit 7 switches darkening off.</summary>
			public byte ShadingFlags;

			/// <summary>Starting brightness at the player's own position, as a level in LIGHT.DAT. 5 at
			/// light level 0 (already noticeably dark), 0 at level 4 and higher.</summary>
			public byte StartingLightLevel;

			public byte Unknown3;

			/// <summary>Offset of the start of darkening, negative. At light levels 0 and 1
			/// equal to -3, zero from level 3 on.</summary>
			public short DistanceBeforeShading;

			/// <summary>View distance in tiles. Everything beyond is black. 3 at
			/// light level 0, 7 at level 7. The docs warn that the original may crash above
			/// roughly 9 to 10.</summary>
			public short ViewingDistance;

			public short TexturingDistance;

			public short Unknown10;
		}

		public const int EntryCount = 8;

		/// <summary>Length of a shade table: one level per whole tile of distance.
		/// Also the number of brightness levels in LIGHT.DAT.</summary>
		public const int ShadeTableLength = 16;

		private const int EntrySize = 12;

		private readonly Entry[] mOEntries = new Entry[EntryCount];

		private readonly byte[][] myShadeTables = new byte[EntryCount][];

		private readonly byte[][] myDiagonalShadeTables = new byte[EntryCount][];

		public bool IsLoaded { get; private set; }

		public UWShades(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "SHADES.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyData = File.ReadAllBytes(lsFile);

			if (lyData.Length < EntryCount * EntrySize)
				return;

			for (int liIndex = 0; liIndex < EntryCount; liIndex++)
			{
				int liOffset = liIndex * EntrySize;

				mOEntries[liIndex] = new Entry
				{
					Shading = lyData[liOffset],
					ShadingFlags = lyData[liOffset + 1],
					StartingLightLevel = (byte)(lyData[liOffset + 2] & 0xF),
					Unknown3 = lyData[liOffset + 3],
					DistanceBeforeShading = fRead16(lyData, liOffset + 4),
					ViewingDistance = (short)(fRead16(lyData, liOffset + 6) & 0xF),
					TexturingDistance = fRead16(lyData, liOffset + 8),
					Unknown10 = fRead16(lyData, liOffset + 10)
				};
			}

			IsLoaded = true;
		}

		public bool TryGet(int piIndex, out Entry pOEntry)
		{
			if (!IsLoaded || piIndex < 0 || piIndex >= EntryCount)
			{
				pOEntry = default;
				return false;
			}

			pOEntry = mOEntries[piIndex];
			return true;
		}

		/// <summary>
		/// The shade table for a light level: for each integer tile distance
		/// the brightness level used for the lookup in LIGHT.DAT there. 0 is the
		/// original colours, 15 is black.
		///
		/// The calculation comes from the reference's disassembly (shade.ExtractShadeArray, with
		/// segment labels from the original): the distance is stretched by root two - the
		/// original computes in tile units of eight and measures diagonally -, weighted by
		/// Shading, shifted by DistanceBeforeShading and added on top of
		/// StartingLightLevel. It is capped at 14; beyond the view distance the value is 15.
		///
		/// The calculation uses the original's square root (see UWVanillaMath), not
		/// Math.Sqrt - the reference notes that the two can differ by one.
		///
		/// THE STRETCH BY ROOT TWO IS QUESTIONABLE. The reference squares the distance in
		/// eighths, DOUBLES it and then takes the root - so in effect distance times
		/// root two. It does not explain where the doubling comes from; possibly
		/// a diagonal is meant there.
		///
		/// Against this stands an observation of the original: there the colours persist about one
		/// tile further than in our version, with the same light source (per user in an
		/// image comparison, 2026-09-05). Without the doubling the curve stretches by exactly
		/// that factor and matches - at light level 4, 0 4 9 14 becomes 0 3 7 10 14,
		/// i.e. almost the table of the next higher light level.
		///
		/// CONFIRMED (per user, 2026-09-05): without the doubling the reach matches
		/// the original. So at this point the reference either misrepresents the
		/// original's wording, or the doubling belongs to a context there
		/// that we do not rebuild.
		///
		/// pbDiagonalStretch selects between the two: false is the state after the observation,
		/// true the reference's wording.
		///
		/// What this yields with the real UW1 data with pbDiagonalStretch true (the tables without
		/// the doubling reach correspondingly further, see the level 4 example above):
		///
		///   Level 0:  5 11 14 14 15 15 15 ...
		///   Level 1:  4  8 14 14 15 15 15 ...
		///   Level 2:  2  6 13 14 14 15 15 ...
		///   Level 3:  1  6 11 14 14 15 15 ...
		///   Level 4:  0  4  9 14 14 14 15 ...
		///   Level 5:  0  3  7 10 14 14 14 15 ...
		///   Level 6:  0  3  6  9 12 14 14 14 15 ...
		///   Level 7:  0  3  6  9 12 14 14 14 15 ...
		/// </summary>
		public byte[] GetShadeTable(int piIndex, bool pbDiagonalStretch = false)
		{
			if (!IsLoaded || piIndex < 0 || piIndex >= EntryCount)
				return null;

			byte[][] lyCache = pbDiagonalStretch ? myDiagonalShadeTables : myShadeTables;

			if (lyCache[piIndex] == null)
				lyCache[piIndex] = fBuildShadeTable(mOEntries[piIndex], pbDiagonalStretch);

			return lyCache[piIndex];
		}

		private static byte[] fBuildShadeTable(Entry pOEntry, bool pbDiagonalStretch)
		{
			byte[] lyTable = new byte[ShadeTableLength];

			for (int liDistance = 0; liDistance < ShadeTableLength; liDistance++)
			{
				if (liDistance > pOEntry.ViewingDistance)
				{
					lyTable[liDistance] = 15;
					continue;
				}

				// The distance in eighths. With stretching it is doubled first, which after
				// taking the root corresponds to the factor root two - see GetShadeTable.
				int liSquared = (liDistance * 8) * (liDistance * 8);

				int liStretched = UWVanillaMath.Sqrt(pbDiagonalStretch ? liSquared << 1 : liSquared);

				int liShaded = ((liStretched * pOEntry.Shading) / 64) + pOEntry.DistanceBeforeShading;

				if (liShaded < 0)
					liShaded = 0;

				int liLevel = liShaded + pOEntry.StartingLightLevel;

				lyTable[liDistance] = (byte)(liLevel > 14 ? 14 : liLevel);
			}

			return lyTable;
		}

		private static short fRead16(byte[] pyData, int piOffset)
		{
			return (short)(pyData[piOffset] | (pyData[piOffset + 1] << 8));
		}
	}
}
