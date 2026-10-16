using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MODERN HUD'S FRAMES, cut from the original graphics (look decided per user on mockups,
	/// 2026-10-03, see Todo "Modern control scheme"), engine-free (moved out of UWModernHudArt
	/// 2026-10-05, per user: reusable for UW2 or another engine):
	///
	///   - the LEATHER of the bags and the action bar: INV.GR image 6, the original's 85 x 41
	///     container panel, stretched to any size from its calm parts only - its highlight streak
	///     and dark patch made a tiling of the whole centre uneven;
	///   - the SLOT CIRCLE: the top right circle of the backpack panel in MAIN.BYT, whose fill is
	///     closest to that leather, cut round;
	///   - the STONE SHELF the flasks stand on, from MAIN.BYT, widened between the flasks so the
	///     power gem fits, with the gem's own pedestal from the frame below it.
	///
	/// Everything is composed from the palette colours (rows top down, as the files store them)
	/// and keyed there; the engine turns the pictures into textures, with the colour help applied.
	/// </summary>
	public static class UWHudArt
	{
		// ------------------------------------------------- The leather (INV.GR 6)

		/// <summary>The panel's frame: 4 columns on the left, 3 on the right, 3 rows on top, 4 at
		/// the bottom - after the two fully transparent columns at the panel's right edge (83 and 84,
		/// LeatherTransparentRight) are cut off; with them the windows showed a see-through strip on
		/// the right (per user, 2026-10-03).</summary>
		public const int LeatherLeft = 4;

		public const int LeatherRight = 3;

		internal const int LeatherTransparentRight = 2;

		public const int LeatherTop = 3;

		public const int LeatherBottom = 4;

		internal const int LeatherImage = 6;

		/// <summary>The most even part of the leather, 40 x 12, no highlight and no dark patch.</summary>
		private static readonly UWRectInt msCalmCentre = new UWRectInt(28, 23, 40, 12);

		/// <summary>The stretch of the top and bottom edge with the least variation.</summary>
		private const int CalmEdgeX0 = 36;

		private const int CalmEdgeX1 = 50;

		/// <summary>The same for the left and right edge.</summary>
		private const int CalmEdgeY0 = 14;

		private const int CalmEdgeY1 = 26;

		// ------------------------------------------------- The slot circle (MAIN.BYT)

		/// <summary>The top right circle of the backpack panel: its fill (106,61,17) is closest to
		/// the leather (92,52,13); the top left one lies in the panel's highlight. 17 x 17 - the
		/// 18th column is already the panel's frame.</summary>
		private static readonly UWRectInt msSlotCircle = new UWRectInt(298, 81, 17, 17);

		private const float SlotCircleRadius = 9f;

		public const int SlotSize = 17;

		// ------------------------------------------------- The shelf (MAIN.BYT)

		private const int ShelfTop = 125;

		private const int ShelfBottom = 166;

		/// <summary>The shelf with the vitality flask (drawn at 248,125), up to the mana flask.</summary>
		private const int ShelfLeftX0 = 237;

		private const int ShelfLeftX1 = 272;

		/// <summary>The shelf with the mana flask (drawn at 284,125).</summary>
		private const int ShelfRightX0 = 284;

		private const int ShelfRightX1 = 318;

		/// <summary>The flask the left part holds, from its left edge.</summary>
		private const int HealthFlaskX = 248 - ShelfLeftX0;

		/// <summary>The rows of the 12 columns between the flasks that carry the chain ring.</summary>
		private const int ChainRows = 15;

		/// <summary>Where MAIN.BYT draws the power gem; its pedestal is the 6 rows below it.</summary>
		private const int GemX = 4;

		private const int GemY = 139;

		private const int GemWidth = 31;

		private const int GemHeight = 12;

		private const int PedestalRows = 6;

		/// <summary>Free columns on each side of the gem in the widened middle.</summary>
		private const int GemMargin = 3;

		/// <summary>The gem's pedestal ends 2 rows below the flask pictures (chosen per user from
		/// three heights).</summary>
		private const int GemBottom = 35;

		/// <summary>Above this row the shelf's back edge was dithered against the black: the loose
		/// dark dots left there are dirt without it.</summary>
		private const int ShelfDitherRows = 26;

		/// <summary>Where the shelf picture puts the flasks and the gem, top-left corners in its
		/// own pixels, rows counted from the top.</summary>
		public struct ShelfLayout
		{
			public int HealthFlaskX;

			public int HealthFlaskY;

			public int ManaFlaskX;

			public int ManaFlaskY;

			public int GemX;

			public int GemY;

			public int Width;

			public int Height;
		}

		// ------------------------------------------------- Pixel work

		/// <summary>Tiles a piece with every second copy flipped, so no seam repeats.</summary>
		private static UWPicture fMirrorTile(UWPicture pOSource, int piWidth, int piHeight, bool pbFlipX, bool pbFlipY)
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

		/// <summary>The original's backdrop behind the flasks and the gem: transparent, black, and
		/// the dark blue-grey (28,28,36) the panels dither with.</summary>
		private static bool fIsBackdrop(UWColor32 pOColour)
		{
			return pOColour.A == 0 || (pOColour.R == pOColour.G && pOColour.R <= 28 && pOColour.B - pOColour.R <= 8);
		}

		/// <summary>Which pixels are backdrop reached from the picture's border - the rest stays.</summary>
		private static bool[] fFloodBackdrop(UWPicture pOPicture)
		{
			bool[] lbBackdrop = new bool[pOPicture.Pixels.Length];
			Stack<int> lOTodo = new Stack<int>();
			int liWidth = pOPicture.Width;
			int liHeight = pOPicture.Height;

			// A point as x and y one after the other, y on top.
			void fPush(int piX, int piY)
			{
				lOTodo.Push(piX);
				lOTodo.Push(piY);
			}

			for (int x = 0; x < liWidth; x++)
			{
				fPush(x, 0);
				fPush(x, liHeight - 1);
			}

			for (int y = 0; y < liHeight; y++)
			{
				fPush(0, y);
				fPush(liWidth - 1, y);
			}

			while (lOTodo.Count > 0)
			{
				int liY = lOTodo.Pop();
				int liX = lOTodo.Pop();

				if (liX < 0 || liY < 0 || liX >= liWidth || liY >= liHeight)
					continue;

				int liIndex = (liY * liWidth) + liX;

				if (lbBackdrop[liIndex] || !fIsBackdrop(pOPicture.Pixels[liIndex]))
					continue;

				lbBackdrop[liIndex] = true;

				fPush(liX + 1, liY);
				fPush(liX - 1, liY);
				fPush(liX, liY + 1);
				fPush(liX, liY - 1);
			}

			return lbBackdrop;
		}

		private static void fClear(UWPicture pOPicture, bool[] pbClear)
		{
			for (int liAt = 0; liAt < pOPicture.Pixels.Length; liAt++)
			{
				if (pbClear[liAt])
					pOPicture.Pixels[liAt] = new UWColor32(0, 0, 0, 0);
			}
		}

		internal static UWPicture fMain(UWTextures pOTextures)
		{
			return UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0));
		}

		// ------------------------------------------------- The pictures

		/// <summary>The leather at any size, its inside the clouds (fClouds); piVariant picks another
		/// part of the cloud field, so neighbouring panels do not start alike.</summary>
		public static UWPicture BuildLeather(UWTextures pOTextures, int piWidth, int piHeight, int piVariant = 0)
		{
			return fLeather(pOTextures, piWidth, piHeight, piVariant);
		}

		/// <summary>A tab of leather, framed on the top, left and bottom and OPEN on the right. Laid
		/// over a panel's left frame (LeatherLeft columns wider than it shows), its top and bottom
		/// frame lines turn into the panel's frame and its leather runs on into the panel's - the
		/// handle is one piece with the panel (per user, 2026-10-03: beside the panel it looked cut
		/// off).</summary>
		public static UWPicture BuildLeatherTab(UWTextures pOTextures, int piWidth, int piHeight)
		{
			UWPicture lOFull = fLeather(pOTextures, piWidth + LeatherRight + LeatherLeft, piHeight, 0);

			return lOFull.Crop(new UWRectInt(0, 0, piWidth, piHeight));
		}

		/// <summary>
		/// The original's inventory page above the backpack - MAIN.BYT x 236 to 317, rows 7 to 81:
		/// the leather with the circles of the shoulders, hands and rings, on which the paperdoll
		/// stands (the character panel of the modern scheme, look per user on a mockup, 2026-10-03).
		/// WIDENED BY FOUR to the backpack window's 86 (per user: then it docks onto it): the two
		/// leather columns inside each border are doubled (PaperdollWidenLeft/Right), so everything
		/// on the page sits PaperdollShift further right.
		/// </summary>
		public static UWPicture BuildPaperdollPage(UWTextures pOTextures)
		{
			UWPicture lOPage = fMain(pOTextures).Crop(PaperdollPage);
			UWPicture lOOut = new UWPicture(PaperdollPageWidth, lOPage.Height);

			for (int x = 0; x < PaperdollPageWidth; x++)
			{
				int liColumn = PageColumnToMain(x) - PaperdollPage.X;

				for (int y = 0; y < lOPage.Height; y++)
					lOOut.Set(x, y, lOPage.Get(liColumn, y));
			}

			return lOOut;
		}

		/// <summary>The page's place in MAIN.BYT (rows from the top).</summary>
		public static readonly UWRectInt PaperdollPage = new UWRectInt(236, 7, 82, 75);

		/// <summary>The widened page: as wide as the backpack window.</summary>
		public const int PaperdollPageWidth = 86;

		/// <summary>Page columns from which a column is doubled: the leather just inside the left
		/// border (MAIN x 239 and 240) and inside the right one (x 310 and 311).</summary>
		private const int PaperdollWidenLeft = 3;

		private const int PaperdollWidenRight = 74;

		/// <summary>How far the left doubling moves what lies on the page.</summary>
		public const int PaperdollShift = 2;

		/// <summary>The MAIN.BYT column a column of the widened page shows.</summary>
		public static int PageColumnToMain(int piColumn)
		{
			int liColumn;

			if (piColumn < PaperdollWidenLeft + 2)
				liColumn = piColumn;
			else if (piColumn < PaperdollWidenLeft + 4)
				liColumn = piColumn - 2;
			else if (piColumn < PaperdollWidenRight + 4)
				liColumn = piColumn - PaperdollShift;
			else
				liColumn = piColumn - 4;

			return PaperdollPage.X + Math.Min(Math.Max(liColumn, 0), PaperdollPage.Width - 1);
		}

		// ------------------------------------------------- The heading strip

		/// <summary>
		/// THE HEADING STRIP (per user on mockups, 2026-10-04): the modern scheme's heading on a strip
		/// of leather at the top centre, the gargoyle's head of the original's frame at the left in its
		/// own colours (burned into the leather it looked like a graphics fault in so few pixels, per
		/// user the same day), its eyes lit by the original's EYES.GR after a hit, the foe's name at
		/// the right. Closed it is only as wide as the heading; while the eyes are lit it opens
		/// as wide as the brand and the foe's name need, the same to both sides (UWModernHud.fUpdateStrip;
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

		/// <summary>The gargoyle's head in MAIN.BYT, its eyes at 130-133 and 142-145, rows 4 and 5.</summary>
		private static readonly UWRectInt msGargoyleHead = new UWRectInt(126, 0, 24, 18);

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
			UWPicture lOStrip = fLeather(pOTextures, piWidth, StripHeight, 4, -(piWidth / 2));

			if (piWidth > piClosedWidth)
				fPasteGargoyle(pOTextures, lOStrip, BrandLeft(piWidth, piOpenHalf), BrandTop);

			return lOStrip;
		}

		/// <summary>The strip with the brand at a left edge of the caller's choosing, also on the
		/// closed strip (the compass variant: centred when closed, moving to the left as it opens).</summary>
		public static UWPicture BuildHeadingStripWithBrandAt(UWTextures pOTextures, int piWidth, int piBrandLeft)
		{
			UWPicture lOStrip = fLeather(pOTextures, piWidth, StripHeight, 4, -(piWidth / 2));

			fPasteGargoyle(pOTextures, lOStrip, piBrandLeft, BrandTop);

			return lOStrip;
		}

		/// <summary>
		/// The head's own pixels in msGargoyleHead (X), without what belongs to the frame: the dark
		/// and blue-grey band beside the jaw, the frame's wooden bar below it except the chin, the
		/// corners (per user, 2026-10-04: dark patches beside the teeth). Worked out by a flood from
		/// the crop's sides and bottom through dark and blue pixels, checked on a render.
		/// </summary>
		private static readonly string[] msGargoyleMask =
		{
			".XXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"...XXXXXXXXXXXXXXXXXX...",
			"...XXXXXXXXXXXXXXXXXX...",
			"...XXXXXXXXXXXXXXXXXX...",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"........XXXXXXXX........",
			"........................"
		};

		/// <summary>Lays the gargoyle's head onto the strip's inside at the given top left - its own
		/// pixels (msGargoyleMask). BuildHeadingStrip leaves it off the closed strip: there its edge
		/// showed a sliver of it (per user, 2026-10-04).</summary>
		private static void fPasteGargoyle(UWTextures pOTextures, UWPicture pOStrip, int piLeft, int piTop)
		{
			UWPicture lOHead = fMain(pOTextures).Crop(msGargoyleHead);
			int liInsideRight = pOStrip.Width - LeatherRight;
			int liInsideBottom = pOStrip.Height - LeatherBottom;

			for (int y = 0; y < lOHead.Height; y++)
			{
				for (int x = 0; x < lOHead.Width; x++)
				{
					UWColor32 lOColour = lOHead.Get(x, y);
					int liAtX = piLeft + x;
					int liAtY = piTop + y;

					if (lOColour.A == 0 || fLuma(lOColour) > 240f || msGargoyleMask[y][x] != 'X'
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
		/// from its own colours (fCloudColours) with a little grain; the frame stays the original's.
		/// Variant C of three offline renders per user: five shades, a dark one among them, as the
		/// light text wants a darker ground; per user the same day for every modern background.
		///
		/// ONE FIXED FIELD, anchored at the inside's top left: every value comes from its coordinates
		/// alone (fHash), so a panel that grows or shrinks shows more or less of the same clouds - a
		/// seed per size drew it anew at every change (per user, the same day: it looked odd).
		/// </summary>
		private static readonly float[] mfCloudCells = { 24f, 10f, 4f };

		/// <summary>The cloud octaves' weights, to the cell sizes above.</summary>
		private static readonly float[] mfCloudWeights = { 0.55f, 0.3f, 0.15f };

		/// <summary>The noise range spread over the shades, and the grain.</summary>
		private const float CloudLow = 0.2f;

		private const float CloudHigh = 0.8f;

		private const float CloudGrain = 0.08f;

		/// <summary>The shades, darkest first: the calm centre's colours, and below them the most
		/// frequent darker colour of the leather's inside (not the near-black of the scratches).</summary>
		internal static List<UWColor32> fCloudColours(UWPicture pOSource)
		{
			List<UWColor32> lOShades = new List<UWColor32>();
			UWPicture lOCalm = pOSource.Crop(msCalmCentre);

			foreach (UWColor32 lOColour in lOCalm.Pixels)
			{
				if (!lOShades.Exists(lOOther => fSame(lOOther, lOColour)))
					lOShades.Add(lOColour);
			}

			lOShades.Sort((lOA, lOB) => fLuma(lOA).CompareTo(fLuma(lOB)));

			if (lOShades.Count == 0)
				return lOShades;

			float lfDarkest = fLuma(lOShades[0]);
			Dictionary<int, int> lOCounts = new Dictionary<int, int>();
			UWColor32 lODark = lOShades[0];
			int liBest = 0;

			for (int y = LeatherTop; y < pOSource.Height - LeatherBottom; y++)
			{
				for (int x = LeatherLeft; x < pOSource.Width - LeatherRight; x++)
				{
					UWColor32 lOColour = pOSource.Get(x, y);
					float lfLuma = fLuma(lOColour);

					if (lOColour.A == 0 || lfLuma >= lfDarkest || lfLuma < lfDarkest * 0.6f)
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

		internal static bool fSame(UWColor32 pOA, UWColor32 pOB)
		{
			return pOA.R == pOB.R && pOA.G == pOB.G && pOA.B == pOB.B && pOA.A == pOB.A;
		}

		internal static float fLuma(UWColor32 pOColour)
		{
			return (0.299f * pOColour.R) + (0.587f * pOColour.G) + (0.114f * pOColour.B);
		}

		/// <summary>Soft clouds of the leather's shades - see the cloud leather above.</summary>
		private static UWPicture fClouds(UWPicture pOSource, int piWidth, int piHeight, int piVariant, int piShiftX = 0)
		{
			UWPicture lOOut = new UWPicture(Math.Max(0, piWidth), Math.Max(0, piHeight));
			List<UWColor32> lOShades = fCloudColours(pOSource);

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
					float lfValue = 0f;

					for (int liOctave = 0; liOctave < mfCloudCells.Length; liOctave++)
						lfValue += mfCloudWeights[liOctave] * fSmoothNoise(liOctave, liX / mfCloudCells[liOctave], liY / mfCloudCells[liOctave]);

					lfValue += (fHash(liX, liY, 99) - 0.5f) * CloudGrain;
					lfValue = fClamp((lfValue - CloudLow) / (CloudHigh - CloudLow), 0f, 0.999f);

					lOOut.Set(x, y, lOShades[(int)(lfValue * lOShades.Count)]);
				}
			}

			return lOOut;
		}

		/// <summary>A value from 0 to 1 that depends on the point and the layer alone (also
		/// UWBackdropArt's, as the noise below).</summary>
		internal static float fHash(int piX, int piY, int piLayer)
		{
			unchecked
			{
				uint luHash = ((uint)piX * 73856093u) ^ ((uint)piY * 19349663u) ^ ((uint)piLayer * 83492791u);

				luHash ^= luHash >> 13;
				luHash *= 0x5bd1e995u;
				luHash ^= luHash >> 15;

				return (luHash & 0xFFFFFF) / 16777216f;
			}
		}

		internal static float fSmoothNoise(int piLayer, float pfX, float pfY)
		{
			int liX = (int)Math.Floor(pfX);
			int liY = (int)Math.Floor(pfY);
			float lfX = fSmoothStep(pfX - liX);
			float lfY = fSmoothStep(pfY - liY);
			float lfTop = fLerp(fHash(liX, liY, piLayer), fHash(liX + 1, liY, piLayer), lfX);
			float lfBottom = fLerp(fHash(liX, liY + 1, piLayer), fHash(liX + 1, liY + 1, piLayer), lfX);

			return fLerp(lfTop, lfBottom, lfY);
		}

		private static float fSmoothStep(float pfT)
		{
			return pfT * pfT * (3f - (2f * pfT));
		}

		/// <summary>As Unity's Mathf.Lerp: t kept to 0..1.</summary>
		private static float fLerp(float pfA, float pfB, float pfT)
		{
			return pfA + ((pfB - pfA) * fClamp(pfT, 0f, 1f));
		}

		internal static float fClamp(float pfValue, float pfMin, float pfMax)
		{
			if (pfValue < pfMin)
				return pfMin;

			return pfValue > pfMax ? pfMax : pfValue;
		}

		private static UWPicture fLeather(UWTextures pOTextures, int piWidth, int piHeight, int piVariant, int piShiftX = 0)
		{
			UWPicture lOPanel = UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.INV, LeatherImage));
			UWPicture lOSource = lOPanel.Crop(new UWRectInt(0, 0, lOPanel.Width - LeatherTransparentRight, lOPanel.Height));
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

			lOOut.Paste(fMirrorTile(lOSource.Crop(lOTop), liInnerWidth, LeatherTop, true, false), LeatherLeft, 0);
			lOOut.Paste(fMirrorTile(lOSource.Crop(lOBottom), liInnerWidth, LeatherBottom, true, false), LeatherLeft, piHeight - LeatherBottom);
			lOOut.Paste(fMirrorTile(lOSource.Crop(lOLeft), LeatherLeft, liInnerHeight, false, true), 0, LeatherTop);
			lOOut.Paste(fMirrorTile(lOSource.Crop(lORight), LeatherRight, liInnerHeight, false, true), piWidth - LeatherRight, LeatherTop);

			lOOut.Paste(lOSource.Crop(new UWRectInt(0, 0, LeatherLeft, LeatherTop)), 0, 0);
			lOOut.Paste(lOSource.Crop(new UWRectInt(liSourceWidth - LeatherRight, 0, LeatherRight, LeatherTop)), piWidth - LeatherRight, 0);
			lOOut.Paste(lOSource.Crop(new UWRectInt(0, liSourceHeight - LeatherBottom, LeatherLeft, LeatherBottom)), 0, piHeight - LeatherBottom);
			lOOut.Paste(lOSource.Crop(new UWRectInt(liSourceWidth - LeatherRight, liSourceHeight - LeatherBottom, LeatherRight, LeatherBottom)),
				piWidth - LeatherRight, piHeight - LeatherBottom);

			return lOOut;
		}

		// ------------------------------------------------- The slot circle and the shelf

		/// <summary>One slot circle, 17 x 17, the corners outside the circle transparent so the
		/// leather shows there.</summary>
		public static UWPicture BuildSlotCircle(UWTextures pOTextures)
		{
			UWPicture lOCircle = fMain(pOTextures).Crop(msSlotCircle);
			float lfCentre = (SlotSize - 1) * 0.5f;

			for (int y = 0; y < lOCircle.Height; y++)
			{
				for (int x = 0; x < lOCircle.Width; x++)
				{
					float lfX = x - lfCentre;
					float lfY = y - lfCentre;

					if ((lfX * lfX) + (lfY * lfY) > SlotCircleRadius * SlotCircleRadius)
						lOCircle.Set(x, y, new UWColor32(0, 0, 0, 0));
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
		public static UWPicture BuildSlotRing(UWTextures pOTextures, List<UWColor32> pOCarve = null)
		{
			UWPicture lOCircle = BuildSlotCircle(pOTextures);
			float lfCentre = (SlotSize - 1) * 0.5f;

			for (int y = 0; y < lOCircle.Height; y++)
			{
				for (int x = 0; x < lOCircle.Width; x++)
				{
					float lfX = x - lfCentre;
					float lfY = y - lfCentre;
					UWColor32 lOPixel = lOCircle.Get(x, y);
					float lfLuma = fLuma(lOPixel);
					float lfSquare = (lfX * lfX) + (lfY * lfY);
					bool lbRing = lOPixel.A > 0 && lfSquare >= 5.5f * 5.5f && (lfLuma <= 60f || lfLuma >= 76f);

					if (!lbRing)
						lOCircle.Set(x, y, lfSquare < 5.5f * 5.5f ? SlotHollow : new UWColor32(0, 0, 0, 0));
					else if (pOCarve != null && pOCarve.Count > 0)
						lOCircle.Set(x, y, lfLuma <= 60f ? pOCarve[0] : pOCarve[pOCarve.Count - 1]);
				}
			}

			return lOCircle;
		}

		/// <summary>The shadow inside a freed slot: black, a fifth opaque.</summary>
		private static readonly UWColor32 SlotHollow = new UWColor32(0, 0, 0, 52);

		/// <summary>The page's circles (centre x, y in the page of MAIN.BYT, before the widening, the
		/// radius of their dark line and how far around it a pixel may stand out): shoulders, hands,
		/// the two ring holes (narrow - the sheen crosses the left one).</summary>
		private static readonly float[,] mfPageCircles =
		{
			{ 16.5f, 13.5f, 7.5f, 1.8f }, { 65.5f, 13.5f, 7.5f, 1.8f }, { 13.5f, 35.5f, 7.5f, 1.8f }, { 67.5f, 35.5f, 7.5f, 1.8f },
			{ 25.5f, 51.5f, 1.8f, 0.9f }, { 56.5f, 51.5f, 1.8f, 0.9f }
		};

		/// <summary>
		/// THE PAPERDOLL PAGE WITHOUT ITS LEATHER (per user, 2026-10-09, with the slot rings): the
		/// widened page's size, transparent but for the circles of the shoulders, hands and rings -
		/// without the grey stone, the place where the original shows the weight (MAIN.BYT 305/62,
		/// UWGameUI.mOWeightLabelPosition; per user, the same day: "remove it too"). The circles are their pixels that stand out from the leather around them
		/// (more than 12 of luma from the median of their 7 x 7 neighbourhood) near their line; they
		/// are taken from the page BEFORE the widening and moved by PaperdollShift, so the right ones
		/// lose the doubled edge the widening gives them on the leather page. With pOCarve the circles
		/// are carved into the back (as BuildSlotRing).
		/// </summary>
		public static UWPicture BuildPaperdollMarks(UWTextures pOTextures, List<UWColor32> pOCarve = null)
		{
			UWPicture lOPage = fMain(pOTextures).Crop(PaperdollPage);
			UWPicture lOOut = new UWPicture(PaperdollPageWidth, lOPage.Height);
			float[] lfLuma = new float[lOPage.Width * lOPage.Height];
			float[] lfWindow = new float[49];

			for (int y = 0; y < lOPage.Height; y++)
			{
				for (int x = 0; x < lOPage.Width; x++)
					lfLuma[(y * lOPage.Width) + x] = fLuma(lOPage.Get(x, y));
			}

			for (int y = 0; y < lOPage.Height; y++)
			{
				for (int x = 0; x < lOPage.Width; x++)
				{
					UWColor32 lOColour = lOPage.Get(x, y);
					bool lbKeep = false;

					for (int liCircle = 0; !lbKeep && liCircle < mfPageCircles.GetLength(0); liCircle++)
					{
						float lfX = x - mfPageCircles[liCircle, 0];
						float lfY = y - mfPageCircles[liCircle, 1];
						float lfRadius = mfPageCircles[liCircle, 2];
						float lfDistance = (float)Math.Sqrt((lfX * lfX) + (lfY * lfY));
						float lfBand = mfPageCircles[liCircle, 3];

						// The hollow inside the large circles, as in a freed slot.
						if (pOCarve != null && lfRadius > 3f && lfDistance < lfRadius - lfBand)
						{
							lOColour = SlotHollow;
							lbKeep = true;
							continue;
						}

						if (lfDistance < lfRadius - lfBand || lfDistance > lfRadius + lfBand)
							continue;

						int liCount = 0;

						for (int liDy = -3; liDy <= 3; liDy++)
						{
							for (int liDx = -3; liDx <= 3; liDx++)
							{
								int liX = Math.Min(Math.Max(x + liDx, 0), lOPage.Width - 1);
								int liY = Math.Min(Math.Max(y + liDy, 0), lOPage.Height - 1);

								lfWindow[liCount++] = lfLuma[(liY * lOPage.Width) + liX];
							}
						}

						Array.Sort(lfWindow, 0, liCount);

						float lfStandOut = lfLuma[(y * lOPage.Width) + x] - lfWindow[liCount / 2];

						lbKeep = Math.Abs(lfStandOut) > 9f;

						if (lbKeep && pOCarve != null && pOCarve.Count > 0)
							lOColour = lfStandOut < 0f ? pOCarve[0] : pOCarve[pOCarve.Count - 1];
					}

					if (lbKeep && x + PaperdollShift < lOOut.Width)
						lOOut.Set(x + PaperdollShift, y, lOColour);
				}
			}

			return lOOut;
		}

		/// <summary>Where the body picture lies on MAIN.BYT (UWModernPanel.msBodyAt).</summary>
		public const int PaperdollBodyX = 260;

		public const int PaperdollBodyY = 11;

		/// <summary>
		/// THE PAPERDOLL'S BODY WITHOUT ITS LEATHER (per user, 2026-10-09, screenshot: on a back of
		/// its own the figure stood in a box of copper). BODIES.GR pictures are opaque, their
		/// background the page itself at PaperdollBodyX/Y - pixel for pixel, but for a drop shadow
		/// along the figure. So it is freed from the page (FreeOnPage).
		/// </summary>
		public static UWPicture FreePaperdollBody(UWTextures pOTextures, UWPicture pOBody)
		{
			return FreeOnPage(pOTextures, pOBody, PaperdollBodyX, PaperdollBodyY);
		}

		/// <summary>The first body picture of CHRBTNS.GR, the character creation's (bodies in the
		/// order of BODIES.GR).</summary>
		private const int CreationBodyImage = 17;

		/// <summary>
		/// THE WHOLE FIGURE - body and armour - without the page it was painted on (per user,
		/// 2026-10-09). Freeing the paperdoll's body from the page by its colours did not come out
		/// clean - shirt and trousers are the page's own browns, the shadow is painted by hand (per
		/// user). The user's idea: THE CHARACTER CREATION SHOWS THE SAME FIGURES (CHRBTNS.GR 17 to
		/// 26) on colour index 0, without the outline and the shadow - the original's own
		/// silhouette. So:
		///   - The body: the pixels inside that silhouette, plus the black pixels of the paperdoll's
		///     picture touching it (the outline). The two lie apart by two columns and a row for the
		///     men, four and two for the women (found by brightness, the mean difference 7 to 9
		///     there against 14 and more one step off - the creation has its own palette).
		///   - The armour in the classic order (helmet, gloves, legs, chest, boots), drawn as it is -
		///     the pieces are transparent around them - but for THE HELMET: the women's helmets paint
		///     the page and its shadow over the long hair they hide (per user: "helmets with the
		///     background worked in"). The helmet is freed from the page by itself (FreeOnPage, its own
		///     transparent pixels taken as black, so they hold the flood like the outline); what stays
		///     is drawn, what goes takes the figure out there too. (Freeing the other pieces by colour
		///     took a leather boot's cuff, per user - leather and page share their browns.)
		/// The pieces are top-down pictures with their places on MAIN.BYT; piOriginX/Y is where the
		/// result lies on MAIN.BYT.
		/// </summary>
		public static UWPicture ComposePaperdoll(UWTextures pOTextures, int piBody, IList<UWPicture> pOArmour, IList<(int X, int Y)> pOAt,
			out int piOriginX, out int piOriginY)
		{
			UWPicture lOMain = fMain(pOTextures);
			UWTexture lOBodySource = pOTextures.GetTextureByType(UWTexture.TextureTypes.BODIES, piBody);
			UWPicture lOBody = UWPicture.From(lOBodySource);
			int liLeft = PaperdollBodyX;
			int liTop = PaperdollBodyY;
			int liRight = PaperdollBodyX + lOBody.Width;
			int liBottom = PaperdollBodyY + lOBody.Height;

			for (int liAt = 0; liAt < pOArmour.Count; liAt++)
			{
				if (pOArmour[liAt] == null)
					continue;

				liLeft = Math.Min(liLeft, pOAt[liAt].X);
				liTop = Math.Min(liTop, pOAt[liAt].Y);
				liRight = Math.Max(liRight, pOAt[liAt].X + pOArmour[liAt].Width);
				liBottom = Math.Max(liBottom, pOAt[liAt].Y + pOArmour[liAt].Height);
			}

			UWPicture lOFigure = new UWPicture(liRight - liLeft, liBottom - liTop);

			// The body inside the creation's silhouette, and its outline.
			bool[] lbInside = new bool[lOBody.Width * lOBody.Height];
			UWTexture lOCreation = null;

			try
			{
				lOCreation = pOTextures.GetTextureByType(UWTexture.TextureTypes.CHRBTNS, CreationBodyImage + piBody);
			}
			catch
			{
				lOCreation = null;
			}

			byte[] lyIndices = lOCreation != null ? lOCreation.GetMainPaletteIndices() : null;

			if (lyIndices != null)
			{
				int liShiftX = piBody < 5 ? 2 : 4;
				int liShiftY = piBody < 5 ? 1 : 2;

				for (int y = 0; y < lOCreation.Height; y++)
				{
					for (int x = 0; x < lOCreation.Width; x++)
					{
						int liX = x + liShiftX;
						int liY = y + liShiftY;

						if (lyIndices[(y * lOCreation.Width) + x] != 0 && liX < lOBody.Width && liY < lOBody.Height)
							lbInside[(liY * lOBody.Width) + liX] = true;
					}
				}

				// The creation's figure is a pixel wider at places (per user: the women's thigh, bodies
				// 5, 7, 8, 9): a pixel at the silhouette's edge that is the page itself is not figure -
				// nor one with no outline before it whose colour the page has within two pixels (the
				// page's sheen moves its colours a little).
				bool fPageNear(int piX, int piY)
				{
					UWColor32 lOA = lOBody.Get(piX, piY);

					for (int liDy = -2; liDy <= 2; liDy++)
					{
						for (int liDx = -2; liDx <= 2; liDx++)
						{
							UWColor32 lOB = lOMain.Get(PaperdollBodyX + piX + liDx, PaperdollBodyY + piY + liDy);

							if (lOA.R == lOB.R && lOA.G == lOB.G && lOA.B == lOB.B)
								return true;
						}
					}

					return false;
				}

				bool fOpenSide(int piX, int piY)
				{
					for (int liDir = 0; liDir < 4; liDir++)
					{
						int liX = piX + (liDir == 0 ? 1 : liDir == 1 ? -1 : 0);
						int liY = piY + (liDir == 2 ? 1 : liDir == 3 ? -1 : 0);

						if (liX < 0 || liY < 0 || liX >= lOBody.Width || liY >= lOBody.Height)
							return true;

						UWColor32 lOC = lOBody.Get(liX, liY);

						if (!lbInside[(liY * lOBody.Width) + liX] && lOC.R + lOC.G + lOC.B >= 30)
							return true;
					}

					return false;
				}

				for (int liPass = 0; liPass < 2; liPass++)
				{
					List<int> lOOut = new List<int>();

					for (int y = 0; y < lOBody.Height; y++)
					{
						for (int x = 0; x < lOBody.Width; x++)
						{
							int liAt = (y * lOBody.Width) + x;

							if (!lbInside[liAt])
								continue;

							bool lbEdge = x == 0 || y == 0 || x == lOBody.Width - 1 || y == lOBody.Height - 1
								|| !lbInside[liAt - 1] || !lbInside[liAt + 1] || !lbInside[liAt - lOBody.Width] || !lbInside[liAt + lOBody.Width];
							UWColor32 lOA = lOBody.Get(x, y);
							UWColor32 lOB = lOMain.Get(PaperdollBodyX + x, PaperdollBodyY + y);

							if (lbEdge && ((lOA.R == lOB.R && lOA.G == lOB.G && lOA.B == lOB.B) || (fOpenSide(x, y) && fPageNear(x, y))))
								lOOut.Add(liAt);
						}
					}

					foreach (int liAt in lOOut)
						lbInside[liAt] = false;
				}
			}
			else
			{
				// Without the creation's picture: the body freed from the page by its colours.
				UWPicture lOFreed = FreeOnPage(pOTextures, lOBody, PaperdollBodyX, PaperdollBodyY);

				for (int liAt = 0; liAt < lbInside.Length; liAt++)
					lbInside[liAt] = lOFreed.Pixels[liAt].A > 0;
			}

			for (int y = 0; y < lOBody.Height; y++)
			{
				for (int x = 0; x < lOBody.Width; x++)
				{
					UWColor32 lOColour = lOBody.Get(x, y);
					bool lbKeep = lbInside[(y * lOBody.Width) + x];

					if (!lbKeep && lOColour.R + lOColour.G + lOColour.B < 30)
					{
						for (int liDy = -1; liDy <= 1 && !lbKeep; liDy++)
						{
							for (int liDx = -1; liDx <= 1 && !lbKeep; liDx++)
							{
								int liX = x + liDx;
								int liY = y + liDy;

								lbKeep = liX >= 0 && liY >= 0 && liX < lOBody.Width && liY < lOBody.Height && lbInside[(liY * lOBody.Width) + liX];
							}
						}
					}

					if (lbKeep)
						lOFigure.Set(PaperdollBodyX - liLeft + x, PaperdollBodyY - liTop + y, lOColour);
				}
			}

			// The armour, each piece freed from the page; the helmet's page takes the figure out.
			for (int liAt = 0; liAt < pOArmour.Count; liAt++)
			{
				UWPicture lOPiece = pOArmour[liAt];

				if (lOPiece == null)
					continue;

				int liX0 = pOAt[liAt].X;
				int liY0 = pOAt[liAt].Y;

				// Only the helmet paints page and shadow; the other pieces are transparent around
				// them already, and freeing them by colour took a leather boot's cuff (per user).
				if (liAt != 0)
				{
					for (int y = 0; y < lOPiece.Height; y++)
					{
						for (int x = 0; x < lOPiece.Width; x++)
						{
							if (lOPiece.Get(x, y).A > 0)
								lOFigure.Set(liX0 - liLeft + x, liY0 - liTop + y, lOPiece.Get(x, y));
						}
					}

					continue;
				}
				// The piece's own transparent pixels count as outline (black): a helmet's face opening
				// filled with the page let the flood eat the brim around it.
				UWPicture lOOnPage = new UWPicture(lOPiece.Width, lOPiece.Height);

				for (int y = 0; y < lOPiece.Height; y++)
				{
					for (int x = 0; x < lOPiece.Width; x++)
						lOOnPage.Set(x, y, lOPiece.Get(x, y).A > 0 ? lOPiece.Get(x, y) : new UWColor32(0, 0, 0, 255));
				}

				UWPicture lOFreed = FreeOnPage(pOTextures, lOOnPage, liX0, liY0, false);

				for (int y = 0; y < lOPiece.Height; y++)
				{
					for (int x = 0; x < lOPiece.Width; x++)
					{
						if (lOPiece.Get(x, y).A == 0)
							continue;

						if (lOFreed.Get(x, y).A > 0)
							lOFigure.Set(liX0 - liLeft + x, liY0 - liTop + y, lOPiece.Get(x, y));
						else if (liAt == 0)
							lOFigure.Set(liX0 - liLeft + x, liY0 - liTop + y, new UWColor32(0, 0, 0, 0));
					}
				}
			}

			piOriginX = liLeft;
			piOriginY = liTop;

			return lOFigure;
		}

		/// <summary>Not taken out (FreeOnPage's state per pixel).</summary>
		private const int Kept = int.MinValue;

		/// <summary>
		/// A picture lying on MAIN.BYT at piMainX/Y freed from the page around it (see
		/// FreePaperdollBody). The SHADOW the original paints along the figure is wider than first
		/// thought and its own colours (per user, the same day: "not quite clean yet"), so in steps:
		///   1. From the edges: every pixel that equals the page; from there two more steps into what
		///      does not (near the page) - but only into light pixels (luma 50 and more): the legs are
		///      outlined in dark brown at places, not black (per user: a piece of a leg went missing).
		///   2. The shadow: it falls to the right, so further steps go only LEFTWARD into it (or up
		///      and down within it), six at most, and only into pixels darker than the page there.
		///   No step enters a black pixel or one SANDWICHED between two black ones (a gap in the
		///   outline); the shadow also stays out of the outline's band (a pixel around every black
		///   one), so it does not slip through wider gaps into the figure.
		///   3. The shadow the band kept: a pixel darker than the page with the taken-out background
		///      on one side and the outline two away on the other, then one away (per user: shadow left
		///      at the shoulder); then any pixel with the outline on one side and the taken-out
		///      background on the opposite side.
		///   4. Enclosed strips of background, between the arms and the body and between the legs
		///      (per user), which no flood from outside reaches: 4-connected pieces between the
		///      outlines of at most 40 pixels and at most 3 wide or high, every pixel the page or
		///      darker than it by more than 20 (the armour's own dark patches are not that dark).
		///   5. Spurs and specks: a pixel with five or more of its eight neighbours gone (six for a
		///      black one) goes too, twice.
		///   6. Of what stays, only the largest 8-connected piece (unless pbLargestOnly is off - an
		///      armour piece may be two gloves or two boots; then the shadow may also start at the
		///      picture's edge, where a piece's painted shadow reaches it).
		/// The shirts and trousers are the page's own browns (per user), so where a gap between the
		/// legs is as wide and as dark as a leg, neither shape nor colour tells them apart.
		/// </summary>
		public static UWPicture FreeOnPage(UWTextures pOTextures, UWPicture pOBody, int piMainX, int piMainY, bool pbLargestOnly = true)
		{
			UWPicture lOMain = fMain(pOTextures);
			int liWidth = pOBody.Width;
			int liHeight = pOBody.Height;
			int[] liDepth = new int[liWidth * liHeight];
			bool[] lbBand = new bool[liWidth * liHeight];
			Queue<int> lOOpen = new Queue<int>();

			for (int liAt = 0; liAt < liDepth.Length; liAt++)
				liDepth[liAt] = Kept;

			bool fPage(int piX, int piY)
			{
				UWColor32 lOA = pOBody.Get(piX, piY);
				UWColor32 lOB = lOMain.Get(piMainX + piX, piMainY + piY);

				return lOA.R == lOB.R && lOA.G == lOB.G && lOA.B == lOB.B;
			}

			bool fBlack(int piX, int piY)
			{
				UWColor32 lOA = pOBody.Get(piX, piY);

				return lOA.R + lOA.G + lOA.B < 30;
			}

			bool fDarker(int piX, int piY)
			{
				return fLuma(pOBody.Get(piX, piY)) < fLuma(lOMain.Get(piMainX + piX, piMainY + piY)) - 6f;
			}

			bool fGone(int piX, int piY)
			{
				return piX < 0 || piY < 0 || piX >= liWidth || piY >= liHeight || liDepth[(piY * liWidth) + piX] != Kept;
			}

			bool fBlackAt(int piX, int piY)
			{
				return piX >= 0 && piY >= 0 && piX < liWidth && piY < liHeight && fBlack(piX, piY);
			}

			bool fSandwiched(int piX, int piY)
			{
				return (fBlackAt(piX + 1, piY) && fBlackAt(piX - 1, piY)) || (fBlackAt(piX, piY + 1) && fBlackAt(piX, piY - 1))
					|| (fBlackAt(piX + 1, piY + 1) && fBlackAt(piX - 1, piY - 1)) || (fBlackAt(piX + 1, piY - 1) && fBlackAt(piX - 1, piY + 1));
			}

			float fBelowPage(int piX, int piY)
			{
				return fLuma(lOMain.Get(piMainX + piX, piMainY + piY)) - fLuma(pOBody.Get(piX, piY));
			}

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					if (!fBlack(x, y))
						continue;

					for (int liDy = -1; liDy <= 1; liDy++)
					{
						for (int liDx = -1; liDx <= 1; liDx++)
						{
							if (x + liDx >= 0 && y + liDy >= 0 && x + liDx < liWidth && y + liDy < liHeight)
								lbBand[((y + liDy) * liWidth) + x + liDx] = true;
						}
					}
				}
			}

			// 1 and 2: the page, the steps near it, the shadow.
			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					if (x != 0 && y != 0 && x != liWidth - 1 && y != liHeight - 1)
						continue;

					if (fPage(x, y))
					{
						liDepth[(y * liWidth) + x] = 0;
						lOOpen.Enqueue((y * liWidth) + x);
					}
					else if (!pbLargestOnly && !fBlack(x, y) && fDarker(x, y))
					{
						// An armour piece's shadow reaching its edge (a woman's helmet paints three
						// or four columns of it at its right).
						liDepth[(y * liWidth) + x] = -1;
						lOOpen.Enqueue((y * liWidth) + x);
					}
				}
			}

			while (lOOpen.Count > 0)
			{
				int liAt = lOOpen.Dequeue();
				int liX = liAt % liWidth;
				int liY = liAt / liWidth;
				int liFrom = liDepth[liAt];

				for (int liDir = 0; liDir < 4; liDir++)
				{
					int liDx = liDir == 0 ? 1 : liDir == 1 ? -1 : 0;
					int liDy = liDir == 2 ? 1 : liDir == 3 ? -1 : 0;
					int liNx = liX + liDx;
					int liNy = liY + liDy;

					if (fGone(liNx, liNy) || fBlack(liNx, liNy))
						continue;

					int liNext = (liNy * liWidth) + liNx;
					int liTo;

					if (liFrom == 0 && fPage(liNx, liNy))
						liTo = 0;
					else if (fSandwiched(liNx, liNy))
						continue;
					else if (liFrom >= 0 && liFrom < 2 && fLuma(pOBody.Get(liNx, liNy)) >= 50f)
						liTo = liFrom + 1;
					else if (lbBand[liNext])
						continue;
					else if ((liDx == -1 || (liDx == 0 && liFrom < 0)) && Math.Abs(liFrom) < 6 && fDarker(liNx, liNy))
						liTo = -(Math.Abs(liFrom) + 1);
					else
						continue;

					liDepth[liNext] = liTo;
					lOOpen.Enqueue(liNext);
				}
			}

			// 3: the shadow the band kept (outline two away, then one away), then the fringe along the
			// outline.
			List<int> lOFringe = new List<int>();

			for (int liReach = 2; liReach >= 1; liReach--)
			{
				lOFringe.Clear();

				for (int y = 1; y < liHeight - 1; y++)
				{
					for (int x = 1; x < liWidth - 1; x++)
					{
						if (fGone(x, y) || fBlack(x, y) || !fDarker(x, y))
							continue;

						for (int liDir = 0; liDir < 4; liDir++)
						{
							int liDx = liDir == 0 ? 1 : liDir == 1 ? -1 : 0;
							int liDy = liDir == 2 ? 1 : liDir == 3 ? -1 : 0;
							int liOutX = x - (liDx * liReach);
							int liOutY = y - (liDy * liReach);
							bool lbBetweenFree = liReach == 1 || (!fBlackAt(x - liDx, y - liDy) && !fGone(x - liDx, y - liDy));

							if (fGone(x + liDx, y + liDy) && fBlackAt(liOutX, liOutY) && lbBetweenFree)
							{
								lOFringe.Add((y * liWidth) + x);
								break;
							}
						}
					}
				}

				foreach (int liAt in lOFringe)
					liDepth[liAt] = -9;
			}

			lOFringe.Clear();

			for (int y = 1; y < liHeight - 1; y++)
			{
				for (int x = 1; x < liWidth - 1; x++)
				{
					if (fGone(x, y) || fBlack(x, y))
						continue;

					if ((fBlack(x - 1, y) && fGone(x + 1, y)) || (fBlack(x + 1, y) && fGone(x - 1, y))
						|| (fBlack(x, y - 1) && fGone(x, y + 1)) || (fBlack(x, y + 1) && fGone(x, y - 1)))
						lOFringe.Add((y * liWidth) + x);
				}
			}

			foreach (int liAt in lOFringe)
				liDepth[liAt] = -9;

			// 4: enclosed strips of background.
			bool[] lbSeen = new bool[liDepth.Length];
			List<int> lOStrip = new List<int>();

			for (int liStart = 0; liStart < liDepth.Length; liStart++)
			{
				if (lbSeen[liStart] || liDepth[liStart] != Kept || fBlack(liStart % liWidth, liStart / liWidth))
					continue;

				lOStrip.Clear();
				lbSeen[liStart] = true;
				lOOpen.Enqueue(liStart);

				int liMinX = int.MaxValue, liMaxX = int.MinValue, liMinY = int.MaxValue, liMaxY = int.MinValue;
				bool lbBackground = true;

				while (lOOpen.Count > 0)
				{
					int liAt = lOOpen.Dequeue();
					int liX = liAt % liWidth;
					int liY = liAt / liWidth;

					lOStrip.Add(liAt);
					liMinX = Math.Min(liMinX, liX);
					liMaxX = Math.Max(liMaxX, liX);
					liMinY = Math.Min(liMinY, liY);
					liMaxY = Math.Max(liMaxY, liY);
					lbBackground &= fPage(liX, liY) || fBelowPage(liX, liY) > 20f;

					for (int liDir = 0; liDir < 4; liDir++)
					{
						int liNx = liX + (liDir == 0 ? 1 : liDir == 1 ? -1 : 0);
						int liNy = liY + (liDir == 2 ? 1 : liDir == 3 ? -1 : 0);

						if (fGone(liNx, liNy) || fBlack(liNx, liNy) || lbSeen[(liNy * liWidth) + liNx])
							continue;

						lbSeen[(liNy * liWidth) + liNx] = true;
						lOOpen.Enqueue((liNy * liWidth) + liNx);
					}
				}

				bool lbNarrow = Math.Min(liMaxX - liMinX + 1, liMaxY - liMinY + 1) <= 3;

				if (lbBackground && lbNarrow && lOStrip.Count <= 40)
				{
					foreach (int liAt in lOStrip)
						liDepth[liAt] = -8;
				}
			}

			// 5: spurs and specks.
			for (int liPass = 0; liPass < 2; liPass++)
			{
				lOFringe.Clear();

				for (int y = 0; y < liHeight; y++)
				{
					for (int x = 0; x < liWidth; x++)
					{
						if (fGone(x, y))
							continue;

						int liAround = 0;

						for (int liDy = -1; liDy <= 1; liDy++)
						{
							for (int liDx = -1; liDx <= 1; liDx++)
							{
								if ((liDx != 0 || liDy != 0) && fGone(x + liDx, y + liDy))
									liAround++;
							}
						}

						if (liAround >= (fBlack(x, y) ? 6 : 5))
							lOFringe.Add((y * liWidth) + x);
					}
				}

				foreach (int liAt in lOFringe)
					liDepth[liAt] = -9;
			}

			// 6: the largest piece of what stays.
			int[] liPiece = new int[liDepth.Length];
			int liBest = 0;
			int liBestSize = 0;
			int liPieces = 0;

			for (int liStart = 0; liStart < liDepth.Length; liStart++)
			{
				if (liDepth[liStart] != Kept || liPiece[liStart] != 0)
					continue;

				int liSize = 0;

				liPieces++;
				liPiece[liStart] = liPieces;
				lOOpen.Enqueue(liStart);

				while (lOOpen.Count > 0)
				{
					int liAt = lOOpen.Dequeue();
					int liX = liAt % liWidth;
					int liY = liAt / liWidth;

					liSize++;

					for (int liDy = -1; liDy <= 1; liDy++)
					{
						for (int liDx = -1; liDx <= 1; liDx++)
						{
							int liNx = liX + liDx;
							int liNy = liY + liDy;

							if (liNx < 0 || liNy < 0 || liNx >= liWidth || liNy >= liHeight)
								continue;

							int liNext = (liNy * liWidth) + liNx;

							if (liDepth[liNext] != Kept || liPiece[liNext] != 0)
								continue;

							liPiece[liNext] = liPieces;
							lOOpen.Enqueue(liNext);
						}
					}
				}

				if (liSize > liBestSize)
				{
					liBestSize = liSize;
					liBest = liPieces;
				}
			}

			UWPicture lOOut = new UWPicture(liWidth, liHeight);

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					int liPieceOf = liPiece[(y * liWidth) + x];

					if (liPieceOf != 0 && (liPieceOf == liBest || !pbLargestOnly))
						lOOut.Set(x, y, pOBody.Get(x, y));
				}
			}

			return lOOut;
		}

		/// <summary>
		/// The stone shelf, without the flasks and the gem themselves (they change; they are laid
		/// on top, keyed with FlaskMask and GemMask) but with the gem's pedestal: the shelf of
		/// MAIN.BYT split between the flasks and widened there with the 12 columns between them,
		/// the chain ring cut off; the black backdrop, the frame's wood at the left end and the
		/// loose dither dots of the back edge keyed out.
		/// </summary>
		/// <param name="pbGemApart">THE GEM APART (Classic+, per user, 2026-10-09: "in the original the
		/// power gem stands alone"): the shelf as the original has it - the two flasks with the chain
		/// ring between them, no gem and no pedestal (GemX/GemY -1); the gem stands on its own
		/// (BuildGemStand).</param>
		public static UWPicture BuildShelf(UWTextures pOTextures, out ShelfLayout pOLayout, bool pbGemApart = false)
		{
			UWPicture lOMain = fMain(pOTextures);
			int liRows = ShelfBottom - ShelfTop;

			UWPicture lOLeft = lOMain.Crop(new UWRectInt(ShelfLeftX0, ShelfTop, ShelfLeftX1 - ShelfLeftX0, liRows));
			UWPicture lORight = lOMain.Crop(new UWRectInt(ShelfRightX0, ShelfTop, ShelfRightX1 - ShelfRightX0, liRows));
			UWPicture lOMiddleSource = lOMain.Crop(new UWRectInt(ShelfLeftX1, ShelfTop, ShelfRightX0 - ShelfLeftX1, liRows));

			if (!pbGemApart)
			{
				for (int y = 0; y < ChainRows; y++)
				{
					for (int x = 0; x < lOMiddleSource.Width; x++)
						lOMiddleSource.Set(x, y, new UWColor32(0, 0, 0, 0));
				}
			}

			int liMiddleWidth = pbGemApart ? lOMiddleSource.Width : GemWidth + (2 * GemMargin);
			UWPicture lOMiddle = pbGemApart ? lOMiddleSource : fMirrorTile(lOMiddleSource, liMiddleWidth, liRows, true, false);

			UWPicture lOShelf = new UWPicture(lOLeft.Width + liMiddleWidth + lORight.Width, liRows);
			lOShelf.Paste(lOLeft, 0, 0);
			lOShelf.Paste(lOMiddle, lOLeft.Width, 0);
			lOShelf.Paste(lORight, lOLeft.Width + liMiddleWidth, 0);

			fClear(lOShelf, fFloodBackdrop(lOShelf));

			for (int y = 0; y < lOShelf.Height; y++)
			{
				for (int x = 0; x < lOShelf.Width; x++)
				{
					UWColor32 lOColour = lOShelf.Get(x, y);

					if (lOColour.A == 0)
						continue;

					bool lbWood = lOColour.R > lOColour.G + 30;
					bool lbDitherDot = y < ShelfDitherRows && lOColour.R + lOColour.G + lOColour.B < 150;

					if (lbWood || lbDitherDot)
						lOShelf.Set(x, y, new UWColor32(0, 0, 0, 0));
				}
			}

			int liGemX = pbGemApart ? -1 : lOLeft.Width + GemMargin;
			int liGemY = pbGemApart ? -1 : GemBottom - PedestalRows - GemHeight;

			if (!pbGemApart)
				lOShelf.Overlay(lOMain.Crop(new UWRectInt(GemX, GemY + GemHeight, GemWidth, PedestalRows)), liGemX, liGemY + GemHeight);

			pOLayout = new ShelfLayout
			{
				HealthFlaskX = HealthFlaskX,
				HealthFlaskY = 0,
				ManaFlaskX = lOLeft.Width + liMiddleWidth,
				ManaFlaskY = 0,
				GemX = liGemX,
				GemY = liGemY,
				Width = lOShelf.Width,
				Height = lOShelf.Height
			};

			return lOShelf;
		}

		/// <summary>The gem with its hexagonal stone collar in MAIN.BYT, from the dome's top (row 139)
		/// down to the collar's lower edge (row 157); the ledge below it is the stone shelf's
		/// (BuildStoneShelf).</summary>
		public const int GemStandX = 1;

		public const int GemStandY = 139;

		private static readonly UWRectInt msGemStand = new UWRectInt(GemStandX, GemStandY, 37, 19);

		/// <summary>
		/// The gem's and its collar's own pixels in msGemStand (X), painted by the user in Paint.NET
		/// on 2026-10-10 (the collar is hexagonal, per user; its grey is the ledge's around it, so no
		/// rule told them apart). Only this mask is in the project, not the picture.
		/// </summary>
		private static readonly string[] msGemStandMask =
		{
			"..............XXXXXXXXXX.............",
			".........XXXXXXXXXXXXXXXXXXX.........",
			"......XXXXXXXXXXXXXXXXXXXXXXXXX......",
			".....XXXXXXXXXXXXXXXXXXXXXXXXXXX.....",
			"....XXXXXXXXXXXXXXXXXXXXXXXXXXXXX....",
			"...XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX...",
			"..XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX..",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX..",
			"...XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX....",
			".....XXXXXXXXXXXXXXXXXXXXXXXXXX......",
			".......XXXXXXXXXXXXXXXXXXXXXX........",
			"........XXXXXXXXXXXXXXXXXXXX........."
		};

		/// <summary>
		/// THE POWER GEM'S OWN STAND (Classic+, per user, 2026-10-09): the gem on its hexagonal
		/// stone collar alone - not on the ledge (per user, the same day: "only on the stone
		/// pedestal"; the ledge is a deco panel of its own, BuildStoneShelf). Freed by
		/// msGemStandMask; the first version cut an ellipse, but the collar is hexagonal (per user,
		/// 2026-10-09). Kept pixels are all opaque (the original's black inside is index 0). The gem
		/// picture itself changes (inactive, charging, full) and is laid on top at piGemX/piGemY,
		/// keyed with GemMask.
		/// </summary>
		public static UWPicture BuildGemStand(UWTextures pOTextures, out int piGemX, out int piGemY)
		{
			UWPicture lOStand = fMain(pOTextures).Crop(msGemStand);

			for (int y = 0; y < lOStand.Height; y++)
			{
				for (int x = 0; x < lOStand.Width; x++)
				{
					UWColor32 lOColour = lOStand.Get(x, y);

					lOStand.Set(x, y, msGemStandMask[y][x] == 'X'
						? new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255)
						: new UWColor32(0, 0, 0, 0));
				}
			}

			piGemX = GemX - msGemStand.X;
			piGemY = GemY - msGemStand.Y;

			return lOStand;
		}

		/// <summary>The stone shelf's source in MAIN.BYT: the whole ledge under the flasks (x 237 to
		/// 317), from where its top is solid stone (row 147; above it the back edge dithers into the
		/// black) to the frame's bottom.</summary>
		private static readonly UWRectInt msStoneLedge = new UWRectInt(237, 147, 81, 19);

		/// <summary>The plain middle between the flasks within the ledge (x 272 to 283), the stone the
		/// flasks' places are filled with.</summary>
		private const int StoneMiddleX = 272 - 237;

		private const int StoneMiddleWidth = 12;

		/// <summary>The ledge's ends kept at both sides of every shelf: its right end, mirrored at the
		/// left (the left end lies under the frame's wood).</summary>
		private const int StoneCapWidth = 6;

		/// <summary>The narrowest stone shelf: its two ends and a piece.</summary>
		public const int StoneShelfMinWidth = 24;

		/// <summary>The pieces' lengths: 10 to 18 columns.</summary>
		private const int StonePieceMin = 10;

		private const int StonePieceRange = 9;

		/// <summary>The columns a new piece lies over the shelf so far; the seam runs through them
		/// along the path of the least difference.</summary>
		private const int StoneOverlap = 3;

		/// <summary>
		/// THE STONE SHELF AS A DECO PANEL (per user, 2026-10-09: "to bring over some of the
		/// original's look we need deco panels - the stone shelf freely placed and sized, without a
		/// function; one can put the power gem or the flasks on it as one likes"). The first try
		/// tiled the 12 columns between the flasks and repeated the same patterns too often (per
		/// user); the second put random pieces of the whole ledge side by side, and the user marked
		/// where they met (2026-10-10): the top rows' dither broke (two dark or two lit pixels side by
		/// side) and the brightness jumped. Now:
		///   - the WHOLE LEDGE under the flasks is the source (81 columns), the flasks' places filled
		///     with the plain middle's stone: their bluish glass, the gold feet, the frame's wood and,
		///     on the top surface (rows 150 to 156), the feet's black outline;
		///   - the shelf is put together from PIECES of it, 10 to 18 columns long, each laid 3 columns
		///     over the shelf so far ("image quilting"): of all pieces (also mirrored) that keep the
		///     dither's checkerboard (source and shelf column of the same parity) one that matches
		///     the overlap closely is picked by a fixed seeded sequence (not the one just used), and
		///     the seam runs through the overlap along the path of the least difference, row by row;
		///   - the ledge's right end (6 columns) at the right, joined the same way, and mirrored at
		///     the left. The same shelf every time; a wider one only grows on the right.
		/// No flood from the border: the rows are all stone, with the original's dithered top edge.
		/// </summary>
		public static UWPicture BuildStoneShelf(UWTextures pOTextures, int piWidth)
		{
			UWPicture lOLedge = fMain(pOTextures).Crop(msStoneLedge);
			int liSourceWidth = lOLedge.Width;
			int liRows = lOLedge.Height;
			bool[,] lbStone = new bool[liSourceWidth, liRows];

			for (int y = 0; y < liRows; y++)
			{
				for (int x = 0; x < liSourceWidth; x++)
				{
					UWColor32 lOColour = lOLedge.Get(x, y);
					int liSum = lOColour.R + lOColour.G + lOColour.B;
					bool lbHole = liSum < 30
						? y >= 3 && y <= 9
						: lOColour.B > lOColour.R + 6 || Math.Abs(lOColour.R - lOColour.G) > 14 || Math.Abs(lOColour.G - lOColour.B) > 14;

					// The back edge's dark dither shade (28/28/36) is bluish too, but stone.
					lbStone[x, y] = !lbHole || (y < 3 && liSum >= 30 && liSum < 120);

					if (!lbHole)
						continue;

					int liFrom = StoneMiddleX + (((x - StoneMiddleX) % StoneMiddleWidth) + StoneMiddleWidth) % StoneMiddleWidth;

					lOLedge.Set(x, y, lOLedge.Get(liFrom, y));
				}
			}

			// All opaque: the frame picture's black (palette index 0) comes transparent, and on the
			// stone it is the original's dark specks.
			for (int y = 0; y < liRows; y++)
			{
				for (int x = 0; x < liSourceWidth; x++)
				{
					UWColor32 lOColour = lOLedge.Get(x, y);

					lOLedge.Set(x, y, new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255));
				}
			}

			int liWidth = Math.Max(StoneShelfMinWidth, piWidth);
			UWPicture lOShelf = new UWPicture(liWidth, liRows);
			uint luState = 0x2545F491u;

			// The left end: the ledge's right end mirrored (the ledge is 81 columns wide, so the
			// mirrored columns keep their parity).
			for (int x = 0; x < StoneCapWidth; x++)
			{
				for (int y = 0; y < liRows; y++)
					lOShelf.Set(x, y, lOLedge.Get(liSourceWidth - 1 - x, y));
			}

			int liFilled = StoneCapWidth;
			int liEnd = liWidth - StoneCapWidth;
			int liLastStart = -StonePieceMin;
			bool lbLastMirrored = false;
			List<int> lOCandidates = new List<int>();
			List<long> lOCosts = new List<long>();

			while (liFilled < liEnd)
			{
				int liLength = StonePieceMin + (int)(fNextRandom(ref luState) % (uint)StonePieceRange);
				int liAt = liFilled - StoneOverlap;

				lOCandidates.Clear();
				lOCosts.Clear();

				for (int liStart = StoneCapWidth; liStart + liLength <= liSourceWidth - StoneCapWidth; liStart++)
				{
					for (int liMirror = 0; liMirror < 2; liMirror++)
					{
						bool lbMirrored = liMirror == 1;
						int liFirst = lbMirrored ? liStart + liLength - 1 : liStart;

						if (((liFirst - liAt) & 1) != 0
							|| (lbMirrored == lbLastMirrored && Math.Abs(liStart - liLastStart) < StonePieceMin))
							continue;

						long llCost = 0;

						for (int k = 0; k < StoneOverlap; k++)
						{
							int liSource = lbMirrored ? liFirst - k : liFirst + k;

							for (int y = 0; y < liRows; y++)
								llCost += fColourDistance(lOShelf.Get(liAt + k, y), lOLedge.Get(liSource, y));
						}

						lOCandidates.Add((liStart * 2) + liMirror);
						lOCosts.Add(llCost);
					}
				}

				// Any of the close matches, so the same overlap does not always call the same piece.
				long llBest = long.MaxValue;

				foreach (long llCost in lOCosts)
					llBest = Math.Min(llBest, llCost);

				long llLimit = llBest + (llBest / 4);
				int liClose = 0;

				foreach (long llCost in lOCosts)
					liClose += llCost <= llLimit ? 1 : 0;

				int liPick = (int)(fNextRandom(ref luState) % (uint)liClose);
				int liChosen = 0;

				for (int i = 0; i < lOCosts.Count; i++)
				{
					if (lOCosts[i] <= llLimit && liPick-- == 0)
					{
						liChosen = lOCandidates[i];
						break;
					}
				}

				liLastStart = liChosen / 2;
				lbLastMirrored = (liChosen & 1) != 0;

				fQuiltPiece(lOShelf, lOLedge, liAt, lbLastMirrored ? liLastStart + liLength - 1 : liLastStart,
					lbLastMirrored ? -1 : 1, liLength);
				liFilled = liAt + liLength;
			}

			// The right end as the ledge has it, with the overlap before it; for a shelf whose width
			// has the other parity than the ledge's the ledge's last column is left off.
			int liCapFirst = liSourceWidth - StoneCapWidth - StoneOverlap - ((liSourceWidth - liWidth) & 1);

			fQuiltPiece(lOShelf, lOLedge, liEnd - StoneOverlap, liCapFirst, 1, StoneOverlap + StoneCapWidth);
			fSynthesiseBand(lOShelf, lOLedge, lbStone);

			return lOShelf;
		}

		/// <summary>The upper band of the ledge (the back edge's dither and the dark slope, rows 147
		/// to 157, down to the flasks' feet): there the flasks stood over most of the ledge, so its
		/// real stone is only the left end, the middle between the flasks and the right end, and
		/// pieces of it repeat whatever they are.</summary>
		private const int StoneBandRows = 11;

		/// <summary>
		/// Grows the upper band between the shelf's ends anew pixel by pixel (texture synthesis by
		/// neighbourhood): column by column from the left, each column from the bottom up, every
		/// pixel takes the ledge pixel of the same row (and column parity, for the dither) whose
		/// surroundings - the pixels already in place within two columns and rows - match best,
		/// one of the three best by a fixed seeded sequence of its own. So the band joins the
		/// pieces below and the ends seamlessly, recombines the little stone there is without a
		/// period, and a wider shelf still only grows on the right. Only the ledge's REAL stone
		/// (pbStone) is drawn from and compared with: the first version also took the flasks'
		/// places, filled with copies of the middle, and so grew the middle's pattern again and
		/// again (per user, 2026-10-10: "in the upper part one clearly sees the pattern repeat").
		/// </summary>
		private static void fSynthesiseBand(UWPicture pOShelf, UWPicture pOLedge, bool[,] pbStone)
		{
			int liWidth = pOShelf.Width;
			int liRows = pOShelf.Height;
			int liSourceWidth = pOLedge.Width;
			bool[,] lbKnown = new bool[liWidth, liRows];
			uint luState = 0x9E3779B9u;
			long[] llBest = new long[3];
			int[] liBest = new int[3];

			for (int x = 0; x < liWidth; x++)
			{
				for (int y = 0; y < liRows; y++)
					lbKnown[x, y] = y >= StoneBandRows || x < StoneCapWidth || x >= liWidth - StoneCapWidth;
			}

			for (int x = StoneCapWidth; x < liWidth - StoneCapWidth; x++)
			{
				for (int y = StoneBandRows - 1; y >= 0; y--)
				{
					int liFound = 0;

					for (int liSource = 2 + ((x - 2) & 1); liSource < liSourceWidth - 2; liSource += 2)
					{
						if (!pbStone[liSource, y])
							continue;

						long llCost = 0;
						int liCompared = 0;

						for (int dy = -2; dy <= 2; dy++)
						{
							int liY = y + dy;

							if (liY < 0 || liY >= liRows)
								continue;

							for (int dx = -2; dx <= 2; dx++)
							{
								int liX = x + dx;

								if ((dx == 0 && dy == 0) || liX < 0 || liX >= liWidth || !lbKnown[liX, liY] || !pbStone[liSource + dx, liY])
									continue;

								llCost += fColourDistance(pOShelf.Get(liX, liY), pOLedge.Get(liSource + dx, liY));
								liCompared++;
							}
						}

						// The average over the pixels compared (their number differs at the stone's
						// edges), so few compared pixels do not look like a good match.
						if (liCompared < 4)
							continue;

						llCost = llCost * 24 / liCompared;

						// Keep the three cheapest, cheapest first.
						int liSlot = Math.Min(liFound, 3);

						while (liSlot > 0 && llBest[liSlot - 1] > llCost)
						{
							if (liSlot < 3)
							{
								llBest[liSlot] = llBest[liSlot - 1];
								liBest[liSlot] = liBest[liSlot - 1];
							}

							liSlot--;
						}

						if (liSlot < 3)
						{
							llBest[liSlot] = llCost;
							liBest[liSlot] = liSource;
						}

						liFound++;
					}

					if (liFound > 0)
						pOShelf.Set(x, y, pOLedge.Get(liBest[(int)(fNextRandom(ref luState) % (uint)Math.Min(liFound, 3))], y));

					lbKnown[x, y] = true;
				}
			}
		}

		/// <summary>Lays piLength source columns (from piFirst, in piStep direction) onto the shelf
		/// at piAt; in the first StoneOverlap columns it takes the new pixels only from the path of
		/// the least difference on (one column per row, moving at most one column from row to
		/// row).</summary>
		private static void fQuiltPiece(UWPicture pOShelf, UWPicture pOLedge, int piAt, int piFirst, int piStep, int piLength)
		{
			int liRows = pOShelf.Height;
			long[,] llPath = new long[liRows, StoneOverlap];

			for (int y = 0; y < liRows; y++)
			{
				for (int k = 0; k < StoneOverlap; k++)
				{
					long llAbove = 0;

					if (y > 0)
					{
						llAbove = llPath[y - 1, k];

						if (k > 0)
							llAbove = Math.Min(llAbove, llPath[y - 1, k - 1]);

						if (k < StoneOverlap - 1)
							llAbove = Math.Min(llAbove, llPath[y - 1, k + 1]);
					}

					llPath[y, k] = llAbove + fColourDistance(pOShelf.Get(piAt + k, y), pOLedge.Get(piFirst + (k * piStep), y));
				}
			}

			int liSeam = 0;

			for (int k = 1; k < StoneOverlap; k++)
			{
				if (llPath[liRows - 1, k] < llPath[liRows - 1, liSeam])
					liSeam = k;
			}

			for (int y = liRows - 1; y >= 0; y--)
			{
				for (int k = liSeam; k < piLength && piAt + k < pOShelf.Width; k++)
					pOShelf.Set(piAt + k, y, pOLedge.Get(piFirst + (k * piStep), y));

				if (y == 0)
					break;

				int liNext = liSeam;

				if (liSeam > 0 && llPath[y - 1, liSeam - 1] < llPath[y - 1, liNext])
					liNext = liSeam - 1;

				if (liSeam < StoneOverlap - 1 && llPath[y - 1, liSeam + 1] < llPath[y - 1, liNext])
					liNext = liSeam + 1;

				liSeam = liNext;
			}
		}

		/// <summary>The squared colour difference of two pixels.</summary>
		private static long fColourDistance(UWColor32 pOA, UWColor32 pOB)
		{
			int liR = pOA.R - pOB.R;
			int liG = pOA.G - pOB.G;
			int liB = pOA.B - pOB.B;

			return (liR * liR) + (liG * liG) + (liB * liB);
		}

		// ------------------------------------------------- The classic frame widened (Classic Wide)

		/// <summary>
		/// THE ORIGINAL'S FRAME (MAIN.BYT) PULLED TO A WIDER SCREEN, "Classic Wide" (per user,
		/// 2026-10-10, the name theirs): the picture cut at fixed places and columns put in, half of
		/// them left of the middle, half right, so the left command column, the right panel with
		/// the flasks, the gargoyle, the compass, the rune hollow and both side bars (the dragons
		/// wind round them, UWHudDragons) keep their look, and the view's hole, the ledge and the
		/// message scroll grow. The cuts, by band of rows:
		///   - the top bar (rows 0 to 18) at x 78 and 198: in the plain bar only, between the
		///     corner ornaments and the gargoyle's claws;
		///   - the view (rows 19 to 127): the hole, nothing to draw;
		///   - the ledge (rows 128 to 167) at x 100 (the left stone, right of the active spell
		///     icons' places, x 52 to 99, left of the compass base's outline at 104) and at x 173
		///     (between the compass base's outline and the rune hollow: the hollow, the right
		///     dragon's head lying over its right end and the bar move together - the first cut
		///     right of the hollow would have split the head, per user);
		///   - the message scroll (rows 168 to 199) at x 82 and 223, the dark paper being uniform.
		/// THE COLUMNS PUT IN ARE GROWN PIXEL BY PIXEL as the stone shelf's band (fSynthesiseBand):
		/// every pixel takes a pixel of the same row from the band's plain source (the left stone,
		/// x 56 to 103; the bar, x 72 to 82 and 194 to 203; the paper, x 40 to 109 and 170 to 229)
		/// whose surroundings match the pixels already in place best - one of the three best by a
		/// seeded sequence -, of the same column parity. The RUST MARKS are left out of the source
		/// (a whole wood row, the ledge's edges, stays): the first mockups copied blocks with their
		/// rust, and the eye caught every shape that came twice (per user, 2026-10-10: "the frame
		/// still has repeating spots - one sees them at once"); grown stone has no period and no
		/// shape to recognise. The picture's black is opaque (the screen is black there); only the
		/// hole is transparent.
		/// </summary>
		public sealed class ClassicWideFrame
		{
			/// <summary>The widened picture, (320 + Extra) x 200.</summary>
			public UWPicture Picture;

			/// <summary>The columns put in, and how many of them left of the middle part.</summary>
			public int Extra;

			public int Left;

			/// <summary>The view's hole in the widened picture.</summary>
			public UWRectInt Hole;

			/// <summary>Where an original pixel column lies in the widened picture: unchanged left of
			/// the band's first cut, Left columns further between the cuts, Extra further right of the
			/// second cut.</summary>
			public int ShiftX(int piX, int piY)
			{
				return ClassicWideShiftX(piX, piY, Extra);
			}
		}

		/// <summary>Where an original pixel column lies in a frame widened by piExtra columns (half of
		/// them left of the middle): unchanged left of its band's first cut, half of piExtra further
		/// between the cuts, piExtra further right of the second cut.</summary>
		public static int ClassicWideShiftX(int piX, int piY, int piExtra)
		{
			int liBand = piY < ClassicWideTopRows ? 0 : piY < ClassicWideLedgeTop ? 1 : piY < ClassicWideScrollTop ? 2 : 3;

			return piX < msClassicWideCuts[liBand, 0] ? 0 : piX < msClassicWideCuts[liBand, 1] ? piExtra / 2 : piExtra;
		}

		/// <summary>The bands' first rows (the top bar from row 0) and the view's hole.</summary>
		public const int ClassicWideTopRows = 19;

		public const int ClassicWideLedgeTop = 128;

		public const int ClassicWideScrollTop = 168;

		public static readonly UWRectInt ClassicWideHole = new UWRectInt(52, 19, 172, 109);

		/// <summary>The camera's area in the classic scheme (UWGameUI: x 50 to 226, rows 17 to 138),
		/// the hole's flood bounded by it.</summary>
		public const int ClassicWideCameraLeft = 50;

		public const int ClassicWideCameraRight = 226;

		public const int ClassicWideCameraTop = 17;

		public const int ClassicWideCameraBottom = 138;

		/// <summary>The two cuts of each band (top bar, view, ledge, scroll).</summary>
		/// The view's band cuts at the hole's edges, where its columns are black from top to bottom
		/// (x 53 to 222): the hole's contents then move with its middle, the bars with their sides
		/// (the classic scheme's parts, UWClassicWide). Columns 223 and 224 carry the right vine's
		/// leaves - a cut at 224 left a piece of them behind (per user's screenshot, 2026-10-10).
		private static readonly int[,] msClassicWideCuts = { { 78, 198 }, { 53, 223 }, { 100, 173 }, { 82, 223 } };

		/// <summary>A band's first (0) or second (1) cut, by a row from the top.</summary>
		public static int ClassicWideCut(int piRow, int piWhich)
		{
			int liBand = piRow < ClassicWideTopRows ? 0 : piRow < ClassicWideLedgeTop ? 1 : piRow < ClassicWideScrollTop ? 2 : 3;

			return msClassicWideCuts[liBand, piWhich];
		}

		/// <summary>Each band's plain source columns, as ranges (first, end).</summary>
		private static readonly int[][] msClassicWideSources =
		{
			new[] { 72, 83, 194, 204 },
			new int[0],
			new[] { 56, 104 },
			new[] { 40, 110, 170, 230 }
		};

		public static ClassicWideFrame BuildClassicWideFrame(UWTextures pOTextures, int piExtra)
		{
			UWPicture lOMain = fMain(pOTextures);
			int liExtra = Math.Max(0, piExtra);
			int liLeft = liExtra / 2;
			int liWidth = lOMain.Width + liExtra;
			int liHeight = lOMain.Height;
			UWPicture lOOut = new UWPicture(liWidth, liHeight);
			bool[,] lbKnown = new bool[liWidth, liHeight];
			int[] liBandTops = { 0, ClassicWideTopRows, ClassicWideLedgeTop, ClassicWideScrollTop, liHeight };
			uint luState = 0xC0FFEE11u;

			for (int liBand = 0; liBand < 4; liBand++)
			{
				int liY0 = liBandTops[liBand];
				int liY1 = liBandTops[liBand + 1];
				int liCutLeft = msClassicWideCuts[liBand, 0];
				int liCutRight = msClassicWideCuts[liBand, 1];

				for (int y = liY0; y < liY1; y++)
				{
					for (int x = 0; x < lOMain.Width; x++)
					{
						int liTo = x + (x < liCutLeft ? 0 : x < liCutRight ? liLeft : liExtra);

						lOOut.Set(liTo, y, lOMain.Get(x, y));
						lbKnown[liTo, y] = true;
					}
				}

				int[] liSources = msClassicWideSources[liBand];

				if (liSources.Length == 0)
					continue;

				fGrowClassicWideStrip(lOOut, lbKnown, lOMain, liCutLeft, liLeft, liY0, liY1, liSources, ref luState);
				fGrowClassicWideStrip(lOOut, lbKnown, lOMain, liCutRight + liLeft, liExtra - liLeft, liY0, liY1, liSources, ref luState);
			}

			// The compass without its needle: MAIN.BYT has the north needle baked in, which the
			// classic scheme covers with the disc pictures (COMPASS.GR 0 to 3, UWHudCompass); the
			// compass element lies on it with its own (per user's screenshot, 2026-10-10: "one sees
			// the baked needle").
			UWTexture lOCompassDisc = pOTextures.GetTextureByType(UWTexture.TextureTypes.COMPASS, 0);

			if (lOCompassDisc != null)
				lOOut.Overlay(UWPicture.From(lOCompassDisc), 112 + ClassicWideShiftX(112, 131, liExtra), 131);

			// The hole: the black reached from the view's middle through black, within the camera's
			// area (the classic scheme's, x 50 to 226 and rows 17 to 138, widened) - not the frame's
			// black outlines on the ledge, which a plain rect would take. All else opaque.
			UWRectInt lOHole = new UWRectInt(ClassicWideHole.X, ClassicWideHole.Y, ClassicWideHole.Width + liExtra, ClassicWideHole.Height);
			bool[] lbHole = new bool[liWidth * liHeight];
			Queue<int> lOQueue = new Queue<int>();
			int liStart = ((lOHole.Y + (lOHole.Height / 2)) * liWidth) + lOHole.X + (lOHole.Width / 2);

			bool fBlack(int piX, int piY)
			{
				UWColor32 lOColour = lOOut.Get(piX, piY);

				return lOColour.R + lOColour.G + lOColour.B < 16;
			}

			lbHole[liStart] = true;
			lOQueue.Enqueue(liStart);

			while (lOQueue.Count > 0)
			{
				int liAt = lOQueue.Dequeue();
				int liX0 = liAt % liWidth;
				int liY0 = liAt / liWidth;

				for (int liStep = 0; liStep < 4; liStep++)
				{
					int liX = liX0 + (liStep == 0 ? 1 : liStep == 1 ? -1 : 0);
					int liY = liY0 + (liStep == 2 ? 1 : liStep == 3 ? -1 : 0);

					if (liX < ClassicWideCameraLeft || liX >= ClassicWideCameraRight + liExtra || liY < ClassicWideCameraTop || liY >= ClassicWideCameraBottom
						|| lbHole[(liY * liWidth) + liX] || !fBlack(liX, liY))
						continue;

					lbHole[(liY * liWidth) + liX] = true;
					lOQueue.Enqueue((liY * liWidth) + liX);
				}
			}

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					UWColor32 lOColour = lOOut.Get(x, y);

					lOOut.Set(x, y, lbHole[(y * liWidth) + x] ? new UWColor32(0, 0, 0, 0) : new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255));
				}
			}

			return new ClassicWideFrame { Picture = lOOut, Extra = liExtra, Left = liLeft, Hole = lOHole };
		}

		/// <summary>A rust mark on the frame's stone or bar: red well over blue and over green.</summary>
		private static bool fIsRust(UWColor32 pOColour)
		{
			return pOColour.R > pOColour.B + 12 && pOColour.R > pOColour.G + 4;
		}

		/// <summary>
		/// Grows piCount columns at piAt of the widened picture, rows piY0 to piY1, from the
		/// original's source ranges: column by column, each from the bottom up, every pixel the
		/// source pixel of the same row and column parity whose known surroundings (within two
		/// columns and rows, the rust left out) match best - one of the three best.
		/// </summary>
		private static void fGrowClassicWideStrip(UWPicture pOOut, bool[,] pbKnown, UWPicture pOMain, int piAt, int piCount, int piY0, int piY1,
			int[] piSources, ref uint puState)
		{
			if (piCount <= 0)
				return;

			List<int> lOColumns = new List<int>();

			for (int liRange = 0; liRange < piSources.Length; liRange += 2)
			{
				for (int x = piSources[liRange]; x < piSources[liRange + 1]; x++)
					lOColumns.Add(x);
			}

			// The source's mask: the rust left out, except in rows that are mostly rust (the
			// ledge's wooden edges) - there everything counts.
			int liRows = piY1 - piY0;
			bool[,] lbStone = new bool[pOMain.Width, liRows];

			for (int y = 0; y < liRows; y++)
			{
				int liRust = 0;

				foreach (int x in lOColumns)
					liRust += fIsRust(pOMain.Get(x, piY0 + y)) ? 1 : 0;

				bool lbWoodRow = liRust * 10 > lOColumns.Count * 6;

				foreach (int x in lOColumns)
					lbStone[x, y] = lbWoodRow || !fIsRust(pOMain.Get(x, piY0 + y));
			}

			bool fInSource(int piX)
			{
				for (int liRange = 0; liRange < piSources.Length; liRange += 2)
				{
					if (piX >= piSources[liRange] && piX < piSources[liRange + 1])
						return true;
				}

				return false;
			}

			long[] llBest = new long[3];
			int[] liBest = new int[3];

			for (int x = piAt; x < piAt + piCount; x++)
			{
				for (int y = piY1 - 1; y >= piY0; y--)
				{
					int liFound = 0;

					foreach (int liSource in lOColumns)
					{
						if (((liSource - x) & 1) != 0 || !lbStone[liSource, y - piY0])
							continue;

						long llCost = 0;
						int liCompared = 0;

						for (int dy = -2; dy <= 2; dy++)
						{
							int liY = y + dy;

							if (liY < piY0 || liY >= piY1)
								continue;

							for (int dx = -2; dx <= 2; dx++)
							{
								int liX = x + dx;
								int liSX = liSource + dx;

								if ((dx == 0 && dy == 0) || liX < 0 || liX >= pOOut.Width || !pbKnown[liX, liY]
									|| !fInSource(liSX) || !lbStone[liSX, liY - piY0])
									continue;

								llCost += fColourDistance(pOOut.Get(liX, liY), pOMain.Get(liSX, liY));
								liCompared++;
							}
						}

						if (liCompared < 4)
							continue;

						llCost = llCost * 24 / liCompared;

						int liSlot = Math.Min(liFound, 3);

						while (liSlot > 0 && llBest[liSlot - 1] > llCost)
						{
							if (liSlot < 3)
							{
								llBest[liSlot] = llBest[liSlot - 1];
								liBest[liSlot] = liBest[liSlot - 1];
							}

							liSlot--;
						}

						if (liSlot < 3)
						{
							llBest[liSlot] = llCost;
							liBest[liSlot] = liSource;
						}

						liFound++;
					}

					if (liFound > 0)
						pOOut.Set(x, y, pOMain.Get(liBest[(int)(fNextRandom(ref puState) % (uint)Math.Min(liFound, 3))], y));

					pbKnown[x, y] = true;
				}
			}
		}

		/// <summary>A small fixed random sequence (xorshift), the same on every run and engine.</summary>
		private static uint fNextRandom(ref uint puState)
		{
			puState ^= puState << 13;
			puState ^= puState >> 17;
			puState ^= puState << 5;

			return puState;
		}

		/// <summary>
		/// Which pixels of a flask picture to keep: not the backdrop reached from its border.
		/// Taken from the empty and the full flask together - the fill and the bubbles only ever
		/// paint inside the glass, so every flask picture shares this outline. Rows top down.
		/// </summary>
		public static bool[] BuildFlaskMask(UWFlasks pOFlasks)
		{
			return fKeepMask(new[]
			{
				pOFlasks.GetFlask(UWFlasks.FlaskTypeEnum.Red, 0f),
				pOFlasks.GetFlask(UWFlasks.FlaskTypeEnum.Red, 1f)
			});
		}

		/// <summary>The flask pictures' rows from which down the shelf's stone shows beside and
		/// under the flask's foot.</summary>
		private const int FlaskStoneTop = 24;

		/// <summary>
		/// THE FLASK STANDING FREE (Classic+, per user 2026-10-10: "the flasks freed one by one"):
		/// BuildFlaskMask, and from FlaskStoneTop down also the shelf's stone gone that the flask
		/// pictures carry beside and under the gold foot - a flood from the kept area's edge through
		/// pixels that are grey (channels within 16) in every flask picture (red, blue, green;
		/// empty, half, full), so the glass, its fill and the gold foot stay. Rows top down.
		/// </summary>
		public static bool[] BuildFreeFlaskMask(UWFlasks pOFlasks)
		{
			bool[] lbKeep = BuildFlaskMask(pOFlasks);
			List<UWPicture> lOPictures = new List<UWPicture>();

			foreach (UWFlasks.FlaskTypeEnum leType in new[] { UWFlasks.FlaskTypeEnum.Red, UWFlasks.FlaskTypeEnum.Blue, UWFlasks.FlaskTypeEnum.Green })
			{
				foreach (float lfFill in new[] { 0f, 0.5f, 1f })
				{
					UWTexture lOTexture = pOFlasks.GetFlask(leType, lfFill);

					if (lOTexture != null)
						lOPictures.Add(UWPicture.From(lOTexture));
				}
			}

			if (lbKeep == null || lOPictures.Count == 0)
				return lbKeep;

			int liWidth = lOPictures[0].Width;
			int liHeight = lOPictures[0].Height;
			bool[] lbStone = new bool[liWidth * liHeight];

			for (int y = FlaskStoneTop; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					bool lbGrey = true;

					foreach (UWPicture lOPicture in lOPictures)
					{
						UWColor32 lOColour = lOPicture.Get(x, y);
						int liMax = Math.Max(lOColour.R, Math.Max(lOColour.G, lOColour.B));
						int liMin = Math.Min(lOColour.R, Math.Min(lOColour.G, lOColour.B));

						lbGrey &= lOColour.A > 0 && liMax - liMin <= 16;
					}

					lbStone[(y * liWidth) + x] = lbGrey;
				}
			}

			Queue<int> lOQueue = new Queue<int>();

			for (int y = FlaskStoneTop; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					int liAt = (y * liWidth) + x;

					if (!lbKeep[liAt] || !lbStone[liAt])
						continue;

					bool lbEdge = x == 0 || x == liWidth - 1 || y == liHeight - 1
						|| !lbKeep[liAt - 1] || !lbKeep[liAt + 1] || !lbKeep[liAt + liWidth];

					if (!lbEdge)
						continue;

					lbKeep[liAt] = false;
					lOQueue.Enqueue(liAt);
				}
			}

			while (lOQueue.Count > 0)
			{
				int liAt = lOQueue.Dequeue();
				int liX0 = liAt % liWidth;
				int liY0 = liAt / liWidth;

				for (int liStep = 0; liStep < 4; liStep++)
				{
					int liX = liX0 + (liStep == 0 ? 1 : liStep == 1 ? -1 : 0);
					int liY = liY0 + (liStep == 2 ? 1 : liStep == 3 ? -1 : 0);
					int liNext = (liY * liWidth) + liX;

					if (liX < 0 || liX >= liWidth || liY < FlaskStoneTop || liY >= liHeight || !lbKeep[liNext] || !lbStone[liNext])
						continue;

					lbKeep[liNext] = false;
					lOQueue.Enqueue(liNext);
				}
			}

			return lbKeep;
		}

		/// <summary>The same for the power gem, over all its fourteen pictures: a pixel goes only
		/// where every one of them shows backdrop.</summary>
		public static bool[] BuildGemMask(UWPowerGem pOGem)
		{
			List<UWTexture> lOAll = new List<UWTexture> { pOGem.Inactive, pOGem.Unpowered };

			lOAll.AddRange(pOGem.Powering);
			lOAll.AddRange(pOGem.FullyPowered);

			return fKeepMask(lOAll);
		}

		private static bool[] fKeepMask(IEnumerable<UWTexture> pOPictures)
		{
			bool[] lbKeep = null;

			foreach (UWTexture lOTexture in pOPictures)
			{
				if (lOTexture == null)
					continue;

				bool[] lbBackdrop = fFloodBackdrop(UWPicture.From(lOTexture));

				if (lbKeep == null)
					lbKeep = new bool[lbBackdrop.Length];

				for (int liAt = 0; liAt < lbKeep.Length && liAt < lbBackdrop.Length; liAt++)
					lbKeep[liAt] |= !lbBackdrop[liAt];
			}

			return lbKeep;
		}

		// ------------------------------------------------- The original's conversation (CONV.BYT)

		/// <summary>The conversation's top in CONV.BYT: the two name plates, the portraits' frames
		/// and the two trade areas between them (x 43 to 232, rows 0 to 46).</summary>
		public static readonly UWRectInt ConversationHeader = new UWRectInt(43, 0, 190, 47);

		/// <summary>The parchment under it, its rolled rims included, and the ends of its two wooden
		/// rollers over the view's bars (x 42 to 233, rows 47 to 135; the bars and the vines on them
		/// go).</summary>
		public static readonly UWRectInt ConversationParchment = new UWRectInt(42, 47, 192, 89);

		/// <summary>The side columns of that crop where the bars lie (10 at either side), and the
		/// rows the rollers' ends take there: the upper roller 47 to 56 with the chain it hangs on
		/// (rows 47 to 54, from the conversation's top down over the roller), the lower 127 to 133.</summary>
		private const int ParchmentSide = 10;

		private static readonly (int Top, int Bottom)[] msRollerRows = { (47 - 47, 56 - 47), (127 - 47, 133 - 47) };

		private const int ChainBottom = 54 - 47;

		private const int UpperWoodTop = 50 - 47;

		/// <summary>The parchment's plain colour (#8C6854) and where CONV.BYT's sample text lies on
		/// it (x 56 to 218, rows 52 to 130).</summary>
		private static readonly UWColor32 ParchmentColour = new UWColor32(140, 104, 84, 255);

		private static readonly UWRectInt msParchmentText = new UWRectInt(56, 52, 163, 79);

		/// <summary>The rows of the parchment's rolled rims (with the rollers' ends) kept at the top and the bottom when it is
		/// made lower or taller (BuildConversationParchment).</summary>
		private const int ParchmentRim = 14;

		/// <summary>The name plates' text rows and colours: CONV.BYT's sample names (#8C8CA8) on the
		/// plates' brown (#48280C).</summary>
		private static readonly UWColor32 NamePlateColour = new UWColor32(72, 40, 12, 255);

		/// <summary>The two portraits' places (34 x 34), filled with black under the real ones.</summary>
		private static readonly UWRectInt[] msPortraitPlaces = { new UWRectInt(45, 11, 34, 34), new UWRectInt(197, 11, 34, 34) };

		/// <summary>
		/// THE ORIGINAL'S CONVERSATION TOP (Classic+, per user 2026-10-10: "free the conversation
		/// UI"): CONV.BYT's top block - the file is a sample conversation, so its names ("Derek",
		/// "Tyrone Pop") go (the plates' text colour turned into the plates' brown) and its two
		/// portraits too (black under the real ones); the black reached from the block's edge
		/// becomes transparent. The names, portraits and traded things are drawn on it
		/// (UWModernConversation, original look).
		/// </summary>
		public static UWPicture BuildConversationHeader(UWTextures pOTextures)
		{
			UWPicture lOConv = UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.CONV, 0));
			UWPicture lOHeader = lOConv.Crop(ConversationHeader);

			for (int y = 1; y <= 7; y++)
			{
				for (int x = 0; x < lOHeader.Width; x++)
				{
					UWColor32 lOColour = lOHeader.Get(x, y);

					if (lOColour.A > 0 && lOColour.R == 140 && lOColour.G == 140 && lOColour.B == 168)
						lOHeader.Set(x, y, NamePlateColour);
				}
			}

			foreach (UWRectInt lOPlace in msPortraitPlaces)
			{
				for (int y = lOPlace.Y; y < lOPlace.Y + lOPlace.Height; y++)
				{
					for (int x = lOPlace.X; x < lOPlace.X + lOPlace.Width; x++)
						lOHeader.Set(x - ConversationHeader.X, y - ConversationHeader.Y, new UWColor32(0, 0, 0, 255));
				}
			}

			fClearEdgeBlack(lOHeader);

			return lOHeader;
		}

		/// <summary>
		/// THE ORIGINAL'S CONVERSATION PARCHMENT (Classic+, per user 2026-10-10: "only free the
		/// scroll" - the vines beside it belong to the view's bars, as the dragons): CONV.BYT's
		/// parchment with its rolled rims, its sample text painted over in the plain colour, the
		/// black of the frame around it transparent. piRows other than the original's 89: the rims'
		/// 14 rows kept at both ends and the plain middle repeated (the answers' parchment is a
		/// lower one, per user the same day: "the area of the player's lines gets a parchment
		/// scroll, always the same size").
		/// </summary>
		public static UWPicture BuildConversationParchment(UWTextures pOTextures, int piRows, bool pbKnobRollers = false)
		{
			UWPicture lOConv = UWPicture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.CONV, 0));

			for (int y = msParchmentText.Y; y < msParchmentText.Y + msParchmentText.Height; y++)
			{
				for (int x = msParchmentText.X; x < msParchmentText.X + msParchmentText.Width; x++)
					lOConv.Set(x, y, ParchmentColour);
			}

			UWPicture lOFull = lOConv.Crop(ConversationParchment);
			UWPicture lOSource = lOFull.Crop(new UWRectInt(0, 0, lOFull.Width, lOFull.Height));

			// The frame's grey stone at the top right corner (the parchment has no neutral grey), and
			// the side columns with the bars, the brackets and the vines.
			for (int y = 0; y < lOFull.Height; y++)
			{
				for (int x = 0; x < lOFull.Width; x++)
				{
					UWColor32 lOColour = lOFull.Get(x, y);
					bool lbSide = x < ParchmentSide || x >= lOFull.Width - ParchmentSide;
					bool lbGrey = lOColour.A > 0 && lOColour.R == lOColour.G && lOColour.G == lOColour.B && lOColour.R > 20;

					if (lbSide || lbGrey)
						lOFull.Set(x, y, new UWColor32(0, 0, 0, 0));
				}
			}

			fClearEdgeBlack(lOFull);

			// THE ROLLERS' ENDS (per user, 2026-10-10: "take the wooden rollers along", "they are
			// outlined in black", "the upper ones hang on a chain"): in the side columns their wood
			// (warm: red over blue by more than 30, over green), the upper ones' chain and the black
			// touching either; the bars and the vines left out.
			foreach ((int liTop, int liBottom) in msRollerRows)
			{
				for (int y = liTop; y <= liBottom; y++)
				{
					for (int x = 0; x < lOFull.Width; x++)
					{
						if (x >= ParchmentSide && x < lOFull.Width - ParchmentSide)
							continue;

						UWColor32 lOColour = lOSource.Get(x, y);
						bool lbChainRow = liTop == 0 && y <= ChainBottom;
						// The upper roller's wood from row 50: above it the bar's copper edge.
						bool lbWoodRow = liTop != 0 || y >= UpperWoodTop;
						bool lbKeep = (lbWoodRow && fIsWood(lOColour)) || (lbChainRow && fIsChain(lOColour));

						if (!lbKeep && lOColour.R + lOColour.G + lOColour.B < 16)
						{
							for (int liStep = 0; liStep < 4 && !lbKeep; liStep++)
							{
								int liX = x + (liStep == 0 ? 1 : liStep == 1 ? -1 : 0);
								int liY = y + (liStep == 2 ? 1 : liStep == 3 ? -1 : 0);

								lbKeep = liX >= 0 && liY >= liTop && liX < lOFull.Width && liY <= liBottom
									&& (((liTop != 0 || liY >= UpperWoodTop) && fIsWood(lOSource.Get(liX, liY)))
										|| (liTop == 0 && liY <= ChainBottom && fIsChain(lOSource.Get(liX, liY))));
							}
						}

						if (lbKeep)
							lOFull.Set(x, y, new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255));
					}
				}
			}

			// A black outline round the rollers' ends and the chains (per user, 2026-10-10: "in the
			// original the chain runs out into the black around it" - the answers' parchment's chain
			// hangs right under the history's lower roller): every empty side pixel touching one of
			// them, also diagonally, within the rollers' rows.
			UWPicture lOEnds = lOFull.Crop(new UWRectInt(0, 0, lOFull.Width, lOFull.Height));

			// Only round their coloured pixels: where the original's black edge already is, nothing
			// more (per user, 2026-10-10).
			bool fEnd(int piX, int piY)
			{
				if (piX < 0 || piY < 0 || piX >= lOEnds.Width || piY >= lOEnds.Height
					|| (piX >= ParchmentSide && piX < lOEnds.Width - ParchmentSide))
					return false;

				UWColor32 lOColour = lOEnds.Get(piX, piY);

				return lOColour.A > 0 && lOColour.R + lOColour.G + lOColour.B >= 16;
			}

			for (int y = 0; y < lOFull.Height; y++)
			{
				// Within the rollers' rows only: under the lower ones a row too many (per user).
				if (!((y >= msRollerRows[0].Top && y <= msRollerRows[0].Bottom) || (y >= msRollerRows[1].Top && y <= msRollerRows[1].Bottom)))
					continue;

				for (int x = 0; x < lOFull.Width; x++)
				{
					if ((x >= ParchmentSide && x < lOFull.Width - ParchmentSide) || lOEnds.Get(x, y).A > 0)
						continue;

					bool lbTouches = false;

					for (int dy = -1; dy <= 1 && !lbTouches; dy++)
					{
						for (int dx = -1; dx <= 1 && !lbTouches; dx++)
							lbTouches = (dx != 0 || dy != 0) && fEnd(x + dx, y + dy);
					}

					if (lbTouches)
						lOFull.Set(x, y, new UWColor32(0, 0, 0, 255));
				}
			}

			int liRows = Math.Max(2 * ParchmentRim + 1, piRows);

			if (liRows == lOFull.Height && !pbKnobRollers)
				return lOFull;

			UWPicture lOOut = new UWPicture(lOFull.Width, liRows);
			int liMiddle = lOFull.Height / 2;

			for (int y = 0; y < liRows; y++)
			{
				int liFrom = y < ParchmentRim ? y
					: y >= liRows - ParchmentRim ? lOFull.Height - (liRows - y)
					: liMiddle;

				for (int x = 0; x < lOFull.Width; x++)
					lOOut.Set(x, y, lOFull.Get(x, liFrom));
			}

			if (pbKnobRollers)
			{
				// Above: the parchment's chain and roller give way to the message scroll's upper
				// roller with its own chain (per user: "it has a chain too, it only looks a little
				// different"), from the picture's top.
				fLayKnobRollers(pOTextures, lOOut, 0, 0, ParchmentRim - 1, KnobUpperTop, KnobUpperRows);
				fLayKnobRollers(pOTextures, lOOut, liRows - (lOFull.Height - msRollerRows[1].Top) - (KnobRollerOutlineTop - KnobRollerTop),
					liRows - (lOFull.Height - msRollerRows[1].Top) - 1, liRows - 1, KnobRollerTop, KnobRollerRows);
			}

			return lOOut;
		}

		/// <summary>The message scroll's lower rollers in MAIN.BYT, knob to rod (x 1 to 10 and 310 to
		/// 319), and the rows from the one above their top outline to the one under the bottom
		/// one (189 to 197); their top outline (190) lies on the parchment roller's top row.</summary>
		private const int KnobRollerLeftX = 1;

		private const int KnobRollerRightX = 310;

		private const int KnobRollerTop = 189;

		private const int KnobRollerRows = 9;

		private const int KnobRollerOutlineTop = 190;

		/// <summary>The message scroll's upper rollers with the chain each hangs on, chain top to
		/// roller bottom (rows 167 to 174; the same columns).</summary>
		private const int KnobUpperTop = 167;

		private const int KnobUpperRows = 8;

		/// <summary>
		/// THE ANSWERS' PARCHMENT WITH THE MESSAGE SCROLL'S KNOB ROLLERS (per user, 2026-10-10: "the
		/// knob rollers of the message scroll" - in the original the answers stand in that scroll;
		/// the history's parchment above keeps its own): the side columns' rows piClearFrom to
		/// piClearTo cleared (the parchment's roller, above also its chain) and the message scroll's
		/// rows piSourceTop on (piSourceRows of them) laid there from row piAt, knob to rod in the 10
		/// side columns: below its lower rollers, above its upper ones with their chains.
		/// </summary>
		private static void fLayKnobRollers(UWTextures pOTextures, UWPicture pOParchment, int piAt, int piClearFrom, int piClearTo,
			int piSourceTop, int piSourceRows)
		{
			UWPicture lOMain = fMain(pOTextures);
			int liWidth = pOParchment.Width;

			for (int y = Math.Max(0, piClearFrom); y <= piClearTo && y < pOParchment.Height; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					if (y >= 0 && (x < ParchmentSide || x >= liWidth - ParchmentSide))
						pOParchment.Set(x, y, new UWColor32(0, 0, 0, 0));
				}
			}

			for (int liRow = 0; liRow < piSourceRows; liRow++)
			{
				int liY = piAt + liRow;

				if (liY < 0 || liY >= pOParchment.Height)
					continue;

				for (int liColumn = 0; liColumn < ParchmentSide; liColumn++)
				{
					UWColor32 lOLeft = lOMain.Get(KnobRollerLeftX + liColumn, piSourceTop + liRow);
					UWColor32 lORight = lOMain.Get(KnobRollerRightX + liColumn, piSourceTop + liRow);

					if (lOLeft.A > 0)
						pOParchment.Set(liColumn, liY, new UWColor32(lOLeft.R, lOLeft.G, lOLeft.B, 255));

					if (lORight.A > 0)
						pOParchment.Set(liWidth - ParchmentSide + liColumn, liY, new UWColor32(lORight.R, lORight.G, lORight.B, 255));
				}
			}
		}

		/// <summary>The upper rollers' chain (per user, 2026-10-10: "the upper ones hang on a chain"):
		/// its cool links - bluish grey, the top ones blue-green (5C6C70, 405458, 38484C; per user,
		/// the same day, they were missing) - blue over red, green not under red by more than 20;
		/// not the frame's neutral grey stone beside it (per user: a pixel too many).</summary>
		private static bool fIsChain(UWColor32 pOColour)
		{
			return pOColour.A > 0 && pOColour.B > pOColour.R && pOColour.G >= pOColour.R && pOColour.G - pOColour.R <= 20
				&& pOColour.R + pOColour.G + pOColour.B > 100;
		}

		/// <summary>
		/// A picture a pixel larger each way, and with pbOutline a black outline in that room and in
		/// its gaps: every empty pixel touching a coloured one, also diagonally - not round black,
		/// where the original's edge already is (the conversation's pieces in the original's look,
		/// per user 2026-10-10: "with the outline it looks better", as an option); without it the
		/// black edge the pieces bring goes as well.
		/// </summary>
		public static UWPicture PadAndOutline(UWPicture pOPicture, bool pbOutline)
		{
			UWPicture lOOut = new UWPicture(pOPicture.Width + 2, pOPicture.Height + 2);

			lOOut.Overlay(pOPicture, 1, 1);

			UWPicture lOShape = lOOut.Crop(new UWRectInt(0, 0, lOOut.Width, lOOut.Height));

			// Without the outline no black edge at all (per user, 2026-10-10: "with Off remove the
			// whole pixel of black all round, the rollers' too"): every black pixel touching the
			// empty outside, also diagonally, goes - once.
			if (!pbOutline)
			{
				for (int y = 0; y < lOOut.Height; y++)
				{
					for (int x = 0; x < lOOut.Width; x++)
					{
						UWColor32 lOColour = lOShape.Get(x, y);

						if (lOColour.A == 0 || lOColour.R + lOColour.G + lOColour.B >= 16)
							continue;

						bool lbEdge = false;

						for (int dy = -1; dy <= 1 && !lbEdge; dy++)
						{
							for (int dx = -1; dx <= 1 && !lbEdge; dx++)
							{
								int liX = x + dx;
								int liY = y + dy;

								lbEdge = (dx != 0 || dy != 0) && (liX < 0 || liY < 0 || liX >= lOShape.Width || liY >= lOShape.Height
									|| lOShape.Get(liX, liY).A == 0);
							}
						}

						if (lbEdge)
							lOOut.Set(x, y, new UWColor32(0, 0, 0, 0));
					}
				}

				return lOOut;
			}

			bool fColoured(int piX, int piY)
			{
				if (piX < 0 || piY < 0 || piX >= lOShape.Width || piY >= lOShape.Height)
					return false;

				UWColor32 lOColour = lOShape.Get(piX, piY);

				return lOColour.A > 0 && lOColour.R + lOColour.G + lOColour.B >= 16;
			}

			for (int y = 0; y < lOOut.Height; y++)
			{
				for (int x = 0; x < lOOut.Width; x++)
				{
					if (lOShape.Get(x, y).A > 0)
						continue;

					bool lbTouches = false;

					for (int dy = -1; dy <= 1 && !lbTouches; dy++)
					{
						for (int dx = -1; dx <= 1 && !lbTouches; dx++)
							lbTouches = (dx != 0 || dy != 0) && fColoured(x + dx, y + dy);
					}

					if (lbTouches)
						lOOut.Set(x, y, new UWColor32(0, 0, 0, 255));
				}
			}

			return lOOut;
		}

		/// <summary>The rollers' wood: warm - red over blue by more than 10, not under green, not
		/// black (per user, 2026-10-10: a stricter 30 left holes, the dark browns 24/18/0C and
		/// 24/14/0C).</summary>
		private static bool fIsWood(UWColor32 pOColour)
		{
			return pOColour.A > 0 && pOColour.R - pOColour.B > 10 && pOColour.R >= pOColour.G && pOColour.R + pOColour.G + pOColour.B > 40;
		}

		/// <summary>The black (or transparent) reached from a picture's edge through black becomes
		/// transparent; black inside, an outline or a gap between parts, stays.</summary>
		private static void fClearEdgeBlack(UWPicture pOPicture)
		{
			int liWidth = pOPicture.Width;
			int liHeight = pOPicture.Height;
			bool[] lbDone = new bool[liWidth * liHeight];
			Queue<int> lOQueue = new Queue<int>();

			bool fBlack(int piX, int piY)
			{
				UWColor32 lOColour = pOPicture.Get(piX, piY);

				return lOColour.A == 0 || lOColour.R + lOColour.G + lOColour.B < 16;
			}

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					if ((x == 0 || y == 0 || x == liWidth - 1 || y == liHeight - 1) && fBlack(x, y))
					{
						lbDone[(y * liWidth) + x] = true;
						lOQueue.Enqueue((y * liWidth) + x);
					}
				}
			}

			while (lOQueue.Count > 0)
			{
				int liAt = lOQueue.Dequeue();
				int liX0 = liAt % liWidth;
				int liY0 = liAt / liWidth;

				pOPicture.Set(liX0, liY0, new UWColor32(0, 0, 0, 0));

				for (int liStep = 0; liStep < 4; liStep++)
				{
					int liX = liX0 + (liStep == 0 ? 1 : liStep == 1 ? -1 : 0);
					int liY = liY0 + (liStep == 2 ? 1 : liStep == 3 ? -1 : 0);

					if (liX < 0 || liY < 0 || liX >= liWidth || liY >= liHeight || lbDone[(liY * liWidth) + liX] || !fBlack(liX, liY))
						continue;

					lbDone[(liY * liWidth) + liX] = true;
					lOQueue.Enqueue((liY * liWidth) + liX);
				}
			}

			// Opaque elsewhere: the picture's black (palette index 0) comes transparent.
			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					if (lbDone[(y * liWidth) + x])
						continue;

					UWColor32 lOColour = pOPicture.Get(x, y);

					pOPicture.Set(x, y, new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255));
				}
			}
		}

		// ------------------------------------------------- The rune hollow (MAIN.BYT)

		/// <summary>The hollow for the prepared runes in the frame's lower edge, with a pixel or two
		/// of its slate rim all round.</summary>
		public const int RuneHollowX = 173;

		public const int RuneHollowY = 136;

		private static readonly UWRectInt msRuneHollow = new UWRectInt(RuneHollowX, RuneHollowY, 49, 19);

		/// <summary>Where the first prepared rune lies in it (the frame draws them from 176/138,
		/// UWHudRunes), and the step to the next.</summary>
		public const int RuneHollowRuneX = 176 - 173;

		public const int RuneHollowRuneY = 138 - 136;

		public const int RuneHollowPitch = 15;

		/// <summary>
		/// THE RUNE HOLLOW AS AN ELEMENT (Classic+, per user 2026-10-10: "the hollow with the
		/// prepared runes"): the recess of the frame's lower edge, a rectangle, so cut out as it is
		/// with its slate rim - no mask; all opaque. pbOutline: a black pixel all round (per user,
		/// the same day, as an option, as the compass disc's), the picture a pixel larger each way.
		/// </summary>
		public static UWPicture BuildRuneHollow(UWTextures pOTextures, bool pbOutline)
		{
			UWPicture lOHollow = fMain(pOTextures).Crop(msRuneHollow);

			for (int y = 0; y < lOHollow.Height; y++)
			{
				for (int x = 0; x < lOHollow.Width; x++)
				{
					UWColor32 lOColour = lOHollow.Get(x, y);

					lOHollow.Set(x, y, new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255));
				}
			}

			if (!pbOutline)
				return lOHollow;

			UWPicture lOFramed = new UWPicture(lOHollow.Width + 2, lOHollow.Height + 2);
			UWColor32 lOBlack = new UWColor32(0, 0, 0, 255);

			for (int y = 0; y < lOFramed.Height; y++)
			{
				for (int x = 0; x < lOFramed.Width; x++)
					lOFramed.Set(x, y, lOBlack);
			}

			lOFramed.Overlay(lOHollow, 1, 1);

			return lOFramed;
		}

		// ------------------------------------------------- The compass on its stone disc (MAIN.BYT, COMPASS.GR)

		/// <summary>The compass on its stone disc: its picture's top left in MAIN.BYT (the cross's
		/// top, row 131, down to the pedestal's foot, row 166; the disc's left to its right edge),
		/// with a pixel of room left, right and below for the outline.</summary>
		public const int CompassDiscX = CompassMaskX - 1;

		public const int CompassDiscY = 131;

		/// <summary>Where msCompassDiscMask starts in MAIN.BYT.</summary>
		private const int CompassMaskX = 107;

		/// <summary>
		/// The disc's own pixels in that picture (X and F), painted by the user in Paint.NET on
		/// 2026-10-10 over a 1:1 picture of the original compass; F marks where the user retouched
		/// the cross and the disc's slate rim away to bare granite (per user, the same day: "the
		/// stone disc with the compass needle retouched away"). Only this mask is in the project -
		/// the retouched picture is not (original art): the F pixels are grown anew from the
		/// disc's own granite (BuildCompassDisc).
		/// </summary>
		private static readonly string[] msCompassDiscMask =
		{
			"...............................................................",
			"...............................................................",
			"...............................................................",
			"...............................................................",
			"...............................................................",
			"...................XXXXXFFFFFFFFFFFFFFXXXXXX...................",
			"............XXXXFFFFXXXFFFFFFFFFFFFFFFXFXXXXFFXXXXX............",
			"........XXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXFXXXX........",
			"....XXFXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXFXX....",
			"..XXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXX..",
			".XXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXX.",
			"XXXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXX",
			"XXXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXX",
			"XXXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXXX",
			"XXXXXXFFFXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXXX",
			"XXXXXXXXXFXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXFFFXXXXXXXXX",
			"XXXXXXXXXXXXXFFFFFFFXXXFFFFFFFFFFFFFFFFXFXXFFFFXXXXFXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXFXXXXXFFFFFFFFFFFFFFFXXXXXXFXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFXFXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXFXFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXFXFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXX",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXFFFFXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			"..XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			"...XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX...",
			".....XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.....",
			".........XXXXX........XXXXXXXXXXXXXXXXXXX........XXXXX.........",
			"........................XXXXXXXXXXXXXXX........................"
		};

		/// <summary>The rows of the disc picture whose stone is the disc's top (MAIN.BYT down to row
		/// 156): the F pixels draw from there only, not from the pedestal's lighter front.</summary>
		private const int CompassDiscTopRows = 156 - CompassDiscY + 1;

		/// <summary>
		/// THE COMPASS ON ITS STONE DISC (Classic+, per user 2026-10-10: the compass freed with its
		/// stone disc, "then we can show the movement arrows on the compass's stone disc"): the
		/// frame's compass pedestal with the disc picture COMPASS.GR 0, freed by msCompassDiscMask,
		/// the arrows on its front kept. The cross and the rim under it (F) are grown anew: on the
		/// top edge in the colour of the nearest outline pixel the disc kept, the rest from the
		/// disc's own granite, pixel by pixel - those first that have the most finished neighbours -
		/// each taking the granite pixel (X, on the disc's top, within six rows) whose surroundings
		/// within two pixels match best, one of the three best by a fixed seeded sequence. The
		/// turning cross (UWHudCompass.BuildComposite, freed) is laid on top of it per step.
		/// pbOutline: THE BLACK OUTLINE the original's disc has in the frame, which the mask leaves
		/// out - one pixel all round (per user, 2026-10-10, as an option).
		/// </summary>
		public static UWPicture BuildCompassDisc(UWTextures pOTextures, bool pbOutline)
		{
			int liWidth = msCompassDiscMask[0].Length;
			int liHeight = msCompassDiscMask.Length;
			UWPicture lODisc = fMain(pOTextures).Crop(new UWRectInt(CompassMaskX, CompassDiscY, liWidth, liHeight));
			UWTexture lOStone = pOTextures.GetTextureByType(UWTexture.TextureTypes.COMPASS, 0);

			if (lOStone != null)
				lODisc.Overlay(UWPicture.From(lOStone), 112 - CompassMaskX, 131 - CompassDiscY);

			bool[,] lbKnown = new bool[liWidth, liHeight];
			List<int> lOTodo = new List<int>();
			List<int> lOSources = new List<int>();

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					char lcMask = msCompassDiscMask[y][x];
					UWColor32 lOColour = lODisc.Get(x, y);

					if (lcMask == '.')
					{
						lODisc.Set(x, y, new UWColor32(0, 0, 0, 0));
						continue;
					}

					// Opaque: the frame picture's black (palette index 0) comes transparent.
					lODisc.Set(x, y, new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255));

					if (lcMask == 'F')
						lOTodo.Add((y * liWidth) + x);
					else
						lbKnown[x, y] = true;
				}
			}

			// The sources: the disc top's granite - not its dark outline at the top edge, not the
			// darker rim, else they spread into the middle as streaks.
			for (int y = 0; y < CompassDiscTopRows; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					UWColor32 lOColour = lODisc.Get(x, y);

					if (msCompassDiscMask[y][x] == 'X' && !fCompassDiscEdge(x, y) && lOColour.R + lOColour.G + lOColour.B >= CompassGraniteMin)
						lOSources.Add((y * liWidth) + x);
				}
			}

			// The top edge first: an F pixel with nothing above it takes the colour of the nearest
			// edge pixel the disc kept (its dark outline).
			foreach (int liAt in lOTodo.ToArray())
			{
				int liX = liAt % liWidth;
				int liY = liAt / liWidth;

				if (!fCompassDiscEdge(liX, liY))
					continue;

				int liNearest = -1;

				for (int liDistance = 1; liDistance < liWidth && liNearest < 0; liDistance++)
				{
					foreach (int liSide in new[] { -1, 1 })
					{
						int liSX = liX + (liSide * liDistance);

						for (int liSY = Math.Max(0, liY - 1); liSY <= Math.Min(liHeight - 1, liY + 1) && liNearest < 0; liSY++)
						{
							if (liSX >= 0 && liSX < liWidth && msCompassDiscMask[liSY][liSX] == 'X' && fCompassDiscEdge(liSX, liSY))
								liNearest = (liSY * liWidth) + liSX;
						}
					}
				}

				if (liNearest >= 0)
					lODisc.Set(liX, liY, lODisc.Get(liNearest % liWidth, liNearest / liWidth));

				lbKnown[liX, liY] = true;
				lOTodo.Remove(liAt);
			}

			uint luState = 0x5BD1E995u;
			long[] llBest = new long[3];
			int[] liBest = new int[3];

			while (lOTodo.Count > 0)
			{
				// The pixels with the most finished neighbours first, an onion from the edge in.
				int liMost = -1;

				foreach (int liAt in lOTodo)
					liMost = Math.Max(liMost, fKnownAround(lbKnown, liAt % liWidth, liAt / liWidth));

				List<int> lOBatch = lOTodo.FindAll(liAt => fKnownAround(lbKnown, liAt % liWidth, liAt / liWidth) == liMost);

				foreach (int liAt in lOBatch)
				{
					int liX = liAt % liWidth;
					int liY = liAt / liWidth;
					int liFound = 0;

					foreach (int liSource in lOSources)
					{
						int liSourceX = liSource % liWidth;
						int liSourceY = liSource / liWidth;

						if (Math.Abs(liSourceY - liY) > 6)
							continue;

						long llCost = 0;
						int liCompared = 0;

						for (int dy = -2; dy <= 2; dy++)
						{
							for (int dx = -2; dx <= 2; dx++)
							{
								int liTX = liX + dx;
								int liTY = liY + dy;
								int liSX = liSourceX + dx;
								int liSY = liSourceY + dy;

								if ((dx == 0 && dy == 0) || liTX < 0 || liTY < 0 || liTX >= liWidth || liTY >= liHeight
									|| liSX < 0 || liSY < 0 || liSX >= liWidth || liSY >= liHeight
									|| !lbKnown[liTX, liTY] || !lbKnown[liSX, liSY])
									continue;

								llCost += fColourDistance(lODisc.Get(liTX, liTY), lODisc.Get(liSX, liSY));
								liCompared++;
							}
						}

						if (liCompared < 3)
							continue;

						llCost = llCost * 24 / liCompared;

						// Keep the three cheapest, cheapest first.
						int liSlot = Math.Min(liFound, 3);

						while (liSlot > 0 && llBest[liSlot - 1] > llCost)
						{
							if (liSlot < 3)
							{
								llBest[liSlot] = llBest[liSlot - 1];
								liBest[liSlot] = liBest[liSlot - 1];
							}

							liSlot--;
						}

						if (liSlot < 3)
						{
							llBest[liSlot] = llCost;
							liBest[liSlot] = liSource;
						}

						liFound++;
					}

					if (liFound > 0)
					{
						int liPick = liBest[(int)(fNextRandom(ref luState) % (uint)Math.Min(liFound, 3))];

						lODisc.Set(liX, liY, lODisc.Get(liPick % liWidth, liPick / liWidth));
					}

					lbKnown[liX, liY] = true;
					lOTodo.Remove(liAt);
				}
			}

			// The room for the outline, and the outline: every empty pixel touching the disc, also
			// diagonally.
			UWPicture lOOut = new UWPicture(liWidth + 2, liHeight + 1);

			lOOut.Overlay(lODisc, CompassMaskX - CompassDiscX, 0);

			if (!pbOutline)
				return lOOut;

			UWPicture lOShape = lOOut.Crop(new UWRectInt(0, 0, lOOut.Width, lOOut.Height));
			UWColor32 lOBlack = new UWColor32(0, 0, 0, 255);

			for (int y = 0; y < lOOut.Height; y++)
			{
				for (int x = 0; x < lOOut.Width; x++)
				{
					if (lOShape.Get(x, y).A != 0)
						continue;

					bool lbTouches = false;

					for (int dy = -1; dy <= 1 && !lbTouches; dy++)
					{
						for (int dx = -1; dx <= 1 && !lbTouches; dx++)
						{
							int liX = x + dx;
							int liY = y + dy;

							lbTouches = liX >= 0 && liY >= 0 && liX < lOOut.Width && liY < lOOut.Height && lOShape.Get(liX, liY).A != 0;
						}
					}

					if (lbTouches)
						lOOut.Set(x, y, lOBlack);
				}
			}

			return lOOut;
		}

		/// <summary>The darkest granite the F pixels draw from (the sum of the channels); the
		/// outline and the rim are darker.</summary>
		private const int CompassGraniteMin = 150;

		/// <summary>Whether a disc pixel lies on its top edge: nothing of the disc right above it.</summary>
		private static bool fCompassDiscEdge(int piX, int piY)
		{
			return msCompassDiscMask[piY][piX] != '.' && (piY == 0 || msCompassDiscMask[piY - 1][piX] == '.');
		}

		/// <summary>The finished pixels among a pixel's eight neighbours.</summary>
		private static int fKnownAround(bool[,] pbKnown, int piX, int piY)
		{
			int liCount = 0;

			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					int liX = piX + dx;
					int liY = piY + dy;

					if ((dx != 0 || dy != 0) && liX >= 0 && liY >= 0 && liX < pbKnown.GetLength(0) && liY < pbKnown.GetLength(1) && pbKnown[liX, liY])
						liCount++;
				}
			}

			return liCount;
		}

		/// <summary>The easy-movement arrow at a pixel of the disc picture (rows from the top), -1
		/// for none - the original's own click areas (UWClickRules.EasyArrows, y counted from the
		/// bottom of the 200-row screen).</summary>
		public static int CompassDiscArrowAt(int piX, int piY)
		{
			int liX = CompassDiscX + piX;
			int liY = 199 - (CompassDiscY + piY);

			for (int liArrow = 0; liArrow < UWClickRules.EasyArrows.Length; liArrow++)
			{
				if (UWClickRules.EasyArrows[liArrow].Contains(liX, liY))
					return liArrow;
			}

			return -1;
		}

		// ------------------------------------------------- The compass's cross, freed (COMPASS.GR)

		/// <summary>
		/// THE COMPASS'S CROSS WITHOUT ITS STONE (per user, 2026-10-09: the compass shall stand free in
		/// the modern interface, "the compass has a black edge, except north, which has a red pixel").
		/// The four disc pictures (COMPASS.GR 0-3) are opaque 52x26 and carry the cross in four turns
		/// over the disc's slate rim and granite; what belongs to the cross is the LARGEST connected
		/// piece of WARM pixels (gold and brown: red minus blue over 25) and the PURE BLACK pixels
		/// touching it (the outline is 0/0/0; the stone's darkest greys are 28 to 48). Grey and blue
		/// stone drops out, also inside the rings of the turned crosses, and so do the rust spots on
		/// the rim, which do not touch the cross. Worked out on the user's pictures the same day.
		/// Pixels top-down, as UWTexture.GetUWColor32 gives them; true for the cross.
		/// </summary>
		public static bool[] CompassCrossMask(UWColor32[] pyPixels, int piWidth, int piHeight)
		{
			int liCount = piWidth * piHeight;
			bool[] lbWarm = new bool[liCount];
			bool[] lbSeen = new bool[liCount];
			bool[] lbBody = new bool[liCount];
			System.Collections.Generic.List<int> lOBest = new System.Collections.Generic.List<int>();
			System.Collections.Generic.List<int> lOPiece = new System.Collections.Generic.List<int>();
			System.Collections.Generic.Stack<int> lOStack = new System.Collections.Generic.Stack<int>();

			for (int liAt = 0; liAt < liCount; liAt++)
				lbWarm[liAt] = pyPixels[liAt].A > 0 && pyPixels[liAt].R - pyPixels[liAt].B > 25;

			for (int liStart = 0; liStart < liCount; liStart++)
			{
				if (!lbWarm[liStart] || lbSeen[liStart])
					continue;

				lOPiece.Clear();
				lOStack.Push(liStart);
				lbSeen[liStart] = true;

				while (lOStack.Count > 0)
				{
					int liAt = lOStack.Pop();
					int liX = liAt % piWidth;
					int liY = liAt / piWidth;

					lOPiece.Add(liAt);

					if (liX > 0) fVisit(liAt - 1, lbWarm, lbSeen, lOStack);
					if (liX < piWidth - 1) fVisit(liAt + 1, lbWarm, lbSeen, lOStack);
					if (liY > 0) fVisit(liAt - piWidth, lbWarm, lbSeen, lOStack);
					if (liY < piHeight - 1) fVisit(liAt + piWidth, lbWarm, lbSeen, lOStack);
				}

				if (lOPiece.Count > lOBest.Count)
				{
					lOBest.Clear();
					lOBest.AddRange(lOPiece);
				}
			}

			foreach (int liAt in lOBest)
				lbBody[liAt] = true;

			bool[] lbMask = new bool[liCount];

			for (int liY = 0; liY < piHeight; liY++)
			{
				for (int liX = 0; liX < piWidth; liX++)
				{
					int liAt = (liY * piWidth) + liX;

					if (lbBody[liAt])
					{
						lbMask[liAt] = true;
						continue;
					}

					if (!fIsPureBlack(pyPixels[liAt]))
						continue;

					for (int liDy = -1; liDy <= 1 && !lbMask[liAt]; liDy++)
					{
						for (int liDx = -1; liDx <= 1; liDx++)
						{
							int liNx = liX + liDx;
							int liNy = liY + liDy;

							if (liNx >= 0 && liNx < piWidth && liNy >= 0 && liNy < piHeight && lbBody[(liNy * piWidth) + liNx])
							{
								lbMask[liAt] = true;
								break;
							}
						}
					}
				}
			}

			return lbMask;
		}

		/// <summary>The needle tips (COMPASS.GR 4-19) are small opaque pictures with the disc's stone
		/// around the red point: only their red and their pure black belong to the tip.</summary>
		public static bool CompassTipKeeps(UWColor32 pOPixel)
		{
			return pOPixel.A > 0 && (fIsPureBlack(pOPixel) || (pOPixel.R > 100 && pOPixel.R - pOPixel.G > 50));
		}

		private static bool fIsPureBlack(UWColor32 pOPixel)
		{
			return pOPixel.A > 0 && pOPixel.R + pOPixel.G + pOPixel.B < 10;
		}

		private static void fVisit(int piAt, bool[] pbWarm, bool[] pbSeen, System.Collections.Generic.Stack<int> pOStack)
		{
			if (pbWarm[piAt] && !pbSeen[piAt])
			{
				pbSeen[piAt] = true;
				pOStack.Push(piAt);
			}
		}
	}
}
