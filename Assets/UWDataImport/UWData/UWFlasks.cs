using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWFlasks
	{
		public enum FlaskTypeEnum
		{
			Red,
			Blue,
			Green
		}

		private UWTexture mEmpty;

		private List<UWTexture> mRed;

		private List<UWTexture> mRedAnimation;

		private List<UWTexture> mBlue;

		private List<UWTexture> mBlueAnimation;

		private List<UWTexture> mGreen;

		private List<UWTexture> mGreenAnimation;

		private readonly int mFlaskTextureLength;

		private readonly int[] mOffetMultipliers = new int[13]
		{
			1, 1, 1, 2, 2, 1, 2, 2, 3, 2,
			2, 2, 2
		};

		public UWFlasks(UWTextures pTextures)
		{
			List<UWTexture> texturesByType = pTextures.GetTexturesByType(UWTexture.TextureTypes.FLASKS);
			mEmpty = texturesByType[75];
			mFlaskTextureLength = mEmpty.Width * mEmpty.Height;
			mRed = texturesByType.GetRange(0, 13);
			mRedAnimation = texturesByType.GetRange(13, 12);
			mBlue = texturesByType.GetRange(25, 13);
			mBlueAnimation = texturesByType.GetRange(38, 12);
			mGreen = texturesByType.GetRange(50, 13);
			mGreenAnimation = texturesByType.GetRange(63, 12);
		}

		/// <summary>
		/// The dark colour of the hatching that marks poison (pbHatch) - the poisoned flask is
		/// otherwise only told apart by its colour, which is exactly the case a red-green
		/// deficiency cannot see (per user, 2026-09-17). Index 1 is the black of the interface.
		/// </summary>
		private const byte HatchColour = 1;

		/// <summary>Every third diagonal of the liquid is darkened.</summary>
		private const int HatchSpacing = 3;

		/// <summary>The bubbling pictures of a colour follow right behind its thirteen fill
		/// slices.</summary>
		private const int BubbleOffset = 13;

		/// <summary>piBubbleFrame: the bubbling picture over the top of the liquid, or -1 (see
		/// UWFlaskAnimation).</summary>
		public UWTexture GetFlask(FlaskTypeEnum pFlaskType, float pFillStatus, bool pbHatch = false,
			int piBubbleFrame = -1)
		{
			List<UWTexture> list = null;
			switch (pFlaskType)
			{
			case FlaskTypeEnum.Red:
				list = mRed;
				break;
			case FlaskTypeEnum.Blue:
				list = mBlue;
				break;
			case FlaskTypeEnum.Green:
				list = mGreen;
				break;
			}
			// Clamped to the thirteen fill slices: vitality or mana above the maximum (the
			// original lowers the maximum on a level-up and leaves the current value - a save at
			// 113 of 90, per user 2026-09-29) indexed past them into the bubbles and beyond, and
			// the exception in UWCharacter.Init left the whole interface out.
			int num = Math.Max(0, Math.Min(13, (int)(pFillStatus * 13f)));
			int liTopStart = -1;
			int num2 = 3 * mEmpty.Width;
			UWTexture uWTexture = new UWTexture(mEmpty);
			for (int i = 0; i < num; i++)
			{
				int liStart = mFlaskTextureLength - num2 - list[i].Width * list[i].Height;

				uWTexture.ReplaceIndices(liStart, list[i].PaletteIndices);

				if (pbHatch)
					fHatch(uWTexture, liStart, list[i].PaletteIndices.Length);

				liTopStart = liStart;
				num2 += mOffetMultipliers[i] * mEmpty.Width;
			}

			// The bubbles sit on the topmost slice, as in the reference (uimanager_flasks).
			if (piBubbleFrame >= 0 && piBubbleFrame < UWFlaskAnimation.FrameCount && liTopStart >= 0)
			{
				List<UWTexture> lOBubbles = pFlaskType == FlaskTypeEnum.Red
					? mRedAnimation : (pFlaskType == FlaskTypeEnum.Blue ? mBlueAnimation : mGreenAnimation);

				if (piBubbleFrame < lOBubbles.Count)
					fBlend(uWTexture, liTopStart, lOBubbles[piBubbleFrame].PaletteIndices, pbHatch);
			}

			return uWTexture;
		}

		/// <summary>
		/// Copies a bubbling picture over the liquid.
		///
		/// ONLY WHERE LIQUID IS: the pictures are full rectangles and carry the glass edges of the
		/// level they were drawn for, so pasting one whole put glass and black over the bulb at
		/// other levels (per user with a screenshot, 2026-09-17). A pixel counts as liquid when the
		/// flask differs there from the empty one.
		/// </summary>
		private void fBlend(UWTexture pOFlask, int piStart, byte[] pyPixels, bool pbHatch)
		{
			for (int liAt = 0; liAt < pyPixels.Length; liAt++)
			{
				int liPixel = piStart + liAt;

				if (liPixel < 0 || liPixel >= pOFlask.PaletteIndices.Length || pyPixels[liAt] == 0)
					continue;

				if (liPixel < mEmpty.PaletteIndices.Length
					&& pOFlask.PaletteIndices[liPixel] == mEmpty.PaletteIndices[liPixel])
					continue;

				pOFlask.PaletteIndices[liPixel] = pyPixels[liAt];
			}

			if (pbHatch)
				fHatch(pOFlask, piStart, pyPixels.Length);
		}

		/// <summary>Diagonal stripes over a piece of liquid just put in. Only over pixels that
		/// really carry liquid - the pictures are rectangular and have transparent corners.</summary>
		private void fHatch(UWTexture pOFlask, int piStart, int piLength)
		{
			int liWidth = mEmpty.Width;

			for (int liAt = 0; liAt < piLength; liAt++)
			{
				int liPixel = piStart + liAt;

				if (liPixel < 0 || liPixel >= pOFlask.PaletteIndices.Length)
					continue;

				if (pOFlask.PaletteIndices[liPixel] == 0)
					continue;

				int liX = liPixel % liWidth;
				int liY = liPixel / liWidth;

				if ((liX + liY) % HatchSpacing == 0)
					pOFlask.PaletteIndices[liPixel] = HatchColour;
			}
		}
	}
}
