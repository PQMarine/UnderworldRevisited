using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE SLOT CIRCLE and the freed slot ring (MAIN.BYT; cleanup 9.11, 2026-10-25: a builder of its
	/// own, UWHudArt forwards to it). Build reads MAIN.BYT, Compose works on the frame picture
	/// given.
	///
	/// THE SECOND GAME'S CIRCLE (UW2 plan, the modern interface's part U4b, 2026-11-23): a ring engraved in
	/// marble, dark at the top left and light at the bottom right, 16 pixels across. It is cut from the
	/// inventory page itself (PANELS.GR 0, the left shoulder's circle, which stands alone - the pack's eight
	/// are joined by ornaments), in a box of the first game's 17 so the windows' measures hold.
	/// </summary>
	public static class UWSlotBuilder
	{
		/// <summary>The left shoulder's circle on the second game's inventory page: the ring at x 8 to 23 and
		/// y 5 to 20, a pixel of marble left of it and above it.</summary>
		[UWClassicLayout.Measured("UW2 PANELS.GR 0")]
		public static readonly UWRectInt Uw2Circle = new UWRectInt(7, 4, 17, 17);

		private const int Uw2Page = 0;

		private static bool fIsUw2(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2;
		}

		/// <summary>The second game's inventory page as a picture.</summary>
		private static UWPicture fUw2Page(UWTextures pOTextures)
		{
			return UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.PANELS, Uw2Page));
		}

		// ------------------------------------------------- The slot circle (MAIN.BYT)

		/// <summary>The slot circle's size (UWMainFrame.SlotCircle).</summary>
		public const int SlotSize = 17;

		/// <summary>One slot circle, 17 x 17, the corners outside the circle transparent so the
		/// leather shows there.</summary>
		public static UWPicture BuildCircle(UWTextures pOTextures)
		{
			return fIsUw2(pOTextures) ? ComposeCircle(fUw2Page(pOTextures), Uw2Circle) : ComposeCircle(UWMainFrame.Picture(pOTextures));
		}

		public static UWPicture ComposeCircle(UWPicture pOMain)
		{
			return ComposeCircle(pOMain, UWMainFrame.SlotCircle);
		}

		/// <summary>The circle at pOPlace of the picture given.</summary>
		public static UWPicture ComposeCircle(UWPicture pOMain, UWRectInt pOPlace)
		{
			UWPicture lOCircle = pOMain.Crop(pOPlace);
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
			return fIsUw2(pOTextures)
				? ComposeRing(fUw2Page(pOTextures), Uw2Circle, UWHudTuning.Slot.RingDarkLumaUw2, UWHudTuning.Slot.RingLightLumaUw2, pOCarve)
				: ComposeRing(UWMainFrame.Picture(pOTextures), pOCarve);
		}

		public static UWPicture ComposeRing(UWPicture pOMain, List<UWColor32> pOCarve = null)
		{
			return ComposeRing(pOMain, UWMainFrame.SlotCircle, UWHudTuning.Slot.RingDarkLuma, UWHudTuning.Slot.RingLightLuma, pOCarve);
		}

		/// <summary>The ring of the circle at pOPlace: its pixels are those at or under pfDarkLuma and at or over
		/// pfLightLuma.</summary>
		public static UWPicture ComposeRing(UWPicture pOMain, UWRectInt pOPlace, float pfDarkLuma, float pfLightLuma, List<UWColor32> pOCarve = null)
		{
			UWPicture lOCircle = ComposeCircle(pOMain, pOPlace);
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
					bool lbRing = lOPixel.A > 0 && !lbInner && (lfLuma <= pfDarkLuma || lfLuma >= pfLightLuma);

					if (!lbRing)
						lOCircle.Set(x, y, lbInner ? UWHudTuning.Slot.Hollow : UWPixelOps.Transparent);
					else if (pOCarve != null && pOCarve.Count > 0)
						lOCircle.Set(x, y, lfLuma <= pfDarkLuma ? pOCarve[0] : pOCarve[pOCarve.Count - 1]);
				}
			}

			return lOCircle;
		}
	}
}
