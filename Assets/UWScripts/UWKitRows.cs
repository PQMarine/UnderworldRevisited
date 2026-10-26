using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE WINDOWS' ROWS ON THE KIT (cleanup 8b.1, 2026-10-24; phase 8b, decision 26): the same row
/// vocabulary as UWGuiRows (IUWRows) drawn with the uGUI kit (UWUiKit) on a canvas of its own
/// (UWUiSorting.KitWindows), so the setup bar and its windows leave IMGUI - the precondition for a
/// pointer of our own (UWPointer part B, Docs/CLEANUP-PLAN.md 8.5): no window takes its clicks from
/// the system pointer's IMGUI events any more.
///
/// DRAWN LIKE IMGUI, KEPT LIKE uGUI: the host calls the rows once a frame between Begin and End, in
/// the order IMGUI drew them, fed by plain values like UWGuiRows; each call takes the next of a pool
/// of uGUI pieces (a fill, a picture, a text) and sets it only where something changed, and End hides
/// what this frame left unused. So the page tables, the page drawer (UWSetupPageDrawer) and the
/// dialogs' frames stay as they are, and the hierarchy's order is the drawing order.
///
/// THE LOOK is UWGuiTheme's: the palette, the interface font (UWInterfaceFont) and the setup family's
/// sizes; the rectangles are the IMGUI form's numbers, in the same layout units (720 lines high, the
/// canvas scaled by UWGuiTheme.Scale as the IMGUI windows by BeginScaled). Pixel differences between
/// IMGUI's and uGUI's text and the switch's and slider's own drawing are accepted (phase 8b's rule).
/// What changes on purpose: label columns, choices, buttons and bar items are SIZED BY THEIR TEXT
/// (UWRowSizing; SizesByText), and a line too long for its width wraps below its first line instead of
/// running on under its neighbour.
///
/// CLICKS: release-based like IMGUI's buttons - the first piece drawn under the pointer when the left
/// button goes down takes the press, and it counts when the button comes up over that piece again -
/// through the modern root's gate (UWModernUi.WindowTakesPointer), from the Input System, without an
/// EventSystem, as the modern parts take theirs. The gamepad's pointer presses its own mouse's button
/// (UWPointer.PadButton since cleanup step 7.7B; the real one through UWOsMouse until then), and the
/// left button here is UWPointer's, so its clicks arrive the same way. KEYS: UWKitKeyNav (F10 and the
/// arrows as before; since cleanup 8d.4 Left and Right move a slider with the focus, and a scrolled
/// area scrolls to show the entry the keys put the focus on);
/// TYPING: TextField on UWTextEntry and UWTypedKeys, locking the game's keys meanwhile (IsTyping,
/// UWTextEntryState).
/// </summary>
public sealed class UWKitRows : IUWRows
{
    // ------------------------------------------------- The IMGUI form's numbers (UWGuiRows)

    private const float ArrowPitch = 30f;

    private const float ArrowSize = 26f;

    private const float SliderDrop = 8f;

    private const float SliderHeight = 20f;

    private const float DoneWidth = 110f;

    private const float DoneHeight = 30f;

    /// <summary>The Done button's right edge stands this far from the dialog's (130 - 110).</summary>
    private const float DoneRightGap = 20f;

    private const float DoneFromBottom = 46f;

    // ------------------------------------------------- The kit's own pieces

    /// <summary>The slider's groove and thumb, centred where IMGUI's skin drew them (6 below the
    /// slider's top, 14 below the row's).</summary>
    private const float GrooveCentre = 6f;

    private const float GrooveHeight = 4f;

    private const float ThumbWidth = 10f;

    private const float ThumbHeight = 16f;

    /// <summary>The switch's box, and where its text starts (the skin toggle's left padding).</summary>
    private const float ToggleBox = 14f;

    private const float ToggleTextLeft = 15f;

    /// <summary>A scrolled area's bar, inside the view on the right as IMGUI's.</summary>
    private const float ScrollBarWidth = 15f;

    /// <summary>A wheel notch moves a scrolled area as far as IMGUI's (UWImguiInput.WheelStep).</summary>
    private const float WheelStep = 20f;

    /// <summary>A greyed row's colours lose half their strength, as IMGUI draws a disabled control.</summary>
    private const float DisabledAlpha = 0.5f;

    /// <summary>How a text is drawn.</summary>
    private struct Look
    {
        public int Size;

        public Color Colour;

        public TextAnchor Anchor;

        /// <summary>Left and right inside the piece.</summary>
        public float Padding;

        public bool Wraps;
    }

    private static Look fLook(int piSize, Color pOColour, TextAnchor peAnchor, float pfPadding = 0f, bool pbWraps = false)
    {
        return new Look { Size = piSize, Colour = pOColour, Anchor = peAnchor, Padding = pfPadding, Wraps = pbWraps };
    }

    // The setup family's looks (UWGuiTheme.SetupStyles), or the layout editor's (UWGuiTheme.
    // LayoutEditorStyles: 15 points, the heading 16 in the accent colour, the hints 13 from the top;
    // cleanup 8b.4, 2026-10-24).

    /// <summary>The window the rows are drawn for (the constructor's).</summary>
    private readonly UWRowLookEnum meLook;

    private bool IsEditor => meLook == UWRowLookEnum.LayoutEditor;

    /// <summary>The font every piece of this drawer is set in and measured with: the interface font,
    /// in the game menu's look the modern font (UWGuiTheme.GameMenuStyles), in the letter grid's the
    /// IMGUI skin's own (UWGuiTheme.LetterGridStyles copied GUI.skin.label: LegacyRuntime) - cleanup 8b.4.</summary>
    private Font PieceFont
    {
        get
        {
            switch (meLook)
            {
                case UWRowLookEnum.GameMenu:
                    return UWInterfaceFont.ModernOrInterface;

                case UWRowLookEnum.LetterGrid:
                    if (msOSkinFont == null)
                    {
                        msOSkinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                        // The language's installed fonts behind it (translation wave 6).
                        UWFallbackFont.Follow(msOSkinFont);
                    }

                    return msOSkinFont;

                default:
                    return UWInterfaceFont.Font;
            }
        }
    }

    private static Font msOSkinFont;

    /// <summary>The rows' type size: labels, values, buttons, choices, switches, list rows, text fields.</summary>
    private int TextSize => IsEditor ? UWGuiTheme.LayoutEditorTextSize : UWGuiTheme.SetupTextSize;

    private Look HeadingLook => IsEditor
        ? fLook(UWGuiTheme.LayoutEditorTitleSize, mOPalette.Accent, TextAnchor.MiddleLeft)
        : fLook(UWGuiTheme.SetupHeadingSize, mOPalette.Text, TextAnchor.MiddleLeft);

    private Look RowLook => fLook(TextSize, mOPalette.Text, TextAnchor.MiddleLeft);

    private Look ValueLook => fLook(TextSize, mOPalette.Text, TextAnchor.MiddleCenter);

    private Look fDimLook(Color pOColour) => IsEditor
        ? fLook(UWGuiTheme.LayoutEditorHintSize, pOColour, TextAnchor.UpperLeft, 0f, true)
        : fLook(UWGuiTheme.SetupHintSize, pOColour, TextAnchor.MiddleLeft, 0f, true);

    /// <summary>The setup family's dim look whatever the window (the accent and error lines, which
    /// UWGuiRows drew in the setup styles in either look).</summary>
    private static Look fSetupDimLook(Color pOColour) => fLook(UWGuiTheme.SetupHintSize, pOColour, TextAnchor.MiddleLeft, 0f, true);

    // ------------------------------------------------- The theme (cleanup phase 8d, 2026-10-25)

    /// <summary>The palette this pass draws in: the theme in force, taken at each Begin (so a switch in
    /// the Colours window shows at once), or FixedPalette.</summary>
    private UWGuiTheme.Palette mOPalette = UWGuiTheme.Plain;

    /// <summary>A palette this drawer keeps whatever theme is chosen (the debug panel's: Plain), null
    /// for the theme in force.</summary>
    public UWGuiTheme.Palette FixedPalette { get; set; }

    // ------------------------------------------------- The pieces

    /// <summary>One piece of the pool: a group at a rectangle with a fill, a picture and a text in it,
    /// each made on its first use; a clip (a scrolled area, a text field) holds the pieces drawn inside
    /// it in Inside.</summary>
    private sealed class Slot
    {
        public RectTransform Rect;

        public Image Fill;

        public RawImage Picture;

        public Text Label;

        public RectTransform Inside;

        public Transform Parent;

        public Rect At = new Rect(float.NaN, 0f, 0f, 0f);

        public bool Shown;

        public float LabelPadding = float.NaN;
    }

    /// <summary>Where the pieces drawn now go: the root or a clip's inside, the offset from the
    /// callers' coordinates to the parent's, and to the root's, and the clip in root coordinates.</summary>
    private sealed class Layer
    {
        public Transform Parent;

        public Vector2 ToParent;

        public Vector2 ToRoot;

        public Rect Clip;

        public int NextSibling;

        /// <summary>The scrolled area the pieces drawn now lie in (cleanup 8d.4): its layer's index
        /// and its count in drawing order (BeginScroll), -1 for none.</summary>
        public int ScrollLayer = -1;

        public int ScrollOrdinal = -1;
    }

    private readonly Transform mOOwner;

    private readonly int miSortingOrder;

    private Canvas mOCanvas;

    private CanvasScaler mOScaler;

    private RectTransform mORoot;

    private readonly List<Slot> mOSlots = new List<Slot>();

    private int miSlot;

    private readonly List<Layer> mOLayers = new List<Layer>();

    private int miLayer;

    private bool mbDrawing;

    private bool mbEnabled = true;

    private float mfScale = 1f;

    /// <summary>The kit's arrow-key navigation.</summary>
    public readonly UWKitKeyNav KeyNav = new UWKitKeyNav();

    public UWKitRows(Transform pOOwner, int piSortingOrder)
        : this(pOOwner, piSortingOrder, UWRowLookEnum.Setup)
    {
    }

    /// <summary>peLook: the window's own type (cleanup 8b.4: the layout editor's).</summary>
    public UWKitRows(Transform pOOwner, int piSortingOrder, UWRowLookEnum peLook)
    {
        mOOwner = pOOwner;
        miSortingOrder = piSortingOrder;
        meLook = peLook;
        // The field's line decides what it takes: the one filter, or any text (TypesAnyText, 8b.2).
        mOEntry = new UWTextEntry(fOnCharacter, pcChar => mOLine.Takes(pcChar));
    }

    /// <summary>The screen's width in layout units (the IMGUI windows' Screen.width / Scale).</summary>
    public float Width => Screen.width / mfScale;

    // ------------------------------------------------- The frame

    /// <summary>
    /// Starts a frame of drawing. Not shown: the canvas goes off, a text field lets go, and the host
    /// draws nothing until the next Begin. pbTakesKeys: the arrow keys may step (UWKitKeyNav);
    /// pbArrowActivates: the first arrow switches the navigation on; pbKeysElsewhere: a text input
    /// elsewhere has the keyboard (the IMGUI navigation's own rule; since cleanup 8b.4 the debug
    /// tools' input lines, on the debug panel's own kit drawer since cleanup 8c).
    /// </summary>
    public void Begin(bool pbShown, bool pbTakesKeys, bool pbArrowActivates, bool pbKeysElsewhere)
    {
        mbDrawing = pbShown;

        if (!pbShown)
        {
            if (mOCanvas != null && mOCanvas.enabled)
                mOCanvas.enabled = false;

            fLetGoOfField();
            miActive = 0;

            return;
        }

        fEnsureCanvas();

        if (!mOCanvas.enabled)
            mOCanvas.enabled = true;

        mOPalette = FixedPalette ?? UWGuiTheme.Current;

        float lfScale = UWGuiTheme.Scale;

        if (lfScale != mfScale)
        {
            mfScale = lfScale;
            mOWidths.Clear();
            mOHeights.Clear();
        }

        if (mOScaler.scaleFactor != mfScale)
            mOScaler.scaleFactor = mfScale;

        Vector2 lOSize = new Vector2(Width, UWGuiTheme.ReferenceHeight);

        if (mORoot.sizeDelta != lOSize)
            mORoot.sizeDelta = lOSize;

        if (miLanguage != UWText.LanguageVersion)
        {
            miLanguage = UWText.LanguageVersion;
            mOWidths.Clear();
            mOHeights.Clear();
        }

        fReadPointer();
        fReadTyping();

        KeyNav.Begin(pbTakesKeys && !IsTyping && !pbKeysElsewhere, pbArrowActivates);
        fBeginReveal();

        miSlot = 0;
        miLayer = 0;
        miNextId = 0;
        miActiveSeen = 0;
        mbEnabled = true;
        PointerWithheld = false;
        mbFieldDrawn = false;
        mbFieldHit = false;

        Layer lORoot = fLayerAt(0);

        lORoot.Parent = mORoot;
        lORoot.ToParent = Vector2.zero;
        lORoot.ToRoot = Vector2.zero;
        lORoot.Clip = new Rect(float.MinValue / 4f, float.MinValue / 4f, float.MaxValue / 2f, float.MaxValue / 2f);
        lORoot.NextSibling = 0;
        lORoot.ScrollLayer = -1;
        lORoot.ScrollOrdinal = -1;
    }

    /// <summary>Ends the frame: what it left unused hides, a press ends with the button's release,
    /// a text field no longer drawn or clicked beside lets go.</summary>
    public void End()
    {
        if (!mbDrawing)
            return;

        for (int liAt = miSlot; liAt < mOSlots.Count; liAt++)
        {
            Slot lOSlot = mOSlots[liAt];

            if (lOSlot.Shown)
            {
                lOSlot.Rect.gameObject.SetActive(false);
                lOSlot.Shown = false;
            }
        }

        if (miActive != 0 && (miActiveSeen != miActive || !mbHeld))
            miActive = 0;

        if (msFocusField != null && (!mbFieldDrawn || (mbDown && !mbFieldHit)))
            fLetGoOfField();

        mbEnter = false;
        mbEscape = false;
        msEscapedField = null;

        KeyNav.End();
        mbDrawing = false;
    }

    /// <summary>The text field's typing ends with the window (the host's OnDestroy).</summary>
    public void Dispose()
    {
        fLetGoOfField();
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        mOCanvas = UWUiKit.CreateOverlayCanvas(mOOwner, "Kit Windows Canvas", miSortingOrder);
        mOScaler = mOCanvas.GetComponent<CanvasScaler>();
        mORoot = UWUiKit.CreateGroup(mOCanvas.transform, "Rows", UWUiKit.TopLeft);
        mORoot.anchoredPosition = Vector2.zero;
    }

    private Layer fLayerAt(int piAt)
    {
        while (mOLayers.Count <= piAt)
            mOLayers.Add(new Layer());

        return mOLayers[piAt];
    }

    // ------------------------------------------------- The pointer

    private bool mbPointer;

    /// <summary>The pointer in layout units from the top-left corner.</summary>
    private Vector2 mOPointer;

    private bool mbDown;

    private bool mbUp;

    private bool mbHeld;

    private float mfWheel;

    /// <summary>The pieces that take the pointer count themselves in drawing order.</summary>
    private int miNextId;

    /// <summary>The piece that took the press (0: none), and whether it was drawn this frame.</summary>
    private int miActive;

    private int miActiveSeen;

    /// <summary>Where the pointer holds a dragged piece or a scroll bar's thumb.</summary>
    private Vector2 mODragOffset;

    private void fReadPointer()
    {
        mbDown = false;
        mbUp = false;
        mbHeld = false;
        mfWheel = 0f;
        mbPointer = UWModernUi.WindowTakesPointer(out Vector2 lOScreen);

        if (!mbPointer)
            return;

        mOPointer = new Vector2(lOScreen.x / mfScale, (Screen.height - lOScreen.y) / mfScale);

        mbDown = UWPointer.LeftPressed;
        mbUp = UWPointer.LeftReleased;
        mbHeld = UWPointer.LeftHeld;
        mfWheel = UWPointer.Scroll.y;
    }

    /// <summary>
    /// THE POINTER WITHHELD from the pieces drawn while this is on (cleanup 8b.4, 2026-10-24): nothing
    /// lights, takes a press or turns with the wheel, and switched off again the pieces drawn after
    /// take it as before - a window under a modal question (the layout editor's save question, IMGUI's
    /// ModalWindow until then) is drawn first with it on, the question after with it off. Off at Begin.
    /// </summary>
    public bool PointerWithheld { get; set; }

    /// <summary>The pointer is there for the piece being drawn.</summary>
    private bool PointerOn => mbPointer && !PointerWithheld;

    /// <summary>A piece that takes the pointer: whether the pointer is over it (pbOver), it was
    /// clicked - pressed and released over it - (pbClicked), and the button holds it (pbHolding, the
    /// pointer anywhere). Greyed pieces take nothing.</summary>
    private int fControl(Rect pORect, out bool pbOver, out bool pbClicked, out bool pbHolding)
    {
        int liId = ++miNextId;
        Layer lOLayer = mOLayers[miLayer];

        pbOver = PointerOn && lOLayer.Clip.Contains(mOPointer)
            && new Rect(pORect.position + lOLayer.ToRoot, pORect.size).Contains(mOPointer);

        if (mbEnabled && mbDown && miActive == 0 && pbOver)
            miActive = liId;

        if (miActive == liId)
            miActiveSeen = liId;

        pbClicked = mbEnabled && miActive == liId && mbUp && pbOver;
        pbHolding = miActive == liId && (mbHeld || mbUp);

        return liId;
    }

    /// <summary>The pointer is over the piece and nothing else holds the button.</summary>
    private bool fHover(int piId, bool pbOver)
    {
        return mbEnabled && pbOver && (miActive == 0 || miActive == piId);
    }

    /// <summary>The pointer in the callers' coordinates of the current layer.</summary>
    private Vector2 PointerHere => mOPointer - mOLayers[miLayer].ToRoot;

    // ------------------------------------------------- Drawing the pieces

    private Slot fSlot(Rect pORect)
    {
        Slot lOSlot;

        if (miSlot < mOSlots.Count)
            lOSlot = mOSlots[miSlot];
        else
        {
            lOSlot = new Slot { Rect = UWUiKit.CreateGroup(mORoot, "Piece", UWUiKit.TopLeft), Parent = mORoot, Shown = true };
            mOSlots.Add(lOSlot);
        }

        miSlot++;

        Layer lOLayer = mOLayers[miLayer];

        if (lOSlot.Parent != lOLayer.Parent)
        {
            lOSlot.Rect.SetParent(lOLayer.Parent, false);
            lOSlot.Parent = lOLayer.Parent;
        }

        int liIndex = lOLayer.NextSibling++;

        if (lOSlot.Rect.GetSiblingIndex() != liIndex)
            lOSlot.Rect.SetSiblingIndex(liIndex);

        if (!lOSlot.Shown)
        {
            lOSlot.Rect.gameObject.SetActive(true);
            lOSlot.Shown = true;
        }

        Rect lOAt = new Rect(pORect.position + lOLayer.ToParent, pORect.size);

        if (lOAt != lOSlot.At)
        {
            lOSlot.At = lOAt;
            lOSlot.Rect.anchoredPosition = new Vector2(lOAt.x, -lOAt.y);
            lOSlot.Rect.sizeDelta = lOAt.size;
        }

        return lOSlot;
    }

    private Color fTint(Color pOColour)
    {
        return mbEnabled ? pOColour : new Color(pOColour.r, pOColour.g, pOColour.b, pOColour.a * DisabledAlpha);
    }

    private static void fSetFill(Slot pOSlot, bool pbOn, Color pOColour)
    {
        if (pOSlot.Fill == null)
        {
            if (!pbOn)
                return;

            pOSlot.Fill = UWUiKit.CreateImage(pOSlot.Rect, "Fill", UWUiKit.TopLeft, UWUiKit.TopLeft, pOColour);
            UWUiKit.Stretch(pOSlot.Fill.rectTransform);
            pOSlot.Fill.rectTransform.SetAsFirstSibling();
        }

        if (pOSlot.Fill.enabled != pbOn)
            pOSlot.Fill.enabled = pbOn;

        if (pbOn && pOSlot.Fill.color != pOColour)
            pOSlot.Fill.color = pOColour;
    }

    /// <summary>The whole picture, stretched (a RawImage's own uvRect).</summary>
    private static readonly Rect FullPicture = new Rect(0f, 0f, 1f, 1f);

    private static void fSetPicture(Slot pOSlot, Texture pOTexture, Color pOColour)
    {
        fSetPicture(pOSlot, pOTexture, pOColour, FullPicture);
    }

    /// <summary>A picture over the piece; pOUv the part of it shown, beyond 0 to 1 repeated where the
    /// texture wraps (the carved grounds' clouds and grain, cleanup phase 8d) - set back to the whole
    /// picture for any other, since the pool hands a piece to whatever is drawn next.</summary>
    private static void fSetPicture(Slot pOSlot, Texture pOTexture, Color pOColour, Rect pOUv)
    {
        bool lbOn = pOTexture != null;

        if (pOSlot.Picture == null)
        {
            if (!lbOn)
                return;

            pOSlot.Picture = UWUiKit.CreateRawImage(pOSlot.Rect, "Picture", UWUiKit.TopLeft, UWUiKit.TopLeft, true);
            UWUiKit.Stretch(pOSlot.Picture.rectTransform);
        }

        if (pOSlot.Picture.enabled != lbOn)
            pOSlot.Picture.enabled = lbOn;

        if (!lbOn)
            return;

        if (pOSlot.Picture.texture != pOTexture)
            pOSlot.Picture.texture = pOTexture;

        if (pOSlot.Picture.color != pOColour)
            pOSlot.Picture.color = pOColour;

        if (pOSlot.Picture.uvRect != pOUv)
            pOSlot.Picture.uvRect = pOUv;
    }

    private void fSetLabel(Slot pOSlot, string psText, in Look pOLook)
    {
        bool lbOn = !string.IsNullOrEmpty(psText);

        if (pOSlot.Label == null)
        {
            if (!lbOn)
                return;

            pOSlot.Label = UWUiKit.CreateText(pOSlot.Rect, "Text", UWUiKit.TopLeft, UWUiKit.TopLeft, pOLook.Anchor, pOLook.Colour, null,
                new UWUiKit.TextStyle { Font = UWUiKit.TextFontEnum.Interface, Effect = UWUiKit.TextEffectEnum.None, KeepsItsFont = true });
            pOSlot.Label.supportRichText = false;
            UWUiKit.Stretch(pOSlot.Label.rectTransform);

            if (pOSlot.Label.font != PieceFont)
                pOSlot.Label.font = PieceFont;
        }

        Text lOText = pOSlot.Label;

        if (lOText.enabled != lbOn)
            lOText.enabled = lbOn;

        if (!lbOn)
            return;

        if (lOText.text != psText)
            lOText.text = psText;

        if (lOText.fontSize != pOLook.Size)
            lOText.fontSize = pOLook.Size;

        Color lOColour = fTint(pOLook.Colour);

        if (lOText.color != lOColour)
            lOText.color = lOColour;

        if (lOText.alignment != pOLook.Anchor)
            lOText.alignment = pOLook.Anchor;

        HorizontalWrapMode leWrap = pOLook.Wraps ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;

        if (lOText.horizontalOverflow != leWrap)
            lOText.horizontalOverflow = leWrap;

        if (pOSlot.LabelPadding != pOLook.Padding)
        {
            pOSlot.LabelPadding = pOLook.Padding;
            lOText.rectTransform.offsetMin = new Vector2(pOLook.Padding, 0f);
            lOText.rectTransform.offsetMax = new Vector2(-pOLook.Padding, 0f);
        }
    }

    /// <summary>A plain colour over the rectangle.</summary>
    private void fFill(Rect pORect, Color pOColour)
    {
        Slot lOSlot = fSlot(pORect);

        fSetFill(lOSlot, true, fTint(pOColour));
        fSetPicture(lOSlot, null, Color.white);
        fSetLabel(lOSlot, null, default);
    }

    /// <summary>A text over the rectangle, on a ground when pbGround.</summary>
    private void fText(Rect pORect, string psText, in Look pOLook, bool pbGround = false, Color pOGround = default)
    {
        Slot lOSlot = fSlot(pORect);

        fSetFill(lOSlot, pbGround, fTint(pOGround));
        fSetPicture(lOSlot, null, Color.white);
        fSetLabel(lOSlot, psText, pOLook);
    }

    /// <summary>The navigation's gold frame round a piece with the focus; in a theme with a glow (Stone:
    /// the runes' ember, cleanup phase 8d) a soft frame of it outside the gold one first.</summary>
    private void fFocusFrame(Rect pORect)
    {
        const float Frame = UWKitKeyNav.FocusFrame;
        Color lOColour = mOPalette.Focus;
        Color lOGlow = mOPalette.FocusGlow;
        bool lbEnabled = mbEnabled;

        mbEnabled = true;

        if (lOGlow.a > 0f)
        {
            Rect lOOuter = new Rect(pORect.x - Frame, pORect.y - Frame, pORect.width + (2f * Frame), pORect.height + (2f * Frame));

            fFill(new Rect(lOOuter.x, lOOuter.y, lOOuter.width, Frame), lOGlow);
            fFill(new Rect(lOOuter.x, lOOuter.yMax - Frame, lOOuter.width, Frame), lOGlow);
            fFill(new Rect(lOOuter.x, pORect.y, Frame, pORect.height), lOGlow);
            fFill(new Rect(lOOuter.xMax - Frame, pORect.y, Frame, pORect.height), lOGlow);
        }

        fFill(new Rect(pORect.x, pORect.y, pORect.width, Frame), lOColour);
        fFill(new Rect(pORect.x, pORect.yMax - Frame, pORect.width, Frame), lOColour);
        fFill(new Rect(pORect.x, pORect.y, Frame, pORect.height), lOColour);
        fFill(new Rect(pORect.xMax - Frame, pORect.y, Frame, pORect.height), lOColour);
        mbEnabled = lbEnabled;
    }

    /// <summary>A navigation entry at pORect: true when a key pressed it (a greyed piece is counted, as
    /// in IMGUI, but not pressed); its frame is drawn by fFocusFrame after the piece.</summary>
    private bool fEntry(Rect pORect, out bool pbFocused)
    {
        bool lbPressed = KeyNav.Entry(out pbFocused);

        if (pbFocused)
            fFocusDrawn(pORect);

        return lbPressed && mbEnabled;
    }

    // ------------------------------------------------- The view follows the focus (cleanup 8d.4)

    // THE VIEW FOLLOWS THE FOCUS (cleanup 8d.4, per user 2026-10-25): when the arrow keys put the focus
    // on an entry (or move the slider with it) that a scrolled area holds out of view, the area scrolls
    // by the least that shows it whole (UWKeyNavRules.RevealScroll) - the Remastered effects, the
    // Controls lists, Motion, the resolutions and the folder list. The entry's place is known only once
    // it is drawn, after its area's BeginScroll, so the area scrolls in the next frame's BeginScroll -
    // the same area by its count in drawing order. An entry outside any scrolled area is offered to the
    // host instead (FocusToReveal: the palette effects' window moves itself on the screen). The wheel and
    // the bar scroll as before; the view follows only a change the keys made.

    /// <summary>The focus the last frame was drawn with.</summary>
    private int miSeenFocus = -1;

    /// <summary>The entry with the focus is to be shown whole when it is drawn this frame.</summary>
    private bool mbRevealFocus;

    /// <summary>The scrolled areas of this frame so far, counted in drawing order.</summary>
    private int miScrollCount;

    /// <summary>The scrolled area (its count) that scrolls in the next frame to show mfRevealTop to
    /// mfRevealBottom of its inside; -1 for none. Taken over by miApplyScroll at Begin.</summary>
    private int miRevealScroll = -1;

    private int miApplyScroll = -1;

    private float mfRevealTop;

    private float mfRevealBottom;

    /// <summary>The focused entry drawn outside any scrolled area this frame, in the root's coordinates,
    /// for FocusToReveal.</summary>
    private bool mbRevealOutside;

    private Rect mORevealOutside;

    /// <summary>At Begin, after the keys: the request of the last frame is this frame's to apply; a
    /// focus moved by the keys (or a slider moved) is to be shown.</summary>
    private void fBeginReveal()
    {
        int liFocus = KeyNav.Focus;

        miApplyScroll = miRevealScroll;
        miRevealScroll = -1;
        miScrollCount = 0;
        mbRevealOutside = false;
        mbRevealFocus = liFocus >= 0 && (liFocus != miSeenFocus || KeyNav.Slides);
        miSeenFocus = liFocus;
    }

    /// <summary>The entry with the focus is drawn at pORect (the callers' coordinates of the current
    /// layer): where it is to be shown, its place inside its scrolled area is kept for that area's next
    /// BeginScroll, the focus frame's glow included - or, outside any, for FocusToReveal.</summary>
    private void fFocusDrawn(Rect pORect)
    {
        if (!mbRevealFocus)
            return;

        mbRevealFocus = false;

        Layer lOLayer = mOLayers[miLayer];
        Rect lORoot = new Rect(pORect.position + lOLayer.ToRoot, pORect.size);

        if (lOLayer.ScrollLayer < 0)
        {
            mbRevealOutside = true;
            mORevealOutside = lORoot;

            return;
        }

        // The inside's coordinates of the area: the root's less where the inside's corner lies.
        float lfInside = mOLayers[lOLayer.ScrollLayer].ToRoot.y;

        miRevealScroll = lOLayer.ScrollOrdinal;
        mfRevealTop = lORoot.yMin - lfInside - UWKitKeyNav.FocusFrame;
        mfRevealBottom = lORoot.yMax - lfInside + UWKitKeyNav.FocusFrame;
    }

    /// <summary>The entry the keys put the focus on this frame was drawn outside any scrolled area, at
    /// pORect (layout units from the screen's top-left corner): true once, then taken. A host whose
    /// window can stand partly off the screen moves it so the entry shows (the palette effects').</summary>
    public bool FocusToReveal(out Rect pORect)
    {
        pORect = mORevealOutside;

        bool lbReveal = mbRevealOutside;

        mbRevealOutside = false;

        return lbReveal;
    }

    /// <summary>Starts a clip: what is drawn until fEndClip lies inside pOView and is cut at its
    /// edges. pOOrigin: the point of the callers' coordinates that lands on the view's top-left corner
    /// (0,0 for a scroll view's content, the view's own corner for a field drawn in the outer ones);
    /// pOScroll moves the inside up and left.</summary>
    private void fBeginClip(Rect pOView, Vector2 pOOrigin, Vector2 pOScroll)
    {
        Layer lOOuter = mOLayers[miLayer];
        Slot lOSlot = fSlot(pOView);

        fSetFill(lOSlot, false, Color.clear);
        fSetPicture(lOSlot, null, Color.white);
        fSetLabel(lOSlot, null, default);

        if (lOSlot.Inside == null)
        {
            lOSlot.Inside = UWUiKit.CreateClipArea(lOSlot.Rect, "Inside", UWUiKit.TopLeft);
            UWUiKit.Stretch(lOSlot.Inside);
        }

        Rect lOViewRoot = new Rect(pOView.position + lOOuter.ToRoot, pOView.size);
        Layer lOInner = fLayerAt(++miLayer);

        lOInner.Parent = lOSlot.Inside;
        lOInner.ToParent = -pOOrigin - pOScroll;
        lOInner.ToRoot = lOViewRoot.position - pOOrigin - pOScroll;
        lOInner.Clip = fIntersect(lOOuter.Clip, lOViewRoot);
        lOInner.NextSibling = 0;
        // Inside a scrolled area still (a clip of its own does not scroll); BeginScroll sets its own.
        lOInner.ScrollLayer = lOOuter.ScrollLayer;
        lOInner.ScrollOrdinal = lOOuter.ScrollOrdinal;
    }

    private void fEndClip()
    {
        if (miLayer > 0)
            miLayer--;
    }

    private static Rect fIntersect(Rect pOA, Rect pOB)
    {
        float lfX = Mathf.Max(pOA.xMin, pOB.xMin);
        float lfY = Mathf.Max(pOA.yMin, pOB.yMin);

        return Rect.MinMaxRect(lfX, lfY, Mathf.Max(lfX, Mathf.Min(pOA.xMax, pOB.xMax)), Mathf.Max(lfY, Mathf.Min(pOA.yMax, pOB.yMax)));
    }

    // ------------------------------------------------- Measuring

    private readonly TextGenerator mOGenerator = new TextGenerator();

    private readonly Dictionary<(string, int), float> mOWidths = new Dictionary<(string, int), float>();

    private readonly Dictionary<(string, int, int), float> mOHeights = new Dictionary<(string, int, int), float>();

    private int miLanguage = -1;

    /// <summary>The settings a Text of the canvas generates with (as Text.GetGenerationSettings makes
    /// them: the size in layout units, the canvas' scale as the scale factor, the extents in layout
    /// units), so a measure is what the canvas draws.</summary>
    private TextGenerationSettings fSettings(int piSize, float pfWrapWidth)
    {
        bool lbWraps = pfWrapWidth > 0f;

        return new TextGenerationSettings
        {
            font = PieceFont,
            fontSize = piSize,
            fontStyle = FontStyle.Normal,
            lineSpacing = 1f,
            richText = false,
            scaleFactor = mfScale,
            color = Color.white,
            textAnchor = TextAnchor.UpperLeft,
            alignByGeometry = false,
            resizeTextForBestFit = false,
            updateBounds = false,
            verticalOverflow = VerticalWrapMode.Overflow,
            horizontalOverflow = lbWraps ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow,
            generationExtents = new Vector2(lbWraps ? pfWrapWidth : 0f, 0f),
            pivot = Vector2.zero,
            generateOutOfBounds = true
        };
    }

    /// <summary>A text's width in layout units at a size, measured as the canvas draws it (the font
    /// at the size times the scale, back in layout units).</summary>
    private float fTextWidth(string psText, int piSize)
    {
        if (string.IsNullOrEmpty(psText))
            return 0f;

        if (mOWidths.TryGetValue((psText, piSize), out float lfWidth))
            return lfWidth;

        lfWidth = mOGenerator.GetPreferredWidth(psText, fSettings(piSize, 0f)) / mfScale;
        mOWidths[(psText, piSize)] = lfWidth;

        return lfWidth;
    }

    /// <summary>A text's height wrapped at a width, in layout units.</summary>
    private float fTextHeight(string psText, int piSize, float pfWidth)
    {
        string lsText = string.IsNullOrEmpty(psText) ? " " : psText;
        int liWidth = Mathf.RoundToInt(pfWidth);

        if (mOHeights.TryGetValue((lsText, piSize, liWidth), out float lfHeight))
            return lfHeight;

        lfHeight = mOGenerator.GetPreferredHeight(lsText, fSettings(piSize, Mathf.Max(1f, pfWidth))) / mfScale;
        mOHeights[(lsText, piSize, liWidth)] = lfHeight;

        return lfHeight;
    }

    // ------------------------------------------------- IUWRows: sizes

    public bool Enabled
    {
        get { return mbEnabled; }
        set { mbEnabled = value; }
    }

    /// <summary>The kit sizes by text (UWRowSizing).</summary>
    public bool SizesByText => true;

    public float TextWidth(string psText, UWRowInfoEnum peLook)
    {
        return fTextWidth(psText, fInfoLook(peLook).Size);
    }

    public float TextHeight(string psText, UWRowInfoEnum peLook, float pfWidth)
    {
        Look lOLook = fInfoLook(peLook);

        return lOLook.Wraps ? fTextHeight(psText, lOLook.Size, pfWidth) : fTextHeight(psText, lOLook.Size, 10000f);
    }

    public float ToggleWidth(string psText)
    {
        return ToggleTextLeft + fTextWidth(psText, TextSize);
    }

    public float FitWidth(string psText, float pfNominal)
    {
        return UWRowSizing.Fit(fTextWidth(psText, TextSize), pfNominal);
    }

    public float LabelWidth(string psText)
    {
        return fTextWidth(psText, TextSize);
    }

    // ------------------------------------------------- IUWRows: frames

    public Rect Dialog(float pfScreenWidth, float pfMaxWidth, float pfTop, float pfHeight)
    {
        float lfWidth = Mathf.Min(pfMaxWidth, pfScreenWidth - 40f);
        Rect lODialog = new Rect((pfScreenWidth - lfWidth) / 2f, pfTop, lfWidth, pfHeight);

        Frame(lODialog);

        return lODialog;
    }

    /// <summary>A dialog's ground (Ground: in Plain the panel's colour, as always).</summary>
    public void Frame(Rect pORect)
    {
        Ground(pORect, UWWindowGroundEnum.Dialog);
    }

    // ------------------------------------------------- The grounds (cleanup phase 8d, 2026-10-25)

    /// <summary>
    /// A WINDOW'S GROUND in the theme of this pass (cleanup phase 8d, 2026-10-25). A flat theme fills
    /// it in the ground's colour - in Plain the one fill each window always drew, nothing more. A
    /// carved theme (Stone) lays the stone there: the gradient stretched over the window, or the
    /// clouds repeated at one texel per layout unit (anchored at its top-left corner, so they move with
    /// the window and stand still while it grows downward) with their light stretched over them; the
    /// grain over either at one texel per screen pixel; all at the ground colour's opacity (the help
    /// and the game menu stay a little see-through); then the edges, lit on the top and left and in
    /// shadow on the bottom and right - not round the first-run screen, which is the whole screen.
    /// </summary>
    public void Ground(Rect pORect, UWWindowGroundEnum peGround)
    {
        Color lOColour = mOPalette.GroundColour(peGround);

        if (!mOPalette.IsCarved)
        {
            fFill(pORect, lOColour);
            return;
        }

        UWGuiTheme.GroundPictures lOPictures = UWGuiTheme.PicturesOf(mOPalette);
        Color lOTint = new Color(1f, 1f, 1f, lOColour.a);

        if (UWGuiTheme.Ground == UWWindowBackgroundEnum.Clouds)
        {
            float lfTile = UWThemeGround.CloudTileUnits;
            float lfAcross = pORect.width / lfTile;
            float lfDown = pORect.height / lfTile;

            fPicture(pORect, lOPictures.Clouds, lOTint, new Rect(0f, -lfDown, lfAcross, lfDown));
            fPicture(pORect, lOPictures.Light, lOTint, FullPicture);
        }
        else
            fPicture(pORect, lOPictures.Gradient, lOTint, FullPicture);

        float lfGrain = UWThemeGround.GrainSize / mfScale;

        fPicture(pORect, lOPictures.Grain, lOTint, new Rect(0f, 0f, pORect.width / lfGrain, pORect.height / lfGrain));

        if (peGround != UWWindowGroundEnum.Screen)
            fEdges(pORect, mOPalette.Theme.EdgeWidth, false);
    }

    /// <summary>A picture over the rectangle, the part pOUv of it (greyed with the row).</summary>
    private void fPicture(Rect pORect, Texture pOTexture, Color pOColour, Rect pOUv)
    {
        Slot lOSlot = fSlot(pORect);

        fSetFill(lOSlot, false, Color.clear);
        fSetPicture(lOSlot, pOTexture, fTint(pOColour), pOUv);
        fSetLabel(lOSlot, null, default);
    }

    /// <summary>A carved theme's edges round the rectangle, pfWidth wide: the light edge on the top and
    /// left, the shadow on the bottom and right - the other way round when pbSunk (a held button).</summary>
    private void fEdges(Rect pORect, float pfWidth, bool pbSunk)
    {
        Color lOLight = pbSunk ? mOPalette.EdgeShadow : mOPalette.EdgeLight;
        Color lOShadow = pbSunk ? mOPalette.EdgeLight : mOPalette.EdgeShadow;
        float lfEdge = Mathf.Min(pfWidth, Mathf.Min(pORect.width, pORect.height) / 2f);

        fFill(new Rect(pORect.x, pORect.y, pORect.width, lfEdge), lOLight);
        fFill(new Rect(pORect.x, pORect.y + lfEdge, lfEdge, pORect.height - lfEdge), lOLight);
        fFill(new Rect(pORect.x + lfEdge, pORect.yMax - lfEdge, pORect.width - lfEdge, lfEdge), lOShadow);
        fFill(new Rect(pORect.xMax - lfEdge, pORect.y + lfEdge, lfEdge, pORect.height - (2f * lfEdge)), lOShadow);
    }

    /// <summary>The first-run screen's ground: the whole screen in the theme's ground (Plain: the
    /// darkest ground's colour, as the backdrop always was).</summary>
    public void ScreenGround(Rect pORect)
    {
        Ground(pORect, UWWindowGroundEnum.Screen);
    }

    /// <summary>A dialog's title; in the layout editor's look its title row: the accent colour on the
    /// bar's ground, set in by the window's margin (UWGuiTheme.LayoutEditorStyles.Title).</summary>
    public void Title(Rect pORect, string psText)
    {
        if (IsEditor)
        {
            fText(pORect, psText, fLook(UWGuiTheme.LayoutEditorTitleSize, mOPalette.Accent, TextAnchor.MiddleLeft, UWModernLayoutEditor.WindowPad),
                true, mOPalette.Bar);
            return;
        }

        fText(pORect, psText, HeadingLook);
    }

    /// <summary>Done in the dialog's bottom right corner, its right edge where the IMGUI form's
    /// stands, as wide as its text needs.</summary>
    public bool Done(Rect pODialog)
    {
        string lsDone = UWText.T("setup.done");
        float lfWidth = FitWidth(lsDone, DoneWidth);

        return Action(new Rect(pODialog.xMax - DoneRightGap - lfWidth, pODialog.yMax - DoneFromBottom, lfWidth, DoneHeight), lsDone);
    }

    public float Hint(float pfLeft, float pfTop, float pfWidth, string psText)
    {
        Look lOLook = fDimLook(mOPalette.DimText);
        float lfHeight = fTextHeight(psText, lOLook.Size, pfWidth);

        fText(new Rect(pfLeft, pfTop, pfWidth, lfHeight), psText, lOLook);

        return lfHeight;
    }

    /// <summary>A thin line between groups; in a carved theme a groove (cleanup phase 8d): the shadow in
    /// the line, the separator's light edge just below it.</summary>
    public void Separator(Rect pORect)
    {
        if (!mOPalette.IsCarved)
        {
            fFill(pORect, mOPalette.Separator);
            return;
        }

        fFill(pORect, mOPalette.EdgeShadow);
        fFill(new Rect(pORect.x, pORect.yMax, pORect.width, pORect.height), mOPalette.Separator);
    }

    public void Picture(Rect pORect, Texture pOTexture)
    {
        Slot lOSlot = fSlot(pORect);

        fSetFill(lOSlot, false, Color.clear);
        fSetPicture(lOSlot, pOTexture, fTint(Color.white));
        fSetLabel(lOSlot, null, default);
    }

    /// <summary>
    /// A scrolled area as IMGUI's scroll view: the wheel over it and the bar on its right (there while
    /// the inside is taller) move it; what is drawn until EndScroll is in pOContent's coordinates and
    /// cut at the view's edges. Returns the new scroll position. Since cleanup 8d.4 it also scrolls to
    /// show the entry the arrow keys put the focus on in it in the last frame (fFocusDrawn).
    /// </summary>
    public Vector2 BeginScroll(Rect pOView, Vector2 pOScroll, Rect pOContent)
    {
        float lfRoom = Mathf.Max(0f, pOContent.height - pOView.height);
        Vector2 lOScroll = new Vector2(0f, Mathf.Clamp(pOScroll.y, 0f, lfRoom));
        Layer lOLayer = mOLayers[miLayer];
        int liOrdinal = miScrollCount++;

        if (liOrdinal == miApplyScroll)
        {
            miApplyScroll = -1;
            lOScroll.y = UWKeyNavRules.RevealScroll(lOScroll.y, pOView.height, mfRevealTop, mfRevealBottom, lfRoom);
        }

        bool lbOverView = PointerOn && lOLayer.Clip.Contains(mOPointer)
            && new Rect(pOView.position + lOLayer.ToRoot, pOView.size).Contains(mOPointer);

        if (lbOverView && Mathf.Abs(mfWheel) > 0.01f && lfRoom > 0f)
        {
            float lfSteps = Mathf.Max(1f, Mathf.Abs(mfWheel) / 120f);

            lOScroll.y = Mathf.Clamp(lOScroll.y - (Mathf.Sign(mfWheel) * lfSteps * WheelStep), 0f, lfRoom);
        }

        if (lfRoom > 0f)
        {
            Rect lOTrack = new Rect(pOView.xMax - ScrollBarWidth, pOView.y, ScrollBarWidth, pOView.height);
            float lfThumbHeight = Mathf.Max(20f, pOView.height * pOView.height / pOContent.height);
            float lfTravel = Mathf.Max(1f, lOTrack.height - lfThumbHeight);
            Rect lOThumb = new Rect(lOTrack.x + 2f, lOTrack.y + (lOScroll.y / lfRoom * lfTravel), ScrollBarWidth - 4f, lfThumbHeight);
            int liId = fControl(lOTrack, out bool lbOver, out bool lbClicked, out bool lbHolding);

            if (miActive == liId && mbDown)
                mODragOffset = new Vector2(0f, lOThumb.Contains(PointerHere) ? PointerHere.y - lOThumb.y : lfThumbHeight / 2f);

            if (lbHolding)
            {
                lOScroll.y = Mathf.Clamp((PointerHere.y - mODragOffset.y - lOTrack.y) / lfTravel * lfRoom, 0f, lfRoom);
                lOThumb.y = lOTrack.y + (lOScroll.y / lfRoom * lfTravel);
            }

            fFill(lOTrack, mOPalette.Background);
            fFill(lOThumb, lbHolding || fHover(liId, lbOver) ? mOPalette.ButtonHover : mOPalette.Separator);
        }

        fBeginClip(pOView, Vector2.zero, lOScroll);

        Layer lOInside = mOLayers[miLayer];

        lOInside.ScrollLayer = miLayer;
        lOInside.ScrollOrdinal = liOrdinal;

        return lOScroll;
    }

    public void EndScroll()
    {
        fEndClip();
    }

    /// <summary>A frame moved by the left button held on pOHandle (UWGuiRows.Drag): returns where
    /// its corner now stands, kept within pOMin to pOMax.</summary>
    public Vector2 Drag(Rect pOHandle, Vector2 pOAt, Vector2 pOMin, Vector2 pOMax)
    {
        int liId = fControl(pOHandle, out bool _, out bool _, out bool lbHolding);

        if (miActive == liId && mbDown)
            mODragOffset = PointerHere - pOAt;

        if (!lbHolding || mbDown)
            return pOAt;

        Vector2 lOWanted = PointerHere - mODragOffset;

        return new Vector2(Mathf.Clamp(lOWanted.x, pOMin.x, pOMax.x), Mathf.Clamp(lOWanted.y, pOMin.y, pOMax.y));
    }

    // ------------------------------------------------- IUWRows: lines

    public void Label(Rect pORect, string psText)
    {
        fText(pORect, psText, RowLook);
    }

    /// <summary>
    /// A line in one of the looks. The dim looks wrap inside their rectangle as IMGUI's; a row's or a
    /// heading's line that is wider than its rectangle WRAPS BELOW ITS FIRST LINE (2026-10-24, the
    /// pseudo-language: the MT-32 line ran on under its Open folder button) - the first line stands
    /// where IMGUI's single one did.
    /// </summary>
    public void Info(Rect pORect, string psText, UWRowInfoEnum peLook)
    {
        Look lOLook = fInfoLook(peLook);

        if (lOLook.Wraps || pORect.width <= 0f || fTextWidth(psText, lOLook.Size) <= pORect.width)
        {
            fText(pORect, psText, lOLook);
            return;
        }

        float lfLine = fTextHeight("Ay", lOLook.Size, 10000f);
        float lfHeight = fTextHeight(psText, lOLook.Size, pORect.width);

        lOLook.Anchor = TextAnchor.UpperLeft;
        lOLook.Wraps = true;

        fText(new Rect(pORect.x, pORect.y + ((pORect.height - lfLine) / 2f), pORect.width, lfHeight), psText, lOLook);
    }

    private Look fInfoLook(UWRowInfoEnum peLook)
    {
        switch (peLook)
        {
            case UWRowInfoEnum.Heading: return HeadingLook;
            case UWRowInfoEnum.Dim: return fDimLook(mOPalette.DimText);
            case UWRowInfoEnum.Accent: return fSetupDimLook(mOPalette.Accent);
            case UWRowInfoEnum.Error: return fSetupDimLook(mOPalette.Error);
            default: return RowLook;
        }
    }

    // ------------------------------------------------- IUWRows: buttons and rows

    /// <summary>A button with a face: Hover, ButtonHover under the pointer, the accent with dark text
    /// while held (UWGuiTheme's faced button).</summary>
    public bool Action(Rect pORect, string psText)
    {
        return fFaced(pORect, psText, mOPalette.Text);
    }

    private bool fFaced(Rect pORect, string psText, Color pOText)
    {
        int liId = fControl(pORect, out bool lbOver, out bool lbClicked, out bool lbHolding);
        bool lbKey = fEntry(pORect, out bool lbFocused);
        bool lbPressing = lbHolding && lbOver;
        Color lOGround = lbPressing ? mOPalette.Accent : (fHover(liId, lbOver) ? mOPalette.ButtonHover : mOPalette.Hover);
        Look lOLook = fLook(TextSize, lbPressing ? mOPalette.Background : pOText, TextAnchor.MiddleCenter);

        fText(pORect, psText, lOLook, true, lOGround);

        // A carved theme's face is cut from the stone: lit on the top and left, sunk while held.
        if (mOPalette.IsCarved)
            fEdges(pORect, 1f, lbPressing);

        if (lbFocused)
            fFocusFrame(pORect);

        return lbClicked || lbKey;
    }

    /// <summary>A list row: lit under the pointer, its text in the accent colour when current.</summary>
    public bool Item(Rect pORect, string psText, bool pbCurrent = false)
    {
        return fListRow(pORect, psText, pbCurrent ? mOPalette.Accent : mOPalette.Text, TextAnchor.MiddleLeft, UWGuiTheme.SetupListPadding);
    }

    private bool fListRow(Rect pORect, string psText, Color pOText, TextAnchor peAnchor, float pfPadding)
    {
        int liId = fControl(pORect, out bool lbOver, out bool lbClicked, out bool _);
        bool lbKey = fEntry(pORect, out bool lbFocused);
        bool lbHover = fHover(liId, lbOver);

        fText(pORect, psText, fLook(TextSize, pOText, peAnchor, pfPadding), lbHover, mOPalette.Hover);

        if (lbFocused)
            fFocusFrame(pORect);

        return lbClicked || lbKey;
    }

    /// <summary>A switch: its box at the left, its text beside it; the whole rectangle takes the click,
    /// as IMGUI's. Returns the state it now stands at.</summary>
    public bool Toggle(Rect pORect, bool pbValue, string psText)
    {
        int liId = fControl(pORect, out bool lbOver, out bool lbClicked, out bool _);
        bool lbKey = fEntry(pORect, out bool lbFocused);
        bool lbHover = fHover(liId, lbOver);
        float lfBoxTop = pORect.y + ((pORect.height - ToggleBox) / 2f);
        Rect lOBox = new Rect(pORect.x, lfBoxTop, ToggleBox, ToggleBox);

        fFill(lOBox, lbHover ? mOPalette.ButtonHover : mOPalette.Separator);
        fFill(new Rect(lOBox.x + 1f, lOBox.y + 1f, ToggleBox - 2f, ToggleBox - 2f), mOPalette.Background);

        if (pbValue)
        {
            Slot lOCheck = fSlot(new Rect(lOBox.x + 1f, lOBox.y + 1f, ToggleBox - 2f, ToggleBox - 2f));

            fSetFill(lOCheck, false, Color.clear);
            fSetPicture(lOCheck, CheckTexture, fTint(mOPalette.Text));
            fSetLabel(lOCheck, null, default);
        }

        fText(new Rect(pORect.x + ToggleTextLeft, pORect.y, Mathf.Max(0f, pORect.width - ToggleTextLeft), pORect.height), psText, RowLook);

        if (lbFocused)
            fFocusFrame(pORect);

        return lbClicked || lbKey ? !pbValue : pbValue;
    }

    /// <summary>
    /// Choices side by side over pOArea as UWGuiRows.Choices - the current one in the accent colour,
    /// the others as list rows or buttons with a face, bit n of piEnabled off greys choice n - but
    /// SIZED BY THEIR TEXT (UWRowSizing.SegmentWidths): the even shares while every text fits its
    /// share, else each choice its text and padding and the spare shared out. Bit n of piTextOnly draws
    /// choice n as a plain text in its share (cleanup 8d.3, 2026-10-25: the Graphics window's Custom,
    /// per user): the accent colour while current, the dim text's otherwise; it takes no press and is
    /// no entry of the arrow keys.
    /// </summary>
    public int Choices(Rect pOArea, string[] psChoices, int piCurrent, float pfGap = 0f, bool pbFaced = false, int piEnabled = -1,
        int piTextOnly = 0)
    {
        int liCount = psChoices.Length;
        float[] laTexts = new float[liCount];
        float[] laWidths = new float[liCount];

        for (int liAt = 0; liAt < liCount; liAt++)
            laTexts[liAt] = fTextWidth(psChoices[liAt], TextSize) + pfGap;

        UWRowSizing.SegmentWidths(pOArea.width, laTexts, laWidths);

        bool lbEnabled = mbEnabled;
        float lfX = pOArea.x;
        int liPressed = -1;

        for (int liAt = 0; liAt < liCount; liAt++)
        {
            if (piEnabled != -1)
                mbEnabled = lbEnabled && (piEnabled & (1 << liAt)) != 0;

            Rect lORect = new Rect(lfX, pOArea.y, laWidths[liAt] - pfGap, pOArea.height);

            if ((piTextOnly & (1 << liAt)) != 0)
            {
                fText(lORect, psChoices[liAt], fLook(TextSize, liAt == piCurrent ? mOPalette.Accent : mOPalette.DimText, TextAnchor.MiddleLeft,
                    UWGuiTheme.SetupListPadding));
                mbEnabled = lbEnabled;
                lfX += laWidths[liAt];
                continue;
            }

            bool lbPressed = liAt == piCurrent
                ? fListRow(lORect, psChoices[liAt], mOPalette.Accent, TextAnchor.MiddleLeft, UWGuiTheme.SetupListPadding)
                : pbFaced
                    ? fFaced(lORect, psChoices[liAt], mOPalette.Text)
                    : fListRow(lORect, psChoices[liAt], mOPalette.Text, TextAnchor.MiddleLeft, UWGuiTheme.SetupListPadding);

            if (lbPressed)
                liPressed = liAt;

            mbEnabled = lbEnabled;
            lfX += laWidths[liAt];
        }

        return liPressed;
    }

    public int ChoiceRow(Rect pORow, in UWChoiceColumns pOColumns, string psLabel, string[] psChoices, int piCurrent, int piTextOnly = 0)
    {
        fText(new Rect(pORow.x + pOColumns.LabelLeft, pORow.y, pOColumns.LabelWidth, pORow.height), psLabel, RowLook);

        return Choices(new Rect(pORow.x + pOColumns.ChoicesLeft, pORow.y, pORow.width - pOColumns.ChoicesLess, pORow.height),
            psChoices, piCurrent, 0f, false, -1, piTextOnly);
    }

    /// <summary>A slider row as UWGuiRows.Slider, the same columns, snapping, arrows, clamp and value.
    /// The slider is an entry of the arrow keys (cleanup 8d.4): with the focus on it Left and Right move
    /// the value by the row's step (UWKeyNavRules.SliderKeyValue), returned as a drag or the arrows
    /// return theirs, so the caller writes it, saves it and runs its OnChange the same way.</summary>
    public float Slider(Rect pORow, in UWSliderColumns pOColumns, string psLabel, float pfValue, float pfMin, float pfMax,
        float pfStep, bool pbClamp, Func<float, string> pFFormat)
    {
        if (psLabel != null)
            fText(new Rect(pORow.x + pOColumns.LabelLeft, pORow.y, pOColumns.LabelWidth, pORow.height), psLabel, RowLook);

        float lfResult = fSliderBar(new Rect(pORow.x + pOColumns.SliderLeft, pORow.y + SliderDrop, pORow.width - pOColumns.SliderLess, SliderHeight),
            pfValue, pfMin, pfMax, pORow, out int liSlide);

        if (pfStep > 0f)
            lfResult = Mathf.Round(lfResult / pfStep) * pfStep;

        if (pOColumns.ArrowsAt > 0f)
        {
            int liStep = 0;

            if (Action(new Rect(pORow.x + pORow.width - pOColumns.ArrowsAt, pORow.y, ArrowSize, ArrowSize), "<"))
                liStep = -1;

            if (Action(new Rect(pORow.x + pORow.width - (pOColumns.ArrowsAt - ArrowPitch), pORow.y, ArrowSize, ArrowSize), ">"))
                liStep = 1;

            if (liStep != 0)
                lfResult = liStep < 0 ? pfValue - pfStep : pfValue + pfStep;
        }

        // Left or Right on the focused slider: one step from the value it had, on the step's grid,
        // within its ends (a greyed slider takes no key).
        if (liSlide != 0 && mbEnabled)
            lfResult = UWKeyNavRules.SliderKeyValue(pfValue, liSlide, pfStep, pfMin, pfMax);

        if (pbClamp)
            lfResult = Mathf.Clamp(lfResult, pfMin, pfMax);

        fText(new Rect(pORow.x + pORow.width - pOColumns.ValueAt, pORow.y, pOColumns.ValueWidth, pORow.height), pFFormat(lfResult), RowLook);

        return lfResult;
    }

    /// <summary>The slider itself: the groove and the thumb; held, the thumb follows the pointer
    /// (a press on the groove takes the thumb there, as IMGUI's). Returns the value it stands at;
    /// piSlide: -1 or 1 when Left or Right moves it this frame (an entry of the arrow keys since
    /// cleanup 8d.4, framed while it has the focus; pORow is what a scrolled area shows of it).</summary>
    private float fSliderBar(Rect pORect, float pfValue, float pfMin, float pfMax, Rect pORow, out int piSlide)
    {
        int liId = fControl(pORect, out bool lbOver, out bool _, out bool lbHolding);

        piSlide = KeyNav.SliderEntry(out bool lbFocused);

        if (lbFocused)
            fFocusDrawn(pORow);

        float lfRange = pfMax - pfMin;
        float lfTravel = Mathf.Max(1f, pORect.width - ThumbWidth);
        float lfValue = pfValue;

        if (lbHolding && mbEnabled && Mathf.Abs(lfRange) > 0f)
        {
            float lfAt = Mathf.Clamp01((PointerHere.x - pORect.x - (ThumbWidth / 2f)) / lfTravel);

            lfValue = pfMin + (lfAt * lfRange);
        }

        float lfShown = Mathf.Abs(lfRange) > 0f ? Mathf.Clamp01((lfValue - pfMin) / lfRange) : 0f;
        float lfCentre = pORect.y + GrooveCentre;

        fFill(new Rect(pORect.x, lfCentre - (GrooveHeight / 2f), pORect.width, GrooveHeight), mOPalette.Separator);
        fFill(new Rect(pORect.x + (lfShown * lfTravel), lfCentre - (ThumbHeight / 2f), ThumbWidth, ThumbHeight),
            lbHolding || fHover(liId, lbOver) ? mOPalette.Text : mOPalette.Thumb);

        if (lbFocused)
            fFocusFrame(pORect);

        return lfValue;
    }

    public int Stepper(Rect pORow, float pfLabelWidth, float pfButtonWidth, string psLabel, string psValue, bool pbCanDown, bool pbCanUp)
    {
        float lfControl = pORow.width - pfLabelWidth;
        float lfLeft = pORow.x + pfLabelWidth;
        int liStep = 0;

        fText(new Rect(pORow.x, pORow.y, pfLabelWidth, pORow.height), psLabel, RowLook);

        mbEnabled = pbCanDown;

        if (Action(new Rect(lfLeft, pORow.y, pfButtonWidth, pORow.height), "-"))
            liStep = -1;

        mbEnabled = pbCanUp;

        if (Action(new Rect(lfLeft + lfControl - pfButtonWidth, pORow.y, pfButtonWidth, pORow.height), "+"))
            liStep = 1;

        mbEnabled = true;

        fText(new Rect(lfLeft + pfButtonWidth, pORow.y, lfControl - (2f * pfButtonWidth), pORow.height), psValue, ValueLook);

        return liStep;
    }

    /// <summary>A rebinding row as UWGuiRows.Rebind; the key's button as wide as its text needs
    /// (cleanup 8b.2, 2026-10-24: "Press a key or button..." in the pseudo-language), growing to the
    /// left with its right edge kept, the glyph and the label's room moving along.</summary>
    public bool Rebind(Rect pORow, string psLabel, string psKey, Texture2D pOGlyph, bool pbPad)
    {
        float lfExtra = FitWidth(psKey, 160f) - 160f;

        if (!pbPad)
        {
            fText(new Rect(pORow.x + 6f, pORow.y, pORow.width - 180f - lfExtra, 26f), psLabel, RowLook);

            return Action(new Rect(pORow.x + pORow.width - 170f - lfExtra, pORow.y, 160f + lfExtra, 26f), psKey);
        }

        fText(new Rect(pORow.x + 6f, pORow.y + 4f, pORow.width - 220f - lfExtra, 26f), psLabel, RowLook);

        if (pOGlyph != null)
            Picture(new Rect(pORow.x + pORow.width - 206f - lfExtra, pORow.y, 32f, 32f), pOGlyph);

        return Action(new Rect(pORow.x + pORow.width - 170f - lfExtra, pORow.y + 3f, 160f + lfExtra, 26f), psKey);
    }

    // ------------------------------------------------- IUWRows: the layout editor's rows (cleanup 8b.4)

    /// <summary>A segment of the editor's choice rows: the one in force on the accent with dark text
    /// (UWGuiTheme.LayoutEditorStyles.Segment's "on" faces), the others faced buttons. True when
    /// pressed, the one in force too.</summary>
    private bool fSegment(Rect pORect, string psText, bool pbOn)
    {
        if (!pbOn)
            return fFaced(pORect, psText, mOPalette.Text);

        fControl(pORect, out bool _, out bool lbClicked, out bool _);
        fText(pORect, psText, fLook(TextSize, mOPalette.Background, TextAnchor.MiddleCenter), true, mOPalette.Accent);

        return lbClicked;
    }

    /// <summary>
    /// Named segments in a row (UWGuiRows.Segments, the editor's Preset row), the chosen one lit: each
    /// as wide as its name and the rest shared out evenly (UWRowSizing.SpreadWidths) - the IMGUI form's
    /// widths while they fit; where they do not, the row runs over instead of squeezing the names (the
    /// window widens beforehand, UWRowSizing.SpreadNeeded). Returns the chosen one, a newly pressed one
    /// if any.
    /// </summary>
    public int Segments(Rect pORect, int piChosen, string[] psNames)
    {
        float[] laTexts = new float[psNames.Length];
        float[] laWidths = new float[psNames.Length];

        for (int liAt = 0; liAt < psNames.Length; liAt++)
            laTexts[liAt] = fTextWidth(psNames[liAt], TextSize);

        UWRowSizing.SpreadWidths(pORect.width, laTexts, laWidths);

        float lfX = pORect.x;
        int liChosen = piChosen;

        for (int liAt = 0; liAt < psNames.Length; liAt++)
        {
            if (fSegment(new Rect(lfX, pORect.y, laWidths[liAt], pORect.height), psNames[liAt], liAt == piChosen) && liAt != piChosen)
                liChosen = liAt;

            lfX += laWidths[liAt];
        }

        return liChosen;
    }

    /// <summary>Segments sharing the rectangle (UWGuiRows.Toolbar, IMGUI's GUI.Toolbar: the editor's
    /// choice rows), the current one lit - SIZED BY THEIR TEXT as the setup windows' choices
    /// (UWRowSizing.SegmentWidths: the even shares while every name fits its share). Returns the one
    /// chosen now, the current one when none was pressed.</summary>
    public int Toolbar(Rect pORect, int piCurrent, string[] psChoices)
    {
        int liCount = psChoices.Length;
        float[] laTexts = new float[liCount];
        float[] laWidths = new float[liCount];

        for (int liAt = 0; liAt < liCount; liAt++)
            laTexts[liAt] = fTextWidth(psChoices[liAt], TextSize);

        UWRowSizing.SegmentWidths(pORect.width, laTexts, laWidths);

        float lfX = pORect.x;
        int liChosen = piCurrent;

        for (int liAt = 0; liAt < liCount; liAt++)
        {
            if (fSegment(new Rect(lfX, pORect.y, laWidths[liAt], pORect.height), psChoices[liAt], liAt == piCurrent))
                liChosen = liAt;

            lfX += laWidths[liAt];
        }

        return liChosen;
    }

    /// <summary>
    /// Choices shown as samples (UWGuiRows.Samples; per user 2026-10-09: each choice a sample of
    /// itself): each picture in a frame, the chosen one framed 2 wide in the accent colour, the others
    /// 1 wide in the separator's; all at a third of their strength while pbEnabled is off (IMGUI's
    /// GUI.color 0.35). A click on another one than the chosen one, enabled, returns it; else -1.
    /// </summary>
    public int Samples(float pfX, float pfY, float pfEach, float pfHeight, Texture2D[] paPictures, int piChosen, bool pbEnabled)
    {
        const float Gap = 3f;
        Color lOShade = pbEnabled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        int liPressed = -1;

        for (int liAt = 0; liAt < paPictures.Length; liAt++)
        {
            Rect lOCell = new Rect(pfX + (liAt * pfEach) + Gap, pfY + 2f, pfEach - (2f * Gap), pfHeight - 4f);
            bool lbChosen = liAt == piChosen;
            float lfFrame = lbChosen ? 2f : 1f;
            Slot lOFrame = fSlot(lOCell);

            fSetFill(lOFrame, true, (lbChosen && pbEnabled ? mOPalette.Accent : mOPalette.Separator) * lOShade);
            fSetPicture(lOFrame, null, Color.white);
            fSetLabel(lOFrame, null, default);

            if (paPictures[liAt] != null)
            {
                Slot lOPicture = fSlot(new Rect(lOCell.x + lfFrame, lOCell.y + lfFrame, lOCell.width - (2f * lfFrame), lOCell.height - (2f * lfFrame)));

                fSetFill(lOPicture, false, Color.clear);
                fSetPicture(lOPicture, paPictures[liAt], lOShade);
                fSetLabel(lOPicture, null, default);
            }

            fControl(lOCell, out bool _, out bool lbClicked, out bool _);

            if (lbClicked && pbEnabled && !lbChosen)
                liPressed = liAt;
        }

        return liPressed;
    }

    /// <summary>Starts a window's inside (the layout editor's window and its save question; IMGUI's
    /// GUI.Window until cleanup 8b.4): what is drawn until EndWindow is in the window's own coordinates,
    /// its top-left corner at 0,0, and cut at its edges. The window's ground is the caller's (Frame).</summary>
    public void BeginWindow(Rect pOWindow)
    {
        fBeginClip(pOWindow, Vector2.zero, Vector2.zero);
    }

    public void EndWindow()
    {
        fEndClip();
    }

    // ------------------------------------------------- IUWRows: the bar and the first-run screen

    /// <summary>The setup bar's ground (Ground: in Plain the bar's colour, as always).</summary>
    public void Bar(Rect pORect)
    {
        Ground(pORect, UWWindowGroundEnum.Bar);
    }

    /// <summary>An item of the bar, lit while its window is open and under the pointer.</summary>
    public bool BarItem(Rect pORect, string psText, bool pbOpen)
    {
        if (pbOpen)
            fFill(pORect, mOPalette.Hover);

        return fListRow(pORect, psText, mOPalette.Text, TextAnchor.MiddleCenter, 0f);
    }

    public void Backdrop(Rect pORect)
    {
        fFill(pORect, mOPalette.Background);
    }

    public void BigTitle(Rect pORect, string psText)
    {
        fText(pORect, psText, fLook(UWGuiTheme.SetupBigTitleSize, mOPalette.Accent, TextAnchor.MiddleCenter));
    }

    // ------------------------------------------------- IUWRows: typing

    /// <summary>The text field that has the keyboard (its name), null for none.</summary>
    private string msFocusField;

    private readonly UWTextLine mOLine = new UWTextLine();

    private readonly UWTextEntry mOEntry;

    /// <summary>The text the focused field gave back last; another one from its owner starts the
    /// line anew (as IMGUI's field takes a changed text).</summary>
    private string msLastGiven;

    private float mfFocusedAt;

    private bool mbEnter;

    private bool mbEscape;

    private bool mbFieldDrawn;

    private bool mbFieldHit;

    /// <summary>The kit drawer whose text field has the keyboard.</summary>
    private static UWKitRows msOTyping;

    /// <summary>Text is typed into a field of the kit: the game's keys stay silent
    /// (UWTextEntryState.IsTextEntryActive), the arrows move the cursor, not the focus.</summary>
    public static bool IsTyping => msOTyping != null;

    private void fOnCharacter(char pcChar)
    {
        if (msFocusField != null)
            mOLine.Insert(pcChar);
    }

    /// <summary>The keys of the field with the keyboard, once a frame before drawing (the text events
    /// arrive by themselves, fOnCharacter).</summary>
    private void fReadTyping()
    {
        mbEnter = false;
        mbEscape = false;
        msEscapedField = null;

        if (msFocusField == null)
            return;

        Keyboard lOKeyboard = Keyboard.current;

        if (UWTextEntry.BackspacePressed)
            mOLine.Backspace();

        if (lOKeyboard != null)
        {
            if (lOKeyboard.deleteKey.wasPressedThisFrame)
                mOLine.Delete();

            if (lOKeyboard.leftArrowKey.wasPressedThisFrame)
                mOLine.Left();

            if (lOKeyboard.rightArrowKey.wasPressedThisFrame)
                mOLine.Right();

            if (lOKeyboard.homeKey.wasPressedThisFrame)
                mOLine.Home();

            if (lOKeyboard.endKey.wasPressedThisFrame)
                mOLine.End();

            fPaste(lOKeyboard);
        }

        // Where no text events come (Linux), the keys themselves - after Backspace (UWTextEntry).
        mOEntry.ReadKeys();

        mbEnter = UWTextEntry.EnterPressed;
        mbEscape = UWBack.Takes(UWBackOwner.KitText);

        // Escape lets go of the field (its owner reads it this frame through EscapeIn).
        if (mbEscape)
        {
            msEscapedField = msFocusField;
            fLetGoOfField();
        }
    }

    private void fTakeField(string psName, string psText, int piMaxLength)
    {
        msFocusField = psName;
        mOLine.AnyText = TypesAnyText;
        mOLine.Set(psText, piMaxLength);
        msLastGiven = mOLine.Text;
        mfFocusedAt = Time.unscaledTime;
        mOEntry.Begin();
        msOTyping = this;
    }

    private void fLetGoOfField()
    {
        msFocusField = null;
        mOEntry.End();

        if (msOTyping == this)
            msOTyping = null;
    }

    /// <summary>The field Escape let go of this frame, for EscapeIn.</summary>
    private string msEscapedField;

    /// <summary>
    /// A line to type in (UWGuiRows.TextField on the kit): a press on it gives it the keyboard, a press
    /// beside it or Escape takes it away; while it has it the characters typed go in at the cursor (one
    /// filter, at most piMaxLength when above 0), Backspace and Delete take one away, the arrows, Home
    /// and End move the cursor, which blinks at the message log's rate. Returns its text.
    /// </summary>
    public string TextField(Rect pORect, string psName, string psText, int piMaxLength = 0)
    {
        int liId = fControl(pORect, out bool lbOver, out bool _, out bool _);

        if (mbEnabled && mbDown && lbOver && miActive == liId)
        {
            mbFieldHit = true;

            if (msFocusField != psName)
                fTakeField(psName, psText, piMaxLength);
        }

        bool lbFocused = msFocusField == psName;

        if (lbFocused)
        {
            mbFieldDrawn = true;

            if (psText != msLastGiven)
                mOLine.Set(psText, piMaxLength);
        }

        string lsText = lbFocused ? mOLine.Text : (psText ?? string.Empty);
        float lfPadding = UWGuiTheme.SetupFieldPadding;
        float lfInner = Mathf.Max(0f, pORect.width - (2f * lfPadding));
        float lfCaretX = lbFocused ? fTextWidth(lsText.Substring(0, mOLine.Caret), TextSize) : 0f;
        float lfTextWidth = fTextWidth(lsText, TextSize);
        float lfScroll = lbFocused ? Mathf.Max(0f, lfCaretX + 2f - lfInner) : 0f;

        fFill(pORect, mOPalette.Background);
        Rect lOView = new Rect(pORect.x + lfPadding, pORect.y, lfInner, pORect.height);

        fBeginClip(lOView, lOView.position, new Vector2(lfScroll, 0f));
        fText(new Rect(pORect.x + lfPadding, pORect.y, Mathf.Max(lfInner, lfTextWidth + 4f), pORect.height), lsText, RowLook);

        bool lbBlinkOn = ((Time.unscaledTime - mfFocusedAt) % (2f * UWMessageLogModel.CursorBlinkSeconds)) < UWMessageLogModel.CursorBlinkSeconds;

        if (lbFocused && lbBlinkOn)
            fFill(new Rect(pORect.x + lfPadding + lfCaretX, pORect.y + 4f, 1.5f, pORect.height - 8f), mOPalette.Text);

        fEndClip();

        if (lbFocused)
            msLastGiven = lsText;

        return lsText;
    }

    /// <summary>Gives the text field named psName the keyboard before it is drawn, its text psText and
    /// the cursor at its end (IMGUI's GUI.FocusControl; cleanup 8b.4: the save name, typed at once).</summary>
    public void FocusField(string psName, string psText, int piMaxLength = 0)
    {
        if (mbDrawing && msFocusField != psName)
            fTakeField(psName, psText ?? string.Empty, piMaxLength);
    }

    /// <summary>The text field named psName has the keyboard.</summary>
    public bool FieldHasKeys(string psName)
    {
        return msFocusField == psName;
    }

    public bool EnterIn(string psName)
    {
        if (!mbEnter || msFocusField != psName)
            return false;

        mbEnter = false;

        return true;
    }

    public bool EscapeIn(string psName)
    {
        if (!mbEscape || msEscapedField != psName)
            return false;

        mbEscape = false;
        msEscapedField = null;

        return true;
    }

    // ------------------------------------------------- The switch's check

    private static Texture2D msOCheck;

    /// <summary>A check mark for the switch's box, white on clear, tinted when drawn (made once).</summary>
    private static Texture2D CheckTexture
    {
        get
        {
            if (msOCheck != null)
                return msOCheck;

            const int Size = 24;

            msOCheck = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "UWKitRows check", filterMode = FilterMode.Bilinear };

            Vector2 lOA = new Vector2(5f, 12f);
            Vector2 lOB = new Vector2(10f, 6.5f);
            Vector2 lOC = new Vector2(19f, 18f);

            for (int liY = 0; liY < Size; liY++)
            {
                for (int liX = 0; liX < Size; liX++)
                {
                    Vector2 lOAt = new Vector2(liX + 0.5f, liY + 0.5f);
                    float lfDistance = Mathf.Min(fDistance(lOAt, lOA, lOB), fDistance(lOAt, lOB, lOC));
                    float lfAlpha = Mathf.Clamp01(2.4f - lfDistance);

                    msOCheck.SetPixel(liX, liY, new Color(1f, 1f, 1f, lfAlpha));
                }
            }

            msOCheck.Apply();

            return msOCheck;
        }
    }

    private static float fDistance(Vector2 pOPoint, Vector2 pOA, Vector2 pOB)
    {
        Vector2 lOLine = pOB - pOA;
        float lfAt = Mathf.Clamp01(Vector2.Dot(pOPoint - pOA, lOLine) / lOLine.sqrMagnitude);

        return Vector2.Distance(pOPoint, pOA + (lOLine * lfAt));
    }

    // ------------------------------------------------- The folder path's typing (cleanup 8b.2)

    /// <summary>
    /// THE TEXT FIELDS OF THIS DRAWER TAKE ANY TEXT (cleanup 8b.2, 2026-10-24): every character that is
    /// no control code, not only ASCII (UWTextLine.AnyText) - the setup bar's one field is the folder
    /// path, and a path may hold "C:\Users\Jürgen", as IMGUI's field took it. A drawer whose fields
    /// feed the original's font (a save name) keeps the one filter (off, the default).
    /// </summary>
    public bool TypesAnyText { get; set; }

    /// <summary>
    /// PASTING into the field with the keyboard (cleanup 8b.2, 2026-10-24): Ctrl+V or Shift+Insert puts
    /// the clipboard's text in at the cursor, through the line's filter and limit, as IMGUI's field
    /// did - the folder path is "typed or pasted". Ctrl combinations are no text (UWTypedKeys), so the
    /// V itself does not go in.
    /// </summary>
    private void fPaste(Keyboard pOKeyboard)
    {
        bool lbPaste = (pOKeyboard.ctrlKey.isPressed && pOKeyboard.vKey.wasPressedThisFrame)
            || (pOKeyboard.shiftKey.isPressed && pOKeyboard.insertKey.wasPressedThisFrame);

        if (lbPaste)
            mOLine.InsertText(GUIUtility.systemCopyBuffer);
    }

    // ================================================= The help's pieces (cleanup 8b.3, 2026-10-24)
    //
    // THE HELP WINDOW ON THE KIT (UWHelpWindow and its seven tabs, laid out by UWHelpKitFlow): what the
    // setup rows did not need - texts of the help's own family (UWGuiTheme.Help: rich text, bold, sizes
    // 13 to 28), buttons with the help's faces (the tabs, the fold-outs, the manual's arrows), a PRESS
    // for the spell rows and the manual's pages (IMGUI's MouseDown there, not the release), the wheel
    // over a piece, a clip that keeps the callers' coordinates (the help behind the sliding classic
    // frame), the canvas fading with the help's slide, the pointer taken away under the setup bar, and
    // the notes' MULTI-LINE TEXT AREA on UWTextEntry and UWTypedKeys. A region of its own, so the
    // rows above stay as 8b.1 left them.

    /// <summary>How a help text is drawn: UWGuiTheme.Help's sizes and colours as values.</summary>
    public struct HelpLook
    {
        public int Size;

        public Color Colour;

        public TextAnchor Anchor;

        /// <summary>Breaks lines at the piece's width (IMGUI's wordWrap).</summary>
        public bool Wraps;

        public bool Bold;

        /// <summary>The tags of the help's texts (bold, italics, the accent colour) are read, as IMGUI's
        /// richText styles did; the notes' text is shown as typed.</summary>
        public bool Rich;
    }

    private readonly Dictionary<(string, int, bool, bool), float> mOHelpWidths = new Dictionary<(string, int, bool, bool), float>();

    private readonly Dictionary<(string, int, bool, bool, int), float> mOHelpHeights = new Dictionary<(string, int, bool, bool, int), float>();

    /// <summary>The scale and the language the help's measures were taken at.</summary>
    private float mfHelpScale = -1f;

    private int miHelpLanguage = -1;

    /// <summary>The live values (experience, the vitals) make new texts all the time: the measures
    /// start afresh beyond this many.</summary>
    private const int HelpMeasuresKept = 4096;

    private CanvasGroup mOGroup;

    /// <summary>The whole canvas at this opacity (the help fades with its slide, as IMGUI's GUI.color
    /// did); call after Begin.</summary>
    public void SetAlpha(float pfAlpha)
    {
        if (mOCanvas == null)
            return;

        if (mOGroup == null)
        {
            mOGroup = mOCanvas.gameObject.AddComponent<CanvasGroup>();
            mOGroup.interactable = false;
            mOGroup.blocksRaycasts = false;
        }

        if (mOGroup.alpha != pfAlpha)
            mOGroup.alpha = pfAlpha;
    }

    /// <summary>No pointer for this frame's pieces: nothing lights, takes a press or turns with the
    /// wheel (the help under the setup bar's open windows, whose clicks are theirs). Call after Begin.</summary>
    public void DropPointer()
    {
        mbPointer = false;
        mbDown = false;
        mbUp = false;
        mbHeld = false;
        mfWheel = 0f;
    }

    /// <summary>Starts a clip at pOView: what is drawn until EndClipArea is cut at its edges, in the
    /// same coordinates as outside it.</summary>
    public void BeginClipArea(Rect pOView)
    {
        fBeginClip(pOView, pOView.position, Vector2.zero);
    }

    public void EndClipArea()
    {
        fEndClip();
    }

    /// <summary>A plain colour over the rectangle.</summary>
    public void Fill(Rect pORect, Color pOColour)
    {
        fFill(pORect, pOColour);
    }

    /// <summary>The room a scrolled area's bar takes on its right (BeginScroll).</summary>
    public const float ScrollBarRoom = ScrollBarWidth;

    /// <summary>A key this frame, its press and then its repeat while it stays held (the reading
    /// mode's page keys; the text area's keys go the same way).</summary>
    public bool KeyWithRepeat(Key peKey)
    {
        Keyboard lOKeyboard = Keyboard.current;

        return lOKeyboard != null && fAreaKey(lOKeyboard, peKey);
    }

    private void fCheckHelpMeasures()
    {
        if (mfHelpScale != mfScale || miHelpLanguage != UWText.LanguageVersion
            || mOHelpWidths.Count > HelpMeasuresKept || mOHelpHeights.Count > HelpMeasuresKept)
        {
            mfHelpScale = mfScale;
            miHelpLanguage = UWText.LanguageVersion;
            mOHelpWidths.Clear();
            mOHelpHeights.Clear();
        }
    }

    private TextGenerationSettings fHelpSettings(in HelpLook pOLook, float pfWrapWidth)
    {
        TextGenerationSettings lOSettings = fSettings(pOLook.Size, pfWrapWidth);

        lOSettings.fontStyle = pOLook.Bold ? FontStyle.Bold : FontStyle.Normal;
        lOSettings.richText = pOLook.Rich;

        return lOSettings;
    }

    /// <summary>A help text's width on one line, in layout units.</summary>
    public float HelpTextWidth(string psText, in HelpLook pOLook)
    {
        if (string.IsNullOrEmpty(psText))
            return 0f;

        fCheckHelpMeasures();

        var lOKey = (psText, pOLook.Size, pOLook.Bold, pOLook.Rich);

        if (mOHelpWidths.TryGetValue(lOKey, out float lfWidth))
            return lfWidth;

        lfWidth = mOGenerator.GetPreferredWidth(psText, fHelpSettings(pOLook, 0f)) / mfScale;
        mOHelpWidths[lOKey] = lfWidth;

        return lfWidth;
    }

    /// <summary>A help text's height, wrapped at pfWidth when its look wraps, in layout units - what
    /// IMGUI's CalcHeight gave GUILayout.</summary>
    public float HelpTextHeight(string psText, in HelpLook pOLook, float pfWidth)
    {
        string lsText = string.IsNullOrEmpty(psText) ? " " : psText;

        fCheckHelpMeasures();

        int liWidth = pOLook.Wraps ? Mathf.RoundToInt(pfWidth) : 0;
        var lOKey = (lsText, pOLook.Size, pOLook.Bold, pOLook.Rich, liWidth);

        if (mOHelpHeights.TryGetValue(lOKey, out float lfHeight))
            return lfHeight;

        lfHeight = mOGenerator.GetPreferredHeight(lsText, fHelpSettings(pOLook, pOLook.Wraps ? Mathf.Max(1f, pfWidth) : 0f)) / mfScale;
        mOHelpHeights[lOKey] = lfHeight;

        return lfHeight;
    }

    /// <summary>A help text over the rectangle.</summary>
    public void HelpText(Rect pORect, string psText, in HelpLook pOLook)
    {
        Slot lOSlot = fSlot(pORect);

        fSetFill(lOSlot, false, Color.clear);
        fSetPicture(lOSlot, null, Color.white);
        fSetHelpLabel(lOSlot, psText, pOLook);
    }

    /// <summary>The help's text settings on top of fSetLabel's: the tags and the bold face, set on
    /// every help piece, since the pool hands a piece to whatever is drawn next.</summary>
    private void fSetHelpLabel(Slot pOSlot, string psText, in HelpLook pOLook)
    {
        fSetLabel(pOSlot, psText, fLook(pOLook.Size, pOLook.Colour, pOLook.Anchor, 0f, pOLook.Wraps));

        Text lOText = pOSlot.Label;

        if (lOText == null)
            return;

        if (lOText.supportRichText != pOLook.Rich)
            lOText.supportRichText = pOLook.Rich;

        FontStyle leStyle = pOLook.Bold ? FontStyle.Bold : FontStyle.Normal;

        if (lOText.fontStyle != leStyle)
            lOText.fontStyle = leStyle;
    }

    /// <summary>A button of the help on a face of its own, lighter under the pointer with its text in
    /// pOHoverText (the tabs, the fold-outs, the manual's arrows - IMGUI's Tab style and its copies);
    /// it counts on the release over it, as UWImguiInput.LayoutButton.</summary>
    public bool HelpButton(Rect pORect, string psText, in HelpLook pOLook, Color pOGround, Color pOHoverGround, Color pOHoverText)
    {
        int liId = fControl(pORect, out bool lbOver, out bool lbClicked, out bool lbHolding);
        bool lbLit = fHover(liId, lbOver) || (lbHolding && lbOver);
        Slot lOSlot = fSlot(pORect);
        HelpLook lOLook = pOLook;

        if (lbLit)
            lOLook.Colour = pOHoverText;

        fSetFill(lOSlot, true, fTint(lbLit ? pOHoverGround : pOGround));
        fSetPicture(lOSlot, null, Color.white);
        fSetHelpLabel(lOSlot, psText, lOLook);

        return lbClicked;
    }

    /// <summary>
    /// A BUTTON OF THE MODERN GAME MENU (cleanup 8b.4, 2026-10-24; UWGuiTheme.GameMenuStyles.Button): a
    /// help button (its ground, lighter under the pointer and while held, its text in pOHoverText then)
    /// that is also an entry of the arrow keys' navigation (the gold frame, Enter or Space presses it),
    /// with an optional picture before its text, the two centred together as IMGUI drew a GUIContent's
    /// image left of its text (the right mouse button's glyph). True on the release over it or a key.
    /// </summary>
    public bool MenuButton(Rect pORect, string psText, Texture2D pOPicture, in HelpLook pOLook, Color pOGround, Color pOHoverGround,
        Color pOHoverText)
    {
        int liId = fControl(pORect, out bool lbOver, out bool lbClicked, out bool lbHolding);
        bool lbKey = fEntry(pORect, out bool lbFocused);
        bool lbLit = fHover(liId, lbOver) || (lbHolding && lbOver);
        HelpLook lOLook = pOLook;
        Slot lOSlot = fSlot(pORect);

        if (lbLit)
            lOLook.Colour = pOHoverText;

        fSetFill(lOSlot, true, fTint(lbLit ? pOHoverGround : pOGround));
        fSetPicture(lOSlot, null, Color.white);

        if (pOPicture == null)
            fSetHelpLabel(lOSlot, psText, lOLook);
        else
        {
            fSetHelpLabel(lOSlot, null, lOLook);

            // The picture at its own size (shrunk to the button's height), the text right after it.
            float lfPictureHeight = Mathf.Min(pOPicture.height, pORect.height);
            float lfPictureWidth = pOPicture.width * lfPictureHeight / Mathf.Max(1, pOPicture.height);
            float lfTextWidth = HelpTextWidth(psText, lOLook);
            float lfLeft = pORect.center.x - ((lfPictureWidth + lfTextWidth) / 2f);

            Picture(new Rect(lfLeft, pORect.center.y - (lfPictureHeight / 2f), lfPictureWidth, lfPictureHeight), pOPicture);

            lOLook.Anchor = TextAnchor.MiddleLeft;
            HelpText(new Rect(lfLeft + lfPictureWidth, pORect.y, lfTextWidth + 2f, pORect.height), psText, lOLook);
        }

        // A carved theme's button is cut from the stone, as the setup windows' (fFaced).
        if (mOPalette.IsCarved)
            fEdges(pORect, 1f, lbHolding && lbOver);

        if (lbFocused)
            fFocusFrame(pORect);

        return lbClicked || lbKey;
    }

    /// <summary>A press of the left button on the piece this frame (the spell rows, the manual's
    /// page, the reading mode's pages): IMGUI's MouseDown there (UWImguiInput.PressedIn), not the
    /// release; the first piece under the pointer takes it.</summary>
    public bool PressedIn(Rect pORect)
    {
        int liId = fControl(pORect, out bool lbOver, out bool _, out bool _);

        return mbEnabled && mbDown && lbOver && miActive == liId;
    }

    /// <summary>The wheel over the piece this frame (positive up, as the Input System gives it), 0
    /// elsewhere or greyed; taken, so nothing drawn after it turns with it.</summary>
    public float WheelIn(Rect pORect)
    {
        Layer lOLayer = mOLayers[miLayer];
        bool lbOver = PointerOn && lOLayer.Clip.Contains(mOPointer)
            && new Rect(pORect.position + lOLayer.ToRoot, pORect.size).Contains(mOPointer);

        if (!mbEnabled || !lbOver || Mathf.Abs(mfWheel) < 0.01f)
            return 0f;

        float lfWheel = mfWheel;

        mfWheel = 0f;

        return lfWheel;
    }

    // ------------------------------------------------- The help: the notes' text area

    /// <summary>A held key of the text area steps again after this (seconds), then every
    /// AreaRepeatEvery - the system's key repeat that IMGUI's TextArea had.</summary>
    private const float AreaRepeatDelay = 0.4f;

    private const float AreaRepeatEvery = 0.05f;

    private readonly UWTextArea mOArea = new UWTextArea();

    private UWTextEntry mOAreaEntry;

    /// <summary>The text area that has the keyboard (its name), null for none.</summary>
    private string msAreaFocus;

    /// <summary>The text the focused area gave back last; another one from its owner (a save loaded)
    /// starts the area anew.</summary>
    private string msAreaLastGiven;

    private float mfAreaFocusedAt;

    private bool mbAreaTab;

    /// <summary>The cursor moved or the text changed: the area scrolls to keep the cursor in view.</summary>
    private bool mbAreaCaretMoved;

    /// <summary>The held key of the area and the reading mode (a Key, None for none) and when it
    /// repeats (UWRepeat since cleanup step 7.3, 2026-10-25): the next repeat AreaRepeatEvery after
    /// the last one's due time, now where that is already past - so one more follows at once.</summary>
    private readonly UWRepeat mOAreaRepeat = new UWRepeat(AreaRepeatDelay, AreaRepeatEvery, UWRepeat.RuleEnum.AccumulateOrCatchUp);

    private readonly TextGenerator mOAreaGenerator = new TextGenerator();

    /// <summary>What the area's generator was last filled with.</summary>
    private string msAreaGenerated;

    private float mfAreaGeneratedWidth = -1f;

    private float mfAreaGeneratedScale = -1f;

    /// <summary>A text area has the keyboard.</summary>
    public bool AreaHasKeys => msAreaFocus != null;

    private void fOnAreaCharacter(char pcChar)
    {
        if (msAreaFocus != null && mOArea.Insert(pcChar))
            mbAreaCaretMoved = true;
    }

    private void fTakeArea(string psName, string psText)
    {
        if (mOAreaEntry == null)
            mOAreaEntry = new UWTextEntry(fOnAreaCharacter, UWTextArea.IsTypeable);

        msAreaFocus = psName;
        mOArea.Set(psText);
        msAreaLastGiven = mOArea.Text;
        mfAreaFocusedAt = Time.unscaledTime;
        mOAreaRepeat.Reset();
        mOAreaEntry.Begin();
        msOTyping = this;
    }

    /// <summary>The text area lets go of the keyboard (the help closes, another tab, the reading mode).</summary>
    public void LetGoOfArea()
    {
        if (msAreaFocus == null)
            return;

        msAreaFocus = null;

        if (mOAreaEntry != null)
            mOAreaEntry.End();

        if (msOTyping == this && msFocusField == null)
            msOTyping = null;
    }

    /// <summary>Tab went down in the text area named psName this frame (without Alt or Shift): the area
    /// typed no tab and let go; true once (the help's notes close the help with it).</summary>
    public bool TabIn(string psName)
    {
        if (!mbAreaTab || psName == null)
            return false;

        mbAreaTab = false;

        return true;
    }

    /// <summary>A key of the area this frame: its press, then its repeat while it stays held.</summary>
    private bool fAreaKey(Keyboard pOKeyboard, Key peKey)
    {
        UnityEngine.InputSystem.Controls.KeyControl lOKey = pOKeyboard[peKey];

        return mOAreaRepeat.Tick((int)peKey, lOKey.wasPressedThisFrame, lOKey.isPressed, Time.unscaledTime);
    }

    /// <summary>The keys that edit, once a frame before the text is laid out: the characters arrive by
    /// themselves (fOnAreaCharacter) or from the keys where no text events come (UWTypedKeys); returns
    /// the vertical step asked for (-1 up, 1 down) and whether Home or End went down.</summary>
    private void fReadAreaKeys(out int piVertical, out bool pbHome, out bool pbEnd)
    {
        piVertical = 0;
        pbHome = false;
        pbEnd = false;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return;

        // Tab closes the help while typing too - the game's keys are silent then; with Alt it is the
        // switch to another program and is left alone, with Shift as well (UWHelpNotesTab).
        if (lOKeyboard.tabKey.wasPressedThisFrame)
        {
            if (!lOKeyboard.altKey.isPressed && !lOKeyboard.shiftKey.isPressed)
            {
                mbAreaTab = true;
                LetGoOfArea();
            }

            return;
        }

        // Escape lets go of the area, as of the kit's line (TextField).
        if (UWBack.Takes(UWBackOwner.KitText))
        {
            LetGoOfArea();
            return;
        }

        bool lbChanged = false;

        if (fAreaKey(lOKeyboard, Key.Backspace))
            lbChanged |= mOArea.Backspace();

        if (fAreaKey(lOKeyboard, Key.Delete))
            lbChanged |= mOArea.Delete();

        if (fAreaKey(lOKeyboard, Key.Enter) || fAreaKey(lOKeyboard, Key.NumpadEnter))
        {
            mOArea.NewLine();
            lbChanged = true;
        }

        if (lOKeyboard.ctrlKey.isPressed && lOKeyboard.vKey.wasPressedThisFrame)
            lbChanged |= mOArea.Paste(GUIUtility.systemCopyBuffer) > 0;

        if (fAreaKey(lOKeyboard, Key.LeftArrow))
        {
            mOArea.Left();
            lbChanged = true;
        }

        if (fAreaKey(lOKeyboard, Key.RightArrow))
        {
            mOArea.Right();
            lbChanged = true;
        }

        if (fAreaKey(lOKeyboard, Key.UpArrow))
            piVertical = -1;

        if (fAreaKey(lOKeyboard, Key.DownArrow))
            piVertical = 1;

        pbHome = lOKeyboard.homeKey.wasPressedThisFrame;
        pbEnd = lOKeyboard.endKey.wasPressedThisFrame;

        // Where no text events come (Linux), the keys themselves - after Backspace (UWTextEntry).
        mOAreaEntry.ReadKeys();

        if (lbChanged || piVertical != 0 || pbHome || pbEnd)
            mbAreaCaretMoved = true;
    }

    /// <summary>The area's text laid out at pfWidth (layout units) in the generator, again only when
    /// the text, the width or the scale changed.</summary>
    private void fGenerateArea(string psText, in HelpLook pOLook, float pfWidth)
    {
        if (psText == msAreaGenerated && pfWidth == mfAreaGeneratedWidth && mfScale == mfAreaGeneratedScale)
            return;

        TextGenerationSettings lOSettings = fHelpSettings(pOLook, Mathf.Max(1f, pfWidth));

        lOSettings.textAnchor = TextAnchor.UpperLeft;
        lOSettings.pivot = new Vector2(0f, 1f);
        mOAreaGenerator.Populate(string.IsNullOrEmpty(psText) ? " " : psText, lOSettings);
        msAreaGenerated = psText;
        mfAreaGeneratedWidth = pfWidth;
        mfAreaGeneratedScale = mfScale;
    }

    /// <summary>The generated line the place piAt stands on.</summary>
    private int fAreaLineOf(int piAt)
    {
        IList<UILineInfo> lOLines = mOAreaGenerator.lines;
        int liLine = 0;

        for (int liAt = 1; liAt < lOLines.Count; liAt++)
        {
            if (lOLines[liAt].startCharIdx > piAt)
                break;

            liLine = liAt;
        }

        return liLine;
    }

    /// <summary>The first and the last place of a generated line: the last before the next line's
    /// first character (its line break or the space it wrapped at), the text's end on the last line.</summary>
    private void fAreaLineSpan(int piLine, int piLength, out int piFirst, out int piLast)
    {
        IList<UILineInfo> lOLines = mOAreaGenerator.lines;

        piFirst = Mathf.Clamp(lOLines[piLine].startCharIdx, 0, piLength);
        piLast = piLine + 1 < lOLines.Count ? Mathf.Clamp(lOLines[piLine + 1].startCharIdx - 1, piFirst, piLength) : piLength;
    }

    /// <summary>The cursor's x on its line, in layout units from the text's left edge.</summary>
    private float fAreaX(int piAt)
    {
        IList<UICharInfo> lOChars = mOAreaGenerator.characters;

        if (lOChars.Count == 0)
            return 0f;

        return lOChars[Mathf.Clamp(piAt, 0, lOChars.Count - 1)].cursorPos.x / mfScale;
    }

    /// <summary>The generated text's height: down to its last line's foot, in layout units.</summary>
    private float fAreaTextHeight()
    {
        int liLines = mOAreaGenerator.lines.Count;

        if (liLines == 0)
            return 0f;

        fAreaLineBox(liLines - 1, out float lfTop, out float lfHeight);

        return lfTop + lfHeight;
    }

    /// <summary>A line's top below the first line's and its height, in layout units.</summary>
    private void fAreaLineBox(int piLine, out float pfTop, out float pfHeight)
    {
        IList<UILineInfo> lOLines = mOAreaGenerator.lines;

        if (lOLines.Count == 0)
        {
            pfTop = 0f;
            pfHeight = 0f;
            return;
        }

        pfTop = (lOLines[0].topY - lOLines[piLine].topY) / mfScale;
        pfHeight = lOLines[piLine].height / mfScale;
    }

    /// <summary>The place on a line nearest to x (layout units).</summary>
    private int fAreaNearest(int piLine, float pfX, int piLength)
    {
        fAreaLineSpan(piLine, piLength, out int liFirst, out int liLast);

        int liBest = liFirst;
        float lfBest = float.MaxValue;

        for (int liAt = liFirst; liAt <= liLast; liAt++)
        {
            float lfDistance = Mathf.Abs(fAreaX(liAt) - pfX);

            if (lfDistance < lfBest)
            {
                lfBest = lfDistance;
                liBest = liAt;
            }
        }

        return liBest;
    }

    /// <summary>
    /// A TEXT AREA OF SEVERAL LINES (the help's notes; IMGUI's TextArea in a scroll view until 8b.3):
    /// on a dark ground over pOView, the text wrapped at its width with pfPadding round it, scrolled by
    /// the wheel and the bar on its right as BeginScroll. A press on it gives it the keyboard and puts
    /// the cursor where it pressed; a press beside it, Escape or Tab takes it away (TabIn tells the
    /// owner). While it has the keyboard the characters typed go in at the cursor (UWTextArea's filter,
    /// accents and umlauts too), Enter breaks the line, Backspace and Delete take one away, the arrows
    /// move the cursor - up and down by the wrapped lines -, Home and End to its line's start and end,
    /// Ctrl+V pastes; the held keys repeat; the cursor blinks at the message log's rate and stays in
    /// view. Returns its text; pOScroll is the scroll position, kept by the owner.
    /// </summary>
    public string TextArea(Rect pOView, string psName, string psText, in HelpLook pOLook, float pfPadding, ref Vector2 pOScroll)
    {
        bool lbFocused = msAreaFocus == psName;
        int liVertical = 0;

        mbAreaTab = false;
        bool lbHome = false;
        bool lbEnd = false;

        if (lbFocused)
        {
            if (psText != msAreaLastGiven)
                mOArea.Set(psText);

            fReadAreaKeys(out liVertical, out lbHome, out lbEnd);
            lbFocused = msAreaFocus == psName;
        }

        string lsText = lbFocused ? mOArea.Text : (psText ?? string.Empty);

        // The text's width: the view's, less the bar where the text runs below it.
        float lfWidth = pOView.width - (2f * pfPadding);

        fGenerateArea(lsText, pOLook, lfWidth);

        float lfTextHeight = fAreaTextHeight();

        if (lfTextHeight + (2f * pfPadding) > pOView.height)
        {
            lfWidth = pOView.width - ScrollBarWidth - (2f * pfPadding);
            fGenerateArea(lsText, pOLook, lfWidth);
            lfTextHeight = fAreaTextHeight();
        }

        float lfContentHeight = Mathf.Max(pOView.height, lfTextHeight + (2f * pfPadding));
        float lfContentWidth = lfWidth + (2f * pfPadding);

        if (lbFocused)
        {
            int liLength = lsText.Length;
            int liLine = fAreaLineOf(mOArea.Caret);

            if (liVertical != 0)
            {
                int liTarget = liLine + liVertical;

                if (liTarget < 0)
                    mOArea.MoveTo(0);
                else if (liTarget >= mOAreaGenerator.lines.Count)
                    mOArea.MoveTo(liLength);
                else
                    mOArea.MoveTo(fAreaNearest(liTarget, fAreaX(mOArea.Caret), liLength));
            }

            fAreaLineSpan(fAreaLineOf(mOArea.Caret), liLength, out int liFirst, out int liLast);

            if (lbHome)
                mOArea.MoveTo(liFirst);
            else if (lbEnd)
                mOArea.MoveTo(liLast);
        }

        fFill(pOView, mOPalette.Background);

        // The cursor's line in the content, to keep it in view after it moved.
        if (lbFocused && mbAreaCaretMoved)
        {
            fAreaLineBox(fAreaLineOf(mOArea.Caret), out float lfTop, out float lfHeight);

            float lfCaretTop = pfPadding + lfTop;

            if (lfCaretTop < pOScroll.y)
                pOScroll.y = lfCaretTop;
            else if (lfCaretTop + lfHeight > pOScroll.y + pOView.height)
                pOScroll.y = lfCaretTop + lfHeight - pOView.height;

            mbAreaCaretMoved = false;
        }

        // Whether the pointer is over the view, its bar included (before the scroll's layer starts).
        Layer lOOuter = mOLayers[miLayer];
        bool lbOverView = PointerOn && lOOuter.Clip.Contains(mOPointer)
            && new Rect(pOView.position + lOOuter.ToRoot, pOView.size).Contains(mOPointer);

        pOScroll = BeginScroll(pOView, pOScroll, new Rect(0f, 0f, lfContentWidth, lfContentHeight));

        Rect lOContent = new Rect(0f, 0f, lfContentWidth, lfContentHeight);
        int liId = fControl(lOContent, out bool lbOver, out bool _, out bool _);

        if (mbEnabled && mbDown && lbOver && miActive == liId)
        {
            if (!lbFocused)
            {
                fTakeArea(psName, lsText);
                lbFocused = true;
            }

            // The cursor where the press was: its line by the height, then the nearest place on it.
            Vector2 lOAt = PointerHere - new Vector2(pfPadding, pfPadding);
            int liLines = mOAreaGenerator.lines.Count;
            int liPressed = Mathf.Max(0, liLines - 1);

            for (int liLine = 0; liLine < liLines; liLine++)
            {
                fAreaLineBox(liLine, out float lfTop, out float lfHeight);

                if (lOAt.y < lfTop + lfHeight)
                {
                    liPressed = liLine;
                    break;
                }
            }

            if (liLines > 0)
                mOArea.MoveTo(fAreaNearest(liPressed, lOAt.x, lsText.Length));

            mfAreaFocusedAt = Time.unscaledTime;
        }
        else if (lbFocused && mbDown && !lbOverView)
        {
            // A press beside it takes the keyboard away (the tabs, the panel, the game); one on its
            // bar keeps it.
            LetGoOfArea();
            lbFocused = false;
        }

        HelpLook lOLook = pOLook;

        lOLook.Wraps = true;
        lOLook.Anchor = TextAnchor.UpperLeft;
        HelpText(new Rect(pfPadding, pfPadding, lfWidth, Mathf.Max(lfTextHeight, lfContentHeight - (2f * pfPadding))), lsText, lOLook);

        bool lbBlinkOn = ((Time.unscaledTime - mfAreaFocusedAt) % (2f * UWMessageLogModel.CursorBlinkSeconds)) < UWMessageLogModel.CursorBlinkSeconds;

        if (lbFocused && lbBlinkOn)
        {
            fAreaLineBox(fAreaLineOf(mOArea.Caret), out float lfTop, out float lfHeight);
            fFill(new Rect(pfPadding + fAreaX(mOArea.Caret), pfPadding + lfTop, 1.5f, lfHeight), mOPalette.Text);
        }

        EndScroll();

        if (lbFocused)
            msAreaLastGiven = lsText;

        return lsText;
    }
}
