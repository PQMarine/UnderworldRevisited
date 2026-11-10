using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S CROSSHAIR (out of UWModernHud, cleanup 5.16, 2026-10-22): the game's own
/// pointer in the view's middle while the pointer is locked, the original's target pointer while
/// a spell or a drawn bow waits, the ring of E's hold, the name of what the crosshair is on
/// (Interaction.GetCrosshairTargetName) and the gamepad's button hint under it.
/// </summary>
public class UWModernCrosshair : UWModernPart
{
    private Text mOTarget;

    private Image mOCrosshair;

    /// <summary>In the crosshair's place while a spell waits for its target and the pointer is
    /// locked: the original's target pointer (per user, 2026-10-04).</summary>
    private RawImage mOTargetCursor;

    /// <summary>The bar under the crosshair while E is held for the direct use.</summary>
    private Image mOHoldBar;

    private float mfTargetTimer;

    private RawImage mOPadUseGlyph;

    private RawImage mOPadLookGlyph;

    private Text mOPadUseText;

    private Text mOPadLookText;

    /// <summary>Beside the name of a locked door: the key that fits it, or the crossed-out key icon (GitHub issue 30).</summary>
    private RawImage mOLockIcon;

    /// <summary>The crossed-out key icon, made once per colour version (the colour help).</summary>
    private Texture2D mONoKeyIcon;

    private int miNoKeyIconColours = -1;

    /// <summary>The keys' own pictures by id.</summary>
    private readonly System.Collections.Generic.Dictionary<int, Texture2D> mOKeyPictures = new System.Collections.Generic.Dictionary<int, Texture2D>();

    private int miKeyPicturesColours = -1;

    public UWModernCrosshair(UWModernHud pOHud) : base(pOHud)
    {
    }

    public override void Build(Transform pORoot)
    {
        mOCrosshair = UWUiKit.CreateImage(pORoot, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Color(1f, 1f, 1f, 0.85f));
        mOTargetCursor = UWUiKit.CreateRawImage(pORoot, "Spell target", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UWPixelArtUI.Apply(mOTargetCursor);
        mOTargetCursor.enabled = false;
        mOTarget = UWUiKit.CreateText(pORoot, "Target", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), TextAnchor.UpperCenter, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
        mOLockIcon = UWUiKit.CreateRawImage(pORoot, "Door lock", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        UWPixelArtUI.Apply(mOLockIcon);
        mOLockIcon.enabled = false;
        mOHoldBar = UWUiKit.CreateImage(pORoot, "Hold ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            UWModernPalette.WithAlpha(UWModernPalette.Accent, 0.9f));
        mOHoldBar.enabled = false;

        // A ring that fills clockwise from the top (fUpdate).
        Texture2D lORing = UWModernActionBar.BuildRing(128, 60f, 7f);

        mOHoldBar.sprite = Sprite.Create(lORing, new Rect(0f, 0f, lORing.width, lORing.height), new Vector2(0.5f, 0.5f));
        mOHoldBar.color = Color.white;
        mOHoldBar.type = Image.Type.Filled;
        mOHoldBar.fillMethod = Image.FillMethod.Radial360;
        mOHoldBar.fillOrigin = (int)Image.Origin360.Top;
        mOHoldBar.fillClockwise = true;
    }

    public override void OnPixelScale(float pfPixelScale)
    {
        base.OnPixelScale(pfPixelScale);

        float liScale = pfPixelScale;
        int liFontSize = FontSize;

        mOTarget.fontSize = liFontSize;

        float lfCross = Mathf.Max(2f, liScale);

        ((RectTransform)mOCrosshair.transform).sizeDelta = new Vector2(lfCross * 2f, lfCross * 2f);
        ((RectTransform)mOTarget.transform).anchoredPosition = new Vector2(0f, -4f * liScale);
        ((RectTransform)mOTarget.transform).sizeDelta = new Vector2(600f, liFontSize * 2f);
    }

    /// <summary>The crosshair and the name of its target, asked ten times a second.</summary>
    protected override void fUpdate()
    {
        bool lbCross = !mOHud.IsOpen && (mOScheme == null || !mOScheme.IsUIModalOpen);

        // The view's middle against the screen's: in Classic Wide the hole's (UWModernClassicFrame).
        Vector2 lOOffset = UWModernClassicFrame.ViewCentre - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        ((RectTransform)mOCrosshair.transform).anchoredPosition = lOOffset;
        ((RectTransform)mOTarget.transform).anchoredPosition = lOOffset + new Vector2(0f, -4f * miPixelScale);

        // The game's own pointer in the middle, the target pointer while a spell or a drawn
        // ranged weapon waits; with the pointer free none (UWPointerCursor.CrosshairPicture).
        bool lbLocked = lbCross && !UWModernPointer.IsFree;
        Texture2D lOCursor = mOUi.PointerCursor.CrosshairPicture(mOInteraction, lbLocked);

        mOTargetCursor.enabled = lOCursor != null;
        // The dot only where the pointer pictures are missing.
        mOCrosshair.enabled = lbLocked && lOCursor == null;

        if (lOCursor != null)
        {
            mOTargetCursor.texture = lOCursor;
            ((RectTransform)mOTargetCursor.transform).anchoredPosition = lOOffset;
            ((RectTransform)mOTargetCursor.transform).sizeDelta = new Vector2(lOCursor.width * miPixelScale * UWModernHudArt.PixelAspectX, lOCursor.height * miPixelScale);
        }

        // The hold of E for the direct use (Interaction.ModernUseHoldProgress): a ring filling
        // clockwise around the pointer (per user, 2026-10-04 - a bar under the dot before); with
        // the pointer free around the pointer, where E acts then.
        float lfHold = mOInteraction.ModernUseHoldProgress;

        mOHoldBar.enabled = lbCross && lfHold >= 0f;

        if (mOHoldBar.enabled)
        {
            float lfRing = 24f * miPixelScale;
            RectTransform lORect = (RectTransform)mOHoldBar.transform;
            Vector2 lOAt = lOOffset;

            if (UWModernPointer.IsFree && UWPointer.HasMouse)
                lOAt = UWPointer.Position - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            lORect.anchoredPosition = lOAt;
            lORect.sizeDelta = new Vector2(lfRing, lfRing);
            mOHoldBar.fillAmount = lfHold;
        }

        mfTargetTimer -= Time.unscaledDeltaTime;

        if (mfTargetTimer > 0f)
            return;

        mfTargetTimer = 0.1f;
        mOTarget.text = lbCross ? mOInteraction.GetCrosshairTargetName() ?? string.Empty : string.Empty;
        fUpdateLockIcon(lbCross && mOTarget.text.Length > 0, lOOffset);

        fUpdatePadPrompt(lbCross && !UWModernPointer.IsFree);
    }

    /// <summary>
    /// THE KEY BESIDE A LOCKED DOOR'S NAME (GitHub issue 30, 2026-11-10, per user: the texts were too large): the key in
    /// the bags that fits it, in its own picture - every key of the original is "a_key", the shape tells which -, or
    /// where none fits the key icon crossed out (UWKeyIconBuilder, the game's palette colours). Right of the name, on
    /// its first line, as high as a line of the text.
    /// </summary>
    private void fUpdateLockIcon(bool pbShown, Vector2 pOOffset)
    {
        int liKey = -1;
        bool lbLocked = pbShown && mOInteraction.GetCrosshairLock(out liKey);
        Texture2D lOPicture = lbLocked ? (liKey >= 0 ? fKeyPicture(liKey) : fNoKeyIcon()) : null;

        mOLockIcon.enabled = lOPicture != null;

        if (lOPicture == null)
            return;

        // Whole pixels of the picture, as many as make it about a line of the text high (pixel art stays crisp).
        // One scale for both pictures (UWKeyIconBuilder.ScaleHeight), so the no-key icon looks no larger than a key.
        int liScale = Mathf.Max(1, Mathf.RoundToInt(mOTarget.fontSize * 1.2f / UWDataImport.Presentation.UWKeyIconBuilder.ScaleHeight));
        float lfHeight = lOPicture.height * liScale;
        float lfWidth = lOPicture.width * liScale * UWModernHudArt.PixelAspectX;
        float lfLineMiddle = (-4f * miPixelScale) - (mOTarget.fontSize * 0.6f);

        mOLockIcon.texture = lOPicture;
        ((RectTransform)mOLockIcon.transform).sizeDelta = new Vector2(lfWidth, lfHeight);
        ((RectTransform)mOLockIcon.transform).anchoredPosition = pOOffset
            + new Vector2((mOTarget.preferredWidth * 0.5f) + (2f * miPixelScale) + (lfWidth * 0.5f), lfLineMiddle);
    }

    /// <summary>A key's own picture (OBJECTS.GR), made once per colour version.</summary>
    private Texture2D fKeyPicture(int piKeyId)
    {
        if (miKeyPicturesColours != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOKeyPictures.Values)
                Object.Destroy(lOOld);

            mOKeyPictures.Clear();
            miKeyPicturesColours = UWColourVision.Version;
        }

        if (mOKeyPictures.TryGetValue(piKeyId, out Texture2D lOPicture))
            return lOPicture;

        UWTexture lOKey = fObjectTexture(piKeyId);

        lOPicture = lOKey != null
            ? UWTextureFactory.FromPicture(UWDataImport.Presentation.UWKeyIconBuilder.Crop(UWPicture.From(lOKey)), FilterMode.Point, "Key " + piKeyId)
            : null;
        mOKeyPictures[piKeyId] = lOPicture;

        return lOPicture;
    }

    /// <summary>The generic key with its X in the game's palette colours, made once per colour version.</summary>
    private Texture2D fNoKeyIcon()
    {
        if (mONoKeyIcon != null && miNoKeyIconColours == UWColourVision.Version)
            return mONoKeyIcon;

        if (mONoKeyIcon != null)
            Object.Destroy(mONoKeyIcon);

        mONoKeyIcon = null;
        miNoKeyIconColours = UWColourVision.Version;

        UWPalette lOPalette = mOUi.mOUWData != null && mOUi.mOUWData.Palettes != null ? mOUi.mOUWData.Palettes.GetPalette(0) : null;

        if (lOPalette == null)
            return null;

        UWPicture lOIcon = UWDataImport.Presentation.UWKeyIconBuilder.BuildNoKey(
            UWDataImport.Presentation.UWKeyIconBuilder.Nearest(lOPalette, UWDataImport.Presentation.UWKeyIconBuilder.InkWanted),
            UWDataImport.Presentation.UWKeyIconBuilder.Nearest(lOPalette, UWDataImport.Presentation.UWKeyIconBuilder.StrokeWanted),
            UWDataImport.Presentation.UWKeyIconBuilder.Nearest(lOPalette, UWDataImport.Presentation.UWKeyIconBuilder.DarkWanted));

        mONoKeyIcon = UWTextureFactory.FromPicture(lOIcon, FilterMode.Point, "No key");

        return mONoKeyIcon;
    }

    private UWTexture fObjectTexture(int piObjectId)
    {
        try
        {
            return mOUi.mOUWData != null ? mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piObjectId) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// THE GAMEPAD'S BUTTON HINT under the crosshair's target name (per user, 2026-10-08: "my wife
    /// would not manage without glyphs"): A with what it does there (Interaction.GetCrosshairPadVerb),
    /// X to look - while the pad is in use and the hints are on (UWUserSettings.ShowsPadHints), with
    /// the pointer locked. The glyphs of the buttons as bound now, their names where a glyph is
    /// missing.
    /// </summary>
    private void fUpdatePadPrompt(bool pbLocked)
    {
        string lsVerb = pbLocked && UWUserSettings.ShowsPadHints && mOInteraction != null ? mOInteraction.GetCrosshairPadVerb() : null;

        if (mOPadUseGlyph == null)
        {
            if (lsVerb == null)
                return;

            Transform lOParent = mOTarget.transform.parent;

            mOPadUseGlyph = UWUiKit.CreateRawImage(lOParent, "Pad use glyph", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
            mOPadLookGlyph = UWUiKit.CreateRawImage(lOParent, "Pad look glyph", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f));
            mOPadUseText = UWUiKit.CreateText(lOParent, "Pad use", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), TextAnchor.MiddleLeft, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
            mOPadLookText = UWUiKit.CreateText(lOParent, "Pad look", new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), TextAnchor.MiddleLeft, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
        }

        bool lbShow = lsVerb != null;

        mOPadUseGlyph.enabled = lbShow;
        mOPadLookGlyph.enabled = lbShow;
        mOPadUseText.enabled = lbShow;
        mOPadLookText.enabled = lbShow;

        if (!lbShow)
            return;

        Texture2D lOUse = UWGlyphs.ForEntry("ModernUse", 1);
        Texture2D lOLook = UWGlyphs.ForEntry("ModernLook", 1);
        int liFontSize = mOTarget.fontSize;
        float lfGlyph = Mathf.Round(liFontSize * 1.5f);
        float lfGap = Mathf.Round(lfGlyph * 0.2f);

        mOPadUseText.fontSize = liFontSize;
        mOPadLookText.fontSize = liFontSize;
        mOPadUseText.text = lOUse != null ? lsVerb : UWBindingInfo.ButtonName(UWBindingInfo.PadPath("ModernUse", 1)) + " " + lsVerb;
        mOPadLookText.text = lOLook != null ? UWText.T("hud.crosshair.look")
            : UWBindingInfo.ButtonName(UWBindingInfo.PadPath("ModernLook", 1)) + " " + UWText.T("hud.crosshair.look");

        float lfUseWidth = mOPadUseText.preferredWidth;
        float lfLookWidth = mOPadLookText.preferredWidth;
        float lfUseGlyph = lOUse != null ? lfGlyph + lfGap : 0f;
        float lfLookGlyph = lOLook != null ? lfGlyph + lfGap : 0f;
        float lfTotal = lfUseGlyph + lfUseWidth + lfGlyph + lfLookGlyph + lfLookWidth;
        float lfX = -lfTotal * 0.5f;
        float lfY = -(4f * miPixelScale) - (liFontSize * 1.5f);

        mOPadUseGlyph.texture = lOUse;
        mOPadUseGlyph.enabled = lOUse != null;
        fPlace(mOPadUseGlyph.transform, lfX, lfY, lfGlyph, lfGlyph);
        fPlace(mOPadUseText.transform, lfX + lfUseGlyph, lfY, lfUseWidth + 2f, lfGlyph);

        lfX += lfUseGlyph + lfUseWidth + lfGlyph;

        mOPadLookGlyph.texture = lOLook;
        mOPadLookGlyph.enabled = lOLook != null;
        fPlace(mOPadLookGlyph.transform, lfX, lfY, lfGlyph, lfGlyph);
        fPlace(mOPadLookText.transform, lfX + lfLookGlyph, lfY, lfLookWidth + 2f, lfGlyph);
    }

    private static void fPlace(Transform pOTransform, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        RectTransform lORect = (RectTransform)pOTransform;

        lORect.anchoredPosition = new Vector2(pfX, pfY);
        lORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }
}
