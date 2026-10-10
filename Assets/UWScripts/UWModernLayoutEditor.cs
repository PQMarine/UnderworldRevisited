using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// THE LAYOUT EDITOR (per user on a mockup, 2026-10-04; every part since the same day): "Edit layout"
/// in the game menu opens it -
/// the game menu's Layout page, so the world stands still and the other windows take no clicks
/// (UWModernHud.IsOpen). Over the HUD:
///
///   - every part of UWModernLayout that shows gets a gold frame, its name on a tag, its size in
///     percent and a handle at its bottom right corner;
///   - the LEFT BUTTON drags a part (snapping to the screen's edges and centre and to the other
///     parts' edges and centres, a blue guide showing where), on its handle it sizes the part;
///   - the WHEEL over a part sizes it in steps of 10 %, the RIGHT BUTTON puts it back as it was;
///   - THE STONE SHELF has a grip on its left and right edge: dragged like a window's edge, it
///     widens or narrows the shelf in its own pixels, up to the whole screen (per user,
///     2026-10-10), the other edge staying where it is;
///   - THE POINTER shows what a press does (Kenney's arrows, per user 2026-10-10): left-right
///     over the shelf's grips, four ways over a part to move;
///   - THE PANELS move like the rest and are free windows then (UWModernLayout); the right
///     button docks them again. A closed panel grabbed by its handle opens for the drag, so it
///     does not vanish when it becomes free (per user, 2026-10-04), and closes again with the
///     editor. (The minimap docking into the character panel's head is gone, per user the same
///     day: placed freely it can sit wherever one wants it.)
///   - THE EDITOR'S WINDOW (since 2026-10-09 with ordinary controls in the menu bar's look, see
///     the section "The editor's window"; before, a leather strip under the heading): a short
///     help, the UI SIZE with its - and + (moved here from the game menu, per user 2026-10-04: it
///     is the base all the parts' sizes multiply, and sizes the boxes and menus that are no
///     parts), Panels out or in, Dialog on or off (the conversation's preview with sample text),
///     Snap on or off, Reset all, Done, and below them the inspector of the selected part;
///     Escape goes back to the game menu.
///
/// WHAT IS EDITED SHOWS (per user, 2026-10-04: the bags shut with the Escape that leads to the
/// menu, and the closed panels could not be seen): the bags open while the editor is open, and
/// "Panels: out" slides the character panel (Character tab) and the rune panel out - by default
/// they stay in, as the minimap hides behind the open character panel. Both go back as they were.
/// </summary>
public class UWModernLayoutEditor : MonoBehaviour
{
    private static readonly Color msGold = new Color(0.925f, 0.77f, 0.44f, 1f);

    private static readonly Color msGoldDim = new Color(0.925f, 0.77f, 0.44f, 0.6f);

    private static readonly Color msHover = new Color(1f, 0.92f, 0.69f, 1f);

    private static readonly Color msGuide = new Color(0.47f, 0.78f, 1f, 0.9f);

    private static readonly Color msText = new Color(0.94f, 0.87f, 0.71f);

    /// <summary>Screen pixels within which an edge snaps.</summary>
    private const float SnapDistance = 10f;

    private UWGameUI mOUi;

    private Font mOFont;

    private Canvas mOCanvas;

    private readonly Image[][] mOLines = new Image[UWModernLayout.ElementCount][];

    private readonly Image[] mOTags = new Image[UWModernLayout.ElementCount];

    private readonly Text[] mOTagTexts = new Text[UWModernLayout.ElementCount];

    private readonly Image[] mOHandles = new Image[UWModernLayout.ElementCount];

    private readonly Text[] mOPercents = new Text[UWModernLayout.ElementCount];

    private readonly Rect[] mOHandleRects = new Rect[UWModernLayout.ElementCount];

    private Image mOGuideX;

    private Image mOGuideY;

    // --- What the editor opened, to give back
    private bool mbWasEditing;

    private bool mbOpenedBags;

    private bool mbPanelsOut;

    private bool mbCharacterWasOpen;

    private UWModernPanel.TabEnum meCharacterTab;

    private bool mbRunesWereOpen;

    private bool mbSnap = true;

    // --- The drag
    private int miDragged = -1;

    private bool mbSizing;

    private Vector2 mODragFrom;

    private Rect mODragRect;

    private int miDragPercent;

    /// <summary>The stone shelf's edge being dragged (-1 left, 1 right, 0 none), and its width in
    /// screen pixels per pixel of its own when the drag began.</summary>
    private int miWidening;

    private float mfShelfPixel;

    /// <summary>The stone shelf's grips at its left and right edge (per user, 2026-10-10: "can it
    /// be done with the mouse as well? As one is used to from windows").</summary>
    private readonly Image[] mOGrips = new Image[2];

    private readonly Rect[] mOGripRects = new Rect[2];

    /// <summary>The mouse pointer the editor shows (per user, 2026-10-10, Kenney's arrows): the
    /// left-right arrow over and on the stone shelf's grips, the four-way arrow over a part that
    /// moves and while moving it, else the system's own.</summary>
    private enum PointerEnum
    {
        System,
        LeftRight,
        FourWays
    }

    private PointerEnum mePointer = PointerEnum.System;

    /// <summary>The Kenney tile UWGameUI shows as the pointer while the editor is open, -1 for the
    /// game's own cross: the game sets its pointer every frame, so the editor cannot set it itself.</summary>
    public static int PointerTile { get; private set; } = -1;

    /// <summary>A panel the editor opened for a drag, to close with it.</summary>
    private bool mbOpenedCharacter;

    private bool mbOpenedRunes;

    private float mfGuideX = -1f;

    private float mfGuideY = -1f;

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOFont = Resources.Load<Font>("Fonts/LexendExa");
    }

    private void OnDestroy()
    {
        fShowPointer(PointerEnum.System);

        foreach (Texture2D lOTexture in mOTextures.Values)
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        foreach (Texture2D lOTexture in mOPreviews.Values)
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }
    }

    // ------------------------------------------------- Input

    private void Update()
    {
        bool lbEditing = UWModernLayout.IsEditing;

        if (lbEditing && !mbWasEditing)
            fBegin();
        else if (!lbEditing && mbWasEditing)
            fEnd();

        mbWasEditing = lbEditing;

        if (!lbEditing || Mouse.current == null)
        {
            miDragged = -1;
            return;
        }

        Mouse lOMouse = Mouse.current;
        Vector2 lOPointer = lOMouse.position.ReadValue();

        if (miDragged >= 0)
        {
            if (lOMouse.leftButton.isPressed)
                fDrag(lOPointer);
            else
            {
                miDragged = -1;
                UWModernLayout.Save();

                if (miWidening != 0)
                {
                    miWidening = 0;
                    fSaveSettings();
                }
            }

            return;
        }

        // Presses, the right button and the wheel on the editor's window are its own.
        if (fWindowContains(lOPointer))
            return;

        mfGuideX = -1f;
        mfGuideY = -1f;

        if (lOMouse.leftButton.wasPressedThisFrame)
        {
            int liGrip = fGripAt(lOPointer);

            if (liGrip != 0 && UWModernLayout.TryGetRect(UWModernLayout.ElementEnum.StoneShelf, out mODragRect))
            {
                miSelected = (int)UWModernLayout.ElementEnum.StoneShelf;
                miDragged = miSelected;
                mbSizing = false;
                miWidening = liGrip;
                mODragFrom = lOPointer;
                mfShelfPixel = mODragRect.width / Mathf.Min(UWModernHud.StoneShelfWidth, UWModernHud.StoneShelfMaxWidth);
                return;
            }

            int liElement = fElementAt(lOPointer, out bool lbHandle);

            // THE INSPECTOR'S SELECTION (per user, 2026-10-09): the part pressed, none for a
            // press beside every part.
            miSelected = liElement;

            if (liElement < 0)
                return;

            UWModernLayout.ElementEnum leElement = (UWModernLayout.ElementEnum)liElement;

            UWModernLayout.TryGetRect(leElement, out mODragRect);

            // A closed panel (its handle grabbed) opens, else it would vanish once it is free.
            if (!lbHandle)
                fOpenForDrag(leElement);

            miDragged = liElement;
            mbSizing = lbHandle;
            mODragFrom = lOPointer;
            miDragPercent = UWModernLayout.Percent((UWModernLayout.ElementEnum)liElement);
            return;
        }

        if (lOMouse.rightButton.wasPressedThisFrame)
        {
            int liElement = fElementAt(lOPointer, out bool _);

            if (liElement >= 0)
                UWModernLayout.Reset((UWModernLayout.ElementEnum)liElement);

            return;
        }

        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (Mathf.Abs(lfWheel) > 0.01f)
        {
            int liElement = fElementAt(lOPointer, out bool _);
            UWModernLayout.ElementEnum leElement = (UWModernLayout.ElementEnum)liElement;

            // Shift and the wheel on the original's scroll: its lines, the paper extended in its
            // middle (UWModernScroll, per user 2026-10-09).
            if (liElement >= 0 && leElement == UWModernLayout.ElementEnum.Messages && UWModernLayout.IsScrollShown
                && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed)
            {
                UWUserSettings.ModernScrollLines = Mathf.Clamp(UWModernScroll.Lines + (lfWheel > 0f ? 1 : -1), UWModernScroll.MinLines, UWModernScroll.MaxLines);
                UWUserSettings.Save();
            }
            else if (liElement >= 0 && UWModernLayout.TryGetRect(leElement, out Rect lORect))
                UWModernLayout.SetPercent(leElement, UWModernLayout.Percent(leElement)
                    + (lfWheel > 0f ? UWModernLayout.PercentStep : -UWModernLayout.PercentStep), lORect);
        }
    }

    private void fOpenForDrag(UWModernLayout.ElementEnum peElement)
    {
        if (peElement == UWModernLayout.ElementEnum.CharacterPanel && UWModernPanel.Instance != null && !UWModernPanel.Instance.IsOpen)
        {
            UWModernPanel.Instance.Open(UWModernPanel.TabEnum.Character);
            mbOpenedCharacter = true;
        }
        else if (peElement == UWModernLayout.ElementEnum.RunePanel && UWModernRunePanel.Instance != null && !UWModernRunePanel.Instance.IsOpen)
        {
            UWModernRunePanel.Instance.Open(UWModernRunePanel.TabEnum.Runes);
            mbOpenedRunes = true;
        }
    }

    private void fShowPointer(PointerEnum pePointer)
    {
        if (pePointer == mePointer)
            return;

        PointerTile = pePointer == PointerEnum.System ? -1
            : pePointer == PointerEnum.LeftRight ? UWGlyphs.ArrowLeftRightTile : UWGlyphs.ArrowFourWaysTile;
        mePointer = pePointer;
    }

    /// <summary>The stone shelf's grip under the pointer: -1 its left edge, 1 its right, 0 none.</summary>
    private int fGripAt(Vector2 pOPointer)
    {
        if (!UWModernLayout.TryGetRect(UWModernLayout.ElementEnum.StoneShelf, out Rect _))
            return 0;

        if (mOGripRects[0].Contains(pOPointer))
            return -1;

        return mOGripRects[1].Contains(pOPointer) ? 1 : 0;
    }

    /// <summary>The part under the pointer, its handle first; -1 for none.</summary>
    private int fElementAt(Vector2 pOPointer, out bool pbHandle)
    {
        pbHandle = false;

        for (int liAt = 0; liAt < UWModernLayout.ElementCount; liAt++)
        {
            if (UWModernLayout.TryGetRect((UWModernLayout.ElementEnum)liAt, out Rect _) && mOHandleRects[liAt].Contains(pOPointer))
            {
                pbHandle = true;
                return liAt;
            }
        }

        // The smallest part under the pointer - the heading lies over nothing, the spells may
        // lie near the minimap.
        int liBest = -1;
        float lfBestArea = float.MaxValue;

        for (int liAt = 0; liAt < UWModernLayout.ElementCount; liAt++)
        {
            if (!UWModernLayout.TryGetRect((UWModernLayout.ElementEnum)liAt, out Rect lORect) || !lORect.Contains(pOPointer))
                continue;

            float lfArea = lORect.width * lORect.height;

            if (lfArea < lfBestArea)
            {
                lfBestArea = lfArea;
                liBest = liAt;
            }
        }

        return liBest;
    }

    private void fDrag(Vector2 pOPointer)
    {
        UWModernLayout.ElementEnum leElement = (UWModernLayout.ElementEnum)miDragged;
        Vector2 lODelta = pOPointer - mODragFrom;

        if (miWidening != 0)
        {
            fWiden(lODelta.x);
            return;
        }

        if (mbSizing)
        {
            // The handle at the bottom right: wider to the right, in steps of 5 %.
            float lfFactor = Mathf.Max(0.1f, (mODragRect.width + lODelta.x) / Mathf.Max(1f, mODragRect.width));
            int liPercent = Mathf.RoundToInt(miDragPercent * lfFactor / 5f) * 5;

            if (liPercent != UWModernLayout.Percent(leElement))
                UWModernLayout.SetPercent(leElement, liPercent, UWModernLayout.TryGetRect(leElement, out Rect lONow) ? lONow : mODragRect, false);

            return;
        }

        Rect lOMoved = new Rect(mODragRect.position + lODelta, mODragRect.size);

        if (mbSnap)
            lOMoved = fSnap(lOMoved);

        UWModernLayout.Move(leElement, lOMoved, false);
    }

    /// <summary>
    /// The stone shelf's edge dragged like a window's: the other edge stays, the width follows the
    /// pointer in the shelf's own pixels (from StoneShelfMinWidth to the whole screen), and with
    /// Snap the edge snaps to the screen's sides, margins and centre and to the other parts'
    /// edges and centres.
    /// </summary>
    private void fWiden(float pfDeltaX)
    {
        float lfEdge = (miWidening > 0 ? mODragRect.xMax : mODragRect.xMin) + pfDeltaX;

        mfGuideX = -1f;
        mfGuideY = -1f;

        if (mbSnap)
        {
            float lfMargin = 4f * UWModernHud.PixelScale;
            System.Collections.Generic.List<float> lOLines = new System.Collections.Generic.List<float>
            {
                0f, lfMargin, Screen.width * 0.5f, Screen.width - lfMargin, Screen.width
            };

            for (int liAt = 0; liAt < UWModernLayout.ElementCount; liAt++)
            {
                if (liAt == miDragged || !UWModernLayout.TryGetRect((UWModernLayout.ElementEnum)liAt, out Rect lOOther))
                    continue;

                lOLines.Add(lOOther.xMin);
                lOLines.Add(lOOther.center.x);
                lOLines.Add(lOOther.xMax);
            }

            lfEdge += fNearest(new[] { lfEdge }, lOLines, out mfGuideX);
        }

        float lfPixels = miWidening > 0 ? lfEdge - mODragRect.xMin : mODragRect.xMax - lfEdge;
        int liWidth = Mathf.Clamp(Mathf.RoundToInt(lfPixels / Mathf.Max(0.01f, mfShelfPixel)),
            UWDataImport.UWData.UWHudArt.StoneShelfMinWidth, UWModernHud.StoneShelfMaxWidth);
        float lfWidth = liWidth * mfShelfPixel;

        UWUserSettings.ModernStoneShelfWidth = liWidth;
        UWModernLayout.Move(UWModernLayout.ElementEnum.StoneShelf,
            new Rect(miWidening > 0 ? mODragRect.xMin : mODragRect.xMax - lfWidth, mODragRect.y, lfWidth, mODragRect.height), false);
    }

    /// <summary>The nearest of the part's edges and centre to the screen's margins and centre and
    /// to the other parts' edges and centres, within SnapDistance - per axis.</summary>
    private Rect fSnap(Rect pORect)
    {
        float lfMargin = 4f * UWModernHud.PixelScale;
        System.Collections.Generic.List<float> lOLinesX = new System.Collections.Generic.List<float>
        {
            lfMargin, Screen.width * 0.5f, Screen.width - lfMargin
        };
        System.Collections.Generic.List<float> lOLinesY = new System.Collections.Generic.List<float>
        {
            lfMargin, Screen.height * 0.5f, Screen.height - lfMargin
        };

        for (int liAt = 0; liAt < UWModernLayout.ElementCount; liAt++)
        {
            if (liAt == miDragged || !UWModernLayout.TryGetRect((UWModernLayout.ElementEnum)liAt, out Rect lOOther))
                continue;

            lOLinesX.Add(lOOther.xMin);
            lOLinesX.Add(lOOther.center.x);
            lOLinesX.Add(lOOther.xMax);
            lOLinesY.Add(lOOther.yMin);
            lOLinesY.Add(lOOther.center.y);
            lOLinesY.Add(lOOther.yMax);
        }

        float lfShiftX = fNearest(new[] { pORect.xMin, pORect.center.x, pORect.xMax }, lOLinesX, out mfGuideX);
        float lfShiftY = fNearest(new[] { pORect.yMin, pORect.center.y, pORect.yMax }, lOLinesY, out mfGuideY);

        return new Rect(pORect.x + lfShiftX, pORect.y + lfShiftY, pORect.width, pORect.height);
    }

    private static float fNearest(float[] pfOwn, System.Collections.Generic.List<float> pOLines, out float pfGuide)
    {
        float lfBest = SnapDistance;
        float lfShift = 0f;

        pfGuide = -1f;

        foreach (float lfOwn in pfOwn)
        {
            foreach (float lfLine in pOLines)
            {
                float lfDistance = Mathf.Abs(lfLine - lfOwn);

                if (lfDistance < lfBest)
                {
                    lfBest = lfDistance;
                    lfShift = lfLine - lfOwn;
                    pfGuide = lfLine;
                }
            }
        }

        return lfShift;
    }

    /// <summary>The bags open for the editor; the panels' state is remembered.</summary>
    private void fBegin()
    {
        UWModernBags lOBags = UWModernBags.Instance;

        mbOpenedBags = lOBags != null && !lOBags.IsOpen;

        if (mbOpenedBags)
            lOBags.Open();

        UWModernPanel lOPanel = UWModernPanel.Instance;
        UWModernRunePanel lORunes = UWModernRunePanel.Instance;

        mbCharacterWasOpen = lOPanel != null && lOPanel.IsOpen;
        meCharacterTab = lOPanel != null ? lOPanel.Tab : UWModernPanel.TabEnum.Character;
        mbRunesWereOpen = lORunes != null && lORunes.IsOpen;
        mbPanelsOut = false;
        miSelected = -1;
        UWModernConversation.Previewing = false;
    }

    private void fEnd()
    {
        UWModernConversation.Previewing = false;

        if (mbPanelsOut)
            fSetPanelsOut(false);

        // What a drag opened closes again, unless it was open before.
        if (mbOpenedCharacter && !mbCharacterWasOpen && UWModernPanel.Instance != null && UWModernPanel.Instance.IsOpen)
            UWModernPanel.Instance.Close();

        if (mbOpenedRunes && !mbRunesWereOpen && UWModernRunePanel.Instance != null && UWModernRunePanel.Instance.IsOpen)
            UWModernRunePanel.Instance.Close();

        mbOpenedCharacter = false;
        mbOpenedRunes = false;

        UWModernBags lOBags = UWModernBags.Instance;

        if (mbOpenedBags && lOBags != null && lOBags.IsOpen)
            lOBags.Close();

        mbOpenedBags = false;
        miDragged = -1;
    }

    /// <summary>Out: both panels slide out; in: back as they were before.</summary>
    private void fSetPanelsOut(bool pbOut)
    {
        UWModernPanel lOPanel = UWModernPanel.Instance;
        UWModernRunePanel lORunes = UWModernRunePanel.Instance;

        mbPanelsOut = pbOut;

        if (lOPanel != null)
        {
            if (pbOut)
                lOPanel.Open(UWModernPanel.TabEnum.Character);
            else if (!mbCharacterWasOpen)
                lOPanel.Close();
            else if (meCharacterTab != UWModernPanel.TabEnum.Character)
                lOPanel.Open(meCharacterTab);
        }

        if (lORunes != null)
        {
            if (pbOut)
                lORunes.Open(UWModernRunePanel.TabEnum.Runes);
            else if (!mbRunesWereOpen)
                lORunes.Close();
        }
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!UWModernLayout.IsEditing || mOUi == null || mOUi.mOUWData == null)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            fShowPointer(PointerEnum.System);
            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        float lfScale = UWModernHud.PixelScale;
        Vector2 lOPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
        int liHovered = miDragged >= 0 ? miDragged : fElementAt(lOPointer, out bool _);

        for (int liAt = 0; liAt < UWModernLayout.ElementCount; liAt++)
        {
            UWModernLayout.ElementEnum leElement = (UWModernLayout.ElementEnum)liAt;
            bool lbShown = UWModernLayout.TryGetRect(leElement, out Rect lORect);

            foreach (Image lOLine in mOLines[liAt])
                lOLine.enabled = lbShown;

            mOTags[liAt].enabled = lbShown;
            mOTagTexts[liAt].enabled = lbShown;
            mOHandles[liAt].enabled = lbShown;
            mOPercents[liAt].enabled = lbShown;

            if (!lbShown)
            {
                mOHandleRects[liAt] = Rect.zero;
                continue;
            }

            bool lbActive = liAt == liHovered || liAt == miSelected;
            Color lOColour = lbActive ? msHover : msGoldDim;
            float lfLine = Mathf.Max(2f, lfScale * (lbActive ? 0.75f : 0.5f));

            fSetRect(mOLines[liAt][0].rectTransform, lORect.x, lORect.yMax, lORect.width, lfLine);
            fSetRect(mOLines[liAt][1].rectTransform, lORect.x, lORect.y - lfLine, lORect.width, lfLine);
            fSetRect(mOLines[liAt][2].rectTransform, lORect.x - lfLine, lORect.y - lfLine, lfLine, lORect.height + (2f * lfLine));
            fSetRect(mOLines[liAt][3].rectTransform, lORect.xMax, lORect.y - lfLine, lfLine, lORect.height + (2f * lfLine));

            foreach (Image lOLine in mOLines[liAt])
                lOLine.color = lOColour;

            // The name on a tag above the part - inside it at the screen's top.
            Text lOTag = mOTagTexts[liAt];

            lOTag.text = UWModernLayout.Names[liAt] + (UWModernLayout.IsDocked(leElement) ? " (docked)" : string.Empty);
            lOTag.fontSize = Mathf.Max(10, Mathf.RoundToInt(3.8f * lfScale));
            lOTag.color = lbActive ? msHover : msText;

            float lfTagWidth = lOTag.preferredWidth + (4f * lfScale);
            float lfTagHeight = lOTag.fontSize * 1.5f;
            float lfTagY = lORect.yMax + lfLine + lfTagHeight <= Screen.height ? lORect.yMax + lfLine : lORect.yMax - lfTagHeight;

            fSetRect(mOTags[liAt].rectTransform, lORect.x, lfTagY, lfTagWidth, lfTagHeight);
            fSetRect(lOTag.rectTransform, lORect.x, lfTagY, lfTagWidth, lfTagHeight);
            mOTags[liAt].color = new Color(0.12f, 0.08f, 0.05f, 0.85f);

            // The handle and the size.
            float lfHandle = 5f * lfScale;
            Rect lOHandle = new Rect(lORect.xMax - (lfHandle * 0.6f), lORect.y - (lfHandle * 0.4f), lfHandle, lfHandle);

            mOHandleRects[liAt] = lOHandle;
            fSetRect(mOHandles[liAt].rectTransform, lOHandle.x, lOHandle.y, lOHandle.width, lOHandle.height);
            mOHandles[liAt].color = lbActive ? msHover : msGold;

            mOPercents[liAt].text = UWModernLayout.Percent(leElement) + " %";
            mOPercents[liAt].fontSize = Mathf.Max(9, Mathf.RoundToInt(3.6f * lfScale));
            fSetRect(mOPercents[liAt].rectTransform, lORect.x, lORect.y + lfScale, lORect.width - lfHandle, mOPercents[liAt].fontSize * 1.4f);
        }

        // The stone shelf's grips: a bar in the middle of each side edge, grabbed a little beyond it.
        bool lbShelf = UWModernLayout.TryGetRect(UWModernLayout.ElementEnum.StoneShelf, out Rect lOShelf);
        int liGripHovered = miWidening != 0 ? miWidening : (miDragged < 0 ? fGripAt(lOPointer) : 0);

        for (int liSide = 0; liSide < 2; liSide++)
        {
            mOGrips[liSide].enabled = lbShelf;

            if (!lbShelf)
            {
                mOGripRects[liSide] = Rect.zero;
                continue;
            }

            float lfEdge = liSide == 0 ? lOShelf.xMin : lOShelf.xMax;
            float lfReach = 3f * lfScale;
            float lfBar = Mathf.Max(3f, lfScale);
            float lfBottom = lOShelf.y + (5f * lfScale * 0.6f);

            mOGripRects[liSide] = new Rect(lfEdge - lfReach, lfBottom, 2f * lfReach, Mathf.Max(lfReach, lOShelf.yMax - lfBottom));
            fSetRect(mOGrips[liSide].rectTransform, lfEdge - (lfBar * 0.5f), lOShelf.y + (lOShelf.height * 0.2f), lfBar, lOShelf.height * 0.6f);
            mOGrips[liSide].color = liGripHovered == (liSide == 0 ? -1 : 1) ? msHover : msGold;
        }

        // The pointer: what a press here would do.
        PointerEnum lePointer = PointerEnum.System;

        if (miDragged >= 0)
            lePointer = miWidening != 0 ? PointerEnum.LeftRight : (mbSizing ? PointerEnum.System : PointerEnum.FourWays);
        else if (!fWindowContains(lOPointer))
        {
            if (fGripAt(lOPointer) != 0)
                lePointer = PointerEnum.LeftRight;
            else if (fElementAt(lOPointer, out bool lbHandle) >= 0 && !lbHandle)
                lePointer = PointerEnum.FourWays;
        }

        fShowPointer(lePointer);

        // The snapping guides while dragging.
        mOGuideX.enabled = miDragged >= 0 && !mbSizing && mfGuideX >= 0f;
        mOGuideY.enabled = miDragged >= 0 && !mbSizing && mfGuideY >= 0f;

        if (mOGuideX.enabled)
            fSetRect(mOGuideX.rectTransform, mfGuideX - 1f, 0f, 2f, Screen.height);

        if (mOGuideY.enabled)
            fSetRect(mOGuideY.rectTransform, 0f, mfGuideY - 1f, Screen.width, 2f);

    }

    // ------------------------------------------------- The editor's window

    /// <summary>
    /// THE EDITOR'S WINDOW (per user, 2026-10-09: "the whole layout panel best with ordinary
    /// controls, then it stands apart from the interface better"; before, two leather strips with
    /// the interface's own buttons, the inspector one long row that took too much room): one window
    /// in the plain look of the menu bar (UWSetupMenu's colours and the interface font), drawn with
    /// IMGUI over everything. At the top the editor's own controls - the UI size, Panels out, the
    /// conversation's preview, Snap, Reset all, Done -, below them THE INSPECTOR as a property grid
    /// (per user, the same day: "a context window shown all the time, its content fitting the part
    /// clicked", then "like a property grid, not a long row"): the selected part's name, then one
    /// row per setting, its name on the left and a control on the right - choices as a segmented
    /// row, numbers with - and + -, last Reset for the part. With nothing selected it lists the
    /// parts that are switched off, each with Show, so none is out of reach. The window is dragged
    /// by its title and stays where it was put while the game runs.
    /// </summary>
    private enum InspectorKindEnum
    {
        Choice,

        /// <summary>Choices without names: a row of radio buttons (per user, 2026-10-09, for the
        /// backs' patterns: "no need to name them, just four radio buttons side by side", then the
        /// colours too: "saves room").</summary>
        Radio,
        Stepper,
        Action,
        Info
    }

    private struct InspectorItem
    {
        public InspectorKindEnum Kind;

        public string Label;

        /// <summary>Choice: the choices; Action: the button's text in [0].</summary>
        public string[] Choices;

        /// <summary>Choice: the choice in force.</summary>
        public int Current;

        /// <summary>Stepper: the value between - and +.</summary>
        public string Value;

        /// <summary>Stepper: - and + still possible.</summary>
        public bool CanDown;

        public bool CanUp;

        public bool Enabled;

        /// <summary>Choice: the index chosen; Stepper: 0 for -, 1 for +; Action: 0.</summary>
        public System.Action<int> Choose;

        /// <summary>Radio: a picture per choice, drawn instead of the radio buttons (the backs'
        /// colours and patterns).</summary>
        public Texture2D[] Previews;
    }

    /// <summary>The window's size in the menu bar's reference units (720 rows high).</summary>
    private const float WindowWidth = 320f;

    private const float WindowPad = 10f;

    private const float TitleHeight = 26f;

    private const float HintHeight = 38f;

    private const float RowHeight = 26f;

    private const float RowGap = 4f;

    private const float LabelWidth = 110f;

    private const float StepButtonWidth = 26f;

    private const int WindowId = 0x55574C45;

    private static Rect msWindowRect = new Rect(-1f, 70f, WindowWidth, 200f);

    private int miSelected = -1;

    private readonly System.Collections.Generic.List<InspectorItem> mOItems = new System.Collections.Generic.List<InspectorItem>();

    private readonly System.Collections.Generic.Dictionary<Color, Texture2D> mOTextures = new System.Collections.Generic.Dictionary<Color, Texture2D>();

    private GUIStyle mOWindowStyle;

    private GUIStyle mOTitleStyle;

    private GUIStyle mOLabelStyle;

    private GUIStyle mOHintStyle;

    private GUIStyle mOHeadingStyle;

    private GUIStyle mOValueStyle;

    private GUIStyle mOButtonStyle;

    private GUIStyle mOSegmentStyle;

    private GUIStyle mOToggleStyle;

    /// <summary>The window on the screen, in screen pixels with y up as the pointer has it, for
    /// Update to leave presses on it alone.</summary>
    private Rect mOWindowScreenRect;

    private static InspectorItem fChoice(string psLabel, string[] psChoices, int piCurrent, System.Action<int> pOChoose, bool pbEnabled = true)
    {
        return new InspectorItem { Kind = InspectorKindEnum.Choice, Label = psLabel, Choices = psChoices, Current = piCurrent, Choose = pOChoose, Enabled = pbEnabled };
    }

    private static InspectorItem fRadio(string psLabel, int piCount, int piCurrent, System.Action<int> pOChoose, bool pbEnabled = true)
    {
        return new InspectorItem { Kind = InspectorKindEnum.Radio, Label = psLabel, Choices = new string[piCount], Current = piCurrent, Choose = pOChoose, Enabled = pbEnabled };
    }

    private static InspectorItem fStepper(string psLabel, string psValue, bool pbCanDown, bool pbCanUp, System.Action<int> pOChoose)
    {
        return new InspectorItem { Kind = InspectorKindEnum.Stepper, Label = psLabel, Value = psValue, CanDown = pbCanDown, CanUp = pbCanUp, Choose = pOChoose, Enabled = true };
    }

    private static InspectorItem fAction(string psLabel, string psButton, System.Action<int> pOChoose, bool pbEnabled = true)
    {
        return new InspectorItem { Kind = InspectorKindEnum.Action, Label = psLabel, Choices = new[] { psButton }, Choose = pOChoose, Enabled = pbEnabled };
    }

    private static InspectorItem fInfo(string psText)
    {
        return new InspectorItem { Kind = InspectorKindEnum.Info, Label = psText, Enabled = true };
    }

    private static void fSaveSettings()
    {
        UWUserSettings.Save();
    }

    /// <summary>Whether a part draws a back at all - the Background items are left out where they
    /// would change nothing (per user, 2026-10-10): the stone shelf is a deco panel itself, the
    /// rune tablet, the stats panel and the character page are the original's own pages, and the
    /// conversation in the original's look is its parchment, not leather.</summary>
    private static bool fOffersBack(UWModernLayout.ElementEnum peElement)
    {
        switch (peElement)
        {
            case UWModernLayout.ElementEnum.StoneShelf:
            case UWModernLayout.ElementEnum.RuneTablet:
            case UWModernLayout.ElementEnum.StatsPanel:
            case UWModernLayout.ElementEnum.CharacterPage:
                return false;

            case UWModernLayout.ElementEnum.Conversation:
                return !UWModernLayout.IsConversationOriginal;

            default:
                return true;
        }
    }

    /// <summary>
    /// A part's generated back (UWModernBacks, UWBackdropArt; per user, 2026-10-09): its shape, and
    /// once it has one the colour, the pattern and the border - colour and pattern apart, per user.
    /// Every part has it; a leather part chooses between its leather and a back in its place, which
    /// is always a rectangle.
    /// </summary>
    private void fAddBackItems(UWModernLayout.ElementEnum peElement)
    {
        UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);
        bool lbLeather = UWModernBacks.IsLeatherPart(peElement);

        // "Background" written out ("Back" also reads as "go back"), "Default" for the leather -
        // the modern scheme's default, not the original's look (per user, 2026-10-09, after
        // "Original") -, "Custom" for a back of the part's own ("Leather / Own" did not please).
        if (lbLeather)
        {
            mOItems.Add(fChoice("Background", new[] { "Default", "Custom" }, lOBack.Shape == UWDataImport.UWData.UWBackdropArt.ShapeEnum.None ? 0 : 1, liAt =>
            {
                UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

                lONew.Shape = liAt == 0 ? UWDataImport.UWData.UWBackdropArt.ShapeEnum.None : UWDataImport.UWData.UWBackdropArt.ShapeEnum.Rect;
                UWModernBacks.Set(peElement, lONew);
            }));
        }
        else
        {
            mOItems.Add(fChoice("Background", new[] { "None", "Oval", "Rect" }, (int)lOBack.Shape, liAt =>
            {
                UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

                lONew.Shape = (UWDataImport.UWData.UWBackdropArt.ShapeEnum)liAt;
                UWModernBacks.Set(peElement, lONew);
            }));
        }

        bool lbShaped = lOBack.Shape != UWDataImport.UWData.UWBackdropArt.ShapeEnum.None;

        // Each choice shown as a sample of itself (per user, the same day, instead of bare radio
        // buttons): the colours in the pattern chosen, the patterns in the colour chosen.
        InspectorItem lOColours = fRadio("Colour", 4, (int)lOBack.Colour, liAt =>
        {
            UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

            lONew.Colour = (UWDataImport.UWData.UWBackdropArt.ColourEnum)liAt;
            UWModernBacks.Set(peElement, lONew);
        }, lbShaped);
        InspectorItem lOPatterns = fRadio("Pattern", 4, (int)lOBack.Pattern, liAt =>
        {
            UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

            lONew.Pattern = (UWDataImport.UWData.UWBackdropArt.PatternEnum)liAt;
            UWModernBacks.Set(peElement, lONew);
        }, lbShaped);

        lOColours.Previews = new Texture2D[4];
        lOPatterns.Previews = new Texture2D[4];

        for (int liAt = 0; liAt < 4; liAt++)
        {
            lOColours.Previews[liAt] = fBackPreview(liAt, (int)lOBack.Pattern);
            lOPatterns.Previews[liAt] = fBackPreview((int)lOBack.Colour, liAt);
        }

        mOItems.Add(lOColours);
        mOItems.Add(lOPatterns);
        mOItems.Add(fChoice("Border", new[] { "On", "Off" }, lOBack.Border ? 0 : 1, liAt =>
        {
            UWModernBacks.Back lONew = UWModernBacks.Get(peElement);

            lONew.Border = liAt == 0;
            UWModernBacks.Set(peElement, lONew);
        }, lbShaped));
    }

    /// <summary>The samples of the backs' colours and patterns, built once per colour help.</summary>
    private readonly System.Collections.Generic.Dictionary<int, Texture2D> mOPreviews = new System.Collections.Generic.Dictionary<int, Texture2D>();

    /// <summary>The size of a sample in the back's own pixels.</summary>
    private const int PreviewWidth = 30;

    private const int PreviewHeight = 12;

    /// <summary>A sample of a back in this colour and pattern: a rectangle without a border.</summary>
    private Texture2D fBackPreview(int piColour, int piPattern)
    {
        int liKey = (UWColourVision.Version * 100) + (piColour * 10) + piPattern;

        if (mOPreviews.TryGetValue(liKey, out Texture2D lOTexture) && lOTexture != null)
            return lOTexture;

        if (mOUi == null || mOUi.mOUWData == null)
            return null;

        UWModernBacks.Back lOBack = new UWModernBacks.Back
        {
            Shape = UWDataImport.UWData.UWBackdropArt.ShapeEnum.Rect,
            Colour = (UWDataImport.UWData.UWBackdropArt.ColourEnum)piColour,
            Pattern = (UWDataImport.UWData.UWBackdropArt.PatternEnum)piPattern,
            Border = false
        };

        lOTexture = UWModernHudArt.BuildBackdrop(mOUi.mOUWData.Textures, PreviewWidth, PreviewHeight, lOBack, FilterMode.Point, piColour + 1);
        mOPreviews[liKey] = lOTexture;

        return lOTexture;
    }

    /// <summary>What the inspector offers for the selection now.</summary>
    private void fGatherItems()
    {
        mOItems.Clear();

        if (miSelected < 0)
        {
            // Nothing selected: the settings of the whole interface - its font (UWUiFonts; per user,
            // 2026-10-09: the original's font as an option, Modern keeping its own) -, then the
            // parts switched off, to be switched on again.
            mOItems.Add(fChoice("Font", new[] { "Modern", "Original" }, UWUserSettings.InterfaceFont == 1 ? 1 : 0,
                liAt => { UWUserSettings.InterfaceFont = liAt; fSaveSettings(); }));

            if (UWUserSettings.MinimapHidden)
                mOItems.Add(fAction("Minimap", "Show", liAt => { UWUserSettings.MinimapHidden = false; fSaveSettings(); }));

            if (UWUserSettings.ActionBarHidden)
                mOItems.Add(fAction("Action bar", "Show", liAt => { UWUserSettings.ActionBarHidden = false; fSaveSettings(); }));

            if (!UWUserSettings.ModernCompass)
                mOItems.Add(fAction("Compass", "Show", liAt => { UWUserSettings.ModernCompass = true; fSaveSettings(); }));

            if (!UWUserSettings.ModernStoneShelf)
                mOItems.Add(fAction("Stone shelf", "Show", liAt => { UWUserSettings.ModernStoneShelf = true; fSaveSettings(); }));

            if (!UWUserSettings.ModernRuneHollow)
                mOItems.Add(fAction("Rune hollow", "Show", liAt => { UWUserSettings.ModernRuneHollow = true; fSaveSettings(); }));

            if (!UWUserSettings.ModernRuneTablet)
                mOItems.Add(fAction("Rune tablet", "Show", liAt => { UWUserSettings.ModernRuneTablet = true; fSaveSettings(); }));

            if (!UWUserSettings.ModernStatsPanel)
                mOItems.Add(fAction("Stats panel", "Show", liAt => { UWUserSettings.ModernStatsPanel = true; fSaveSettings(); }));

            if (!UWUserSettings.ModernCharacterPage)
                mOItems.Add(fAction("Character page", "Show", liAt => { UWUserSettings.ModernCharacterPage = true; fSaveSettings(); }));


            if (mOItems.Count == 1)
                mOItems.Add(fInfo("Click a part to see its settings."));

            return;
        }

        UWModernLayout.ElementEnum leElement = (UWModernLayout.ElementEnum)miSelected;
        int liPercent = UWModernLayout.Percent(leElement);

        mOItems.Add(fStepper("Size", liPercent + " %", true, true, liAt =>
        {
            if (UWModernLayout.TryGetRect(leElement, out Rect lORect))
                UWModernLayout.SetPercent(leElement, liPercent + (liAt == 0 ? -UWModernLayout.PercentStep : UWModernLayout.PercentStep), lORect);
        }));

        switch (leElement)
        {
            case UWModernLayout.ElementEnum.ActionBar:
                // Off hides the bar and its keys (per user, 2026-10-10); Show brings it back.
                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, UWUserSettings.ActionBarHidden ? 1 : 0,
                    liAt => { UWUserSettings.ActionBarHidden = liAt == 1; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.Minimap:
                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, UWUserSettings.MinimapHidden ? 1 : 0,
                    liAt => { UWUserSettings.MinimapHidden = liAt == 1; fSaveSettings(); }));
                mOItems.Add(fChoice("North", new[] { "Up", "Turns" }, UWUserSettings.MinimapTurns ? 1 : 0,
                    liAt => { UWUserSettings.MinimapTurns = liAt == 1; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.Heading:
                mOItems.Add(fChoice("Direction", new[] { "Text", "Compass" }, UWUserSettings.ModernCompass ? 1 : 0,
                    liAt => { UWUserSettings.ModernCompass = liAt == 1; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.Compass:
                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, UWUserSettings.ModernCompass ? 0 : 1,
                    liAt => { UWUserSettings.ModernCompass = liAt == 0; fSaveSettings(); }));
                // The free cross or the original's stone disc with the movement arrows (per user, 2026-10-10).
                mOItems.Add(fChoice("Look", new[] { "Cross", "Disc" }, UWUserSettings.ModernCompassDisc ? 1 : 0,
                    liAt => { UWUserSettings.ModernCompassDisc = liAt == 1; fSaveSettings(); }));

                // The disc's black outline, as the original's in the frame (per user, 2026-10-10).
                if (UWModernLayout.IsCompassDisc)
                    mOItems.Add(fChoice("Outline", new[] { "Off", "On" }, UWUserSettings.ModernCompassOutline ? 1 : 0,
                        liAt => { UWUserSettings.ModernCompassOutline = liAt == 1; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.Vitals:
                // The power gem between the flasks (Modern) or on a stand of its own (the original's;
                // per user, 2026-10-09).
                mOItems.Add(fChoice("Power gem", new[] { "Shelf", "Apart" }, UWUserSettings.ModernGemApart ? 1 : 0,
                    liAt => { UWUserSettings.ModernGemApart = liAt == 1; fSaveSettings(); }));
                // The flasks freed one by one (per user, 2026-10-10) - the shelf goes, the gem stands apart.
                mOItems.Add(fChoice("Flasks", new[] { "Shelf", "Apart" }, UWUserSettings.ModernFlasksApart ? 1 : 0,
                    liAt => { UWUserSettings.ModernFlasksApart = liAt == 1; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.Conversation:
                // The original's look (per user, 2026-10-10: "free the conversation UI").
                mOItems.Add(fChoice("Look", new[] { "Modern", "Original" }, UWUserSettings.ModernConversationOriginal ? 1 : 0,
                    liAt => { UWUserSettings.ModernConversationOriginal = liAt == 1; fSaveSettings(); }));

                // The history's and the answers' text size: here only, no more in the game (per
                // user, 2026-10-10).
                int liTextPercent = UWModernConversation.TextPercent;

                mOItems.Add(fStepper("Text", liTextPercent + " %", liTextPercent > UWModernConversation.MinTextPercent,
                    liTextPercent < UWModernConversation.MaxTextPercent, liAt =>
                    {
                        UWUserSettings.ConversationTextPercent = Mathf.Clamp(liTextPercent
                            + (liAt == 0 ? -UWModernConversation.TextPercentStep : UWModernConversation.TextPercentStep),
                            UWModernConversation.MinTextPercent, UWModernConversation.MaxTextPercent);
                        fSaveSettings();
                    }));

                // The original look's black outline, on by default (per user, 2026-10-10).
                if (UWModernLayout.IsConversationOriginal)
                    mOItems.Add(fChoice("Outline", new[] { "On", "Off" }, UWUserSettings.ModernConversationNoOutline ? 1 : 0,
                        liAt => { UWUserSettings.ModernConversationNoOutline = liAt == 1; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.CharacterPage:
                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, 0,
                    liAt => { UWUserSettings.ModernCharacterPage = liAt == 0; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.StatsPanel:
                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, 0,
                    liAt => { UWUserSettings.ModernStatsPanel = liAt == 0; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.RuneTablet:
                // Off brings the rune panel back.
                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, 0,
                    liAt => { UWUserSettings.ModernRuneTablet = liAt == 0; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.RuneHollow:
                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, 0,
                    liAt => { UWUserSettings.ModernRuneHollow = liAt == 0; fSaveSettings(); }));
                // A black outline, as the compass disc's (per user, 2026-10-10).
                mOItems.Add(fChoice("Outline", new[] { "Off", "On" }, UWUserSettings.ModernRuneHollowOutline ? 1 : 0,
                    liAt => { UWUserSettings.ModernRuneHollowOutline = liAt == 1; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.HealthFlask:
            case UWModernLayout.ElementEnum.ManaFlask:
                mOItems.Add(fChoice("Stands", new[] { "Apart", "On shelf" }, 0,
                    liAt => { UWUserSettings.ModernFlasksApart = liAt == 0; fSaveSettings(); }));
                break;

            case UWModernLayout.ElementEnum.PowerGem:
                // Back on the shelf takes the flasks back as well: without them there is none.
                mOItems.Add(fChoice("Stands", new[] { "Apart", "On shelf" }, 0, liAt =>
                {
                    UWUserSettings.ModernGemApart = liAt == 0;

                    if (liAt == 1)
                        UWUserSettings.ModernFlasksApart = false;

                    fSaveSettings();
                }));
                break;

            case UWModernLayout.ElementEnum.StoneShelf:
            {
                // A deco panel (per user, 2026-10-09): shown or not, and its width in the shelf's own
                // pixels, a tile (12) at a time.
                // The edges can be dragged with the mouse too (fGripAt).
                int liMax = UWModernHud.StoneShelfMaxWidth;
                int liWidth = Mathf.Min(UWModernHud.StoneShelfWidth, liMax);

                mOItems.Add(fChoice("Shown", new[] { "On", "Off" }, 0, liAt => { UWUserSettings.ModernStoneShelf = liAt == 0; fSaveSettings(); }));
                mOItems.Add(fStepper("Width", liWidth.ToString(), liWidth > UWDataImport.UWData.UWHudArt.StoneShelfMinWidth, liWidth < liMax, liAt =>
                {
                    UWUserSettings.ModernStoneShelfWidth = Mathf.Clamp(liWidth + (liAt == 0 ? -12 : 12), UWDataImport.UWData.UWHudArt.StoneShelfMinWidth, liMax);
                    fSaveSettings();
                }));
                break;
            }

            case UWModernLayout.ElementEnum.Messages:
                mOItems.Add(fChoice("Style", new[] { "Modern", "Scroll" }, UWUserSettings.ModernScroll ? 1 : 0,
                    liAt => { UWUserSettings.ModernScroll = liAt == 1; fSaveSettings(); }));

                if (UWUserSettings.ModernScroll)
                {
                    int liLines = UWModernScroll.Lines;

                    mOItems.Add(fStepper("Lines", liLines.ToString(), liLines > UWModernScroll.MinLines, liLines < UWModernScroll.MaxLines, liAt =>
                    {
                        UWUserSettings.ModernScrollLines = Mathf.Clamp(liLines + (liAt == 0 ? -1 : 1), UWModernScroll.MinLines, UWModernScroll.MaxLines);
                        fSaveSettings();
                    }));
                }
                break;

            case UWModernLayout.ElementEnum.CharacterPanel:
            case UWModernLayout.ElementEnum.RunePanel:
                bool lbDocked = UWModernLayout.IsDocked(leElement);

                mOItems.Add(fAction("Window", lbDocked ? "Docked" : "Dock", liAt => UWModernLayout.Reset(leElement), !lbDocked));
                break;
        }

        if (fOffersBack(leElement))
            fAddBackItems(leElement);

        mOItems.Add(fAction(string.Empty, "Reset part", liAt => UWModernLayout.Reset(leElement)));
    }

    private Texture2D fTexture(Color pOColour)
    {
        if (mOTextures.TryGetValue(pOColour, out Texture2D lOTexture) && lOTexture != null)
            return lOTexture;

        lOTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernLayoutEditor";
        lOTexture.SetPixel(0, 0, pOColour);
        lOTexture.Apply();
        mOTextures[pOColour] = lOTexture;

        return lOTexture;
    }

    /// <summary>The styles, in the menu bar's look (UWSetupMenu.fEnsureStyles).</summary>
    private void fEnsureStyles()
    {
        if (mOWindowStyle != null)
            return;

        Font lOFont = UWInterfaceFont.Font;
        Color lOButtonHover = new Color(0.30f, 0.30f, 0.35f, 1f);

        mOWindowStyle = new GUIStyle { border = new RectOffset(1, 1, 1, 1) };
        mOWindowStyle.normal.background = fTexture(UWSetupMenu.PanelColour);
        mOWindowStyle.onNormal.background = fTexture(UWSetupMenu.PanelColour);

        mOLabelStyle = new GUIStyle { font = lOFont, fontSize = 15, alignment = TextAnchor.MiddleLeft };
        mOLabelStyle.normal.textColor = UWSetupMenu.TextColour;

        mOTitleStyle = new GUIStyle(mOLabelStyle) { fontSize = 16, padding = new RectOffset((int)WindowPad, 0, 0, 0) };
        mOTitleStyle.normal.textColor = UWSetupMenu.AccentColour;
        mOTitleStyle.normal.background = fTexture(UWSetupMenu.BarColour);

        mOHintStyle = new GUIStyle(mOLabelStyle) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperLeft };
        mOHintStyle.normal.textColor = UWSetupMenu.DimTextColour;

        mOHeadingStyle = new GUIStyle(mOLabelStyle) { fontSize = 16 };
        mOHeadingStyle.normal.textColor = UWSetupMenu.AccentColour;

        mOValueStyle = new GUIStyle(mOLabelStyle) { alignment = TextAnchor.MiddleCenter };

        mOButtonStyle = new GUIStyle(mOLabelStyle) { alignment = TextAnchor.MiddleCenter };
        mOButtonStyle.normal.background = fTexture(UWSetupMenu.HoverColour);
        mOButtonStyle.hover.background = fTexture(lOButtonHover);
        mOButtonStyle.hover.textColor = UWSetupMenu.TextColour;
        mOButtonStyle.active.background = fTexture(UWSetupMenu.AccentColour);
        mOButtonStyle.active.textColor = UWSetupMenu.BackgroundColour;

        // The segmented choice: the one in force in the accent colour.
        mOSegmentStyle = new GUIStyle(mOButtonStyle) { margin = new RectOffset(0, 0, 0, 0) };
        mOSegmentStyle.onNormal.background = fTexture(UWSetupMenu.AccentColour);
        mOSegmentStyle.onNormal.textColor = UWSetupMenu.BackgroundColour;
        mOSegmentStyle.onHover.background = fTexture(UWSetupMenu.AccentColour);
        mOSegmentStyle.onHover.textColor = UWSetupMenu.BackgroundColour;
        mOSegmentStyle.onActive.background = fTexture(UWSetupMenu.AccentColour);
        mOSegmentStyle.onActive.textColor = UWSetupMenu.BackgroundColour;

        mOToggleStyle = new GUIStyle(GUI.skin.toggle) { font = lOFont, fontSize = 15 };
        mOToggleStyle.normal.textColor = UWSetupMenu.TextColour;
        mOToggleStyle.onNormal.textColor = UWSetupMenu.TextColour;
        mOToggleStyle.hover.textColor = UWSetupMenu.TextColour;
        mOToggleStyle.onHover.textColor = UWSetupMenu.TextColour;
        mOToggleStyle.active.textColor = UWSetupMenu.TextColour;
        mOToggleStyle.onActive.textColor = UWSetupMenu.TextColour;
    }

    /// <summary>The height a row of the inspector takes.</summary>
    private static float fItemHeight(InspectorItem pOItem)
    {
        return pOItem.Kind == InspectorKindEnum.Info ? RowHeight * 1.5f : RowHeight;
    }

    private void OnGUI()
    {
        if (!UWModernLayout.IsEditing || mOCanvas == null || !mOCanvas.enabled)
        {
            mOWindowScreenRect = Rect.zero;
            return;
        }

        fEnsureStyles();
        fGatherItems();

        float lfScale = Screen.height / UWSetupMenu.ReferenceHeight;
        float lfScreenWidth = Screen.width / lfScale;

        GUI.matrix = Matrix4x4.Scale(new Vector3(lfScale, lfScale, 1f));

        // The height follows the content: title, help, the editor's three rows, the line, the
        // part's name and its rows.
        float lfHeight = TitleHeight + WindowPad + HintHeight + (3f * (RowHeight + RowGap)) + 9f + RowHeight + RowGap;

        foreach (InspectorItem lOItem in mOItems)
            lfHeight += fItemHeight(lOItem) + RowGap;

        lfHeight += WindowPad - RowGap;

        if (msWindowRect.x < 0f)
            msWindowRect.x = Mathf.Round((lfScreenWidth - WindowWidth) * 0.5f);

        msWindowRect.width = WindowWidth;
        msWindowRect.height = lfHeight;
        msWindowRect = GUI.Window(WindowId, msWindowRect, fDrawWindow, GUIContent.none, mOWindowStyle);

        // Never off the screen.
        msWindowRect.x = Mathf.Clamp(msWindowRect.x, 0f, Mathf.Max(0f, lfScreenWidth - msWindowRect.width));
        msWindowRect.y = Mathf.Clamp(msWindowRect.y, 0f, Mathf.Max(0f, UWSetupMenu.ReferenceHeight - msWindowRect.height));

        mOWindowScreenRect = new Rect(msWindowRect.x * lfScale, Screen.height - (msWindowRect.yMax * lfScale),
            msWindowRect.width * lfScale, msWindowRect.height * lfScale);
    }

    private void fDrawWindow(int piId)
    {
        float lfInner = WindowWidth - (2f * WindowPad);
        float lfX = WindowPad;

        GUI.Label(new Rect(0f, 0f, WindowWidth, TitleHeight), "Edit layout", mOTitleStyle);

        float lfY = TitleHeight + WindowPad;

        GUI.Label(new Rect(lfX, lfY, lfInner, HintHeight),
            "Click a part for its settings. Drag it to move, wheel to size, right click to reset. Drag this title to move the window.",
            mOHintStyle);
        lfY += HintHeight;

        // The UI size: the base every part's size multiplies.
        int liUiPercent = UWModernHud.UiPercent;

        fDrawStepper(lfX, lfY, lfInner, fStepper("UI size", liUiPercent + " %",
            liUiPercent > UWModernHud.MinUiPercent, liUiPercent < UWModernHud.MaxUiPercent,
            liAt => UWModernHud.SetUiPercent(liUiPercent + (liAt == 0 ? -UWModernHud.UiPercentStep : UWModernHud.UiPercentStep))));
        lfY += RowHeight + RowGap;

        float lfThird = lfInner / 3f;
        bool lbPanels = GUI.Toggle(new Rect(lfX, lfY, lfThird, RowHeight), mbPanelsOut, " Panels out", mOToggleStyle);
        bool lbDialog = GUI.Toggle(new Rect(lfX + lfThird, lfY, lfThird, RowHeight), UWModernConversation.Previewing, " Dialog", mOToggleStyle);
        bool lbSnap = GUI.Toggle(new Rect(lfX + (2f * lfThird), lfY, lfThird, RowHeight), mbSnap, " Snap", mOToggleStyle);

        if (lbPanels != mbPanelsOut)
            fSetPanelsOut(lbPanels);

        UWModernConversation.Previewing = lbDialog;
        mbSnap = lbSnap;
        lfY += RowHeight + RowGap;

        float lfHalf = (lfInner - RowGap) * 0.5f;

        if (GUI.Button(new Rect(lfX, lfY, lfHalf, RowHeight), "Reset all", mOButtonStyle))
            UWModernLayout.ResetAll();

        // Done fires on the release (IMGUI buttons do), so the press reaches nothing behind it
        // once the menu is gone (per user, 2026-10-04: Done over the minimap opened the big map).
        if (GUI.Button(new Rect(lfX + lfHalf + RowGap, lfY, lfHalf, RowHeight), "Done", mOButtonStyle) && UWModernHud.Instance != null)
            UWModernHud.Instance.CloseMenu();

        lfY += RowHeight + RowGap;

        // The inspector.
        GUI.DrawTexture(new Rect(0f, lfY + 4f, WindowWidth, 1f), fTexture(UWSetupMenu.SeparatorColour));
        lfY += 9f;

        string lsHeading = miSelected >= 0
            ? UWModernLayout.Names[miSelected] + (UWModernLayout.IsDocked((UWModernLayout.ElementEnum)miSelected) ? " (docked)" : string.Empty)
            : "No part selected";

        GUI.Label(new Rect(lfX, lfY, lfInner, RowHeight), lsHeading, mOHeadingStyle);
        lfY += RowHeight + RowGap;

        foreach (InspectorItem lOItem in mOItems)
        {
            switch (lOItem.Kind)
            {
                case InspectorKindEnum.Info:
                    GUI.Label(new Rect(lfX, lfY, lfInner, fItemHeight(lOItem)), lOItem.Label, mOHintStyle);
                    break;

                case InspectorKindEnum.Stepper:
                    fDrawStepper(lfX, lfY, lfInner, lOItem);
                    break;

                case InspectorKindEnum.Choice:
                    GUI.Label(new Rect(lfX, lfY, LabelWidth, RowHeight), lOItem.Label, mOLabelStyle);
                    GUI.enabled = lOItem.Enabled;

                    int liChosen = GUI.Toolbar(new Rect(lfX + LabelWidth, lfY, lfInner - LabelWidth, RowHeight),
                        Mathf.Clamp(lOItem.Current, 0, lOItem.Choices.Length - 1), lOItem.Choices, mOSegmentStyle);

                    GUI.enabled = true;

                    if (liChosen != lOItem.Current && lOItem.Enabled && lOItem.Choose != null)
                        lOItem.Choose(liChosen);
                    break;

                case InspectorKindEnum.Radio:
                {
                    GUI.Label(new Rect(lfX, lfY, LabelWidth, RowHeight), lOItem.Label, mOLabelStyle);
                    GUI.enabled = lOItem.Enabled;

                    float lfEach = (lfInner - LabelWidth) / Mathf.Max(1, lOItem.Choices.Length);

                    if (lOItem.Previews != null)
                    {
                        fDrawPreviews(lfX + LabelWidth, lfY, lfEach, lOItem);
                        GUI.enabled = true;
                        break;
                    }

                    for (int liAt = 0; liAt < lOItem.Choices.Length; liAt++)
                    {
                        bool lbOn = GUI.Toggle(new Rect(lfX + LabelWidth + (liAt * lfEach), lfY, lfEach, RowHeight), liAt == lOItem.Current, string.Empty, mOToggleStyle);

                        if (lbOn && liAt != lOItem.Current && lOItem.Enabled && lOItem.Choose != null)
                            lOItem.Choose(liAt);
                    }

                    GUI.enabled = true;
                    break;
                }

                case InspectorKindEnum.Action:
                    GUI.Label(new Rect(lfX, lfY, LabelWidth, RowHeight), lOItem.Label, mOLabelStyle);
                    GUI.enabled = lOItem.Enabled;

                    if (GUI.Button(new Rect(lfX + LabelWidth, lfY, lfInner - LabelWidth, RowHeight), lOItem.Choices[0], mOButtonStyle)
                        && lOItem.Choose != null)
                        lOItem.Choose(0);

                    GUI.enabled = true;
                    break;
            }

            lfY += fItemHeight(lOItem) + RowGap;
        }

        GUI.DragWindow(new Rect(0f, 0f, WindowWidth, TitleHeight));
    }

    /// <summary>A radio row's choices as samples (InspectorItem.Previews): each a picture in a frame,
    /// the chosen one framed in the accent colour, dimmed while the row is off; a click chooses.</summary>
    private void fDrawPreviews(float pfX, float pfY, float pfEach, InspectorItem pOItem)
    {
        const float Gap = 3f;
        Color lOOld = GUI.color;

        for (int liAt = 0; liAt < pOItem.Previews.Length; liAt++)
        {
            Rect lOCell = new Rect(pfX + (liAt * pfEach) + Gap, pfY + 2f, pfEach - (2f * Gap), RowHeight - 4f);
            bool lbChosen = liAt == pOItem.Current;
            float lfFrame = lbChosen ? 2f : 1f;

            GUI.color = pOItem.Enabled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(lOCell, fTexture(lbChosen && pOItem.Enabled ? UWSetupMenu.AccentColour : UWSetupMenu.SeparatorColour));

            if (pOItem.Previews[liAt] != null)
                GUI.DrawTexture(new Rect(lOCell.x + lfFrame, lOCell.y + lfFrame, lOCell.width - (2f * lfFrame), lOCell.height - (2f * lfFrame)),
                    pOItem.Previews[liAt], ScaleMode.StretchToFill);

            if (GUI.Button(lOCell, GUIContent.none, GUIStyle.none) && pOItem.Enabled && !lbChosen && pOItem.Choose != null)
                pOItem.Choose(liAt);
        }

        GUI.color = lOOld;
    }

    /// <summary>A row with its name, - , the value and +.</summary>
    private void fDrawStepper(float pfX, float pfY, float pfWidth, InspectorItem pOItem)
    {
        float lfControl = pfWidth - LabelWidth;
        float lfLeft = pfX + LabelWidth;

        GUI.Label(new Rect(pfX, pfY, LabelWidth, RowHeight), pOItem.Label, mOLabelStyle);

        GUI.enabled = pOItem.CanDown;

        if (GUI.Button(new Rect(lfLeft, pfY, StepButtonWidth, RowHeight), "-", mOButtonStyle) && pOItem.Choose != null)
            pOItem.Choose(0);

        GUI.enabled = pOItem.CanUp;

        if (GUI.Button(new Rect(lfLeft + lfControl - StepButtonWidth, pfY, StepButtonWidth, RowHeight), "+", mOButtonStyle) && pOItem.Choose != null)
            pOItem.Choose(1);

        GUI.enabled = true;

        GUI.Label(new Rect(lfLeft + StepButtonWidth, pfY, lfControl - (2f * StepButtonWidth), RowHeight), pOItem.Value, mOValueStyle);
    }

    /// <summary>The pointer (screen pixels, y up) is on the editor's window.</summary>
    private bool fWindowContains(Vector2 pOPointer)
    {
        return mOWindowScreenRect.width > 0f && mOWindowScreenRect.Contains(pOPointer);
    }


    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Modern layout editor", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Over the HUD, the bags, the pointer's thing, the boxes and the conversation's preview (46).
        mOCanvas.sortingOrder = 47;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        RectTransform lORootRect = (RectTransform)lORoot.transform;

        for (int liAt = 0; liAt < UWModernLayout.ElementCount; liAt++)
        {
            mOLines[liAt] = new Image[4];

            for (int liLine = 0; liLine < 4; liLine++)
                mOLines[liAt][liLine] = fCreateImage(lORootRect, "Frame", msGoldDim);

            mOTags[liAt] = fCreateImage(lORootRect, "Tag", Color.black);
            mOTagTexts[liAt] = fCreateText(lORootRect, "Name", TextAnchor.MiddleCenter, msText);
            mOHandles[liAt] = fCreateImage(lORootRect, "Handle", msGold);
            mOPercents[liAt] = fCreateText(lORootRect, "Size", TextAnchor.LowerRight, msText);
        }

        mOGrips[0] = fCreateImage(lORootRect, "Grip left", msGold);
        mOGrips[1] = fCreateImage(lORootRect, "Grip right", msGold);
        mOGuideX = fCreateImage(lORootRect, "Guide x", msGuide);
        mOGuideY = fCreateImage(lORootRect, "Guide y", msGuide);
    }

    private static RawImage fCreateRawImage(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private static Image fCreateImage(Transform pOParent, string psName, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.color = pOColour;
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private Text fCreateText(Transform pOParent, string psName, TextAnchor peAlignment, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Outline));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = mOFont != null ? mOFont : UWInterfaceFont.Font;
        lOText.alignment = peAlignment;
        lOText.color = pOColour;
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline lOOutline = lOObject.GetComponent<Outline>();
        lOOutline.effectColor = new Color(0.16f, 0.09f, 0.04f, 0.9f);
        lOOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return lOText;
    }
}
