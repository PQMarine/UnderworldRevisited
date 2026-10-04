using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the pictures in the 3D window (window views, scrolls, gravestones) and the window flash".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudPictures
{
    private readonly UWGameUI mOUi;

    internal UWHudPictures(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    internal Image mWindowPictureImage;

    private Sprite[] mOWindowPictures;

    /// <summary>Top-left corner of the window picture in the viewport, computed from the position of the painted
    /// area within the cutout (see fBuildWindowPictures).</summary>
    private Vector2 mOWindowPictureCorner;

    /// <summary>Whether a window picture is currently shown - as long as it is, the display is modal,
    /// the next key press or click only hides it again (see
    /// Interaction.Update).</summary>
    public bool IsWindowPictureVisible
    {
        get { return mWindowPictureImage != null && mWindowPictureImage.enabled; }
    }

    /// <summary>The picture shown in the view window now, null for none - the modern scheme shows
    /// it in a frame of its own (UWModernHud), its classic window being hidden.</summary>
    public Sprite CurrentWindowPicture => IsWindowPictureVisible ? mWindowPictureImage.sprite : null;

    /// <summary>Shows the window picture of the given level in the viewport. Returns false
    /// if the cutscene file is missing (incomplete game data - the CUTS folder often lies
    /// only in the CD image), then only the plain look text remains.</summary>
    public bool ShowWindowPicture(int piLevelIndex)
    {
        if (mWindowPictureImage == null)
            return false;

        Sprite lOSprite = fGetWindowPicture(UWObjectMechanics.GetWindowFrame(piLevelIndex));

        if (lOSprite == null)
            return false;

        Vector2 lOSize = lOSprite.rect.size;
        RectTransform lORect = (RectTransform)mWindowPictureImage.transform;

        mWindowPictureImage.sprite = lOSprite;
        lORect.sizeDelta = lOSize;
        // The position is set here on every display and thus deliberately overwrites what
        // is in the prefab: moving it in the prefab has no effect, for readjusting use
        // the fine offset.
        lORect.anchoredPosition = new Vector2(
            mOWindowPictureCorner.x + mOUi.mOWindowPictureNudge.x,
            -(mOWindowPictureCorner.y + mOUi.mOWindowPictureNudge.y));

        mWindowPictureImage.enabled = true;

        return true;
    }

    /// <summary>
    /// Shows the picture of a picture scroll in the viewport - the same area and the same
    /// modal handling as for the windows, only from a different cutscene.
    ///
    /// Returns false if the file is missing (the CUTS folder often lies only in the CD image).
    /// </summary>
    public bool ShowScrollPicture()
    {
        if (mWindowPictureImage == null || mOUi.mOUWData == null)
            return false;

        if (mOScrollPicture == null)
        {
            UWCutscene lOCutscene = mOUi.mOUWData.GetCutscene(UWObjectMechanics.PictureScrollCutsceneFile);

            if (lOCutscene == null)
            {
                Debug.LogWarning("UWGameUI: " + UWObjectMechanics.PictureScrollCutsceneFile
                    + " not found - picture scrolls stay empty. The CUTS folder belongs next to DATA.");

                return false;
            }

            Vector2 lOContentMin;
            Vector2 lOContentSize;

            fMeasureWindowContent(lOCutscene, out lOContentMin, out lOContentSize);

            mOScrollPicture = fBuildCutsceneSprite(lOCutscene, 0,
                (int)lOContentMin.x, (int)lOContentMin.y,
                (int)lOContentSize.x, (int)lOContentSize.y);

            mOScrollPictureCorner = new Vector2(
                UWGameUI.windowHoleLeft + Mathf.Floor((UWGameUI.windowHoleWidthInclusive - lOContentSize.x) * 0.5f),
                UWGameUI.windowHoleTop + UWGameUI.windowContentInsetTop);
        }

        if (mOScrollPicture == null)
            return false;

        RectTransform lORect = (RectTransform)mWindowPictureImage.transform;

        mWindowPictureImage.sprite = mOScrollPicture;
        lORect.sizeDelta = mOScrollPicture.rect.size;
        lORect.anchoredPosition = new Vector2(
            mOScrollPictureCorner.x + mOUi.mOWindowPictureNudge.x,
            -(mOScrollPictureCorner.y + mOUi.mOWindowPictureNudge.y));

        mWindowPictureImage.enabled = true;

        return true;
    }

    private Sprite mOScrollPicture;

    private Vector2 mOScrollPictureCorner;

    /// <summary>Cuts a picture section out of a cutscene and makes a sprite
    /// of it. Drawn opaque, including the transparency marker - for the same reason as with
    /// the window pictures, see fBuildWindowPictures.</summary>
    private Sprite fBuildCutsceneSprite(UWCutscene pOCutscene, int piFrame,
        int piMinX, int piMinY, int piWidth, int piHeight)
    {
        byte[] lyPixels = pOCutscene.GetFrame(piFrame);

        if (lyPixels == null || piWidth <= 0 || piHeight <= 0)
            return null;

        Texture2D lOTexture = new Texture2D(piWidth, piHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI.cs:5787";
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;

        for (int liY = 0; liY < piHeight; liY++)
        {
            for (int liX = 0; liX < piWidth; liX++)
            {
                byte lyIndex = lyPixels[((piMinY + liY) * pOCutscene.Width) + piMinX + liX];

                lOTexture.SetPixel(liX, piHeight - 1 - liY, new Color32(
                    pOCutscene.PaletteRgb[(lyIndex * 3) + 0],
                    pOCutscene.PaletteRgb[(lyIndex * 3) + 1],
                    pOCutscene.PaletteRgb[(lyIndex * 3) + 2],
                    255));
            }
        }

        lOTexture.Apply();

        return Sprite.Create(lOTexture, new Rect(0f, 0f, piWidth, piHeight), Vector2.zero, 1f);
    }

    /// <summary>
    /// Shows the picture of a gravestone from CS401.N01 in the viewport - the same area and
    /// the same modal handling as for the windows (per user, 2026-09-13: "the pictures
    /// that are shown when you look at a gravestone. Similar to the windows").
    ///
    /// Each picture has its own painted block, therefore it is measured per picture. The
    /// reference draws mOUi file transparently over a disabled 3D picture, i.e. onto
    /// black - the background becomes black here. Placement as with the window pictures.
    /// </summary>
    public bool ShowGravePicture(int piFrame)
    {
        if (mWindowPictureImage == null || mOUi.mOUWData == null || piFrame < 0)
            return false;

        Sprite lOSprite;
        Vector2 lOCorner;

        if (!mOGravePictures.TryGetValue(piFrame, out lOSprite))
        {
            UWCutscene lOCutscene = mOUi.mOUWData.GetCutscene(GraveCutsceneFile);

            if (lOCutscene == null || piFrame >= lOCutscene.FrameCount)
            {
                mOGravePictures[piFrame] = null;
                return false;
            }

            lOSprite = fBuildGraveSprite(lOCutscene, piFrame);
            mOGravePictures[piFrame] = lOSprite;
        }

        if (lOSprite == null)
            return false;

        Vector2 lOSize = lOSprite.rect.size;

        // The same spot as the window pictures (fBuildWindowPictures): the grave pictures lie
        // on the canvas likewise at bottom right, with the same block - horizontally centred,
        // vertically with the measured distance from the top.
        lOCorner = new Vector2(
            UWGameUI.windowHoleLeft + Mathf.Floor((UWGameUI.windowHoleWidthInclusive - lOSize.x) * 0.5f),
            UWGameUI.windowHoleTop + UWGameUI.windowContentInsetTop);

        RectTransform lORect = (RectTransform)mWindowPictureImage.transform;

        mWindowPictureImage.sprite = lOSprite;
        lORect.sizeDelta = lOSize;
        lORect.anchoredPosition = new Vector2(
            lOCorner.x + mOUi.mOWindowPictureNudge.x,
            -(lOCorner.y + mOUi.mOWindowPictureNudge.y));

        mWindowPictureImage.enabled = true;

        return true;
    }

    private const string GraveCutsceneFile = "CS401.N01";

    private readonly System.Collections.Generic.Dictionary<int, Sprite> mOGravePictures
        = new System.Collections.Generic.Dictionary<int, Sprite>();

    /// <summary>Cut out the painted block of a grave picture; background (colour of the
    /// top-left corner) becomes black.</summary>
    private Sprite fBuildGraveSprite(UWCutscene pOCutscene, int piFrame)
    {
        byte[] lyPixels = pOCutscene.GetFrame(piFrame);

        if (lyPixels == null || lyPixels.Length < pOCutscene.Width * pOCutscene.Height)
            return null;

        byte lyBackground = lyPixels[0];
        int liMinX = pOCutscene.Width;
        int liMinY = pOCutscene.Height;
        int liMaxX = -1;
        int liMaxY = -1;

        for (int liY = 0; liY < pOCutscene.Height; liY++)
        {
            for (int liX = 0; liX < pOCutscene.Width; liX++)
            {
                if (lyPixels[(liY * pOCutscene.Width) + liX] == lyBackground)
                    continue;

                liMinX = Mathf.Min(liMinX, liX);
                liMaxX = Mathf.Max(liMaxX, liX);
                liMinY = Mathf.Min(liMinY, liY);
                liMaxY = Mathf.Max(liMaxY, liY);
            }
        }

        if (liMaxX < liMinX)
            return null;

        int liWidth = liMaxX - liMinX + 1;
        int liHeight = liMaxY - liMinY + 1;

        Texture2D lOTexture = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI.cs:5906";
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;

        for (int liY = 0; liY < liHeight; liY++)
        {
            for (int liX = 0; liX < liWidth; liX++)
            {
                byte lyIndex = lyPixels[((liMinY + liY) * pOCutscene.Width) + liMinX + liX];

                Color32 lOColour = lyIndex == lyBackground
                    ? new Color32(0, 0, 0, 255)
                    : new Color32(pOCutscene.PaletteRgb[lyIndex * 3], pOCutscene.PaletteRgb[(lyIndex * 3) + 1],
                        pOCutscene.PaletteRgb[(lyIndex * 3) + 2], 255);

                lOTexture.SetPixel(liX, liHeight - 1 - liY, lOColour);
            }
        }

        lOTexture.Apply();

        return Sprite.Create(lOTexture, new Rect(0f, 0f, liWidth, liHeight), Vector2.zero, 1f);
    }

    public void HideWindowPicture()
    {
        if (mWindowPictureImage != null)
            mWindowPictureImage.enabled = false;
    }

    /// <summary>Builds the window pictures once from CS400.N01. The frames are
    /// delta-compressed, so decoding from the start is needed anyway - here all
    /// seven at once. From each frame only the painted block is cut out
    /// (the 320x200 canvas is empty all around), so that the result fits the viewport
    /// exactly.</summary>
    private Sprite fGetWindowPicture(int piFrame)
    {
        if (mOWindowPictures == null)
        {
            UWCutscene lOCutscene = mOUi.mOUWData == null
                ? null
                : mOUi.mOUWData.GetCutscene(UWObjectMechanics.WindowCutsceneFile);

            if (lOCutscene == null)
            {
                mOWindowPictures = new Sprite[0];
                Debug.LogWarning("UWGameUI: " + UWObjectMechanics.WindowCutsceneFile
                    + " not found - window pictures stay off. The CUTS folder belongs next to DATA.");
            }
            else
            {
                mOWindowPictures = fBuildWindowPictures(lOCutscene);
            }
        }

        if (piFrame < 0 || piFrame >= mOWindowPictures.Length)
            return null;

        return mOWindowPictures[piFrame];
    }

    /// <summary>Finds the painted block on the canvas (position and size), measured on frame 0.
    ///
    /// The background is the palette index of the top-left corner, not a fixed zero.
    /// In CS400 that corner is index 0, the transparency marker (R0 G0 B7, the known dark blue
    /// of mOUi project, see UWPalette); real black in that cutscene lies on indices 1 and 3
    /// (R0 G0 B0).
    /// For CS400 that is the same, for CS410 - the picture scroll - it is not: its canvas is
    /// filled all around with a REAL colour. With the fixed zero the block there covered
    /// the whole canvas, and the picture came out much too big and misplaced (per user
    /// with screenshot, 2026-09-03).</summary>
    private static void fMeasureWindowContent(UWCutscene pOCutscene, out Vector2 pOMin, out Vector2 pOSize)
    {
        byte[] lyPixels = pOCutscene.GetFrame(0);

        byte lyBackground = lyPixels.Length > 0 ? lyPixels[0] : (byte)0;
        int liMinX = pOCutscene.Width;
        int liMinY = pOCutscene.Height;
        int liMaxX = -1;
        int liMaxY = -1;

        for (int liY = 0; liY < pOCutscene.Height; liY++)
        {
            for (int liX = 0; liX < pOCutscene.Width; liX++)
            {
                if (lyPixels[(liY * pOCutscene.Width) + liX] == lyBackground)
                    continue;

                if (liX < liMinX) liMinX = liX;
                if (liX > liMaxX) liMaxX = liX;
                if (liY < liMinY) liMinY = liY;
                if (liY > liMaxY) liMaxY = liY;
            }
        }

        if (liMaxX < liMinX || liMaxY < liMinY)
        {
            pOMin = Vector2.zero;
            pOSize = new Vector2(pOCutscene.Width, pOCutscene.Height);
            return;
        }

        pOMin = new Vector2(liMinX, liMinY);
        pOSize = new Vector2(liMaxX - liMinX + 1, liMaxY - liMinY + 1);
    }

    private Sprite[] fBuildWindowPictures(UWCutscene pOCutscene)
    {
        // What is cut out is EXACTLY the painted area (172x112), no more.
        //
        // In between the cutout was hole-sized, i.e. with the black of the empty
        // canvas all around. In the game mOUi put black bars left and top in the
        // viewport that do not exist in the original (reported per user with screenshot,
        // 2026-08-29). The bigger cutout was a misdiagnosis: the transparent
        // line that led to it did not come from a too small cutout, but from the
        // then still wrong position - which has since been measured pixel-exactly.
        //
        // What the original shows in the few pixels between picture edge and hole edge is
        // thus open again; see follow-up question to the user.
        Vector2 lOContentMin;
        Vector2 lOContentSize;

        fMeasureWindowContent(pOCutscene, out lOContentMin, out lOContentSize);

        int liMinX = (int)lOContentMin.x;
        int liMinY = (int)lOContentMin.y;
        int liWidth = (int)lOContentSize.x;
        int liHeight = (int)lOContentSize.y;

        // Horizontally the centring in the inclusively counted hole works out (2 pixels
        // left and right, CONFIRMED per user test). VERTICALLY NOT: centred would be 5
        // pixels at the top, measured in it is 2 (four rounds of user tests, 2026-08-29). Which
        // rule the original uses is open - the value is measured, not derived.
        mOWindowPictureCorner = new Vector2(
            UWGameUI.windowHoleLeft + Mathf.Floor((UWGameUI.windowHoleWidthInclusive - lOContentSize.x) * 0.5f),
            UWGameUI.windowHoleTop + UWGameUI.windowContentInsetTop);

        // Frame 7 is the delta back to frame 0 (animator loop), not a picture of its own.
        int liCount = Mathf.Min(pOCutscene.FrameCount, 7);
        Sprite[] lOResult = new Sprite[liCount];

        for (int liFrame = 0; liFrame < liCount; liFrame++)
        {
            byte[] lyPixels = pOCutscene.GetFrame(liFrame);
            Texture2D lOTexture = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
            lOTexture.name = "UWGameUI.cs:6051";
            lOTexture.filterMode = mOUi.TextureFilterMode;
            lOTexture.wrapMode = TextureWrapMode.Clamp;

            for (int liY = 0; liY < liHeight; liY++)
            {
                for (int liX = 0; liX < liWidth; liX++)
                {
                    byte lyIndex = lyPixels[((liMinY + liY) * pOCutscene.Width) + liMinX + liX];

                    // Drawn opaque, including the transparency marker (index 0, R0 G0 B7): it
                    // fills the dark area behind the bars in the picture, and that is
                    // black in the original. Leaving it transparent here would show the same
                    // area, provided black lies behind it - the difference would only become visible
                    // if the original let the 3D view shine through there,
                    // which it does not.
                    lOTexture.SetPixel(liX, liHeight - 1 - liY, new Color32(
                        pOCutscene.PaletteRgb[(lyIndex * 3) + 0],
                        pOCutscene.PaletteRgb[(lyIndex * 3) + 1],
                        pOCutscene.PaletteRgb[(lyIndex * 3) + 2],
                        255));
                }
            }

            lOTexture.Apply();
            lOResult[liFrame] = Sprite.Create(lOTexture, new Rect(0f, 0f, liWidth, liHeight), Vector2.zero, 1f);
        }

        return lOResult;
    }

    /// <summary>
    /// Makes the 3D window black for a while.
    ///
    /// The original does mOUi when sleeping if no dream comes: two seconds of black
    /// in the viewport, nothing else (reference: uimanager.FlashColour with colour 1 on the
    /// 3D window). Only the window, not the whole screen - the frame with backpack
    /// and compass stays.
    ///
    /// The area is created the first time and afterwards stays in place switched off.
    /// </summary>
    public void FlashWindowBlack(float pfSeconds)
    {
        fFlashWindow(Color.black, pfSeconds, true);
    }

    /// <summary>
    /// Short flash of the viewport in a PALETTE COLOUR - for the curse of a
    /// cursed piece of equipment, which in the reference flashes with colour 0xA8 (see
    /// UWCharacter.ApplyCurse).
    ///
    /// Unlike when sleeping, input stays ON: it is ten hundredths of a second, and
    /// whoever lost control during it would be left standing in the middle of combat.
    /// </summary>
    public void FlashWindowColour(int piPaletteIndex, float pfSeconds)
    {
        if (mOUi.mOUWData == null || mOUi.mOUWData.Palettes == null)
            return;

        UWDataImport.UWData.UWColor32 lOColour =
            mOUi.mOUWData.Palettes.GetPalette(0).GetUWColor(piPaletteIndex);

        fFlashWindow(new Color32(lOColour.R, lOColour.G, lOColour.B, 255), pfSeconds, false);
    }

    private void fFlashWindow(Color pOColour, float pfSeconds, bool pbBlockInput)
    {
        if (pfSeconds <= 0f)
            return;

        // THE MODERN SCHEME has no classic frame: its view is the whole screen, so the whole
        // screen goes black (or flashes) - drawn over everything (per user, 2026-10-04: asleep
        // nothing went dark there).
        if (fIsModern())
        {
            fFlashModern(pOColour, pfSeconds, pbBlockInput);
            return;
        }

        if (mOUi.mGameFrame == null)
            return;

        if (mWindowFlashImage == null)
        {
            mWindowFlashImage = UWGameUI.fCreateImage("WindowFlash", mOUi.mGameFrame,
                UWGameUI.windowHoleLeft, -UWGameUI.windowHoleTop, UWGameUI.windowHoleWidth, UWGameUI.windowHoleHeight, Color.black);

            // IN FRONT of the frame picture, so that the frame still lies above and the black
            // area sits cleanly in the hole - just like the window picture (see there).
            mWindowFlashImage.transform.SetSiblingIndex(mOUi.mMainFrameImage.transform.GetSiblingIndex());
        }

        mWindowFlashImage.color = pOColour;

        // Stop via the handle, not via the name: mOUi.StopCoroutine(string) only works
        // for a run that was also started with the name.
        if (mOWindowFlashRoutine != null)
            mOUi.StopCoroutine(mOWindowFlashRoutine);

        mOWindowFlashRoutine = mOUi.StartCoroutine(fRunWindowFlash(pfSeconds, pbBlockInput));
    }

    private Coroutine mOWindowFlashRoutine;

    private Image mOModernFlashImage;

    private bool fIsModern()
    {
        return mOUi.mControlSchemeRef != null && mOUi.mControlSchemeRef.Current == UWControlScheme.SchemeEnum.Modern;
    }

    private void fFlashModern(Color pOColour, float pfSeconds, bool pbBlockInput)
    {
        if (mOModernFlashImage == null)
        {
            GameObject lORoot = new GameObject("Modern flash", typeof(Canvas), typeof(CanvasScaler));
            lORoot.transform.SetParent(mOUi.transform, false);

            Canvas lOCanvas = lORoot.GetComponent<Canvas>();
            lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            lOCanvas.sortingOrder = 60;

            GameObject lOImage = new GameObject("Black", typeof(RectTransform), typeof(Image));
            lOImage.transform.SetParent(lORoot.transform, false);

            RectTransform lORect = (RectTransform)lOImage.transform;
            lORect.anchorMin = Vector2.zero;
            lORect.anchorMax = Vector2.one;
            lORect.offsetMin = Vector2.zero;
            lORect.offsetMax = Vector2.zero;

            mOModernFlashImage = lOImage.GetComponent<Image>();
            mOModernFlashImage.raycastTarget = false;
            mOModernFlashImage.enabled = false;
        }

        mOModernFlashImage.color = pOColour;

        if (mOWindowFlashRoutine != null)
            mOUi.StopCoroutine(mOWindowFlashRoutine);

        mOWindowFlashRoutine = mOUi.StartCoroutine(fRunModernFlash(pfSeconds, pbBlockInput));
    }

    /// <summary>As fRunWindowFlash, but the input is held WITHOUT a modal hold, which would free
    /// the pointer (UWControlScheme.HoldInput).</summary>
    private System.Collections.IEnumerator fRunModernFlash(float pfSeconds, bool pbBlockInput)
    {
        if (pbBlockInput && mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.HoldInput("window flash");

        mOModernFlashImage.enabled = true;

        yield return new WaitForSeconds(pfSeconds);

        mOModernFlashImage.enabled = false;

        if (pbBlockInput && mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.ReleaseInput("window flash");

        mOWindowFlashRoutine = null;
    }

    private System.Collections.IEnumerator fRunWindowFlash(float pfSeconds, bool pbBlockInput)
    {
        // While the picture is black, input is off as well - you are asleep after all.
        // The same switch as for conversation and cutscene.
        if (pbBlockInput && mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.HoldUiModal("window flash");

        mWindowFlashImage.enabled = true;

        yield return new WaitForSeconds(pfSeconds);

        mWindowFlashImage.enabled = false;

        if (pbBlockInput && mOUi.mControlSchemeRef != null)
            mOUi.mControlSchemeRef.ReleaseUiModal("window flash");

        mOWindowFlashRoutine = null;
    }

    private Image mWindowFlashImage;

    /// <summary>The picture in the 3D window - created before the main frame so that it sits IN the hole and the frame lies above it, exactly as in the original, where the UI around it stays visible.</summary>
    internal void Build()
    {
        mWindowPictureImage = UWGameUI.fCreateImage("WindowPicture", mOUi.mGameFrame,
            UWGameUI.windowHoleLeft, -UWGameUI.windowHoleTop, 1f, 1f, Color.white);
        mWindowPictureImage.enabled = false;
    }
}
