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
		public static UWPicture BuildShelf(UWTextures pOTextures, out ShelfLayout pOLayout)
		{
			UWPicture lOMain = fMain(pOTextures);
			int liRows = ShelfBottom - ShelfTop;

			UWPicture lOLeft = lOMain.Crop(new UWRectInt(ShelfLeftX0, ShelfTop, ShelfLeftX1 - ShelfLeftX0, liRows));
			UWPicture lORight = lOMain.Crop(new UWRectInt(ShelfRightX0, ShelfTop, ShelfRightX1 - ShelfRightX0, liRows));
			UWPicture lOMiddleSource = lOMain.Crop(new UWRectInt(ShelfLeftX1, ShelfTop, ShelfRightX0 - ShelfLeftX1, liRows));

			for (int y = 0; y < ChainRows; y++)
			{
				for (int x = 0; x < lOMiddleSource.Width; x++)
					lOMiddleSource.Set(x, y, new UWColor32(0, 0, 0, 0));
			}

			int liMiddleWidth = GemWidth + (2 * GemMargin);
			UWPicture lOMiddle = fMirrorTile(lOMiddleSource, liMiddleWidth, liRows, true, false);

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

			int liGemX = lOLeft.Width + GemMargin;
			int liGemY = GemBottom - PedestalRows - GemHeight;

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
