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
///   - THE PANELS move like the rest and are free windows then (UWModernLayout); the right
///     button docks them again. A closed panel grabbed by its handle opens for the drag, so it
///     does not vanish when it becomes free (per user, 2026-10-04), and closes again with the
///     editor. (The minimap docking into the character panel's head is gone, per user the same
///     day: placed freely it can sit wherever one wants it.)
///   - a strip under the heading in two rows (per user, the same day): the title and a short
///     help, below them the UI SIZE with its - and + (moved here from the game menu, per user the
///     same day: it is the base all the parts' sizes multiply, and sizes the boxes and menus that
///     are no parts) and the buttons - Panels out or in, Dialog on or off (the conversation's
///     preview with sample text), Minimap on or off (switched off for good, for whoever does not
///     want it), Snap on or off, Reset all, Done; Escape goes back to the game menu.
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

    /// <summary>The strip's two rows: the text and the buttons.</summary>
    private const int ToolbarTextRow = 12;

    private const int ToolbarPad = 4;

    private const int ToolbarHeight = ToolbarPad + ToolbarTextRow + 2 + ButtonHeight + ToolbarPad;

    private const int ButtonPanels = 0;

    private const int ButtonDialog = 1;

    private const int ButtonMinimap = 2;

    private const int ButtonSnap = 3;

    private const int ButtonReset = 4;

    private const int ButtonDone = 5;

    /// <summary>The UI size's - and + (square) and its value between them.</summary>
    private const int SizeValueWidth = 48;

    private const int SizeGroupGap = 8;

    private readonly RawImage[] mOSizeButtons = new RawImage[2];

    private readonly Text[] mOSizeButtonTexts = new Text[2];

    private readonly Rect[] mOSizeRects = new Rect[2];

    private Text mOSizeValue;

    private Texture2D mOSizeButtonTexture;

    private const int ButtonWidth = 36;

    private const int ButtonHeight = 14;

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

    private RawImage mOToolbar;

    private Text mOTitle;

    private Text mOHint;

    private const int ButtonCount = 6;

    private readonly RawImage[] mOButtons = new RawImage[ButtonCount];

    private readonly Text[] mOButtonTexts = new Text[ButtonCount];

    private readonly Rect[] mOButtonRects = new Rect[ButtonCount];

    // --- What the editor opened, to give back
    private bool mbWasEditing;

    private bool mbOpenedBags;

    private bool mbPanelsOut;

    private bool mbCharacterWasOpen;

    private UWModernPanel.TabEnum meCharacterTab;

    private bool mbRunesWereOpen;

    private Texture2D mOToolbarTexture;

    private Texture2D mOButtonTexture;

    private int miToolbarWidth;

    private int miArtVersion = -1;

    private bool mbSnap = true;

    // --- The drag
    private int miDragged = -1;

    private bool mbSizing;

    private Vector2 mODragFrom;

    private Rect mODragRect;

    private int miDragPercent;

    /// <summary>Done was pressed: the editor shuts on the release, so the press reaches nothing
    /// behind it once the menu is gone (per user, 2026-10-04: Done over the minimap opened the
    /// big map).</summary>
    private bool mbDonePressed;

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
        foreach (Texture2D lOTexture in new[] { mOToolbarTexture, mOButtonTexture, mOSizeButtonTexture })
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
            mbDonePressed = false;
            return;
        }

        Mouse lOMouse = Mouse.current;
        Vector2 lOPointer = lOMouse.position.ReadValue();

        if (mbDonePressed)
        {
            if (!lOMouse.leftButton.isPressed)
            {
                mbDonePressed = false;

                if (mOButtonRects[ButtonDone].Contains(lOPointer))
                    UWModernHud.Instance.CloseMenu();
            }

            return;
        }

        if (miDragged >= 0)
        {
            if (lOMouse.leftButton.isPressed)
                fDrag(lOPointer);
            else
            {
                miDragged = -1;
                UWModernLayout.Save();
            }

            return;
        }

        mfGuideX = -1f;
        mfGuideY = -1f;

        if (lOMouse.leftButton.wasPressedThisFrame)
        {
            if (fToolbarClick(lOPointer))
                return;

            int liElement = fElementAt(lOPointer, out bool lbHandle);

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

            if (liElement >= 0 && UWModernLayout.TryGetRect(leElement, out Rect lORect))
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

    private bool fToolbarClick(Vector2 pOPointer)
    {
        if (mOSizeRects[0].Contains(pOPointer) || mOSizeRects[1].Contains(pOPointer))
        {
            int liStep = mOSizeRects[0].Contains(pOPointer) ? -UWModernHud.UiPercentStep : UWModernHud.UiPercentStep;

            UWModernHud.SetUiPercent(UWModernHud.UiPercent + liStep);
        }
        else if (mOButtonRects[ButtonPanels].Contains(pOPointer))
            fSetPanelsOut(!mbPanelsOut);
        else if (mOButtonRects[ButtonDialog].Contains(pOPointer))
            UWModernConversation.Previewing = !UWModernConversation.Previewing;
        else if (mOButtonRects[ButtonMinimap].Contains(pOPointer))
        {
            UWUserSettings.MinimapHidden = !UWUserSettings.MinimapHidden;
            UWUserSettings.Save();
        }
        else if (mOButtonRects[ButtonSnap].Contains(pOPointer))
            mbSnap = !mbSnap;
        else if (mOButtonRects[ButtonReset].Contains(pOPointer))
            UWModernLayout.ResetAll();
        else if (mOButtonRects[ButtonDone].Contains(pOPointer))
            mbDonePressed = true;
        else
            return mOToolbar != null && mOToolbar.enabled && fScreenRectOf(mOToolbar.rectTransform).Contains(pOPointer);

        return true;
    }

    private static Rect fScreenRectOf(RectTransform pORect)
    {
        return new Rect(pORect.anchoredPosition, pORect.sizeDelta);
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!UWModernLayout.IsEditing || mOUi == null || mOUi.mOUWData == null)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

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

            bool lbActive = liAt == liHovered;
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

        // The snapping guides while dragging.
        mOGuideX.enabled = miDragged >= 0 && !mbSizing && mfGuideX >= 0f;
        mOGuideY.enabled = miDragged >= 0 && !mbSizing && mfGuideY >= 0f;

        if (mOGuideX.enabled)
            fSetRect(mOGuideX.rectTransform, mfGuideX - 1f, 0f, 2f, Screen.height);

        if (mOGuideY.enabled)
            fSetRect(mOGuideY.rectTransform, 0f, mfGuideY - 1f, Screen.width, 2f);

        fLayoutToolbar(lfScale, lOPointer);
    }

    /// <summary>The strip under the heading: the title, the help, Snap, Reset all, Done.</summary>
    private void fLayoutToolbar(float pfScale, Vector2 pOPointer)
    {
        int liFont = Mathf.Max(10, Mathf.RoundToInt(4.2f * pfScale));

        mOTitle.fontSize = Mathf.RoundToInt(liFont * 1.2f);
        mOHint.fontSize = liFont;
        mOHint.text = "Drag to move, the corner or the wheel to resize, right click resets one";

        float lfTitle = mOTitle.preferredWidth / pfScale;
        float lfHint = mOHint.preferredWidth / pfScale;
        int liSizeGroup = ButtonHeight + 2 + SizeValueWidth + 2 + ButtonHeight + SizeGroupGap;
        int liButtonsWidth = liSizeGroup + (ButtonCount * (ButtonWidth + 2)) - 2;
        int liWidth = Mathf.CeilToInt(Mathf.Max(8 + lfTitle + 10 + lfHint + 8, liButtonsWidth + 16));

        if (miArtVersion != UWColourVision.Version || mOToolbarTexture == null || liWidth != miToolbarWidth)
        {
            if (mOToolbarTexture != null)
                Destroy(mOToolbarTexture);

            if (mOButtonTexture != null)
                Destroy(mOButtonTexture);

            mOToolbarTexture = UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, liWidth, ToolbarHeight, mOUi.TextureFilterMode, 5);
            mOButtonTexture = UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, ButtonWidth, ButtonHeight, mOUi.TextureFilterMode, 6);

            if (mOSizeButtonTexture != null)
                Destroy(mOSizeButtonTexture);

            mOSizeButtonTexture = UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, ButtonHeight, ButtonHeight, mOUi.TextureFilterMode, 7);

            foreach (RawImage lOSizeButton in mOSizeButtons)
                lOSizeButton.texture = mOSizeButtonTexture;
            miToolbarWidth = liWidth;
            miArtVersion = UWColourVision.Version;
            mOToolbar.texture = mOToolbarTexture;

            foreach (RawImage lOButton in mOButtons)
                lOButton.texture = mOButtonTexture;
        }

        float lfWidth = liWidth * pfScale;
        float lfHeight = ToolbarHeight * pfScale;
        float lfX = Mathf.Round((Screen.width - lfWidth) * 0.5f);
        float lfY = Mathf.Round(Screen.height - ((4 + UWModernHudArt.StripHeight + 8) * pfScale) - lfHeight);

        float lfTextY = lfY + lfHeight - ((ToolbarPad + ToolbarTextRow) * pfScale);

        fSetRect(mOToolbar.rectTransform, lfX, lfY, lfWidth, lfHeight);
        fSetRect(mOTitle.rectTransform, lfX + (8 * pfScale), lfTextY, lfTitle * pfScale + pfScale, ToolbarTextRow * pfScale);
        fSetRect(mOHint.rectTransform, lfX + ((18 + lfTitle) * pfScale), lfTextY, lfHint * pfScale + pfScale, ToolbarTextRow * pfScale);

        string[] lsLabels =
        {
            mbPanelsOut ? "Panels: out" : "Panels: in", UWModernConversation.Previewing ? "Dialog: on" : "Dialog: off",
            UWUserSettings.MinimapHidden ? "Minimap: off" : "Minimap: on", mbSnap ? "Snap: on" : "Snap: off", "Reset all", "Done"
        };
        float lfButtonX = Mathf.Round(lfX + ((lfWidth - (liButtonsWidth * pfScale)) * 0.5f));
        float lfButtonY = lfY + (ToolbarPad * pfScale);

        // The UI size first: - , its value, +.
        int liPercent = UWModernHud.UiPercent;
        float[] lfSizeX = { lfButtonX, lfButtonX + ((ButtonHeight + 2 + SizeValueWidth + 2) * pfScale) };

        for (int liSide = 0; liSide < 2; liSide++)
        {
            Rect lOSizeRect = new Rect(lfSizeX[liSide], lfButtonY, ButtonHeight * pfScale, ButtonHeight * pfScale);
            bool lbAtEnd = liSide == 0 ? liPercent <= UWModernHud.MinUiPercent : liPercent >= UWModernHud.MaxUiPercent;

            mOSizeRects[liSide] = lbAtEnd ? Rect.zero : lOSizeRect;
            fSetRect(mOSizeButtons[liSide].rectTransform, lOSizeRect.x, lOSizeRect.y, lOSizeRect.width, lOSizeRect.height);
            mOSizeButtonTexts[liSide].text = liSide == 0 ? "-" : "+";
            mOSizeButtonTexts[liSide].fontSize = Mathf.RoundToInt(liFont * 1.2f);
            mOSizeButtonTexts[liSide].color = lbAtEnd ? msText * 0.6f : lOSizeRect.Contains(pOPointer) ? msHover : msGold;
            fSetRect(mOSizeButtonTexts[liSide].rectTransform, lOSizeRect.x, lOSizeRect.y + (0.5f * pfScale), lOSizeRect.width, lOSizeRect.height);
        }

        mOSizeValue.text = "UI size: " + liPercent + " %";
        mOSizeValue.fontSize = liFont;
        fSetRect(mOSizeValue.rectTransform, lfButtonX + ((ButtonHeight + 2) * pfScale), lfButtonY + (0.5f * pfScale),
            SizeValueWidth * pfScale, ButtonHeight * pfScale);

        lfButtonX += liSizeGroup * pfScale;

        for (int liButton = 0; liButton < ButtonCount; liButton++)
        {
            Rect lORect = new Rect(lfButtonX + (liButton * (ButtonWidth + 2) * pfScale), lfButtonY, ButtonWidth * pfScale, ButtonHeight * pfScale);

            mOButtonRects[liButton] = lORect;
            fSetRect(mOButtons[liButton].rectTransform, lORect.x, lORect.y, lORect.width, lORect.height);
            mOButtonTexts[liButton].text = lsLabels[liButton];
            mOButtonTexts[liButton].fontSize = liFont;
            mOButtonTexts[liButton].color = lORect.Contains(pOPointer) ? msHover : msGold;
            fSetRect(mOButtonTexts[liButton].rectTransform, lORect.x, lORect.y + (0.5f * pfScale), lORect.width, lORect.height);
        }
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
        // Over the HUD, the bags, the pointer's thing and the boxes.
        mOCanvas.sortingOrder = 46;

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

        mOGuideX = fCreateImage(lORootRect, "Guide x", msGuide);
        mOGuideY = fCreateImage(lORootRect, "Guide y", msGuide);

        mOToolbar = fCreateRawImage(lORootRect, "Toolbar");
        UWPixelArtUI.Apply(mOToolbar);
        mOTitle = fCreateText(lORootRect, "Title", TextAnchor.MiddleLeft, msGold);
        mOTitle.text = "Edit layout";
        mOHint = fCreateText(lORootRect, "Help", TextAnchor.MiddleLeft, msText);

        for (int liSide = 0; liSide < 2; liSide++)
        {
            mOSizeButtons[liSide] = fCreateRawImage(lORootRect, liSide == 0 ? "Smaller" : "Larger");
            UWPixelArtUI.Apply(mOSizeButtons[liSide]);
            mOSizeButtonTexts[liSide] = fCreateText(lORootRect, "Size button", TextAnchor.MiddleCenter, msGold);
        }

        mOSizeValue = fCreateText(lORootRect, "UI size", TextAnchor.MiddleCenter, msText);

        for (int liButton = 0; liButton < ButtonCount; liButton++)
        {
            mOButtons[liButton] = fCreateRawImage(lORootRect, "Button");
            UWPixelArtUI.Apply(mOButtons[liButton]);
            mOButtonTexts[liButton] = fCreateText(lORootRect, "Button text", TextAnchor.MiddleCenter, msGold);
        }
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
        lOText.font = mOFont != null ? mOFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
