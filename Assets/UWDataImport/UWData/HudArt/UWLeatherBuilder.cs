using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE LEATHER of the bags and the action bar (INV.GR 6) with its cloud inside, its tab, and the
	/// heading strip with the gargoyle's head (cleanup 9.11, 2026-10-25: a builder of its own,
	/// UWHudArt forwards to it). Build reads the pictures from the data, Compose works on pictures
	/// given - INV.GR 6 without its transparent right columns (Source) and the gargoyle's head
	/// (GargoyleHead) - so the core runs on synthetic input too.
	/// </summary>
	public static class UWLeatherBuilder
	{
		// ------------------------------------------------- The leather (INV.GR 6)

		/// <summary>The panel's frame: 4 columns on the left, 3 on the right, 3 rows on top, 4 at
		/// the bottom - after the two fully transparent columns at the panel's right edge (83 and 84,
		/// LeatherTransparentRight) are cut off; with them the windows showed a see-through strip on
		/// the right (per user, 2026-10-03).</summary>
		public const int LeatherLeft = 4;

		public const int LeatherRight = 3;

		public const int LeatherTransparentRight = 2;

		public const int LeatherTop = 3;

		public const int LeatherBottom = 4;

		public const int LeatherImage = 6;

		/// <summary>The most even part of the leather, 40 x 12, no highlight and no dark patch.</summary>
		private static readonly UWRectInt msCalmCentre = new UWRectInt(28, 23, 40, 12);

		/// <summary>The stretch of the top and bottom edge with the least variation.</summary>
		private const int CalmEdgeX0 = 36;

		private const int CalmEdgeX1 = 50;

		/// <summary>The same for the left and right edge.</summary>
		private const int CalmEdgeY0 = 14;

		private const int CalmEdgeY1 = 26;

		// ------------------------------------------------- The leather's pictures

		/// <summary>The leather's source: INV.GR 6 without its two transparent right columns.</summary>
		public static UWPicture Source(UWTextures pOTextures)
		{
			UWPicture lOPanel = UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.INV, LeatherImage));

			return lOPanel.Crop(new UWRectInt(0, 0, lOPanel.Width - LeatherTransparentRight, lOPanel.Height));
		}

		/// <summary>The gargoyle's head's crop of MAIN.BYT (UWHudMasks.GargoyleHead's place).</summary>
		public static UWPicture GargoyleHead(UWTextures pOTextures)
		{
			return UWMainFrame.Picture(pOTextures).Crop(UWHudMasks.GargoyleHead.Place);
		}

		/// <summary>The leather at any size, its inside the clouds (fClouds); piVariant picks another
		/// part of the cloud field, so neighbouring panels do not start alike.</summary>
		public static UWPicture Build(UWTextures pOTextures, int piWidth, int piHeight, int piVariant = 0)
		{
			return Compose(Source(pOTextures), piWidth, piHeight, piVariant);
		}

		/// <summary>A tab of leather, framed on the top, left and bottom and OPEN on the right. Laid
		/// over a panel's left frame (LeatherLeft columns wider than it shows), its top and bottom
		/// frame lines turn into the panel's frame and its leather runs on into the panel's - the
		/// handle is one piece with the panel (per user, 2026-10-03: beside the panel it looked cut
		/// off).</summary>
		public static UWPicture BuildTab(UWTextures pOTextures, int piWidth, int piHeight)
		{
			return ComposeTab(Source(pOTextures), piWidth, piHeight);
		}

		public static UWPicture ComposeTab(UWPicture pOSource, int piWidth, int piHeight)
		{
			UWPicture lOFull = Compose(pOSource, piWidth + LeatherRight + LeatherLeft, piHeight, 0);

			return lOFull.Crop(new UWRectInt(0, 0, piWidth, piHeight));
		}

		// ------------------------------------------------- The heading strip

		/// <summary>
		/// THE HEADING STRIP (per user on mockups, 2026-10-04): the modern scheme's heading on a strip
		/// of leather at the top centre, the gargoyle's head of the original's frame at the left in its
		/// own colours (burned into the leather it looked like a graphics fault in so few pixels, per
		/// user the same day), its eyes lit by the original's EYES.GR after a hit, the foe's name at
		/// the right. Closed it is only as wide as the heading; while the eyes are lit it opens
		/// as wide as the brand and the foe's name need, the same to both sides (UWModernHeadingStrip.fUpdateStrip;
		/// a fixed 150 left too much room, per user 2026-10-04). The clouds are anchored at the strip's
		/// centre and the brand at the open strip's left edge, so both stand still while it opens.
		/// </summary>
		public const int StripHeight = 24;

		public const int StripClosedWidth = 44;

		/// <summary>The open strip's least half width: the brand at the left with its margin, the
		/// heading's half and a gap.</summary>
		public const int StripMinOpenHalf = 46;

		/// <summary>The brand's distance from the open strip's left edge, and its top.</summary>
		public const int BrandMargin = 6;

		public const int BrandTop = 3;

		public const int BrandWidth = 24;

		/// <summary>The original's eyes (EYES.GR, 20 x 3) on the brand: the slits lie in rows 4 and 5
		/// of the head, the image's left edge two columns in.</summary>
		public const int EyesLeftInBrand = 2;

		public const int EyesTopInBrand = 3;

		/// <summary>The brand's left edge in a strip of this width that opens to piOpenHalf.</summary>
		public static int BrandLeft(int piWidth, int piOpenHalf)
		{
			return (piWidth / 2) - piOpenHalf + BrandMargin;
		}

		/// <summary>The brand's left edge in the middle of a strip of this width - where it sits in the
		/// closed strip while the original's compass stands in for the heading's text (per user,
		/// 2026-10-09: the closed strip was empty then).</summary>
		public static int BrandCentredLeft(int piWidth)
		{
			return (piWidth / 2) - (BrandWidth / 2);
		}

		/// <summary>The strip; piClosedWidth is the closed strip's width in the strip's own pixels
		/// (wider when the modern scheme shows the strip narrowed, UWModernHud).</summary>
		public static UWPicture BuildHeadingStrip(UWTextures pOTextures, int piWidth, int piOpenHalf, int piClosedWidth = StripClosedWidth)
		{
			return ComposeHeadingStrip(Source(pOTextures), piWidth > piClosedWidth ? GargoyleHead(pOTextures) : null, piWidth, piOpenHalf,
				piClosedWidth);
		}

		/// <summary>The strip from the leather's source and the gargoyle's head's crop (none: no
		/// brand).</summary>
		public static UWPicture ComposeHeadingStrip(UWPicture pOSource, UWPicture pOHead, int piWidth, int piOpenHalf,
			int piClosedWidth = StripClosedWidth)
		{
			UWPicture lOStrip = Compose(pOSource, piWidth, StripHeight, 4, -(piWidth / 2));

			if (piWidth > piClosedWidth && pOHead != null)
				fPasteGargoyle(pOHead, lOStrip, BrandLeft(piWidth, piOpenHalf), BrandTop);

			return lOStrip;
		}

		/// <summary>The strip with the brand at a left edge of the caller's choosing, also on the
		/// closed strip (the compass variant: centred when closed, moving to the left as it opens).</summary>
		public static UWPicture BuildHeadingStripWithBrandAt(UWTextures pOTextures, int piWidth, int piBrandLeft)
		{
			return ComposeHeadingStripWithBrandAt(Source(pOTextures), GargoyleHead(pOTextures), piWidth, piBrandLeft);
		}

		public static UWPicture ComposeHeadingStripWithBrandAt(UWPicture pOSource, UWPicture pOHead, int piWidth, int piBrandLeft)
		{
			UWPicture lOStrip = Compose(pOSource, piWidth, StripHeight, 4, -(piWidth / 2));

			fPasteGargoyle(pOHead, lOStrip, piBrandLeft, BrandTop);

			return lOStrip;
		}

		/// <summary>Lays the gargoyle's head onto the strip's inside at the given top left - its own
		/// pixels (UWHudMasks.GargoyleHead). BuildHeadingStrip leaves it off the closed strip: there
		/// its edge showed a sliver of it (per user, 2026-10-04).</summary>
		private static void fPasteGargoyle(UWPicture pOHead, UWPicture pOStrip, int piLeft, int piTop)
		{
			UWPicture lOHead = pOHead;
			int liInsideRight = pOStrip.Width - LeatherRight;
			int liInsideBottom = pOStrip.Height - LeatherBottom;

			for (int y = 0; y < lOHead.Height; y++)
			{
				for (int x = 0; x < lOHead.Width; x++)
				{
					UWColor32 lOColour = lOHead.Get(x, y);
					int liAtX = piLeft + x;
					int liAtY = piTop + y;

					if (lOColour.A == 0 || UWPixelRules.Luma(lOColour) > UWHudTuning.Leather.GargoyleMaxLuma || !UWHudMasks.GargoyleHead.Is(x, y, 'X')
						|| liAtX < LeatherLeft || liAtY < LeatherTop || liAtX >= liInsideRight || liAtY >= liInsideBottom)
						continue;

					pOStrip.Set(liAtX, liAtY, lOColour);
				}
			}
		}

		// ------------------------------------------------- The cloud leather

		/// <summary>
		/// THE CLOUD LEATHER (per user, 2026-10-04): the mirrored tiling of the calm centre showed as a
		/// regular pattern on the larger panels, and the leather's stripes do not tile without seams
		/// either - so the inside is drawn anew as soft clouds, as the original's leather has them,
		/// from its own colours (CloudColours) with a little grain; the frame stays the original's.
		/// Variant C of three offline renders per user: five shades, a dark one among them, as the
		/// light text wants a darker ground; per user the same day for every modern background.
		///
		/// ONE FIXED FIELD, anchored at the inside's top left: every value comes from its coordinates
		/// alone (UWNoise.Hash), so a panel that grows or shrinks shows more or less of the same
		/// clouds - a seed per size drew it anew at every change (per user, the same day: it looked
		/// odd). The noise (three octaves, UWNoise.Clouds) is the modern backs' too, its range and
		/// grain UWHudTuning.Leather's.
		/// </summary>
		private static UWPicture fClouds(UWPicture pOSource, int piWidth, int piHeight, int piVariant, int piShiftX = 0)
		{
			UWPicture lOOut = new UWPicture(Math.Max(0, piWidth), Math.Max(0, piHeight));
			List<UWColor32> lOShades = CloudColours(pOSource);

			if (lOShades.Count == 0)
				return lOOut;

			// Another variant: another part of the same field, far away.
			int liOffsetX = (piVariant * 977) + piShiftX;
			int liOffsetY = piVariant * 613;

			for (int y = 0; y < lOOut.Height; y++)
			{
				for (int x = 0; x < lOOut.Width; x++)
				{
					int liX = x + liOffsetX;
					int liY = y + liOffsetY;
					float lfValue = UWNoise.Clouds(liX, liY, 0);

					lfValue += (UWNoise.Hash(liX, liY, UWNoise.GrainLayer) - 0.5f) * UWHudTuning.Leather.CloudGrain;
					lfValue = UWMath.Clamp((lfValue - UWHudTuning.Leather.CloudLow) / (UWHudTuning.Leather.CloudHigh - UWHudTuning.Leather.CloudLow),
						0f, 0.999f);

					lOOut.Set(x, y, lOShades[(int)(lfValue * lOShades.Count)]);
				}
			}

			return lOOut;
		}

		/// <summary>The shades, darkest first: the calm centre's colours, and below them the most
		/// frequent darker colour of the leather's inside (not the near-black of the scratches). The
		/// modern backs' leather ramp too (UWBackdropArt).</summary>
		public static List<UWColor32> CloudColours(UWPicture pOSource)
		{
			List<UWColor32> lOShades = new List<UWColor32>();
			UWPicture lOCalm = pOSource.Crop(msCalmCentre);

			foreach (UWColor32 lOColour in lOCalm.Pixels)
			{
				if (!lOShades.Exists(lOOther => UWPixelRules.Same(lOOther, lOColour)))
					lOShades.Add(lOColour);
			}

			lOShades.Sort((lOA, lOB) => UWPixelRules.Luma(lOA).CompareTo(UWPixelRules.Luma(lOB)));

			if (lOShades.Count == 0)
				return lOShades;

			float lfDarkest = UWPixelRules.Luma(lOShades[0]);
			Dictionary<int, int> lOCounts = new Dictionary<int, int>();
			UWColor32 lODark = lOShades[0];
			int liBest = 0;

			for (int y = LeatherTop; y < pOSource.Height - LeatherBottom; y++)
			{
				for (int x = LeatherLeft; x < pOSource.Width - LeatherRight; x++)
				{
					UWColor32 lOColour = pOSource.Get(x, y);
					float lfLuma = UWPixelRules.Luma(lOColour);

					if (lOColour.A == 0 || lfLuma >= lfDarkest || lfLuma < lfDarkest * UWHudTuning.Leather.DarkShadeShare)
						continue;

					int liKey = (lOColour.R << 16) | (lOColour.G << 8) | lOColour.B;

					lOCounts.TryGetValue(liKey, out int liCount);
					lOCounts[liKey] = ++liCount;

					if (liCount > liBest)
					{
						liBest = liCount;
						lODark = lOColour;
					}
				}
			}

			if (liBest > 0)
				lOShades.Insert(0, lODark);

			return lOShades;
		}

		/// <summary>The leather at any size from its source (Source); piShiftX moves the clouds (the
		/// heading strip anchors them at its centre).</summary>
		public static UWPicture Compose(UWPicture pOSource, int piWidth, int piHeight, int piVariant, int piShiftX = 0)
		{
			UWPicture lOSource = pOSource;
			int liSourceWidth = lOSource.Width;
			int liSourceHeight = lOSource.Height;

			piWidth = Math.Max(piWidth, LeatherLeft + LeatherRight + 1);
			piHeight = Math.Max(piHeight, LeatherTop + LeatherBottom + 1);

			int liInnerWidth = piWidth - LeatherLeft - LeatherRight;
			int liInnerHeight = piHeight - LeatherTop - LeatherBottom;

			UWPicture lOOut = new UWPicture(piWidth, piHeight);

			lOOut.Paste(fClouds(lOSource, liInnerWidth, liInnerHeight, piVariant, piShiftX), LeatherLeft, LeatherTop);

			UWRectInt lOTop = new UWRectInt(CalmEdgeX0, 0, CalmEdgeX1 - CalmEdgeX0, LeatherTop);
			UWRectInt lOBottom = new UWRectInt(CalmEdgeX0, liSourceHeight - LeatherBottom, CalmEdgeX1 - CalmEdgeX0, LeatherBottom);
			UWRectInt lOLeft = new UWRectInt(0, CalmEdgeY0, LeatherLeft, CalmEdgeY1 - CalmEdgeY0);
			UWRectInt lORight = new UWRectInt(liSourceWidth - LeatherRight, CalmEdgeY0, LeatherRight, CalmEdgeY1 - CalmEdgeY0);

			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lOTop), liInnerWidth, LeatherTop, true, false), LeatherLeft, 0);
			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lOBottom), liInnerWidth, LeatherBottom, true, false), LeatherLeft, piHeight - LeatherBottom);
			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lOLeft), LeatherLeft, liInnerHeight, false, true), 0, LeatherTop);
			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lORight), LeatherRight, liInnerHeight, false, true), piWidth - LeatherRight, LeatherTop);

			lOOut.Paste(lOSource.Crop(new UWRectInt(0, 0, LeatherLeft, LeatherTop)), 0, 0);
			lOOut.Paste(lOSource.Crop(new UWRectInt(liSourceWidth - LeatherRight, 0, LeatherRight, LeatherTop)), piWidth - LeatherRight, 0);
			lOOut.Paste(lOSource.Crop(new UWRectInt(0, liSourceHeight - LeatherBottom, LeatherLeft, LeatherBottom)), 0, piHeight - LeatherBottom);
			lOOut.Paste(lOSource.Crop(new UWRectInt(liSourceWidth - LeatherRight, liSourceHeight - LeatherBottom, LeatherRight, LeatherBottom)),
				piWidth - LeatherRight, piHeight - LeatherBottom);

			return lOOut;
		}
	}
}
