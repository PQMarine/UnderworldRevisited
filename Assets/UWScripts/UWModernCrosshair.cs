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

            if (UWModernPointer.IsFree && UnityEngine.InputSystem.Mouse.current != null)
                lOAt = UnityEngine.InputSystem.Mouse.current.position.ReadValue() - new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            lORect.anchoredPosition = lOAt;
            lORect.sizeDelta = new Vector2(lfRing, lfRing);
            mOHoldBar.fillAmount = lfHold;
        }

        mfTargetTimer -= Time.unscaledDeltaTime;

        if (mfTargetTimer > 0f)
            return;

        mfTargetTimer = 0.1f;
        mOTarget.text = lbCross ? mOInteraction.GetCrosshairTargetName() ?? string.Empty : string.Empty;

        fUpdatePadPrompt(lbCross && !UWModernPointer.IsFree);
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
        mOPadUseText.text = lOUse != null ? lsVerb : UWGamepad.ButtonName(UWGlyphs.PadPath("ModernUse", 1)) + " " + lsVerb;
        mOPadLookText.text = lOLook != null ? "Look" : UWGamepad.ButtonName(UWGlyphs.PadPath("ModernLook", 1)) + " Look";

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
