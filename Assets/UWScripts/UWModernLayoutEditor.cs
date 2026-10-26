using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

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
/// A PRESET AS A STARTING POINT, SAVED ONLY ON LEAVING (per user, 2026-10-11): "Preset" in the window
/// chooses what is edited - Custom (the own layout as saved), Modern or Classic Modular (their
/// places and switches copied into the own layout, to change from there). Nothing is written while
/// the editor is open (UWUserSettings.SaveSuspended); leaving it with changes asks to save them or to
/// throw them away, which puts the own layout back as it was (a snapshot taken on opening). Classic
/// Wide cannot be a starting point: its frame is no part of the own layout.
///
/// WHAT IS EDITED SHOWS (per user, 2026-10-04: the bags shut with the Escape that leads to the
/// menu, and the closed panels could not be seen): the bags open while the editor is open, and
/// "Panels: out" slides the character panel (Character tab) and the rune panel out - by default
/// they stay in, as the minimap hides behind the open character panel. Both go back as they were.
/// </summary>
public partial class UWModernLayoutEditor : MonoBehaviour
{
    private static readonly Color msGoldDim = UWModernPalette.WithAlpha(UWModernPalette.Gold, 0.6f);

    private static readonly Color msGuide = new Color(0.47f, 0.78f, 1f, 0.9f);

    /// <summary>Screen pixels within which an edge snaps.</summary>
    private const float SnapDistance = 10f;

    private UWGameUI mOUi;

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

    /// <summary>The message box's top edge being dragged for its lines (per user, 2026-10-21: as
    /// Shift and the wheel), the lines when the drag began and the screen pixels of one line.</summary>
    private bool mbLining;

    private int miLinesFrom;

    private float mfLinePixels;

    /// <summary>The mouse pointer the editor shows (per user, 2026-10-10, Kenney's arrows): the
    /// left-right arrow over and on the stone shelf's grips, the up-down arrow on the message
    /// box's top edge, the four-way arrow over a part that moves and while moving it, else the
    /// system's own.</summary>
    private enum PointerEnum
    {
        System,
        LeftRight,
        UpDown,
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

    public static UWModernLayoutEditor Instance { get; private set; }

    /// <summary>What "Preset" shows as chosen: the own layout, or the preset copied into it (the
    /// starting points and their values are UWLayoutPresets' since cleanup step 8.4).</summary>
    private UWDataImport.UWData.UWLayoutStart meStart;

    /// <summary>The own layout as saved, taken when the editor opens.</summary>
    private UWUserSettings.LayoutSnapshot mOSaved;

    /// <summary>Leaving waits for the save question; what to do once it is answered.</summary>
    private System.Action mOLeave;

    private void Awake()
    {
        Instance = this;
        mOKitRows = new UWKitRows(transform, UWUiSorting.LayoutEditorWindow, UWRowLookEnum.LayoutEditor);
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        fShowPointer(PointerEnum.System);

        mOPreviews.ReleaseAll();

        if (mOKitRows != null)
            mOKitRows.Dispose();
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

        if (!lbEditing || !UWPointer.HasMouse || mOLeave != null)
        {
            miDragged = -1;
            return;
        }

        Vector2 lOPointer = UWPointer.Position;

        if (miDragged >= 0)
        {
            if (UWPointer.LeftHeld)
                fDrag(lOPointer);
            else
            {
                miDragged = -1;
                UWModernLayout.Save();

                if (miWidening != 0 || mbLining)
                {
                    miWidening = 0;
                    mbLining = false;
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

        if (UWPointer.LeftPressed)
        {
            int liGrip = fGripAt(lOPointer);

            if (liGrip != 0 && UWModernLayout.TryGetRect(UWModernLayout.ElementEnum.StoneShelf, out mODragRect))
            {
                miSelected = (int)UWModernLayout.ElementEnum.StoneShelf;
                miDragged = miSelected;
                mbSizing = false;
                miWidening = liGrip;
                mODragFrom = lOPointer;
                mfShelfPixel = mODragRect.width / Mathf.Min(UWModernStoneShelf.StoneShelfWidth, UWModernStoneShelf.StoneShelfMaxWidth);
                return;
            }

            if (fLinesEdgeAt(lOPointer) && UWModernLayout.TryGetRect(UWModernLayout.ElementEnum.Messages, out mODragRect))
            {
                miSelected = (int)UWModernLayout.ElementEnum.Messages;
                miDragged = miSelected;
                mbSizing = false;
                mbLining = true;
                mODragFrom = lOPointer;
                miLinesFrom = fMessageLines();
                mfLinePixels = mODragRect.height / Mathf.Max(1, fMessageRows());
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

        if (UWPointer.RightPressed)
        {
            int liElement = fElementAt(lOPointer, out bool _);

            if (liElement >= 0)
                UWModernLayout.Reset((UWModernLayout.ElementEnum)liElement);

            return;
        }

        float lfWheel = UWPointer.Scroll.y;

        if (Mathf.Abs(lfWheel) > 0.01f)
        {
            int liElement = fElementAt(lOPointer, out bool _);
            UWModernLayout.ElementEnum leElement = (UWModernLayout.ElementEnum)liElement;

            // Shift and the wheel on the original's scroll: its lines, the paper extended in its
            // middle (UWModernScroll, per user 2026-10-09); on the dark box its lines likewise.
            if (liElement >= 0 && leElement == UWModernLayout.ElementEnum.Messages
                && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed)
            {
                fSetMessageLines(fMessageLines() + (lfWheel > 0f ? 1 : -1));
                UWUserSettings.Save();
            }
            else if (liElement >= 0 && UWModernLayout.TryGetRect(leElement, out Rect lORect))
                UWModernLayout.SetPercent(leElement, UWModernLayout.Percent(leElement)
                    + (lfWheel > 0f ? UWModernLayout.PercentStep : -UWModernLayout.PercentStep), lORect);
        }
    }

    /// <summary>The modern message box one line taller or shorter (Interaction.ModernMessageCount).</summary>
    private static void fStepModernMessageLines(int piStep)
    {
        UWUserSettings.ModernMessageLines = Mathf.Clamp(Interaction.ModernMessageCount + piStep,
            UWModernScroll.MinLines, Interaction.MessageHistoryCount);
    }

    /// <summary>The messages' line setting of the style shown: the scroll's or the dark box's.</summary>
    private static int fMessageLines()
    {
        return UWModernLayout.IsScrollShown ? UWModernScroll.Lines : Interaction.ModernMessageCount;
    }

    /// <summary>The rows the style shows for it - the scroll's paper has one more (UWModernScroll.Rows).</summary>
    private static int fMessageRows()
    {
        return UWModernLayout.IsScrollShown ? UWModernScroll.Rows : Interaction.ModernMessageCount;
    }

    private static void fSetMessageLines(int piLines)
    {
        if (UWModernLayout.IsScrollShown)
            UWUserSettings.ModernScrollLines = Mathf.Clamp(piLines, UWModernScroll.MinLines, UWModernScroll.MaxLines);
        else
            UWUserSettings.ModernMessageLines = Mathf.Clamp(piLines, UWModernScroll.MinLines, Interaction.MessageHistoryCount);
    }

    /// <summary>The pointer on the message box's top edge, a little beyond it either way.</summary>
    private bool fLinesEdgeAt(Vector2 pOPointer)
    {
        if (!UWModernLayout.TryGetRect(UWModernLayout.ElementEnum.Messages, out Rect lORect))
            return false;

        float lfReach = 3f * UWModernUi.PixelScale;

        return new Rect(lORect.x, lORect.yMax - lfReach, lORect.width, 2f * lfReach).Contains(pOPointer);
    }

    /// <summary>The top edge dragged: a line per line height the pointer moved, up taller.</summary>
    private void fDragLines(float pfDeltaY)
    {
        int liLines = miLinesFrom + Mathf.RoundToInt(pfDeltaY / Mathf.Max(1f, mfLinePixels));

        if (liLines != fMessageLines())
            fSetMessageLines(liLines);
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
            : pePointer == PointerEnum.LeftRight ? UWGlyphs.ArrowLeftRightTile
            : pePointer == PointerEnum.UpDown ? UWGlyphs.ArrowUpDownTile : UWGlyphs.ArrowFourWaysTile;
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

        if (mbLining)
        {
            fDragLines(lODelta.y);
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
            float lfMargin = 4f * UWModernUi.PixelScale;
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
            UWDataImport.Presentation.UWHudArt.StoneShelfMinWidth, UWModernStoneShelf.StoneShelfMaxWidth);
        float lfWidth = liWidth * mfShelfPixel;

        UWUserSettings.ModernStoneShelfWidth = liWidth;
        UWModernLayout.Move(UWModernLayout.ElementEnum.StoneShelf,
            new Rect(miWidening > 0 ? mODragRect.xMin : mODragRect.xMax - lfWidth, mODragRect.y, lfWidth, mODragRect.height), false);
    }

    /// <summary>The nearest of the part's edges and centre to the screen's margins and centre and
    /// to the other parts' edges and centres, within SnapDistance - per axis.</summary>
    private Rect fSnap(Rect pORect)
    {
        float lfMargin = 4f * UWModernUi.PixelScale;
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

        // The session: the own layout as saved, nothing written until it is left.
        mOSaved = UWUserSettings.TakeLayoutSnapshot();
        UWUserSettings.SaveSuspended = true;
        meStart = UWDataImport.UWData.UWLayoutStart.Custom;
        mOLeave = null;
    }

    private void fEnd()
    {
        UWModernConversation.Previewing = false;

        // Left without the question (the menu closed from outside): the changes go.
        if (UWUserSettings.SaveSuspended)
            fFinish(false);

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

            // The window goes with the frames (cleanup 8b.4).
            mOKitRows.Begin(false, false, false, false);
            mOWindowScreenRect = Rect.zero;

            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        float lfScale = UWModernUi.PixelScale;
        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);
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
            Color lOColour = lbActive ? UWModernPalette.LightText : msGoldDim;
            float lfLine = Mathf.Max(2f, lfScale * (lbActive ? 0.75f : 0.5f));

            UWUiKit.SetRect(mOLines[liAt][0].rectTransform, lORect.x, lORect.yMax, lORect.width, lfLine);
            UWUiKit.SetRect(mOLines[liAt][1].rectTransform, lORect.x, lORect.y - lfLine, lORect.width, lfLine);
            UWUiKit.SetRect(mOLines[liAt][2].rectTransform, lORect.x - lfLine, lORect.y - lfLine, lfLine, lORect.height + (2f * lfLine));
            UWUiKit.SetRect(mOLines[liAt][3].rectTransform, lORect.xMax, lORect.y - lfLine, lfLine, lORect.height + (2f * lfLine));

            foreach (Image lOLine in mOLines[liAt])
                lOLine.color = lOColour;

            // The name on a tag above the part - inside it at the screen's top.
            Text lOTag = mOTagTexts[liAt];

            lOTag.text = UWModernLayout.IsDocked(leElement) ? UWText.F("editor.part.docked", UWModernLayout.Name(liAt)) : UWModernLayout.Name(liAt);
            lOTag.fontSize = Mathf.Max(10, Mathf.RoundToInt(3.8f * lfScale));
            lOTag.color = lbActive ? UWModernPalette.LightText : UWModernPalette.Text;

            float lfTagWidth = lOTag.preferredWidth + (4f * lfScale);
            float lfTagHeight = lOTag.fontSize * 1.5f;
            float lfTagY = lORect.yMax + lfLine + lfTagHeight <= Screen.height ? lORect.yMax + lfLine : lORect.yMax - lfTagHeight;

            UWUiKit.SetRect(mOTags[liAt].rectTransform, lORect.x, lfTagY, lfTagWidth, lfTagHeight);
            UWUiKit.SetRect(lOTag.rectTransform, lORect.x, lfTagY, lfTagWidth, lfTagHeight);
            mOTags[liAt].color = new Color(0.12f, 0.08f, 0.05f, 0.85f);

            // The handle and the size.
            float lfHandle = 5f * lfScale;
            Rect lOHandle = new Rect(lORect.xMax - (lfHandle * 0.6f), lORect.y - (lfHandle * 0.4f), lfHandle, lfHandle);

            mOHandleRects[liAt] = lOHandle;
            UWUiKit.SetRect(mOHandles[liAt].rectTransform, lOHandle.x, lOHandle.y, lOHandle.width, lOHandle.height);
            mOHandles[liAt].color = lbActive ? UWModernPalette.LightText : UWModernPalette.Gold;

            mOPercents[liAt].text = UWModernLayout.Percent(leElement) + " %";
            mOPercents[liAt].fontSize = Mathf.Max(9, Mathf.RoundToInt(3.6f * lfScale));
            UWUiKit.SetRect(mOPercents[liAt].rectTransform, lORect.x, lORect.y + lfScale, lORect.width - lfHandle, mOPercents[liAt].fontSize * 1.4f);
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
            UWUiKit.SetRect(mOGrips[liSide].rectTransform, lfEdge - (lfBar * 0.5f), lOShelf.y + (lOShelf.height * 0.2f), lfBar, lOShelf.height * 0.6f);
            mOGrips[liSide].color = liGripHovered == (liSide == 0 ? -1 : 1) ? UWModernPalette.LightText : UWModernPalette.Gold;
        }

        // The pointer: what a press here would do.
        PointerEnum lePointer = PointerEnum.System;

        if (miDragged >= 0)
            lePointer = miWidening != 0 ? PointerEnum.LeftRight : mbLining ? PointerEnum.UpDown : (mbSizing ? PointerEnum.System : PointerEnum.FourWays);
        else if (!fWindowContains(lOPointer))
        {
            if (fGripAt(lOPointer) != 0)
                lePointer = PointerEnum.LeftRight;
            else if (fLinesEdgeAt(lOPointer))
                lePointer = PointerEnum.UpDown;
            else if (fElementAt(lOPointer, out bool lbHandle) >= 0 && !lbHandle)
                lePointer = PointerEnum.FourWays;
        }

        fShowPointer(lePointer);

        // The snapping guides while dragging.
        mOGuideX.enabled = miDragged >= 0 && !mbSizing && mfGuideX >= 0f;
        mOGuideY.enabled = miDragged >= 0 && !mbSizing && mfGuideY >= 0f;

        if (mOGuideX.enabled)
            UWUiKit.SetRect(mOGuideX.rectTransform, mfGuideX - 1f, 0f, 2f, Screen.height);

        if (mOGuideY.enabled)
            UWUiKit.SetRect(mOGuideY.rectTransform, 0f, mfGuideY - 1f, Screen.width, 2f);

        // The editor's window over the frames, on the kit (cleanup 8b.4).
        fDrawKitWindow();
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
    ///
    /// ON THE KIT (cleanup 8b.4, 2026-10-24; phase 8b, decision 26): the window and its save question
    /// are drawn by UWKitRows in the editor's look (UWRowLookEnum.LayoutEditor) on a canvas of their
    /// own over the editor's frames (UWUiSorting.LayoutEditorWindow), in LateUpdate after them - the
    /// same rows, rectangles and order as the IMGUI form (UWGuiRows' Window, ModalWindow and DragBy in
    /// OnGUI), which went with the side-by-side switch at the end of phase 8b. Sized by text on the
    /// kit: the label column, the window's width and the hints' heights grow where the language needs
    /// it; in English they stand as laid out.
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

    /// <summary>Also the title row's text inset (UWKitRows.Title in the editor's look).</summary>
    internal const float WindowPad = 10f;

    private const float TitleHeight = 26f;

    private const float HintHeight = 38f;

    private const float RowHeight = 26f;

    private const float RowGap = 4f;

    private const float LabelWidth = 110f;

    private const float StepButtonWidth = 26f;

    private static Rect msWindowRect = new Rect(-1f, 70f, WindowWidth, 200f);

    private int miSelected = -1;

    private readonly System.Collections.Generic.List<InspectorItem> mOItems = new System.Collections.Generic.List<InspectorItem>();

    /// <summary>The window's rows on the kit (cleanup 8b.4), in the editor's look; no arrow-key
    /// navigation here (cleanup step 8.4 drew them through UWGuiRows, the one place that called GUI.*
    /// for them, as for the setup bar).</summary>
    private UWKitRows mOKitRows;

    /// <summary>The window's width, its label column and its hint's height in this pass (fLayOut):
    /// WindowWidth, LabelWidth and HintHeight as laid out, wider or taller on the kit where the text
    /// needs it (cleanup 8b.4: the pseudo-language must not clip).</summary>
    private float mfWindowWidth = WindowWidth;

    private float mfLabelWidth = LabelWidth;

    private float mfHintHeight = HintHeight;

    /// <summary>The save question's width and its text's height in this pass, likewise.</summary>
    private float mfAskWidth = AskWidth;

    private float mfAskTextHeight = AskTextHeight;

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

    /// <summary>The samples of the backs' colours and patterns, built once per colour help (the
    /// stamp, UWArtCache: until 2026-10-19 the colour help's version was part of the key and the
    /// old samples stayed).</summary>
    private readonly UWArtCache<int> mOPreviews = new UWArtCache<int>();

    /// <summary>The size of a sample in the back's own pixels.</summary>
    private const int PreviewWidth = 30;

    private const int PreviewHeight = 12;

    /// <summary>A sample of a back in this colour and pattern: a rectangle without a border.</summary>
    private Texture2D fBackPreview(int piColour, int piPattern)
    {
        int liKey = (piColour * 10) + piPattern;
        UWDataImport.Presentation.UWArtStamp lOStamp = UWVersionedTexture.Colours();

        if (mOPreviews.TryGet(liKey, lOStamp, out Texture2D lOTexture))
            return lOTexture;

        if (mOUi == null || mOUi.mOUWData == null)
            return null;

        UWModernBacks.Back lOBack = new UWModernBacks.Back
        {
            Shape = UWDataImport.Presentation.UWBackdropArt.ShapeEnum.Rect,
            Colour = (UWDataImport.Presentation.UWBackdropArt.ColourEnum)piColour,
            Pattern = (UWDataImport.Presentation.UWBackdropArt.PatternEnum)piPattern,
            Border = false
        };

        return mOPreviews.Set(liKey, lOStamp,
            UWModernHudArt.BuildBackdrop(mOUi.mOUWData.Textures, PreviewWidth, PreviewHeight, lOBack, FilterMode.Point, piColour + 1));
    }

    /// <summary>The height a row of the inspector takes: an Info line half a row more - on the kit as
    /// tall as its wrapped text where that is more (cleanup 8b.4).</summary>
    private static float fItemHeight(IUWRows pORows, InspectorItem pOItem, float pfInner)
    {
        if (pOItem.Kind != InspectorKindEnum.Info)
            return RowHeight;

        float lfHeight = RowHeight * 1.5f;

        return pORows.SizesByText ? Mathf.Max(lfHeight, pORows.TextHeight(pOItem.Label, UWRowInfoEnum.Dim, pfInner)) : lfHeight;
    }

    /// <summary>The UI size's row: the base every part's size multiplies.</summary>
    private static InspectorItem fUiSizeItem()
    {
        int liUiPercent = UWModernUi.UiPercent;

        return fStepper(UWDataImport.UWData.UWUserSettingsTable.ModernUiPercent, liUiPercent,
            liUiPercent > UWModernUi.MinUiPercent, liUiPercent < UWModernUi.MaxUiPercent,
            liAt => UWModernUi.SetUiPercent(liUiPercent + (liAt == 0 ? -UWModernUi.UiPercentStep : UWModernUi.UiPercentStep)));
    }

    /// <summary>
    /// The window's size for this pass (both forms): its height follows the content - title, help,
    /// the editor's four rows, the line, the part's name and its rows; its width, its label column
    /// and the hint as laid out in the IMGUI form, on the kit wider or taller where the language
    /// needs it (cleanup 8b.4: a label wider than the column widens it, the rows' choices, buttons,
    /// steppers, the Preset segments, the three switches and the heading widen the window). First
    /// shown, it stands in the middle of the screen.
    /// </summary>
    private void fLayOut(IUWRows pORows, float pfScreenWidth)
    {
        mfWindowWidth = WindowWidth;
        mfLabelWidth = LabelWidth;
        mfHintHeight = HintHeight;

        if (pORows.SizesByText)
        {
            float lfShift = UWRowSizing.LabelShift(pORows.LabelWidth(fUiSizeItem().Label), LabelWidth);

            foreach (InspectorItem lOItem in mOItems)
            {
                if (lOItem.Kind != InspectorKindEnum.Info && !string.IsNullOrEmpty(lOItem.Label))
                    lfShift = Mathf.Max(lfShift, UWRowSizing.LabelShift(pORows.LabelWidth(lOItem.Label), LabelWidth));
            }

            mfLabelWidth = LabelWidth + lfShift;
            mfWindowWidth = Mathf.Max(WindowWidth, fInnerNeeded(pORows) + (2f * WindowPad));
            mfHintHeight = Mathf.Max(HintHeight, pORows.TextHeight(UWText.T("editor.hint"), UWRowInfoEnum.Dim, mfWindowWidth - (2f * WindowPad)));
        }

        float lfInner = mfWindowWidth - (2f * WindowPad);
        float lfHeight = TitleHeight + WindowPad + mfHintHeight + (4f * (RowHeight + RowGap)) + 9f + RowHeight + RowGap;

        foreach (InspectorItem lOItem in mOItems)
            lfHeight += fItemHeight(pORows, lOItem, lfInner) + RowGap;

        lfHeight += WindowPad - RowGap;

        if (msWindowRect.x < 0f)
            msWindowRect.x = Mathf.Round((pfScreenWidth - mfWindowWidth) * 0.5f);

        msWindowRect.width = mfWindowWidth;
        msWindowRect.height = lfHeight;
    }

    /// <summary>The width the window's rows need inside its margins on the kit (fLayOut), at least
    /// the inner width it was laid out with.</summary>
    private float fInnerNeeded(IUWRows pORows)
    {
        float lfNeed = Mathf.Max(WindowWidth - (2f * WindowPad), fRowNeeded(pORows, fUiSizeItem()));

        foreach (InspectorItem lOItem in mOItems)
            lfNeed = Mathf.Max(lfNeed, fRowNeeded(pORows, lOItem));

        // Preset: its label and the segments, their names and a margin each (UWRowSizing.SpreadNeeded).
        string[] lsNames = UWDataImport.UWData.UWLayoutPresets.StartNames;
        float[] laNames = new float[lsNames.Length];

        for (int liAt = 0; liAt < lsNames.Length; liAt++)
            laNames[liAt] = pORows.LabelWidth(lsNames[liAt]);

        float lfPreset = pORows.LabelWidth(UWText.T("editor.preset")) + RowGap + UWRowSizing.SpreadNeeded(laNames);

        // The three switches in thirds, Reset all and Done in halves.
        float lfSwitch = Mathf.Max(pORows.ToggleWidth(" " + UWText.T("editor.panels_out")),
            Mathf.Max(pORows.ToggleWidth(" " + UWText.T("editor.dialog")), pORows.ToggleWidth(" " + UWText.T("editor.snap")))) + UWRowSizing.Margin;
        float lfButton = Mathf.Max(pORows.FitWidth(UWText.T("editor.reset_all"), 0f), pORows.FitWidth(UWText.T("editor.done"), 0f));

        lfNeed = Mathf.Max(lfNeed, Mathf.Max(lfPreset, Mathf.Max(3f * lfSwitch, (2f * lfButton) + RowGap)));

        return Mathf.Max(lfNeed, pORows.TextWidth(fHeading(), UWRowInfoEnum.Heading));
    }

    /// <summary>What a row with a label needs: the label column and the control right of it - the
    /// choices each their name and padding, a button its text and padding, a stepper its two buttons
    /// and its value.</summary>
    private float fRowNeeded(IUWRows pORows, InspectorItem pOItem)
    {
        float lfPadding = 2f * UWRowSizing.Padding;

        switch (pOItem.Kind)
        {
            case InspectorKindEnum.Choice:
            {
                float lfChoices = 0f;

                foreach (string lsChoice in pOItem.Choices)
                    lfChoices += pORows.LabelWidth(lsChoice) + lfPadding;

                return mfLabelWidth + lfChoices;
            }

            case InspectorKindEnum.Stepper:
                return mfLabelWidth + (2f * StepButtonWidth) + pORows.LabelWidth(pOItem.Value) + lfPadding;

            case InspectorKindEnum.Action:
                return mfLabelWidth + pORows.FitWidth(pOItem.Choices[0], 0f);

            default:
                return 0f;
        }
    }

    /// <summary>The inspector's heading: the selected part's name, or that nothing is selected.</summary>
    private string fHeading()
    {
        return miSelected >= 0
            ? (UWModernLayout.IsDocked((UWModernLayout.ElementEnum)miSelected) ? UWText.F("editor.part.docked", UWModernLayout.Name(miSelected))
                : UWModernLayout.Name(miSelected))
            : UWText.T("editor.no_selection");
    }

    /// <summary>Never off the screen.</summary>
    private static void fKeepOnScreen(float pfScreenWidth)
    {
        msWindowRect.x = Mathf.Clamp(msWindowRect.x, 0f, Mathf.Max(0f, pfScreenWidth - msWindowRect.width));
        msWindowRect.y = Mathf.Clamp(msWindowRect.y, 0f, Mathf.Max(0f, UWGuiTheme.ReferenceHeight - msWindowRect.height));
    }

    /// <summary>The window in screen pixels with y up, for Update to leave presses on it alone.</summary>
    private void fSetWindowScreenRect(float pfScale)
    {
        mOWindowScreenRect = new Rect(msWindowRect.x * pfScale, Screen.height - (msWindowRect.yMax * pfScale),
            msWindowRect.width * pfScale, msWindowRect.height * pfScale);
    }

    /// <summary>
    /// THE WINDOW ON THE KIT (cleanup 8b.4, 2026-10-24), after the frames in LateUpdate: laid out as the
    /// IMGUI form was (fLayOut), dragged by its title row and kept on the screen as GUI.Window and
    /// DragWindow did, its rows (fDrawWindowRows) in the window's own coordinates; the save question
    /// over it in the middle of the screen, MODAL as IMGUI's ModalWindow: while it is up the window
    /// takes no pointer (UWKitRows.PointerWithheld) and is drawn greyed as before.
    /// </summary>
    private void fDrawKitWindow()
    {
        mOKitRows.Begin(true, false, false, false);

        fGatherItems();

        float lfScreenWidth = mOKitRows.Width;

        fLayOut(mOKitRows, lfScreenWidth);

        mOKitRows.PointerWithheld = mOLeave != null;

        msWindowRect.position = mOKitRows.Drag(new Rect(msWindowRect.x, msWindowRect.y, msWindowRect.width, TitleHeight), msWindowRect.position,
            Vector2.zero, new Vector2(Mathf.Max(0f, lfScreenWidth - msWindowRect.width), Mathf.Max(0f, UWGuiTheme.ReferenceHeight - msWindowRect.height)));
        fKeepOnScreen(lfScreenWidth);
        fSetWindowScreenRect(UWGuiTheme.Scale);

        mOKitRows.Frame(msWindowRect);
        mOKitRows.BeginWindow(msWindowRect);
        fDrawWindowRows(mOKitRows);
        mOKitRows.EndWindow();

        mOKitRows.Enabled = true;
        mOKitRows.PointerWithheld = false;

        // The save question over everything. THE KEYS of a dialog (per user, 2026-10-14: on Linux
        // neither a click nor a key got out of this question): Enter saves, Escape goes back to
        // editing. Read from the Input System (as IMGUI's own events did not reliably reach the
        // game's windows there), once a frame; the Escape that opened the question was taken in an
        // earlier frame (UWModernGameMenu.ToggleMenu, RequestLeave).
        if (mOLeave != null)
        {
            Keyboard lOKeys = Keyboard.current;
            bool lbEnter = lOKeys != null && (lOKeys.enterKey.wasPressedThisFrame || lOKeys.numpadEnterKey.wasPressedThisFrame);
            bool lbEscape = lOKeys != null && Time.frameCount != miLeaveAskedFrame && UWBack.Takes(UWBackOwner.LayoutAsk);
            Rect lOAsk = fAskRect(mOKitRows, lfScreenWidth);

            mOKitRows.Frame(lOAsk);
            mOKitRows.BeginWindow(lOAsk);
            fDrawAskRows(mOKitRows, lbEnter, lbEscape);
            mOKitRows.EndWindow();
        }

        mOKitRows.End();
    }

    private const float AskWidth = 300f;

    /// <summary>The question's text, between its title and its buttons.</summary>
    private const float AskTextHeight = 44f;

    /// <summary>The save question in the middle of the screen: 300 wide as laid out, on the kit wider
    /// where its buttons' texts need it and taller where its text does (cleanup 8b.4).</summary>
    private Rect fAskRect(IUWRows pORows, float pfScreenWidth)
    {
        mfAskWidth = AskWidth;
        mfAskTextHeight = AskTextHeight;

        if (pORows.SizesByText)
        {
            float lfButton = Mathf.Max(pORows.FitWidth(UWText.T("editor.ask.save"), 0f),
                Mathf.Max(pORows.FitWidth(UWText.T("editor.ask.discard"), 0f), pORows.FitWidth(UWText.T("editor.ask.back"), 0f)));

            mfAskWidth = Mathf.Max(AskWidth, (3f * lfButton) + (2f * RowGap) + (2f * WindowPad));
            mfAskTextHeight = Mathf.Max(AskTextHeight,
                pORows.TextHeight(UWText.T("editor.ask.text"), UWRowInfoEnum.Dim, mfAskWidth - (2f * WindowPad)));
        }

        float lfHeight = TitleHeight + WindowPad + mfAskTextHeight + RowHeight + WindowPad;

        return new Rect((pfScreenWidth - mfAskWidth) * 0.5f, (UWGuiTheme.ReferenceHeight - lfHeight) * 0.5f, mfAskWidth, lfHeight);
    }

    /// <summary>The save question: Save, Discard (the own layout as it was), or back to editing;
    /// pbEnter saves, pbEscape goes back to editing.</summary>
    private void fDrawAskRows(IUWRows pORows, bool pbEnter, bool pbEscape)
    {
        float lfInner = mfAskWidth - (2f * WindowPad);

        pORows.Title(new Rect(0f, 0f, mfAskWidth, TitleHeight), UWText.T("editor.ask.title"));
        pORows.Info(new Rect(WindowPad, TitleHeight + WindowPad, lfInner, mfAskTextHeight),
            UWText.T("editor.ask.text"), UWRowInfoEnum.Dim);

        float lfY = TitleHeight + WindowPad + mfAskTextHeight;
        float lfThird = (lfInner - (2f * RowGap)) / 3f;
        System.Action lOLeave = mOLeave;

        if (pbEscape)
        {
            mOLeave = null;
        }
        else if (pbEnter || pORows.Action(new Rect(WindowPad, lfY, lfThird, RowHeight), UWText.T("editor.ask.save")))
        {
            mOLeave = null;
            fFinish(true);
            lOLeave();
        }
        else if (pORows.Action(new Rect(WindowPad + lfThird + RowGap, lfY, lfThird, RowHeight), UWText.T("editor.ask.discard")))
        {
            mOLeave = null;
            fFinish(false);
            lOLeave();
        }
        else if (pORows.Action(new Rect(WindowPad + (2f * (lfThird + RowGap)), lfY, lfThird, RowHeight), UWText.T("editor.ask.back")))
        {
            mOLeave = null;
        }
    }

    /// <summary>
    /// Leaving the editor (Done, Escape): at once when nothing changed, else after the save
    /// question; pOLeave is what the leaving does (close the menu, back to its main page).
    /// </summary>
    public void RequestLeave(System.Action pOLeave)
    {
        if (mOLeave != null || pOLeave == null)
            return;

        if (UWUserSettings.LayoutEquals(mOSaved))
        {
            fFinish(true);

            pOLeave();
            return;
        }

        mOLeave = pOLeave;
        miLeaveAskedFrame = Time.frameCount;
    }

    /// <summary>The frame the save question came up in: the Escape that asked it is not its answer.</summary>
    private int miLeaveAskedFrame = -1;

    /// <summary>The save question is up - UWBack: its Escape is the question's alone (cleanup 7, wave 2b;
    /// until then the game menu took it as well, and its leave did nothing only because UWGameUI's
    /// Update came before the question's LateUpdate).</summary>
    public static bool IsAskingLeave => Instance != null && Instance.mOLeave != null;

    /// <summary>Ends the session: writes the settings, or first puts the own layout back.</summary>
    private void fFinish(bool pbSave)
    {
        if (!pbSave)
            fRestore(mOSaved);

        UWUserSettings.SaveSuspended = false;
        UWUserSettings.Save();
    }

    private static void fRestore(UWUserSettings.LayoutSnapshot pOSnapshot)
    {
        UWUserSettings.RestoreLayout(pOSnapshot);
        UWModernLayout.Reload();
        UWModernBacks.Reloaded();
    }

    /// <summary>
    /// "Preset": the own layout as saved, or a preset copied into it - Modern its default places and
    /// no original pieces, Classic Modular its places, pieces and looks (UWModernClassicPlus), the
    /// backs cleared as it has none. The UI size and the font stay as they are. The values are the
    /// preset table's (UWLayoutPresets, cleanup step 8.4; until then twenty lines here).
    /// </summary>
    private void fChooseStart(UWDataImport.UWData.UWLayoutStart peStart)
    {
        meStart = peStart;
        fRestore(mOSaved);

        if (peStart == UWDataImport.UWData.UWLayoutStart.Custom)
            return;

        UWDataImport.UWData.UWLayoutPresets.Apply(peStart, UWUserSettings.Data);

        UWModernLayout.Reload();
        UWModernBacks.Reloaded();

        // The shelf as wide as the screen leaves beside the stats panel (its size read from the
        // layout just set).
        if (peStart == UWDataImport.UWData.UWLayoutStart.ClassicModular)
            UWUserSettings.ModernStoneShelfWidth = Mathf.Max(UWDataImport.Presentation.UWHudArt.StoneShelfMinWidth,
                UWModernStoneShelf.StoneShelfMaxWidth - UWDataImport.UWData.UWTextures.PanelWidth);

        miSelected = -1;
    }

    /// <summary>The window's rows in its own coordinates, through the drawer (cleanup 8b.4: the kit's,
    /// until the end of phase 8b also the IMGUI form's - one code for both).</summary>
    private void fDrawWindowRows(IUWRows pORows)
    {
        float lfInner = mfWindowWidth - (2f * WindowPad);
        float lfX = WindowPad;

        pORows.Enabled = mOLeave == null;

        pORows.Title(new Rect(0f, 0f, mfWindowWidth, TitleHeight), UWText.T("editor.title"));

        float lfY = TitleHeight + WindowPad;

        pORows.Info(new Rect(lfX, lfY, lfInner, mfHintHeight),
            UWText.T("editor.hint"),
            UWRowInfoEnum.Dim);
        lfY += mfHintHeight;

        // The UI size: the base every part's size multiplies.
        fDrawStepper(pORows, lfX, lfY, lfInner, fUiSizeItem());
        lfY += RowHeight + RowGap;

        // The starting point: the own layout, or a preset copied into it. The segments as wide as
        // their names need (per user, 2026-10-11: "Classic Modular" did not fit a third).
        float lfLabel = pORows.LabelWidth(UWText.T("editor.preset")) + RowGap;

        pORows.Label(new Rect(lfX, lfY, lfLabel, RowHeight), UWText.T("editor.preset"));

        int liStart = pORows.Segments(new Rect(lfX + lfLabel, lfY, lfInner - lfLabel, RowHeight), (int)meStart, UWDataImport.UWData.UWLayoutPresets.StartNames);

        if (liStart != (int)meStart)
            fChooseStart((UWDataImport.UWData.UWLayoutStart)liStart);

        lfY += RowHeight + RowGap;

        float lfThird = lfInner / 3f;
        bool lbPanels = pORows.Toggle(new Rect(lfX, lfY, lfThird, RowHeight), mbPanelsOut, " " + UWText.T("editor.panels_out"));
        bool lbDialog = pORows.Toggle(new Rect(lfX + lfThird, lfY, lfThird, RowHeight), UWModernConversation.Previewing, " " + UWText.T("editor.dialog"));
        bool lbSnap = pORows.Toggle(new Rect(lfX + (2f * lfThird), lfY, lfThird, RowHeight), mbSnap, " " + UWText.T("editor.snap"));

        if (lbPanels != mbPanelsOut)
            fSetPanelsOut(lbPanels);

        UWModernConversation.Previewing = lbDialog;
        mbSnap = lbSnap;
        lfY += RowHeight + RowGap;

        float lfHalf = (lfInner - RowGap) * 0.5f;

        if (pORows.Action(new Rect(lfX, lfY, lfHalf, RowHeight), UWText.T("editor.reset_all")))
            UWModernLayout.ResetAll();

        // Done fires on the release (IMGUI buttons do, the kit's as well), so the press reaches
        // nothing behind it once the menu is gone (per user, 2026-10-04: Done over the minimap
        // opened the big map).
        if (pORows.Action(new Rect(lfX + lfHalf + RowGap, lfY, lfHalf, RowHeight), UWText.T("editor.done")) && UWModernHud.Instance != null)
            RequestLeave(UWModernHud.Instance.Menu.CloseMenu);

        lfY += RowHeight + RowGap;

        // The inspector.
        pORows.Separator(new Rect(0f, lfY + 4f, mfWindowWidth, 1f));
        lfY += 9f;

        pORows.Info(new Rect(lfX, lfY, lfInner, RowHeight), fHeading(), UWRowInfoEnum.Heading);
        lfY += RowHeight + RowGap;

        foreach (InspectorItem lOItem in mOItems)
        {
            float lfItemHeight = fItemHeight(pORows, lOItem, lfInner);

            switch (lOItem.Kind)
            {
                case InspectorKindEnum.Info:
                    pORows.Info(new Rect(lfX, lfY, lfInner, lfItemHeight), lOItem.Label, UWRowInfoEnum.Dim);
                    break;

                case InspectorKindEnum.Stepper:
                    fDrawStepper(pORows, lfX, lfY, lfInner, lOItem);
                    break;

                case InspectorKindEnum.Choice:
                    pORows.Label(new Rect(lfX, lfY, mfLabelWidth, RowHeight), lOItem.Label);
                    pORows.Enabled = lOItem.Enabled;

                    int liChosen = pORows.Toolbar(new Rect(lfX + mfLabelWidth, lfY, lfInner - mfLabelWidth, RowHeight),
                        Mathf.Clamp(lOItem.Current, 0, lOItem.Choices.Length - 1), lOItem.Choices);

                    pORows.Enabled = true;

                    if (liChosen != lOItem.Current && lOItem.Enabled && lOItem.Choose != null)
                        lOItem.Choose(liChosen);
                    break;

                case InspectorKindEnum.Radio:
                {
                    pORows.Label(new Rect(lfX, lfY, mfLabelWidth, RowHeight), lOItem.Label);
                    pORows.Enabled = lOItem.Enabled;

                    float lfEach = (lfInner - mfLabelWidth) / Mathf.Max(1, lOItem.Choices.Length);

                    if (lOItem.Previews != null)
                    {
                        fDrawPreviews(pORows, lfX + mfLabelWidth, lfY, lfEach, lOItem);
                        pORows.Enabled = true;
                        break;
                    }

                    for (int liAt = 0; liAt < lOItem.Choices.Length; liAt++)
                    {
                        bool lbOn = pORows.Toggle(new Rect(lfX + mfLabelWidth + (liAt * lfEach), lfY, lfEach, RowHeight), liAt == lOItem.Current, string.Empty);

                        if (lbOn && liAt != lOItem.Current && lOItem.Enabled && lOItem.Choose != null)
                            lOItem.Choose(liAt);
                    }

                    pORows.Enabled = true;
                    break;
                }

                case InspectorKindEnum.Action:
                    pORows.Label(new Rect(lfX, lfY, mfLabelWidth, RowHeight), lOItem.Label);
                    pORows.Enabled = lOItem.Enabled;

                    if (pORows.Action(new Rect(lfX + mfLabelWidth, lfY, lfInner - mfLabelWidth, RowHeight), lOItem.Choices[0])
                        && lOItem.Choose != null)
                        lOItem.Choose(0);

                    pORows.Enabled = true;
                    break;
            }

            lfY += lfItemHeight + RowGap;
        }
    }

    /// <summary>A radio row's choices as samples (InspectorItem.Previews), through the drawer
    /// (Samples); a click on another one chooses it.</summary>
    private void fDrawPreviews(IUWRows pORows, float pfX, float pfY, float pfEach, InspectorItem pOItem)
    {
        int liPressed = pORows.Samples(pfX, pfY, pfEach, RowHeight, pOItem.Previews, pOItem.Current, pOItem.Enabled);

        if (liPressed >= 0 && pOItem.Choose != null)
            pOItem.Choose(liPressed);
    }

    /// <summary>A row with its name, - , the value and + (the drawer's Stepper).</summary>
    private void fDrawStepper(IUWRows pORows, float pfX, float pfY, float pfWidth, InspectorItem pOItem)
    {
        int liStep = pORows.Stepper(new Rect(pfX, pfY, pfWidth, RowHeight), mfLabelWidth, StepButtonWidth, pOItem.Label, pOItem.Value,
            pOItem.CanDown, pOItem.CanUp);

        if (liStep != 0 && pOItem.Choose != null)
            pOItem.Choose(liStep < 0 ? 0 : 1);
    }

    /// <summary>The pointer (screen pixels, y up) is on the editor's window.</summary>
    private bool fWindowContains(Vector2 pOPointer)
    {
        return mOWindowScreenRect.width > 0f && mOWindowScreenRect.Contains(pOPointer);
    }


    // ------------------------------------------------- Building

    private void fBuild()
    {
        // Over the HUD, the bags, the pointer's thing, the boxes and the conversation's preview (46).
        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Modern layout editor", UWUiSorting.LayoutEditor);

        RectTransform lORootRect = (RectTransform)mOCanvas.transform;

        for (int liAt = 0; liAt < UWModernLayout.ElementCount; liAt++)
        {
            mOLines[liAt] = new Image[4];

            for (int liLine = 0; liLine < 4; liLine++)
                mOLines[liAt][liLine] = UWUiKit.CreateImage(lORootRect, "Frame", msGoldDim);

            mOTags[liAt] = UWUiKit.CreateImage(lORootRect, "Tag", Color.black);
            mOTagTexts[liAt] = UWUiKit.CreateText(lORootRect, "Name", TextAnchor.MiddleCenter, UWModernPalette.Text, mOUi, UWUiKit.EditorText);
            mOHandles[liAt] = UWUiKit.CreateImage(lORootRect, "Handle", UWModernPalette.Gold);
            mOPercents[liAt] = UWUiKit.CreateText(lORootRect, "Size", TextAnchor.LowerRight, UWModernPalette.Text, mOUi, UWUiKit.EditorText);
        }

        mOGrips[0] = UWUiKit.CreateImage(lORootRect, "Grip left", UWModernPalette.Gold);
        mOGrips[1] = UWUiKit.CreateImage(lORootRect, "Grip right", UWModernPalette.Gold);
        mOGuideX = UWUiKit.CreateImage(lORootRect, "Guide x", msGuide);
        mOGuideY = UWUiKit.CreateImage(lORootRect, "Guide y", msGuide);
    }
}
