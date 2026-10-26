using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE SLOT CIRCLE and the freed slot ring (MAIN.BYT; cleanup 9.11, 2026-10-25: a builder of its
	/// own, UWHudArt forwards to it). Build reads MAIN.BYT, Compose works on the frame picture
	/// given.
	/// </summary>
	public static class UWSlotBuilder
	{
		// ------------------------------------------------- The slot circle (MAIN.BYT)

		/// <summary>The slot circle's size (UWMainFrame.SlotCircle).</summary>
		public const int SlotSize = 17;

		/// <summary>One slot circle, 17 x 17, the corners outside the circle transparent so the
		/// leather shows there.</summary>
		public static UWPicture BuildCircle(UWTextures pOTextures)
		{
			return ComposeCircle(UWMainFrame.Picture(pOTextures));
		}

		public static UWPicture ComposeCircle(UWPicture pOMain)
		{
			UWPicture lOCircle = pOMain.Crop(UWMainFrame.SlotCircle);
			float lfCentre = (SlotSize - 1) * 0.5f;

			for (int y = 0; y < lOCircle.Height; y++)
			{
				for (int x = 0; x < lOCircle.Width; x++)
				{
					float lfX = x - lfCentre;
					float lfY = y - lfCentre;

					if ((lfX * lfX) + (lfY * lfY) > UWHudTuning.Slot.CircleRadius * UWHudTuning.Slot.CircleRadius)
						lOCircle.Set(x, y, UWPixelOps.Transparent);
				}
			}

			return lOCircle;
		}

		/// <summary>
		/// THE SLOT CIRCLE WITHOUT ITS LEATHER (per user, 2026-10-09: on a part with a back of its own
		/// the circles showed squares of leather - "draw the inventory slots without background"):
		/// only the carved ring - its dark line and the light edges beside it, the pixels between
		/// radius 5.5 and the rim that are darker or lighter than the leather -, transparent inside
		/// and outside, so the back shows through. With pOCarve (a back's shade ramp, darkest first)
		/// the ring is CARVED INTO THE BACK as into the leather: its dark pixels in the darkest shade,
		/// its light ones in the lightest. Inside the ring a light shadow (black, a fifth opaque) keeps
		/// the slot a hollow on any pattern.
		/// </summary>
		public static UWPicture BuildRing(UWTextures pOTextures, List<UWColor32> pOCarve = null)
		{
			return ComposeRing(UWMainFrame.Picture(pOTextures), pOCarve);
		}

		public static UWPicture ComposeRing(UWPicture pOMain, List<UWColor32> pOCarve = null)
		{
			UWPicture lOCircle = ComposeCircle(pOMain);
			float lfCentre = (SlotSize - 1) * 0.5f;

			for (int y = 0; y < lOCircle.Height; y++)
			{
				for (int x = 0; x < lOCircle.Width; x++)
				{
					float lfX = x - lfCentre;
					float lfY = y - lfCentre;
					UWColor32 lOPixel = lOCircle.Get(x, y);
					float lfLuma = UWPixelRules.Luma(lOPixel);
					float lfSquare = (lfX * lfX) + (lfY * lfY);
					bool lbInner = lfSquare < UWHudTuning.Slot.RingInnerRadius * UWHudTuning.Slot.RingInnerRadius;
					bool lbRing = lOPixel.A > 0 && !lbInner && (lfLuma <= UWHudTuning.Slot.RingDarkLuma || lfLuma >= UWHudTuning.Slot.RingLightLuma);

					if (!lbRing)
						lOCircle.Set(x, y, lbInner ? UWHudTuning.Slot.Hollow : UWPixelOps.Transparent);
					else if (pOCarve != null && pOCarve.Count > 0)
						lOCircle.Set(x, y, lfLuma <= UWHudTuning.Slot.RingDarkLuma ? pOCarve[0] : pOCarve[pOCarve.Count - 1]);
				}
			}

			return lOCircle;
		}
	}
}
