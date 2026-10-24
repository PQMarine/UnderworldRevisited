using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE MAP SHOWS (cleanup step 5.11, 2026-10-19): the discovered tiles of a level as
	/// UW.EXE paints them (UWAutomapPainter) in the parchment's palette, the player's mark, and
	/// where the tiles and the character sit on the 320x200 map screen. ONE source for the
	/// classic map (UWHudMap, also the modern scheme's big map), the help window's map tab and
	/// the modern minimap: until then the minimap and the help window asked the classic map class
	/// for its pictures (UWHudMap.BuildMinimapTiles and its fellows), and the tile constants stood
	/// in two places. Decision 26 of the cleanup plan: the picture is made here, without an engine
	/// type; the Unity side (UWAutomapPictures) only uploads the bytes.
	///
	/// The buffer is RGBA bytes, BlockSize by BlockSize, the BOTTOM row first - as a texture
	/// upload wants it and as the tiles' rows count (row 0 is the south edge) - and 0,0,0,0 where
	/// the parchment stays as it is.
	/// </summary>
	public static class UWAutomapView
	{
		/// <summary>Three pixels a tile (UWClassicLayout.Map, ovr092_48B).</summary>
		public const int TileSize = UWClassicLayout.Map.TileSize;

		public const int TilesPerAxis = UWWorldScale.TilesPerAxis;

		/// <summary>The side of the tiles' block in pixels.</summary>
		public const int BlockSize = TilesPerAxis * TileSize;

		/// <summary>The block's top-left corner on the 320x200 map screen, y from the top.</summary>
		public const int BlockLeft = UWClassicLayout.Map.CellLeft;

		public const int BlockTop = UWClassicLayout.Map.TilesTop;

		public const int BufferLength = BlockSize * BlockSize * UWUploadPixels.BytesPerPixel;

		/// <summary>
		/// ROLLED ONCE (per user, 2026-09-26): the original rolls each pixel's small colour step anew
		/// on every drawing, but the help window's map tab redraws the tiles every half second, and a
		/// fresh roll each time made the parchment shimmer. So each random value is a hash of the
		/// pixel, the level, what it is drawn for and a seed taken once per run: random-looking, but
		/// the same on every redraw and every opening of the map. The drawing itself is
		/// UWAutomapPainter's, after UW.EXE (since 2026-09-26). Moved from UWHudMap.fMapRandom.
		/// </summary>
		public static int Random(uint puSeed, int piLevelIndex, int piX, int piY, UWAutomapPainter.RandomUse peUse)
		{
			unchecked
			{
				uint luHash = puSeed ^ ((uint)(piLevelIndex + 1) * 83492791u) ^ ((uint)piX * 73856093u)
					^ ((uint)piY * 19349663u) ^ ((uint)((int)peUse + 1) * 2246822519u);

				luHash ^= luHash >> 13;
				luHash *= 0x5BD1E995u;
				luHash ^= luHash >> 15;

				return (int)(luHash & 0x7FFF);
			}
		}

		/// <summary>The run's seed for Random, from a tick count taken once.</summary>
		public static uint SeedFrom(int piTickCount)
		{
			return unchecked((uint)piTickCount * 2654435761u);
		}

		/// <summary>
		/// The level's map into pyRgba (cleared first): the discovered tiles of piLevelIndex and - when
		/// pbMark - the character's mark on tile piMarkX/piMarkY. The caller decides pbMark: only on
		/// the level the character really stands on, and the modern minimap leaves it out, its arrow
		/// says it (per user, 2026-10-04). Levels 10 to 99 have no automap, only notes - the
		/// parchment then stays empty (the mark is still drawn when asked for).
		/// </summary>
		public static void Compose(byte[] pyRgba, DataImport pOData, int piLevelIndex, uint puSeed, UWColourFilter pOFilter,
			bool pbMark, int piMarkX, int piMarkY)
		{
			Array.Clear(pyRgba, 0, pyRgba.Length);

			UWLevel lOLevel = pOData != null && pOData.Levels != null
				&& piLevelIndex >= 0 && piLevelIndex < pOData.Levels.Count
				? pOData.Levels[piLevelIndex] : null;

			UWTexture lOParchment;

			if (lOLevel != null && lOLevel.AutomapTiles != null
				&& pOData.Textures.BitmapFiles.TryGetValue(UWTexture.TextureTypes.BLNKMAP, out lOParchment)
				&& lOParchment != null && lOParchment.PaletteIndices != null
				&& lOParchment.PaletteIndices.Length >= UWAutomapPainter.ScreenWidth * UWAutomapPainter.ScreenHeight)
			{
				PaintTiles(pyRgba, lOParchment.PaletteIndices, lOParchment.Palettes.GetPalette(lOParchment.MainPaletteIndex),
					lOLevel.AutomapTiles, (piX, piY, peUse) => Random(puSeed, piLevelIndex, piX, piY, peUse), pOFilter);
			}

			if (!pbMark)
				return;

			if (!DrawPlayerMark(pyRgba, piMarkX, piMarkY, pOData))
				DrawHandMadeMark(pyRgba, piMarkX, piMarkY);
		}

		/// <summary>
		/// The discovered tiles as pixels.
		///
		/// SINCE 2026-09-26 PAINTED AS UW.EXE PAINTS IT (UWAutomapPainter, after ovr092): on the
		/// parchment's palette indices, each pixel the parchment plus a small random step, borders
		/// only outside the sides whose neighbour is not a discovered tile, a diagonal bordered on its
		/// open half's two sides only. Until then the look came from the reference's fixed colour
		/// tones plus rules tuned by eye (a floor overhang into undiscovered neighbours, lines on a
		/// diagonal's solid half towards open neighbours) - the latter made the "knob" where two
		/// diagonals meet (per user, 2026-09-26).
		///
		/// A pixel the painting left at the parchment's own index stays clear, so the parchment
		/// shows through; every other one takes its palette colour through the colour help, opaque.
		/// </summary>
		public static void PaintTiles(byte[] pyRgba, byte[] pyParchment, UWPalette pOPalette, byte[] pyAutomap,
			Func<int, int, UWAutomapPainter.RandomUse, int> pFRandom, UWColourFilter pOFilter)
		{
			byte[] lyScreen = (byte[])pyParchment.Clone();

			UWAutomapPainter.Paint(lyScreen, pyAutomap, pFRandom);

			bool lbFilter = pOFilter != null && pOFilter.IsActive;

			for (int liY = 0; liY < BlockSize; liY++)
			{
				// The painter's screen is stored from the top, its y counts from the bottom.
				int liRow = (UWAutomapPainter.ScreenHeight - 1 - (liY + UWAutomapPainter.CellBottom)) * UWAutomapPainter.ScreenWidth;

				for (int liX = 0; liX < BlockSize; liX++)
				{
					int liAt = liRow + liX + UWAutomapPainter.CellLeft;
					byte lyIndex = lyScreen[liAt];

					if (lyIndex == pyParchment[liAt])
						continue;

					UWColor32 lOColour = pOPalette.GetUWColor(lyIndex);
					byte lyRed = lOColour.R;
					byte lyGreen = lOColour.G;
					byte lyBlue = lOColour.B;

					if (lbFilter)
						pOFilter.Apply(ref lyRed, ref lyGreen, ref lyBlue);

					fSetPixel(pyRgba, liX, liY, lyRed, lyGreen, lyBlue);
				}
			}
		}

		/// <summary>
		/// THE ORIGINAL'S OWN PICTURE (read 2026-09-30, per user: "the shadow of the cross is not
		/// right yet"): ovr092_F66 draws the mark through seg009_265 with graphic 0x103F, which is
		/// BUTTONS.GR image 0x3F (0x1000 + n counts from the file after ANIMO.GR, loaded at
		/// ovr115_8B3). It is 8 by 5: a small gold dagger in the first three columns - point
		/// at the top, a guard of three shades of gold, grey blade below - and its dark blue
		/// shadow falling to the right. Index 0 is clear. The dagger's column sits on the tile's
		/// middle column and its lowest pixel on the tile's lowest row, where the hand-drawn mark
		/// had them (per user, 2026-09-03); the picture replaces its colours and shadow.
		///
		/// False when the picture or the palette is missing (then DrawHandMadeMark).
		/// </summary>
		public static bool DrawPlayerMark(byte[] pyRgba, int piTileX, int piTileY, DataImport pOData)
		{
			UWTexture lOMark = pOData != null && pOData.Textures != null
				? pOData.Textures.GetTextureByType(UWTexture.TextureTypes.BUTTONS, UWClassicLayout.Map.PlayerMarkImage)
				: null;

			if (lOMark == null || lOMark.Width <= 0 || lOMark.Height <= 0)
				return false;

			byte[] laIndices = lOMark.GetMainPaletteIndices();

			if (laIndices == null || laIndices.Length < lOMark.Width * lOMark.Height || pOData.Palettes == null)
				return false;

			// IN THE MAP'S PALETTE, not the game's (per user, 2026-09-30: the shadow is grey-black in
			// the original, ours dark blue): ovr092_F66 switches to palette 1 for the whole map
			// screen, the parchment's palette, and the same indices are brown and dull gold there.
			UWPalette lOPalette = pOData.Palettes.GetPalette(UWClassicLayout.Map.Palette);

			if (lOPalette == null)
				return false;

			DrawMarkPicture(pyRgba, piTileX, piTileY, laIndices, lOMark.Width, lOMark.Height, lOPalette);

			return true;
		}

		/// <summary>The mark's picture (palette indices, rows top down, 0 clear) on the tile, in
		/// the palette's colours as they are - the colour help is not applied to it.</summary>
		public static void DrawMarkPicture(byte[] pyRgba, int piTileX, int piTileY, byte[] pyIndices, int piWidth, int piHeight,
			UWPalette pOPalette)
		{
			int liLeft = (piTileX * TileSize) + (TileSize / 2) - UWClassicLayout.Map.PlayerMarkDaggerColumn;
			int liBottom = piTileY * TileSize;

			for (int liRow = 0; liRow < piHeight; liRow++)
			{
				for (int liColumn = 0; liColumn < piWidth; liColumn++)
				{
					int liAt = (liRow * piWidth) + liColumn;

					if (pyIndices[liAt] == 0)
						continue;

					byte[] laRgb = pOPalette.GetRGB(pyIndices[liAt]);

					// The picture's first row is its top; the map counts from the bottom.
					fSetPixel(pyRgba, liLeft + liColumn, liBottom + (piHeight - 1 - liRow), laRgb[0], laRgb[1], laRgb[2]);
				}
			}
		}

		/// <summary>
		/// The marker on the own tile when BUTTONS.GR does not hold the original's picture.
		///
		/// A small gold cross with a dark shadow and two grey pixels below it (a small dagger),
		/// drawn by hand after the original (per user, 2026-09-03). Which graphic the original
		/// uses for it was still open then - the reference hooks in a hand-assigned picture there
		/// that cannot be derived from the game data (answered 2026-09-30, see DrawPlayerMark).
		/// </summary>
		public static void DrawHandMadeMark(byte[] pyRgba, int piTileX, int piTileY)
		{
			// Gold with a dark shadow offset by one pixel - that is how it looks in the original
			// (per user with cutout, 2026-09-03).
			UWColor32 lOMark = new UWColor32(230, 180, 60, 255);
			UWColor32 lOShadow = new UWColor32(40, 25, 15, 255);

			// Below the golden part sit two grey pixels, light and a bit darker - which gives the
			// impression of a small dagger (per user, 2026-09-03).
			UWColor32 lOLightGrey = new UWColor32(200, 200, 205, 255);
			UWColor32 lODarkGrey = new UWColor32(140, 140, 145, 255);

			int liLeft = piTileX * TileSize;
			int liMiddle = TileSize / 2;

			// The two grey pixels attach at the bottom - so that the marker still sits on its
			// tile, the golden part and its shadow move up by two pixels
			// (per user, 2026-09-03).
			int liBottom = (piTileY * TileSize) + 2;

			// The shadow lies offset one pixel to the right and down and reaches as far
			// as the marker itself, so down below the grey pixels.
			for (int liAt = 0; liAt < TileSize; liAt++)
			{
				fSetPixel(pyRgba, liLeft + liAt + 1, liBottom + liMiddle - 1, lOShadow);
				fSetPixel(pyRgba, liLeft + liMiddle + 1, liBottom + liAt - 1, lOShadow);
			}

			fSetPixel(pyRgba, liLeft + liMiddle + 1, liBottom - 2, lOShadow);
			fSetPixel(pyRgba, liLeft + liMiddle + 1, liBottom - 3, lOShadow);

			for (int liAt = 0; liAt < TileSize; liAt++)
			{
				fSetPixel(pyRgba, liLeft + liAt, liBottom + liMiddle, lOMark);
				fSetPixel(pyRgba, liLeft + liMiddle, liBottom + liAt, lOMark);
			}

			fSetPixel(pyRgba, liLeft + liMiddle, liBottom - 1, lOLightGrey);
			fSetPixel(pyRgba, liLeft + liMiddle, liBottom - 2, lODarkGrey);
		}

		/// <summary>The middle of tile piTileX/piTileY on the 320x200 map screen, y from the TOP
		/// (the tiles' rows count from the bottom, row 0 is the south edge).</summary>
		public static void PlayerPixel(int piTileX, int piTileY, out float pfX, out float pfY)
		{
			pfX = BlockLeft + (piTileX * TileSize) + (TileSize / 2f);
			pfY = BlockTop + ((TilesPerAxis - piTileY) * TileSize) - (TileSize / 2f);
		}

		/// <summary>Sets a pixel, provided it lies on the block. The player mark can reach
		/// beyond the tile edge and fall out at the map edge.</summary>
		private static void fSetPixel(byte[] pyRgba, int piX, int piY, byte pyRed, byte pyGreen, byte pyBlue)
		{
			if (piX < 0 || piY < 0 || piX >= BlockSize || piY >= BlockSize)
				return;

			int liAt = ((piY * BlockSize) + piX) * UWUploadPixels.BytesPerPixel;

			pyRgba[liAt] = pyRed;
			pyRgba[liAt + 1] = pyGreen;
			pyRgba[liAt + 2] = pyBlue;
			pyRgba[liAt + 3] = 255;
		}

		private static void fSetPixel(byte[] pyRgba, int piX, int piY, UWColor32 pOColour)
		{
			fSetPixel(pyRgba, piX, piY, pOColour.R, pOColour.G, pOColour.B);
		}
	}
}
