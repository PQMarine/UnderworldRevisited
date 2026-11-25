using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE LEATHER of the bags and the action bar (INV.GR 6) with its cloud inside, its tab, and the
	/// heading strip with the gargoyle's head (cleanup 9.11, 2026-10-25: a builder of its own,
	/// UWHudArt forwards to it). Build reads the pictures from the data, Compose works on pictures
	/// given - INV.GR 6 without its transparent right columns (Source) and the gargoyle's head
	/// (GargoyleHead) - so the core runs on synthetic input too.
	///
	/// THE SECOND GAME'S PANEL (UW2 plan, the modern interface's part U4b, 2026-11-23): its INV.GR 6 is the
	/// open bag's dark marble, 75 x 41, with a thinner frame and no transparent columns. The panel's measures
	/// are a Frame per game (FrameOf); the constants below stay the first game's and stay what the windows lay
	/// their contents out by - on the second game the marble simply runs on under the two pixels the frame is
	/// thinner by.
	/// </summary>
	public static class UWLeatherBuilder
	{
		/// <summary>Where a game's panel picture has its frame and its calm parts.</summary>
		public sealed class Frame
		{
			public int Left;

			public int Right;

			public int Top;

			public int Bottom;

			/// <summary>Fully transparent columns at the picture's right edge, cut off.</summary>
			public int TransparentRight;

			/// <summary>The most even part of the inside: the clouds' shades.</summary>
			public UWRectInt CalmCentre;

			/// <summary>The stretch of the top and bottom edge with the least variation.</summary>
			public int CalmEdgeX0;

			public int CalmEdgeX1;

			/// <summary>The same for the left and right edge.</summary>
			public int CalmEdgeY0;

			public int CalmEdgeY1;
		}

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

		/// <summary>The first game's leather (the constants above).</summary>
		public static readonly Frame Uw1Frame = new Frame
		{
			Left = LeatherLeft, Right = LeatherRight, Top = LeatherTop, Bottom = LeatherBottom, TransparentRight = LeatherTransparentRight,
			CalmCentre = msCalmCentre, CalmEdgeX0 = CalmEdgeX0, CalmEdgeX1 = CalmEdgeX1, CalmEdgeY0 = CalmEdgeY0, CalmEdgeY1 = CalmEdgeY1
		};

		/// <summary>
		/// The second game's marble: one light row on top, a light and a shadow row at the bottom, a light and a
		/// dark column on the left, one light column on the right. The calm centre is a part without the
		/// near-black blotches (as a shade of the clouds they made the panels heavy); the calm edges are where
		/// the frame's lines change least.
		/// </summary>
		public static class Uw2
		{
			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int Left = 2;

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int Right = 1;

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int Top = 1;

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int Bottom = 2;

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public static readonly UWRectInt CalmCentre = new UWRectInt(40, 12, 18, 10);

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int CalmEdgeX0 = 43;

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int CalmEdgeX1 = 53;

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int CalmEdgeY0 = 10;

			[UWClassicLayout.Measured("UW2 INV.GR 6")]
			public const int CalmEdgeY1 = 18;
		}

		public static readonly Frame Uw2Frame = new Frame
		{
			Left = Uw2.Left, Right = Uw2.Right, Top = Uw2.Top, Bottom = Uw2.Bottom, TransparentRight = 0,
			CalmCentre = Uw2.CalmCentre, CalmEdgeX0 = Uw2.CalmEdgeX0, CalmEdgeX1 = Uw2.CalmEdgeX1, CalmEdgeY0 = Uw2.CalmEdgeY0, CalmEdgeY1 = Uw2.CalmEdgeY1
		};

		/// <summary>The measures of the panel picture the data carries.</summary>
		public static Frame FrameOf(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? Uw2Frame : Uw1Frame;
		}

		// ------------------------------------------------- The leather's pictures

		/// <summary>The leather's source: INV.GR 6 without its transparent right columns (the first game's two).</summary>
		public static UWPicture Source(UWTextures pOTextures)
		{
			UWPicture lOPanel = UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.INV, LeatherImage));

			return lOPanel.Crop(new UWRectInt(0, 0, lOPanel.Width - FrameOf(pOTextures).TransparentRight, lOPanel.Height));
		}

		/// <summary>The gargoyle's head's crop of the game's frame (UWHudMasks.GargoyleHeadOf's place).</summary>
		public static UWPicture GargoyleHead(UWTextures pOTextures)
		{
			return UWMainFrame.Picture(pOTextures).Crop(UWHudMasks.GargoyleHeadOf(pOTextures).Place);
		}

		/// <summary>The leather at any size, its inside the clouds (fClouds); piVariant picks another
		/// part of the cloud field, so neighbouring panels do not start alike.</summary>
		public static UWPicture Build(UWTextures pOTextures, int piWidth, int piHeight, int piVariant = 0)
		{
			return Compose(Source(pOTextures), piWidth, piHeight, piVariant, 0, FrameOf(pOTextures));
		}

		/// <summary>A tab of leather, framed on the top, left and bottom and OPEN on the right. Laid
		/// over a panel's left frame (LeatherLeft columns wider than it shows), its top and bottom
		/// frame lines turn into the panel's frame and its leather runs on into the panel's - the
		/// handle is one piece with the panel (per user, 2026-10-03: beside the panel it looked cut
		/// off).</summary>
		public static UWPicture BuildTab(UWTextures pOTextures, int piWidth, int piHeight)
		{
			return ComposeTab(Source(pOTextures), piWidth, piHeight, FrameOf(pOTextures));
		}

		public static UWPicture ComposeTab(UWPicture pOSource, int piWidth, int piHeight, Frame pOFrame = null)
		{
			UWPicture lOFull = Compose(pOSource, piWidth + LeatherRight + LeatherLeft, piHeight, 0, 0, pOFrame);

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
		/// (wider when the modern scheme shows the strip narrowed, UWModernHud). PER GAME since part U4d step 1 of the
		/// UW2 plan (2026-11-23): the game's panel and frame measures, its gargoyle's head and that head's mask - until
		/// then the leather's measures and the head were the first game's whatever the data.</summary>
		public static UWPicture BuildHeadingStrip(UWTextures pOTextures, int piWidth, int piOpenHalf, int piClosedWidth = StripClosedWidth)
		{
			return ComposeHeadingStrip(Source(pOTextures), piWidth > piClosedWidth ? GargoyleHead(pOTextures) : null, piWidth, piOpenHalf,
				piClosedWidth, FrameOf(pOTextures), UWHudMasks.GargoyleHeadOf(pOTextures));
		}

		/// <summary>The strip from the leather's source and the gargoyle's head's crop (none: no
		/// brand); the frame and the head's mask the first game's unless given.</summary>
		public static UWPicture ComposeHeadingStrip(UWPicture pOSource, UWPicture pOHead, int piWidth, int piOpenHalf,
			int piClosedWidth = StripClosedWidth, Frame pOFrame = null, UWMaskText pOHeadMask = null)
		{
			UWPicture lOStrip = Compose(pOSource, piWidth, StripHeight, 4, -(piWidth / 2), pOFrame);

			if (piWidth > piClosedWidth && pOHead != null)
				fPasteGargoyle(pOHead, lOStrip, BrandLeft(piWidth, piOpenHalf), BrandTop, pOHeadMask);

			return lOStrip;
		}

		/// <summary>The strip with the brand at a left edge of the caller's choosing, also on the
		/// closed strip (the compass variant: centred when closed, moving to the left as it opens).</summary>
		public static UWPicture BuildHeadingStripWithBrandAt(UWTextures pOTextures, int piWidth, int piBrandLeft)
		{
			return ComposeHeadingStripWithBrandAt(Source(pOTextures), GargoyleHead(pOTextures), piWidth, piBrandLeft, FrameOf(pOTextures),
				UWHudMasks.GargoyleHeadOf(pOTextures));
		}

		public static UWPicture ComposeHeadingStripWithBrandAt(UWPicture pOSource, UWPicture pOHead, int piWidth, int piBrandLeft,
			Frame pOFrame = null, UWMaskText pOHeadMask = null)
		{
			UWPicture lOStrip = Compose(pOSource, piWidth, StripHeight, 4, -(piWidth / 2), pOFrame);

			fPasteGargoyle(pOHead, lOStrip, piBrandLeft, BrandTop, pOHeadMask);

			return lOStrip;
		}

		/// <summary>Lays the gargoyle's head onto the strip's inside at the given top left - its own
		/// pixels (UWHudMasks.GargoyleHead). BuildHeadingStrip leaves it off the closed strip: there
		/// its edge showed a sliver of it (per user, 2026-10-04).</summary>
		private static void fPasteGargoyle(UWPicture pOHead, UWPicture pOStrip, int piLeft, int piTop, UWMaskText pOMask = null)
		{
			UWPicture lOHead = pOHead;
			UWMaskText lOMask = pOMask ?? UWHudMasks.GargoyleHead;
			int liInsideRight = pOStrip.Width - LeatherRight;
			int liInsideBottom = pOStrip.Height - LeatherBottom;

			for (int y = 0; y < lOHead.Height; y++)
			{
				for (int x = 0; x < lOHead.Width; x++)
				{
					UWColor32 lOColour = lOHead.Get(x, y);
					int liAtX = piLeft + x;
					int liAtY = piTop + y;

					if (lOColour.A == 0 || UWPixelRules.Luma(lOColour) > UWHudTuning.Leather.GargoyleMaxLuma || !lOMask.Is(x, y, 'X')
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
		private static UWPicture fClouds(UWPicture pOSource, int piWidth, int piHeight, int piVariant, int piShiftX, Frame pOFrame)
		{
			UWPicture lOOut = new UWPicture(Math.Max(0, piWidth), Math.Max(0, piHeight));
			List<UWColor32> lOShades = CloudColours(pOSource, pOFrame);

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
		public static List<UWColor32> CloudColours(UWPicture pOSource, Frame pOFrame = null)
		{
			Frame lOFrame = pOFrame ?? Uw1Frame;
			List<UWColor32> lOShades = new List<UWColor32>();
			UWPicture lOCalm = pOSource.Crop(lOFrame.CalmCentre);

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

			for (int y = lOFrame.Top; y < pOSource.Height - lOFrame.Bottom; y++)
			{
				for (int x = lOFrame.Left; x < pOSource.Width - lOFrame.Right; x++)
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
		/// heading strip anchors them at its centre). pOFrame says where the source has its frame (the first
		/// game's when left out).</summary>
		public static UWPicture Compose(UWPicture pOSource, int piWidth, int piHeight, int piVariant, int piShiftX = 0, Frame pOFrame = null)
		{
			Frame lOFrame = pOFrame ?? Uw1Frame;
			UWPicture lOSource = pOSource;
			int liSourceWidth = lOSource.Width;
			int liSourceHeight = lOSource.Height;
			int liLeft = lOFrame.Left;
			int liRight = lOFrame.Right;
			int liTop = lOFrame.Top;
			int liBottom = lOFrame.Bottom;

			// The least size is the windows' (the constants), whatever the frame's own thickness.
			piWidth = Math.Max(piWidth, LeatherLeft + LeatherRight + 1);
			piHeight = Math.Max(piHeight, LeatherTop + LeatherBottom + 1);

			int liInnerWidth = piWidth - liLeft - liRight;
			int liInnerHeight = piHeight - liTop - liBottom;

			UWPicture lOOut = new UWPicture(piWidth, piHeight);

			// The clouds stay anchored where the windows' inside begins, so a thinner frame shows the same field.
			lOOut.Paste(fClouds(lOSource, liInnerWidth, liInnerHeight, piVariant, piShiftX - (LeatherLeft - liLeft), lOFrame), liLeft, liTop);

			UWRectInt lOTop = new UWRectInt(lOFrame.CalmEdgeX0, 0, lOFrame.CalmEdgeX1 - lOFrame.CalmEdgeX0, liTop);
			UWRectInt lOBottom = new UWRectInt(lOFrame.CalmEdgeX0, liSourceHeight - liBottom, lOFrame.CalmEdgeX1 - lOFrame.CalmEdgeX0, liBottom);
			UWRectInt lOLeft = new UWRectInt(0, lOFrame.CalmEdgeY0, liLeft, lOFrame.CalmEdgeY1 - lOFrame.CalmEdgeY0);
			UWRectInt lORight = new UWRectInt(liSourceWidth - liRight, lOFrame.CalmEdgeY0, liRight, lOFrame.CalmEdgeY1 - lOFrame.CalmEdgeY0);

			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lOTop), liInnerWidth, liTop, true, false), liLeft, 0);
			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lOBottom), liInnerWidth, liBottom, true, false), liLeft, piHeight - liBottom);
			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lOLeft), liLeft, liInnerHeight, false, true), 0, liTop);
			lOOut.Paste(UWPixelOps.MirrorTile(lOSource.Crop(lORight), liRight, liInnerHeight, false, true), piWidth - liRight, liTop);

			lOOut.Paste(lOSource.Crop(new UWRectInt(0, 0, liLeft, liTop)), 0, 0);
			lOOut.Paste(lOSource.Crop(new UWRectInt(liSourceWidth - liRight, 0, liRight, liTop)), piWidth - liRight, 0);
			lOOut.Paste(lOSource.Crop(new UWRectInt(0, liSourceHeight - liBottom, liLeft, liBottom)), 0, piHeight - liBottom);
			lOOut.Paste(lOSource.Crop(new UWRectInt(liSourceWidth - liRight, liSourceHeight - liBottom, liRight, liBottom)),
				piWidth - liRight, piHeight - liBottom);

			return lOOut;
		}
	}
}
