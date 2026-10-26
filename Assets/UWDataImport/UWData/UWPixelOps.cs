using System;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE PIXEL CANVAS' TOOLS beside UWPicture's Crop, Paste and Overlay (cleanup 9.9, 2026-10-25,
	/// CLEANUP-PLAN 8.4): filling, clearing by a mask, forcing opaque - the frame pictures' black is
	/// palette index 0 and comes transparent, so most pieces make it opaque again -, the mirrored
	/// tiling, padding and the outline every freed piece may get. The HUD art builders wrote each of
	/// these out where they needed it (eight opaque-forcing loops, six outlines).
	/// </summary>
	public static class UWPixelOps
	{
		/// <summary>Transparent (all channels 0).</summary>
		public static readonly UWColor32 Transparent = new UWColor32(0, 0, 0, 0);

		/// <summary>Opaque black, the outlines' colour.</summary>
		public static readonly UWColor32 Black = new UWColor32(0, 0, 0, 255);

		/// <summary>The colour fully opaque.</summary>
		public static UWColor32 Opaque(UWColor32 pOColour)
		{
			return new UWColor32(pOColour.R, pOColour.G, pOColour.B, 255);
		}

		/// <summary>A copy of the whole picture.</summary>
		public static UWPicture Copy(UWPicture pOPicture)
		{
			return pOPicture.Crop(new UWRectInt(0, 0, pOPicture.Width, pOPicture.Height));
		}

		/// <summary>Every pixel the colour.</summary>
		public static void Fill(UWPicture pOPicture, UWColor32 pOColour)
		{
			for (int liAt = 0; liAt < pOPicture.Pixels.Length; liAt++)
				pOPicture.Pixels[liAt] = pOColour;
		}

		/// <summary>The pixels of a rectangle the colour (it must lie inside the picture).</summary>
		public static void Fill(UWPicture pOPicture, UWRectInt pORect, UWColor32 pOColour)
		{
			for (int y = pORect.Y; y < pORect.Y + pORect.Height; y++)
			{
				for (int x = pORect.X; x < pORect.X + pORect.Width; x++)
					pOPicture.Set(x, y, pOColour);
			}
		}

		/// <summary>The mask's pixels transparent.</summary>
		public static void Clear(UWPicture pOPicture, UWMask pOMask)
		{
			for (int liAt = 0; liAt < pOPicture.Pixels.Length; liAt++)
			{
				if (pOMask.Bits[liAt])
					pOPicture.Pixels[liAt] = Transparent;
			}
		}

		/// <summary>Every pixel opaque.</summary>
		public static void MakeOpaque(UWPicture pOPicture)
		{
			for (int liAt = 0; liAt < pOPicture.Pixels.Length; liAt++)
				pOPicture.Pixels[liAt] = Opaque(pOPicture.Pixels[liAt]);
		}

		/// <summary>Every pixel opaque but the mask's.</summary>
		public static void MakeOpaque(UWPicture pOPicture, UWMask pOExcept)
		{
			for (int liAt = 0; liAt < pOPicture.Pixels.Length; liAt++)
			{
				if (!pOExcept.Bits[liAt])
					pOPicture.Pixels[liAt] = Opaque(pOPicture.Pixels[liAt]);
			}
		}

		/// <summary>Tiles a piece with every second copy flipped, so no seam repeats.</summary>
		public static UWPicture MirrorTile(UWPicture pOSource, int piWidth, int piHeight, bool pbFlipX, bool pbFlipY)
		{
			UWPicture lOOut = new UWPicture(Math.Max(0, piWidth), Math.Max(0, piHeight));

			for (int y = 0; y < lOOut.Height; y++)
			{
				int liTileY = y / pOSource.Height;
				int liInY = y % pOSource.Height;

				if (pbFlipY && (liTileY & 1) != 0)
					liInY = pOSource.Height - 1 - liInY;

				for (int x = 0; x < lOOut.Width; x++)
				{
					int liTileX = x / pOSource.Width;
					int liInX = x % pOSource.Width;

					if (pbFlipX && (liTileX & 1) != 0)
						liInX = pOSource.Width - 1 - liInX;

					lOOut.Set(x, y, pOSource.Get(liInX, liInY));
				}
			}

			return lOOut;
		}

		/// <summary>A picture with room around it: piLeft to piBottom pixels more on each side,
		/// transparent there; its own visible pixels laid in (UWPicture.Overlay), so a transparent
		/// pixel comes as all channels 0.</summary>
		public static UWPicture Pad(UWPicture pOPicture, int piLeft, int piTop, int piRight, int piBottom)
		{
			UWPicture lOOut = new UWPicture(pOPicture.Width + piLeft + piRight, pOPicture.Height + piTop + piBottom);

			lOOut.Overlay(pOPicture, piLeft, piTop);

			return lOOut;
		}

		/// <summary>
		/// AN OUTLINE: every pixel pOTarget holds for and one of whose eight neighbours pOTouches
		/// holds for takes pOColour. Both rules look at the picture as it was before (they read a
		/// copy, or a mask), so what is set here does not spread; they say themselves what lies
		/// outside the picture.
		/// </summary>
		public static void Outline(UWPicture pOPicture, Func<int, int, bool> pOTarget, Func<int, int, bool> pOTouches, UWColor32 pOColour)
		{
			for (int y = 0; y < pOPicture.Height; y++)
			{
				for (int x = 0; x < pOPicture.Width; x++)
				{
					if (pOTarget(x, y) && TouchesAround(x, y, pOTouches))
						pOPicture.Set(x, y, pOColour);
				}
			}
		}

		/// <summary>Whether the rule holds for one of the eight neighbours of a pixel.</summary>
		public static bool TouchesAround(int piX, int piY, Func<int, int, bool> pORule)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					if ((dx != 0 || dy != 0) && pORule(piX + dx, piY + dy))
						return true;
				}
			}

			return false;
		}
	}
}
