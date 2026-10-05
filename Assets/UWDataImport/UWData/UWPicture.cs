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
