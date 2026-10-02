using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// Building blocks for the original's full screens - main menu (UWMainMenu) and
/// character creation (UWCharacterCreationScreen).
///
/// Both sit on their own canvas with the reference resolution 320x200 in the centre,
/// behind it a black area across the whole screen (the images are 4:3, the
/// screen usually is not). The images come with their REAL colours from the respective
/// palette - OPSCR.BYT with palette 2, CHARGEN.BYT and CHRBTNS.GR with palette 3 - not
/// through the palette shader of the game UI (UWIconPalette), which always looks up
/// palette 0 and would colour these images wrongly.
/// </summary>
public static class UWScreenUi
{
    public const int ScreenWidth = 320;

    public const int ScreenHeight = 200;

    /// <summary>Canvas with a black backdrop and the 320x200 frame in the centre.</summary>
    public static Canvas CreateCanvas(Transform pOParent, string psName, int piSortingOrder, out RectTransform pOFrame)
    {
        GameObject lOCanvasObject = new GameObject(psName, typeof(Canvas), typeof(CanvasScaler));
        lOCanvasObject.transform.SetParent(pOParent, false);

        Canvas lOCanvas = lOCanvasObject.GetComponent<Canvas>();
        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lOCanvas.sortingOrder = piSortingOrder;

        CanvasScaler lOScaler = lOCanvasObject.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        lOScaler.referenceResolution = new Vector2(ScreenWidth, ScreenHeight);
        lOScaler.matchWidthOrHeight = 1f;

        // Fitted whole into the window, bars over and under it when narrow (UWUiFit).
        lOCanvasObject.AddComponent<UWFitCanvas>();

        GameObject lOBackdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        lOBackdrop.transform.SetParent(lOCanvasObject.transform, false);

        RectTransform lOBackdropRect = (RectTransform)lOBackdrop.transform;
        lOBackdropRect.anchorMin = Vector2.zero;
        lOBackdropRect.anchorMax = Vector2.one;
        lOBackdropRect.offsetMin = Vector2.zero;
        lOBackdropRect.offsetMax = Vector2.zero;

        Image lOBackdropImage = lOBackdrop.GetComponent<Image>();
        lOBackdropImage.color = Color.black;
        lOBackdropImage.raycastTarget = false;

        GameObject lOFrameObject = new GameObject("Frame", typeof(RectTransform));
        lOFrameObject.transform.SetParent(lOCanvasObject.transform, false);

        pOFrame = (RectTransform)lOFrameObject.transform;
        lOFrameObject.AddComponent<UWPixelAspectFrame>();
        pOFrame.anchorMin = new Vector2(0.5f, 0.5f);
        pOFrame.anchorMax = new Vector2(0.5f, 0.5f);
        pOFrame.pivot = new Vector2(0.5f, 0.5f);
        pOFrame.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        pOFrame.anchoredPosition = Vector2.zero;

        return lOCanvas;
    }

    /// <summary>A RawImage with its top left corner at (left, top) in original pixels.
    /// </summary>
    public static RawImage CreateRawImage(RectTransform pOParent, string psName, int piLeft, int piTop)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(piLeft, -piTop);

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.enabled = false;

        return lOImage;
    }

    public static void Place(RawImage pOImage, int piLeft, int piTop)
    {
        ((RectTransform)pOImage.transform).anchoredPosition = new Vector2(piLeft, -piTop);
    }

    /// <summary>Shows a texture at original size.</summary>
    public static void SetTexture(RawImage pOImage, Texture2D pOTexture)
    {
        if (pOImage == null)
            return;

        if (pOTexture == null)
        {
            pOImage.enabled = false;
            return;
        }

        pOImage.texture = pOTexture;
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(pOTexture.width, pOTexture.height);
        pOImage.enabled = true;
    }

    /// <summary>
    /// An image from the game data with the colours of its own palette.
    ///
    /// pbZeroTransparent: colour index 0 becomes transparent. In CHRBTNS.GR index 0 is two things -
    /// the black outline of the buttons (opaque, visible in the user's photos of the
    /// original) and the empty background of the focus frames, bodies and faces (transparent).
    /// Which applies is therefore decided by the caller per image.
    /// </summary>
    /// <summary>piTransparentIndex: an extra palette index that becomes transparent - the flask
    /// pictures, for instance, are full rectangles with the panel stone (index 3) around them and
    /// have to be cut out to stand on their own (per user, 2026-09-17).</summary>
    public static Texture2D BuildTexture(UWTexture pOSource, FilterMode peFilter, bool pbZeroTransparent = false,
        int piTransparentIndex = -1)
    {
        if (pOSource == null || pOSource.PaletteIndices == null)
            return null;

        UWColor32[] lyColours = pOSource.GetUWColor32();
        byte[] lyIndices = pbZeroTransparent || piTransparentIndex >= 0 ? pOSource.GetMainPaletteIndices() : null;
        int liWidth = pOSource.Width;
        int liHeight = pOSource.Height;

        Color32[] lyPixels = new Color32[liWidth * liHeight];

        for (int liY = 0; liY < liHeight; liY++)
        {
            for (int liX = 0; liX < liWidth; liX++)
            {
                int liSource = (liY * liWidth) + liX;

                if (liSource >= lyColours.Length)
                    continue;

                UWColor32 lOColour = lyColours[liSource];

                bool lbTransparent = lyIndices != null && liSource < lyIndices.Length
                    && ((pbZeroTransparent && lyIndices[liSource] == 0) || lyIndices[liSource] == piTransparentIndex);

                byte lyAlpha = lbTransparent ? (byte)0 : lOColour.A;

                // Unity counts rows from the bottom.
                // The menu screens are built as colours, not as palette indices - the colour help
                // has to be applied here as well (see UWColourVision).
                lyPixels[((liHeight - 1 - liY) * liWidth) + liX] =
                    UWColourVision.Apply(new Color32(lOColour.R, lOColour.G, lOColour.B, lyAlpha));
            }
        }

        Texture2D lOTexture = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWScreenUi.cs:145";
        lOTexture.filterMode = peFilter;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lyPixels);
        lOTexture.Apply();

        return lOTexture;
    }

    /// <summary>An image from a GR or BYT file, or null if it is missing.</summary>
    public static Texture2D BuildTexture(UWTextures pOTextures, UWTexture.TextureTypes peType, int piIndex, FilterMode peFilter,
        bool pbZeroTransparent = false)
    {
        if (pOTextures == null)
            return null;

        UWTexture lOSource;

        try
        {
            lOSource = pOTextures.GetTextureByType(peType, piIndex);
        }
        catch
        {
            return null;
        }

        return BuildTexture(lOSource, peFilter, pbZeroTransparent);
    }

    /// <summary>A palette colour as a Unity colour.</summary>
    public static Color32 GetColour(UWPalettes pOPalettes, int piPalette, int piIndex)
    {
        if (pOPalettes == null)
            return new Color32(255, 255, 255, 255);

        UWPalette lOPalette = pOPalettes.GetPalette(piPalette);

        if (lOPalette == null)
            return new Color32(255, 255, 255, 255);

        UWColor32 lOColour = lOPalette.GetUWColor(piIndex);

        return new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
    }

    /// <summary>
    /// Shows text in an original font, or optionally in the modern one (see UWTextLabel).
    /// The field's old texture is released.
    /// </summary>
    public static void SetText(RawImage pOImage, UWFont pOFont, List<string> pOLines, Color32 pOColour,
        int piWidth, bool pbCentred)
    {
        if (pOImage == null)
            return;

        if (pOFont == null || pOLines == null || pOLines.Count == 0)
        {
            UWTextLabel.Hide(pOImage);
            return;
        }

        if (pOImage.texture != null)
            Object.Destroy(pOImage.texture);

        Texture2D lOTexture = UWFontRenderer.RenderLines(pOFont, pOLines, piWidth, pOColour, FilterMode.Point, pbCentred);

        UWTextLabel.Get(pOImage).Set(pOFont, lOTexture, pOLines, new List<Color32> { pOColour }, pbCentred);
    }

    public static void SetText(RawImage pOImage, UWFont pOFont, string psText, Color32 pOColour, int piWidth, bool pbCentred)
    {
        SetText(pOImage, pOFont, string.IsNullOrEmpty(psText) ? null : new List<string> { psText }, pOColour, piWidth, pbCentred);
    }

    /// <summary>Is the pointer over the field?</summary>
    public static bool IsMouseOver(RawImage pOImage)
    {
        if (pOImage == null || !pOImage.gameObject.activeInHierarchy)
            return false;

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)pOImage.transform,
            lOMouse.position.ReadValue(), null);
    }

    public static bool WasLeftClicked()
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        return lOMouse != null && UWMouseButtons.LeftPressed;
    }

    /// <summary>
    /// Is the main menu or character creation showing? Then the only pointer is the cross
    /// (UWGameUI), and the special keys B, N and F stay silent (DebugFunctions,
    /// UWGameUI.fCheckFontToggle) - per user, 2026-09-11. Otherwise, typing the name
    /// would toggle brightness and spectator mode as a side effect.
    /// </summary>
    public static bool IsScreenMenuOpen
    {
        get { return UWMainMenu.IsOpen || UWCharacterCreationScreen.IsAnyOpen; }
    }

    // ------------------------------------------------------------------
    // Loading cover
    // ------------------------------------------------------------------

    /// <summary>Above everything, including the intro (100).</summary>
    private const int LoadingCoverSortingOrder = 1000;

    private static GameObject msLoadingCover;

    /// <summary>
    /// Black across the whole screen until HideLoadingCover is called - and that across
    /// a scene restart (DontDestroyOnLoad).
    ///
    /// After character creation the scene is rebuilt. Without a cover you saw
    /// the old world in the last frame and, during loading, whatever was left of it
    /// (per user, 2026-09-11: "an immediate black screen until loading is finished"). It is
    /// removed by UWLevelLoader.Start once the new character's world is built (no intro follows
    /// character creation since 2026-09-14).
    /// </summary>
    public static void ShowLoadingCover()
    {
        if (msLoadingCover != null)
        {
            msLoadingCover.SetActive(true);
            SetLoadingCoverAlpha(1f);
            return;
        }

        msLoadingCover = new GameObject("UW Loading Cover", typeof(Canvas));
        Object.DontDestroyOnLoad(msLoadingCover);

        Canvas lOCanvas = msLoadingCover.GetComponent<Canvas>();
        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lOCanvas.sortingOrder = LoadingCoverSortingOrder;

        GameObject lOBlack = new GameObject("Black", typeof(RectTransform), typeof(Image));
        lOBlack.transform.SetParent(msLoadingCover.transform, false);

        RectTransform lORect = (RectTransform)lOBlack.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.one;
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;

        Image lOImage = lOBlack.GetComponent<Image>();
        lOImage.color = Color.black;
        lOImage.raycastTarget = false;
        msLoadingCoverImage = lOImage;
    }

    private static Image msLoadingCoverImage;

    /// <summary>The cover as a fade: 0 transparent, 1 black. The end of a conversation
    /// uses it to fade out and back in (see UWConversationScreen).</summary>
    public static void SetLoadingCoverAlpha(float pfAlpha)
    {
        if (msLoadingCoverImage != null)
            msLoadingCoverImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(pfAlpha));
    }

    public static void HideLoadingCover()
    {
        if (msLoadingCover != null)
        {
            Object.Destroy(msLoadingCover);
            msLoadingCover = null;
        }
    }
}
