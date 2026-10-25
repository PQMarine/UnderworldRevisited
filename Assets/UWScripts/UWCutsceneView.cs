using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE PICTURE OF A CUTSCENE (cleanup step 8.7, 2026-10-24: out of UWIntroPlayer): the canvas
/// with the picture track, the subtitle, the black backdrop and the frames - drawing the frames
/// the interpreter (UWCutsceneRunner) asks for, full screen in the 320x200 frame or inside the 3D
/// viewport, there in the view window (classic, Classic Wide) or in the modern scheme's leather
/// frame. Also the full-screen bitmaps of the logos and the victory screens. Owned by
/// UWIntroPlayer, which decides when; this class only how it looks. Moved as it was.
/// </summary>
public sealed class UWCutsceneView
{
    /// <summary>Line width of cutscene text: UW.EXE CutsceneDisplayString_ovr105_3B6 breaks a
    /// line once it would exceed 0x140 (320) pixels, at most six lines.</summary>
    private const int SubtitleWidth = 320;

    public const int ScreenWidth = 320;

    public const int ScreenHeight = 200;

    private readonly UWIntroPlayer mOPlayer;

    private Canvas mOCanvas;

    private RawImage mOPictureImage;

    /// <summary>The centred 320x200 frame a FULL-SCREEN picture sits in, kept at the pixel
    /// proportion of the classic screen (UWDisplayAspect) by UWPixelAspectFrame - the same frame the game,
    /// the conversation and the menus use. Until 2026-09-22 a full-screen picture was stretched
    /// over the whole window instead, so on a wide screen the logos, the title and the intro
    /// came out neither 4:3 nor 16:10 (per user: "take the 4:3 fix into account").</summary>
    private RectTransform mOFullScreenFrame;

    /// <summary>Black behind a full-screen picture, so the bars beside the frame do not show
    /// the game. Follows the picture, see LateUpdate.</summary>
    private Image mOBackdrop;

    private RawImage mOSubtitleImage;

    private Texture2D mOPictureTexture;

    private Color32[] mOPictureBuffer;

    private UWCutscene mOAnimation;

    /// <summary>Brightness of the picture track, 0 dark to 1 bright - for the fades.</summary>
    private float mfBrightness = 1f;

    /// <summary>Time from which the subtitle is hidden.</summary>
    private float mfSubtitleUntil;

    /// <summary>Does the running cutscene play inside the viewport? See PlacePicture.</summary>
    private bool mbInViewport;

    public UWCutsceneView(UWIntroPlayer pOPlayer)
    {
        mOPlayer = pOPlayer;
    }

    /// <summary>The open animation file, null while the picture track pauses.</summary>
    public UWCutscene Animation => mOAnimation;

    /// <summary>Does the running cutscene play inside the viewport? Set before the first file
    /// opens - its viewport crop already needs it.</summary>
    public bool InViewport
    {
        get { return mbInViewport; }
        set { mbInViewport = value; }
    }

    /// <summary>The picture track's pixels, bottom row first - for the victory screens' text.
    /// Null before the canvas exists.</summary>
    public Color32[] PictureBuffer => mOPictureBuffer;

    /// <summary>
    /// Full screen or the 3D viewport.
    ///
    /// IN THE VIEWPORT the frames are handled like the window and gravestone pictures: their
    /// content sits somewhere on the 320x200 canvas (bottom right), so the painted block is
    /// cut out and placed where UWGameUI puts those pictures. Scaling the whole canvas into the
    /// viewport put the death cutscene too far down and right (per user, 2026-09-14). The block
    /// is the union of all frames of the animation file (see fPlaceViewportCrop).
    /// </summary>
    public void PlacePicture(bool pbInViewport)
    {
        mbInViewport = pbInViewport;

        if (mOPictureImage == null)
            return;

        RectTransform lORect = (RectTransform)mOPictureImage.transform;

        // FULL SCREEN goes into the 320x200 frame and so follows the 4:3 switch; IN THE VIEWPORT
        // the picture hangs on the canvas itself, because fPlaceViewportCrop sets its anchors
        // in screen fractions.
        Transform lOParent = pbInViewport || mOFullScreenFrame == null
            ? mOCanvas.transform
            : mOFullScreenFrame;

        if (lORect.parent != lOParent)
        {
            lORect.SetParent(lOParent, false);

            // Stay beneath the subtitle.
            lORect.SetSiblingIndex(lOParent == mOCanvas.transform ? 2 : 0);
        }

        if (!pbInViewport)
        {
            lORect.anchorMin = Vector2.zero;
            lORect.anchorMax = Vector2.one;
            lORect.offsetMin = Vector2.zero;
            lORect.offsetMax = Vector2.zero;
            mOPictureImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    /// <summary>Cuts the painted block of the given animation file out of the canvas and puts
    /// it at the window picture position in the viewport.</summary>
    private void fPlaceViewportCrop(string psPath)
    {
        if (!mbInViewport || mOPictureImage == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi == null)
            return;

        RectInt lOBlock = fMeasurePaintedBlock(psPath);

        // ONE FIXED CANVAS OFFSET for all viewport cutscenes. Centring each file's own block
        // put the final death cutscene (CS403) 10 pixels too low - its painted area sits
        // elsewhere on the canvas than that of CS402. The offset is taken from CS402, whose
        // placement is confirmed (per user, 2026-09-14: "the sequence sits perfectly").
        Vector2 lOOffset = fGetViewportCanvasOffset(lOUi, psPath, lOBlock);
        Vector2 lOCorner = new Vector2(lOBlock.x, lOBlock.y) + lOOffset;

        // The final death cutscene sits 2 reference pixels LOWER than the CS402 offset gives.
        // UW.EXE PlayCutscene_ovr105_1402 passes the viewport rectangle with its top 9 rows higher
        // (row 10 instead of 19) and 11 rows taller (123 instead of 112) for 0x103; with the canvas
        // bottom-aligned into that rectangle, canvas row r lands on screen row r - 67 instead of
        // r - 69. Per user on 2026-09-14 first "2 pixels too low", then with -2 "4 pixels too
        // high" - the second measurement agrees with UW.EXE.
        if (Path.GetFileName(psPath).StartsWith(FinalDeathFilePrefix, System.StringComparison.OrdinalIgnoreCase))
            lOCorner.y += FinalDeathNudgeY;
        Rect lOScreen = lOUi.ClassicScreen.GetUiPictureScreenRect(lOCorner, new Vector2(lOBlock.width, lOBlock.height));

        // THE MODERN SCHEME hides the classic view window: the picture lies in the middle in a
        // leather frame at the UI's size, as the view's pictures do (UWModernHud, per user
        // 2026-10-04: the anvil's sequence was not in such a frame).
        mbModernFrame = UWControlScheme.Ui.Policy.CutsceneInLeatherFrame;

        if (mbModernFrame)
            lOScreen = fPlaceModernFrame(lOUi, lOBlock);

        RectTransform lORect = (RectTransform)mOPictureImage.transform;

        lORect.anchorMin = new Vector2(lOScreen.xMin / Screen.width, lOScreen.yMin / Screen.height);
        lORect.anchorMax = new Vector2(lOScreen.xMax / Screen.width, lOScreen.yMax / Screen.height);
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;

        // The picture texture is built bottom-up (see ShowFrame).
        mOPictureImage.uvRect = new Rect((float)lOBlock.x / ScreenWidth,
            (float)(ScreenHeight - lOBlock.y - lOBlock.height) / ScreenHeight,
            (float)lOBlock.width / ScreenWidth, (float)lOBlock.height / ScreenHeight);
    }

    /// <summary>The modern scheme's frame around a viewport cutscene (fPlaceModernFrame).</summary>
    private RawImage mOModernFrame;

    private Texture2D mOModernFrameTexture;

    private bool mbModernFrame;

    private const int ModernFrameBorder = 3;

    /// <summary>The block's place in the middle of the screen at the modern UI's size, and the
    /// leather frame around it; returns the picture's screen rectangle.</summary>
    private Rect fPlaceModernFrame(UWGameUI pOUi, RectInt pOBlock)
    {
        float liScale = UWModernUi.PixelScale;
        float lfWidth = pOBlock.width * liScale;
        float lfHeight = pOBlock.height * liScale;
        Rect lOPicture = new Rect(Mathf.Round((Screen.width - lfWidth) * 0.5f), Mathf.Round((Screen.height * 0.55f) - (lfHeight * 0.5f)),
            lfWidth, lfHeight);

        int liFrameWidth = pOBlock.width + UWModernHudArt.LeatherLeft + UWModernHudArt.LeatherRight + (2 * ModernFrameBorder);
        int liFrameHeight = pOBlock.height + UWModernHudArt.LeatherTop + UWModernHudArt.LeatherBottom + (2 * ModernFrameBorder);

        if (mOModernFrame == null)
        {
            GameObject lOObject = new GameObject("Modern frame", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mOCanvas.transform, false);
            mOModernFrame = lOObject.GetComponent<RawImage>();
            mOModernFrame.raycastTarget = false;
            UWPixelArtUI.Apply(mOModernFrame);
        }

        if (mOModernFrameTexture == null || mOModernFrameTexture.width != liFrameWidth || mOModernFrameTexture.height != liFrameHeight)
        {
            if (mOModernFrameTexture != null)
                Object.Destroy(mOModernFrameTexture);

            mOModernFrameTexture = UWModernHudArt.BuildLeather(pOUi.mOUWData.Textures, liFrameWidth, liFrameHeight, pOUi.TextureFilterMode);
            mOModernFrame.texture = mOModernFrameTexture;
        }

        // BENEATH THE PICTURE - only moved when it lies above it. Setting the frame to the
        // picture's index every time put it beneath on the first play and ABOVE on the second
        // (the frame already stood before the picture, so the picture slid down one place), so
        // every second anvil sequence showed nothing but leather (per user, 2026-10-12).
        int liPicture = mOPictureImage.transform.GetSiblingIndex();

        if (mOModernFrame.transform.GetSiblingIndex() > liPicture)
            mOModernFrame.transform.SetSiblingIndex(liPicture);

        Rect lOFrame = new Rect(lOPicture.x - ((UWModernHudArt.LeatherLeft + ModernFrameBorder) * liScale),
            lOPicture.y - ((UWModernHudArt.LeatherBottom + ModernFrameBorder) * liScale), liFrameWidth * liScale, liFrameHeight * liScale);
        RectTransform lORect = (RectTransform)mOModernFrame.transform;

        lORect.anchorMin = new Vector2(lOFrame.xMin / Screen.width, lOFrame.yMin / Screen.height);
        lORect.anchorMax = new Vector2(lOFrame.xMax / Screen.width, lOFrame.yMax / Screen.height);
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;

        return lOPicture;
    }

    /// <summary>Files of the final death cutscene (0x103) and their vertical nudge in reference
    /// pixels, positive is down - see fPlaceViewportCrop.</summary>
    private const string FinalDeathFilePrefix = "CS403.";

    private const float FinalDeathNudgeY = 2f;

    /// <summary>The reference file for the canvas offset of viewport cutscenes (death with
    /// silver tree, placement confirmed).</summary>
    private const string ViewportReferenceFile = "CS402.N01";

    private static bool mbViewportOffsetKnown;

    private static Vector2 mOViewportOffset;

    /// <summary>UI position minus canvas position, derived once from the reference file placed
    /// like a window picture. Falls back to the given file itself if the reference is missing.
    /// </summary>
    private Vector2 fGetViewportCanvasOffset(UWGameUI pOUi, string psPath, RectInt pOBlock)
    {
        if (!mbViewportOffsetKnown)
        {
            string lsReference = Path.Combine(mOPlayer.CutsPath, ViewportReferenceFile);
            RectInt lOReference = File.Exists(lsReference) ? fMeasurePaintedBlock(lsReference) : pOBlock;

            mOViewportOffset = UWClassicScreen.GetViewportPictureCorner(new Vector2(lOReference.width, lOReference.height))
                - new Vector2(lOReference.x, lOReference.y);
            mbViewportOffsetKnown = File.Exists(lsReference);
        }

        return mOViewportOffset;
    }

    /// <summary>The painted block of a file (UWCutscene.MeasurePaintedBlock, here until
    /// 2026-10-24) as a rectangle.</summary>
    private static RectInt fMeasurePaintedBlock(string psPath)
    {
        UWCutscene.MeasurePaintedBlock(psPath, out int liX, out int liY, out int liWidth, out int liHeight);

        return new RectInt(liX, liY, liWidth, liHeight);
    }

    /// <summary>Opens an animation file of the CUTS folder, places it (fPlaceViewportCrop) and
    /// shows its frame 0; a missing or unreadable file pauses the picture track.</summary>
    public UWCutscene OpenAnimation(string psFileName)
    {
        try
        {
            string lsPath = Path.Combine(mOPlayer.CutsPath, psFileName);

            if (!File.Exists(lsPath))
            {
                Debug.LogWarning("UWIntroPlayer: " + psFileName + " missing, picture track paused.");
                mOAnimation = null;
                return null;
            }

            mOAnimation = new UWCutscene(lsPath);

            fPlaceViewportCrop(lsPath);

            ShowFrame(0);
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("UWIntroPlayer: " + psFileName + " not readable - " + lOError.Message);
            mOAnimation = null;
        }

        return mOAnimation;
    }

    /// <summary>Shows a frame. If the number is outside the file, the last frame simply
    /// stays up - the counter keeps running anyway, because the script regularly
    /// counts past the end of the file and playback would otherwise get stuck.</summary>
    public void ShowFrame(int piFrame)
    {
        if (mOAnimation == null || mOPictureImage == null)
            return;

        if (piFrame < 0 || piFrame >= mOAnimation.FrameCount)
            return;

        byte[] lyPixels = mOAnimation.GetFrame(piFrame);

        for (int liY = 0; liY < mOAnimation.Height; liY++)
        {
            // The texture is built bottom-up, the picture top-down.
            int liSourceRow = liY * mOAnimation.Width;
            int liTargetRow = (mOAnimation.Height - 1 - liY) * mOAnimation.Width;

            for (int liX = 0; liX < mOAnimation.Width; liX++)
            {
                byte lyIndex = lyPixels[liSourceRow + liX];

                mOPictureBuffer[liTargetRow + liX] = new Color32(
                    mOAnimation.PaletteRgb[(lyIndex * 3) + 0],
                    mOAnimation.PaletteRgb[(lyIndex * 3) + 1],
                    mOAnimation.PaletteRgb[(lyIndex * 3) + 2],
                    255);
            }
        }

        mOPictureTexture.SetPixels32(mOPictureBuffer);
        mOPictureTexture.Apply();
        mOPictureImage.enabled = true;
    }

    /// <summary>Tints the picture track - the fades, and 1 before the logos and the victory
    /// screens.</summary>
    public void SetBrightness(float pfBrightness)
    {
        mfBrightness = pfBrightness;

        if (mOPictureImage != null)
            mOPictureImage.color = new Color(mfBrightness, mfBrightness, mfBrightness, 1f);
    }

    /// <summary>A line of the interpreter: its subtitle (null while the recording speaks) and
    /// from when it goes - pfUntil on the unscaled clock.</summary>
    public void ShowLine(string psSubtitle, int piColourIndex, float pfUntil)
    {
        ShowSubtitle(psSubtitle, fGetColour(piColourIndex));

        mfSubtitleUntil = pfUntil;
    }

    /// <summary>Hides the subtitle once its time is up - asked every frame while a sequence
    /// plays.</summary>
    public void HideSubtitleWhenDue()
    {
        if (mOSubtitleImage != null && UWTextLabel.Get(mOSubtitleImage).IsShown && Time.unscaledTime >= mfSubtitleUntil)
            UWTextLabel.Hide(mOSubtitleImage);
    }

    /// <summary>
    /// The colour value from the script is a palette index - into the palette of the running
    /// animation file. Until 2026-09-14 palette 0 from PALS.DAT was used here: there 244 and
    /// 245 are almost black, and the lines of the Baron and his daughter in the end sequence stayed
    /// invisible (per user). In the files of CS000 and CS001, 240 is grey, 241 blue,
    /// 244 purple and 245 orange. Palette 0 remains the fallback when no file is open.
    /// </summary>
    private Color32 fGetColour(int piColourIndex)
    {
        if (mOAnimation != null && mOAnimation.PaletteRgb != null && piColourIndex >= 0 && piColourIndex < 256)
            return new Color32(mOAnimation.PaletteRgb[piColourIndex * 3], mOAnimation.PaletteRgb[(piColourIndex * 3) + 1],
                mOAnimation.PaletteRgb[(piColourIndex * 3) + 2], 255);

        try
        {
            byte[] lyRgb = mOPlayer.Data.Palettes.GetPalette(0).GetRGB((byte)piColourIndex);

            if (lyRgb[0] == 0 && lyRgb[1] == 0 && lyRgb[2] == 0)
                return new Color32(255, 255, 255, 255);

            return new Color32(lyRgb[0], lyRgb[1], lyRgb[2], 255);
        }
        catch
        {
            return new Color32(255, 255, 255, 255);
        }
    }

    public void ShowSubtitle(string psText, Color32 pOColour)
    {
        if (mOSubtitleImage == null)
            return;

        if (string.IsNullOrEmpty(psText))
        {
            UWTextLabel.Hide(mOSubtitleImage);
            return;
        }

        // FONTBIG.SYS: UW.EXE PlayCutscene_ovr105_1402 opens it before every cutscene and
        // switches back to font5x6p.sys afterwards. The end sequence text was too small with the
        // normal font (per user, 2026-09-14).
        UWFont lOFont = mOPlayer.Data.Fonts.Get(UWFonts.FontType.Big);
        List<string> lOLines = UWFontRenderer.WrapText(lOFont, psText, SubtitleWidth);

        // Centred, as the cutscenes show it; the old subtitle goes back to UWTextTextures.
        UWTextLabel.Get(mOSubtitleImage).SetText(lOFont, lOLines, SubtitleWidth, new[] { pOColour }, UWTextRaster.ColoursEnum.Codes,
            true, FilterMode.Point);
    }

    /// <summary>
    /// Draws one of the full-screen pictures from the BYT files into the picture track. Row 0 of the
    /// source is at the top, Unity starts at the bottom - hence the flip.
    /// </summary>
    public bool DrawFullScreenBitmap(UWTexture.TextureTypes peType)
    {
        EnsureCanvas();
        PlacePicture(false);

        DataImport lOData = mOPlayer.Data;

        if (lOData == null || mOPictureBuffer == null)
            return false;

        UWTexture lOSource;

        if (!lOData.Textures.BitmapFiles.TryGetValue(peType, out lOSource) || lOSource == null)
            return false;

        UWColor32[] lOColours = lOSource.GetUWColor32();

        if (lOColours == null)
            return false;

        for (int liPixel = 0; liPixel < mOPictureBuffer.Length; liPixel++)
            mOPictureBuffer[liPixel] = new Color32(0, 0, 0, 255);

        int liWidth = Mathf.Min(lOSource.Width, ScreenWidth);
        int liHeight = Mathf.Min(lOSource.Height, ScreenHeight);

        for (int liY = 0; liY < liHeight; liY++)
        {
            int liSourceRow = (lOSource.Height - 1 - liY) * lOSource.Width;
            int liTargetRow = liY * ScreenWidth;

            for (int liX = 0; liX < liWidth; liX++)
            {
                int liSource = liSourceRow + liX;

                if (liSource < 0 || liSource >= lOColours.Length)
                    continue;

                mOPictureBuffer[liTargetRow + liX] =
                    new Color32(lOColours[liSource].R, lOColours[liSource].G, lOColours[liSource].B, 255);
            }
        }

        mOPictureTexture.SetPixels32(mOPictureBuffer);
        mOPictureTexture.Apply(false, false);

        return true;
    }

    /// <summary>Clears the picture track to black without hiding it.</summary>
    public void FillBlack()
    {
        if (mOPictureBuffer == null || mOPictureTexture == null)
            return;

        for (int liPixel = 0; liPixel < mOPictureBuffer.Length; liPixel++)
            mOPictureBuffer[liPixel] = new Color32(0, 0, 0, 255);

        UploadPicture();
    }

    /// <summary>Hands the picture track's pixels to its texture (after FillBlack or the victory
    /// screens' text).</summary>
    public void UploadPicture()
    {
        mOPictureTexture.SetPixels32(mOPictureBuffer);
        mOPictureTexture.Apply(false, false);
    }

    public void ShowPicture()
    {
        mOPictureImage.enabled = true;
    }

    public void HidePicture()
    {
        if (mOPictureImage != null)
            mOPictureImage.enabled = false;
    }

    public void EnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        // Fitted whole into the window, bars over and under it when narrow (UWUiFit).
        mOCanvas = UWUiKit.CreateClassicCanvas(mOPlayer.transform, "UW Intro Canvas", UWUiSorting.Intro);

        // The black backdrop first, then the frame for full-screen pictures - see mOFullScreenFrame.
        mOBackdrop = UWUiKit.CreateBackdrop(mOCanvas.transform, "Backdrop", Color.black);
        mOBackdrop.enabled = false;

        mOFullScreenFrame = UWUiKit.CreateClassicFrame(mOCanvas.transform, "Full screen frame");

        // Picture track first, so the subtitle lies on top of it.
        mOPictureTexture = new Texture2D(ScreenWidth, ScreenHeight, TextureFormat.RGBA32, false);
        mOPictureTexture.name = "UWIntroPlayer.cs:988";
        mOPictureTexture.filterMode = FilterMode.Point;
        mOPictureTexture.wrapMode = TextureWrapMode.Clamp;
        mOPictureBuffer = new Color32[ScreenWidth * ScreenHeight];

        // Over the whole canvas; the pivot stays in the middle, Unity's default.
        mOPictureImage = UWUiKit.CreateRawImage(mOCanvas.transform, "Picture", UWUiKit.Centre, UWUiKit.Centre);
        UWUiKit.Stretch(mOPictureImage.rectTransform);
        mOPictureImage.texture = mOPictureTexture;

        mOSubtitleImage = UWUiKit.CreateRawImage(mOCanvas.transform, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        mOSubtitleImage.rectTransform.anchoredPosition = new Vector2(0f, mOPlayer.SubtitleBottomOffset);
    }

    /// <summary>The black backdrop follows the picture: on behind every full-screen picture,
    /// off in the viewport, where the game around it belongs to the scene. After the
    /// coroutines and before drawing, so it never lags a frame.</summary>
    public void LateUpdate()
    {
        if (mOBackdrop == null || mOPictureImage == null)
            return;

        bool lbOn = mOPictureImage.enabled && !mbInViewport;

        if (mOBackdrop.enabled != lbOn)
            mOBackdrop.enabled = lbOn;

        if (mOModernFrame != null)
            mOModernFrame.enabled = mOPictureImage.enabled && mbInViewport && mbModernFrame;

        // WITH THE HELP OPEN THE PICTURE SLIDES LEFT with the game frame (per user, 2026-09-30:
        // a dream played while the help was open lay partly under it) - the same shift the
        // conversation's frame follows (UWClassicConversationRenderer.Tick).
        if (mOFullScreenFrame != null)
            mOFullScreenFrame.anchoredPosition = new Vector2(UWHelpLayout.FrameShift(UWUiFit.CanvasWidth), 0f);
    }
}
