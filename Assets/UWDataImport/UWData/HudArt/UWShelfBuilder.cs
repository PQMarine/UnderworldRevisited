using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FLASKS' SHELF with the gem's pedestal, and the keep masks of the flasks and the gem
	/// (MAIN.BYT, FLASKS.GR, POWER.GR; cleanup 9.11, 2026-10-25: a builder of its own, UWHudArt
	/// forwards to it). The Build methods read the pictures, the Compose methods work on the
	/// pictures given.
	/// </summary>
	public static class UWShelfBuilder
	{
		// ------------------------------------------------- The shelf (MAIN.BYT)

		/// <summary>The flask the left part holds, from its left edge.</summary>
		private const int HealthFlaskX = UWClassicLayout.Flasks.HealthLeft - UWMainFrame.ShelfLeftX0;

		/// <summary>Where MAIN.BYT draws the power gem.</summary>
		private const int GemX = UWClassicLayout.PowerGem.Left;

		private const int GemY = UWClassicLayout.PowerGem.Top;

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
		/// (UWFrameCutBuilder.BuildGemStand).</param>
		public static UWPicture Build(UWTextures pOTextures, out ShelfLayout pOLayout, bool pbGemApart = false)
		{
			return Compose(UWMainFrame.Picture(pOTextures), out pOLayout, pbGemApart);
		}

		public static UWPicture Compose(UWPicture pOMain, out ShelfLayout pOLayout, bool pbGemApart = false)
		{
			UWPicture lOMain = pOMain;
			int liRows = UWMainFrame.ShelfBottom - UWMainFrame.ShelfTop;

			UWPicture lOLeft = lOMain.Crop(new UWRectInt(UWMainFrame.ShelfLeftX0, UWMainFrame.ShelfTop,
				UWMainFrame.ShelfLeftX1 - UWMainFrame.ShelfLeftX0, liRows));
			UWPicture lORight = lOMain.Crop(new UWRectInt(UWMainFrame.ShelfRightX0, UWMainFrame.ShelfTop,
				UWMainFrame.ShelfRightX1 - UWMainFrame.ShelfRightX0, liRows));
			UWPicture lOMiddleSource = lOMain.Crop(new UWRectInt(UWMainFrame.ShelfLeftX1, UWMainFrame.ShelfTop,
				UWMainFrame.ShelfRightX0 - UWMainFrame.ShelfLeftX1, liRows));

			if (!pbGemApart)
				UWPixelOps.Fill(lOMiddleSource, new UWRectInt(0, 0, lOMiddleSource.Width, UWMainFrame.ChainRows), UWPixelOps.Transparent);

			int liMiddleWidth = pbGemApart ? lOMiddleSource.Width : UWMainFrame.GemWidth + (2 * UWHudTuning.Shelf.GemMargin);
			UWPicture lOMiddle = pbGemApart ? lOMiddleSource : UWPixelOps.MirrorTile(lOMiddleSource, liMiddleWidth, liRows, true, false);

			UWPicture lOShelf = new UWPicture(lOLeft.Width + liMiddleWidth + lORight.Width, liRows);
			lOShelf.Paste(lOLeft, 0, 0);
			lOShelf.Paste(lOMiddle, lOLeft.Width, 0);
			lOShelf.Paste(lORight, lOLeft.Width + liMiddleWidth, 0);

			UWPixelOps.Clear(lOShelf, UWMask.FloodFromBorder(lOShelf, UWPixelRules.IsBackdrop));

			for (int y = 0; y < lOShelf.Height; y++)
			{
				for (int x = 0; x < lOShelf.Width; x++)
				{
					UWColor32 lOColour = lOShelf.Get(x, y);

					if (lOColour.A == 0)
						continue;

					bool lbWood = lOColour.R > lOColour.G + UWHudTuning.Shelf.WoodRedOverGreen;
					bool lbDitherDot = y < UWMainFrame.ShelfDitherRows && UWPixelRules.Sum(lOColour) < UWHudTuning.Shelf.DitherDotSum;

					if (lbWood || lbDitherDot)
						lOShelf.Set(x, y, UWPixelOps.Transparent);
				}
			}

			int liGemX = pbGemApart ? -1 : lOLeft.Width + UWHudTuning.Shelf.GemMargin;
			int liGemY = pbGemApart ? -1 : UWHudTuning.Shelf.GemBottom - UWMainFrame.PedestalRows - UWMainFrame.GemHeight;

			if (!pbGemApart)
				lOShelf.Overlay(lOMain.Crop(new UWRectInt(GemX, GemY + UWMainFrame.GemHeight, UWMainFrame.GemWidth, UWMainFrame.PedestalRows)),
					liGemX, liGemY + UWMainFrame.GemHeight);

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

		/// <summary>
		/// THE FLASK STANDING FREE (Classic+, per user 2026-10-10: "the flasks freed one by one"):
		/// BuildFlaskMask, and from UWHudTuning.Shelf.FlaskStoneTop down also the shelf's stone gone
		/// that the flask pictures carry beside and under the gold foot - a flood from the kept
		/// area's edge through pixels that are grey (channels within 16) in every flask picture (red,
		/// blue, green; empty, half, full), so the glass, its fill and the gold foot stay. Rows top
		/// down.
		/// </summary>
		public static bool[] BuildFreeFlaskMask(UWFlasks pOFlasks)
		{
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

			return ComposeFreeFlaskMask(BuildFlaskMask(pOFlasks), lOPictures);
		}

		/// <summary>The free flask's mask from the flask's keep mask (BuildFlaskMask) and the flask
		/// pictures; changes and returns pbKeep.</summary>
		public static bool[] ComposeFreeFlaskMask(bool[] pbKeep, IList<UWPicture> pOPictures)
		{
			bool[] lbKeep = pbKeep;
			IList<UWPicture> lOPictures = pOPictures;

			if (lbKeep == null || lOPictures.Count == 0)
				return lbKeep;

			int liWidth = lOPictures[0].Width;
			int liHeight = lOPictures[0].Height;
			UWMask lOStone = new UWMask(liWidth, liHeight);

			for (int y = UWHudTuning.Shelf.FlaskStoneTop; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					bool lbGrey = true;

					foreach (UWPicture lOPicture in lOPictures)
					{
						UWColor32 lOColour = lOPicture.Get(x, y);
						int liMax = Math.Max(lOColour.R, Math.Max(lOColour.G, lOColour.B));
						int liMin = Math.Min(lOColour.R, Math.Min(lOColour.G, lOColour.B));

						lbGrey &= lOColour.A > 0 && liMax - liMin <= UWHudTuning.Shelf.FlaskGreySpread;
					}

					lOStone[x, y] = lbGrey;
				}
			}

			// The flood starts at the kept stone on the kept area's edge (left, right, below) and runs
			// through kept stone from UWHudTuning.Shelf.FlaskStoneTop down.
			List<int> lOSeeds = new List<int>();

			for (int y = UWHudTuning.Shelf.FlaskStoneTop; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					int liAt = (y * liWidth) + x;

					if (!lbKeep[liAt] || !lOStone[x, y])
						continue;

					if (x == 0 || x == liWidth - 1 || y == liHeight - 1 || !lbKeep[liAt - 1] || !lbKeep[liAt + 1] || !lbKeep[liAt + liWidth])
						lOSeeds.Add(liAt);
				}
			}

			UWMask lOGone = UWMask.Flood(liWidth, liHeight, lOSeeds,
				(piX, piY) => piY >= UWHudTuning.Shelf.FlaskStoneTop && lbKeep[(piY * liWidth) + piX] && lOStone[piX, piY]);

			for (int liAt = 0; liAt < lbKeep.Length; liAt++)
				lbKeep[liAt] &= !lOGone.Bits[liAt];

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

		private static bool[] fKeepMask(IEnumerable<UWTexture> pOTextures)
		{
			List<UWPicture> lOPictures = new List<UWPicture>();

			foreach (UWTexture lOTexture in pOTextures)
			{
				if (lOTexture != null)
					lOPictures.Add(UWPicture.From(lOTexture));
			}

			return ComposeKeepMask(lOPictures);
		}

		/// <summary>The pixels to keep over all the pictures: not the backdrop reached from the border
		/// in at least one of them; null for none.</summary>
		public static bool[] ComposeKeepMask(IEnumerable<UWPicture> pOPictures)
		{
			bool[] lbKeep = null;

			foreach (UWPicture lOPicture in pOPictures)
			{
				bool[] lbBackdrop = UWMask.FloodFromBorder(lOPicture, UWPixelRules.IsBackdrop).Bits;

				if (lbKeep == null)
					lbKeep = new bool[lbBackdrop.Length];

				for (int liAt = 0; liAt < lbKeep.Length && liAt < lbBackdrop.Length; liAt++)
					lbKeep[liAt] |= !lbBackdrop[liAt];
			}

			return lbKeep;
		}
	}
}
