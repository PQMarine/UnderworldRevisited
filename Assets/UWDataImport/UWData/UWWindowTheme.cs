namespace UWDataImport.Presentation
{
	/// <summary>The look of the game's own windows (UWWindowThemes); the number is settings.json's
	/// (UWUserData.WindowTheme).</summary>
	public enum UWWindowThemeEnum
	{
		/// <summary>The windows as phase 8b drew them: flat grey-blue grounds.</summary>
		Plain = 0,

		/// <summary>The application icon's carved stone, its torchlight and the runes' ember.</summary>
		Stone = 1
	}

	/// <summary>What a carved theme lays under a window; the number is settings.json's
	/// (UWUserData.WindowBackground).</summary>
	public enum UWWindowBackgroundEnum
	{
		/// <summary>The stone's radial light and the torch, stretched over the window.</summary>
		Gradient = 0,

		/// <summary>A cloud pattern in the stone's colours (UWThemeGround.Clouds), the light over it.</summary>
		Clouds = 1
	}

	/// <summary>The kinds of window ground a theme fills.</summary>
	public enum UWWindowGroundEnum
	{
		/// <summary>The setup bar's windows, the layout editor's window and its question (Panel).</summary>
		Dialog,

		/// <summary>The setup bar itself (Bar).</summary>
		Bar,

		/// <summary>The help window and the modern game menu, a little see-through (Overlay).</summary>
		Overlay,

		/// <summary>The first-run screen behind its title (Background).</summary>
		Screen,

		/// <summary>The gamepad's letter grid, a little see-through (LetterGridPanel).</summary>
		LetterGrid
	}

	/// <summary>
	/// A COLOUR OF THE THEME TABLE as the engine takes it: four floats from 0 to 1. Floats and not
	/// UWColor32 because the Plain theme is the windows' former palette value for value, and those
	/// values (0.05, 0.11, 0.14...) do not fall on whole bytes - a byte copy would not be the same
	/// colour (the reason UWModernPalette gives as well).
	/// </summary>
	public readonly struct UWThemeColour
	{
		public readonly float R;

		public readonly float G;

		public readonly float B;

		public readonly float A;

		public UWThemeColour(float pfR, float pfG, float pfB, float pfA = 1f)
		{
			R = pfR;
			G = pfG;
			B = pfB;
			A = pfA;
		}

		/// <summary>0xRRGGBB as the icon's SVG writes it, with an opacity.</summary>
		public static UWThemeColour Hex(int piRgb, float pfAlpha = 1f)
		{
			return new UWThemeColour(((piRgb >> 16) & 0xFF) / 255f, ((piRgb >> 8) & 0xFF) / 255f, (piRgb & 0xFF) / 255f, pfAlpha);
		}
	}

	/// <summary>
	/// THE CARVED STONE OF A THEME (cleanup phase 8d, 2026-10-25, per user: the application icon's
	/// colours, Art/Icon/ur-icon.svg): the numbers UWThemeGround makes a window's ground from. Whole
	/// numbers only - colours as 0xRRGGBB, places and strengths per mille - so the generator's
	/// pictures are the same bytes on every machine and engine (their pins in the self-check).
	/// </summary>
	public sealed class UWStoneLook
	{
		// ------------------------------------------------- The stone's light (the icon's "stoneLight")

		/// <summary>The radial gradient's stops: light at the centre, the middle at MiddleAt, dark at
		/// the radius and beyond.</summary>
		public int Light;

		public int Middle;

		public int Dark;

		public int MiddleAt;

		/// <summary>The gradient's centre and radius, per mille of the window's width and height.</summary>
		public int CentreX;

		public int CentreY;

		public int Radius;

		/// <summary>The window's stone is the icon's in shade: every stop times this, per mille, so
		/// that the light text keeps its contrast over the brightest spot (the self-check measures it).</summary>
		public int Shade;

		// ------------------------------------------------- The torch (the icon's "torch")

		/// <summary>The warm light from the top left: its colour and opacity (per mille) at the centre,
		/// at TorchOuterAt and nothing at the radius.</summary>
		public int TorchInner;

		public int TorchInnerAlpha;

		public int TorchOuter;

		public int TorchOuterAlpha;

		public int TorchOuterAt;

		public int TorchX;

		public int TorchY;

		public int TorchRadius;

		/// <summary>The icon's torch opacities times this, per mille: faint, under the text.</summary>
		public int TorchStrength;

		// ------------------------------------------------- The clouds

		/// <summary>The noise's seed, its cells across the tile at the coarsest octave, and how many
		/// octaves (each with twice the cells and half the weight).</summary>
		public int CloudSeed;

		public int CloudCells;

		public int CloudOctaves;

		/// <summary>How far the noise is spread round its middle before it picks the stone's colour,
		/// per mille (1000 as it comes).</summary>
		public int CloudContrast;

		/// <summary>The darkening towards the far corner the clouds get with the torch (UWThemeGround.
		/// Light), per mille opacity of black at the gradient's radius.</summary>
		public int CloudShadow;

		// ------------------------------------------------- The grain (the icon's feTurbulence)

		public int GrainSeed;

		/// <summary>The strongest speck: light or dark at this opacity, out of 255.</summary>
		public int GrainAlpha;
	}

	/// <summary>
	/// ONE THEME OF THE GAME'S OWN WINDOWS (cleanup phase 8d, 2026-10-25): the colour of each role the
	/// kit's drawers paint with (UWKitRows, the help, the game menu, the letter grid), and how a
	/// window's ground is laid - flat in the role's colour (Stone null), or carved: the stone's picture
	/// with the light edge on the top and left and the shadow on the bottom and right.
	/// </summary>
	public sealed class UWWindowTheme
	{
		public UWWindowThemeEnum Id;

		// ------------------------------------------------- The roles (UWGuiTheme's palette until 8d)

		/// <summary>The darkest ground: text fields, lists, the help's notes and map, a scroll bar's
		/// track, a switch's box; the first-run screen.</summary>
		public UWThemeColour Background;

		/// <summary>The setup bar; the layout editor's title row.</summary>
		public UWThemeColour Bar;

		/// <summary>The dialogs, the help's tabs, the game menu's buttons.</summary>
		public UWThemeColour Panel;

		/// <summary>A row or tab under the pointer, an open bar item, a button's face.</summary>
		public UWThemeColour Hover;

		/// <summary>A button's face under the pointer.</summary>
		public UWThemeColour ButtonHover;

		/// <summary>A thin line between groups, a slider's groove, a switch's frame.</summary>
		public UWThemeColour Separator;

		public UWThemeColour Text;

		public UWThemeColour DimText;

		/// <summary>Titles, the current choice and tab, a held button's face.</summary>
		public UWThemeColour Accent;

		/// <summary>The accent for rich text, RRGGBB.</summary>
		public string AccentHex;

		/// <summary>The help window's and the game menu's ground, a little see-through.</summary>
		public UWThemeColour Overlay;

		/// <summary>The shade over the picture while the manual is read.</summary>
		public UWThemeColour ReadingShade;

		/// <summary>A status line that reports a failure.</summary>
		public UWThemeColour Error;

		/// <summary>A slider's thumb at rest (under the pointer it is Text).</summary>
		public UWThemeColour Thumb;

		/// <summary>The arrow keys' frame round the piece with the focus, and the glow outside it (an
		/// alpha of 0: none).</summary>
		public UWThemeColour Focus;

		public UWThemeColour FocusGlow;

		/// <summary>The gamepad's letter grid: its panel, a key, the key with the focus, the hints.</summary>
		public UWThemeColour LetterGridPanel;

		public UWThemeColour LetterGridKey;

		public UWThemeColour LetterGridFocus;

		public UWThemeColour LetterGridHint;

		// ------------------------------------------------- The carving

		/// <summary>Null: every ground is a flat fill in its role's colour. Else the stone's numbers.</summary>
		public UWStoneLook Stone;

		/// <summary>A carved ground's edges: lit on the top and left, in shadow on the bottom and
		/// right, EdgeWidth layout units wide; a separator is a groove of the two; a button's face
		/// gets them one unit wide (the other way round while held).</summary>
		public UWThemeColour EdgeLight;

		public UWThemeColour EdgeShadow;

		public float EdgeWidth;

		/// <summary>The ground is the stone's picture, not a flat colour.</summary>
		public bool IsCarved => Stone != null;

		/// <summary>The colour of a ground: a flat theme's fill, a carved theme's opacity (its alpha).</summary>
		public UWThemeColour GroundColour(UWWindowGroundEnum peGround)
		{
			switch (peGround)
			{
				case UWWindowGroundEnum.Bar: return Bar;
				case UWWindowGroundEnum.Overlay: return Overlay;
				case UWWindowGroundEnum.Screen: return Background;
				case UWWindowGroundEnum.LetterGrid: return LetterGridPanel;
				default: return Panel;
			}
		}
	}

	/// <summary>
	/// THE THEME TABLE OF THE GAME'S OWN WINDOWS (cleanup phase 8d, 2026-10-25; Docs/CLEANUP-PLAN.md
	/// phase 8d). Per user the same day: today's look stays as the first theme and the default; a
	/// second takes the application icon's colours (Art/Icon/ur-icon.svg, Assets/UWIcon/UWRIcon.png).
	/// The setting is UWUserData.WindowTheme, the carved theme's ground UWUserData.WindowBackground;
	/// UWGuiTheme turns the current entry into Unity colours. The modern HUD's panels and bags keep
	/// their own colours, the debug panel stays Plain whatever is chosen (decided 2026-10-25).
	///
	/// PLAIN is UWGuiTheme's palette of before 8d value for value (the self-check pins every float
	/// of it against those literals): Text was UWModernPalette.HudText, Accent UWModernPalette.Accent,
	/// Thumb UWKitRows' ThumbColour, Focus UWKitKeyNav.FocusColour, the letter grid's three
	/// UWLetterGrid's own and its focus UWModernPalette.Gold.
	///
	/// STONE: the icon's stone (#8a7f70 - #5e564c - #2f2a25 from the top left) in shade, the torch
	/// (#ffb347) faint over it, the runes' ember (#ffe29a - #ffa53a - #d9531e) for the accent, the
	/// focus and its glow, the stone's light edge (#b3a68f) for frames and separators with the dark
	/// shadow on the far side, light warm text. The contrast of every text role over the brightest
	/// spot of either ground is measured by the self-check (group WindowThemes).
	/// </summary>
	public static class UWWindowThemes
	{
		public static readonly UWWindowTheme Plain = new UWWindowTheme
		{
			Id = UWWindowThemeEnum.Plain,
			Background = new UWThemeColour(0.05f, 0.05f, 0.06f, 1f),
			Bar = new UWThemeColour(0.11f, 0.11f, 0.13f, 1f),
			Panel = new UWThemeColour(0.14f, 0.14f, 0.16f, 1f),
			Hover = new UWThemeColour(0.22f, 0.22f, 0.26f, 1f),
			ButtonHover = new UWThemeColour(0.30f, 0.30f, 0.35f, 1f),
			Separator = new UWThemeColour(0.30f, 0.30f, 0.34f, 1f),
			Text = new UWThemeColour(0.86f, 0.86f, 0.84f, 1f),
			DimText = new UWThemeColour(0.55f, 0.55f, 0.55f, 1f),
			Accent = new UWThemeColour(0.80f, 0.64f, 0.34f, 1f),
			AccentHex = "CCA357",
			Overlay = new UWThemeColour(0.08f, 0.08f, 0.09f, 0.96f),
			ReadingShade = new UWThemeColour(0.03f, 0.03f, 0.04f, 0.94f),
			Error = new UWThemeColour(0.9f, 0.4f, 0.35f),
			Thumb = new UWThemeColour(0.62f, 0.62f, 0.66f, 1f),
			Focus = new UWThemeColour(1f, 0.82f, 0.3f, 1f),
			FocusGlow = new UWThemeColour(0f, 0f, 0f, 0f),
			LetterGridPanel = new UWThemeColour(0.11f, 0.08f, 0.05f, 0.94f),
			LetterGridKey = new UWThemeColour(0.25f, 0.19f, 0.12f, 1f),
			LetterGridFocus = new UWThemeColour(0.925f, 0.77f, 0.44f, 1f),
			LetterGridHint = new UWThemeColour(0.85f, 0.8f, 0.7f),
			Stone = null,
			EdgeLight = new UWThemeColour(0f, 0f, 0f, 0f),
			EdgeShadow = new UWThemeColour(0f, 0f, 0f, 0f),
			EdgeWidth = 0f
		};

		public static readonly UWWindowTheme Stone = new UWWindowTheme
		{
			Id = UWWindowThemeEnum.Stone,
			Background = UWThemeColour.Hex(0x1C1814),
			Bar = UWThemeColour.Hex(0x221D18),
			Panel = UWThemeColour.Hex(0x2A251F),
			Hover = UWThemeColour.Hex(0x7A3010, 0.35f),
			ButtonHover = UWThemeColour.Hex(0x7A3412, 0.55f),
			Separator = UWThemeColour.Hex(0xB3A68F, 0.5f),
			Text = UWThemeColour.Hex(0xF3E7D3),
			DimText = UWThemeColour.Hex(0xCDBFA6),
			Accent = UWThemeColour.Hex(0xFFA53A),
			AccentHex = "FFA53A",
			Overlay = UWThemeColour.Hex(0x29241F, 0.96f),
			ReadingShade = UWThemeColour.Hex(0x120F0C, 0.94f),
			Error = UWThemeColour.Hex(0xFF9C9C),
			Thumb = UWThemeColour.Hex(0xB3A68F),
			Focus = UWThemeColour.Hex(0xFFE29A),
			FocusGlow = UWThemeColour.Hex(0xFFA53A, 0.35f),
			LetterGridPanel = UWThemeColour.Hex(0x2A241E, 0.94f),
			LetterGridKey = UWThemeColour.Hex(0x4A4036),
			LetterGridFocus = UWThemeColour.Hex(0xFFB347),
			LetterGridHint = UWThemeColour.Hex(0xE6D6B8),
			Stone = new UWStoneLook
			{
				Light = 0x8A7F70,
				Middle = 0x5E564C,
				Dark = 0x2F2A25,
				MiddleAt = 550,
				CentreX = 300,
				CentreY = 250,
				Radius = 900,
				Shade = 420,
				TorchInner = 0xFFB347,
				TorchInnerAlpha = 450,
				TorchOuter = 0xFF8C1A,
				TorchOuterAlpha = 80,
				TorchOuterAt = 600,
				TorchX = 220,
				TorchY = 180,
				TorchRadius = 750,
				TorchStrength = 160,
				CloudSeed = 7,
				CloudCells = 4,
				CloudOctaves = 5,
				CloudContrast = 1300,
				CloudShadow = 300,
				GrainSeed = 7,
				GrainAlpha = 10
			},
			EdgeLight = UWThemeColour.Hex(0xB3A68F, 0.7f),
			EdgeShadow = UWThemeColour.Hex(0x000000, 0.6f),
			EdgeWidth = 2f
		};

		/// <summary>Every theme, in the settings' order.</summary>
		public static readonly UWWindowTheme[] All = { Plain, Stone };

		/// <summary>The theme of a setting's value; an unknown one is Plain.</summary>
		public static UWWindowTheme Get(UWWindowThemeEnum peTheme)
		{
			int liAt = (int)peTheme;

			return liAt >= 0 && liAt < All.Length ? All[liAt] : Plain;
		}
	}
}
