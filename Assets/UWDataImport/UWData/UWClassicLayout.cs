using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CLASSIC SCREEN'S LAYOUT IN ONE PLACE (cleanup 5.3, 2026-10-19): every place, size and
	/// colour of the original's 320 x 200 screens - the frame and its window hole, the panel and
	/// the chain, the paperdoll page, backpack and container, the rune shelf and hollow, the active
	/// spells, the stats page's paging, the message scroll and its edges, the compass, the dragons,
	/// the map, the conversation (CONV.BYT), the options and the main menu. The classic HUD
	/// (UWGameUI and its UWHud* parts), the art builders (UWHudArt, UWAutomapPainter) and the
	/// modern scheme's original look (UWModernConversation, UWModernClassicFrame, UWModernCompass,
	/// UWModernRunePanel) read their numbers here instead of repeating them, so that a front end
	/// other than Unity (decision 26) finds the whole layout without the Unity scripts.
	///
	/// Coordinates are the original's screen pixels, x from the left and y from the TOP (rows as
	/// MAIN.BYT stores them), unless an entry says otherwise. Every entry carries its source: Exe
	/// (read in UW.EXE, with the routine or table), Measured (measured on the game's data, with the
	/// file), Estimated (by eye or from a reference, not confirmed), UserTuned (set or confirmed by
	/// the user on the original, with the date), Derived (computed from other entries). The
	/// self-check holds that every entry is tagged and that the users read the same numbers.
	///
	/// CONSTANTS ONLY, the Inspector values of UWGameUI included since decision 12 (2026-10-22, per
	/// user: the stats page's rows and colours, the weight's centre and colours, the panel's step,
	/// the view angle - nothing is tuned in the Inspector any more). Pieces of MAIN.BYT that only the
	/// art builders crop (the shelf, the ledge and the rollers, the synthesis ranges) are
	/// UWMainFrame's (cleanup 9.10); here are the places other code needs too. The paperdoll's body
	/// and armour places and the equipment icon slots are UWPaperdollLayout's (cleanup 5.1); the
	/// Paperdoll area points at it.
	/// </summary>
	public static class UWClassicLayout
	{
		// ------------------------------------------------- Source tags

		/// <summary>Where an entry's number comes from.</summary>
		[AttributeUsage(AttributeTargets.Field)]
		public abstract class SourceAttribute : Attribute
		{
			public readonly string Note;

			protected SourceAttribute(string psNote)
			{
				Note = psNote ?? string.Empty;
			}
		}

		/// <summary>Read in UW.EXE; the note names the routine or table.</summary>
		public sealed class ExeAttribute : SourceAttribute
		{
			public ExeAttribute(string psRoutine) : base(psRoutine)
			{
			}
		}

		/// <summary>Measured on the game's data; the note names the file.</summary>
		public sealed class MeasuredAttribute : SourceAttribute
		{
			public MeasuredAttribute(string psFile) : base(psFile)
			{
			}
		}

		/// <summary>Estimated by eye or taken from a reference, not confirmed on the original.</summary>
		public sealed class EstimatedAttribute : SourceAttribute
		{
			public EstimatedAttribute(string psHow) : base(psHow)
			{
			}
		}

		/// <summary>Set or confirmed by the user on the original; the note starts with the date.</summary>
		public sealed class UserTunedAttribute : SourceAttribute
		{
			public UserTunedAttribute(string psDate) : base(psDate)
			{
			}
		}

		/// <summary>Computed from other entries of this table.</summary>
		public sealed class DerivedAttribute : SourceAttribute
		{
			public DerivedAttribute(string psFrom) : base(psFrom)
			{
			}
		}

		// ------------------------------------------------- The screen

		public static class Screen
		{
			[Measured("MAIN.BYT, 320 x 200")]
			public const int Width = 320;

			[Measured("MAIN.BYT, 320 x 200")]
			public const int Height = 200;

			/// <summary>A pixel's width at the original's 4:3 display (UWClassicScreen.VgaPixelAspectCorrection).</summary>
			[Derived("320 x 200 shown at 4:3")]
			public const float PixelAspect = 5f / 6f;
		}

		// ------------------------------------------------- The frame and its window hole

		public static class Frame
		{
			/// <summary>The hole in MAIN.BYT the 3D view shows through: x 50 to 226, rows 17 to 138
			/// (UWClassicGeometry, the classic camera's area).</summary>
			[Measured("MAIN.BYT, flood fill")]
			public const int WindowHoleLeft = 50;

			[Measured("MAIN.BYT, flood fill")]
			public const int WindowHoleTop = 17;

			[Measured("MAIN.BYT, flood fill")]
			public const int WindowHoleRight = 226;

			[Measured("MAIN.BYT, flood fill")]
			public const int WindowHoleBottom = 138;

			[Derived("WindowHoleRight - WindowHoleLeft")]
			public const int WindowHoleWidth = WindowHoleRight - WindowHoleLeft;

			[Derived("WindowHoleBottom - WindowHoleTop")]
			public const int WindowHoleHeight = WindowHoleBottom - WindowHoleTop;

			/// <summary>Both edge pixels counted, for the centring of the window picture.</summary>
			[Derived("WindowHoleWidth + 1")]
			public const int WindowHoleWidthInclusive = WindowHoleWidth + 1;

			[Derived("WindowHoleHeight + 1")]
			public const int WindowHoleHeightInclusive = WindowHoleHeight + 1;

			/// <summary>From the hole's top edge to the painted area of the window picture.</summary>
			[UserTuned("2026-08-29, measured in the original")]
			public const int WindowContentInsetTop = 2;

			/// <summary>
			/// THE ORIGINAL'S 3D WINDOW AND VIEW ANGLE (read 2026-10-22, UnderworldExhumed; the
			/// derivation in the private notes render-projection.md). place_3d_view(0x34, 0xB4,
			/// 0xAC, 0x71): 172 columns by 113 rows at column 52, row 19 (0xB4 is the bottom row
			/// counted from the bottom). The renderer clips to the square frustum |x|, |y| &lt;= z
			/// (code_pnt) and projects x * halfWidth / z, y * halfHeight / z onto the window
			/// (do_closure), so that frustum's edge IS the window's edge. scale_matrix first scales
			/// the depth by the zoom and the X row by halfHeight / (aspect * halfWidth) - the
			/// aspect word 15F4 is the VGA pixel proportion 5/6, which makes the picture round on a
			/// 4:3 CRT. Hence tan of the half angle at the top edge is the zoom, at the side edge
			/// the zoom over that X scale. Until this reading the angle was an Inspector value: 90
			/// horizontal, tuned by eye (the code's 75 was UnderworldGodot's VERTICAL angle, which
			/// the original's 74.7 confirms). The Graphics menu's view angle (UWViewAngle)
			/// overrides it when set. The camera takes the vertical tangent scaled to its own rows
			/// (UWViewportCamera.Apply).
			/// </summary>
			[Exe("UWEDIT.C place_3d_view(0x34, 0xB4, 0xAC, 0x71)")]
			public const int ViewWindowLeft = 52;

			[Exe("UWEDIT.C place_3d_view(0x34, 0xB4, 0xAC, 0x71)")]
			public const int ViewWindowTop = 19;

			[Exe("UWEDIT.C place_3d_view(0x34, 0xB4, 0xAC, 0x71)")]
			public const int ViewWindowWidth = 172;

			[Exe("UWEDIT.C place_3d_view(0x34, 0xB4, 0xAC, 0x71)")]
			public const int ViewWindowHeight = 113;

			/// <summary>The renderer's zoom in the game, 1.15 fixed point: 0x61A8 = 0.7629 (0x7ED2
			/// outside the game mode, 0x6062 with the unused mode bit 3).</summary>
			[Exe("VIEW3D.C place_3d_view: curZoom = 0x61A8 under MODE_GAME")]
			public const int ViewZoom = 0x61A8;

			/// <summary>The aspect word seg051:15F4, 1.15 fixed point: 0x6A3D = 0.830, the VGA
			/// 320x200 pixel proportion 5/6; read from the GOG UW.EXE (file offset 0x4BA24).</summary>
			[Exe("seg051:15F4 in UW.EXE, read by INSTANCE.ASM scale_matrix")]
			public const int ViewAspectWord = 0x6A3D;

			/// <summary>tan of the half angle at the window's top and bottom edge: the zoom.</summary>
			[Derived("ViewZoom / 32768")]
			public const float VerticalViewTangent = ViewZoom / 32768f;

			/// <summary>scale_matrix's X scale in 1.15, with the EXE's integer steps: the product
			/// ViewAspectWord * halfWidth * 2 keeps its high word (71), and halfHeight &lt;&lt; 15
			/// is divided by it - 25845 = 0.7887.</summary>
			[Derived("(ViewWindowHeight / 2 << 15) / (ViewAspectWord * ViewWindowWidth / 2 * 2 >> 16), scale_matrix")]
			public const int ViewXScale = ((ViewWindowHeight >> 1) << 15) / ((ViewAspectWord * (ViewWindowWidth >> 1) * 2) >> 16);

			/// <summary>tan of the half angle at the window's side edge: the zoom over the X scale,
			/// 0.9673.</summary>
			[Derived("ViewZoom / ViewXScale")]
			public const float HorizontalViewTangent = ViewZoom / (float)ViewXScale;

			/// <summary>The angles in degrees: 88.1 across the window's 172 columns, 74.7 across its
			/// 113 rows (on a 4:3 CRT both give the same focal length, 88.9 units).</summary>
			[Derived("2 * atan(HorizontalViewTangent)")]
			public static readonly float HorizontalViewAngle = 2f * (float)Math.Atan(HorizontalViewTangent) * (180f / (float)Math.PI);

			[Derived("2 * atan(VerticalViewTangent)")]
			public static readonly float VerticalViewAngle = 2f * (float)Math.Atan(VerticalViewTangent) * (180f / (float)Math.PI);

			/// <summary>The weapon picture's top left (UWGameUI: wLeftOffset, wTopOffset; the row
			/// the weapon's offsets count from).</summary>
			[Estimated("since the baseline, no measurement recorded")]
			public const int WeaponLeft = 52;

			[Estimated("since the baseline, no measurement recorded")]
			public const int WeaponTop = 131;

			/// <summary>The gargoyle's head on the top bar, its eyes at 130-133 and 142-145, rows 4 and 5.</summary>
			[Measured("MAIN.BYT")]
			public const int GargoyleHeadLeft = 126;

			[Measured("MAIN.BYT")]
			public const int GargoyleHeadTop = 0;

			[Measured("MAIN.BYT")]
			public const int GargoyleHeadWidth = 24;

			[Measured("MAIN.BYT")]
			public const int GargoyleHeadHeight = 18;

			[Derived("GargoyleHeadLeft, GargoyleHeadTop, GargoyleHeadWidth, GargoyleHeadHeight")]
			public static readonly UWRectInt GargoyleHead = new UWRectInt(GargoyleHeadLeft, GargoyleHeadTop, GargoyleHeadWidth, GargoyleHeadHeight);
		}

		// ------------------------------------------------- The frame widened (Classic Wide)

		/// <summary>The bands of rows the widened frame is cut in and where (UWHudArt.BuildClassicWideFrame,
		/// UWClassicWide's shift of the classic parts).</summary>
		public static class ClassicWide
		{
			/// <summary>The bands' first rows: the top bar from row 0, the view, the ledge, the scroll.</summary>
			[Measured("MAIN.BYT")]
			public const int TopRows = 19;

			[Measured("MAIN.BYT")]
			public const int LedgeTop = 128;

			[Measured("MAIN.BYT")]
			public const int ScrollTop = 168;

			/// <summary>The two cuts of each band (top bar, view, ledge, scroll).
			/// The view's band cuts at the hole's edges, where its columns are black from top to bottom
			/// (x 53 to 222): the hole's contents then move with its middle, the bars with their sides
			/// (the classic scheme's parts, UWClassicWide). Columns 223 and 224 carry the right vine's
			/// leaves - a cut at 224 left a piece of them behind (per user's screenshot, 2026-10-10).</summary>
			[UserTuned("2026-10-10, from the user's screenshots")]
			public static readonly int[,] Cuts = { { 78, 198 }, { 53, 223 }, { 100, 173 }, { 82, 223 } };

			/// <summary>The view's hole in the unwidened frame.</summary>
			[Measured("MAIN.BYT")]
			public static readonly UWRectInt Hole = new UWRectInt(52, 19, 172, 109);
		}

		// ------------------------------------------------- The panel and the chain

		public static class Panel
		{
			/// <summary>The turning panel (PANELS.GR 0 to 2): all its pixels match MAIN.BYT there.</summary>
			[Measured("PANELS.GR 0 against MAIN.BYT")]
			public const int Left = 236;

			[Measured("PANELS.GR 0 against MAIN.BYT")]
			public const int Top = 7;

			[Measured("PANELS.GR, 83 x 114")]
			public const int Width = 83;

			[Measured("PANELS.GR, 83 x 114")]
			public const int Height = 114;

			/// <summary>Steps of a turn; at the middle one the panel is seen from its edge.</summary>
			[UserTuned("2026-09-02")]
			public const int TurnSteps = 8;

			/// <summary>How long one step of the turn lasts, in seconds (UWHudPanel, UWModernClassicFrame).</summary>
			[UserTuned("2026-09-02, by eye against the original")]
			public const float TurnStepSeconds = 0.25f;

			/// <summary>The chain between the flasks (CHAINS.GR 0 to 7) and its short piece above
			/// the panel (8 to 15).</summary>
			[Measured("CHAINS.GR against MAIN.BYT")]
			public const int ChainLeft = 272;

			[Measured("CHAINS.GR against MAIN.BYT")]
			public const int ChainTop = 121;

			[Measured("CHAINS.GR against MAIN.BYT")]
			public const int ShortChainTop = 3;

			[Measured("CHAINS.GR")]
			public const int ChainFrames = 8;

			[Measured("CHAINS.GR")]
			public const int ShortChainFirstFrame = 8;
		}

		// ------------------------------------------------- The paperdoll page

		public static class Paperdoll
		{
			/// <summary>The inventory page above the backpack: x 236 to 317, rows 7 to 81.</summary>
			[Measured("MAIN.BYT")]
			public static readonly UWRectInt Page = new UWRectInt(236, 7, 82, 75);

			/// <summary>Where the body picture lies: UWPaperdollLayout's, with the armour places and
			/// the icon slots.</summary>
			[Derived("UWPaperdollLayout.BodyX")]
			public const int BodyLeft = UWPaperdollLayout.BodyX;

			[Derived("UWPaperdollLayout.BodyY")]
			public const int BodyTop = UWPaperdollLayout.BodyY;
		}

		// ------------------------------------------------- Backpack and container

		public static class Backpack
		{
			[Measured("MAIN.BYT, the circles of the page")]
			public const int Columns = 4;

			[Measured("MAIN.BYT, the circles of the page")]
			public const int Rows = 2;

			/// <summary>The slots' hit areas, roughly measured on a DOSBox screenshot.</summary>
			[Estimated("DOSBox screenshot, not pixel-exact")]
			public const int SlotSize = 14;

			[Estimated("DOSBox screenshot, not pixel-exact")]
			public const int SpacingX = 5;

			[Estimated("DOSBox screenshot, not pixel-exact")]
			public const int SpacingY = 4;

			[Estimated("DOSBox screenshot, not pixel-exact")]
			public const int GridLeft = 242;

			[Estimated("DOSBox screenshot, not pixel-exact")]
			public const int GridTop = 83;

			/// <summary>The CENTRE of the carrying capacity's number in the circle below the
			/// paperdoll: the original centres it there, so a two-digit one grows to both sides
			/// (UWHudInventory, the modern original page).</summary>
			[UserTuned("2026-09-01, the circle on MAIN.BYT, centred as the original")]
			public const int WeightCentreX = 305;

			[UserTuned("2026-09-01, the circle on MAIN.BYT, centred as the original")]
			public const int WeightCentreY = 62;

			/// <summary>The number's colour, a light orange (#FFBE76).</summary>
			[Estimated("picked in the original, #FFBE76")]
			public static readonly UWColor32 WeightColour = new UWColor32(255, 190, 118, 255);

			/// <summary>The stacks' counts at the slots: white.</summary>
			[Estimated("white, as seen in the original")]
			public static readonly UWColor32 CountColour = new UWColor32(255, 255, 255, 255);
		}

		public static class Container
		{
			/// <summary>The open container's picture, above the panel and left-aligned to it,
			/// growing upwards from this corner.</summary>
			[Estimated("from the panel's position")]
			public const int OpenIconLeft = 241;

			[Estimated("from the panel's position")]
			public const int OpenIconTop = 80;

			/// <summary>The scroll arrows (BUTTONS.GR 27 and 28): side by side at the icon's height,
			/// the down arrow's right edge at the panel's edge.</summary>
			[UserTuned("before 2026-09-15, confirmed per user")]
			public const int ScrollDownRight = 314;

			[UserTuned("before 2026-09-15, confirmed per user")]
			public const int ScrollSize = 8;

			[UserTuned("before 2026-09-15, confirmed per user")]
			public const int ScrollUpExtraLeft = 3;

			/// <summary>The container panel (INV.GR 6), stretched over the frame: its anchored
			/// position and size delta, taken over 1:1 from the user's prefab.</summary>
			[UserTuned("before 2026-09-15, the user's prefab")]
			public const float PanelAnchoredX = 118.51324f;

			[UserTuned("before 2026-09-15, the user's prefab")]
			public const float PanelAnchoredY = -0.50367f;

			[UserTuned("before 2026-09-15, the user's prefab")]
			public const float PanelSizeDeltaX = -234.98553f;

			[UserTuned("before 2026-09-15, the user's prefab")]
			public const float PanelSizeDeltaY = -158.99f;

			/// <summary>The panel's top left in the original's frame (from the anchored position and
			/// size above: x 236, row 80), the place UWClassicWide.FrameSized moves it as.</summary>
			[Derived("PanelAnchored and PanelSizeDelta")]
			public const int FrameLeft = 236;

			[Derived("PanelAnchored and PanelSizeDelta")]
			public const int FrameTop = 80;
		}

		// ------------------------------------------------- Flasks and power gem

		public static class Flasks
		{
			/// <summary>The vitality and mana flasks (UWFlasks, 24 x 33) on the shelf under the panel.</summary>
			[Measured("MAIN.BYT")]
			public const int HealthLeft = 248;

			[Measured("MAIN.BYT")]
			public const int ManaLeft = 284;

			[Measured("MAIN.BYT")]
			public const int Top = 125;

			[Measured("FLASKS.GR, 24 x 33")]
			public const int Rows = 33;
		}

		public static class PowerGem
		{
			[Measured("MAIN.BYT")]
			public const int Left = 4;

			[Measured("MAIN.BYT")]
			public const int Top = 139;
		}

		// ------------------------------------------------- Rune shelf, hollow and active spells

		public static class RuneShelf
		{
			/// <summary>The grid of PANELS.GR 1, relative to the panel; one pixel right and one
			/// down since 2026-09-26 (per user, against the original).</summary>
			[Measured("PANELS.GR 1")]
			public const int Columns = 4;

			[UserTuned("2026-09-26")]
			public const int SlotLeft = 8;

			[UserTuned("2026-09-26")]
			public const int SlotTop = 5;

			[Measured("PANELS.GR 1")]
			public const int PitchX = 18;

			[Measured("PANELS.GR 1")]
			public const int PitchY = 15;

			/// <summary>A compartment's size.</summary>
			[Measured("PANELS.GR 1")]
			public const int Cell = 14;

			/// <summary>
			/// The page's click area, relative to the panel (decision 27, 2026-10-20): the panel's
			/// mouse region that BeginInventory registers for mous_in_panel (UnderworldExhumed
			/// INVPANEL.C:161), x 0xF0 to 0x13B and y 0x52 to 0xBD from the screen's bottom (MOUSE.C:9,
			/// row = 199 - y), less Panel.Left and Panel.Top - both ends included. Within it
			/// mous_in_rune (RUNES.C:117) tiles the runes into gapless cells
			/// (UWRuneShelfModel.RuneCellAt) above the clear strip, which ends on HitBottom.
			/// </summary>
			[Exe("BeginInventory panel_input, x 0xF0 less Panel.Left")]
			public const int HitLeft = 4;

			[Exe("BeginInventory panel_input, x 0x13B less Panel.Left")]
			public const int HitRight = 79;

			[Exe("BeginInventory panel_input, y 0xBD from the bottom, less Panel.Top")]
			public const int HitTop = 3;

			[Exe("BeginInventory panel_input, y 0x52 from the bottom, less Panel.Top")]
			public const int HitBottom = 110;

			/// <summary>The strip under the compartments that clears the selection, all of it
			/// clickable (tested per user in the original).</summary>
			[Estimated("PANELS.GR 1, below the last row ending at 92")]
			public const int ClearTop = 93;

			[Estimated("PANELS.GR 1")]
			public const int ClearBottom = 110;
		}

		public static class RuneHollow
		{
			/// <summary>The hollow for the prepared runes in the frame's lower edge, with a pixel or
			/// two of its slate rim all round.</summary>
			[Measured("MAIN.BYT")]
			public const int Left = 173;

			[Measured("MAIN.BYT")]
			public const int Top = 136;

			[Measured("MAIN.BYT")]
			public const int Width = 49;

			[Measured("MAIN.BYT")]
			public const int Height = 19;

			/// <summary>Where the first prepared rune lies: the recess reaches x 174 to 219 and rows
			/// 136 to 153; three 14-pixel tiles fit at a pitch of 15.</summary>
			[Measured("MAIN.BYT")]
			public const int RuneLeft = 176;

			[Measured("MAIN.BYT")]
			public const int RuneTop = 138;

			[Measured("MAIN.BYT")]
			public const int RunePitch = 15;
		}

		public static class ActiveSpells
		{
			/// <summary>The first spell's icon at the right, further ones to the left (the reference's
			/// scene at factor 4: 340, 276 and 212 at height 552).</summary>
			[Estimated("reference scene at factor 4")]
			public const int Right = 85;

			[Estimated("reference scene at factor 4")]
			public const int Top = 138;

			[Estimated("reference scene at factor 4")]
			public const int Pitch = 16;
		}

		// ------------------------------------------------- The stats page

		/// <summary>The stats page's skill list and paging; its label rows are Inspector values on
		/// UWGameUI (decision 12).</summary>
		public static class Stats
		{
			/// <summary>The skill list's first row: the recessed box reaches from 63 to 104, six rows
			/// in the seven-pixel grid.</summary>
			[Measured("PANELS.GR 2")]
			public const int SkillFirstRow = 64;

			[Measured("PANELS.GR 2")]
			public const int SkillRows = 6;

			/// <summary>A click in the panel's bottom 8 rows pages: left of x 37 back, from 37 on
			/// forward (relative to the panel).</summary>
			[Exe("ovr145_445")]
			public static readonly UWRectInt PageBack = new UWRectInt(0, 106, 37, 8);

			[Exe("ovr145_445")]
			public static readonly UWRectInt PageForward = new UWRectInt(37, 106, 46, 8);

			/// <summary>The six attribute rows under the header: the first label's row and the
			/// grid's pitch (21, 28 ... 56), relative to the panel's top.</summary>
			[Measured("PANELS.GR 2, the baked-in labels")]
			public const int FirstRow = 21;

			[Measured("PANELS.GR 2, the baked-in labels")]
			public const int RowHeight = 7;

			/// <summary>The one right edge of every value and of the level in the header line, and
			/// the left edge of the class and the skill names, relative to the panel.</summary>
			[Estimated("PANELS.GR 2, by eye")]
			public const int ValueRight = 76;

			[Estimated("PANELS.GR 2, by eye")]
			public const int LevelRight = 76;

			[Estimated("PANELS.GR 2, by eye")]
			public const int LabelLeft = 6;

			/// <summary>Name and class are drawn by the game, the picture has nothing at their
			/// spots. Converted from UnderworldGodot, whose stats page is at exactly factor 4: the
			/// class and the level fell onto the seven-pixel grid at 13.5 and 14, the name at 5.25
			/// did not - set to 7 by the user's eye, onto the grid.</summary>
			[UserTuned("2026-09-03, the reference's 5.25 moved onto the grid")]
			public const int NameRow = 7;

			[Estimated("reference UnderworldGodot at factor 4: 13.5 and 14")]
			public const int ClassRow = 14;

			/// <summary>The values' colour: black, like the baked-in labels.</summary>
			[UserTuned("2026-09-03, black like the labels")]
			public static readonly UWColor32 ValueColour = new UWColor32(0, 0, 0, 255);

			/// <summary>The skill list's colour: a blue-grey (#62647F), not the values' black.</summary>
			[UserTuned("2026-09-03, blue-grey, not black")]
			public static readonly UWColor32 SkillColour = new UWColor32(0x62, 0x64, 0x7F, 255);
		}

		// ------------------------------------------------- The message scroll

		public static class MessageLog
		{
			/// <summary>The text box in the lower frame (the dark area measured by flood fill: x 16
			/// to 304, rows 169 to 198), fine-tuned per user test in the prefab. The conversation's
			/// answers use the same box.</summary>
			[UserTuned("before 2026-09-15, the user's prefab")]
			public const int Left = 15;

			[UserTuned("before 2026-09-15, the user's prefab")]
			public const int Top = 169;

			[UserTuned("before 2026-09-15, the user's prefab")]
			public const int Width = 291;

			[UserTuned("before 2026-09-15, the user's prefab")]
			public const int Height = 30;

			/// <summary>The rolling edges (SCRLEDGE.GR), centre-pivoted: the original draws them at
			/// x 11 and 306 on EXE y 30, our row 169, the box's top.</summary>
			[Exe("seg043_37F0_11E")]
			public const float EdgeLeft = 13f;

			[Exe("seg043_37F0_11E")]
			public const float EdgeRight = 308f;

			[Exe("seg043_37F0_11E")]
			public const float EdgeCentreY = 183.5f;

			[Measured("SCRLEDGE.GR")]
			public const float EdgeWidth = 4f;

			[Measured("SCRLEDGE.GR")]
			public const float EdgeHeight = 29f;

			/// <summary>The palette index of each colour code (UWMessageLogModel.ColourIndices).</summary>
			[Exe("immediates at file 0x3976D-0x397AF")]
			public static readonly int[] ColourIndices = { 46, 38, 241, 96, 180, 196, 212 };

			/// <summary>The text colour while no palette is loaded (#3D1C08).</summary>
			[UserTuned("before 2026-09-15, taken from the original")]
			public static readonly UWColor32 FallbackColour = new UWColor32(61, 28, 8, 255);
		}

		// ------------------------------------------------- The compass

		public static class Compass
		{
			/// <summary>COMPASS.GR 0's place on the pedestal (the cross's top left).</summary>
			[Measured("MAIN.BYT, where COMPASS.GR 0 covers the baked cross")]
			public const int Left = 112;

			[Measured("MAIN.BYT, where COMPASS.GR 0 covers the baked cross")]
			public const int Top = 131;

			/// <summary>The sixteen needle positions, north first, then clockwise.</summary>
			[Estimated("reference scene")]
			public static readonly float[] NeedleLeft =
				{ 136f, 128f, 120f, 116f, 112f, 112f, 116f, 124f, 136f, 144f, 156f, 160f, 160f, 156f, 152f, 144f };

			[Estimated("reference scene")]
			public static readonly float[] NeedleTop =
				{ 131f, 133f, 134f, 137f, 141f, 146f, 147f, 150f, 152f, 150f, 147f, 146f, 141f, 137f, 134f, 133f };
		}

		// ------------------------------------------------- The dragons

		/// <summary>The two dragons' pictures' top-left corners, [0] the left, [1] the right (EXE y
		/// converted: top = 199 - y).</summary>
		public static class Dragons
		{
			[Exe("seg036_3087_16E draws them, EXE y converted")]
			public static readonly (int X, int Y)[] Neck = { (36, 134), (228, 134) };

			[Exe("seg036_3087_16E draws them, EXE y converted")]
			public static readonly (int X, int Y)[] Head = { (36, 145), (204, 145) };

			[Exe("seg036_3087_16E draws them, EXE y converted")]
			public static readonly (int X, int Y)[] Tail = { (40, 65), (224, 65) };

			/// <summary>[side, animation - 1]: the overlay's corner.</summary>
			[Exe("dseg 824/830, 91E/92A")]
			public static readonly (int X, int Y)[,] Overlay =
			{
				{ (40, 155), (48, 145), (36, 145) },
				{ (204, 155), (204, 145), (200, 145) }
			};
		}

		// ------------------------------------------------- The map (BLNKMAP.BYT)

		public static class Map
		{
			/// <summary>Three pixels a tile; the cell of tile x/y at (3x + 7, 3y + 4), y from the
			/// BOTTOM of the screen.</summary>
			[Exe("ovr092_48B")]
			public const int TileSize = 3;

			[Exe("ovr092_48B")]
			public const int CellLeft = 7;

			[Exe("ovr092_48B")]
			public const int CellBottom = 4;

			/// <summary>The tiles' block's top row, counted from the top.</summary>
			[Derived("Screen.Height - CellBottom - 64 * TileSize")]
			public const int TilesTop = Screen.Height - CellBottom - (UWWorldScale.TilesPerAxis * TileSize);

			/// <summary>The map screen's palette.</summary>
			[Exe("ovr092_F66, GetPalette(1)")]
			public const int Palette = 1;

			/// <summary>The player's mark (BUTTONS.GR) and the picture's column with the dagger.</summary>
			[Exe("ovr092_F66, graphic 0x103F")]
			public const int PlayerMarkImage = 0x3F;

			[Measured("BUTTONS.GR 0x3F")]
			public const int PlayerMarkDaggerColumn = 1;

			/// <summary>The level number after the baked-in word "Level", centred on this column.</summary>
			[UserTuned("2026-09-03")]
			public const int LevelNumberCentre = 289;

			[UserTuned("2026-09-03")]
			public const int LevelNumberTop = 5;

			/// <summary>The click areas on the parchment.</summary>
			[Estimated("BLNKMAP.BYT")]
			public static readonly UWRectInt Close = new UWRectInt(263, 150, 47, 26);

			[Estimated("BLNKMAP.BYT")]
			public static readonly UWRectInt PageUp = new UWRectInt(298, 2, 22, 18);

			[Estimated("BLNKMAP.BYT")]
			public static readonly UWRectInt PageDown = new UWRectInt(298, 178, 22, 20);

			[Estimated("BLNKMAP.BYT, the stone and its label")]
			public static readonly UWRectInt Erase = new UWRectInt(274, 117, 28, 32);

			/// <summary>A note's text starts at the clicked column - the pointer's hot spot, which
			/// for the flat quill (43 x 15) is its bottom-left corner, the tip (per user with DOSBox
			/// screenshots, 2026-10-23: the first letter stands at the tip's column; MOUSE.C
			/// set_mouse_data names the picture's middle, which is not what the game draws). Until
			/// 2026-10-23 three columns left of it (user-tuned 2026-09-03, with the system pointer
			/// drawn too small to tell).</summary>
			[Exe("AUTOMAP.C ManageDungeonMap: note->x = mx")]
			public const int NoteColumnOffset = 0;

			/// <summary>A note's top row lies this many rows ABOVE the clicked row (the tip's): the
			/// original keeps note->y = my + 4 with rows counted from the bottom, and its erase test
			/// takes the rows note->y - 5 to note->y - the four-row letters end on the row above the
			/// tip's (DOSBox: tip row 99, letters rows 96 to 99, the box to 101). Until 2026-10-23
			/// the text began three rows BELOW the click.</summary>
			[Exe("AUTOMAP.C ManageDungeonMap: note->y = my + 4; erase: note->y - 5 <= my <= note->y")]
			public const int NoteRowsAboveClick = 4;

			/// <summary>The upright quill while writing (CURSORS.GR 14, 20 x 36, tip at the bottom
			/// left): its lowest row is the one above the clicked row, that many rows below the text's
			/// top - the original puts the pointer at (curx + 10, my + 0x12) with the picture's middle
			/// (9, 18) as the hot spot, so the tip stands one column behind the text's end.</summary>
			[Derived("NoteRowsAboveClick: AUTOMAP.C mouse_putxy(curx + 10, my + 0x12), hot spot (9, 18)")]
			public const int WritingQuillRowsBelowTop = NoteRowsAboveClick;

			/// <summary>The notes' and the level number's ink.</summary>
			[Estimated("no source recorded")]
			public static readonly UWColor32 NoteColour = new UWColor32(60, 40, 25, 255);
		}

		// ------------------------------------------------- The conversation (CONV.BYT)

		public static class Conversation
		{
			/// <summary>The conversation's top: the two name plates, the portraits' frames and the two
			/// trade areas between them (x 43 to 232, rows 0 to 46). The modern original look counts
			/// from its top left.</summary>
			[Measured("CONV.BYT")]
			public const int HeaderLeft = 43;

			[Measured("CONV.BYT")]
			public const int HeaderTop = 0;

			[Measured("CONV.BYT")]
			public const int HeaderWidth = 190;

			[Measured("CONV.BYT")]
			public const int HeaderHeight = 47;

			[Derived("HeaderLeft, HeaderTop, HeaderWidth, HeaderHeight")]
			public static readonly UWRectInt Header = new UWRectInt(HeaderLeft, HeaderTop, HeaderWidth, HeaderHeight);

			/// <summary>The parchment under it, its rolled rims included, and the ends of its two
			/// wooden rollers over the view's bars (x 42 to 233, rows 47 to 135).</summary>
			[Measured("CONV.BYT")]
			public const int ParchmentLeft = 42;

			[Measured("CONV.BYT")]
			public const int ParchmentTop = 47;

			[Measured("CONV.BYT")]
			public const int ParchmentWidth = 192;

			[Measured("CONV.BYT")]
			public const int ParchmentHeight = 89;

			[Derived("ParchmentLeft, ParchmentTop, ParchmentWidth, ParchmentHeight")]
			public static readonly UWRectInt Parchment = new UWRectInt(ParchmentLeft, ParchmentTop, ParchmentWidth, ParchmentHeight);

			/// <summary>The history on the parchment: thirteen six-pixel lines, 159 wide (per user with
			/// a screenshot of the original, 2026-09-30).</summary>
			[Measured("CONV.BYT")]
			public const int HistoryLeft = 59;

			[Measured("CONV.BYT")]
			public const int HistoryTop = 53;

			[UserTuned("2026-09-30")]
			public const int HistoryWidth = 159;

			[Measured("CONV.BYT")]
			public const int HistoryHeight = 78;

			/// <summary>The answers, in the message scroll's box.</summary>
			[Derived("MessageLog")]
			public const int ChoicesLeft = MessageLog.Left;

			[Derived("MessageLog")]
			public const int ChoicesTop = MessageLog.Top;

			[Derived("MessageLog")]
			public const int ChoicesWidth = MessageLog.Width;

			[Derived("MessageLog")]
			public const int ChoicesHeight = MessageLog.Height;

			/// <summary>The names: string_to_screen at 0x30 and 0x90.</summary>
			[Exe("CONVERSE.C string_to_screen")]
			public const int NpcNameLeft = 48;

			[Exe("CONVERSE.C string_to_screen")]
			public const int PlayerNameLeft = 144;

			[Measured("CONV.BYT")]
			public const int NameTop = 2;

			/// <summary>The width the names had up to 2026-10-14; the modern original look keeps it.</summary>
			[Measured("CONV.BYT, the name plates")]
			public const int NameWidth = 88;

			/// <summary>The name frames (CONVERSE.GR 0, 94 x 9): 798 of 846 pixels match.</summary>
			[Measured("CONV.BYT, 2026-09-11")]
			public const int NpcNameFrameLeft = 43;

			[Measured("CONV.BYT, 2026-09-11")]
			public const int PlayerNameFrameLeft = 139;

			/// <summary>The portraits (34 x 34).</summary>
			[Measured("CONV.BYT")]
			public const int NpcPortraitLeft = 45;

			[Measured("CONV.BYT")]
			public const int PlayerPortraitLeft = 197;

			[Measured("CONV.BYT")]
			public const int PortraitTop = 11;

			[Measured("CONV.BYT")]
			public const int PortraitSize = 34;

			/// <summary>The vine edges beside the parchment, three per side.</summary>
			[Exe("seg043_37F0_163")]
			public const int EdgeLeft = 52;

			[Exe("seg043_37F0_163")]
			public const int EdgeRight = 220;

			[Exe("seg043_37F0_163")]
			public static readonly int[] EdgeTops = { 51, 78, 105 };

			/// <summary>The trade slots: the original's tables (0xD74 the partner's, 0xD54 the
			/// player's) put both lower rows on row 29; a slot answers a click on 17 by 17 pixels
			/// (ovr095_52F, ovr095_4D3).</summary>
			[Exe("dseg 0xD74")]
			public static readonly (int X, int Y)[] NpcTradeSlots = { (91, 11), (112, 11), (91, 29), (112, 29) };

			[Exe("dseg 0xD54")]
			public static readonly (int X, int Y)[] PlayerTradeSlots = { (148, 11), (169, 11), (148, 29), (169, 29) };

			[Measured("CONVERSE.GR, the circles")]
			public const int TradeSlotSize = 16;

			[Exe("ovr095_52F")]
			public const int TradeSlotClickSize = 17;

			/// <summary>The crosses at the trade areas' edges, three by three pixels (per user,
			/// 2026-09-18; the tables at 0xD64 and 0xD84 would put them one pixel right and down,
			/// where the original uses them is not found).</summary>
			[UserTuned("2026-09-18")]
			public static readonly (int X, int Y)[] NpcTradeCrosses = { (87, 17), (129, 17), (87, 35), (129, 35) };

			[UserTuned("2026-09-18")]
			public static readonly (int X, int Y)[] PlayerTradeCrosses = { (144, 17), (186, 17), (144, 35), (186, 35) };

			[UserTuned("2026-09-18")]
			public const int TradeCrossSize = 3;

			/// <summary>The strip of the parchment's lower edge (rows 131 to 135) that covers the
			/// compass's top in a conversation.</summary>
			[Measured("CONV.BYT")]
			public const int ParchmentEdgeLeft = 100;

			[Measured("CONV.BYT")]
			public const int ParchmentEdgeTop = 131;

			[Measured("CONV.BYT")]
			public const int ParchmentEdgeRight = 220;

			[Measured("CONV.BYT")]
			public const int ParchmentEdgeBottom = 135;

			/// <summary>The palette index of CONV.BYT's sample text, in the parchment and the scroll.</summary>
			[Measured("CONV.BYT")]
			public const byte SampleTextIndex = 0x2E;

			/// <summary>The names' palette colour (the reference's uimanager.CharNameColour).</summary>
			[Estimated("reference: uimanager.CharNameColour; palette 0 has CONV.BYT's #8C8CA8 there")]
			public const int NameColourIndex = 0x65;

			/// <summary>The names' colour as the modern original look draws it: CONV.BYT's sample
			/// names, #8C8CA8.</summary>
			[Measured("CONV.BYT, the sample names")]
			public static readonly UWColor32 NameColour = new UWColor32(140, 140, 168, 255);

			/// <summary>The partner's text: CONV.BYT's sample text, index 0x2E.</summary>
			[Measured("CONV.BYT, index 0x2E")]
			public static readonly UWColor32 NpcText = new UWColor32(0x3C, 0x28, 0x20, 255);

			/// <summary>The player's answers, reddish brown.</summary>
			[Estimated("reference: MessageDisplay PC_SAY")]
			public static readonly UWColor32 PlayerText = new UWColor32(0x88, 0x3E, 0x14, 255);

			/// <summary>The narrator's text (print), black.</summary>
			[Estimated("reference")]
			public static readonly UWColor32 NarratorText = new UWColor32(0, 0, 0, 255);

			/// <summary>Hints of the interface, white like [MORE] in the message box.</summary>
			[Estimated("as the message box's [MORE]")]
			public static readonly UWColor32 UiText = new UWColor32(255, 255, 255, 255);
		}

		// ------------------------------------------------- The options

		/// <summary>The options menu: the reference's scene divided by four, matching the picture
		/// sizes of OPTBTNS.GR (the background 35 x 108, the buttons 31 x 14).</summary>
		public static class Options
		{
			/// <summary>The button that opens it (LFTI.GR 0 and 1, 28 x 17).</summary>
			[Estimated("reference scene / 4")]
			public const int ButtonLeft = 8;

			[Estimated("reference scene / 4")]
			public const int ButtonTop = 10;

			[Estimated("reference scene / 4")]
			public const int PanelLeft = 4;

			[Estimated("reference scene / 4")]
			public const int PanelTop = 10;

			[Estimated("reference scene / 4")]
			public const int EntryLeft = 5;

			[Estimated("reference scene / 4")]
			public const int EntryTop = 12;

			[Estimated("reference scene / 4")]
			public const int EntryPitch = 15;

			[Measured("OPTBTNS.GR")]
			public const int EntryCount = 7;
		}

		// ------------------------------------------------- The pointer zones of the view

		/// <summary>
		/// THE POINTER ZONES OF THE VIEW (UWPointerZones; the numbers mirror UW.EXE, verified in the
		/// disassembly 2026-09-15 and frozen by section 11 of the cleanup plan): the view registers
		/// its cursor regions in ovr134_6DB, the movement is decided in seg034_2F89_4E for a movement
		/// area of W = 172 by H = 113 pixels (UWPlayerMotion.PointerAreaWidth/Height). With xm/ym
		/// the position in that area (ym counted from the bottom): ym &lt; H/5 (22) strafes or backs at
		/// constant speed; 22 &lt;= ym &lt;= 45 (6H/15) turns without walking; ym &gt; 45 walks and
		/// turns; left of xm = 57 (W*5/15) and from xm = 115 on (xm &gt; 114) the side zones. The
		/// area is the view window of place_3d_view (Frame.ViewWindow*, UWClassicGeometry's
		/// movement area; until 2026-10-23 the hole, 176 x 121, with the frame border), scaled
		/// and widened with the frame, therefore the borders are applied as fractions of it.
		/// </summary>
		public static class PointerZones
		{
			/// <summary>Left of this column (W*5/15) the pointer turns or strafes left.</summary>
			[Exe("seg034_2F89_4E: xm < 57")]
			public const int LeftBorderColumn = 57;

			/// <summary>From this column on (xm > 114) the pointer turns or strafes right.</summary>
			[Exe("seg034_2F89_4E: xm > 114")]
			public const int RightBorderColumn = 115;

			/// <summary>The bottom band, rows from the bottom (H/5): strafing and backing.</summary>
			[Exe("seg034_2F89_4E: ym < 22")]
			public const int StrafeBandRows = 22;

			/// <summary>Up to this row from the bottom (6H/15, ym &lt;= 45) the middle band turns
			/// without walking; above it the pointer walks.</summary>
			[Exe("seg034_2F89_4E: ym <= 45")]
			public const int WalkBandRows = 46;

			/// <summary>The borders as fractions of the area, as UWPointerZones applies them to the
			/// hole: x from the left, the band borders as y from the TOP (1 - rows / H).</summary>
			[Derived("LeftBorderColumn / 172")]
			public const float LeftBorder = (float)LeftBorderColumn / UWPlayerMotion.PointerAreaWidth;

			[Derived("RightBorderColumn / 172")]
			public const float RightBorder = (float)RightBorderColumn / UWPlayerMotion.PointerAreaWidth;

			[Derived("1 - StrafeBandRows / 113")]
			public const float StrafeBorder = 1f - ((float)StrafeBandRows / UWPlayerMotion.PointerAreaHeight);

			[Derived("1 - WalkBandRows / 113")]
			public const float WalkBorder = 1f - ((float)WalkBandRows / UWPlayerMotion.PointerAreaHeight);
		}

		// ------------------------------------------------- The main menu

		public static class MainMenu
		{
			/// <summary>The four buttons' places.</summary>
			[Estimated("reference scene / 4")]
			public static readonly (int X, int Y)[] Buttons = { (98, 81), (81, 104), (72, 128), (85, 153) };

			/// <summary>The list of savegames, centred below the lettering.</summary>
			[Estimated("no source recorded")]
			public const int SaveListTop = 84;

			[Estimated("no source recorded")]
			public const int SaveListPitch = 20;

			/// <summary>The screen's palette and the list's two text colours.</summary>
			[Estimated("reference")]
			public const int Palette = 2;

			[Estimated("reference: PaletteIndexSaveGameSelected")]
			public const int SelectedColourIndex = 0xA2;

			[Estimated("reference: PaletteIndexSaveGameUnSelected")]
			public const int UnselectedColourIndex = 0xAA;
		}
	}
}
