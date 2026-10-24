using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE POINTER'S PICTURE (cleanup 5.17, 2026-10-23: out of UWGameUI, as it was; D32 of the
/// cleanup reviews - the choice was written out in three places): which of the original's
/// pointers (CURSORS.GR) the system pointer shows and where its hotspot lies.
///
///   - THE CLASSIC SCHEME (ApplyClassic, at the end of UWGameUI.Update): the movement arrows of
///     the pointer zones (UWPointerZones.CursorMovement), the
///     red X of look, get and use, the cross in menus and conversations, the quill and the eraser
///     on the map, the target pointer of a waiting spell or a drawn ranged weapon;
///   - THE MODERN SCHEME (ApplyModern): the cross, the target pointer, the layout editor's arrows;
///   - THE CROSSHAIR'S PICTURE of the modern scheme with the pointer locked (CrosshairPicture,
///     for UWModernCrosshair);
///   - the pictures at the size the frame is drawn at (the scaled copies, kept per scale);
///   - THE MODERN BAGS' THING ON THE FREE POINTER hides the system pointer behind it
///     (ShowSystemPointerForBags) - this class is that hiding's one owner since cleanup 5.17;
///   - THE HELD POINTER of the classic walk by the mouse (fApplyHeldPointer, 2026-10-23): while
///     the left button holds in the view, the system pointer is hidden and its picture drawn
///     here at the position UWPointerZones clamped it to, like the thing on the pointer
///     (UWDragIcon). The system pointer is drawn by the operating system between our frames, so
///     with a fast hand it ran past the window's edge and sprang back on the next warp (per
///     user); the original drew its pointer itself and held it in the window.
///
/// A picture is set only while the system pointer shows (Cursor.visible): whoever hides it - the
/// classic drag (UWDragIcon), the bags' thing, a prompt, UWControlScheme's holds - leaves the
/// picture alone. Nothing here writes Cursor.visible each frame.
/// </summary>
public sealed class UWPointerCursor
{
    public UWPointerCursor(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    /// <summary>The original's fifteen pointer pictures (CURSORS.GR), as loaded - also the map's
    /// quill (UWHudMap) and the modern crosshair. Null until UWGameUI.Init.</summary>
    internal List<Texture2D> Pictures { get; private set; }

    /// <summary>The pictures from the game data (UWGameUI.Init).</summary>
    internal void Load(DataImport pOData, FilterMode peFilter)
    {
        Pictures = new List<Texture2D>();
        for (int i = 0; i < 15; i++)
        {
            UWTexture lOCursorTex = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.CURSORS, i);
            Pictures.Add(UWTextureFactory.FromTexture(lOCursorTex, peFilter, "UWGameUI cursor"));
        }
    }

    // ------------------------------------------------- The modern scheme

    /// <summary>The modern scheme's pointer, every frame while the classic frame is off.</summary>
    internal void ApplyModern(Interaction pOInteraction)
    {
        // THE MODERN SCHEME'S POINTER (the bags, the menu) is always the cross, whatever the
        // classic one showed last - the arrows, the red X (per user, 2026-10-03). The choice
        // below is never reached in this scheme, so the last classic pointer used to stay.
        // A spell waiting for its target shows the original's target pointer instead (per
        // user, 2026-10-04; with the pointer locked UWModernHud puts it in the crosshair's place).
        // A ranged weapon drawn far enough shows the same pointer as in the classic scheme
        // (IsRangedTargeting, per user the same day: so one sees when the charge suffices).
        bool lbWorldPointer = !UWModernUi.IsMenuOpen;
        int liModernCursor = pOInteraction != null && pOInteraction.IsSpellTargeting && lbWorldPointer
            ? pOInteraction.SpellTargetingCursor
            : pOInteraction != null && pOInteraction.IsRangedTargeting && lbWorldPointer
            ? UWHudRunes.SpellTargetCursor
            : (int)UWCursors.CursorEnum.Center;

        // The layout editor's arrows (UWModernLayoutEditor.PointerTile, per user 2026-10-10),
        // as large as the game's own pointers; the middle is where they point.
        if (Cursor.visible && UWModernLayout.IsEditing && UWModernLayoutEditor.PointerTile >= 0)
        {
            Texture2D lOArrow = UWGlyphs.Pointer(UWModernLayoutEditor.PointerTile, fModernPointerScale());

            if (lOArrow != null)
            {
                Cursor.SetCursor(lOArrow, new Vector2(lOArrow.width * 0.5f, lOArrow.height * 0.5f), PointerCursorMode);
                return;
            }
        }

        if (Cursor.visible && Pictures != null && liModernCursor < Pictures.Count)
        {
            // As large as the crosshair's picture with the pointer locked (UWModernCrosshair:
            // the modern pixel scale, the columns 5/6 of the rows), so locking and freeing keep
            // the size.
            Texture2D lOCross = fGetScaledCursor(liModernCursor, fModernPointerScale(), UWModernHudArt.PixelAspectX);

            Cursor.SetCursor(lOCross, new Vector2(lOCross.width * 0.5f, lOCross.height * 0.5f), PointerCursorMode);
        }
    }

    /// <summary>The modern scheme's pointer at the MODERN UI'S SCALE (UWModernUi.PixelScale,
    /// rounded to whole pixels), the size the crosshair's picture has with the pointer locked (per
    /// user, 2026-10-23, once Unity drew the pointer at its full size: the classic frame's scale
    /// was too big for the full-screen view, half of it a guess).</summary>
    private static int fModernPointerScale()
    {
        return Mathf.Max(1, Mathf.RoundToInt(UWModernUi.PixelScale));
    }

    /// <summary>
    /// The picture in the middle of the modern view with the pointer locked (UWModernCrosshair),
    /// null where it shows none (the pointer free: the free pointer stands for it) or the
    /// pictures are missing.
    ///
    /// A spell waiting for its target (UWHudRunes.fAfterModernCast) shows the original's target
    /// pointer instead of the crosshair - the free pointer turns into it (ApplyModern).
    /// A ranged weapon drawn back likewise (Interaction.IsRangedTargeting, per user,
    /// 2026-10-04: the red circle shows when the shot is ready - since 2026-10-12 at the end of
    /// the wind-up, as in the original).
    ///
    /// THE CROSSHAIR IS THE GAME'S OWN POINTER (per user, 2026-10-04: the normal cursor instead of
    /// the dot) - the cross the free pointer shows as well (ApplyModern), so locking and freeing
    /// look the same; with the pointer free that one stands for it and the middle stays empty.
    /// </summary>
    internal Texture2D CrosshairPicture(Interaction pOInteraction, bool pbLocked)
    {
        bool lbWaiting = pbLocked && (pOInteraction.IsSpellTargeting || pOInteraction.IsRangedTargeting);
        int liCursor = !lbWaiting ? (int)UWCursors.CursorEnum.Center : pOInteraction.SpellTargetingCursor;

        return pbLocked && Pictures != null && liCursor < Pictures.Count ? Pictures[liCursor] : null;
    }

    /// <summary>Whether this class hid the system pointer for the bags' thing on it.</summary>
    private bool mbHidSystemPointer;

    /// <summary>Hides the system pointer behind the thing on the free pointer, and shows it
    /// again when the hand is empty - only if this class hid it, and only while the pointer is
    /// free (locked, UWControlScheme keeps it hidden anyway). Called by the bags' layout every
    /// frame (UWModernBagDrag.LayoutPointerItem); it writes only when the pointer's visibility is
    /// to change. Until cleanup 5.17 UWModernBags.fShowSystemPointer.</summary>
    internal void ShowSystemPointerForBags(bool pbShow)
    {
        if (!pbShow)
        {
            if (UWModernPointer.IsFree && Cursor.visible)
            {
                Cursor.visible = false;
                mbHidSystemPointer = true;
            }

            return;
        }

        if (mbHidSystemPointer)
        {
            mbHidSystemPointer = false;

            if (UWModernPointer.IsFree)
                Cursor.visible = true;
        }
    }

    // ------------------------------------------------- The classic scheme

    /// <summary>The classic scheme's pointer, at the end of UWGameUI.Update (after the pointer
    /// zones, UWPointerZones).</summary>
    internal void ApplyClassic(Interaction pOInteraction)
    {
        int liCursor = fClassicCursor(pOInteraction);

        fApplyHeldPointer(liCursor);

        if (!Cursor.visible)
            return;

        if (liCursor >= 0 && liCursor < Pictures.Count)
        {
            Texture2D lOCursor = fGetScaledCursor(liCursor);

            // The hotspot lies in the CENTRE - where the pointer visually points. That
            // matters especially for the target cursor: the mouse reports its position
            // independent of the hotspot, but whoever aims at the centre of the crosshair
            // and hits the top-left corner misses (per user,
            // 2026-09-05).
            //
            // Computed from the SCALED texture. Previously a fixed value
            // of 8,8 stood here - right for the 16x16 of the original, but since
            // fGetScaledCursor brings the pointers to screen size, it lies almost
            // in the corner.
            //
            // The eraser stone on the map, by contrast, grips at the LEFT centre, so that it hits the
            // line where it touches it (per user on the original,
            // 2026-09-03). The flat quill's hot spot is its BOTTOM-LEFT CORNER, the tip (per user
            // with DOSBox screenshots, 2026-10-23: the note begins at the tip's column, its top
            // four rows above the tip's row - UWClassicLayout.Map.NoteColumnOffset and
            // NoteRowsAboveClick measured from there). The decompilation's set_mouse_data names
            // the picture's middle; the game draws the quill with the tip at the pointer anyway.
            Vector2 lOHotspot = mOUi.Map.IsMapVisible
                ? (mOUi.Map.IsErasing ? new Vector2(0f, lOCursor.height * 0.5f) : new Vector2(0f, lOCursor.height))
                : new Vector2(lOCursor.width * 0.5f, lOCursor.height * 0.5f);

            Cursor.SetCursor(lOCursor, lOHotspot, PointerCursorMode);
        }
    }

    /// <summary>Which of the original's pointers the classic scheme shows now (an index into
    /// Pictures).</summary>
    private int fClassicCursor(Interaction pOInteraction)
    {
        UWHudMap lOMap = mOUi.Map;

        // On the map you hold a quill, when erasing the eraser stone.
        // A projectile spell waits for its target and shows its own pointer for that.
        // When shooting, the same pointer as for the projectile spell shows as soon as the
        // wind-up is through (since 2026-10-12; the minimum charge before) - see
        // Interaction.IsRangedTargeting.
        // In the main menu and in character creation only the cross (per user,
        // 2026-09-11) - the viewport below would otherwise show the arrows.
        // In conversation likewise only the cross (per user, 2026-09-13).
        // In a conversation the red X of look, get and use STAYS (per user on the original,
        // 2026-09-19, with a goal-10 NPC addressing the player in use mode) - only the
        // function is the usual one there.
        // Over the setup bar and its menus and dialogs, which fold out over the options
        // panel since 2026-10-06, only the cross as well (per user the same day).
        return UWConversationController.IsAnyOpen && pOInteraction != null && pOInteraction.IsRedXCursorMode
            ? (int)UWCursors.CursorEnum.Use
            : UWScreenUi.IsScreenMenuOpen || UWConversationController.IsAnyOpen || !fIsFirstImageShown() || UWSetupMenu.IsCapturingInput
            ? (int)UWCursors.CursorEnum.Center
            : lOMap.IsMapVisible
            ? (lOMap.IsErasing ? UWHudMap.MapEraseCursor : UWHudMap.MapQuillCursor)
            : (pOInteraction != null && pOInteraction.IsSpellTargeting
                ? pOInteraction.SpellTargetingCursor
                : (pOInteraction != null && pOInteraction.IsRangedTargeting
                    ? UWHudRunes.SpellTargetCursor
                    // Look, get and use: the red X instead of the movement arrows
                    // (per user on the original, 2026-09-19); talk keeps them.
                    : (pOInteraction != null && pOInteraction.IsRedXCursorMode
                        ? (int)UWCursors.CursorEnum.Use
                        : (int)mOUi.PointerZones.CursorMovement)));
    }

    // ------------------------------------------------- The held pointer of the classic walk

    /// <summary>Whether this class hid the system pointer for the held walk by the mouse.</summary>
    private bool mbHidForHeldPointer;

    /// <summary>The held pointer's own canvas and picture, made on the first hold; the sprites of
    /// the original's pictures, one per index, made as needed.</summary>
    private GameObject mOHeldCanvas;

    private Image mOHeldImage;

    private Sprite[] mOHeldSprites;

    /// <summary>
    /// THE POINTER DURING THE WALK BY THE MOUSE (2026-10-23, per user: with a fast hand the
    /// pointer ran past the view window's edge and sprang back; in the original it stays in the
    /// window). The operating system draws the system pointer between our frames, wherever the
    /// hand takes it; UWPointerZones warps it back only once a frame. So while the left button
    /// holds in the view the system pointer is hidden and its picture is drawn here, at the
    /// clamped position, like the thing on the pointer (UWDragIcon), which never overshot.
    ///
    /// Only when the system pointer is the pointer: with a thing on it (UWDragIcon hid the
    /// system pointer) that thing is the pointer, as in the original. The pad's pointer is its own
    /// and is not drawn here (UWPointerZones.IsMouseHeld). Writes Cursor.visible only when the
    /// hold begins and ends.
    /// </summary>
    private void fApplyHeldPointer(int piCursor)
    {
        if (mOUi.PointerZones.IsMouseHeld)
        {
            if (!mbHidForHeldPointer)
            {
                if (!Cursor.visible)
                    return;

                Cursor.visible = false;
                mbHidForHeldPointer = true;
            }

            fShowHeldPointer(piCursor, mOUi.PointerZones.PointerPosition);
            return;
        }

        if (!mbHidForHeldPointer)
            return;

        mbHidForHeldPointer = false;

        if (mOHeldImage != null)
            mOHeldImage.enabled = false;

        Cursor.visible = true;
    }

    /// <summary>The picture at the position, as large as the system pointer is drawn (the
    /// frame's whole-number scale, fGetScaledCursor), its middle at the position like the system
    /// pointer's hotspot.</summary>
    private void fShowHeldPointer(int piCursor, Vector2 pOPosition)
    {
        if (Pictures == null || piCursor < 0 || piCursor >= Pictures.Count)
            return;

        if (mOHeldCanvas == null)
        {
            // At the root like the drag's canvas (UWDragIcon: a canvas under the camera is turned
            // with the player before Unity drives it back), with the frame's fitting (UWUiFit).
            mOHeldCanvas = UWUiKit.CreateClassicCanvas(null, "Held Pointer Canvas", UWUiSorting.ItemDrag).gameObject;
            mOHeldImage = UWUiKit.CreateImage(mOHeldCanvas.transform, "HeldPointer", UWUiKit.Centre, UWUiKit.Centre, Color.white);
            mOHeldSprites = new Sprite[Pictures.Count];
        }

        if (mOHeldSprites[piCursor] == null)
            mOHeldSprites[piCursor] = UWGameUI.fToSprite(Pictures[piCursor]);

        Texture2D lOPicture = Pictures[piCursor];
        float lfScale = UWUiFit.Scale;
        float lfUnits = lfScale > 0f ? Mathf.Max(1, Mathf.FloorToInt(lfScale)) / lfScale : 1f;

        mOHeldImage.sprite = mOHeldSprites[piCursor];
        mOHeldImage.rectTransform.sizeDelta = new Vector2(lOPicture.width * lfUnits, lOPicture.height * lfUnits);
        mOHeldImage.rectTransform.position = pOPosition;
        mOHeldImage.enabled = true;
    }

    /// <summary>The held pointer's canvas and sprites go (UWGameUI.OnDestroy).</summary>
    internal void ReleaseHeldPointer()
    {
        if (mOHeldSprites != null)
        {
            foreach (Sprite lOSprite in mOHeldSprites)
            {
                if (lOSprite != null)
                    UnityEngine.Object.Destroy(lOSprite);
            }

            mOHeldSprites = null;
        }

        if (mOHeldCanvas != null)
            UnityEngine.Object.Destroy(mOHeldCanvas);

        mOHeldCanvas = null;
        mOHeldImage = null;
        mbHidForHeldPointer = false;
    }

    /// <summary>
    /// How the pointer's picture is drawn: BY UNITY ITSELF, in both schemes (per user,
    /// 2026-10-23). The system limits the size of a hardware pointer, so the system pointer came
    /// smaller than the frame's scale asks for - the quill on the map (2026-09-03) and, next to
    /// the held pointer drawn by us (fApplyHeldPointer), every pointer; the original drew its
    /// pointer itself too. The price is a frame's delay behind the hand, which the pad on Linux
    /// paid already: the pad moves the pointer by a warp (UWGamepadPointer), which XWayland -
    /// Kubuntu's default session - only emulates, keeping the system pointer invisible (per user,
    /// 2026-10-09; first seen in the VM and put down to it). Until 2026-10-23 the system drew
    /// the pointer (Auto) everywhere else.
    /// </summary>
    private static CursorMode PointerCursorMode => CursorMode.ForceSoftware;

    /// <summary>Has the freshly built scene been drawn at least once? See fIsFirstImageShown.
    /// </summary>
    private bool mbFirstImageShown;

    private int miFirstImageFrame = -1;

    /// <summary>Longest frame that still counts as "the picture is there", in seconds.</summary>
    private const float FirstImageFrameSeconds = 0.1f;

    /// <summary>
    /// Keeps the crosshair until the game picture is actually on screen. After loading a save
    /// game from the menu the first frames of the new scene take long (level geometry, GPU
    /// upload, shader warm-up) while the menu picture is still visible - the movement cursor
    /// showed up over the menu (per user, 2026-09-14). The first quick frame after at least
    /// two frames marks the moment the new picture has been presented.
    /// </summary>
    private bool fIsFirstImageShown()
    {
        if (mbFirstImageShown)
            return true;

        if (miFirstImageFrame < 0)
            miFirstImageFrame = Time.frameCount;

        if (Time.frameCount >= miFirstImageFrame + 2 && Time.unscaledDeltaTime < FirstImageFrameSeconds)
            mbFirstImageShown = true;

        return mbFirstImageShown;
    }

    // ------------------------------------------------- The pictures at the frame's size

    /// <summary>
    /// The mouse pointer at the size at which the original shows it.
    ///
    /// Unity draws the pointer in real screen pixels, while the whole game is scaled up from
    /// 320x200 - a pointer of forty pixels therefore looks tiny next to
    /// everything else (noticed per user at the quill of the map, 2026-09-03). Here it is
    /// enlarged with the same integer factor as the frame, with nearest neighbour,
    /// so that the pixels stay hard.
    ///
    /// The enlarged pictures are kept; they are only rebuilt on a changed
    /// window size.
    /// </summary>
    private Texture2D[] mOScaledCursors;

    private int miCursorScale;

    private float mfCursorAspectX = 1f;

    private Texture2D fGetScaledCursor(int piIndex)
    {
        return fGetScaledCursor(piIndex, Mathf.Max(1, Mathf.FloorToInt(UWUiFit.Scale)));
    }

    /// <summary>The picture at the given whole-number scale of its rows: the frame's for the
    /// classic scheme, the modern UI's for the modern one (fModernPointerScale), there with the
    /// columns at pfAspectX of the rows (5/6, as the modern HUD draws the original's pictures).
    /// The cache holds one scale; a scheme switch rebuilds it once.</summary>
    private Texture2D fGetScaledCursor(int piIndex, int piScale, float pfAspectX = 1f)
    {
        int liScale = piScale;

        if (liScale <= 1 && pfAspectX == 1f)
            return Pictures[piIndex];

        if (mOScaledCursors == null || miCursorScale != liScale || mfCursorAspectX != pfAspectX)
        {
            // The pictures of the old scale are destroyed (2026-10-19, found by cleanup step 5.4:
            // they were dropped and stayed behind on every change of the window size). The pointer
            // is set to the new picture in the same frame.
            ReleaseScaledCursors();
            mOScaledCursors = new Texture2D[Pictures.Count];
            miCursorScale = liScale;
            mfCursorAspectX = pfAspectX;
        }

        if (mOScaledCursors[piIndex] != null)
            return mOScaledCursors[piIndex];

        Texture2D lOSource = Pictures[piIndex];

        int liWidth = Mathf.Max(1, Mathf.RoundToInt(lOSource.width * liScale * pfAspectX));
        int liHeight = lOSource.height * liScale;

        Color32[] lOSourcePixels = lOSource.GetPixels32();
        Color32[] lOPixels = new Color32[liWidth * liHeight];

        for (int liY = 0; liY < liHeight; liY++)
        {
            int liSourceRow = (liY / liScale) * lOSource.width;

            // Nearest neighbour along the columns, whole-number along the rows.
            for (int liX = 0; liX < liWidth; liX++)
                lOPixels[(liY * liWidth) + liX] = lOSourcePixels[liSourceRow + ((liX * lOSource.width) / liWidth)];
        }

        Texture2D lOScaled = UWTextureFactory.FromColours(lOPixels, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Repeat,
            "UWGameUI scaled picture");

        mOScaledCursors[piIndex] = lOScaled;

        return lOScaled;
    }

    /// <summary>The scaled copies go (a changed scale, UWGameUI.OnDestroy).</summary>
    internal void ReleaseScaledCursors()
    {
        if (mOScaledCursors == null)
            return;

        foreach (Texture2D lOScaled in mOScaledCursors)
        {
            if (lOScaled != null)
                UnityEngine.Object.Destroy(lOScaled);
        }

        mOScaledCursors = null;
    }
}
