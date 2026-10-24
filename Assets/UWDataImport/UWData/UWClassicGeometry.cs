using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CLASSIC SCREEN ON THE WINDOW (cleanup 5.18, 2026-10-23: out of UWGameUI, the arithmetic
	/// as it was): how the original's 320 x 200 picture lies on the real window, and the
	/// conversions between the two - a screen point back to the original's column and row (as
	/// its click areas are registered, UWClickRules), a picture's place on the screen, the 3D
	/// view's rectangle (the hole in the frame, which the camera renders into and the pointer
	/// zones measure), and where a picture of a given size lands in the view.
	///
	/// A value of this struct is the fit of one frame: the screen pixels per original row
	/// (UWUiFit.Scale), the width of a column over a row's height (UWClassicScreen.HorizontalPixelFactor:
	/// 1 shows square pixels, 5/6 the CRT's 4:3), the bar left of the frame in screen pixels (the
	/// letterbox plus the help window's slide, UWHelpLayout) and under it (a narrow window fits
	/// the frame by its width, UWUiFit), and the columns the frame is widened by (UWClassicWide.
	/// Extra, 0 at the original's 320). The Unity side (UWClassicScreen) builds it from the
	/// window each time it is asked; nothing here reads the window.
	///
	/// Screen coordinates are pixels with the origin at the BOTTOM left, as Unity reports the
	/// mouse; the original's rows count from the top of the 200 line screen unless a method says
	/// otherwise.
	/// </summary>
	public readonly struct UWClassicGeometry
	{
		/// <summary>Screen pixels per original row.</summary>
		public readonly float Scale;

		/// <summary>A column's width over a row's height - 1 for square pixels, 5/6 at 4:3.</summary>
		public readonly float PixelFactor;

		/// <summary>The bar left of the frame, in screen pixels: the letterbox margin plus the
		/// help window's slide of the frame.</summary>
		public readonly float LeftMarginPixels;

		/// <summary>The bar under the frame, in screen pixels - a window narrower than the frame.
		/// </summary>
		public readonly float BottomMarginPixels;

		/// <summary>The columns the frame is wider than the original's 320 (Classic Wide).</summary>
		public readonly int Extra;

		public UWClassicGeometry(float pfScale, float pfPixelFactor, float pfLeftMarginPixels, float pfBottomMarginPixels, int piExtra)
		{
			Scale = pfScale;
			PixelFactor = pfPixelFactor;
			LeftMarginPixels = pfLeftMarginPixels;
			BottomMarginPixels = pfBottomMarginPixels;
			Extra = piExtra;
		}

		/// <summary>Screen pixels per original column.</summary>
		public float ScaleX => Scale * PixelFactor;

		/// <summary>The frame's width in original columns, the widening included.</summary>
		public int FrameColumns => UWClassicLayout.Screen.Width + Extra;

		// ------------------------------------------------- The original's point of a screen point

		/// <summary>
		/// A screen point in the original's own terms, as its click areas are registered (see
		/// UWClickRules): x the column from the left, y COUNTED FROM THE BOTTOM of the 200 line
		/// screen, 0 to 199, so that the row r from the top is y = 199 - r (the original clamps y at
		/// 199 and its "up" pointer key puts it there; until 2026-09-26 we read 200 - r). False
		/// outside the 320x200 frame.
		/// </summary>
		public bool TryGetOriginalPoint(float pfScreenX, float pfScreenY, out int piX, out int piY)
		{
			float lfX = (pfScreenX - LeftMarginPixels) / ScaleX;
			float lfY = (pfScreenY - BottomMarginPixels) / Scale;

			piY = (int)Math.Floor(lfY);

			// The widened frame's column back to the original's (UWClassicWide): the columns put in
			// belong to no area.
			piX = OriginalColumn((int)Math.Floor(lfX), (UWClassicLayout.Screen.Height - 1) - piY);

			return piX >= 0 && lfX >= 0f && lfX < FrameColumns && lfY >= 0f && lfY < UWClassicLayout.Screen.Height;
		}

		/// <summary>Is the pointer inside an area of the original's table - both ends included, as
		/// the dispatcher tests (UWClickRules)? The position relative to the corner comes back,
		/// as the original hands it to the area's handler.</summary>
		public bool IsPointerInOriginalArea(float pfScreenX, float pfScreenY, UWClickRules.Area pOArea, out int piX, out int piY)
		{
			piX = 0;
			piY = 0;

			if (!TryGetOriginalPoint(pfScreenX, pfScreenY, out int liX, out int liY) || !pOArea.Contains(liX, liY))
				return false;

			piX = liX - pOArea.X0;
			piY = liY - pOArea.Y0;

			return true;
		}

		/// <summary>
		/// An original column from a column of the widened frame at a row from the top: -1 in the
		/// columns put in (they belong to no area of the original's click table). The cuts are
		/// Classic Wide's (UWHudArt.ClassicWideCut), the left half of the extra columns at the
		/// left cut, the right half at the right one.
		/// </summary>
		public int OriginalColumn(int piX, int piRow)
		{
			return OriginalColumn(piX, piRow, Extra);
		}

		/// <summary>The same for a frame widened by the given columns (UWClassicWide.OriginalX).</summary>
		public static int OriginalColumn(int piX, int piRow, int piExtra)
		{
			if (piExtra <= 0)
				return piX;

			int liLeft = piExtra / 2;
			int liCutLeft = UWHudArt.ClassicWideCut(piRow, 0);
			int liCutRight = UWHudArt.ClassicWideCut(piRow, 1);

			if (piX < liCutLeft)
				return piX;

			if (piX < liCutLeft + liLeft)
				return -1;

			if (piX < liCutRight + liLeft)
				return piX - liLeft;

			if (piX < liCutRight + piExtra)
				return -1;

			return piX - piExtra;
		}

		// ------------------------------------------------- Pictures on the screen

		/// <summary>Screen rectangle (pixels, origin bottom left: X, Y, Width, Height) of a picture
		/// whose top-left corner lies at the given original UI position (top down).</summary>
		public (float X, float Y, float Width, float Height) UiPictureScreenRect(float pfCornerX, float pfCornerY, float pfWidth, float pfHeight)
		{
			return (LeftMarginPixels + (pfCornerX * ScaleX),
				BottomMarginPixels + ((UWClassicLayout.Screen.Height - pfCornerY - pfHeight) * Scale),
				pfWidth * ScaleX, pfHeight * Scale);
		}

		/// <summary>
		/// Top-left corner (original UI pixels, top down) of a picture of the given size placed in
		/// the viewport exactly like the window and gravestone pictures: horizontally centred in
		/// the hole, WindowContentInsetTop below its top edge. The window picture in CS400.N01 is
		/// a 172x112 block; the hole in MAIN.BYT (176x121, 177x122 with both edge pixels) is 4
		/// pixels wider and 9 taller, so the block is CENTRED IN THE HOLE instead of placed at its
		/// corner (the first attempt used the corner, reported per user as "a bit too far left and
		/// up" - exactly half the difference). For the centring the inclusive count is the right
		/// one - the difference comes out evenly at 2 pixels left/right (user, 2026-08-29: "two to
		/// the left and 4-5 up"); vertically the measured inset of 2, not the centred 5.
		/// </summary>
		public static (float X, float Y) ViewportPictureCorner(float pfWidth, float pfHeight)
		{
			return (UWClassicLayout.Frame.WindowHoleLeft + (float)Math.Floor((UWClassicLayout.Frame.WindowHoleWidthInclusive - pfWidth) * 0.5f),
				UWClassicLayout.Frame.WindowHoleTop + UWClassicLayout.Frame.WindowContentInsetTop);
		}

		/// <summary>Screen rectangle (pixels, origin bottom left) of a picture of the given size in
		/// original pixels, placed in the viewport like the window pictures
		/// (ViewportPictureCorner), moved with the widened hole's middle (half the extra
		/// columns, UWClassicWide.Left). Used by UWIntroPlayer for the viewport cutscenes.</summary>
		public (float X, float Y, float Width, float Height) ViewportPictureScreenRect(float pfWidth, float pfHeight)
		{
			(float X, float Y) lOCorner = ViewportPictureCorner(pfWidth, pfHeight);

			return UiPictureScreenRect(lOCorner.X + (Extra / 2), lOCorner.Y, pfWidth, pfHeight);
		}

		// ------------------------------------------------- The 3D view's rectangle

		// Readjusted to 53/226/182/69 over several rounds of DOSBox screenshot comparison -
		// the height (113) stayed noticeably smaller than the actual hole, no
		// FOV combination could compensate for that, as it was caused by the aspect ratio (see
		// UWViewportCamera). Instead measured pixel-exactly by flood fill directly from the transparent area in
		// Assets/UWExportedTextures/Main_0.png (320x200, alpha=0 in the hole):
		// hole = X 50..226, Y (image top-down) 17..138 -> 176x121, not 173x113. In
		// bottom-up screen coordinates (200 minus image Y) this gives 183/62.
		public float GameAreaLeft => LeftMarginPixels + (UWClassicLayout.Frame.WindowHoleLeft * ScaleX);

		public float GameAreaRight => LeftMarginPixels + ((UWClassicLayout.Frame.WindowHoleRight + Extra) * ScaleX);

		public float GameAreaTop => BottomMarginPixels + ((UWClassicLayout.Screen.Height - UWClassicLayout.Frame.WindowHoleTop) * Scale);

		public float GameAreaBottom => BottomMarginPixels + ((UWClassicLayout.Screen.Height - UWClassicLayout.Frame.WindowHoleBottom) * Scale);

		/// <summary>For Interaction.cs: is a screen position inside the small
		/// 3D viewport (original scheme)? A world raycast from outside (e.g. over the
		/// paper doll/backpack panel) would otherwise hit invisible geometry behind the UI, since
		/// the UI does not block the physics query (no EventSystem/GraphicRaycaster).</summary>
		public bool IsInGameArea(float pfScreenX, float pfScreenY)
		{
			return pfScreenX > GameAreaLeft && pfScreenX < GameAreaRight
				&& pfScreenY > GameAreaBottom && pfScreenY < GameAreaTop;
		}

		/// <summary>The same with the border counting as inside: during a drag the pointer is clamped
		/// exactly onto it, and with a strict check the zone was then not re-evaluated any more
		/// (per user, 2026-09-15). The pointer zones test the movement area this way
		/// (IsInMovementAreaOrOnBorder) since 2026-10-23.</summary>
		public bool IsInGameAreaOrOnBorder(float pfScreenX, float pfScreenY)
		{
			return pfScreenX >= GameAreaLeft && pfScreenX <= GameAreaRight && pfScreenY >= GameAreaBottom && pfScreenY <= GameAreaTop;
		}

		// ------------------------------------------------- The pointer scheme's movement area

		// THE VIEW WINDOW THE ORIGINAL PLACES (UWEDIT.C place_3d_view: 52/19, 172 x 113,
		// UWClassicLayout.Frame.ViewWindow*), two columns and two rows inside the hole's top-left
		// corner and seven rows above its bottom edge: the area the pointer scheme measures
		// (seg034_2F89_4E, W = 172, H = 113) and keeps the pointer in while the button is held (per
		// user from the original, 2026-10-23). Until then the pointer was held at the hole's edges,
		// and its picture hung into the rune hollow below the view. Widened with the frame (Extra).
		public float MovementAreaLeft => LeftMarginPixels + (UWClassicLayout.Frame.ViewWindowLeft * ScaleX);

		public float MovementAreaRight => LeftMarginPixels + ((UWClassicLayout.Frame.ViewWindowLeft + UWClassicLayout.Frame.ViewWindowWidth + Extra) * ScaleX);

		public float MovementAreaTop => BottomMarginPixels + ((UWClassicLayout.Screen.Height - UWClassicLayout.Frame.ViewWindowTop) * Scale);

		public float MovementAreaBottom => BottomMarginPixels + ((UWClassicLayout.Screen.Height - UWClassicLayout.Frame.ViewWindowTop - UWClassicLayout.Frame.ViewWindowHeight) * Scale);

		/// <summary>The movement area with its border counting as inside - the pointer zones
		/// (UWPointerZones): the clamped pointer lies exactly on the border, and there the zone must
		/// still be read (per user, 2026-09-15, for the hole's edges then).</summary>
		public bool IsInMovementAreaOrOnBorder(float pfScreenX, float pfScreenY)
		{
			return pfScreenX >= MovementAreaLeft && pfScreenX <= MovementAreaRight && pfScreenY >= MovementAreaBottom && pfScreenY <= MovementAreaTop;
		}

		/// <summary>For UWItemDrag (original: drop vs. throw on release in the
		/// 3D viewport) - 0 at the bottom, 1 at the top edge of the viewport. Only meaningful
		/// if IsInGameArea was already true for the same position.</summary>
		public float VerticalFractionInGameArea(float pfScreenY)
		{
			float lfHeight = GameAreaTop - GameAreaBottom;

			return lfHeight > 0f ? fClamp01((pfScreenY - GameAreaBottom) / lfHeight) : 0f;
		}

		/// <summary>0 at the left, 1 at the right edge of the viewport. Counterpart to
		/// VerticalFractionInGameArea - needed for the target direction of
		/// spell projectiles, which the original computes from the horizontal pointer position.</summary>
		public float HorizontalFractionInGameArea(float pfScreenX)
		{
			float lfWidth = GameAreaRight - GameAreaLeft;

			return lfWidth > 0f ? fClamp01((pfScreenX - GameAreaLeft) / lfWidth) : 0f;
		}

		private static float fClamp01(float pfValue)
		{
			if (pfValue < 0f)
				return 0f;

			if (pfValue > 1f)
				return 1f;

			return pfValue;
		}
	}
}
