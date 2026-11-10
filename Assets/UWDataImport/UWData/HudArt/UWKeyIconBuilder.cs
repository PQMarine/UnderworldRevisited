using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE KEY ICON UNDER THE MODERN CROSSHAIR (GitHub issue 30, 2026-11-10, per user: the text "(unlock with the key)"
	/// was too large; every key of the original is named "a_key" anyway). A locked door shows beside its name the
	/// picture of the key in the bags that fits it (the key's own picture cut to the key, Crop), or where none fits
	/// a GENERIC KEY of our own drawing (per user: no key of the game, so its shape tells nothing) with a red X over its
	/// middle, both with a dark outline (variant I of the user's choice) - in palette 0's nearest colours.
	/// </summary>
	public static class UWKeyIconBuilder
	{
		/// <summary>The icon's colour before the palette's nearest is taken: the modern HUD's text (UWModernPalette.HudText).</summary>
		public static readonly UWColor32 InkWanted = new UWColor32(219, 219, 214, 255);

		/// <summary>The stroke's colour before the palette's nearest is taken: a clear red.</summary>
		public static readonly UWColor32 StrokeWanted = new UWColor32(200, 30, 30, 255);

		/// <summary>The outline's colour before the palette's nearest is taken.</summary>
		public static readonly UWColor32 DarkWanted = new UWColor32(0, 0, 0, 255);

		/// <summary>The palette 0 colour nearest to a wanted one - the icon keeps to the game's colours (per user).</summary>
		public static UWColor32 Nearest(UWPalette pOPalette, UWColor32 pOWanted)
		{
			UWColor32 lOColour = pOPalette.GetUWColor(pOPalette.GetNearestIndex(pOWanted.R, pOWanted.G, pOWanted.B));

			lOColour.A = 255;

			return lOColour;
		}

		/// <summary>
		/// The picture cut to its visible pixels and one transparent pixel round them (per user, 2026-11-10: the key fills
		/// only a corner of its 16 x 16 picture and looked "a little small" beside the name). An empty one stays as it is.
		/// </summary>
		public static UWPicture Crop(UWPicture pOPicture)
		{
			int liLeft = pOPicture.Width;
			int liTop = pOPicture.Height;
			int liRight = -1;
			int liBottom = -1;

			for (int liY = 0; liY < pOPicture.Height; liY++)
			{
				for (int liX = 0; liX < pOPicture.Width; liX++)
				{
					if (pOPicture.Get(liX, liY).A == 0)
						continue;

					liLeft = System.Math.Min(liLeft, liX);
					liRight = System.Math.Max(liRight, liX);
					liTop = System.Math.Min(liTop, liY);
					liBottom = System.Math.Max(liBottom, liY);
				}
			}

			if (liRight < 0)
				return pOPicture;

			UWPicture lOOut = new UWPicture(liRight - liLeft + 3, liBottom - liTop + 3);

			for (int liY = liTop; liY <= liBottom; liY++)
			{
				for (int liX = liLeft; liX <= liRight; liX++)
					lOOut.Set(liX - liLeft + 1, liY - liTop + 1, pOPicture.Get(liX, liY));
			}

			return lOOut;
		}

		/// <summary>The generic key: a ring, the shaft and two teeth ('X' the key, '.' nothing).</summary>
		private static readonly string[] msKey =
		{
			".XX...........",
			"X..X..........",
			"X..XXXXXXXXXXX",
			"X..X.......X.X",
			".XX........X.X"
		};

		/// <summary>The picture's size: the key with its outline, as long and as high as the game's keys (per user,
		/// 2026-11-10: 16 x 7 with their own outline), the X on its middle.</summary>
		public const int NoKeyWidth = 16;

		public const int NoKeyHeight = 7;

		/// <summary>The height both pictures are scaled by (UWModernCrosshair): the same whole-pixel scale for each - a key
		/// cut to its picture is about this high, so a fitting key looks as the user approved it and the icon smaller.</summary>
		public const int ScaleHeight = 9;

		/// <summary>
		/// THE NO-KEY ICON: the generic key in pOInk, outlined in pODark, and over its middle a red X (pOStroke) of five
		/// pixels each way, outlined in pODark too.
		/// </summary>
		public static UWPicture BuildNoKey(UWColor32 pOInk, UWColor32 pOStroke, UWColor32 pODark)
		{
			UWPicture lOOut = new UWPicture(NoKeyWidth, NoKeyHeight);
			bool[,] lbKey = new bool[NoKeyWidth, NoKeyHeight];

			for (int liY = 0; liY < msKey.Length; liY++)
			{
				for (int liX = 0; liX < msKey[liY].Length; liX++)
					lbKey[liX + 1, liY + 1] = msKey[liY][liX] == 'X';
			}

			fOutline(lOOut, lbKey, pODark);

			for (int liY = 0; liY < NoKeyHeight; liY++)
			{
				for (int liX = 0; liX < NoKeyWidth; liX++)
				{
					if (lbKey[liX, liY])
						lOOut.Set(liX, liY, pOInk);
				}
			}

			const int CentreX = 8;
			const int CentreY = 3;
			const int Reach = 2;
			bool[,] lbCross = new bool[NoKeyWidth, NoKeyHeight];

			for (int liAt = -Reach; liAt <= Reach; liAt++)
			{
				lbCross[CentreX + liAt, CentreY + liAt] = true;
				lbCross[CentreX + liAt, CentreY - liAt] = true;
			}

			fOutline(lOOut, lbCross, pODark);

			for (int liY = 0; liY < NoKeyHeight; liY++)
			{
				for (int liX = 0; liX < NoKeyWidth; liX++)
				{
					if (lbCross[liX, liY])
						lOOut.Set(liX, liY, pOStroke);
				}
			}

			return lOOut;
		}

		/// <summary>Every pixel next to a set one (eight ways) that is not set itself, in pOColour.</summary>
		private static void fOutline(UWPicture pOOut, bool[,] pbSet, UWColor32 pOColour)
		{
			int liWidth = pbSet.GetLength(0);
			int liHeight = pbSet.GetLength(1);

			for (int liY = 0; liY < liHeight; liY++)
			{
				for (int liX = 0; liX < liWidth; liX++)
				{
					if (!pbSet[liX, liY])
						continue;

					for (int liDy = -1; liDy <= 1; liDy++)
					{
						for (int liDx = -1; liDx <= 1; liDx++)
						{
							int liNx = liX + liDx;
							int liNy = liY + liDy;

							if (liNx >= 0 && liNy >= 0 && liNx < liWidth && liNy < liHeight && !pbSet[liNx, liNy])
								pOOut.Set(liNx, liNy, pOColour);
						}
					}
				}
			}
		}
	}
}
