using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S ACTIVE SPELLS (SPELLS.GR through the palette shader, as on the classic shelf),
/// a buff row at the top right (fPlaceSpells). Out of UWModernHud since cleanup 5.16
/// (2026-10-22); the clicks on them are the HUD's routing (UWModernHudInput).
/// </summary>
public class UWModernActiveSpells : UWModernPart
{
    private RawImage[] mOSpellIcons;

    private Texture2D[] mOSpellTextures;

    /// <summary>The active spells' places this frame (screen pixels), and the hover text.</summary>
    private readonly Rect[] mOSpellRects = new Rect[UWPlayerVitals.MaxActiveSpells];

    private Text mOSpellTip;

    /// <summary>The spell under the free pointer, -1 for none (UWModernHudInput sets it).</summary>
    public int HoveredSpell { get; set; } = -1;

    private int miSpellVersion = -1;

    public UWModernActiveSpells(UWModernHud pOHud) : base(pOHud)
    {
    }

    public override void Build(Transform pORoot)
    {
        mOSpellIcons = new RawImage[UWPlayerVitals.MaxActiveSpells];
        mOSpellTextures = new Texture2D[UWPlayerVitals.MaxActiveSpells];

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
            mOSpellIcons[liSlot] = UWUiKit.CreateRawImage(pORoot, "Spell " + liSlot, Vector2.zero, Vector2.zero);

        mOSpellTip = UWUiKit.CreateText(pORoot, "Spell hover", Vector2.zero, new Vector2(1f, 1f), TextAnchor.UpperRight, UWModernPalette.HudText, mOUi, UWUiKit.HudText);
    }

    /// <summary>A new scale builds the icons again.</summary>
    public override void OnPixelScale(float pfPixelScale)
    {
        base.OnPixelScale(pfPixelScale);
        miSpellVersion = -1;
    }

    protected override void fUpdate()
    {
        fUpdateSpells();
        fPlaceSpells();
    }

    /// <summary>The active spells top left, rebuilt when they change (ActiveSpellVersion), as the
    /// classic shelf builds them (UWHudRunes).</summary>
    private void fUpdateSpells()
    {
        if (miSpellVersion == mOCharacter.Vitals.ActiveSpellVersion)
            return;

        miSpellVersion = mOCharacter.Vitals.ActiveSpellVersion;

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            RawImage lOImage = mOSpellIcons[liSlot];

            mOSpellTextures[liSlot] = null;

            UWTexture lOSource = null;

            if (liSlot < mOCharacter.Vitals.ActiveSpells.Count)
            {
                UWActiveSpellEffect lOSpell = mOCharacter.Vitals.ActiveSpells[liSlot];
                int liIcon = UWRunicMagic.GetIconIndex(lOSpell.MajorClass, lOSpell.MinorClass);

                if (liIcon < UWRunicMagic.NoIcon)
                    lOSource = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.SPELLS, liIcon);
            }

            if (lOSource == null)
            {
                lOImage.enabled = false;
                continue;
            }

            UWIconPalette.ApplySmooth(lOImage);
            mOSpellTextures[liSlot] = UWIconCache.Texture(lOSource, mOUi.TextureFilterMode);
            lOImage.texture = mOSpellTextures[liSlot];
            lOImage.enabled = true;
        }
    }

    /// <summary>
    /// THE ACTIVE SPELLS (per user, 2026-10-04: the rune panel now covers the top left): at the top
    /// right, left of the minimap - or of the character panel while it is out - as a buff row;
    /// the pointer over one shows its name and state.
    /// </summary>
    private void fPlaceSpells()
    {
        float liScale = fScaleOf(UWModernLayout.ElementEnum.Spells);
        float lfMargin = 4f * miPixelScale;
        float lfRight = Screen.width - lfMargin;
        UWModernMinimap lOMap = UWModernMinimap.Instance;
        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOMap != null && lOMap.ScreenRect.width > 0f)
            lfRight = Mathf.Min(lfRight, lOMap.ScreenRect.xMin - lfMargin);

        if (lOPanel != null && lOPanel.IsOpen)
            lfRight = Mathf.Min(lfRight, lOPanel.LeftEdge - lfMargin);

        float lfTop = Screen.height - lfMargin;
        float lfX = lfRight;

        // Right-aligned, the first spell leftmost as on the classic page.
        float lfWidth = 0f;

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            if (mOSpellIcons[liSlot].enabled && mOSpellTextures[liSlot] != null)
                lfWidth += ((mOSpellTextures[liSlot].width * UWModernHudArt.PixelAspectX) + 3) * liScale;
        }

        // Its own place (UWModernLayout): right-aligned at its anchor, as wide as the spells - three
        // places' room while the layout is edited without any.
        float lfRowHeight = 17f * liScale;
        float lfRowWidth = lfWidth > 0f ? lfWidth - (3 * liScale) : (UWModernLayout.IsEditing ? 3f * 20f * liScale : 0f);

        if (lfRowWidth > 0f)
        {
            Rect lODefault = new Rect(lfRight - lfRowWidth, lfTop - lfRowHeight, lfRowWidth, lfRowHeight);
            Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Spells, lODefault);

            lfRight = lOPlaced.xMax;
            lfTop = lOPlaced.yMax;
            UWModernLayout.Report(UWModernLayout.ElementEnum.Spells, lOPlaced);
        }

        lfX = Mathf.Round(lfRight - lfWidth + (3 * liScale));

        for (int liSlot = 0; liSlot < mOSpellIcons.Length; liSlot++)
        {
            RawImage lOImage = mOSpellIcons[liSlot];
            Texture2D lOTexture = mOSpellTextures[liSlot];

            if (!lOImage.enabled || lOTexture == null)
            {
                mOSpellRects[liSlot] = Rect.zero;
                continue;
            }

            Rect lORect = new Rect(lfX, lfTop - (lOTexture.height * liScale), lOTexture.width * liScale * UWModernHudArt.PixelAspectX, lOTexture.height * liScale);

            mOSpellRects[liSlot] = lORect;
            ((RectTransform)lOImage.transform).anchoredPosition = lORect.position;
            ((RectTransform)lOImage.transform).sizeDelta = lORect.size;
            lfX += ((lOTexture.width * UWModernHudArt.PixelAspectX) + 3) * liScale;
        }

        // The hover text below the row, right-aligned to it.
        string lsTip = HoveredSpell >= 0 && mOUi.Runes != null ? mOUi.Runes.ActiveSpellText(HoveredSpell) : string.Empty;

        mOSpellTip.enabled = !string.IsNullOrEmpty(lsTip);
        mOSpellTip.text = lsTip;
        mOSpellTip.fontSize = fFontOf(UWModernLayout.ElementEnum.Spells);

        RectTransform lOTip = (RectTransform)mOSpellTip.transform;
        lOTip.anchoredPosition = new Vector2(lfRight, lfTop - (19f * liScale));
        lOTip.sizeDelta = new Vector2(600f, FontSize * 2f);
    }

    /// <summary>The active spell under a screen point, -1 for none.</summary>
    public int SpellAt(Vector2 pOPointer)
    {
        for (int liSlot = 0; liSlot < mOSpellRects.Length; liSlot++)
        {
            if (mOSpellRects[liSlot].width > 0f && mOSpellRects[liSlot].Contains(pOPointer))
                return liSlot;
        }

        return -1;
    }
}
