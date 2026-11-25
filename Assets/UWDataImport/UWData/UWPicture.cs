using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A picture as the files hold it: rows top down, palette colours, the transparent ones with
	/// alpha 0. The modern HUD composes its frames in it (UWHudArt) and the engine turns the
	/// result into its own texture at the end (UWModernHudArt).
	/// </summary>
	public sealed class UWPicture
	{
		public readonly int Width;

		public readonly int Height;

		public readonly UWColor32[] Pixels;

		public UWPicture(int piWidth, int piHeight)
		{
			Width = piWidth;
			Height = piHeight;
			Pixels = new UWColor32[piWidth * piHeight];
		}

		public static UWPicture From(UWTexture pOTexture)
		{
			UWPicture lOPicture = new UWPicture(pOTexture.Width, pOTexture.Height);
			UWColor32[] lyColours = pOTexture.GetUWColor32();

			Array.Copy(lyColours, lOPicture.Pixels, Math.Min(lyColours.Length, lOPicture.Pixels.Length));

			return lOPicture;
		}

		/// <summary>
		/// A picture with its OWN clear colour (UWTexture.ZeroIsClear, the second game's objects, spells and armour): colour
		/// 0 clear, every other pixel opaque - the first game's palette rule behind From takes the second game's near black
		/// (0, 0, 4) for clear and leaves colour 0 as it is (UW2 plan U4b step 2, the armour's outline). Any other picture
		/// as From gives it. For the crosshair's key (part U4e step 2).
		/// </summary>
		public static UWPicture FromOwnClear(UWTexture pOTexture)
		{
			return pOTexture.ZeroIsClear ? FromZeroClear(pOTexture) : From(pOTexture);
		}

		/// <summary>
		/// A picture with every pixel opaque in its colour: the second game's panel pages and the open bag's back, which
		/// its classic page draws without a clear colour (UWGameScreenUw2.fPicture's pbTransparent false). Through From
		/// the first game's palette rule would take the second game's near black (0, 0, 4) - the pages' frames and the
		/// rune tablet's clear button - for clear (per user at the test of 197, 2026-11-23: the button's frame showed
		/// through).
		/// </summary>
		public static UWPicture FromOpaque(UWTexture pOTexture)
		{
			UWPicture lOPicture = From(pOTexture);

			for (int liAt = 0; liAt < lOPicture.Pixels.Length; liAt++)
				lOPicture.Pixels[liAt].A = 255;

			return lOPicture;
		}

		/// <summary>
		/// A picture clear in its own colour 0 alone, every other pixel opaque, whatever kind it is: the second game's
		/// buttons, which its classic page draws so (UWGameScreenUw2.fCachedPicture) - the arrows of the open bag on the
		/// modern character page (UW2 plan stage U5 step 2). Without indices as From gives it.
		/// </summary>
		public static UWPicture FromZeroClear(UWTexture pOTexture)
		{
			UWPicture lOPicture = From(pOTexture);

			if (pOTexture.PaletteIndices == null)
				return lOPicture;

			int liCount = Math.Min(pOTexture.PaletteIndices.Length, lOPicture.Pixels.Length);

			for (int liAt = 0; liAt < liCount; liAt++)
				lOPicture.Pixels[liAt].A = pOTexture.PaletteIndices[liAt] == 0 ? (byte)0 : (byte)255;

			return lOPicture;
		}

		public UWColor32 Get(int piX, int piY)
		{
			return Pixels[(piY * Width) + piX];
		}

		public void Set(int piX, int piY, UWColor32 pOColour)
		{
			Pixels[(piY * Width) + piX] = pOColour;
		}

		public UWPicture Crop(UWRectInt pORect)
		{
			UWPicture lOOut = new UWPicture(pORect.Width, pORect.Height);

			for (int y = 0; y < pORect.Height; y++)
			{
				for (int x = 0; x < pORect.Width; x++)
					lOOut.Set(x, y, Get(pORect.X + x, pORect.Y + y));
			}

			return lOOut;
		}

		/// <summary>Copies every pixel, the transparent ones too.</summary>
		public void Paste(UWPicture pOSource, int piX, int piY)
		{
			for (int y = 0; y < pOSource.Height; y++)
			{
				for (int x = 0; x < pOSource.Width; x++)
				{
					int liX = piX + x;
					int liY = piY + y;

					if (liX >= 0 && liY >= 0 && liX < Width && liY < Height)
						Set(liX, liY, pOSource.Get(x, y));
				}
			}
		}

		/// <summary>Copies only the visible pixels.</summary>
		public void Overlay(UWPicture pOSource, int piX, int piY)
		{
			for (int y = 0; y < pOSource.Height; y++)
			{
				for (int x = 0; x < pOSource.Width; x++)
				{
					UWColor32 lOColour = pOSource.Get(x, y);
					int liX = piX + x;
					int liY = piY + y;

					if (lOColour.A != 0 && liX >= 0 && liY >= 0 && liX < Width && liY < Height)
						Set(liX, liY, lOColour);
				}
			}
		}
	}
}
