using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SETTINGS OF settings.json AS A TABLE (cleanup step 8.3, 2026-10-24; Docs/CLEANUP-PLAN.md
	/// 8.7): one UWSettingDef per field of UWUserData, in the file's order, with its kind, its store,
	/// its limits and the rule by which the game reads a stored value. Until then the properties of
	/// UWUserSettings carried their clamps and quirks by hand, about twenty of them, each written
	/// out on its own. UWUserSettings is the facade over this table and the only place that holds
	/// the data; its properties are one line each.
	///
	/// THE LEGACY QUIRKS ARE ENTRIES HERE, NOT CODE: a mouse or stick speed of 0 or below reads as 1
	/// and an easy movement interval of 0 or below as the default (older builds wrote no value);
	/// a frame or ramp length that is not one of the choices reads as 8; the Off/Original/Smooth
	/// modes out of range read Original; InterfacePreset -1 reads Custom where an own layout or a
	/// hidden minimap exists, else Modern; ControlScheme -1 and DisplayMode -1 are "not chosen yet"
	/// and read as they are; MotionSmoothing and MotionViewStepped are switches kept as 0 or 1.
	/// Tools/Fixtures/settings pins every one of them (UWSettingsFixtureCheck in the editor, the
	/// SettingsTable group of the self-check without it).
	///
	/// A NEW SETTING is a field of UWUserData and an entry here at the same place; the self-check
	/// fails until both agree.
	/// </summary>
	public static partial class UWUserSettingsTable
	{
		/// <summary>The mouse look's speed factor in the modern scheme (per user, 2026-10-06).</summary>
		public const float MinMouseLookSpeed = 0.05f;

		public const float MaxMouseLookSpeed = 2f;

		/// <summary>The slider's and the arrows' step: five per cent (the stick's speed too).</summary>
		public const float MouseLookSpeedStep = 0.05f;

		/// <summary>The game's own pointer: screen pixels per mouse count (per user, 2026-10-25; UWPointer).</summary>
		public const float MinPointerSpeed = 0.25f;

		public const float MaxPointerSpeed = 4f;

		/// <summary>The right stick's look speed factor (per user, 2026-10-07).</summary>
		public const float MinStickLookSpeed = 0.25f;

		public const float MaxStickLookSpeed = 2f;

		/// <summary>The sticks' deadzone, at most half the deflection (per user, 2026-10-08).</summary>
		public const float MaxStickDeadzone = 0.5f;

		/// <summary>The deadzone sliders' step (UWSetupMenu.Gamepad fDeadzoneRow).</summary>
		public const float StickDeadzoneStep = 0.01f;

		/// <summary>Below this the Smooth head bob is not worth having - Off is the switch for none.
		/// The weapon's jitter and the swimming sway take the same limits.</summary>
		public const float MinHeadBobStrength = 0.1f;

		/// <summary>The strength sliders' step (UWSetupMenu's HeadBobStrengthStep).</summary>
		public const float HeadBobStrengthStep = 0.05f;

		/// <summary>The volume sliders' step (UWSoundOptions.VolumeStep).</summary>
		public const float VolumeStep = 0.05f;

		/// <summary>The music devices, UWMusicDeviceId: AdLib, General MIDI, MT-32.</summary>
		public const int MaxMusicDevice = (int)UWMusicDeviceId.Mt32;

		/// <summary>4, 8, 12, 16 ticks: 64, 32, 21, 16 frames a second.</summary>
		public static readonly int[] OriginalFrameChoices = { 4, 8, 12, 16 };

		/// <summary>16, 8, 4, 0 ticks: 0.6 s, 0.3 s, 0.15 s, at once.</summary>
		public static readonly int[] SmoothRampChoices = { 16, 8, 4, 0 };

		/// <summary>What a frame or ramp length read from the file that is none of the choices
		/// reads as: 32 frames a second, and the ramp the user matched against his DOSBox.</summary>
		public const int TicksFallback = 8;

		/// <summary>"Not chosen yet" for the schemes, the display mode and the interface preset.</summary>
		public const int NotChosen = -1;

		public static readonly UWTextSettingDef GogInstallPath = fText(nameof(UWUserData.GogInstallPath), d => d.GogInstallPath, (d, v) => d.GogInstallPath = v, UWSettingNormalize.NullIsEmpty);

		public static readonly UWListSettingDef KeyBindings = new UWListSettingDef(nameof(UWUserData.KeyBindings), d => d.KeyBindings, (d, v) => d.KeyBindings = v)
		{
			Normalize = UWSettingNormalize.NullIsNewList
		};

		public static readonly UWIntSettingDef ColourMode = fInt(nameof(UWUserData.ColourMode), UWSettingKind.Choice, d => d.ColourMode, (d, v) => d.ColourMode = v);

		public static readonly UWFloatSettingDef ColourStrength = fFloat(nameof(UWUserData.ColourStrength), d => d.ColourStrength, (d, v) => d.ColourStrength = v);

		public static readonly UWFloatSettingDef ColourBrightness = fFloat(nameof(UWUserData.ColourBrightness), d => d.ColourBrightness, (d, v) => d.ColourBrightness = v);

		public static readonly UWFloatSettingDef ColourContrast = fFloat(nameof(UWUserData.ColourContrast), d => d.ColourContrast, (d, v) => d.ColourContrast = v);

		public static readonly UWBoolSettingDef MarkPoison = fBool(nameof(UWUserData.MarkPoison), d => d.MarkPoison, (d, v) => d.MarkPoison = v);

		public static readonly UWBoolSettingDef ManualAttributes = fBool(nameof(UWUserData.ManualAttributes), d => d.ManualAttributes, (d, v) => d.ManualAttributes = v);

		public static readonly UWBoolSettingDef HelpOpen = fBool(nameof(UWUserData.HelpOpen), d => d.HelpOpen, (d, v) => d.HelpOpen = v);

		/// <summary>UWHelpTabId as a number: Stats 0 to Controls 6 - the numbers are the file's.</summary>
		public static readonly UWIntSettingDef HelpTab = fInt(nameof(UWUserData.HelpTab), UWSettingKind.Choice, d => d.HelpTab, (d, v) => d.HelpTab = v);

		public static readonly UWIntSettingDef HelpMapSpan = fInt(nameof(UWUserData.HelpMapSpan), UWSettingKind.Int, d => d.HelpMapSpan, (d, v) => d.HelpMapSpan = v);

		public static readonly UWIntSettingDef MinimapZoom = fInt(nameof(UWUserData.MinimapZoom), UWSettingKind.Int, d => d.MinimapZoom, (d, v) => d.MinimapZoom = v);

		public static readonly UWBoolSettingDef MinimapTurns = fBool(nameof(UWUserData.MinimapTurns), d => d.MinimapTurns, (d, v) => d.MinimapTurns = v, UWSettingStore.Layout);

		public static readonly UWIntSettingDef ModernUiPercent = fInt(nameof(UWUserData.ModernUiPercent), UWSettingKind.Int, d => d.ModernUiPercent, (d, v) => d.ModernUiPercent = v, UWSettingStore.Layout);

		public static readonly UWIntSettingDef ConversationTextPercent = fInt(nameof(UWUserData.ConversationTextPercent), UWSettingKind.Int, d => d.ConversationTextPercent, (d, v) => d.ConversationTextPercent = v, UWSettingStore.Layout);

		/// <summary>The Custom preset's moved and sized parts, an opaque string of UWModernLayout's.</summary>
		public static readonly UWTextSettingDef ModernLayout = fText(nameof(UWUserData.ModernLayout), d => d.ModernLayout, (d, v) => d.ModernLayout = v, UWSettingNormalize.None, UWSettingStore.Layout);

		/// <summary>UWModernLayout.PresetEnum as a number; -1 = not chosen yet, which reads as Custom
		/// (1) where an own layout exists or the minimap was hidden, else Modern (0).</summary>
		public static readonly UWIntSettingDef InterfacePreset = new UWIntSettingDef(nameof(UWUserData.InterfacePreset), UWSettingKind.Choice, d => d.InterfacePreset, (d, v) => d.InterfacePreset = v)
		{
			Normalize = UWSettingNormalize.UnsetIsComputed,
			Unset = NotChosen,
			WhenUnset = d => string.IsNullOrEmpty(d.ModernLayout) && !d.MinimapHidden ? 0 : 1
		};

		public static readonly UWBoolSettingDef MinimapHidden = fBool(nameof(UWUserData.MinimapHidden), d => d.MinimapHidden, (d, v) => d.MinimapHidden = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ActionBarHidden = fBool(nameof(UWUserData.ActionBarHidden), d => d.ActionBarHidden, (d, v) => d.ActionBarHidden = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernCompass = fBool(nameof(UWUserData.ModernCompass), d => d.ModernCompass, (d, v) => d.ModernCompass = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernCompassDisc = fBool(nameof(UWUserData.ModernCompassDisc), d => d.ModernCompassDisc, (d, v) => d.ModernCompassDisc = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernCompassOutline = fBool(nameof(UWUserData.ModernCompassOutline), d => d.ModernCompassOutline, (d, v) => d.ModernCompassOutline = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernScroll = fBool(nameof(UWUserData.ModernScroll), d => d.ModernScroll, (d, v) => d.ModernScroll = v, UWSettingStore.Layout);

		/// <summary>The parts' generated backs, an opaque string of UWModernBacks'.</summary>
		public static readonly UWTextSettingDef ModernBacks = fText(nameof(UWUserData.ModernBacks), d => d.ModernBacks, (d, v) => d.ModernBacks = v, UWSettingNormalize.None, UWSettingStore.Layout);

		public static readonly UWIntSettingDef ModernScrollLines = fInt(nameof(UWUserData.ModernScrollLines), UWSettingKind.Int, d => d.ModernScrollLines, (d, v) => d.ModernScrollLines = v, UWSettingStore.Layout);

		public static readonly UWIntSettingDef ModernMessageLines = fInt(nameof(UWUserData.ModernMessageLines), UWSettingKind.Int, d => d.ModernMessageLines, (d, v) => d.ModernMessageLines = v, UWSettingStore.Layout);

		public static readonly UWIntSettingDef InterfaceFont = fInt(nameof(UWUserData.InterfaceFont), UWSettingKind.Choice, d => d.InterfaceFont, (d, v) => d.InterfaceFont = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernGemApart = fBool(nameof(UWUserData.ModernGemApart), d => d.ModernGemApart, (d, v) => d.ModernGemApart = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernStoneShelf = fBool(nameof(UWUserData.ModernStoneShelf), d => d.ModernStoneShelf, (d, v) => d.ModernStoneShelf = v, UWSettingStore.Layout);

		public static readonly UWIntSettingDef ModernStoneShelfWidth = fInt(nameof(UWUserData.ModernStoneShelfWidth), UWSettingKind.Int, d => d.ModernStoneShelfWidth, (d, v) => d.ModernStoneShelfWidth = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernFlasksApart = fBool(nameof(UWUserData.ModernFlasksApart), d => d.ModernFlasksApart, (d, v) => d.ModernFlasksApart = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernRuneHollow = fBool(nameof(UWUserData.ModernRuneHollow), d => d.ModernRuneHollow, (d, v) => d.ModernRuneHollow = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernRuneHollowOutline = fBool(nameof(UWUserData.ModernRuneHollowOutline), d => d.ModernRuneHollowOutline, (d, v) => d.ModernRuneHollowOutline = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernRuneTablet = fBool(nameof(UWUserData.ModernRuneTablet), d => d.ModernRuneTablet, (d, v) => d.ModernRuneTablet = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernStatsPanel = fBool(nameof(UWUserData.ModernStatsPanel), d => d.ModernStatsPanel, (d, v) => d.ModernStatsPanel = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernCharacterPage = fBool(nameof(UWUserData.ModernCharacterPage), d => d.ModernCharacterPage, (d, v) => d.ModernCharacterPage = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernConversationOriginal = fBool(nameof(UWUserData.ModernConversationOriginal), d => d.ModernConversationOriginal, (d, v) => d.ModernConversationOriginal = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernConversationNoOutline = fBool(nameof(UWUserData.ModernConversationNoOutline), d => d.ModernConversationNoOutline, (d, v) => d.ModernConversationNoOutline = v, UWSettingStore.Layout);

		public static readonly UWBoolSettingDef ModernPointerHold = fBool(nameof(UWUserData.ModernPointerHold), d => d.ModernPointerHold, (d, v) => d.ModernPointerHold = v);

		/// <summary>0.05 to 2; 0 or below (no value written yet) reads as 1.</summary>
		public static readonly UWFloatSettingDef MouseLookSpeed = new UWFloatSettingDef(nameof(UWUserData.MouseLookSpeed), d => d.MouseLookSpeed, (d, v) => d.MouseLookSpeed = v)
		{
			Normalize = UWSettingNormalize.NotPositiveIsFallback,
			Min = MinMouseLookSpeed,
			Max = MaxMouseLookSpeed,
			Fallback = 1f,
			Step = MouseLookSpeedStep
		};

		public static readonly UWBoolSettingDef MouseInvertY = fBool(nameof(UWUserData.MouseInvertY), d => d.MouseInvertY, (d, v) => d.MouseInvertY = v);

		public static readonly UWBoolSettingDef WalkSlowToggle = fBool(nameof(UWUserData.WalkSlowToggle), d => d.WalkSlowToggle, (d, v) => d.WalkSlowToggle = v);

		/// <summary>UWGamepad.ButtonStyleEnum as a number: Xbox, PlayStation, Nintendo.</summary>
		public static readonly UWIntSettingDef GamepadButtonStyle = fInt(nameof(UWUserData.GamepadButtonStyle), UWSettingKind.Choice, d => d.GamepadButtonStyle, (d, v) => d.GamepadButtonStyle = v);

		/// <summary>0.25 to 2; 0 or below reads as 1.</summary>
		public static readonly UWFloatSettingDef StickLookSpeed = new UWFloatSettingDef(nameof(UWUserData.StickLookSpeed), d => d.StickLookSpeed, (d, v) => d.StickLookSpeed = v)
		{
			Normalize = UWSettingNormalize.NotPositiveIsFallback,
			Min = MinStickLookSpeed,
			Max = MaxStickLookSpeed,
			Fallback = 1f,
			Step = MouseLookSpeedStep
		};

		public static readonly UWBoolSettingDef GamepadSwapSticks = fBool(nameof(UWUserData.GamepadSwapSticks), d => d.GamepadSwapSticks, (d, v) => d.GamepadSwapSticks = v);

		public static readonly UWBoolSettingDef StickInvertY = fBool(nameof(UWUserData.StickInvertY), d => d.StickInvertY, (d, v) => d.StickInvertY = v);

		public static readonly UWFloatSettingDef LeftStickDeadzone = fClamped(nameof(UWUserData.LeftStickDeadzone), d => d.LeftStickDeadzone, (d, v) => d.LeftStickDeadzone = v, 0f, MaxStickDeadzone, StickDeadzoneStep);

		public static readonly UWBoolSettingDef GamepadButtonHints = fBool(nameof(UWUserData.GamepadButtonHints), d => d.GamepadButtonHints, (d, v) => d.GamepadButtonHints = v);

		public static readonly UWFloatSettingDef RightStickDeadzone = fClamped(nameof(UWUserData.RightStickDeadzone), d => d.RightStickDeadzone, (d, v) => d.RightStickDeadzone = v, 0f, MaxStickDeadzone, StickDeadzoneStep);

		public static readonly UWBoolSettingDef ModernPanelPinned = fBool(nameof(UWUserData.ModernPanelPinned), d => d.ModernPanelPinned, (d, v) => d.ModernPanelPinned = v);

		public static readonly UWBoolSettingDef ModernRunesPinned = fBool(nameof(UWUserData.ModernRunesPinned), d => d.ModernRunesPinned, (d, v) => d.ModernRunesPinned = v);

		/// <summary>UWControlScheme.SchemeEnum as a number; -1 = not chosen yet, the scene's
		/// default (UWControlScheme decides), read as it is.</summary>
		public static readonly UWIntSettingDef ControlScheme = new UWIntSettingDef(nameof(UWUserData.ControlScheme), UWSettingKind.Choice, d => d.ControlScheme, (d, v) => d.ControlScheme = v)
		{
			Unset = NotChosen
		};

		/// <summary>UWEasyMovement's repeat, 0.1 to 0.5 s; 0 or below (an older build's file) reads
		/// as the default.</summary>
		public static readonly UWFloatSettingDef EasyMovementInterval = new UWFloatSettingDef(nameof(UWUserData.EasyMovementInterval), d => d.EasyMovementInterval, (d, v) => d.EasyMovementInterval = v)
		{
			Normalize = UWSettingNormalize.NotPositiveIsFallback,
			Min = UWEasyMovement.MinRepeatSeconds,
			Max = UWEasyMovement.MaxRepeatSeconds,
			Fallback = UWEasyMovement.DefaultRepeatSeconds,
			Step = UWEasyMovement.RepeatStepSeconds
		};

		public static readonly UWBoolSettingDef ShowOpeningSequence = fBool(nameof(UWUserData.ShowOpeningSequence), d => d.ShowOpeningSequence, (d, v) => d.ShowOpeningSequence = v);

		public static readonly UWBoolSettingDef RunInBackground = fBool(nameof(UWUserData.RunInBackground), d => d.RunInBackground, (d, v) => d.RunInBackground = v);

		/// <summary>Retired (the classic screen is always 4:3 since 2026-10-10); kept for the file only.</summary>
		public static readonly UWBoolSettingDef DisplayAs4By3 = fBool(nameof(UWUserData.DisplayAs4By3), d => d.DisplayAs4By3, (d, v) => d.DisplayAs4By3 = v);

		public static readonly UWBoolSettingDef ClassicWidescreen = fBool(nameof(UWUserData.ClassicWidescreen), d => d.ClassicWidescreen, (d, v) => d.ClassicWidescreen = v);

		/// <summary>Degrees, 0 = each scheme's own; UWViewAngle keeps the limits.</summary>
		public static readonly UWFloatSettingDef ViewAngle = fFloat(nameof(UWUserData.ViewAngle), d => d.ViewAngle, (d, v) => d.ViewAngle = v);

		public static readonly UWBoolSettingDef OriginalWorldResolution = fBool(nameof(UWUserData.OriginalWorldResolution), d => d.OriginalWorldResolution, (d, v) => d.OriginalWorldResolution = v);

		/// <summary>1 to 4; UWWorldResolution.Factor keeps the limits.</summary>
		public static readonly UWIntSettingDef OriginalWorldResolutionFactor = fInt(nameof(UWUserData.OriginalWorldResolutionFactor), UWSettingKind.Int, d => d.OriginalWorldResolutionFactor, (d, v) => d.OriginalWorldResolutionFactor = v);

		/// <summary>The palette renderer's effects (UWPaletteEffects keeps their maxima).</summary>
		public static readonly UWFloatSettingDef PaletteAmbientOcclusion = fFloat(nameof(UWUserData.PaletteAmbientOcclusion), d => d.PaletteAmbientOcclusion, (d, v) => d.PaletteAmbientOcclusion = v);

		public static readonly UWBoolSettingDef PaletteLightSources = fBool(nameof(UWUserData.PaletteLightSources), d => d.PaletteLightSources, (d, v) => d.PaletteLightSources = v);

		public static readonly UWBoolSettingDef PaletteGlow = fBool(nameof(UWUserData.PaletteGlow), d => d.PaletteGlow, (d, v) => d.PaletteGlow = v);

		public static readonly UWFloatSettingDef PaletteGrime = fFloat(nameof(UWUserData.PaletteGrime), d => d.PaletteGrime, (d, v) => d.PaletteGrime = v);

		/// <summary>UWGrimeTint.ToneEnum as a number: none, dirt, moss, soot.</summary>
		public static readonly UWIntSettingDef PaletteGrimeTone = fInt(nameof(UWUserData.PaletteGrimeTone), UWSettingKind.Choice, d => d.PaletteGrimeTone, (d, v) => d.PaletteGrimeTone = v);

		public static readonly UWFloatSettingDef PaletteGroundShadows = fFloat(nameof(UWUserData.PaletteGroundShadows), d => d.PaletteGroundShadows, (d, v) => d.PaletteGroundShadows = v);

		public static readonly UWFloatSettingDef PaletteCavity = fFloat(nameof(UWUserData.PaletteCavity), d => d.PaletteCavity, (d, v) => d.PaletteCavity = v);

		public static readonly UWFloatSettingDef PaletteDepth = fFloat(nameof(UWUserData.PaletteDepth), d => d.PaletteDepth, (d, v) => d.PaletteDepth = v);

		/// <summary>UWDisplayMode.ModeEnum as a number; -1 = not chosen yet (a build then starts
		/// borderless at the desktop's size - UWDisplayMode decides), read as it is.</summary>
		public static readonly UWIntSettingDef DisplayMode = new UWIntSettingDef(nameof(UWUserData.DisplayMode), UWSettingKind.Choice, d => d.DisplayMode, (d, v) => d.DisplayMode = v)
		{
			Unset = NotChosen
		};

		public static readonly UWIntSettingDef ResolutionWidth = fInt(nameof(UWUserData.ResolutionWidth), UWSettingKind.Int, d => d.ResolutionWidth, (d, v) => d.ResolutionWidth = v);

		public static readonly UWIntSettingDef ResolutionHeight = fInt(nameof(UWUserData.ResolutionHeight), UWSettingKind.Int, d => d.ResolutionHeight, (d, v) => d.ResolutionHeight = v);

		/// <summary>UWHeadBobRules.ModeEnum as a number; an unknown one reads as Original.</summary>
		public static readonly UWIntSettingDef HeadBobMode = fMode(nameof(UWUserData.HeadBobMode), d => d.HeadBobMode, (d, v) => d.HeadBobMode = v);

		public static readonly UWFloatSettingDef HeadBobStrength = fStrength(nameof(UWUserData.HeadBobStrength), d => d.HeadBobStrength, (d, v) => d.HeadBobStrength = v);

		public static readonly UWIntSettingDef WeaponJitterMode = fMode(nameof(UWUserData.WeaponJitterMode), d => d.WeaponJitterMode, (d, v) => d.WeaponJitterMode = v);

		public static readonly UWFloatSettingDef WeaponJitterStrength = fStrength(nameof(UWUserData.WeaponJitterStrength), d => d.WeaponJitterStrength, (d, v) => d.WeaponJitterStrength = v);

		public static readonly UWIntSettingDef SwimViewMode = fMode(nameof(UWUserData.SwimViewMode), d => d.SwimViewMode, (d, v) => d.SwimViewMode = v);

		public static readonly UWFloatSettingDef SwimViewStrength = fStrength(nameof(UWUserData.SwimViewStrength), d => d.SwimViewStrength, (d, v) => d.SwimViewStrength = v);

		/// <summary>Smooth (1, anything but 0) or Original (0).</summary>
		public static readonly UWIntSettingDef MotionSmoothing = fSwitch(nameof(UWUserData.MotionSmoothing), d => d.MotionSmoothing, (d, v) => d.MotionSmoothing = v);

		/// <summary>Stepped (1, anything but 0) or even (0).</summary>
		public static readonly UWIntSettingDef MotionViewStepped = fSwitch(nameof(UWUserData.MotionViewStepped), d => d.MotionViewStepped, (d, v) => d.MotionViewStepped = v);

		public static readonly UWIntSettingDef OriginalFrameTicks = fTicks(nameof(UWUserData.OriginalFrameTicks), d => d.OriginalFrameTicks, (d, v) => d.OriginalFrameTicks = v, OriginalFrameChoices);

		public static readonly UWIntSettingDef SmoothRampTicks = fTicks(nameof(UWUserData.SmoothRampTicks), d => d.SmoothRampTicks, (d, v) => d.SmoothRampTicks = v, SmoothRampChoices);

		public static readonly UWBoolSettingDef MusicEnabled = fBool(nameof(UWUserData.MusicEnabled), d => d.MusicEnabled, (d, v) => d.MusicEnabled = v);

		public static readonly UWFloatSettingDef MusicVolume = fClamped(nameof(UWUserData.MusicVolume), d => d.MusicVolume, (d, v) => d.MusicVolume = v, 0f, 1f, VolumeStep);

		public static readonly UWBoolSettingDef SoundEnabled = fBool(nameof(UWUserData.SoundEnabled), d => d.SoundEnabled, (d, v) => d.SoundEnabled = v);

		public static readonly UWFloatSettingDef SoundVolume = fClamped(nameof(UWUserData.SoundVolume), d => d.SoundVolume, (d, v) => d.SoundVolume = v, 0f, 1f, VolumeStep);

		/// <summary>UWMusicDeviceId as a number, read and written within the three devices.</summary>
		public static readonly UWIntSettingDef MusicDevice = new UWIntSettingDef(nameof(UWUserData.MusicDevice), UWSettingKind.Choice, d => d.MusicDevice, (d, v) => d.MusicDevice = v)
		{
			Normalize = UWSettingNormalize.Clamp,
			Min = 0,
			Max = MaxMusicDevice
		};

		public static readonly UWTextSettingDef Mt32RomPath = fText(nameof(UWUserData.Mt32RomPath), d => d.Mt32RomPath, (d, v) => d.Mt32RomPath = v, UWSettingNormalize.NullIsEmpty);

		/// <summary>The debug panel's folded sections, UWDebugFolds' text (cleanup 8c, 2026-10-25).</summary>
		public static readonly UWTextSettingDef DebugFolded = fText(nameof(UWUserData.DebugFolded), d => d.DebugFolded, (d, v) => d.DebugFolded = v, UWSettingNormalize.NullIsEmpty);

		/// <summary>0.25 to 4 in steps of five per cent; 0 or below reads as 1 (cleanup step 7.7B,
		/// 2026-10-25: a file without the key reads 1 as well, the field's own value).</summary>
		public static readonly UWFloatSettingDef PointerSpeed = new UWFloatSettingDef(nameof(UWUserData.PointerSpeed), d => d.PointerSpeed, (d, v) => d.PointerSpeed = v)
		{
			Normalize = UWSettingNormalize.NotPositiveIsFallback,
			Min = MinPointerSpeed,
			Max = MaxPointerSpeed,
			Fallback = 1f,
			Step = MouseLookSpeedStep
		};

		/// <summary>Every setting, in the order of UWUserData's fields - the order of the file.</summary>
		public static readonly UWSettingDef[] All =
		{
			GogInstallPath, KeyBindings, ColourMode, ColourStrength, ColourBrightness, ColourContrast, MarkPoison, ManualAttributes,
			HelpOpen, HelpTab, HelpMapSpan, MinimapZoom, MinimapTurns, ModernUiPercent, ConversationTextPercent, ModernLayout,
			InterfacePreset, MinimapHidden, ActionBarHidden, ModernCompass, ModernCompassDisc, ModernCompassOutline, ModernScroll,
			ModernBacks, ModernScrollLines, ModernMessageLines, InterfaceFont, ModernGemApart, ModernStoneShelf, ModernStoneShelfWidth,
			ModernFlasksApart, ModernRuneHollow, ModernRuneHollowOutline, ModernRuneTablet, ModernStatsPanel, ModernCharacterPage,
			ModernConversationOriginal, ModernConversationNoOutline, ModernPointerHold, MouseLookSpeed, MouseInvertY, WalkSlowToggle,
			GamepadButtonStyle, StickLookSpeed, GamepadSwapSticks, StickInvertY, LeftStickDeadzone, GamepadButtonHints,
			RightStickDeadzone, ModernPanelPinned, ModernRunesPinned, ControlScheme, EasyMovementInterval, ShowOpeningSequence,
			RunInBackground, DisplayAs4By3, ClassicWidescreen, ViewAngle, OriginalWorldResolution, OriginalWorldResolutionFactor,
			PaletteAmbientOcclusion, PaletteLightSources, PaletteGlow, PaletteGrime, PaletteGrimeTone, PaletteGroundShadows,
			PaletteCavity, PaletteDepth, DisplayMode, ResolutionWidth, ResolutionHeight, HeadBobMode, HeadBobStrength,
			WeaponJitterMode, WeaponJitterStrength, SwimViewMode, SwimViewStrength, MotionSmoothing, MotionViewStepped,
			OriginalFrameTicks, SmoothRampTicks, MusicEnabled, MusicVolume, SoundEnabled, SoundVolume, MusicDevice, Mt32RomPath,
			DebugFolded, PointerSpeed
		};

		/// <summary>The settings of one store, in the file's order (the layout editor's snapshot
		/// takes Layout).</summary>
		public static UWSettingDef[] InStore(UWSettingStore peStore)
		{
			List<UWSettingDef> lOFound = new List<UWSettingDef>();

			foreach (UWSettingDef lODef in All)
			{
				if (lODef.Store == peStore)
					lOFound.Add(lODef);
			}

			return lOFound.ToArray();
		}

		/// <summary>The setting of that key, or null.</summary>
		public static UWSettingDef Find(string psKey)
		{
			foreach (UWSettingDef lODef in All)
			{
				if (lODef.Key == psKey)
					return lODef;
			}

			return null;
		}

		private static UWBoolSettingDef fBool(string psKey, Func<UWUserData, bool> pFGet, Action<UWUserData, bool> pFSet,
			UWSettingStore peStore = UWSettingStore.User)
		{
			return new UWBoolSettingDef(psKey, pFGet, pFSet) { Store = peStore };
		}

		private static UWIntSettingDef fInt(string psKey, UWSettingKind peKind, Func<UWUserData, int> pFGet, Action<UWUserData, int> pFSet,
			UWSettingStore peStore = UWSettingStore.User)
		{
			return new UWIntSettingDef(psKey, peKind, pFGet, pFSet) { Store = peStore };
		}

		private static UWFloatSettingDef fFloat(string psKey, Func<UWUserData, float> pFGet, Action<UWUserData, float> pFSet)
		{
			return new UWFloatSettingDef(psKey, pFGet, pFSet);
		}

		private static UWFloatSettingDef fClamped(string psKey, Func<UWUserData, float> pFGet, Action<UWUserData, float> pFSet,
			float pfMin, float pfMax, float pfStep)
		{
			return new UWFloatSettingDef(psKey, pFGet, pFSet)
			{
				Normalize = UWSettingNormalize.Clamp,
				Min = pfMin,
				Max = pfMax,
				Step = pfStep
			};
		}

		private static UWTextSettingDef fText(string psKey, Func<UWUserData, string> pFGet, Action<UWUserData, string> pFSet,
			UWSettingNormalize peNormalize, UWSettingStore peStore = UWSettingStore.User)
		{
			return new UWTextSettingDef(psKey, pFGet, pFSet) { Normalize = peNormalize, Store = peStore };
		}

		/// <summary>One of the three Off/Original/Smooth switches (UWHeadBobRules.ModeEnum).</summary>
		private static UWIntSettingDef fMode(string psKey, Func<UWUserData, int> pFGet, Action<UWUserData, int> pFSet)
		{
			return new UWIntSettingDef(psKey, UWSettingKind.Choice, pFGet, pFSet)
			{
				Normalize = UWSettingNormalize.InRangeOrFallback,
				Min = 0,
				Max = (int)UWHeadBobRules.ModeEnum.Smooth,
				Fallback = (int)UWHeadBobRules.ModeEnum.Original
			};
		}

		/// <summary>The Smooth strength beside a mode, 0.1 to 1 of the original's.</summary>
		private static UWFloatSettingDef fStrength(string psKey, Func<UWUserData, float> pFGet, Action<UWUserData, float> pFSet)
		{
			return fClamped(psKey, pFGet, pFSet, MinHeadBobStrength, 1f, HeadBobStrengthStep);
		}

		/// <summary>A switch the file keeps as 0 or 1.</summary>
		private static UWIntSettingDef fSwitch(string psKey, Func<UWUserData, int> pFGet, Action<UWUserData, int> pFSet)
		{
			return new UWIntSettingDef(psKey, UWSettingKind.Bool, pFGet, pFSet) { Normalize = UWSettingNormalize.NonZeroIsOn };
		}

		/// <summary>A length in PIT ticks, one of the choices.</summary>
		private static UWIntSettingDef fTicks(string psKey, Func<UWUserData, int> pFGet, Action<UWUserData, int> pFSet, int[] piChoices)
		{
			return new UWIntSettingDef(psKey, UWSettingKind.Choice, pFGet, pFSet)
			{
				Normalize = UWSettingNormalize.OneOfOrFallback,
				Choices = piChoices,
				Fallback = TicksFallback
			};
		}
	}
}
