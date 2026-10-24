using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// THE UI KIT: every canvas, picture, text and group of the uGUI interface is made here (cleanup
/// 5.2, 2026-10-20). Until then each modern part carried its own copies of the same creation
/// helpers - ten SetRect, twelve raw images, seven images, eleven texts and fourteen canvas setups
/// - and the classic screens their own copies of the 320x200 canvas and the frame images, so a
/// change of the text outline or of the canvas scaler meant visiting every file. The copies were nearly identical; where they
/// differed the difference is kept here as an explicit option, so nothing looks or behaves
/// otherwise:
///   - a TEXT: TextStyle says the font (the modern one, or the interface font for the original's
///     character and stats pages), the effect (the parts' outline, the modern HUD's shadow, none
///     on the stats page and the pad hints), wrapping (the modern HUD's texts), whether it follows
///     the font chosen with F (UWUiFonts; the layout editor and the pad hints keep theirs) and
///     whether it starts hidden (the two original pages);
///   - a PICTURE starts hidden unless told otherwise: the leathers and slot circles a part shows
///     at once, the question box's buttons, the pad hints' glyphs, the Classic Wide frame and the
///     classic inventory's number labels start shown;
///   - the anchor and pivot: the modern parts place from the bottom-left corner (screen pixels, y
///     up), the modern HUD from its own corners, the classic screens from the top-left corner of
///     the 320x200 frame.
///
/// A THIN CREATION HELPER on purpose (decision 26 of the cleanup plan): it makes Unity objects
/// and sets them up, nothing else. What to show and where belongs in the engine-free tables and
/// view models; the canvas orders come from UWUiSorting, the fonts from UWInterfaceFont, the
/// colours from UWModernPalette.
/// </summary>
public static class UWUiKit
{
    /// <summary>The modern parts' corner: anchored and pivoted bottom-left (screen pixels, y up).</summary>
    public static readonly Vector2 BottomLeft = Vector2.zero;

    /// <summary>The classic frame's corner: anchored and pivoted top-left (y down is negative).</summary>
    public static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    public static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

    /// <summary>A colour of the engine-free tables (UWClassicLayout) as Unity's.</summary>
    public static Color32 Colour(UWDataImport.UWData.UWColor32 pOColour)
    {
        return new Color32(pOColour.R, pOColour.G, pOColour.B, pOColour.A);
    }

    /// <summary>The parts' text outline, in screen pixels to the right and down.</summary>
    public const float OutlineDistance = 1.5f;

    /// <summary>The modern HUD's text shadow (at Unity's default distance of one pixel right and down).</summary>
    public static readonly Color HudShadow = new Color(0f, 0f, 0f, 0.8f);

    public enum TextEffectEnum
    {
        /// <summary>UWModernPalette.Outline at OutlineDistance: the parts' texts.</summary>
        Outline = 0,

        /// <summary>HudShadow: the modern HUD's texts.</summary>
        Shadow = 1,

        None = 2
    }

    public enum TextFontEnum
    {
        /// <summary>UWInterfaceFont.ModernOrInterface: the modern parts' font.</summary>
        Modern = 0,

        /// <summary>UWInterfaceFont.Font: the original's character and stats pages in the modern
        /// scheme.</summary>
        Interface = 1
    }

    /// <summary>How a text is made. The default (all fields zero) is the modern parts' text: the
    /// modern font, outlined, one line running over its rect, following the font chosen, shown.</summary>
    public struct TextStyle
    {
        public TextFontEnum Font;

        public TextEffectEnum Effect;

        /// <summary>Breaks lines at the rect's width (HorizontalWrapMode.Wrap) instead of running over it.</summary>
        public bool Wraps;

        /// <summary>Not registered with UWUiFonts, so the F toggle never changes it: the port's own
        /// tools keep the modern font (per user, 2026-10-09: the layout editor), and the pad hints
        /// never followed it.</summary>
        public bool KeepsItsFont;

        public bool StartsHidden;
    }

    /// <summary>The modern parts' text (character and rune panels, bags, action bar, minimap,
    /// conversation, question box).</summary>
    public static readonly TextStyle PartText = default;

    /// <summary>The modern HUD's texts: a shadow instead of the outline, wrapping.</summary>
    public static readonly TextStyle HudText = new TextStyle { Effect = TextEffectEnum.Shadow, Wraps = true };

    /// <summary>The layout editor's labels: the port's tool keeps the modern font.</summary>
    public static readonly TextStyle EditorText = new TextStyle { KeepsItsFont = true };

    /// <summary>The original character page's numbers (counts, weight): the interface font,
    /// hidden until placed.</summary>
    public static readonly TextStyle PageText = new TextStyle { Font = TextFontEnum.Interface, StartsHidden = true };

    /// <summary>The original stats page's lines: the interface font without an outline, hidden
    /// until placed.</summary>
    public static readonly TextStyle StatsText = new TextStyle { Font = TextFontEnum.Interface, Effect = TextEffectEnum.None, StartsHidden = true };

    /// <summary>The gamepad hint strip's words: no outline, not following the font chosen.</summary>
    public static readonly TextStyle PadHintText = new TextStyle { Effect = TextEffectEnum.None, KeepsItsFont = true };

    // ------------------------------------------------- Canvases

    /// <summary>A canvas of the modern scheme: screen space, one canvas unit per screen pixel
    /// (the parts scale themselves, UWModernUi.PixelScale).</summary>
    public static Canvas CreateOverlayCanvas(Transform pOParent, string psName, int piSortingOrder)
    {
        GameObject lOObject = new GameObject(psName, typeof(Canvas), typeof(CanvasScaler));
        lOObject.transform.SetParent(pOParent, false);

        Canvas lOCanvas = lOObject.GetComponent<Canvas>();
        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lOCanvas.sortingOrder = piSortingOrder;

        CanvasScaler lOScaler = lOObject.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        return lOCanvas;
    }

    /// <summary>A canvas of the classic 320x200 screen, fitted whole into the window by the
    /// height, by the width when narrow (UWUiFit, kept up every frame by UWFitCanvas, which also
    /// sets the scaler at once when it wakes).</summary>
    public static Canvas CreateClassicCanvas(Transform pOParent, string psName, int piSortingOrder)
    {
        GameObject lOObject = new GameObject(psName, typeof(Canvas), typeof(CanvasScaler));
        lOObject.transform.SetParent(pOParent, false);

        Canvas lOCanvas = lOObject.GetComponent<Canvas>();
        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lOCanvas.sortingOrder = piSortingOrder;

        UWUiFit.Apply(lOObject.GetComponent<CanvasScaler>());
        lOObject.AddComponent<UWFitCanvas>();

        return lOCanvas;
    }

    /// <summary>A canvas inside another one with an order of its own (overrideSorting): a layer
    /// that lies over or under other canvases, such as the modern HUD's view picture.</summary>
    public static Canvas CreateLayerCanvas(Transform pOParent, string psName, int piSortingOrder)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Canvas));
        lOObject.transform.SetParent(pOParent, false);

        Canvas lOCanvas = lOObject.GetComponent<Canvas>();
        lOCanvas.overrideSorting = true;
        lOCanvas.sortingOrder = piSortingOrder;

        return lOCanvas;
    }

    /// <summary>The 320x200 frame in a classic canvas' centre, at the classic screen's pixel
    /// proportion (UWPixelAspectFrame): the parts placed in it lie pixel-exactly on the original's
    /// pictures.</summary>
    public static RectTransform CreateClassicFrame(Transform pOCanvas, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform));
        lOObject.transform.SetParent(pOCanvas, false);

        RectTransform lOFrame = (RectTransform)lOObject.transform;
        lOObject.AddComponent<UWPixelAspectFrame>();
        lOFrame.anchorMin = Centre;
        lOFrame.anchorMax = Centre;
        lOFrame.pivot = Centre;
        lOFrame.sizeDelta = new Vector2(UWScreenUi.ScreenWidth, UWScreenUi.ScreenHeight);
        lOFrame.anchoredPosition = Vector2.zero;

        return lOFrame;
    }

    /// <summary>A plain colour over the whole of its parent (a black backdrop or cover).</summary>
    public static Image CreateBackdrop(Transform pOParent, string psName, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(pOParent, false);

        Stretch((RectTransform)lOObject.transform);

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.color = pOColour;
        lOImage.raycastTarget = false;

        return lOImage;
    }

    // ------------------------------------------------- Groups

    /// <summary>An empty group anchored and pivoted at pOCorner (its size stays Unity's default
    /// until set).</summary>
    public static RectTransform CreateGroup(Transform pOParent, string psName, Vector2 pOCorner)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOCorner;
        lORect.anchorMax = pOCorner;
        lORect.pivot = pOCorner;

        return lORect;
    }

    /// <summary>An empty group at the modern parts' bottom-left corner.</summary>
    public static RectTransform CreateGroup(Transform pOParent, string psName)
    {
        return CreateGroup(pOParent, psName, BottomLeft);
    }

    /// <summary>An empty group over the whole of its parent; its pivot stays Unity's default, the
    /// middle.</summary>
    public static RectTransform CreateStretchedGroup(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        Stretch(lORect);

        return lORect;
    }

    /// <summary>A group that clips what reaches beyond it (RectMask2D), anchored and pivoted at
    /// pOCorner.</summary>
    public static RectTransform CreateClipArea(Transform pOParent, string psName, Vector2 pOCorner)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RectMask2D));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOCorner;
        lORect.anchorMax = pOCorner;
        lORect.pivot = pOCorner;

        return lORect;
    }

    /// <summary>A clipping group of a modern part, at the bottom-left corner.</summary>
    public static RectTransform CreateClipArea(Transform pOParent, string psName)
    {
        return CreateClipArea(pOParent, psName, BottomLeft);
    }

    // ------------------------------------------------- Pictures

    /// <summary>A picture anchored at pOAnchor and pivoted at pOPivot, never catching clicks;
    /// hidden until its texture is set unless pbStartsShown.</summary>
    public static RawImage CreateRawImage(Transform pOParent, string psName, Vector2 pOAnchor, Vector2 pOPivot, bool pbStartsShown = false)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOAnchor;
        lORect.anchorMax = pOAnchor;
        lORect.pivot = pOPivot;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.enabled = pbStartsShown;

        return lOImage;
    }

    /// <summary>A picture of a modern part, at the bottom-left corner.</summary>
    public static RawImage CreateRawImage(Transform pOParent, string psName, bool pbStartsShown = false)
    {
        return CreateRawImage(pOParent, psName, BottomLeft, BottomLeft, pbStartsShown);
    }

    /// <summary>A picture of a classic screen with its top-left corner at (left, top) in the
    /// original's pixels, hidden until its texture is set.</summary>
    public static RawImage CreateRawImageAt(Transform pOParent, string psName, int piLeft, int piTop)
    {
        RawImage lOImage = CreateRawImage(pOParent, psName, TopLeft, TopLeft);
        lOImage.rectTransform.anchoredPosition = new Vector2(piLeft, -piTop);

        return lOImage;
    }

    /// <summary>A sprite or plain colour anchored at pOAnchor and pivoted at pOPivot, shown,
    /// never catching clicks.</summary>
    public static Image CreateImage(Transform pOParent, string psName, Vector2 pOAnchor, Vector2 pOPivot, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOAnchor;
        lORect.anchorMax = pOAnchor;
        lORect.pivot = pOPivot;

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.color = pOColour;
        lOImage.raycastTarget = false;

        return lOImage;
    }

    /// <summary>A sprite or plain colour of a modern part, at the bottom-left corner.</summary>
    public static Image CreateImage(Transform pOParent, string psName, Color pOColour)
    {
        return CreateImage(pOParent, psName, BottomLeft, BottomLeft, pOColour);
    }

    /// <summary>A sprite of a classic screen with its top-left corner at (left, top) in the
    /// original's pixels, hidden until its sprite is set.</summary>
    public static Image CreateImageAt(Transform pOParent, string psName, int piLeft, int piTop)
    {
        Image lOImage = CreateImage(pOParent, psName, TopLeft, TopLeft, Color.white);
        lOImage.rectTransform.anchoredPosition = new Vector2(piLeft, -piTop);
        lOImage.enabled = false;

        return lOImage;
    }

    /// <summary>A sprite or plain colour of the classic frame, top-left anchored, at the anchored
    /// position (x, y) - y negative downwards - and the size given.</summary>
    public static Image CreateFrameImage(Transform pOParent, string psName, float pfX, float pfY, float pfWidth, float pfHeight, Color pOColour)
    {
        Image lOImage = CreateImage(pOParent, psName, TopLeft, TopLeft, pOColour);
        SetRect(lOImage.rectTransform, pfX, pfY, pfWidth, pfHeight);

        return lOImage;
    }

    // ------------------------------------------------- Texts

    /// <summary>
    /// A text anchored at pOAnchor and pivoted at pOPivot, made as pOStyle says, never catching
    /// clicks. A text following the font chosen is registered with UWUiFonts right after its font
    /// is set (UWUiFonts keeps that font as its modern face); pOUi lets UWUiFonts find the game's
    /// own font files.
    /// </summary>
    public static Text CreateText(Transform pOParent, string psName, Vector2 pOAnchor, Vector2 pOPivot, TextAnchor peAlignment,
        Color pOColour, UWGameUI pOUi, TextStyle pOStyle)
    {
        GameObject lOObject = pOStyle.Effect == TextEffectEnum.Outline
            ? new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Outline))
            : pOStyle.Effect == TextEffectEnum.Shadow
                ? new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Shadow))
                : new GameObject(psName, typeof(RectTransform), typeof(Text));

        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = pOAnchor;
        lORect.anchorMax = pOAnchor;
        lORect.pivot = pOPivot;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = pOStyle.Font == TextFontEnum.Interface ? UWInterfaceFont.Font : UWInterfaceFont.ModernOrInterface;

        if (!pOStyle.KeepsItsFont)
            UWUiFonts.Register(lOText, pOUi);

        lOText.alignment = peAlignment;
        lOText.color = pOColour;
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = pOStyle.Wraps ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;

        if (pOStyle.StartsHidden)
            lOText.enabled = false;

        if (pOStyle.Effect == TextEffectEnum.Outline)
        {
            Outline lOOutline = lOObject.GetComponent<Outline>();
            lOOutline.effectColor = UWModernPalette.Outline;
            lOOutline.effectDistance = new Vector2(OutlineDistance, -OutlineDistance);
        }
        else if (pOStyle.Effect == TextEffectEnum.Shadow)
        {
            lOObject.GetComponent<Shadow>().effectColor = HudShadow;
        }

        return lOText;
    }

    /// <summary>A text of a modern part at the bottom-left corner, made as pOStyle says.</summary>
    public static Text CreateText(Transform pOParent, string psName, TextAnchor peAlignment, Color pOColour, UWGameUI pOUi, TextStyle pOStyle)
    {
        return CreateText(pOParent, psName, BottomLeft, BottomLeft, peAlignment, pOColour, pOUi, pOStyle);
    }

    /// <summary>A text of a modern part at the bottom-left corner (PartText).</summary>
    public static Text CreateText(Transform pOParent, string psName, TextAnchor peAlignment, Color pOColour, UWGameUI pOUi)
    {
        return CreateText(pOParent, psName, BottomLeft, BottomLeft, peAlignment, pOColour, pOUi, PartText);
    }

    // ------------------------------------------------- Placement

    /// <summary>Places a rect at its anchored position with its size.</summary>
    public static void SetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    /// <summary>Spreads a rect over the whole of its parent.</summary>
    public static void Stretch(RectTransform pORect)
    {
        pORect.anchorMin = Vector2.zero;
        pORect.anchorMax = Vector2.one;
        pORect.offsetMin = Vector2.zero;
        pORect.offsetMax = Vector2.zero;
    }
}
