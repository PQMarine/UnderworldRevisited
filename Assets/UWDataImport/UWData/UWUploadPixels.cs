using System;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE FINISHED PIXELS OF A PICTURE, ready for an engine to upload (cleanup step 5.4,
	/// 2026-10-19, decision 26 of the cleanup plan: the interface must not hang on Unity). The
	/// game's pictures and the HUD art builders hold rows top down (UWTexture, UWPicture); a
	/// texture upload wants them bottom up as RGBA bytes. The row flip, the colour help
	/// (UWColourFilter) and the transparency rule are done here, without an engine type; the
	/// Unity side (UWTextureFactory) only creates the texture and hands it these bytes.
	///
	/// Every method returns width * height * 4 bytes, R G B A per pixel, the bottom row first.
	///
	/// TWO TRANSPARENCY RULES, both kept as they were (pixel for pixel), because they differ
	/// where a bilinear filter blends a visible pixel with a transparent neighbour:
	///   Keep  - a transparent pixel keeps its palette colour, and the colour help runs over it
	///           too. The game's pictures were always turned into textures this way
	///           (UWGameUI.fGetTextureInvert until 2026-10-19).
	///   Clear - a transparent pixel becomes 0,0,0,0. The modern HUD's art (UWModernHudArt) was
	///           always uploaded this way.
	/// </summary>
	public static class UWUploadPixels
	{
		public const int BytesPerPixel = 4;

		public enum TransparentEnum
		{
			Keep = 0,
			Clear = 1
		}

		/// <summary>Colours (rows top down) as upload bytes, the colour help applied (null or an
		/// inactive filter leaves them as they are).</summary>
		public static byte[] FromColours(UWColor32[] pyTopDown, int piWidth, int piHeight, UWColourFilter pOFilter,
			TransparentEnum peTransparent)
		{
			byte[] lyOut = new byte[piWidth * piHeight * BytesPerPixel];
			bool lbFilter = pOFilter != null && pOFilter.IsActive;
			int liOut = 0;

			for (int y = piHeight - 1; y >= 0; y--)
			{
				int liRow = y * piWidth;

				for (int x = 0; x < piWidth; x++)
				{
					UWColor32 lOColour = pyTopDown[liRow + x];
					byte lyRed = lOColour.R;
					byte lyGreen = lOColour.G;
					byte lyBlue = lOColour.B;
					byte lyAlpha = lOColour.A;

					if (peTransparent == TransparentEnum.Clear && lyAlpha == 0)
					{
						lyRed = 0;
						lyGreen = 0;
						lyBlue = 0;
					}
					else if (lbFilter)
						pOFilter.Apply(ref lyRed, ref lyGreen, ref lyBlue);

					lyOut[liOut++] = lyRed;
					lyOut[liOut++] = lyGreen;
					lyOut[liOut++] = lyBlue;
					lyOut[liOut++] = lyAlpha;
				}
			}

			return lyOut;
		}

		/// <summary>A composed picture (UWHudArt, UWBackdropArt): transparent pixels cleared.</summary>
		public static byte[] FromPicture(UWPicture pOPicture, UWColourFilter pOFilter)
		{
			return FromColours(pOPicture.Pixels, pOPicture.Width, pOPicture.Height, pOFilter, TransparentEnum.Clear);
		}

		/// <summary>A picture with a chosen transparency rule (the Classic Wide frame keeps its
		/// transparent pixels' colours, as MAIN.BYT itself does).</summary>
		public static byte[] FromPicture(UWPicture pOPicture, UWColourFilter pOFilter, TransparentEnum peTransparent)
		{
			return FromColours(pOPicture.Pixels, pOPicture.Width, pOPicture.Height, pOFilter, peTransparent);
		}

		/// <summary>One of the game's pictures in its palette colours, transparent pixels keeping
		/// theirs (the rule of every .GR and .BYT picture shown so far). From
		/// UWGameUI.fGetTextureInvert, "THE ONE place for it - UWCharacter used to carry a copy":
		/// these pictures are turned into colours right here instead of going through the palette
		/// table, so the colour help has to be applied. Until 2026-09-19 only the copy in
		/// UWCharacter did that: the flasks and the paperdoll followed the filter, panels, compass,
		/// map and conversation did not (per user; found by the duplicate audit, Todo 11.2 item
		/// 19).</summary>
		public static byte[] FromTexture(UWTexture pOTexture, UWColourFilter pOFilter)
		{
			return FromColours(pOTexture.GetUWColor32(), pOTexture.Width, pOTexture.Height, pOFilter, TransparentEnum.Keep);
		}

		/// <summary>
		/// A picture whose transparency is palette index 0 rather than the palette's marker: the
		/// eyes (EYES.GR, UWEyesDisplay) - index 0 transparent and black, every other pixel opaque in
		/// its colour, the colour help applied (decision 6 of the cleanup plan, B30, 2026-10-19: the
		/// eyes follow the colour help like every other picture; with the help off nothing changes).
		/// A pixel beyond the colours stays 0,0,0,0, as it did.
		/// </summary>
		public static byte[] FromIndexZeroKeyed(UWTexture pOTexture, UWColourFilter pOFilter)
		{
			UWColor32[] lyColours = pOTexture.GetUWColor32();
			byte[] lyIndices = pOTexture.PaletteIndices;
			int liWidth = pOTexture.Width;
			int liHeight = pOTexture.Height;
			byte[] lyOut = new byte[liWidth * liHeight * BytesPerPixel];
			bool lbFilter = pOFilter != null && pOFilter.IsActive;

			for (int y = 0; y < liHeight; y++)
			{
				int liSourceRow = (liHeight - 1 - y) * liWidth;
				int liTargetRow = y * liWidth;

				for (int x = 0; x < liWidth; x++)
				{
					int liSource = liSourceRow + x;

					if (liSource >= lyColours.Length)
						continue;

					bool lbTransparent = lyIndices != null && liSource < lyIndices.Length && lyIndices[liSource] == 0;

					if (lbTransparent)
						continue;

					byte lyRed = lyColours[liSource].R;
					byte lyGreen = lyColours[liSource].G;
					byte lyBlue = lyColours[liSource].B;

					if (lbFilter)
						pOFilter.Apply(ref lyRed, ref lyGreen, ref lyBlue);

					int liOut = (liTargetRow + x) * BytesPerPixel;

					lyOut[liOut] = lyRed;
					lyOut[liOut + 1] = lyGreen;
					lyOut[liOut + 2] = lyBlue;
					lyOut[liOut + 3] = 255;
				}
			}

			return lyOut;
		}

		/// <summary>
		/// An icon for the palette shader (UWIconTextureBuilder until 2026-10-19): the main palette
		/// index in red, opacity in alpha, everything else 0. The opacity comes from the resolved
		/// colour - the palette's transparency marker is a very dark blue, which the index alone
		/// does not tell (see UWPalette); 4-bit object pictures are mapped back to the main palette
		/// first (UWTexture.GetMainPaletteIndices). No colour help here: the shader's palette table
		/// carries it.
		/// </summary>
		public static byte[] IconIndices(UWTexture pOTexture)
		{
			UWColor32[] lyColours = pOTexture.GetUWColor32();
			byte[] lyIndices = pOTexture.GetMainPaletteIndices();
			int liWidth = pOTexture.Width;
			int liHeight = pOTexture.Height;
			byte[] lyOut = new byte[liWidth * liHeight * BytesPerPixel];
			int liOut = 0;

			for (int y = liHeight - 1; y >= 0; y--)
			{
				for (int x = 0; x < liWidth; x++)
				{
					int liAt = (y * liWidth) + x;

					if (lyColours[liAt].A != 0)
					{
						lyOut[liOut] = lyIndices != null && liAt < lyIndices.Length ? lyIndices[liAt] : (byte)0;
						lyOut[liOut + 3] = 255;
					}

					liOut += BytesPerPixel;
				}
			}

			return lyOut;
		}

		/// <summary>
		/// Upload bytes with the picture moved piRows rows down, the rows it leaves at the top
		/// 0,0,0,0 - the men's plate legs, which sit two rows too high in their picture
		/// (UWWearables.GetArmorDrop, UWPaperdollView). The loop over Unity colours it replaced
		/// (2026-10-19, cleanup phase 5 fix: the paperdoll follows the colour help and keeps one
		/// texture per slot) did the same. Works on a copy; 0 rows or fewer change nothing.
		/// </summary>
		public static byte[] MoveDown(byte[] pyBottomUp, int piWidth, int piHeight, int piRows)
		{
			byte[] lyOut = new byte[pyBottomUp.Length];

			if (piRows <= 0)
			{
				Array.Copy(pyBottomUp, lyOut, pyBottomUp.Length);

				return lyOut;
			}

			int liRowBytes = piWidth * BytesPerPixel;

			// Bottom up: the row y shows what row y + piRows held.
			for (int y = 0; y < piHeight - piRows; y++)
				Array.Copy(pyBottomUp, (y + piRows) * liRowBytes, lyOut, y * liRowBytes, liRowBytes);

			return lyOut;
		}

		/// <summary>Upload bytes with the pixels outside a top-down keep mask made 0,0,0,0 (the
		/// modern shelf's flasks and gem, UWModernHud). A mask of the wrong size changes nothing,
		/// as before. Works on a copy.</summary>
		public static byte[] Keep(byte[] pyBottomUp, int piWidth, int piHeight, bool[] pbKeepTopDown)
		{
			byte[] lyOut = (byte[])pyBottomUp.Clone();

			if (pbKeepTopDown == null || pbKeepTopDown.Length != piWidth * piHeight)
				return lyOut;

			for (int y = 0; y < piHeight; y++)
			{
				int liMaskRow = (piHeight - 1 - y) * piWidth;

				for (int x = 0; x < piWidth; x++)
				{
					if (!pbKeepTopDown[liMaskRow + x])
						Array.Clear(lyOut, ((y * piWidth) + x) * BytesPerPixel, BytesPerPixel);
				}
			}

			return lyOut;
		}
	}
}
