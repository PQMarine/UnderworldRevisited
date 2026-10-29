namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CLASSIC SCREEN (UW2 plan phase 3, 2026-10-29): its places and sizes beside
	/// the first game's table (UWClassicLayout), which stays as it is so that the first game does not
	/// move by a pixel. The areas carry the first game's names where they mean the same thing, and the
	/// same source tags (UWClassicLayout.ExeAttribute and the rest); an Exe note here cites Underworld
	/// Exhumed's UW2 sources (uw2/src, file and line), which rebuild UW2.EXE byte for byte.
	///
	/// Coordinates as in the first game's table: the original's screen pixels, x from the left and y
	/// from the TOP. UW2.EXE counts y up from the screen's bottom (UE include/sys.h:160, ui/MOUSE.C:7),
	/// so a row here is 199 minus its y - the rule that turns the first game's place_3d_view(0x34, 0xB4,
	/// 0xAC, 0x71) into UWClassicLayout's 52/19.
	///
	/// Filled area by area as the steps of phase 3 need them (Docs/UW2-PLAN.md).
	/// </summary>
	public static class UWClassicLayoutUw2
	{
		// ------------------------------------------------- The frame and its 3D view

		public static class Frame
		{
			/// <summary>The game screen's picture: BYT.ARK block 4, served as the first game's MAIN.</summary>
			[UWClassicLayout.Exe("uw2 game/UWEDIT.C:336, gfx/SHOWPIC.C:6-11 (block 4 the game screen)")]
			public const int GameScreenBlock = 4;

			[UWClassicLayout.Exe("uw2 game/UWEDIT.C:333 place_3d_view(0x10, 0xB6, 0xD0, 0x80)")]
			public const int ViewWindowLeft = 0x10;

			/// <summary>
			/// The view's top row: 16, the row ABOVE 199 - 0xB6 (17), where place_3d_view's mouse regions put
			/// it (mous_player: rows 17 to 144). The frame buffer is drawn one row higher: the screen's black
			/// window is rows 16 to 143, 3DWIN.GR 0's two rows equal the screen's rows 16 and 17 at their
			/// place, and the weapons' pictures end on row 143 - with 17 a row of the frame showed under the
			/// fist (per user, 2026-10-29).
			/// </summary>
			[UWClassicLayout.Measured("BYT.ARK block 4's black window rows 16 to 143; 3DWIN.GR 0 and WEAP.GR fit it (user, 2026-10-29)")]
			public const int ViewWindowTop = 16;

			[UWClassicLayout.Exe("uw2 game/UWEDIT.C:333 place_3d_view(0x10, 0xB6, 0xD0, 0x80)")]
			public const int ViewWindowWidth = 0xD0;

			[UWClassicLayout.Exe("uw2 game/UWEDIT.C:333 place_3d_view(0x10, 0xB6, 0xD0, 0x80)")]
			public const int ViewWindowHeight = 0x80;

			/// <summary>The renderer's zoom in the game: the first game's 0x61A8 (UWClassicLayout.Frame.ViewZoom),
			/// so the view spans the same 74.7 degrees from top to bottom; the wider window shows about 91
			/// across instead of 88.</summary>
			[UWClassicLayout.Exe("uw2 3d/VIEW3D.C:141-148 place_3d_view: curZoom = 0x61A8 in the game mode")]
			public const int ViewZoom = 0x61A8;

			/// <summary>The aspect word scale_matrix reads, as the first game's: the VGA pixel's 5/6.</summary>
			[UWClassicLayout.Measured("UW2.EXE file 0x53B94 (DS:14A4 of 3d/INSTANCE.ASM scale_matrix), 0x6A3D")]
			public const int ViewAspectWord = 0x6A3D;

			/// <summary>tan of the half angle at the view's top and bottom edge: the zoom.</summary>
			[UWClassicLayout.Derived("ViewZoom / 32768")]
			public const float VerticalViewTangent = ViewZoom / 32768f;
		}

		// ------------------------------------------------- Over the view: the gargoyle's teeth and the weapon

		/// <summary>
		/// What do_fbuf_bms draws into the view's frame buffer each frame: the weapon, then 3DWIN.GR 0 at
		/// 0x5E and y 0x80 - the gargoyle's teeth over the view's first two rows (the screen's own pixels
		/// there, measured). The frame buffer's y counts up from its bottom: a picture's top row in the view
		/// is 128 minus its y (a fist of nine rows at y 9 ends on the view's last row).
		/// </summary>
		public static class ViewOverlay
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:903 pic_to_fbuf(0x1090, 0x5E, 0x80)")]
			public const int TeethX = 0x5E;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:903 pic_to_fbuf(0x1090, 0x5E, 0x80)")]
			public const int TeethY = 0x80;

			/// <summary>The view's top row for a frame buffer y: 128 - y.</summary>
			[UWClassicLayout.Measured("3DWIN.GR 0's two rows equal BYT.ARK block 4's rows 16 and 17, x 110 to 129")]
			public const int TopY = 0x80;
		}

		/// <summary>
		/// The first-person weapon (do_weapload, adjust_weapon, get_wfr): kind 0 to 2 a melee weapon by its
		/// skill (OBJECTS.DAT's 3 sword, 4 axe, 5 mace, less 3), 3 the fist - an empty hand, anything but a
		/// weapon, a missile weapon of class 0 to 7 -, none for the rest. WEAP.DAT's record ((1 - lefty) * 4
		/// + kind) and WEAP.GR's pictures from (1 - lefty) * 0x7C + kind * 0x1F; step n shows picture
		/// wframe[n] &amp; 0x1F at weap_x, weap_y. Drawn from the fight icon: steps 0x1E down to 0x1B (setting 3),
		/// ready at 0x1B (4), sheathed 0x1B to 0x1D (5), then hidden (6); a step each eighth of a second (the
		/// fast elements). The pictures' sixteen colours are WEAP.CM's set for the body (the second for body
		/// 1), loaded into palette 30 (load_weapcm).
		/// </summary>
		public static class Weapon
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:569 off = ((1 - player->lefty) * 4 + weapid) * 0x61")]
			public const int KindsPerHand = 4;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:581 start = (1 - player->lefty) * 0x7C + weapid * 0x1F")]
			public const int PicturesPerHand = 0x7C;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:581 start = (1 - player->lefty) * 0x7C + weapid * 0x1F")]
			public const int PicturesPerKind = 0x1F;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:865-868 get_wfr: f + 0x1B drawing and sheathing, 0x1B ready")]
			public const int ReadyStep = 0x1B;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:624, 639 weap_frame = 3 to draw, sheathed at >= 3")]
			public const int DrawSteps = 3;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:894 wframe[n] & 0x1F, the picture of a step")]
			public const int PictureMask = 0x1F;

			/// <summary>The fist's kind, and the melee skills that make kinds 0 to 2.</summary>
			[UWClassicLayout.Exe("uw2 game/PLAYDATA.C:381-384 load_weapon(3)")]
			public const int FistKind = 3;

			[UWClassicLayout.Exe("uw2 game/PLAYDATA.C:376-379 skill clamped to 3..5, load_weapon(skill + 0xFD)")]
			public const int FirstSkill = 3;

			[UWClassicLayout.Exe("uw2 game/PLAYDATA.C:376-379 skill clamped to 3..5")]
			public const int LastSkill = 5;

			/// <summary>The hand's slot: Inventory[8 - lefty].</summary>
			[UWClassicLayout.Exe("uw2 game/PLAYDATA.C:371 ActiveObj = AskInventory(8 - player->lefty)")]
			public const int RightHandSlot = 8;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:700 load_weapcm, off = player->body == 1 ? 0x10 : 0")]
			public const int SecondColourMapBody = 1;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:287 update_screen, the fast ones when now >> 5 changes")]
			public const float StepSeconds = 32f / 256f;
		}

		// ------------------------------------------------- The message scroll

		/// <summary>The game's message scroll, main_scroll: x 0x10 to 0xDF, y 0x1E down to 1 (rows 169
		/// to 198), the paper 0x71, the text 0x76 in FONT_5X6P, rolled edges beside it.</summary>
		public static class MessageLog
		{
			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:59 main_scroll, its third word")]
			public const int Left = 0x10;

			[UWClassicLayout.Derived("199 - 0x1E, main_scroll's first word")]
			public const int Top = 199 - 0x1E;

			[UWClassicLayout.Derived("0xDF - 0x10 + 1, main_scroll's third and fourth words")]
			public const int Width = 0xDF - 0x10 + 1;

			[UWClassicLayout.Derived("0x1E - 1 + 1, main_scroll's first two words")]
			public const int Height = 0x1E;

			/// <summary>The paper the scroll clears to.</summary>
			[UWClassicLayout.Exe("uw2 ui/SCROLLIO.C:82 set_the_color(0x71)")]
			public const int PaperColour = 0x71;

			/// <summary>The text's colour (font_color) and those of the escapes 1 to 6 (spec_col; 0's
			/// 0x75 is drawn in the text's colour here, as the log's code 0 is the plain text).</summary>
			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:59 main_scroll font_color 0x76, :64 spec_col")]
			public static readonly int[] ColourIndices = { 0x76, 0x68, 0x01, 0x02, 0x21, 0x50, 0x48 };

			/// <summary>The left edge's place: four columns left of the text, from the scroll's top.</summary>
			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:116 draw_edges, scroll->top - 4")]
			public const int EdgeLeft = 0x10 - 4;

			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:117 draw_edges, scroll->bottom + 1")]
			public const int EdgeRight = 0xDF + 1;

			[UWClassicLayout.Derived("199 - 0x1E, draw_edges' y")]
			public const int EdgeTop = 199 - 0x1E;

			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:116 draw_edges, pic_to_screen(..., 0x1C, 4)")]
			public const int EdgeHeight = 0x1C;

			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:116 draw_edges, pic_to_screen(..., 0x1C, 4)")]
			public const int EdgeWidth = 4;

			/// <summary>SCRLEDGE.GR's first left and first right picture (0x20AB and 0x20B0 less the
			/// file's first number 0x20AB) and the frames each rolls through.</summary>
			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:114-121 draw_edges, 0x20AB/0x20B0 and five frames")]
			public const int EdgeLeftFirst = 0;

			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:114-121 draw_edges, 0x20AB/0x20B0 and five frames")]
			public const int EdgeRightFirst = 5;

			[UWClassicLayout.Exe("uw2 ui/SCROLL.C:114-121 draw_edges, 0x20AB/0x20B0 and five frames")]
			public const int EdgeFrames = 5;
		}

		// ------------------------------------------------- The compass, the power gem, the eyes

		/// <summary>COMPASS.GR: four faces (by heading and 3), then sixteen needles placed by
		/// nedl_x/nedl_y around 0x78/0x2B; rows top down are 199 minus the sprite's y.</summary>
		public static class Compass
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:171 change_sprite(level[2], 0x5E, 0x33, 0x34, 0x10)")]
			public const int Left = 0x5E;

			[UWClassicLayout.Derived("199 - 0x33")]
			public const int Top = 199 - 0x33;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:130-139 set_compass, 0x2059 + (dir & 3) and 0x205D + dir")]
			public const int FirstNeedle = 4;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:136 move_sprite(level[3], nedl_x[dir] + 0x78, ...)")]
			public const int NeedleBaseX = 0x78;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:136 move_sprite(level[3], ..., nedl_y[dir] + 0x2B)")]
			public const int NeedleBaseY = 0x2B;

			/// <summary>The needles' offsets by heading, north first, clockwise; y counted up.</summary>
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:81 nedl_x")]
			public static readonly int[] NeedleX = { -0x5, -0xC, -0x12, -0x18, -0x1A, -0x1B, -0x14, -0xD, -0x6, 0x2, 0xC, 0x10, 0x13, 0x10, 0xB, 0x3 };

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:82 nedl_y")]
			public static readonly int[] NeedleY = { 0x8, 0x8, 0x7, 0x5, 0x3, 0x2, -0x1, -0x2, -0x3, -0x2, -0x1, 0x2, 0x3, 0x5, 0x7, 0x8 };
		}

		/// <summary>POWER.GR on the compass: picture 0 at rest, drawn transparent.</summary>
		public static class PowerGem
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:443 change_sprite(spr, 0x71, 0x30, 1, 1)")]
			public const int Left = 0x71;

			[UWClassicLayout.Derived("199 - 0x30")]
			public const int Top = 199 - 0x30;
		}

		/// <summary>EYES.GR, the gargoyle's eyes above the view: picture 0 open.</summary>
		public static class Eyes
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:175 change_sprite(eyes, 0x6E, 0xC3, 1, 1)")]
			public const int Left = 0x6E;

			[UWClassicLayout.Derived("199 - 0xC3")]
			public const int Top = 199 - 0xC3;
		}

		// ------------------------------------------------- The command icons

		/// <summary>The icon bar at the bottom right: OPTB.GR 0 plain, 1 lit (0x20C1, 0x20C2); two rows
		/// of three icons of 26 x 15 - 0 fight, 1 use, 2 get above, 3 talk, 4 look, 5 options below. The
		/// selected icon shows lit: the lit bar is drawn on the hidden page and the icon's rectangle
		/// copied (new_IconSelect).</summary>
		public static class Commands
		{
			[UWClassicLayout.Exe("uw2 ui/ICONS.C:79 pic_to_screen(0x20C1, 0xF3, 0x1C, 0x4D, 0x1D)")]
			public const int Left = 0xF3;

			[UWClassicLayout.Derived("199 - 0x1C")]
			public const int Top = 199 - 0x1C;

			[UWClassicLayout.Exe("uw2 ui/ICONS.C:96-98 get_iconreg_button, x / 0x1A")]
			public const int IconWidth = 0x1A;

			[UWClassicLayout.Exe("uw2 ui/ICONS.C:96-98 get_iconreg_button, y / 0xF")]
			public const int IconHeight = 0xF;

			/// <summary>The click area, x 0xF2 to 0x13E and y 0 to 0x1C (rows 171 to 199).</summary>
			[UWClassicLayout.Exe("uw2 ui/ICONS.C:77 input_addmouse(0xF2, 0, 0x13E, 0x1C, ...)")]
			public const int AreaLeft = 0xF2;

			[UWClassicLayout.Exe("uw2 ui/ICONS.C:77 input_addmouse(0xF2, 0, 0x13E, 0x1C, ...)")]
			public const int AreaRight = 0x13E;

			[UWClassicLayout.Derived("199 - 0x1C")]
			public const int AreaTop = 199 - 0x1C;
		}

		// ------------------------------------------------- The pointer's regions in the view

		/// <summary>
		/// mous_player's eight cursor regions around the view, in fifteenths of its width and height
		/// (counted up from its bottom): the outer five fifteenths left and right, the bands below three
		/// and below six fifteenths - the same rule and the same pictures (CURSORS.GR 0 to 8, the first
		/// game's UWCursors order) as UW1's, only over the second game's 208 x 128 window. The left button
		/// drives by the pointer's pixel in that window (parse_playin, UWPlayerMotion.PointerCommand).
		/// </summary>
		public static class PointerZones
		{
			[UWClassicLayout.Exe("uw2 game/PLAYER.C:295-302 mous_player, wid * 5 / 15")]
			public const int SideFifteenths = 5;

			[UWClassicLayout.Exe("uw2 game/PLAYER.C:295-300 mous_player, hgt * 3 / 15")]
			public const int BottomFifteenths = 3;

			[UWClassicLayout.Exe("uw2 game/PLAYER.C:298-302 mous_player, hgt * 6 / 15")]
			public const int MiddleFifteenths = 6;
		}

		// ------------------------------------------------- The flasks

		/// <summary>
		/// The vitality and mana flasks (set_flask): the empty flask (FLASKS.GR 75) at FL_X and y 0x43,
		/// then a slice per twelfth filled, FLASKS.GR first + i at liquid_y[i + 1] - red from 0, poisoned
		/// green from 50, blue from 25. The level is the value times 12 over the maximum, at most 12
		/// (set_screen_frame; vitality's maximum is playerdat's avghit, the record's maxhealth on loading).
		/// </summary>
		public static class Flasks
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:76 FL_X[0]")]
			public const int HealthLeft = 0xF8;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:76 FL_X[1]")]
			public const int ManaLeft = 0x120;

			/// <summary>The empty flask's top row.</summary>
			[UWClassicLayout.Derived("199 - 0x43, set_flask's pic_to_screen(0x2057, FL_X, 0x43)")]
			public const int Top = 199 - 0x43;

			/// <summary>The slices' tops counted up (liquid_y; [0] unused, slice i at [i + 1]).</summary>
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:77 liquid_y")]
			public static readonly int[] LiquidY = { 0x25, 0x29, 0x2B, 0x2D, 0x2F, 0x31, 0x33, 0x34, 0x35, 0x36, 0x38, 0x3A, 0x3C, 0x3E };

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:220-223 set_screen_frame, val * 12 / max, at most 12")]
			public const int Levels = 12;

			/// <summary>FLASKS.GR's first slices (0x200C, 0x203E, 0x2025 less the file's 0x200C) and the
			/// empty flask (0x2057).</summary>
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:115 set_flask, 0x200C")]
			public const int HealthFirst = 0;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:115 set_flask, 0x203E when poisoned")]
			public const int PoisonFirst = 0x32;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:117 set_flask, 0x2025")]
			public const int ManaFirst = 0x19;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:120 set_flask, pic_to_screen(0x2057, ...)")]
			public const int Empty = 0x4B;
		}

		// ------------------------------------------------- The right panel and its inventory page

		/// <summary>PANELS.GR's picture of the page shown (0 inventory, 1 runes, 2 statistics), 79 x 112
		/// at 0xEB and y 0xC0.</summary>
		public static class Panel
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:188 show(0xEB, 0xC0, stdat, 0x70, 0x4F, 0, 0)")]
			public const int Left = 0xEB;

			[UWClassicLayout.Derived("199 - 0xC0")]
			public const int Top = 199 - 0xC0;
		}

		/// <summary>
		/// The inventory page (INVPANEL.C): InvDisplay's picture places, x and the top's y counted up,
		/// with the pictures' sizes - display 0 the body (BODIES.GR female * 5 + body), 1 to 5 the armour
		/// (ARMOR_F or ARMOR_M by sex, the type id &amp; 0x1F plus fifteen times the condition quality / 16,
		/// condition 3 above type 14), 6 to 11 shoulders, hands and rings, 12 to 19 the backpack, 20 the
		/// open bag, 21 and 22 the scroll arrows. DisplayToSlot gives each display's Inventory[] slot (the
		/// first game's order: 0 helmet, 1 chest, 2 gloves, 3 leggings, 4 boots, then shoulders, hands,
		/// rings and the eight of the backpack). The objects show OBJECTS.GR's picture of their id, drawn
		/// transparent, a stack's count over it in FONT_4X5P colour 2.
		/// </summary>
		public static class Inventory
		{
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, x")]
			public static readonly int[] PictureX =
				{ 0x102, 0x10A, 0x109, 0x104, 0x103, 0x108, 0xF3, 0x124, 0xF1, 0x127, 0xFD, 0x11B,
				  0xEF, 0x102, 0x115, 0x128, 0xEF, 0x102, 0x115, 0x128, 0xEF, 0x126, 0x130 };

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, y counted up")]
			public static readonly int[] PictureY =
				{ 0xBF, 0xB2, 0xC0, 0xB3, 0xA0, 0x88, 0xBB, 0xBB, 0xA5, 0xA5, 0x95, 0x95,
				  0x76, 0x76, 0x76, 0x76, 0x64, 0x64, 0x64, 0x64, 0x8A, 0x85, 0x85 };

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, w")]
			public static readonly int[] PictureWidth =
				{ 0x24, 0x13, 0x14, 0x21, 0x21, 0x15, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
				  0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x8, 0x8 };

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, h")]
			public static readonly int[] PictureHeight =
				{ 0x45, 0x32, 0x14, 0x2C, 0xC, 0xD, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10,
				  0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0xA, 0xA };

			/// <summary>The hit rectangles (FindInventoryHit: the first whose left &lt;= x &lt;= right and
			/// bottom &lt;= y &lt;= top, y counted up).</summary>
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, left")]
			public static readonly int[] HitLeft =
				{ 0x0, 0x10B, 0x10B, 0x105, 0x105, 0x105, 0xF2, 0x123, 0xF0, 0x126, 0xEF, 0x11C,
				  0xEE, 0x101, 0x114, 0x127, 0xEE, 0x101, 0x114, 0x127, 0xEE, 0x125, 0x12F };

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, top")]
			public static readonly int[] HitTop =
				{ 0x0, 0x93, 0xC2, 0xB1, 0x9F, 0x85, 0xBE, 0xBE, 0xA8, 0xA8, 0x96, 0x96,
				  0x79, 0x79, 0x79, 0x79, 0x67, 0x67, 0x67, 0x67, 0x8C, 0x85, 0x85 };

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, right")]
			public static readonly int[] HitRight =
				{ 0x0, 0x11B, 0x11C, 0x121, 0x121, 0x121, 0x103, 0x134, 0x101, 0x137, 0x10A, 0x136,
				  0xFF, 0x112, 0x125, 0x138, 0xFF, 0x112, 0x125, 0x138, 0xFF, 0x12E, 0x138 };

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:51-75 InvDisplay, bottom")]
			public static readonly int[] HitBottom =
				{ 0x0, 0x86, 0xB2, 0xA0, 0x93, 0x7A, 0xAD, 0xAD, 0x95, 0x95, 0x8B, 0x85,
				  0x68, 0x68, 0x68, 0x68, 0x56, 0x56, 0x56, 0x56, 0x7B, 0x7C, 0x7C };

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:78 DISPLAY_TO_SLOT")]
			public static readonly int[] DisplayToSlot = { 1, 3, 0, 1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };

			/// <summary>The paperdoll's armour displays in their drawing order.</summary>
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:92 ArmorSlots")]
			public static readonly int[] ArmourDisplays = { 1, 3, 5, 4, 2 };

			/// <summary>The displays of shoulders, hands and rings, and of the backpack (displayInventoryArray
			/// 6 to 7, 10 to 11; DisplayInventory 6 to 0x16).</summary>
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:362, 369, 804 displayInventoryArray(6, 7), (10, 11), (6, 0x16)")]
			public const int FirstItemDisplay = 6;

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:78 DISPLAY_TO_SLOT, display 19 is slot 18, the backpack's last")]
			public const int LastItemDisplay = 19;

			/// <summary>Armour types above this are drawn in condition 3.</summary>
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:348 if (type > 14) q = 3")]
			public const int LastConditionedType = 14;

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:355 type += q * 15")]
			public const int ArmourPicturesPerCondition = 15;

			/// <summary>The weight left: centred on 8 + 0x129, its top at y 0x8F, colour 0x88.</summary>
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:878 string_to_screen(buf, 8 - width / 2 + 0x129, 0x8F)")]
			public const int WeightCentreX = 0x129 + 8;

			[UWClassicLayout.Derived("199 - 0x8F")]
			public const int WeightTop = 199 - 0x8F;

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:876 *foreground_color = 0x88")]
			public const int WeightColour = 0x88;

			/// <summary>The open bag (BAGS.C OpenTheBag, FixOpenBag): INV.GR 6 (0x2073) over the backpack's rows
			/// at 0xEE and y 0x7A, the bag itself on display 20 (Inventory[19]), its contents on the backpack's
			/// displays, the arrows 21 and 22 (BUTTONS.GR 0x1B and 0x1C, 0x101B and 0x101C) moving them by a
			/// row of four (ScrollItemsUp: Inventory[20] = Inventory[24]).</summary>
			[UWClassicLayout.Exe("uw2 inv/BAGS.C:299 pic_to_screen(0x2073, 0xEE, 0x7A, 0x2B, 0x4C)")]
			public const int OpenBagBackLeft = 0xEE;

			[UWClassicLayout.Derived("199 - 0x7A")]
			public const int OpenBagBackTop = 199 - 0x7A;

			[UWClassicLayout.Exe("uw2 inv/BAGS.C:299 0x2073, INV.GR's first 0x206D")]
			public const int OpenBagBackPicture = 0x2073 - 0x206D;

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:78 DISPLAY_TO_SLOT, display 20 is slot 19")]
			public const int OpenBagDisplay = 20;

			[UWClassicLayout.Exe("uw2 inv/BAGS.C:60-64 DoSpecialActions 0x15 up, 0x16 down")]
			public const int UpArrowDisplay = 0x15;

			[UWClassicLayout.Exe("uw2 inv/BAGS.C:60-64 DoSpecialActions 0x15 up, 0x16 down")]
			public const int DownArrowDisplay = 0x16;

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:394-398 DisplayInvObject, 0x101B and 0x101C, BUTTONS.GR from 0x1000")]
			public const int UpArrowPicture = 0x1B;

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:394-398 DisplayInvObject, 0x101B and 0x101C, BUTTONS.GR from 0x1000")]
			public const int DownArrowPicture = 0x1C;

			[UWClassicLayout.Exe("uw2 inv/BAGS.C:354 ScrollItemsUp, Inventory[20] = Inventory[24]")]
			public const int ScrollStep = 4;

			/// <summary>A stack's count: FONT_4X5P colour 2 at the picture's x + 3 and y - 1.</summary>
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:848-853 displayInventoryArray")]
			public const int CountColour = 2;
		}

		// ------------------------------------------------- The chain, the flasks' clicks and the panel's slide

		/// <summary>
		/// The flasks' click area (flask_info): x 0xF3 to 0x13C, y 0x24 to 0x45. Between relative x 0x16 and
		/// 0x2C and above relative y 10 the chain is pulled (pull_chain: the inventory page turns to the
		/// statistics, any other back to the inventory); at or below relative y 0x1E a flask says its value,
		/// the vitality left of relative x 0x28.
		/// </summary>
		public static class FlaskClicks
		{
			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:143 input_addmouse(0xF3, 0x24, 0x13C, 0x45, ...)")]
			public const int Left = 0xF3;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:143 input_addmouse(0xF3, 0x24, 0x13C, 0x45, ...)")]
			public const int Right = 0x13C;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:143 input_addmouse(0xF3, 0x24, 0x13C, 0x45, ...)")]
			public const int BottomY = 0x24;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:143 input_addmouse(0xF3, 0x24, 0x13C, 0x45, ...)")]
			public const int TopY = 0x45;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:109 flask_info, x > 0x16 && x < 0x2C")]
			public const int ChainFrom = 0x16;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:109 flask_info, x > 0x16 && x < 0x2C")]
			public const int ChainTo = 0x2C;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:110 flask_info, y > 10")]
			public const int ChainAbove = 10;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:113 flask_info, y > 0x1E returns")]
			public const int InfoUpTo = 0x1E;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:117 flask_info, x < 0x28 the vitality")]
			public const int ManaFrom = 0x28;
		}

		/// <summary>
		/// The panel's slide (do_panel_frame): twenty frames, one each time the fast elements step (every
		/// 32 ticks of the 1/256 s game clock, update_screen); the first ten move the old page eight columns
		/// right a frame (vcopy), the next ten bring the new one in from the right ((20 - frame) * 8 + 0xEB),
		/// clipped to the panel. CHAINS.GR 0 and 1 show on frames 1 and 4, 2 and 3 on frames 13 and 16,
		/// at 0x110 and y 0x50; frame 1 fills the strip the old page leaves in colour 1.
		/// </summary>
		public static class PanelSlide
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:843 pbuf.frame == 0x14")]
			public const int Frames = 0x14;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:824-825 frame <= 10: vcopy")]
			public const int OutFrames = 10;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:825, 836 vcopy to 0xF4 from 0xEC; (0x14 - frame) * 8")]
			public const int StepColumns = 8;

			/// <summary>Seconds a frame: 32 ticks of the 1/256 s clock.</summary>
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:287 update_screen, the fast ones when now >> 5 changes")]
			public const float FrameSeconds = 32f / 256f;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:822 pic_to_screen(frame, 0x110, 0x50, 1, 1)")]
			public const int ChainLeft = 0x110;

			[UWClassicLayout.Derived("199 - 0x50")]
			public const int ChainTop = 199 - 0x50;

			/// <summary>The frames that draw a chain picture, and which (CHAINS.GR 0 to 3).</summary>
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:816-820 frame % 3 == 1: 0x2089 + frame / 3, 0x208A + (frame - 10) / 3")]
			public static readonly int[] ChainFrames = { 1, 4, 13, 16 };

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:841 set_the_color(1)")]
			public const int StripColour = 1;
		}

		// ------------------------------------------------- The rune page, the shelf, the active spells

		/// <summary>
		/// The rune page (PANELS.GR 1, RUNES.C): rune r's picture (OBJECTS.GR 0xE8 + r, drawn transparent)
		/// at x (r &amp; 3) * 0x12 + 0xF1, top y 0xBC - (r / 4) * 0xF. A click in the page's bottom 0x12 rows
		/// clears the shelf, elsewhere it finds rune 0x14 - ((y - 0x12) / 0xF) * 4 + x / 0x12 (relative to
		/// 0xF0 and 0x51): the left button puts it on the shelf (a fourth pushes the oldest off), the right
		/// looks at it. The rune bag's use slides the panel here (OpenTheBag: class 0xF). The shelf's three
		/// at rune_x and y 0x32, the active spells' at spell_x and y 0x37 (SPELLS.GR by spell_class_values
		/// of the word's class plus its subclass; 0x1E none).
		/// </summary>
		public static class Runes
		{
			[UWClassicLayout.Exe("uw2 ui/PANELS.C:101 panel_dispatch[1] RedispRune")]
			public const int PanelPage = 1;

			[UWClassicLayout.Exe("uw2 include/items.h:118 FIRST_RUNESTONE")]
			public const int FirstRunestone = 0xE8;

			[UWClassicLayout.Exe("uw2 combat/RUNES.C:65 ShowRune, (rune & 3) * 0x12 + 0xF1")]
			public const int Left = 0xF1;

			[UWClassicLayout.Exe("uw2 combat/RUNES.C:65 ShowRune, (rune & 3) * 0x12")]
			public const int StepX = 0x12;

			[UWClassicLayout.Exe("uw2 combat/RUNES.C:65 ShowRune, 0xBC - (rune >> 2) * 0xF")]
			public const int TopY = 0xBC;

			[UWClassicLayout.Exe("uw2 combat/RUNES.C:65 ShowRune, 0xBC - (rune >> 2) * 0xF")]
			public const int StepY = 0xF;

			[UWClassicLayout.Exe("uw2 combat/RUNES.C:101 mous_in_rune, inplist->y < 0x12 clears the shelf")]
			public const int ClearStrip = 0x12;

			[UWClassicLayout.Exe("uw2 combat/RUNES.C:104 mous_in_rune, 0x14 - ((y - 0x12) / 0xF << 2) + x / 0x12")]
			public const int TopRowFirst = 0x14;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:99 rune_x")]
			public static readonly int[] ShelfX = { 0xB3, 0xC1, 0xCF };

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:718 change_sprite(spr[i], rune_x[i], 0x32, 0x10, 0x10)")]
			public const int ShelfY = 0x32;

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:100 spell_x")]
			public static readonly int[] SpellX = { 0x2F, 0x21, 0x13 };

			[UWClassicLayout.Exe("uw2 ui/PANELS.C:742 change_sprite(spr[i], spell_x[i], 0x37, 0x10, 0x12)")]
			public const int SpellY = 0x37;

			/// <summary>SPELLS.GR's first picture of each class of an active spell's word (low four bits); the
			/// subclass (bits 4-7) is added; 0x80 and up no class.</summary>
			[UWClassicLayout.Exe("uw2 game/PLAYDATA.C:275 spell_class_values")]
			public static readonly int[] SpellClassFirst = { 0x14, 0xFF, 0x13, 0x05, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x11, 0x80, 0x80, 0x80, 0x80 };

			[UWClassicLayout.Exe("uw2 game/PLAYDATA.C:284 memset(out, 0x1E, 3), no icon")]
			public const int NoSpellPicture = 0x1E;

			/// <summary>The cast area over the shelf (try_cast), x 0xA9 to 0xDF and y 0x22 to 0x34.</summary>
			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:140 input_addmouse(0xA9, 0x22, 0xDF, 0x34, ..., try_cast)")]
			public const int CastLeft = 0xA9;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:140 input_addmouse(0xA9, 0x22, 0xDF, 0x34, ..., try_cast)")]
			public const int CastRight = 0xDF;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:140 input_addmouse(0xA9, 0x22, 0xDF, 0x34, ..., try_cast)")]
			public const int CastBottomY = 0x22;

			[UWClassicLayout.Exe("uw2 ui/GAMESCR.C:140 input_addmouse(0xA9, 0x22, 0xDF, 0x34, ..., try_cast)")]
			public const int CastTopY = 0x34;
		}

		// ------------------------------------------------- The statistics page

		/// <summary>
		/// The statistics page (STATS.C, PANELS.GR 2): FONT_5X6I, the header and the numbers in 0xC4, the
		/// skills in 0xC9; numbers right-aligned at 0x135, names from 0xF0. The name (15 characters, upper
		/// case) centred over 0x48 columns from 0xF0 at y 0xBD; the class and the level with its ordinal at
		/// y 0xB6; the three attributes at (2 - i) * 7 + 0xA1; vitality and mana as "now/most" at 0x9A and
		/// 0x93; the experience in tenths at 0x8C; six skill rows from 0x84 down by 7, the first of them the
		/// skill points while the list is at its top. A click in the panel's bottom ten rows scrolls the list
		/// back (left of relative x 0x26) or on, from 0 to 15.
		/// </summary>
		public static class Statistics
		{
			[UWClassicLayout.Exe("uw2 ui/STATS.C:52-58 sp_hdr, string_to_screen(..., 0xF0, ...)")]
			public const int Left = 0xF0;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:57 string_to_screen(buf, 0x135 - string_width(buf), 0xB6)")]
			public const int Right = 0x135;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:52 (0x48 - string_width(buf)) / 2 + 0xF0")]
			public const int NameWidth = 0x48;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:49 strncpy(buf, player->name, 0xF)")]
			public const int NameLength = 0xF;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:52 sp_hdr, y 0xBD")]
			public const int NameY = 0xBD;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:54-58 sp_hdr, y 0xB6")]
			public const int ClassY = 0xB6;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:69 sp_att, (2 - i) * 7 + 0xA1")]
			public const int AttributeBaseY = 0xA1;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:69 sp_att, (2 - i) * 7 + 0xA1; sp_skill 0x84 - i * 7")]
			public const int RowStep = 7;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:82 sp_hp, y 0x9A")]
			public const int VitalityY = 0x9A;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:95 sp_mp, y 0x93")]
			public const int ManaY = 0x93;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:104 sp_xp, y 0x8C")]
			public const int ExperienceY = 0x8C;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:123-124 sp_skill, y 0x84 - i * 7")]
			public const int SkillY = 0x84;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:149 RedispStat, six sp_skill rows")]
			public const int SkillRows = 6;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:173 mvcheck(&v, dir == 1 ? 0xF : 0, 1, dir)")]
			public const int LastSkillTop = 0xF;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:139 *foreground_color = 0xC4")]
			public const int HeaderColour = 0xC4;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:148 *foreground_color = 0xC9")]
			public const int SkillColour = 0xC9;

			/// <summary>The page's click area (mous_in_panel's, BeginInventory): x 0xF0 to 0x13B, y 0x51 up;
			/// the scroll strip below relative y 10, split at relative x 0x26.</summary>
			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:138 input_addmouse(0xF0, 0x51, 0x13B, 0xBE, ...)")]
			public const int AreaBottomY = 0x51;

			[UWClassicLayout.Exe("uw2 inv/INVPANEL.C:138 input_addmouse(0xF0, 0x51, 0x13B, 0xBE, ...)")]
			public const int AreaRight = 0x13B;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:170 mous_in_stat, inplist->y < 10")]
			public const int ScrollStrip = 10;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:172 mous_in_stat, inplist->x < 0x26")]
			public const int ScrollSplit = 0x26;

			/// <summary>String block 2's first class (0x17) and skill (0x1F), and the skill points' row (0x1F +
			/// 100), the original's numbers; the port's strings count one higher.</summary>
			[UWClassicLayout.Exe("uw2 ui/STATS.C:54 get_string((player->pclass + 0x17) | STR_CHARGEN)")]
			public const int FirstClassString = 0x17;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:123 get_string((sk + 0x1F) | STR_CHARGEN)")]
			public const int FirstSkillString = 0x1F;

			[UWClassicLayout.Exe("uw2 ui/STATS.C:119 sk = 100, the skill points")]
			public const int SkillPointsRow = 100;
		}

		// ------------------------------------------------- The options panel

		/// <summary>
		/// THE OPTIONS PANEL (UW2 plan phase 3 step 4, UWOptionsPanelUw2): a group's unlit picture
		/// (OPTBTNS.GR 3 to 7, 80 x 116) over the right panel at x 0xEC, y 0xC2 counted up; button b is
		/// that picture's rows 16b + 2 to 16b + 17, 79 columns, lit from the group's lit picture (8 to 12)
		/// at the same rows; a message row r (the group's message picture's rows 16r to 16r + 15) lies over
		/// button 0. The sound page has its own title on button 1 (OPTBTNS.GR 14's row 2). The click area
		/// counts buttons up from y 0x4E: 6 - (y - 0x4E) / 16, x 0 to 0x50 from 0xEC. While the panel is
		/// up, the command bar shows OPTBTNS.GR 2 instead of 0.
		/// </summary>
		public static class Options
		{
			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:428 copy_rectangle(0xEC, 0xC2, 0x50, 0x74, 4, 0xC6, 1)")]
			public const int Left = 0xEC;

			[UWClassicLayout.Derived("199 - 0xC2")]
			public const int Top = 199 - 0xC2;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:428 copy_rectangle(0xEC, 0xC2, 0x50, 0x74, 4, 0xC6, 1)")]
			public const int Width = 0x50;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:428 copy_rectangle(0xEC, 0xC2, 0x50, 0x74, 4, 0xC6, 1)")]
			public const int Height = 0x74;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:439 draw_button, copy_rectangle(0xEC, 0xC2 - (b << 4) - 2, 0x4F, 0x10, ...)")]
			public const int ButtonWidth = 0x4F;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:439 draw_button, copy_rectangle(0xEC, 0xC2 - (b << 4) - 2, 0x4F, 0x10, ...)")]
			public const int ButtonHeight = 0x10;

			/// <summary>Button b's first row in the group's picture and below the panel's top: 16b + 2.</summary>
			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:439 draw_button, 0xC2 - (b << 4) - 2")]
			public const int ButtonOffset = 2;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:439 draw_button, b << 4")]
			public const int ButtonStep = 0x10;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:476 reload_gr_vpic(0x20C3, \"optbtns\", unhilit_pics[n]), :60 { 3, 4, 5, 6, 7 }")]
			public const int FirstUnlitPicture = 3;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:478 reload_gr_vpic(0x20C3, \"optbtns\", hilit_pics[n]), :59 { 8, 9, 10, 11, 12 }")]
			public const int FirstLitPicture = 8;

			/// <summary>The groups' message pictures (game, detail, quit, file, music or sound), -1 for none.</summary>
			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:61 message_pics[5] = { -1, 13, -1, 14, 15 }")]
			public static readonly int[] MessagePictures = { -1, 13, -1, 14, 15 };

			/// <summary>A message row's height in its picture (row r at 16r).</summary>
			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:343 load_message, 0x4F - (row << 4)")]
			public const int MessageStep = 0x10;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:481 reload_gr_vpic(0x20C4, \"optbtns\", 0xE)")]
			public const int SoundTitlePicture = 0xE;

			/// <summary>The sound page's title: OPTBTNS.GR 14's rows from 0x4F - 0x2F onto button 1.</summary>
			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:484 copy_rectangle(4, 0xB4, 0x4F, 0x10, 4, 0x2F, 0x63)")]
			public const int SoundTitleRow = 0x4F - 0x2F;

			[UWClassicLayout.Derived("(0xC6 - 0xB4 - ButtonOffset) / ButtonStep")]
			public const int SoundTitleButton = (0xC6 - 0xB4 - ButtonOffset) / ButtonStep;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:101 reload_gr_vpic(0x20C1, \"optbtns\", 2)")]
			public const int OpenBarPicture = 2;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:413-416 get_buttonreg_button, x -= 0xEC")]
			public const int AreaLeft = 0xEC;

			[UWClassicLayout.Exe("uw2 ui/WRAPPER.C:413-416 get_buttonreg_button, y -= 0x4E")]
			public const int AreaBottomY = 0x4E;

			/// <summary>"You cannot select options partway through an action." (a shortcut with an object on
			/// the pointer); the save outcomes from 0xAE + 1 ("No save game there.") to 0xAE + 6 ("Save Game
			/// Aborted."); the descriptions' heading; the description's question - the original's numbers,
			/// the port's strings count one higher.</summary>
			[UWClassicLayout.Exe("uw2 ui/ICONS.C:160 game_sprint(0xAE)")]
			public const int NotNowString = 0xAE;

			[UWClassicLayout.Exe("uw2 game/GAMEWRAP.C:221 game_sprint(msg + 0xAE)")]
			public const int FirstOutcomeString = 0xAE;

			[UWClassicLayout.Exe("uw2 game/GAMEWRAP.C:176 game_sprint(0x120)")]
			public const int DescriptionsString = 0x120;

			[UWClassicLayout.Exe("uw2 game/GAMEWRAP.C:262 game_sprint(0xB7)")]
			public const int DescriptionPromptString = 0xB7;

			[UWClassicLayout.Exe("uw2 game/GAMEWRAP.C:263 wdialog(0, desc, desc, 1, 0x1E)")]
			public const int DescriptionLength = 0x1E;
		}

		// ------------------------------------------------- The main menu

		/// <summary>
		/// THE MAIN MENU (UW2 plan phase 3 step 5 part C, Underworld Exhumed uw2 ui/MAINMENU.C): BYT.ARK's
		/// screen 5 in palette 2 with OPBTN.GR's four buttons - the fourth, Journey Onward, only with a saved
		/// game, and the light on Create Character without one, on Journey Onward with one. Journey Onward
		/// lists the saves found, and only those, centred in FONT_BIG; the one chosen is announced with "You
		/// reenter the Underworld . . .". Every y is counted up and names a top edge.
		/// </summary>
		public static class MainMenu
		{
			/// <summary>The buttons' left edges and tops (struct Button btns[4]).</summary>
			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:131-136 btns[4] = { 0x62, 0x76 }, { 0x51, 0x5F }, { 0x48, 0x47 }, { 0x55, 0x2E }")]
			public static readonly int[] ButtonX = { 0x62, 0x51, 0x48, 0x55 };

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:131-136 btns[4]")]
			public static readonly int[] ButtonY = { 0x76, 0x5F, 0x47, 0x2E };

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:141 sel = found == 0 ? 1 : 3")]
			public const int LitWithoutSaves = 1;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:141 sel = found == 0 ? 1 : 3")]
			public const int LitWithSaves = 3;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:160 read_quikpal(2, pal)")]
			public const int Palette = 2;

			/// <summary>The save list: the lit description in colour 7, the others in 0xCD, centred on x 0xA0,
			/// the first at y 100 and each next 0x16 lower, cut to 0x13E columns.</summary>
			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:103 *foreground_color = 7")]
			public const int ListLitColour = 7;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:106 *foreground_color = 0xCD")]
			public const int ListColour = 0xCD;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:111 string_to_screen(s[i], 0xA0 - w / 2, 100 - i * 0x16)")]
			public const int ListCentre = 0xA0;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:111 100 - i * 0x16")]
			public const int ListTop = 100;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:111 100 - i * 0x16")]
			public const int ListStep = 0x16;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:109 while ((w = string_width(s[i])) > 0x13E)")]
			public const int ListWidth = 0x13E;

			/// <summary>"You reenter the Underworld . . ." (block 1, 0x111 - get_string(0x311)) in colour 7,
			/// ten columns right of centre, on y 0x5A.</summary>
			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:382 get_string(0x311)")]
			public const int ReenterString = 0x111;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:386 string_to_screen(msg, (0x140 - string_width(msg)) / 2 + 10, 0x5A)")]
			public const int ReenterShift = 10;

			[UWClassicLayout.Exe("uw2 ui/MAINMENU.C:386 string_to_screen(msg, ..., 0x5A)")]
			public const int ReenterY = 0x5A;
		}

		// ------------------------------------------------- Character creation

		/// <summary>
		/// THE CHARACTER CREATION SCREEN (UW2 plan phase 3 step 5, UWChargenLayoutUw2 arranges with these):
		/// BYT.ARK's screen 1 in palette 3, CHRBTNS.GR's pictures, FONTCHAR.SYS in colour 0xC4. The right
		/// page holds the question and its answers in as many columns as fit (drawopt); the left page what
		/// has been chosen - sex and class on one line, the attributes, the skills, the body, the name. Every
		/// y is counted up from the screen's bottom (row = 199 - y) and names a top edge, as the original's
		/// show and string_to_screen take it.
		/// </summary>
		public static class Chargen
		{
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:717 read_quikpal(PAL_CHARGEN, pal), include/gfx.h:185 PAL_CHARGEN 3")]
			public const int Palette = 3;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:741 *foreground_color = *background_color = 0xC4")]
			public const int TextColour = 0xC4;

			/// <summary>The right page's left edge and width: the answers are spaced over 0xA0 columns from it.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:266 opt->spacing = (0xA0 - opt->cols * w) / (opt->cols + 1)")]
			public const int RightPage = 0xA0;

			/// <summary>The height the answers are fitted into and centred in, below the top edge 0xC5.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:264 (0xC4 - (hasq ? 20 : 0))")]
			public const int AnswerArea = 0xC4;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:267 y = 0xC5 - (...) / 2")]
			public const int AnswerTop = 0xC5;

			/// <summary>The room a question takes from the answers' height when the columns are counted.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:264 hasq ? 20 : 0")]
			public const int QuestionRoom = 20;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:264 (h + 4)")]
			public const int AnswerGap = 4;

			/// <summary>The question's place: from x 0xA4, centred in 0x91 columns (the name box's width).</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:274 x = 0xA4")]
			public const int QuestionLeft = 0xA4;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:279 x + (0x91 - string_width(s)) / 2")]
			public const int QuestionWidth = 0x91;

			/// <summary>A text's top three rows below the top of the picture it lies on.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:279 string_to_screen(s, ..., y - 3)")]
			public const int TextDrop = 3;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:276 show(x, y, chrbuf + chroff[6], 0x10, 0x91, 0, 0)")]
			public const int NameBoxPicture = 6;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:277 string_to_screen(s, x += 4, y - 3)")]
			public const int NamePromptIndent = 4;

			/// <summary>The answer button (picture 0, lit 1) and the portrait frame (3, lit 4): selopt draws
			/// the new answer with picture + 1, the old with picture + 2, which is picture 0's (and 3's) copy.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:343 pic = chrbuf + chroff[opt->pic + 2 - j]")]
			public const int LitPicture = 1;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:291 show(x, y, chrbuf + chroff[opt->pic], ...), opt->pic 0")]
			public const int AnswerPicture = 0;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:300 case 3: the portraits' frame")]
			public const int PortraitFrame = 3;

			/// <summary>The answer button's height, which drawopt also takes for the name's question (its
			/// record names picture 0): the name box stands where a question with no answers would.</summary>
			[UWClassicLayout.Measured("CHRBTNS.GR 0, 67 x 16")]
			public const int AnswerHeight = 0x10;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:583 opts[4].strings[0] = choice ? 12 : 7")]
			public const int MalePortraits = 7;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:583 opts[4].strings[0] = choice ? 12 : 7")]
			public const int FemalePortraits = 12;

			/// <summary>The bodies: picture 0x11 + female * 5 + the portrait chosen.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:619 chroff[player->female * 5 + choice + 0x11]")]
			public const int FirstBody = 0x11;

			/// <summary>The body's box on the left page: centred in 0x41 columns from 0x0F and in 0x5E rows
			/// below 0xA5.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:623 x = (0x41 - w) / 2 + 0x0F")]
			public const int BodyLeft = 0x0F;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:623 x = (0x41 - w) / 2 + 0x0F")]
			public const int BodyWidth = 0x41;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:624 y = 0xA5 - (0x5E - h) / 2")]
			public const int BodyTop = 0xA5;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:624 y = 0xA5 - (0x5E - h) / 2")]
			public const int BodyHeight = 0x5E;

			/// <summary>Sex at x 0x11 and the class ending at 0x8F, both on y 0xB2.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:586 string_to_screen(s, 0x11, 0xB2)")]
			public const int SexLeft = 0x11;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:602 string_to_screen(s, 0x8F - string_width(s), 0xB2)")]
			public const int ClassRight = 0x8F;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:586 string_to_screen(s, 0x11, 0xB2)")]
			public const int SexClassY = 0xB2;

			/// <summary>The name centred in 0x7E columns from 0x11 on y 0xBD.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:639 string_to_screen(s, (0x7E - string_width(s)) / 2 + 0x11, 0xBD)")]
			public const int NameLeft = 0x11;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:639 string_to_screen(s, (0x7E - string_width(s)) / 2 + 0x11, 0xBD)")]
			public const int NameWidth = 0x7E;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:639 string_to_screen(s, (0x7E - string_width(s)) / 2 + 0x11, 0xBD)")]
			public const int NameY = 0xBD;

			/// <summary>The attributes (Str, Dex, Int, Vit): labels at 0x5D, values ending at 0x8C, from y
			/// 0x96 down by 0x12.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:170 string_to_screen(\"Str:\", 0x5D, 0x96)")]
			public const int AttributeLeft = 0x5D;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:171 string_to_screen(buf, 0x8C - string_width(buf), 0x96)")]
			public const int AttributeRight = 0x8C;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:170 string_to_screen(\"Str:\", 0x5D, 0x96)")]
			public const int AttributeY = 0x96;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:173 string_to_screen(\"Dex:\", 0x5D, 0x84)")]
			public const int AttributeStep = 0x96 - 0x84;

			/// <summary>The skills held, up to six: names at 0x1E, values ending at 0x7D, from y 0x43 down by 11.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:197 string_to_screen(name, 0x1E, 0x43 - row * 11)")]
			public const int SkillLeft = 0x1E;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:198 string_to_screen(buf, 0x7D - string_width(buf), ...)")]
			public const int SkillRight = 0x7D;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:197 string_to_screen(name, 0x1E, 0x43 - row * 11)")]
			public const int SkillY = 0x43;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:197 0x43 - row * 11")]
			public const int SkillStep = 11;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:193 for (i = 0; i < 20 && row <= 5; i++)")]
			public const int SkillRows = 6;

			/// <summary>"awakens . . ." (block 1, 0x110 - get_string(0x310): the block is id >> 9) on y 0x62,
			/// the name a font's height above it, both centred on the right page.</summary>
			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:662 get_string(0x310)")]
			public const int AwakensString = 0x110;

			[UWClassicLayout.Exe("uw2 game/CHARGEN.C:668 string_to_screen(done, (0xA0 - string_width(done)) / 2 + 0xA0, 0x62)")]
			public const int AwakensY = 0x62;
		}
	}
}
