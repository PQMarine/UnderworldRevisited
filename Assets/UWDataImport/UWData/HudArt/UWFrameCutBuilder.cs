using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// PIECES CUT OUT OF THE FRAME AS THEY ARE: the power gem's stand and the rune hollow (MAIN.BYT;
	/// cleanup 9.11, 2026-10-25: a builder of its own, UWHudArt forwards to it). The Build methods
	/// read MAIN.BYT, the Compose methods work on the frame picture given.
	/// </summary>
	public static class UWFrameCutBuilder
	{
		// ------------------------------------------------- The power gem's stand (MAIN.BYT)

		/// <summary>The gem with its hexagonal stone collar in MAIN.BYT (UWMainFrame.GemStand); the
		/// ledge below it is the stone shelf's (UWStoneShelfBuilder).</summary>
		public const int GemStandX = UWMainFrame.GemStandX;

		public const int GemStandY = UWMainFrame.GemStandY;

		/// <summary>
		/// THE POWER GEM'S OWN STAND (Classic+, per user, 2026-10-09): the gem on its hexagonal
		/// stone collar alone - not on the ledge (per user, the same day: "only on the stone
		/// pedestal"; the ledge is a deco panel of its own, UWStoneShelfBuilder). Freed by
		/// UWHudMasks.GemStand; the first version cut an ellipse, but the collar is hexagonal (per
		/// user, 2026-10-09). Kept pixels are all opaque (the original's black inside is index 0).
		/// The gem picture itself changes (inactive, charging, full) and is laid on top at
		/// piGemX/piGemY, keyed with GemMask.
		/// </summary>
		public static UWPicture BuildGemStand(UWTextures pOTextures, out int piGemX, out int piGemY)
		{
			return ComposeGemStand(UWMainFrame.Picture(pOTextures), out piGemX, out piGemY);
		}

		public static UWPicture ComposeGemStand(UWPicture pOMain, out int piGemX, out int piGemY)
		{
			UWMaskText lOMask = UWHudMasks.GemStand;
			UWPicture lOStand = pOMain.Crop(lOMask.Place);

			UWPixelOps.MakeOpaque(lOStand);
			UWPixelOps.Clear(lOStand, lOMask.Where('.'));

			piGemX = UWClassicLayout.PowerGem.Left - lOMask.Place.X;
			piGemY = UWClassicLayout.PowerGem.Top - lOMask.Place.Y;

			return lOStand;
		}

		// ------------------------------------------------- The rune hollow (MAIN.BYT)

		/// <summary>The hollow for the prepared runes in the frame's lower edge, with a pixel or two
		/// of its slate rim all round.</summary>
		public const int RuneHollowX = UWClassicLayout.RuneHollow.Left;

		public const int RuneHollowY = UWClassicLayout.RuneHollow.Top;

		private static readonly UWRectInt msRuneHollow = new UWRectInt(RuneHollowX, RuneHollowY,
			UWClassicLayout.RuneHollow.Width, UWClassicLayout.RuneHollow.Height);

		/// <summary>Where the first prepared rune lies in it (the frame draws them from 176/138,
		/// UWHudRunes), and the step to the next.</summary>
		public const int RuneHollowRuneX = UWClassicLayout.RuneHollow.RuneLeft - RuneHollowX;

		public const int RuneHollowRuneY = UWClassicLayout.RuneHollow.RuneTop - RuneHollowY;

		public const int RuneHollowPitch = UWClassicLayout.RuneHollow.RunePitch;

		/// <summary>
		/// THE RUNE HOLLOW AS AN ELEMENT (Classic+, per user 2026-10-10: "the hollow with the
		/// prepared runes"): the recess of the frame's lower edge, a rectangle, so cut out as it is
		/// with its slate rim - no mask; all opaque. pbOutline: a black pixel all round (per user,
		/// the same day, as an option, as the compass disc's), the picture a pixel larger each way.
		/// </summary>
		public static UWPicture BuildRuneHollow(UWTextures pOTextures, bool pbOutline)
		{
			return ComposeRuneHollow(UWMainFrame.Picture(pOTextures), pbOutline, RuneHollowOf(pOTextures));
		}

		public static UWPicture ComposeRuneHollow(UWPicture pOMain, bool pbOutline)
		{
			return ComposeRuneHollow(pOMain, pbOutline, msRuneHollow);
		}

		/// <summary>The hollow's rectangle on the data's game's frame: the second game's is the right recess of its
		/// marble ledge (UWClassicLayoutUw2.RuneHollow; UW2 plan stage U6 step 1), cut the same way.</summary>
		public static UWRectInt RuneHollowOf(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? UWClassicLayoutUw2.RuneHollow.Place : msRuneHollow;
		}

		/// <summary>
		/// THE ACTIVE SPELLS' HOLLOW (the second game's; UW2 plan stage U6 step 2c): the left hollow of its marble ledge
		/// (UWClassicLayoutUw2.SpellHollow), cut as the rune hollow - opaque, the optional outline. Without the ledge's rim
		/// above it (per user at the test of 204, 2026-11-24, as the rune hollow; the first cut took the rim along for the
		/// spells' tops): the spells reach a little above it, as they reach into the rim on the frame.
		/// </summary>
		public static UWPicture BuildSpellHollow(UWTextures pOTextures, bool pbOutline)
		{
			return ComposeRuneHollow(UWMainFrame.Picture(pOTextures), pbOutline, UWClassicLayoutUw2.SpellHollow.Place);
		}

		/// <summary>Where an active spell's picture lies in the spells' hollow (the frame's spell_x at 0x37; the first spell
		/// at the right), its top-left corner - its top above the hollow's (negative), as on the frame.</summary>
		public static void SpellHollowIcon(int piSlot, out int piX, out int piY)
		{
			piX = UWClassicLayoutUw2.Runes.SpellX[piSlot] - UWClassicLayoutUw2.SpellHollow.Left;
			piY = (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Runes.SpellY) - UWClassicLayoutUw2.SpellHollow.Top;
		}

		/// <summary>Where the first prepared rune lies in the data's game's hollow, and the step to the next.</summary>
		public static void RuneHollowRunes(UWTextures pOTextures, out int piX, out int piY, out int piPitch)
		{
			if (pOTextures != null && pOTextures.Game == UWGame.Uw2)
			{
				piX = UWClassicLayoutUw2.RuneHollow.RuneLeft - UWClassicLayoutUw2.RuneHollow.Left;
				piY = UWClassicLayoutUw2.RuneHollow.RuneTop - UWClassicLayoutUw2.RuneHollow.Top;
				piPitch = UWClassicLayoutUw2.RuneHollow.RunePitch;

				return;
			}

			piX = RuneHollowRuneX;
			piY = RuneHollowRuneY;
			piPitch = RuneHollowPitch;
		}

		public static UWPicture ComposeRuneHollow(UWPicture pOMain, bool pbOutline, UWRectInt pOPlace)
		{
			UWPicture lOHollow = pOMain.Crop(pOPlace);

			UWPixelOps.MakeOpaque(lOHollow);

			if (!pbOutline)
				return lOHollow;

			UWPicture lOFramed = new UWPicture(lOHollow.Width + 2, lOHollow.Height + 2);

			UWPixelOps.Fill(lOFramed, UWPixelOps.Black);
			lOFramed.Overlay(lOHollow, 1, 1);

			return lOFramed;
		}
	}
}
