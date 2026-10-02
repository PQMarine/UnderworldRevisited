namespace UWDataImport.UWData
{
	/// <summary>
	/// What the player sees while hallucinating (PLAYER.DAT 0x61 bits 2-3 above zero). READ
	/// 2026-09-25 (ovr133_299, whole, called from the status update): while the counter runs and
	/// no effect is on, ONE of three effects is rolled (RNG % 3) and stays until the counter is
	/// back at zero, when it is undone. All three SEEN in the original (per user with
	/// screenshots, 2026-09-27) and built:
	///
	///   0  seg031_99(1) writes six random values into the texture mapper's table in seg051 -
	///      the masks with which it forms a texel index from the row (times the width, with the
	///      row's fraction in the low bits) and the column: normally 0xF0, 0x3E0 and 0xFC0 for
	///      16, 32 and 64 pixel textures, two of each; rolled as RNG &amp; 0xFF, 0x3FF and 0xFFF.
	///      The walls turn into swirling moire bands of their own colours, a new pattern on every
	///      mushroom. Built in the palette renderer (UWDungeonPalette, ScrambleMasks).
	///   1  a random one of the eight palettes (RNG &amp; 7), palette 0 again afterwards - on the
	///      whole screen, the panels too.
	///   2  ovr142_181 copies 0x1000 bytes of UW.EXE's segment 51 - the 3D engine's working
	///      variables, not a table - over the light table: bands of wrong colours by distance, an
	///      APPROXIMATION after the user's screenshots (BuildLightTableEffect).
	///
	/// The effect is not saved: after loading, the next update rolls anew, as the original's
	/// status update after a load does.
	/// </summary>
	public sealed class UWHallucinationState
	{
		public const int NoEffect = -1;

		public const int ScrambleEffect = 0;

		public const int PaletteEffect = 1;

		public const int LightTableEffect = 2;

		private const int EffectCount = 3;

		private const int PaletteCount = 8;

		/// <summary>The mapper's normal masks: the row bits of a texel index for 16, 32 and 64
		/// pixel textures (texel index = row * width + column).</summary>
		public const int NormalMask16 = 0xF0;

		public const int NormalMask32 = 0x3E0;

		public const int NormalMask64 = 0xFC0;

		public int Effect { get; private set; } = NoEffect;

		/// <summary>The palette of PaletteEffect.</summary>
		public int Palette { get; private set; }

		/// <summary>
		/// ScrambleEffect's six masks in the order seg031_99 writes them: two for 16 pixel
		/// textures (&amp; 0xFF), two for 32 (&amp; 0x3FF), two for 64 (&amp; 0xFFF). The normal
		/// masks while the effect is off. Which of a pair serves which surface was not read -
		/// ours gives the first to the walls and the second to floors and ceilings (GUESS).
		/// </summary>
		public int[] ScrambleMasks { get; } =
			{ NormalMask16, NormalMask16, NormalMask32, NormalMask32, NormalMask64, NormalMask64 };

		/// <summary>Follows the counter; true when the effect changed.</summary>
		public bool Update(int piCounter)
		{
			if (piCounter > 0)
			{
				if (Effect != NoEffect)
					return false;

				Effect = UWRandom.Next(EffectCount);
				Palette = Effect == PaletteEffect ? UWRandom.Next(PaletteCount) : 0;

				if (Effect == ScrambleEffect)
					fRollMasks();

				return true;
			}

			if (Effect == NoEffect)
				return false;

			Effect = NoEffect;
			Palette = 0;
			fResetMasks();

			return true;
		}

		private void fRollMasks()
		{
			ScrambleMasks[0] = UWRandom.Next(0x8000) & 0xFF;
			ScrambleMasks[1] = UWRandom.Next(0x8000) & 0xFF;
			ScrambleMasks[2] = UWRandom.Next(0x8000) & 0x3FF;
			ScrambleMasks[3] = UWRandom.Next(0x8000) & 0x3FF;
			ScrambleMasks[4] = UWRandom.Next(0x8000) & 0xFFF;
			ScrambleMasks[5] = UWRandom.Next(0x8000) & 0xFFF;
		}

		private void fResetMasks()
		{
			ScrambleMasks[0] = NormalMask16;
			ScrambleMasks[1] = NormalMask16;
			ScrambleMasks[2] = NormalMask32;
			ScrambleMasks[3] = NormalMask32;
			ScrambleMasks[4] = NormalMask64;
			ScrambleMasks[5] = NormalMask64;
		}

		/// <summary>
		/// The texel a scrambled mapper reads, for a texture of piSize pixels: the row position
		/// (piRowTimesSize = row * size, its fraction in the low bits) masked, the column added,
		/// wrapped to the texture. With the normal mask it is row * size + column again. The
		/// shader does the same (UWDungeonPalette); this is its reference for the self-check.
		/// </summary>
		public static int ScrambledTexel(int piRowTimesSize, int piColumn, int piMask, int piSize)
		{
			return ((piRowTimesSize & piMask) + piColumn) & ((piSize * piSize) - 1);
		}

		/// <summary>Shade levels per surface kind (LIGHT.DAT's sixteen).</summary>
		public const int LevelCount = 16;

		/// <summary>The effect table's parts, sixteen levels of 256 entries each: everything else
		/// (sprites, models, ceilings), floors, walls - see BuildLightTableEffect.</summary>
		public const int OtherPart = 0;

		public const int FloorPart = 1;

		public const int WallPart = 2;

		private const int PartCount = 3;

		public const int LightTableSize = PartCount * LevelCount * 256;

		/// <summary>The bands of BuildLightTableEffect, as shade levels (inclusive).</summary>
		public const int GreenLastLevel = 3;

		public const int WallGreyFirstLevel = 9;

		public const int WallGreyLastLevel = 11;

		public const int FloorGreyFirstLevel = 12;

		public const int FloorGreyLastLevel = 13;

		public const int PurpleLevel = 14;

		/// <summary>The grey band's range: the file's row 2 sends indices 98 to 225 to 255.</summary>
		private const int GreyFirstIndex = 98;

		private const int GreyLastIndex = 225;

		private const int GreyIndex = 255;

		private const int PurpleIndex = 252;

		private const int GreenIndex = 210;

		private const int PaleGreenIndex = 10;

		/// <summary>One index in so many shows green in front, purple further off.</summary>
		private const int GreenShare = 10;

		private const int PurpleShare = 6;

		/// <summary>
		/// LightTableEffect, APPROXIMATED AFTER THE USER'S SCREENSHOTS (2026-09-27, per user "way 2,
		/// the approximation after my pictures"). ovr142_181 copies 0x1000 bytes out of UW.EXE's
		/// segment 51 over the active light table and zeroes entries 0 and 1 of every level - no
		/// table but the 3D engine's working variables, most of whose run-time values cannot be
		/// taken from the file (seg051:0000 as the file holds it is zero from row 3 on; the other
		/// reading, seg051:28A0, colours every row of every texture and matches nothing seen).
		///
		/// Seen in the original on SAVE3's spot (floor texture 7, wall texture 66), per user with
		/// screenshots: with the lantern a sparse scatter of green dots on the floor right in front,
		/// behind it black, colours again in the twilight at the far end of the light; without
		/// light grey dots on floor and walls at some distance, sparse purple dots on walls further
		/// off, everything else black. The grey pattern is the file's row 2 (indices 98 to 225 to
		/// 255, 54 percent of floor 7, 35 percent of wall 66); neither the green nor the purple
		/// comes out of any file row for these textures. The bands were then placed by the user
		/// against our build, level by level.
		///
		/// So, in shade levels (SHADES.DAT: without light the own tile runs from 5 to 9 and the
		/// next from 9 to 14, then 14 and 15; with the lantern 0 to 3, 3 to 7, 7 to 10, 10 to 14):
		/// 0 to 3 a green scatter on floors and on the rest (one index in ten, 210 or 10), walls
		/// black; 4 to 8 all black; 9 to 11 grey on WALLS; 12 and 13 grey on FLOORS (both 98-225
		/// to 255); 14 a purple scatter on WALLS (one index in six, 252); everything else black. The
		/// original's table is one per index for all surfaces; the split by surface is ours, to give
		/// floors and walls what was seen on them.
		/// </summary>
		public static byte[] BuildLightTableEffect()
		{
			byte[] lyTable = new byte[LightTableSize];

			for (int liRow = 0; liRow < PartCount * LevelCount; liRow++)
			{
				int liLevel = liRow % LevelCount;
				int liPart = liRow / LevelCount;
				bool lbGrey = liPart == WallPart
					? liLevel >= WallGreyFirstLevel && liLevel <= WallGreyLastLevel
					: liPart == FloorPart && liLevel >= FloorGreyFirstLevel && liLevel <= FloorGreyLastLevel;

				for (int liIndex = 2; liIndex < 256; liIndex++)
				{
					int liHash = (liIndex * 37) + (liLevel * 101);
					int liShown = 0;

					if (liLevel <= GreenLastLevel && liPart != WallPart)
						liShown = liHash % GreenShare == 0 ? (liIndex % 2 == 0 ? GreenIndex : PaleGreenIndex) : 0;
					else if (lbGrey)
						liShown = liIndex >= GreyFirstIndex && liIndex <= GreyLastIndex ? GreyIndex : 0;
					else if (liPart == WallPart && liLevel == PurpleLevel)
						liShown = liHash % PurpleShare == 0 ? PurpleIndex : 0;

					lyTable[(liRow * 256) + liIndex] = (byte)liShown;
				}
			}

			return lyTable;
		}
	}
}
