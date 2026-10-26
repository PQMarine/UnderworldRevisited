using System;
using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE BYTES OF THE RENDERER'S LOOKUP TABLES (cleanup step 6.2, 2026-10-24). Until then
	/// UWShadePalette and UWTextureArrayBuilder built each table straight into a Unity texture;
	/// the contents are the game's rules (LIGHT.DAT, MONO.DAT, SHADES.DAT, XFER.DAT, the palette
	/// rotation, the hallucination's light table), so they are built here without an engine type
	/// (decision 26 of the cleanup plan: nothing that describes the game may hang on Unity), and
	/// UWTextureFactory only uploads them, with each table's format, colour space, filter and mip
	/// setting in one place. The self-check pins every table against what the Unity code built
	/// before the move (LookupTables).
	///
	/// Every method returns the bytes in texture order: row 0 first, x along the row. RGBA tables
	/// hold four bytes per cell (R G B A), index tables one. A null filter means raw colours (no
	/// colour help) - Remastered draws the world in raw colours and puts the colour help on
	/// afterwards, in its post-processing (UWRemasterRenderPath).
	/// </summary>
	public static class UWLookupTables
	{
		/// <summary>Width of the colour and rotation tables - one pixel per palette index.</summary>
		public const int PaletteSize = 256;

		public const int BytesPerColour = 4;

		/// <summary>Brightness steps, i.e. rows of the colour table. Corresponds to the blocks in
		/// LIGHT.DAT.</summary>
		public const int ShadeLevelCount = UWLightLevels.LevelCount;

		/// <summary>Tile distances, i.e. columns of the shade table.</summary>
		public const int DistanceCount = UWShades.ShadeTableLength;

		/// <summary>Player light levels, i.e. rows of the shade table.</summary>
		public const int LightLevelCount = UWShades.EntryCount;

		/// <summary>The palettes of PALS.DAT, the hallucination's choice (RNG &amp; 7).</summary>
		public const int RawPaletteCount = 8;

		/// <summary>Height of the rotation table - least common multiple of both
		/// rotation cycles.</summary>
		public static int RotationStepCount
		{
			get { return UWPaletteRotation.GetCycleLength(true, true); }
		}

		/// <summary>
		/// The colour table: for each palette index the colour it takes on at a given
		/// brightness step. Row 0 is the original colours, row 15 is black. 256 by 16, RGBA.
		/// pbMonochromeUsed tells whether MONO.DAT was there to be used.
		/// </summary>
		public static byte[] ColourTable(DataImport pOData, int piPalette, bool pbMonochrome, UWColourFilter pOFilter,
			out bool pbMonochromeUsed)
		{
			pbMonochromeUsed = false;

			if (pOData == null || pOData.Palettes == null || pOData.LightLevels == null)
				return null;

			// Another palette only for the hallucination's colours (UWHallucinationState).
			UWPalette lOPalette = fGetPalette(pOData, piPalette);

			if (lOPalette == null)
				return null;

			bool lbMono = pbMonochrome && pOData.MiscDataFiles != null && pOData.MiscDataFiles.Monochrome != null
				&& pOData.MiscDataFiles.Monochrome.IsLoaded;

			pbMonochromeUsed = lbMono;

			byte[] lyPixels = new byte[PaletteSize * ShadeLevelCount * BytesPerColour];

			for (int liLevel = 0; liLevel < ShadeLevelCount; liLevel++)
			{
				for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
				{
					// MONO.DAT instead of LIGHT.DAT at light level 5, Night Vision
					// (OpenAndApplyShadesDat_ovr142_0) - see UWShadePalette.MonochromeLightLevel.
					byte lyShaded = lbMono
						? pOData.MiscDataFiles.Monochrome.Remap(liLevel, liIndex)
						: pOData.LightLevels.Remap(liLevel, liIndex);

					// Colour help for colour vision deficiencies - the interface and the palette
					// renderer both read their colours from this table (see UWColourVision).
					fPut(lyPixels, (liLevel * PaletteSize) + liIndex, fShown(lOPalette, lyShaded, pOFilter));
				}
			}

			return lyPixels;
		}

		/// <summary>
		/// The shade table: for each player light level and each whole tile distance
		/// the brightness step. That is SHADES.DAT, computed out - see
		/// UWShades.GetShadeTable. 16 by 8, one byte each.
		///
		/// The shader interpolates between two columns, which is why the step is stored here
		/// raw and not already as a colour.
		/// </summary>
		public static byte[] ShadeTable(DataImport pOData, bool pbDiagonalStretch)
		{
			if (pOData == null || pOData.Shades == null || !pOData.Shades.IsLoaded)
				return null;

			byte[] lyPixels = new byte[DistanceCount * LightLevelCount];

			for (int liLight = 0; liLight < LightLevelCount; liLight++)
			{
				byte[] lyShades = pOData.Shades.GetShadeTable(liLight, pbDiagonalStretch);

				for (int liDistance = 0; liDistance < DistanceCount; liDistance++)
					lyPixels[(liLight * DistanceCount) + liDistance] =
						lyShades != null ? lyShades[liDistance] : (byte)0;
			}

			return lyPixels;
		}

		/// <summary>
		/// The rotation table: for each palette index and each step of the palette rotation the
		/// index currently visible there. Indices outside the lava and
		/// water ranges stand still, their row is the same in every step. 256 by
		/// RotationStepCount, RGBA.
		///
		/// The value is stored in the red channel as index divided by 255. The shader multiplies it
		/// back by 255 and rounds.
		/// </summary>
		public static byte[] RotationTable()
		{
			int liSteps = RotationStepCount;
			byte[] lyPixels = new byte[PaletteSize * liSteps * BytesPerColour];

			for (int liStep = 0; liStep < liSteps; liStep++)
			{
				for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
				{
					byte lySource = (byte)UWPaletteRotation.GetSourceIndex(liIndex, liStep);

					fPut(lyPixels, (liStep * PaletteSize) + liIndex, new UWColor32(lySource, 0, 0, 255));
				}
			}

			return lyPixels;
		}

		/// <summary>
		/// THE TRANSLUCENT COLOURS AS THE ORIGINAL MIXES THEM (audit row 12, 2026-09-27): the
		/// cube that turns a colour of the picture back into its palette index
		/// (UWTransparencyTables.BuildInverseLookup), built from the colours exactly as the
		/// colour table shows them - colour help included, so the picture's own colours are
		/// found again. InverseLookupSize cubed, one byte each.
		/// </summary>
		public static byte[] InverseLookup(DataImport pOData, UWColourFilter pOFilter)
		{
			if (pOData == null || pOData.Palettes == null)
				return null;

			UWPalette lOPalette = pOData.Palettes.GetPalette(0);
			UWColor32[] lOColours = new UWColor32[PaletteSize];

			for (int i = 0; i < PaletteSize; i++)
				lOColours[i] = fShown(lOPalette, i, pOFilter);

			return UWTransparencyTables.BuildInverseLookup(lOColours);
		}

		/// <summary>
		/// For every table of XFER.DAT (rows) and every background index (columns) the colour the
		/// table turns it into - the table and the palette in one step, so the shader needs one
		/// lookup after the inverse one. 256 by UWTransparencyTables.Count, RGBA.
		/// </summary>
		public static byte[] XferColours(DataImport pOData, UWColourFilter pOFilter)
		{
			if (pOData == null || pOData.Palettes == null || pOData.TransparencyTables == null
				|| !pOData.TransparencyTables.IsLoaded)
				return null;

			UWPalette lOPalette = pOData.Palettes.GetPalette(0);
			int liTables = UWTransparencyTables.Count;
			byte[] lyPixels = new byte[PaletteSize * liTables * BytesPerColour];

			for (int liTable = 0; liTable < liTables; liTable++)
			{
				for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
				{
					byte lyResult = pOData.TransparencyTables.Apply((UWTransparencyTables.FadeTable)liTable, liIndex);

					fPut(lyPixels, (liTable * PaletteSize) + liIndex, fShown(lOPalette, lyResult, pOFilter));
				}
			}

			return lyPixels;
		}

		/// <summary>
		/// The hallucination's light table effect (UWHallucinationState.LightTableEffect) as a
		/// colour table like ColourTable, but three times as tall: sixteen levels each for
		/// everything else, floors and walls (UWPaletteLookup's UWSurfacePart picks the part). For
		/// every index and level the colour of the index UWHallucinationState.BuildLightTableEffect
		/// maps it to - bands of green, grey and purple by distance (an approximation after the
		/// user's screenshots of the original). 256 wide, RGBA; the rows are the length / 1024.
		/// </summary>
		public static byte[] LightTableEffect(DataImport pOData, UWColourFilter pOFilter)
		{
			if (pOData == null || pOData.Palettes == null)
				return null;

			byte[] lyTable = UWHallucinationState.BuildLightTableEffect();
			int liRows = lyTable.Length / PaletteSize;

			UWPalette lOPalette = pOData.Palettes.GetPalette(0);
			byte[] lyPixels = new byte[PaletteSize * liRows * BytesPerColour];

			for (int liLevel = 0; liLevel < liRows; liLevel++)
			{
				for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
					fPut(lyPixels, (liLevel * PaletteSize) + liIndex,
						fShown(lOPalette, lyTable[(liLevel * PaletteSize) + liIndex], pOFilter));
			}

			return lyPixels;
		}

		/// <summary>
		/// The eight palettes as they are, one row each (256 by 8, RGBA), for Remastered while the
		/// hallucination swaps the palette (UWHallucinationState.PaletteEffect): its surfaces know
		/// their palette index (the index array, the index atlases, the model faces' vertex) and
		/// take the colour of that index from the row of the hallucination's palette, then light it
		/// as usual. Raw colours - Remastered puts the colour help on in its post-processing.
		/// </summary>
		public static byte[] RawPalettes(DataImport pOData)
		{
			if (pOData == null || pOData.Palettes == null)
				return null;

			byte[] lyPixels = new byte[PaletteSize * RawPaletteCount * BytesPerColour];

			for (int liPalette = 0; liPalette < RawPaletteCount; liPalette++)
			{
				UWPalette lOPalette = fGetPalette(pOData, liPalette);

				for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
					fPut(lyPixels, (liPalette * PaletteSize) + liIndex, fShown(lOPalette, liIndex, null));
			}

			return lyPixels;
		}

		/// <summary>The grime's colour tables (UWGrimeTint), one row per tone: 256 by
		/// UWGrimeTint.ToneCount, one byte each - the palette index a grimy pixel takes.</summary>
		public static byte[] GrimeTint(UWPalette pOPalette)
		{
			byte[] lyAll = new byte[UWGrimeTint.PaletteSize * UWGrimeTint.ToneCount];

			for (int liRow = 0; liRow < UWGrimeTint.ToneCount; liRow++)
				Array.Copy(UWGrimeTint.Build(pOPalette, (UWGrimeTint.ToneEnum)liRow), 0, lyAll, liRow * UWGrimeTint.PaletteSize,
					UWGrimeTint.PaletteSize);

			return lyAll;
		}

		/// <summary>
		/// The surface table for the "Remastered" render mode: for each of the 256
		/// palette indices, what kind of material it is (256 by 1, RGBA; until 2026-10-24
		/// UWTextureArrayBuilder.BuildMaterialLookup).
		///
		/// WHY VIA THE PALETTE: the original has no material data, but its palette
		/// is ordered. Two ranges are assigned and treated specially by the game itself,
		/// because it ROTATES them (see UWPaletteRotation): 16 to 23 is lava, 48 to
		/// 63 is water. Whatever is painted in these colours in a texture IS lava or
		/// water - regardless of which texture it appears in.
		///
		/// This yields per index:
		///   R = gloss factor applied to the gloss strength (0.5 is neutral). Water reflects,
		///       lava barely, stone lies in between.
		///   G = emission. Only lava glows; that way it glimmers even without a light source
		///       and drives the bloom.
		///   B = metal. NOT BUILT - there is no assigned range for metal in the palette,
		///       and guessing would be wrong here. Stays zero.
		///
		/// A table of 256 entries instead of a third texture per slice: the index is
		/// already stored pixel by pixel in the index array (UWTextureArrayBuilder.BuildIndexed), and so
		/// the whole stage costs 1 KB instead of several megabytes.
		/// </summary>
		public static byte[] MaterialLookup()
		{
			byte[] lyPixels = new byte[PaletteSize * BytesPerColour];

			for (int liIndex = 0; liIndex < PaletteSize; liIndex++)
			{
				byte lyGloss = 128;
				byte lyGlow = 0;

				if (UWPaletteRotation.IsWaterIndex(liIndex))
					lyGloss = 255;
				else if (UWPaletteRotation.IsLavaIndex(liIndex))
				{
					lyGloss = 40;
					lyGlow = 255;
				}

				fPut(lyPixels, liIndex, new UWColor32(lyGloss, lyGlow, 0, 255));
			}

			return lyPixels;
		}

		/// <summary>A palette of PALS.DAT, palette 0 where the index is not there.</summary>
		private static UWPalette fGetPalette(DataImport pOData, int piPalette)
		{
			try
			{
				return pOData.Palettes.GetPalette(piPalette);
			}
			catch (Exception)
			{
				return pOData.Palettes.GetPalette(0);
			}
		}

		/// <summary>A palette colour as the colour table shows it (colour help applied when a
		/// filter is given and on), or raw; always opaque.</summary>
		private static UWColor32 fShown(UWPalette pOPalette, int piIndex, UWColourFilter pOFilter)
		{
			UWColor32 lOColour = pOPalette.GetUWColor(piIndex);
			byte lyRed = lOColour.R;
			byte lyGreen = lOColour.G;
			byte lyBlue = lOColour.B;

			if (pOFilter != null && pOFilter.IsActive)
				pOFilter.Apply(ref lyRed, ref lyGreen, ref lyBlue);

			return new UWColor32(lyRed, lyGreen, lyBlue, 255);
		}

		private static void fPut(byte[] pyPixels, int piCell, UWColor32 pOColour)
		{
			int liByte = piCell * BytesPerColour;

			pyPixels[liByte] = pOColour.R;
			pyPixels[liByte + 1] = pOColour.G;
			pyPixels[liByte + 2] = pOColour.B;
			pyPixels[liByte + 3] = pOColour.A;
		}
	}
}
