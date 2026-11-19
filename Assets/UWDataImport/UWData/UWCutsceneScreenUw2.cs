using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CUTSCENE SCREEN, engine-free (UW2 plan phase 5 part C): what the VGA held while uw2 gfx/CUTS.C
	/// played, as palette indices, and the palette the DAC showed.
	///
	/// THE VIRTUAL SCREEN (virtual_screen, VIDMODE's _2977): a picture w by h larger than the screen, of which the part
	/// above the split shows, from the focus on (vscreen_focus). Below the split the VGA's line compare shows memory of
	/// its own, which does not scroll: the subtitle bar. Without a split the whole screen is the virtual one. The plain
	/// screen is the virtual screen 320 by 200 with the focus at 0, 199.
	///
	/// COORDINATES as the scripts give them: x from the left, y counted UP from the bottom (199 is the top row of a
	/// 200-row screen); focus(x, y) puts the virtual screen's point (x, y) at the top left of the view, so its row
	/// (h - 1 - y) is the view's first. Everything is kept top-down inside.
	///
	/// WHAT DRAWS WHERE: an LBACK picture into the virtual screen at its own place (lback_vscreen); an LPF record and the
	/// subtitles at the screen's place (show and draw_rsd at st-&gt;x + (focus_x &amp; 3), st-&gt;y: the view's first pixel), so
	/// above the split into the virtual screen where the view is, below it into the bar.
	/// </summary>
	public sealed class UWCutsceneScreenUw2
	{
		public const int ScreenWidth = 320;

		public const int ScreenHeight = 200;

		/// <summary>The palette, 256 entries of red, green and blue, 6 bits each as the DAC took them.</summary>
		public const int PaletteBytes = 0x300;

		/// <summary>No split (999 in the scripts, -1 in the original's globals).</summary>
		public const int NoSplit = -1;

		public int VirtualWidth { get; private set; } = ScreenWidth;

		public int VirtualHeight { get; private set; } = ScreenHeight;

		/// <summary>The virtual screen's pixels, top-down.</summary>
		private byte[] myVirtual = new byte[ScreenWidth * ScreenHeight];

		/// <summary>The split the VGA was given (virtual_screen's third argument): NoSplit, or the bottom-up row the bar
		/// ends at - the bar is the bottom Split + 1 rows.</summary>
		public int VgaSplit { get; private set; } = NoSplit;

		/// <summary>The bar below a VGA split, 320 by VgaSplit + 1, top-down.</summary>
		private byte[] myBar = new byte[0];

		/// <summary>splity: the row the picture stops at, NoSplit for none (cutsop_splity, virtual_screen).</summary>
		public int SplitY { get; set; } = NoSplit;

		/// <summary>focus_x and focus_y, bottom-up.</summary>
		public int FocusX { get; private set; }

		public int FocusY { get; private set; } = ScreenHeight - 1;

		/// <summary>The palette shown (the DAC's), 6-bit.</summary>
		public byte[] Shown { get; } = new byte[PaletteBytes];

		/// <summary>
		/// THE PORT'S SUBTITLES WITH SPEECH on a layer of their own (per user 2026-11-17: "Greetings, Avatar." was spoken
		/// while the picture faded in, and its line, drawn into the faded palette, could not be seen): while TextOverlay
		/// is set, Put writes into this layer, 320 by 200 palette indices, 0 for none, which the host shows through the
		/// working palette (Unfaded) instead of the faded one. A new subtitle and the fade-out clear it, not the bar's fill
		/// (cuts_do_fadein fills the bar as the fade-in begins, and took the first line with it).
		/// </summary>
		public byte[] Overlay { get; } = new byte[ScreenWidth * ScreenHeight];

		public bool TextOverlay { get; set; }

		/// <summary>The palette the overlay shows through: the working one, before any fade (st-&gt;palette).</summary>
		public byte[] Unfaded { get; } = new byte[PaletteBytes];

		/// <summary>The rows above a VGA split, all 200 without one.</summary>
		public int ViewRows => VgaSplit == NoSplit ? ScreenHeight : ScreenHeight - (VgaSplit + 1);

		/// <summary>The rows an LPF record draws: 200 - (splity + 1) (cuts_process_lp).</summary>
		public int PictureRows => ScreenHeight - (SplitY + 1);

		/// <summary>The view's first pixel in the virtual screen, top-down.</summary>
		public int ViewLeft => Math.Max(0, Math.Min(FocusX, VirtualWidth - ScreenWidth));

		public int ViewTop => Math.Max(0, Math.Min(VirtualHeight - 1 - FocusY, VirtualHeight - ViewRows));

		/// <summary>virtual_screen(w, h, split): a new virtual screen (its memory as it was: ours starts black), the
		/// split given to the VGA and as splity.</summary>
		public void SetVirtualScreen(int piWidth, int piHeight, int piSplit)
		{
			VirtualWidth = Math.Max(ScreenWidth, piWidth);
			VirtualHeight = Math.Max(1, piHeight);

			if (myVirtual.Length != VirtualWidth * VirtualHeight)
				myVirtual = new byte[VirtualWidth * VirtualHeight];

			VgaSplit = piSplit < 0 ? NoSplit : piSplit;
			SplitY = VgaSplit;

			int liBar = VgaSplit == NoSplit ? 0 : ScreenWidth * (VgaSplit + 1);

			if (myBar.Length != liBar)
				myBar = new byte[liBar];
		}

		/// <summary>vscreen_focus(x, y).</summary>
		public void Focus(int piX, int piY)
		{
			FocusX = piX;
			FocusY = piY;
		}

		/// <summary>grfx_clear: the screen black (index 0), the virtual screen and the bar.</summary>
		public void Clear()
		{
			Array.Clear(myVirtual, 0, myVirtual.Length);
			Array.Clear(myBar, 0, myBar.Length);
			ClearOverlay();
		}

		/// <summary>lback_vscreen: a 320 by 200 picture into the virtual screen with its top left at x, y (bottom-up),
		/// clipped to it.</summary>
		public void DrawBack(int piX, int piY, byte[] pyPicture)
		{
			if (pyPicture == null)
				return;

			int liTop = VirtualHeight - 1 - piY;

			for (int liRow = 0; liRow < ScreenHeight; liRow++)
			{
				int liY = liTop + liRow;

				if (liY < 0 || liY >= VirtualHeight)
					continue;

				for (int liColumn = 0; liColumn < ScreenWidth; liColumn++)
				{
					int liX = piX + liColumn;
					int liFrom = (liRow * ScreenWidth) + liColumn;

					if (liX >= 0 && liX < VirtualWidth && liFrom < pyPicture.Length)
						myVirtual[(liY * VirtualWidth) + liX] = pyPicture[liFrom];
				}
			}
		}

		/// <summary>A pixel at the screen's place (x from the left, y from the top): above the VGA split into the virtual
		/// screen at the view, below it into the bar.</summary>
		public void Put(int piX, int piY, byte pyPixel)
		{
			if (piX < 0 || piX >= ScreenWidth || piY < 0 || piY >= ScreenHeight)
				return;

			if (TextOverlay)
			{
				Overlay[(piY * ScreenWidth) + piX] = pyPixel;

				return;
			}

			if (piY >= ViewRows)
			{
				int liBarAt = ((piY - ViewRows) * ScreenWidth) + piX;

				if (liBarAt < myBar.Length)
					myBar[liBarAt] = pyPixel;

				return;
			}

			int liX = ViewLeft + piX;
			int liY = ViewTop + piY;

			if (liX < VirtualWidth && liY < VirtualHeight)
				myVirtual[(liY * VirtualWidth) + liX] = pyPixel;
		}

		/// <summary>The pixel the screen shows at x, y (top-down).</summary>
		public byte Get(int piX, int piY)
		{
			if (piY >= ViewRows)
			{
				int liBarAt = ((piY - ViewRows) * ScreenWidth) + piX;

				return liBarAt < myBar.Length ? myBar[liBarAt] : (byte)0;
			}

			int liX = ViewLeft + piX;
			int liY = ViewTop + piY;

			return liX < VirtualWidth && liY < VirtualHeight ? myVirtual[(liY * VirtualWidth) + liX] : (byte)0;
		}

		/// <summary>A rectangle of the screen in one colour (rectangle, urectangle): rows piTop to piBottom (top-down,
		/// inclusive).</summary>
		public void Fill(int piTop, int piBottom, byte pyColour)
		{
			for (int liY = Math.Max(0, piTop); liY <= Math.Min(ScreenHeight - 1, piBottom); liY++)
			{
				for (int liX = 0; liX < ScreenWidth; liX++)
					Put(liX, liY, pyColour);
			}
		}

		/// <summary>The subtitle bar, splity's bottom rows (rectangle(0, splity, 0x13F, 0)), in one colour; nothing
		/// without a splity.</summary>
		public void FillBar(byte pyColour)
		{
			if (SplitY == NoSplit)
				return;

			bool lbOverlay = TextOverlay;

			TextOverlay = false;
			Fill(ScreenHeight - 1 - SplitY, ScreenHeight - 1, pyColour);
			TextOverlay = lbOverlay;
		}

		/// <summary>The subtitle layer empty.</summary>
		public void ClearOverlay()
		{
			Array.Clear(Overlay, 0, Overlay.Length);
		}

		/// <summary>The screen as shown, 320 by 200 indices top-down, into pyTarget.</summary>
		public void Compose(byte[] pyTarget)
		{
			for (int liY = 0; liY < ScreenHeight; liY++)
			{
				for (int liX = 0; liX < ScreenWidth; liX++)
					pyTarget[(liY * ScreenWidth) + liX] = Get(liX, liY);
			}
		}

		/// <summary>grfx_setpal: the whole palette shown.</summary>
		public void SetPalette(byte[] pyPalette)
		{
			Array.Copy(pyPalette, Shown, PaletteBytes);
		}

		/// <summary>grfx_palrange: entries piFirst to piFirst + piCount - 1 shown from pyPalette.</summary>
		public void SetPaletteRange(byte[] pyPalette, int piFirst, int piCount)
		{
			int liFrom = Math.Max(0, piFirst) * 3;
			int liTo = Math.Min(PaletteBytes, (piFirst + piCount) * 3);

			for (int liAt = liFrom; liAt < liTo; liAt++)
				Shown[liAt] = pyPalette[liAt];
		}

		/// <summary>rotate_bank(first, count, 0) on the shown palette: every entry takes the next one's colour, the last
		/// the first's (COLCYCLE.C).</summary>
		public void RotateDown(int piFirst, int piCount)
		{
			if (piCount < 2 || piFirst < 0 || (piFirst + piCount) * 3 > PaletteBytes)
				return;

			int liAt = piFirst * 3;
			byte lyR = Shown[liAt], lyG = Shown[liAt + 1], lyB = Shown[liAt + 2];

			for (int liEntry = 0; liEntry < piCount - 1; liEntry++, liAt += 3)
			{
				Shown[liAt] = Shown[liAt + 3];
				Shown[liAt + 1] = Shown[liAt + 4];
				Shown[liAt + 2] = Shown[liAt + 5];
			}

			Shown[liAt] = lyR;
			Shown[liAt + 1] = lyG;
			Shown[liAt + 2] = lyB;
		}
	}
}
