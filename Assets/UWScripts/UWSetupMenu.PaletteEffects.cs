using UnityEngine;

/// <summary>
/// THE PALETTE EFFECTS DIALOG (per user, 2026-10-07): the palette renderer's own effects, each with
/// its strength and a line on what it does (UWPaletteEffects). Opened from the Graphics menu;
/// they take effect in the Low detail level, the palette path. Kept compact - one slider and one
/// short line each, Done beside the title - so all of them fit on the 720 lines the menus are
/// laid out for. MOVABLE by its title row (per user, the same day: to find a spot where the effect
/// shows while the sliders move); the place is kept while the program runs.
/// </summary>
public partial class UWSetupMenu
{
    private bool mbPaletteEffectsDialogOpen;

    /// <summary>The dialog's top left corner in the menu's units; NaN until first drawn (then
    /// centred under the bar).</summary>
    private Vector2 mOPaletteDialogAt = new Vector2(float.NaN, float.NaN);

    private bool mbPaletteDialogDragging;

    private Vector2 mOPaletteDragOffset;

    private void fOpenPaletteEffectsDialog()
    {
        meOpenMenu = MenuEnum.None;
        mbControlsDialogOpen = false;
        mbColoursDialogOpen = false;
        mbSoundDialogOpen = false;
        mbDisplayDialogOpen = false;
        mbMotionDialogOpen = false;
        mbPaletteEffectsDialogOpen = true;
    }

    private void fDrawPaletteEffectsDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(720f, pfWidth - 40f);
        const float DialogHeight = 660f;

        if (float.IsNaN(mOPaletteDialogAt.x))
            mOPaletteDialogAt = new Vector2((pfWidth - lfDialogWidth) / 2f, BarHeight + 8f);

        fDragPaletteDialog(new Rect(mOPaletteDialogAt.x, mOPaletteDialogAt.y, lfDialogWidth - 140f, 44f), pfWidth, lfDialogWidth);

        Rect lODialog = new Rect(mOPaletteDialogAt.x, mOPaletteDialogAt.y, lfDialogWidth, DialogHeight);


        fFill(lODialog, PanelColour);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 14f;

        GUI.Label(new Rect(lfLeft, lfTop, 220f, 26f), "Palette effects", mOLabel);
        GUI.Label(new Rect(lfLeft + 230f, lfTop + 4f, lfInner - 370f, 22f), "drag here to move", mODimLabel);

        if (GUI.Button(new Rect(lODialog.xMax - 130f, lfTop - 2f, 110f, 30f), "Done", mOButton))
            mbPaletteEffectsDialogOpen = false;

        lfTop += 32f;

        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            UWGraphicsDetail.CurrentLevel == UWGraphicsDetail.LevelEnum.Low
                ? "Our own effects for the original's palette (detail level Low). They only make a surface lighter or darker by the original's shade steps, so every pixel keeps a palette colour - but turned up they quickly take away the look of the original. 0 is off."
                : "Our own effects for the original's palette - they show in the detail level Low only, which is not chosen at the moment. 0 is off.") + 10f;

        // THE PRESETS (UWPaletteEffects.ApplyPreset): one click sets every effect; Own lights up
        // when the sliders hold something else.
        string[] lsPresets = { "Off", "Subtle", "Full", "Own" };
        UWPaletteEffects.PresetEnum leCurrent = UWPaletteEffects.CurrentPreset;
        float lfPresetWidth = (lfInner - 230f) / lsPresets.Length;

        GUI.Label(new Rect(lfLeft, lfTop, 220f, 26f), "Preset", mOListLabel);

        for (int liPreset = 0; liPreset < lsPresets.Length; liPreset++)
        {
            bool lbCurrent = (int)leCurrent == liPreset;

            if (GUI.Button(new Rect(lfLeft + 230f + (liPreset * lfPresetWidth), lfTop, lfPresetWidth, 26f), lsPresets[liPreset],
                lbCurrent ? mOCurrentItem : mOListItem) && !lbCurrent)
                UWPaletteEffects.ApplyPreset((UWPaletteEffects.PresetEnum)liPreset);
        }

        lfTop += 38f;

        fPaletteEffectRow(ref lfTop, lfLeft, lfInner, "Ambient occlusion",
            UWUserSettings.PaletteAmbientOcclusion, UWPaletteEffects.MaxAmbientOcclusion,
            "Corners, joints and the floor along the walls get darker.", pfValue => UWUserSettings.PaletteAmbientOcclusion = pfValue);

        fPaletteEffectRow(ref lfTop, lfLeft, lfInner, "Grime along the walls",
            UWUserSettings.PaletteGrime, UWPaletteEffects.MaxGrime,
            null, pfValue => UWUserSettings.PaletteGrime = pfValue);

        // The grime's colour (UWGrimeTint): four buttons, the current one in the accent colour.
        string[] lsTones = { "No colour", "Dirt", "Moss", "Soot" };
        float lfToneWidth = (lfInner - 230f) / lsTones.Length;

        for (int liTone = 0; liTone < lsTones.Length; liTone++)
        {
            bool lbCurrent = UWUserSettings.PaletteGrimeTone == liTone;

            if (GUI.Button(new Rect(lfLeft + 230f + (liTone * lfToneWidth), lfTop, lfToneWidth, 26f), lsTones[liTone],
                lbCurrent ? mOCurrentItem : mOListItem) && !lbCurrent)
            {
                UWUserSettings.PaletteGrimeTone = liTone;
                UWUserSettings.Save();
            }
        }

        lfTop += 28f;
        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "Dirt where a wall or a step meets the floor hides the hard edge, in the palette's colour nearest the tone. Grime works in the Remastered mode too.") + 10f;

        fPaletteEffectRow(ref lfTop, lfLeft, lfInner, "Depth",
            UWUserSettings.PaletteDepth, UWPaletteEffects.MaxDepth,
            "Stones stand out from their joints and shift against them as you move, with their shadow.",
            pfValue => UWUserSettings.PaletteDepth = pfValue);

        fPaletteEffectRow(ref lfTop, lfLeft, lfInner, "Hollows",
            UWUserSettings.PaletteCavity, UWPaletteEffects.MaxRelief,
            "Joints and recesses get darker.", pfValue => UWUserSettings.PaletteCavity = pfValue);

        fPaletteEffectRow(ref lfTop, lfLeft, lfInner, "Ground shadows",
            UWUserSettings.PaletteGroundShadows, UWPaletteEffects.MaxGroundShadows,
            "A dark patch under creatures and things, so they stand on the ground.", pfValue => UWUserSettings.PaletteGroundShadows = pfValue);

        bool lbGlow = GUI.Toggle(new Rect(lfLeft, lfTop, lfInner, 26f), UWUserSettings.PaletteGlow,
            "  Glow - lava and flames keep their full colour, in the dark too", mOToggle);

        if (lbGlow != UWUserSettings.PaletteGlow)
        {
            UWUserSettings.PaletteGlow = lbGlow;
            UWUserSettings.Save();
        }

        lfTop += 32f;

        bool lbLights = GUI.Toggle(new Rect(lfLeft, lfTop, lfInner, 26f), UWUserSettings.PaletteLightSources,
            "  Light sources", mOToggle);

        if (lbLights != UWUserSettings.PaletteLightSources)
        {
            UWUserSettings.PaletteLightSources = lbLights;
            UWUserSettings.Save();
        }

        lfTop += 28f;
        fDrawHint(lfLeft, lfTop, lfInner,
            "Torches, candles, lanterns, fires, lava, glowing stones, mushrooms and orbs light their surroundings by the original's own light table, walls cast shadows, fire flickers. The original lights only from you, so lit places beyond your light show here, up to its farthest drawing distance.");
    }

    /// <summary>Moves the dialog while the left button holds its title row (pODragArea), kept
    /// on the screen with its title row reachable.</summary>
    private void fDragPaletteDialog(Rect pODragArea, float pfWidth, float pfDialogWidth)
    {
        Event lOEvent = Event.current;

        if (lOEvent == null)
            return;

        if (lOEvent.type == EventType.MouseDown && lOEvent.button == 0 && pODragArea.Contains(lOEvent.mousePosition))
        {
            mbPaletteDialogDragging = true;
            mOPaletteDragOffset = lOEvent.mousePosition - mOPaletteDialogAt;
            lOEvent.Use();
        }
        else if (mbPaletteDialogDragging && lOEvent.type == EventType.MouseDrag)
        {
            Vector2 lOWanted = lOEvent.mousePosition - mOPaletteDragOffset;

            mOPaletteDialogAt = new Vector2(
                Mathf.Clamp(lOWanted.x, 100f - pfDialogWidth, pfWidth - 100f),
                Mathf.Clamp(lOWanted.y, BarHeight, ReferenceHeight - 44f));
            lOEvent.Use();
        }
        else if (mbPaletteDialogDragging && lOEvent.rawType == EventType.MouseUp)
        {
            mbPaletteDialogDragging = false;
        }
    }

    /// <summary>One effect: its name, a strength in shade steps from 0 to the maximum in half steps
    /// with the value beside it ("Off" at 0), and a short line under it (none if null). A change is
    /// handed to pOStore and saved; pfTop moves on.</summary>
    private void fPaletteEffectRow(ref float pfTop, float pfLeft, float pfWidth, string psLabel, float pfValue, float pfMax,
        string psHint, System.Action<float> pOStore)
    {
        GUI.Label(new Rect(pfLeft, pfTop, 220f, 26f), psLabel, mOListLabel);

        float lfWanted = GUI.HorizontalSlider(new Rect(pfLeft + 230f, pfTop + 8f, pfWidth - 330f, 20f),
            pfValue, 0f, pfMax);

        lfWanted = Mathf.Clamp(Mathf.Round(lfWanted * 2f) / 2f, 0f, pfMax);

        GUI.Label(new Rect(pfLeft + pfWidth - 90f, pfTop, 90f, 26f),
            lfWanted <= 0f ? "Off" : lfWanted.ToString("0.#") + " steps", mOListLabel);

        pfTop += 28f;

        if (psHint != null)
            pfTop += fDrawHint(pfLeft, pfTop, pfWidth, psHint) + 10f;

        // Stored and the file written once a step changes, not every frame.
        if (!Mathf.Approximately(lfWanted, pfValue))
        {
            pOStore(lfWanted);
            UWUserSettings.Save();
        }
    }
}
