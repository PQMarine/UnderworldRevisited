using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE DOCKED PANEL'S STATE MACHINE (cleanup 5.16, 2026-10-22), written twice until then - in the
/// character panel (UWModernPanel, docked at the right edge) and the rune panel
/// (UWModernRunePanel, its mirror at the left edge):
///
///   - closed, only its HANDLE shows, a tab of leather that is one piece with the panel; with the
///     pointer free and near the handle the panel PEEKS out a little (not while it is placed
///     freely); a click on the handle or the peeking edge opens it, a click on the handle closes it;
///   - it SLIDES out and in (SlideRate), its height glides to what its tab needs;
///   - the TAB HEADERS and the PIN on the row over its top edge, once it is out; the pin keeps it
///     out (each panel's own setting) and takes its handle away;
///   - moved in the layout editor it is a FREE window: no handle, no peeking, no sliding;
///   - Escape and the pad's B close it through UWModernUi's chain (IUWModernClosable) - each panel
///     says when it has something to close and what closing means.
///
/// What differs stays in the panels: the side (DockedLeft, the mirrored handle and pin), what a
/// click opens, the tabs' content, the help (UWModernPanel) and the tablet and the spells
/// (UWModernRunePanel). The measures are the ones both had.
/// </summary>
public abstract class UWModernDockedPanel : MonoBehaviour, IUWModernClosable
{
    /// <summary>The layout in original pixels, the same for both panels.</summary>
    protected const int TopMargin = 14;

    protected const int HandleWidth = 9;

    protected const int HandleHeight = 40;

    /// <summary>Below the minimap (UWModernMinimap: 4 margin + 96) and a gap of 4.</summary>
    protected const int HandleTop = 104;

    /// <summary>How far the panel peeks out while the pointer is near the handle.</summary>
    protected const int PeekWidth = 15;

    /// <summary>How near (original pixels left of the character panel's handle, right of the rune
    /// panel's - away from the panel) counts as near.</summary>
    protected const int PeekReach = 14;

    protected const int TabHeaderWidth = 30;

    protected const int TabHeaderHeight = 9;

    /// <summary>The pin at the right end of the tab row.</summary>
    protected const int PinWidth = 11;

    protected const int ContentPad = 6;

    /// <summary>How fast the panel slides: the share of the way left that is covered per second
    /// (exponentially).</summary>
    protected const float SlideRate = 14f;

    protected UWGameUI mOUi;

    protected UWControlScheme mOScheme;

    protected Canvas mOCanvas;

    protected RectTransform mORoot;

    protected RawImage mOBack;

    protected RawImage mOHandle;

    protected Text mOHandleText;

    protected RawImage[] mOTabBacks;

    protected Text[] mOTabTexts;

    protected RawImage mOPin;

    protected RawImage mOPinIcon;

    /// <summary>The leather, built for the panel's size and the backs' and colours' version
    /// (UWModernBacks.ArtVersion) - UWVersionedTexture.</summary>
    protected readonly UWVersionedTexture mOBackArt = new UWVersionedTexture();

    protected Texture2D mOTabTexture;

    protected Texture2D mOPinTexture;

    protected Texture2D mOPinIconTexture;

    protected bool mbOpen;

    protected bool mbPeek;

    /// <summary>How many screen pixels of the panel are out now - it glides towards its target.</summary>
    protected float mfShown;

    /// <summary>The panel's height in original rows, gliding as the width.</summary>
    protected float mfRows;

    protected float miScale = 1f;

    // Screen rectangles (pixels, bottom-left origin) of the last layout, for the clicks.
    protected Rect mOPanelRect;

    protected Rect mOHandleRect;

    protected Rect mOPinRect;

    protected readonly Rect[] mOTabRects = new Rect[2];

    public bool IsOpen => mbOpen;

    /// <summary>Whether Escape has something to close here (UWModernUi's closable chain).</summary>
    public abstract bool IsClosable { get; }

    public abstract void Close();

    /// <summary>Docked at the left edge, its handle at its right (the rune panel); else at the
    /// right edge, its handle at its left (the character panel).</summary>
    protected abstract bool DockedLeft { get; }

    /// <summary>The layout editor's element of the panel (free when placed there).</summary>
    protected abstract UWModernLayout.ElementEnum Element { get; }

    /// <summary>Pinned: always out (the panel's own setting).</summary>
    protected abstract bool IsPinned { get; }

    protected abstract void fSetPinnedSetting(bool pbPinned);

    /// <summary>Whether the panel is on the screen at all.</summary>
    protected abstract bool fIsShown();

    /// <summary>A click on the closed panel's handle or peeking edge: open it on the tab it opens on.</summary>
    protected abstract void fOpenByClick();

    /// <summary>Whether a screen point lies on the panel, its handle, tabs or pin - nothing goes
    /// into the world through it.</summary>
    public bool Contains(Vector2 pOPointer)
    {
        return fIsShown() && (mOPanelRect.Contains(pOPointer) || mOHandleRect.Contains(pOPointer)
            || mOTabRects[0].Contains(pOPointer) || mOTabRects[1].Contains(pOPointer) || mOPinRect.Contains(pOPointer));
    }

    // ------------------------------------------------- Input

    /// <summary>The pointer near the closed panel's handle, or on its peeking edge: it peeks out
    /// (not while it is placed freely).</summary>
    protected void fUpdatePeek(Vector2 pOPointer)
    {
        float lfLeft = DockedLeft ? mOHandleRect.xMin : mOHandleRect.xMin - (PeekReach * miScale);
        Rect lONear = new Rect(lfLeft, mOHandleRect.yMin - (4 * miScale),
            mOHandleRect.width + (PeekReach * miScale), mOHandleRect.height + (8 * miScale));

        mbPeek = !mbOpen && !UWModernLayout.IsPlaced(Element)
            && (lONear.Contains(pOPointer) || mOPanelRect.Contains(pOPointer));
    }

    /// <summary>A left press on the panel's own controls: the pin pins or unpins, the handle opens
    /// or closes, the closed panel's edge opens it. True when the press was theirs.</summary>
    protected bool fClickChrome(Vector2 pOPointer)
    {
        if (mOPinRect.Contains(pOPointer))
        {
            fSetPinnedSetting(!IsPinned);
            return true;
        }

        if (mOHandleRect.Contains(pOPointer))
        {
            if (mbOpen)
                Close();
            else
                fOpenByClick();

            return true;
        }

        if (!mbOpen && mOPanelRect.Contains(pOPointer))
        {
            fOpenByClick();
            return true;
        }

        return false;
    }

    /// <summary>The tab header under a screen point, -1 for none.</summary>
    protected int fTabAt(Vector2 pOPointer)
    {
        for (int liTab = 0; liTab < mOTabRects.Length; liTab++)
        {
            if (mOTabRects[liTab].Contains(pOPointer))
                return liTab;
        }

        return -1;
    }

    // ------------------------------------------------- Sliding

    /// <summary>The share of the way the panel glides this frame.</summary>
    protected static float fSlideStep()
    {
        return 1f - Mathf.Exp(-SlideRate * Time.unscaledDeltaTime);
    }

    /// <summary>A value gliding towards its target, there once it is nearer than pfSnap.</summary>
    protected static float fGlide(float pfValue, float pfTarget, float pfStep, float pfSnap)
    {
        return Mathf.Abs(pfValue - pfTarget) < pfSnap ? pfTarget : Mathf.Lerp(pfValue, pfTarget, pfStep);
    }

    /// <summary>How far the panel is to be out: its width when open, the peek near the handle
    /// (not when free), else nothing.</summary>
    protected float fTargetShown(float pfOpenWidth, bool pbFree)
    {
        return mbOpen ? pfOpenWidth : mbPeek && !pbFree ? PeekWidth * miScale : 0f;
    }

    // ------------------------------------------------- Drawing

    /// <summary>The leather at the panel's size in original pixels, built for it and the backs'
    /// and colours' version; shown while the panel is out.</summary>
    protected void fLayoutBack(int piWidth, int piHeight, float pfLeft, float pfBottom)
    {
        UWArtStamp lOBackStamp = UWVersionedTexture.Backs(piWidth, piHeight);

        if (!mOBackArt.IsCurrent(lOBackStamp))
        {
            mOBack.texture = mOBackArt.Set(lOBackStamp, UWModernHudArt.BuildPartLeather(Element, mOUi.mOUWData.Textures,
                piWidth, piHeight, mOUi.TextureFilterMode));
        }

        mOBack.enabled = mfShown > 0.5f;
        UWUiKit.SetRect(mOBack.rectTransform, pfLeft, pfBottom, piWidth * miScale, piHeight * miScale);
    }

    /// <summary>The handle out of the panel's open side and over its frame there - not when pinned
    /// or free - with its label turned along it.</summary>
    protected void fLayoutHandle(Rect pOHandle, bool pbPinned, bool pbFree)
    {
        float liScale = miScale;

        fRelabelChrome();

        mOHandleRect = pbPinned || pbFree ? Rect.zero : pOHandle;
        mOHandle.enabled = !pbPinned && !pbFree;
        mOHandleText.enabled = !pbPinned && !pbFree;
        UWUiKit.SetRect(mOHandle.rectTransform, DockedLeft ? pOHandle.x - (UWModernHudArt.LeatherLeft * liScale) : pOHandle.x, pOHandle.y,
            (HandleWidth + UWModernHudArt.LeatherLeft) * liScale, pOHandle.height);

        RectTransform lOLabel = mOHandleText.rectTransform;
        lOLabel.pivot = new Vector2(0.5f, 0.5f);
        lOLabel.anchoredPosition = pOHandle.center;
        lOLabel.sizeDelta = new Vector2(pOHandle.height, pOHandle.width);
        lOLabel.localRotation = Quaternion.Euler(0f, 0f, DockedLeft ? -90f : 90f);
        mOHandleText.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
    }

    /// <summary>
    /// The tab headers and the pin over the top edge, once the panel is out (piWidth its width in
    /// original pixels, piTab the tab shown). The pin at the right end of the row: upright and
    /// bright when pinned, tilted otherwise (to the panel's open side). Whether the row shows.
    /// </summary>
    protected bool fLayoutTabRow(float pfLeft, float pfTop, int piWidth, int piTab, bool pbPinned)
    {
        float liScale = miScale;
        bool lbOut = mbOpen && mfShown > PeekWidth * liScale;

        for (int liTab = 0; liTab < mOTabBacks.Length; liTab++)
        {
            Rect lOTab = new Rect(pfLeft + ((ContentPad + (liTab * (TabHeaderWidth + 2))) * liScale), pfTop - (2 * liScale),
                TabHeaderWidth * liScale, TabHeaderHeight * liScale);

            mOTabRects[liTab] = lbOut ? lOTab : Rect.zero;
            mOTabBacks[liTab].enabled = lbOut;
            mOTabTexts[liTab].enabled = lbOut;
            mOTabBacks[liTab].color = piTab == liTab ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
            mOTabTexts[liTab].color = piTab == liTab ? UWModernPalette.Text : UWModernPalette.Dim;
            mOTabTexts[liTab].fontSize = Mathf.Max(9, Mathf.RoundToInt(4.2f * liScale));
            UWUiKit.SetRect(mOTabBacks[liTab].rectTransform, lOTab.x, lOTab.y, lOTab.width, lOTab.height);
            UWUiKit.SetRect(mOTabTexts[liTab].rectTransform, lOTab.x, lOTab.y + liScale, lOTab.width, lOTab.height);
        }

        // The pin at the right end of the row: upright and bright when pinned, tilted otherwise.
        Rect lOPin = new Rect(pfLeft + ((piWidth - ContentPad - PinWidth) * liScale), pfTop - (2 * liScale),
            PinWidth * liScale, TabHeaderHeight * liScale);

        mOPinRect = lbOut ? lOPin : Rect.zero;
        mOPin.enabled = lbOut;
        mOPinIcon.enabled = lbOut;
        mOPin.color = pbPinned ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
        mOPinIcon.color = pbPinned ? Color.white : new Color(0.78f, 0.78f, 0.78f, 1f);
        UWUiKit.SetRect(mOPin.rectTransform, lOPin.x, lOPin.y, lOPin.width, lOPin.height);

        RectTransform lOPinIcon = mOPinIcon.rectTransform;
        lOPinIcon.pivot = new Vector2(0.5f, 0.5f);
        lOPinIcon.anchoredPosition = lOPin.center + new Vector2(0f, 0.5f * liScale);
        lOPinIcon.sizeDelta = new Vector2(7f * liScale, 7f * liScale);
        lOPinIcon.localRotation = Quaternion.Euler(0f, 0f, pbPinned ? 0f : DockedLeft ? -35f : 35f);

        return lbOut;
    }

    // ------------------------------------------------- Building

    /// <summary>The panel's own controls under pORoot, in this order: the tab headers, the pin and
    /// its icon, the leather, the handle (mirrored at the left edge: its open side joins the
    /// panel) and its label.</summary>
    protected void fBuildChrome(Transform pORoot, string[] psTabIds, string psHandleId)
    {
        mOTabBacks = new RawImage[2];
        mOTabTexts = new Text[2];
        msTabIds = psTabIds;
        msHandleId = psHandleId;

        for (int liTab = 0; liTab < 2; liTab++)
        {
            mOTabBacks[liTab] = UWUiKit.CreateRawImage(pORoot, "Tab " + psTabIds[liTab]);
            UWPixelArtUI.Apply(mOTabBacks[liTab]);
            mOTabTexts[liTab] = UWUiKit.CreateText(pORoot, psTabIds[liTab], TextAnchor.MiddleCenter, UWModernPalette.Text, mOUi);
        }

        mOPin = UWUiKit.CreateRawImage(pORoot, "Pin");
        UWPixelArtUI.Apply(mOPin);
        mOPinIcon = UWUiKit.CreateRawImage(pORoot, "Pin icon");

        mOBack = UWUiKit.CreateRawImage(pORoot, "Leather");
        UWPixelArtUI.Apply(mOBack);

        // The handle is the character panel's, mirrored: its open side joins the panel.
        mOHandle = UWUiKit.CreateRawImage(pORoot, "Handle");
        UWPixelArtUI.Apply(mOHandle);

        if (DockedLeft)
            mOHandle.uvRect = new Rect(1f, 0f, -1f, 1f);

        mOHandleText = UWUiKit.CreateText(pORoot, "Handle label", TextAnchor.MiddleCenter, UWModernPalette.Text, mOUi);
        miChromeLanguage = -1;
        fRelabelChrome();
    }

    /// <summary>The text ids of the tab headers and the handle (fBuildChrome).</summary>
    private string[] msTabIds;

    private string msHandleId;

    /// <summary>The language the chrome's texts were set in (UWText.LanguageVersion).</summary>
    private int miChromeLanguage = -1;

    /// <summary>The tab headers' and the handle's texts set anew when the language changed (translation
    /// step 3R: a language switched while the game runs shows at once); called on every layout.</summary>
    protected void fRelabelChrome()
    {
        if (miChromeLanguage == UWText.LanguageVersion || mOTabTexts == null || msTabIds == null)
            return;

        miChromeLanguage = UWText.LanguageVersion;

        for (int liTab = 0; liTab < mOTabTexts.Length && liTab < msTabIds.Length; liTab++)
            mOTabTexts[liTab].text = UWText.T(msTabIds[liTab]);

        if (mOHandleText != null)
            mOHandleText.text = UWText.T(msHandleId);
    }

    /// <summary>The tab headers' and the pin's leather in the panel's back and the pin's icon -
    /// the handle's picture is each panel's own.</summary>
    protected void fBuildChromeArt(UWTextures pOTextures, FilterMode peFilter)
    {
        mOTabTexture = UWModernHudArt.BuildPartLeather(Element, pOTextures, TabHeaderWidth, TabHeaderHeight, peFilter);
        mOPinTexture = UWModernHudArt.BuildPartLeather(Element, pOTextures, PinWidth, TabHeaderHeight, peFilter);
        mOPinIconTexture = UWModernPanel.BuildPinIcon(48);

        mOPin.texture = mOPinTexture;
        mOPinIcon.texture = mOPinIconTexture;

        foreach (RawImage lOTab in mOTabBacks)
            lOTab.texture = mOTabTexture;
    }

    /// <summary>The leather, the tab headers', the pin's and its icon's pictures go.</summary>
    protected void fDestroyChromeArt()
    {
        mOBackArt.Release();

        foreach (Texture2D lOTexture in new[] { mOTabTexture, mOPinTexture, mOPinIconTexture })
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        mOTabTexture = null;
        mOPinTexture = null;
        mOPinIconTexture = null;
    }
}
