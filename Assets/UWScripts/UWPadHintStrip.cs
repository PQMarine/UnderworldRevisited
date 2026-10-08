using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// THE GAMEPAD'S HINT STRIP (per user, 2026-10-08, after the hints at the crosshair and the bar):
/// one row above the action bar with what the pad's buttons do right now - glyph and a word each,
/// as under the letter grid. By the situation: the pointer locked shows how to free it (and the
/// cast with the rune panel open); free, what the buttons do where it lies - on the world, on the
/// bags or the character panel, on the rune panel -, and the context menu's and the split box's
/// own. One place instead of a line at every window, which move as bags open. Not under the game
/// menu, in a conversation, over the map or the letter grid; only while the pad is in use and the
/// hints are on (UWUserSettings.ShowsPadHints).
/// </summary>
public class UWPadHintStrip : MonoBehaviour
{
    private const int MaxItems = 6;

    private Canvas mOCanvas;

    private Image mOBack;

    private readonly RawImage[] mOGlyphs = new RawImage[MaxItems];

    private readonly Text[] mOTexts = new Text[MaxItems];

    private Font mOFont;

    private readonly List<Texture2D> mOItemGlyphs = new List<Texture2D>();

    private readonly List<string> mOItemTexts = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        GameObject lOObject = new GameObject("UWPadHintStrip");
        DontDestroyOnLoad(lOObject);
        lOObject.AddComponent<UWPadHintStrip>();
    }

    private void LateUpdate()
    {
        fGather();

        if (mOItemTexts.Count == 0)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;
        fLayout();
    }

    /// <summary>The items for now; none when the strip stays away.</summary>
    private void fGather()
    {
        mOItemGlyphs.Clear();
        mOItemTexts.Clear();

        UWControlScheme lOScheme = UWScene.ControlScheme;
        UWModernHud lOHud = UWModernHud.Instance;
        UWGameUI lOUi = UWScene.GameUi;

        if (!UWUserSettings.ShowsPadHints || lOScheme == null || lOScheme.Current != UWControlScheme.SchemeEnum.Modern
            || lOHud == null || !lOHud.IsShowing || lOHud.IsOpen || UWConversationScreen.IsAnyOpen || UWLetterGrid.IsShown
            || (lOUi != null && lOUi.IsMapVisible) || UWHelpWindow.BlocksGameKeys)
            return;

        if (UWModernBags.IsSplitting)
        {
            fAdd(UWGlyphs.Tiles(UWGlyphs.DpadHorizontalTile), "count");
            fAdd(UWGlyphs.ForPath("<Gamepad>/buttonSouth"), "take");
            fAdd(UWGlyphs.ForPath("<Gamepad>/buttonEast"), "cancel");
            return;
        }

        if (UWModernBags.IsMenuOpen)
        {
            fAdd(UWGlyphs.Tiles(UWGlyphs.DpadVerticalTile), "step");
            fAdd(UWGlyphs.ForEntry("PadClick", 0), "choose");
            fAdd(UWGlyphs.ForEntry("PadBack", 0), "cancel");
            return;
        }

        UWModernRunePanel lORunes = UWModernRunePanel.Instance;

        if (!lOScheme.IsPointerFree)
        {
            fAdd(UWGlyphs.ForEntry("PadPointer", 0), "pointer");

            if (lORunes != null && lORunes.IsOpen)
                fAdd(UWGlyphs.ForEntry("PadCast", 0), "cast");

            return;
        }

        Vector2 lOAt = UWGamepadPointer.OwnsPosition ? UWGamepadPointer.Position
            : (UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero);
        UWModernBags lOBags = UWModernBags.Instance;
        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lORunes != null && lORunes.IsOpen && lORunes.Contains(lOAt))
        {
            fAdd(UWGlyphs.ForEntry("PadClick", 0), "choose");
            fAdd(UWGlyphs.ForEntry("PadContext", 0), "look");
            fAdd(UWGlyphs.ForEntry("PadBack", 0), "close");
        }
        else if ((lOBags != null && lOBags.IsOverWindow(lOAt)) || (lOPanel != null && lOPanel.IsOpen && lOPanel.Contains(lOAt)))
        {
            fAdd(UWGlyphs.ForEntry("PadClick", 0), "take / put");
            fAdd(UWGlyphs.ForEntry("PadContext", 0), "menu");
            fAdd(UWGlyphs.ForEntry("PadBack", 0), "close");
        }
        else
        {
            fAdd(UWGlyphs.ForEntry("PadClick", 0), "menu");
        }

        fAdd(UWGlyphs.ForEntry("PadPointer", 0), "look");
    }

    private void fAdd(Texture2D pOGlyph, string psText)
    {
        if (mOItemTexts.Count >= MaxItems)
            return;

        mOItemGlyphs.Add(pOGlyph);
        mOItemTexts.Add(psText);
    }

    private void fBuild()
    {
        mOFont = Resources.Load<Font>("Fonts/LexendExa");

        GameObject lORoot = new GameObject("Pad hints", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 45;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        mOBack = new GameObject("Back", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        mOBack.transform.SetParent(lORoot.transform, false);
        mOBack.color = new Color(0.08f, 0.06f, 0.04f, 0.72f);
        mOBack.raycastTarget = false;
        fAnchor(mOBack.rectTransform);

        for (int liItem = 0; liItem < MaxItems; liItem++)
        {
            RawImage lOGlyph = new GameObject("Glyph", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            lOGlyph.transform.SetParent(lORoot.transform, false);
            lOGlyph.raycastTarget = false;
            fAnchor(lOGlyph.rectTransform);
            mOGlyphs[liItem] = lOGlyph;

            Text lOText = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            lOText.transform.SetParent(lORoot.transform, false);
            lOText.font = mOFont != null ? mOFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lOText.alignment = TextAnchor.MiddleLeft;
            lOText.color = new Color(0.94f, 0.87f, 0.71f);
            lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
            lOText.verticalOverflow = VerticalWrapMode.Overflow;
            lOText.raycastTarget = false;
            fAnchor(lOText.rectTransform);
            mOTexts[liItem] = lOText;
        }
    }

    private static void fAnchor(RectTransform pORect)
    {
        pORect.anchorMin = Vector2.zero;
        pORect.anchorMax = Vector2.zero;
        pORect.pivot = Vector2.zero;
    }

    /// <summary>Centred above the action bar - above its slot hint, which stands over the bar - and
    /// moved with it; below it where the bar stands at the top edge.</summary>
    private void fLayout()
    {
        int liFontSize = UWModernHud.FontSize;
        float lfGlyph = Mathf.Round(liFontSize * 1.5f);
        float lfGap = Mathf.Round(lfGlyph * 0.2f);
        float lfSpace = lfGlyph;
        float lfPad = Mathf.Round(lfGlyph * 0.3f);
        float lfTotal = 0f;

        for (int liItem = 0; liItem < MaxItems; liItem++)
        {
            bool lbUsed = liItem < mOItemTexts.Count;

            mOGlyphs[liItem].enabled = lbUsed && mOItemGlyphs[liItem] != null;
            mOTexts[liItem].enabled = lbUsed;

            if (!lbUsed)
                continue;

            mOTexts[liItem].fontSize = liFontSize;
            mOTexts[liItem].text = mOItemTexts[liItem];
            mOGlyphs[liItem].texture = mOItemGlyphs[liItem];

            lfTotal += (liItem > 0 ? lfSpace : 0f) + (mOItemGlyphs[liItem] != null ? lfGlyph + lfGap : 0f)
                + mOTexts[liItem].preferredWidth;
        }

        UWModernActionBar lOBar = UWModernActionBar.Instance;
        float lfBottom = lOBar != null && lOBar.ScreenRect.height > 0f
            ? lOBar.ScreenRect.yMax + (lOBar.ScreenRect.height * 0.55f)
            : liFontSize * 2f;
        // The bar moved to the top edge (the layout editor): below it then, the strip follows the bar
        // and is not placed on its own (per user, 2026-10-08).
        if (lOBar != null && lOBar.ScreenRect.height > 0f && lfBottom + lfGlyph + lfPad > Screen.height)
            lfBottom = lOBar.ScreenRect.yMin - lfGlyph - (2f * lfPad);

        float lfX = Mathf.Round((Screen.width - lfTotal) * 0.5f);

        mOBack.rectTransform.anchoredPosition = new Vector2(lfX - lfPad, lfBottom - lfPad);
        mOBack.rectTransform.sizeDelta = new Vector2(lfTotal + (2f * lfPad), lfGlyph + (2f * lfPad));

        for (int liItem = 0; liItem < mOItemTexts.Count; liItem++)
        {
            if (liItem > 0)
                lfX += lfSpace;

            if (mOItemGlyphs[liItem] != null)
            {
                mOGlyphs[liItem].rectTransform.anchoredPosition = new Vector2(lfX, lfBottom);
                mOGlyphs[liItem].rectTransform.sizeDelta = new Vector2(lfGlyph, lfGlyph);
                lfX += lfGlyph + lfGap;
            }

            float lfWidth = mOTexts[liItem].preferredWidth;

            mOTexts[liItem].rectTransform.anchoredPosition = new Vector2(lfX, lfBottom);
            mOTexts[liItem].rectTransform.sizeDelta = new Vector2(lfWidth + 2f, lfGlyph);
            lfX += lfWidth;
        }
    }
}
