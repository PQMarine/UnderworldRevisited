using System;
using System.Collections.Generic;
using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT settings.json HOLDS (cleanup step 8.3, 2026-10-24; until then the private class
	/// UWUserSettings.Data): the player's own settings, written and read by JsonUtility on the Unity
	/// side (UWUserSettings, the static facade every caller uses).
	///
	/// THIS CLASS IS THE FILE FORMAT. JsonUtility maps a key to the field of the same name, writes
	/// the fields in their order here, and keeps the initializer of every field a file lacks - so a
	/// field is never renamed or removed (a retired one stays, as DisplayAs4By3 does), and the
	/// initializers are the defaults a fresh start and an older file read. Moving a field or adding
	/// one changes the bytes written from then on: record the fixtures again (Tools/Fixtures/settings,
	/// UWSettingsFixtureCheck) and add its entry to UWUserSettingsTable, in the same place.
	///
	/// Plain fields only, nothing else public: what the game makes of a stored value - the clamps,
	/// the legacy quirks of older files, "not chosen yet" - is the table's (UWUserSettingsTable), not
	/// this class's.
	/// </summary>
	[Serializable]
	public sealed class UWUserData
	{
		public string GogInstallPath = string.Empty;

		/// <summary>Changed keys, one line per binding: "map|action|index|path" (see
		/// UWKeyBindings).</summary>
		public List<string> KeyBindings = new List<string>();

		/// <summary>Colour help (see UWColourVision): mode, strength, brightness, contrast.</summary>
		public int ColourMode;

		public float ColourStrength = 1f;

		public float ColourBrightness = 1f;

		public float ColourContrast = 1f;

		/// <summary>Hatch the health flask while poisoned, so it is not only the colour that
		/// tells (see UWFlasks).</summary>
		public bool MarkPoison;

		/// <summary>The player deals the class's bonus points at character creation himself
		/// instead of the dice (UWCharacterGeneration.ManualAttributes). Off by default.</summary>
		public bool ManualAttributes;

		/// <summary>The help window as the player left it (per user, 2026-10-01): open or not,
		/// the tab (UWHelpTabId as a number, 3 = Spells) and how many tiles the map
		/// tab shows across.</summary>
		public bool HelpOpen;

		public int HelpTab = 3;

		public int HelpMapSpan = 20;

		/// <summary>The modern scheme's minimap (UWModernMinimap, per user 2026-10-03): its zoom
		/// as screen pixels per map pixel at the default HUD scale (0 = not chosen yet), and
		/// whether it turns with the view - north up by default.</summary>
		public int MinimapZoom;

		public bool MinimapTurns;

		/// <summary>The modern UI's size chosen in the game menu, in percent of the automatic
		/// one, 0 = 100 (UWModernUi.PixelScale).</summary>
		public int ModernUiPercent;

		/// <summary>The modern conversation's text size in percent, 0 = 100 (UWModernConversation,
		/// its + and - in the history's corner).</summary>
		public int ConversationTextPercent;

		/// <summary>The modern HUD's own layout (UWModernLayout): the moved and sized parts - the
		/// Custom preset's, kept while another preset is chosen.</summary>
		public string ModernLayout;

		/// <summary>The modern scheme's interface preset (UWModernLayout.PresetEnum); -1 = not
		/// chosen yet: Custom when an own layout exists, else Modern.</summary>
		public int InterfacePreset = -1;

		/// <summary>The modern minimap switched off (the layout editor's Minimap button).</summary>
		public bool MinimapHidden;

		/// <summary>The action bar switched off, its keys with it (the layout editor; per user,
		/// 2026-10-10).</summary>
		public bool ActionBarHidden;

		/// <summary>The original's compass in the own layout (the layout editor's Compass button,
		/// UWModernCompass).</summary>
		public bool ModernCompass;

		/// <summary>The compass on its stone disc (UWModernLayout.IsCompassDisc), and its black outline.</summary>
		public bool ModernCompassDisc;

		public bool ModernCompassOutline;

		/// <summary>The original's message scroll in the own layout (the layout editor's Scroll
		/// button, UWModernScroll), and its lines (0 = the original's four; Shift+wheel).</summary>
		public bool ModernScroll;

		/// <summary>The parts' generated backs (UWModernBacks).</summary>
		public string ModernBacks;

		public int ModernScrollLines;

		/// <summary>The modern message box's lines (0 = five; Interaction.ModernMessageCount).</summary>
		public int ModernMessageLines;

		/// <summary>The modern interface's font: 0 modern, 1 the original's (UWUiFonts).</summary>
		public int InterfaceFont;

		/// <summary>The power gem on a stand of its own (UWModernLayout.IsGemApart).</summary>
		public bool ModernGemApart;

		/// <summary>The stone shelf deco panel shown, and its width in its own pixels (0 = default).</summary>
		public bool ModernStoneShelf;

		public int ModernStoneShelfWidth;

		/// <summary>The flasks freed, each on its own (UWModernLayout.IsFlasksApart).</summary>
		public bool ModernFlasksApart;

		/// <summary>The rune hollow as an element (UWModernLayout.IsRuneHollowShown), and its black outline.</summary>
		public bool ModernRuneHollow;

		public bool ModernRuneHollowOutline;

		/// <summary>The original's rune tablet in the rune panel's place (UWModernLayout.IsRuneTabletShown).</summary>
		public bool ModernRuneTablet;

		/// <summary>The original's stats panel as an element (UWModernLayout.IsStatsPanelShown).</summary>
		public bool ModernStatsPanel;

		/// <summary>The original's character panel as an element (UWModernLayout.IsCharacterPageShown).</summary>
		public bool ModernCharacterPage;

		/// <summary>The conversation in the original's look (UWModernLayout.IsConversationOriginal).</summary>
		public bool ModernConversationOriginal;

		/// <summary>The original look's pieces WITHOUT their black outline (on by default, per user
		/// 2026-10-10: "with the outline it looks better").</summary>
		public bool ModernConversationNoOutline;

		/// <summary>The modern pointer is free and the view turns only while the right button is
		/// held, instead of the button toggling (UWModernPointer; toggling by default, per user
		/// 2026-10-03).</summary>
		public bool ModernPointerHold;

		/// <summary>The mouse look's speed in the modern scheme as a factor on the tuned
		/// default, 0.05 to 2 (per user, 2026-10-06: "Die Maussteuerung ist schon sehr
		/// direkt", so adjustable). The stick keeps its own rate.</summary>
		public float MouseLookSpeed = 0.5f;

		/// <summary>The mouse look's up and down reversed in the modern scheme, as StickInvertY
		/// for the stick (UWPlayerLook.ReadLookMouse; pull request 3 by krispit-dev, 2026-10-13).</summary>
		public bool MouseInvertY;

		/// <summary>The slow-walk key switches the slow walk on and off instead of being held
		/// (UWPlayerInput.fIsWalkingSlow; issue 4, 2026-10-21).</summary>
		public bool WalkSlowToggle;

		/// <summary>The gamepad's button names, as UWGamepad.ButtonStyleEnum: 0 Xbox, 1 PlayStation,
		/// 2 Nintendo - chosen, never detected (per user, 2026-10-07: a PlayStation controller
		/// often reports as an Xbox one).</summary>
		public int GamepadButtonStyle;

		/// <summary>The right stick's look speed as a factor on its 180 degrees per second, 0.25 to
		/// 2 (the gamepad tab of the Controls dialog, per user 2026-10-07).</summary>
		public float StickLookSpeed = 1f;

		/// <summary>The sticks swapped - the left one looks, the right one walks (per user,
		/// 2026-10-07: wanted above all by left-handed players; UWKeyBindings.ApplyTo).</summary>
		public bool GamepadSwapSticks;

		/// <summary>The look stick's up and down reversed (UWPlayerLook.ReadLookStick).</summary>
		public bool StickInvertY;

		/// <summary>The deadzone of the physical left and right stick, 0 to 0.5 of the deflection
		/// (per user, 2026-10-08: "sooner or later every controller drifts"; the Input System's own
		/// 0.125 as the default - UWKeyBindings.fApplyStickSwap).</summary>
		public float LeftStickDeadzone = 0.125f;

		/// <summary>The gamepad's button hints in the modern interface - at the crosshair and the
		/// action bar - while the pad is in use (per user, 2026-10-08).</summary>
		public bool GamepadButtonHints = true;

		public float RightStickDeadzone = 0.125f;

		/// <summary>The modern character panel stays out on its Character tab without freeing the
		/// pointer, the minimap in its head (UWModernPanel, per user 2026-10-03).</summary>
		public bool ModernPanelPinned;

		/// <summary>The modern rune panel stays out (UWModernRunePanel, per user 2026-10-04).</summary>
		public bool ModernRunesPinned;

		/// <summary>The control scheme last chosen (Shift+F2), as UWControlScheme.SchemeEnum; -1 =
		/// not chosen yet, the scene's default (per user, 2026-10-04: kept over loading and noclip).</summary>
		public int ControlScheme = -1;

		/// <summary>Seconds between two steps while an easy movement key or arrow is held
		/// down - see UWEasyMovement.DefaultRepeatSeconds.</summary>
		public float EasyMovementInterval = UWEasyMovement.DefaultRepeatSeconds;

		/// <summary>The two company logos, the animated title and the intro at startup. Off
		/// goes straight to the main menu, which is what one wants while testing
		/// (per user, 2026-09-22).</summary>
		public bool ShowOpeningSequence = true;

		/// <summary>The game keeps running while another window has the focus (per user,
		/// 2026-09-28: wanted for idle tests beside the original, "everyone should decide for
		/// themselves"). Off by default, as Unity has it.</summary>
		public bool RunInBackground;

		/// <summary>The classic screen at 4:3 - always on since 2026-10-10 (UWDisplayAspect).
		/// KEPT FOR THE FILE ONLY: settings.json fields are never removed, so a file written by an
		/// older version still reads; nothing reads the value any more.</summary>
		public bool DisplayAs4By3 = true;

		/// <summary>The classic scheme's frame widened to the window (UWClassicWide; per user,
		/// 2026-10-10: the former 4:3 checkbox, the classic screen always at 4:3 now).</summary>
		public bool ClassicWidescreen;

		/// <summary>The 3D view's horizontal angle in degrees, 0 for each scheme's own (UWViewAngle).</summary>
		public float ViewAngle;

		/// <summary>The world drawn at the original's resolution - see UWWorldResolution. Off by
		/// default: the full screen resolution, as before.</summary>
		public bool OriginalWorldResolution;

		/// <summary>Its multiple of the original's resolution, 1 to 4 (UWWorldResolution.Factor).</summary>
		public int OriginalWorldResolutionFactor = 1;

		/// <summary>The palette renderer's ambient occlusion in shade steps, 0 = off
		/// (UWPaletteEffects).</summary>
		public float PaletteAmbientOcclusion;

		/// <summary>The palette renderer's light sources (UWPaletteEffects); off by default.</summary>
		public bool PaletteLightSources;

		/// <summary>The palette renderer's glow (lava, the light sources' flames); off by default.</summary>
		public bool PaletteGlow;

		/// <summary>The palette renderer's grime along the walls in shade steps, 0 = off.</summary>
		public float PaletteGrime;

		/// <summary>The grime's colour (UWGrimeTint.ToneEnum): 0 none, 1 dirt, 2 moss, 3 soot.</summary>
		public int PaletteGrimeTone = 1;

		/// <summary>The palette renderer's ground shadows in shade steps, 0 = off.</summary>
		public float PaletteGroundShadows;

		/// <summary>The palette renderer's hollows (joints darker) in shade steps, 0 = off.</summary>
		public float PaletteCavity;

		/// <summary>The palette renderer's depth (parallax and self shadow), 0 = off.</summary>
		public float PaletteDepth;


		/// <summary>Window or borderless fullscreen (UWDisplayMode.ModeEnum): 0 window, 1 borderless
		/// fullscreen; -1 not chosen yet, then a build starts borderless at the desktop's size.</summary>
		public int DisplayMode = -1;

		/// <summary>The resolution the game renders at; 0 = the desktop's.</summary>
		public int ResolutionWidth;

		public int ResolutionHeight;

		/// <summary>The head bob while walking and the weapon's jitter with it - see UWHeadBobRules:
		/// 0 off, 1 the original's steps, 2 smoothed (per user, 2026-09-23, first a plain on/off
		/// switch). Original by default; off for players who get sick from it.</summary>
		public int HeadBobMode = 1;

		/// <summary>How strong the Smooth head bob is, 0.1 to 1 of the original's amplitude (per
		/// user, 2026-09-23: a slider beside Smooth, none for Off and Original).</summary>
		public float HeadBobStrength = 1f;

		/// <summary>The weapon's jitter, its own three-way switch since 2026-09-23 (per user: "Original
		/// is too tiring for me, but without it something is missing"): 0 off, 1 the original's
		/// jumps, 2 smoothed. Until then the head bob switch turned it off with the bob.</summary>
		public int WeaponJitterMode = 1;

		/// <summary>How strong the Smooth weapon jitter is, 0.1 to 1 of the original's range.</summary>
		public float WeaponJitterStrength = 1f;

		/// <summary>The swimmer's view, a three-way switch like the head bob's (per user,
		/// 2026-10-15: "like the original, but with a switch like the head bob, off as well"):
		/// 0 calm, 1 the original's jitter (UWSwimViewRules), 2 our gentle sway. Original by
		/// default.</summary>
		public int SwimViewMode = 1;

		/// <summary>How strong the Smooth swimming sway is, 0.1 to 1.</summary>
		public float SwimViewStrength = 1f;

		/// <summary>The player's motion on the core (2026-10-06, per user: "per Schalter
		/// einstellbar, wer es nah am Original haben moechte kann das waehlen"): 0 Original - one
		/// motion frame per rendered frame with the ticks that passed, whole units, as UW.EXE
		/// runs it; 1 Smooth - fixed steps of the original's frame length with the view drawn
		/// between them (UWPlayerMovement). Smooth by default.</summary>
		public int MotionSmoothing = 1;

		/// <summary>The picture of the motion (2026-10-06, per user: "soll jeder selbst einstellen"):
		/// 0 even - the view drawn between the simulation steps at the monitor's rate; 1 stepped
		/// - the view set once per step, the 15-20 pictures a second of the original on DOSBox at
		/// fixed cycles. Even by default.</summary>
		public int MotionViewStepped;

		/// <summary>The frame length of the Original motion in PIT ticks - how often UW.EXE ran
		/// its motion frame on the machine one measures against (2026-10-06, per user: DOSBox at
		/// 30000 cycles looks smoother than our 16): 4, 8, 12 or 16 ticks = 64, 32, 21 or 16
		/// frames a second. 8 by default - the user matched 32 fps against his DOSBox by eye.</summary>
		public int OriginalFrameTicks = 8;

		/// <summary>The Smooth motion's ramp (2026-10-06, per user: a long start and stop work
		/// against a sensitive stomach): the speed changes by the original's step per this many
		/// ticks - 16 = 0.56 s to the run (the original at 16 fps), 8 = 0.31 s (at 32 fps, the
		/// user's DOSBox), 4 = 0.16 s, 0 = at once. 8 by default.</summary>
		public int SmoothRampTicks = 8;

		/// <summary>Music and sound, on or off and how loud - see UWSoundOptions. The values
		/// here are the ones the settings asset starts with, so a file written before this
		/// entry existed reads as the old behaviour.</summary>
		public bool MusicEnabled = true;

		public float MusicVolume = 0.6f;

		public bool SoundEnabled = true;

		public float SoundVolume = 0.8f;

		/// <summary>What plays the music (UWMusicDeviceId): 0 the AdLib, 1 General
		/// MIDI, 2 the MT-32. 0 by default, the sound of the original as most heard it.</summary>
		public int MusicDevice;

		/// <summary>The folder with the player's own MT-32 ROMs; empty = the MT32 folder beside
		/// settings.json.</summary>
		public string Mt32RomPath = "";

		/// <summary>The debug panel's folded sections (UWDebugPanel, cleanup 8c, 2026-10-25): their ids,
		/// comma-separated (UWDebugFolds); empty folds nothing, as every older file reads.</summary>
		public string DebugFolded = "";

		/// <summary>The pointer's speed in the running game, where the mouse moves the game's own
		/// pointer by its counts (UWPointer, cleanup step 7.7B, 2026-10-25): screen pixels per mouse
		/// count, 0.25 to 4, no acceleration. 1, one count per pixel, is what Windows gives at its
		/// default speed without "enhance pointer precision" - and what every older file reads.</summary>
		public float PointerSpeed = 1f;

		/// <summary>The look of the game's own windows (UWWindowThemes, cleanup phase 8d, 2026-10-25): 0
		/// Plain, the look they always had, or 1 Stone, the application icon's (UWWindowThemeEnum).
		/// STONE BY DEFAULT since 2026-10-26 (per user: a pity if nobody sees it for not knowing it
		/// can be switched) - also what a file without the key reads; a file that holds 0 keeps
		/// Plain.</summary>
		public int WindowTheme = (int)UWWindowThemeEnum.Stone;

		/// <summary>What the Stone theme lays under a window (UWWindowBackgroundEnum, cleanup phase 8d):
		/// 0 the stone's gradient, the default, or 1 its clouds (UWThemeGround). Plain ignores it.</summary>
		public int WindowBackground;
	}
}
