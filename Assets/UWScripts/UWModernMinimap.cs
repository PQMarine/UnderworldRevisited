using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S MINIMAP (per user on a mockup, 2026-10-03): a square at the top right, as
/// wide as the opened character panel (UWModernPanel), which hides it while it is open.
///
///   - The map in the original's look - the parchment, the discovered tiles, the notes - as the
///     help window's map tab draws it (UWAutomapPictures since 2026-10-19, before that
///     UWHudMap.BuildMinimapTiles and its fellows), around the character, refreshed every half
///     second and on every step. Only with the map carried, as the real one.
///   - North up by default; turning with the view is an option of the layout editor's inspector
///     on the minimap (UWUserSettings.MinimapTurns - per user: some prefer it; in the game menu
///     until 2026-10-09).
///   - Zoom with the + and - on it and with the mouse wheel over it (UWUserSettings.MinimapZoom,
///     whole screen pixels per map pixel, so the pixels stay hard).
///   - A click on the map opens the big map, full screen as in the original (UWGameUI.OpenBigMap,
///     since 2026-10-04; before, the help's map tab).
///   - The buttons need the free pointer (the right button), as everything clickable here.
///   - With the character panel PINNED it lies in the panel's head, without its own frame
///     (UWModernPanel.TryGetMinimapArea) - after the panel in the frame, so it follows at once.
/// </summary>
[DefaultExecutionOrder(UWUpdateOrder.ModernMinimap)]
public class UWModernMinimap : MonoBehaviour
{
    public static UWModernMinimap Instance { get; private set; }

    /// <summary>The layout in original pixels.</summary>
    public const int Size = 96;

    private const int Margin = 4;

    /// <summary>How many tiles across the default zoom shows.</summary>
    private const int DefaultTilesAcross = 16;

    private const int MinZoom = 1;

    private const int MaxZoom = 24;

    private const float RefreshSeconds = 0.5f;

    private const float ArrowSize = 6f;

    private const float ButtonSize = 7f;

    /// <summary>The map screen (BLNKMAP) the tiles and notes are placed on.</summary>
    private const float MapWidth = 320f;

    private const float MapHeight = 200f;

    private UWGameUI mOUi;

    private UWControlScheme mOScheme;

    private UWHelpWindow mOHelp;

    private Canvas mOCanvas;

    private RawImage mOFrame;

    private RectTransform mOViewport;

    private RectTransform mOContent;

    private RawImage mOParchment;

    private RawImage mOTiles;

    private readonly List<RawImage> mONotes = new List<RawImage>();

    private RawImage mOArrow;

    private Text mONorth;

    private Text mOLevel;

    private Text mONoMap;

    private RawImage[] mOButtons;

    private Text[] mOButtonTexts;

    /// <summary>The frame's leather, built for the colours and backs (UWVersionedTexture).</summary>
    private readonly UWVersionedTexture mOFrameArt = new UWVersionedTexture();

    private Texture2D mOArrowTexture;

    private Texture2D mODiscTexture;

    private float miScale = 1f;

    private Rect mORect;

    private readonly Rect[] mOButtonRects = new Rect[2];

    private Vector2 mOBuiltAt = new Vector2(-1f, -1f);

    private float mfBuiltTime = -1f;

    private bool mbShown;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        mOFrameArt.Release();

        foreach (Texture2D lOTexture in new[] { mOArrowTexture, mODiscTexture })
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOHelp = GetComponent<UWHelpWindow>();
        mOScheme = GetComponentInParent<UWControlScheme>();
    }

    /// <summary>Whether the minimap is on screen: the modern HUD shows and the character panel
    /// is shut - or pinned with the minimap in its head (pOHead, else zero).</summary>
    private bool fIsShown(out Rect pOHead)
    {
        pOHead = Rect.zero;

        // Switched off in the layout editor (per user, 2026-10-04: some will not want it at all) -
        // a choice of the own layout, the Modern preset shows it.
        if (UWModernLayout.IsMinimapHidden || !UWModernUi.IsActive
            || !UWModernUi.IsShowing
            || mOUi == null || mOUi.mOUWData == null)
            return false;

        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOPanel != null && lOPanel.TryGetMinimapArea(out pOHead))
            return true;

        pOHead = Rect.zero;

        // AN OPEN PANEL OVER IT hides it, wherever the two lie (per user, 2026-10-04: it shone
        // through the help and took its clicks there) - the character panel and the rune panel.
        UWModernRunePanel lORunes = UWModernRunePanel.Instance;

        // Not in the layout editor: dragged near a panel there it vanished (per user, 2026-10-04).
        if (UWModernLayout.IsEditing)
            return true;

        if (mORect.width > 0f && lOPanel != null && lOPanel.OpenRect.Overlaps(mORect))
            return false;

        return !(mORect.width > 0f && lORunes != null && lORunes.IsOpen && lORunes.ScreenRect.Overlaps(mORect));
    }


    /// <summary>Where the minimap is on the screen, empty while hidden (the active spells keep left
    /// of it, UWModernHud).</summary>
    public Rect ScreenRect => mbShown ? mORect : Rect.zero;

    /// <summary>Whether a screen point lies on the minimap - nothing goes into the world through it.</summary>
    public bool Contains(Vector2 pOPointer)
    {
        return mbShown && mORect.Contains(pOPointer);
    }

    private int fZoom()
    {
        int liZoom = UWUserSettings.MinimapZoom;

        if (liZoom >= MinZoom)
            return Mathf.Clamp(liZoom, MinZoom, MaxZoom);

        float lfInner = (Size - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight) * miScale;

        return Mathf.Clamp(Mathf.RoundToInt(lfInner / (DefaultTilesAcross * UWAutomapPictures.TilePixels)), MinZoom, MaxZoom);
    }

    private void fSetZoom(int piZoom)
    {
        int liZoom = Mathf.Clamp(piZoom, MinZoom, MaxZoom);

        if (UWUserSettings.MinimapZoom == liZoom)
            return;

        UWUserSettings.MinimapZoom = liZoom;
        UWUserSettings.Save();
    }

    // ------------------------------------------------- Input

    private void Update()
    {
        if (!mbShown || mOScheme == null || !mOScheme.IsPointerFree || !UWPointer.HasMouse)
            return;

        if (UWModernUi.IsMenuOpen || UWControls.IsTextEntryActive)
            return;

        Vector2 lOPointer = UWPointer.Position;

        // A bag window lying over the map takes the click (UWModernBags may cover it).
        if (!mORect.Contains(lOPointer)
            || (UWModernBags.Instance != null && UWModernBags.Instance.IsOverWindow(lOPointer)))
            return;

        float lfWheel = UWPointer.Scroll.y;

        if (Mathf.Abs(lfWheel) > 0.01f)
            fSetZoom(fZoom() + (lfWheel > 0f ? 1 : -1));

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null || !lOControls.Player.CursorDrag.WasPressedThisFrame() || UWModernPointer.PanelsHeld)
            return;

        // A thing on the pointer is not put down through the map (UWModernBags asks Contains).
        UWInventory lOInventory = mOUi.mOInventory;

        if (lOInventory != null && lOInventory.CursorItem != null)
            return;

        if (mOButtonRects[0].Contains(lOPointer))
            fSetZoom(fZoom() + 1);
        else if (mOButtonRects[1].Contains(lOPointer))
            fSetZoom(fZoom() - 1);
        // The big map, as the original's full-screen one (per user, 2026-10-04); without the map
        // carried the help's map tab as before.
        else if (mOUi.OpenBigMap())
            return;
        else if (UWModernPanel.Instance != null)
        {
            if (mOHelp != null)
                mOHelp.ShowMapTab();

            UWModernPanel.Instance.Open(UWModernPanel.TabEnum.Help);
        }
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        mbShown = fIsShown(out Rect lOHead);

        if (!mbShown)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        bool lbInHead = lOHead.width > 0f;

        // Its own size and place (UWModernLayout, the layout editor) - not in the pinned panel's head.
        miScale = UWModernUi.PixelScale * (lbInHead ? 1f : UWModernLayout.Scale(UWModernLayout.ElementEnum.Minimap));

        fEnsureArt();

        float liScale = miScale;
        float lfSize = Size * liScale;

        // Over the pinned panel, which shares the sorting order otherwise.
        mOCanvas.sortingOrder = lbInHead ? UWUiSorting.ModernMinimapInHead : UWUiSorting.ModernMinimap;
        mOFrame.enabled = !lbInHead;

        // The map's window inside the leather frame - or in the panel's head.
        float lfWidth = (Size - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight) * liScale;
        float lfHeight = (Size - UWModernHudArt.LeatherTop - UWModernHudArt.LeatherBottom) * liScale;

        if (lbInHead)
        {
            lfWidth = lOHead.width;
            lfHeight = lOHead.height;
            mORect = lOHead;
            UWUiKit.SetRect(mOViewport, lOHead.x, lOHead.y, lfWidth, lfHeight);
            UWModernLayout.Report(UWModernLayout.ElementEnum.Minimap, lOHead);
        }
        else
        {
            float lfMargin = Margin * UWModernUi.PixelScale;

            mORect = UWModernLayout.Place(UWModernLayout.ElementEnum.Minimap,
                new Rect(Screen.width - lfMargin - lfSize, Screen.height - lfMargin - lfSize, lfSize, lfSize));
            UWModernLayout.Report(UWModernLayout.ElementEnum.Minimap, mORect);
            UWUiKit.SetRect(mOFrame.rectTransform, mORect.x, mORect.y, lfSize, lfSize);
            UWUiKit.SetRect(mOViewport, mORect.x + (UWModernHudArt.LeatherLeft * liScale), mORect.y + (UWModernHudArt.LeatherBottom * liScale),
                lfWidth, lfHeight);
        }

        Vector2 lOCentre = new Vector2(lfWidth * 0.5f, lfHeight * 0.5f);
        float lfHeading = Camera.main != null ? Mathf.Repeat(Camera.main.transform.eulerAngles.y, 360f) : 0f;
        bool lbTurns = UWUserSettings.MinimapTurns;
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWAutomapPictures lOMap = mOUi.AutomapPictures;
        bool lbMap = mOUi.IsCarryingMap && lOMap != null && lOMap.TryGetPlayerPixel(out Vector2 lOPlayer);

        mOContent.gameObject.SetActive(lbMap);
        mOArrow.enabled = lbMap;
        mONoMap.enabled = !lbMap;
        mONoMap.text = UWText.T("hud.minimap.no_map");
        mONoMap.fontSize = Mathf.Max(10, Mathf.RoundToInt(4f * liScale));
        UWUiKit.SetRect(mONoMap.rectTransform, 0f, 0f, lfWidth, lfHeight);

        if (lbMap)
        {
            lOMap.TryGetPlayerPixel(out lOPlayer);
            fLayoutMap(lOMap, lOPlayer, lOCentre, lbTurns ? lfHeading : 0f);
        }

        // The character's arrow: it turns with the view on a north-up map, it points up on a
        // turning one.
        float lfArrow = ArrowSize * liScale;

        mOArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        mOArrow.rectTransform.anchoredPosition = lOCentre;
        mOArrow.rectTransform.sizeDelta = new Vector2(lfArrow, lfArrow);
        mOArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, lbTurns ? 0f : -lfHeading);

        // North: at the top, or where north lies on a turning map.
        float lfNorth = lbTurns ? lfHeading * Mathf.Deg2Rad : 0f;
        float lfRadius = (Mathf.Min(lfWidth, lfHeight) * 0.5f) - (4f * liScale);
        Vector2 lONorthAt = lOCentre + new Vector2(-Mathf.Sin(lfNorth), Mathf.Cos(lfNorth)) * lfRadius;

        mONorth.fontSize = Mathf.Max(10, Mathf.RoundToInt(4.5f * liScale));
        mONorth.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        mONorth.rectTransform.anchoredPosition = lONorthAt;
        mONorth.rectTransform.sizeDelta = new Vector2(8f * liScale, 8f * liScale);

        mOLevel.text = lOLoader != null ? UWText.F("hud.minimap.level", lOLoader.CurrentLevelIndex + 1) : string.Empty;
        mOLevel.fontSize = Mathf.Max(9, Mathf.RoundToInt(3.6f * liScale));
        UWUiKit.SetRect(mOLevel.rectTransform, 0f, liScale, lfWidth - (2f * liScale), 6f * liScale);

        // + and - at the bottom left.
        float lfButton = ButtonSize * liScale;

        for (int liButton = 0; liButton < mOButtons.Length; liButton++)
        {
            float lfX = (2f * liScale) + (liButton * (lfButton + liScale));
            float lfY = 2f * liScale;

            UWUiKit.SetRect(mOButtons[liButton].rectTransform, lfX, lfY, lfButton, lfButton);
            // The disc's own rect: the sign is centred by its ink (UWCentredGlyph); the old nudge up
            // by 0.3 put it too high since (per user, 2026-10-09).
            UWUiKit.SetRect(mOButtonTexts[liButton].rectTransform, lfX, lfY, lfButton, lfButton);
            mOButtonTexts[liButton].fontSize = Mathf.Max(10, Mathf.RoundToInt(5.5f * liScale));
            mOButtons[liButton].enabled = lbMap;
            mOButtonTexts[liButton].enabled = lbMap;
            mOButtonRects[liButton] = lbMap ? new Rect(mOViewport.anchoredPosition.x + lfX, mOViewport.anchoredPosition.y + lfY, lfButton, lfButton)
                : Rect.zero;
        }
    }

    /// <summary>The parchment, the tiles and the notes, the character's tile in the middle,
    /// turned by pfTurn degrees (counter-clockwise, so the heading points up).</summary>
    private void fLayoutMap(UWAutomapPictures pOMap, Vector2 pOPlayer, Vector2 pOCentre, float pfTurn)
    {
        int liZoom = fZoom();

        if (pOPlayer != mOBuiltAt || Time.unscaledTime - mfBuiltTime >= RefreshSeconds || mOTiles.texture == null)
        {
            mOTiles.texture = pOMap.BuildTiles(false);
            mOBuiltAt = pOPlayer;
            mfBuiltTime = Time.unscaledTime;
        }

        Texture2D lOParchment = pOMap.GetParchment();

        mOParchment.texture = lOParchment;
        mOParchment.enabled = lOParchment != null;

        // The content is the 320 x 200 map screen at the zoom, its pivot on the character.
        mOContent.pivot = new Vector2(pOPlayer.x / MapWidth, 1f - (pOPlayer.y / MapHeight));
        mOContent.anchoredPosition = pOCentre;
        mOContent.sizeDelta = new Vector2(MapWidth * liZoom, MapHeight * liZoom);
        mOContent.localRotation = Quaternion.Euler(0f, 0f, pfTurn);

        // The map at the original's pixel proportion (UWModernHudArt.PixelAspectX): narrowed in its
        // own space, so it turns with it; the pivot on the character keeps it in the middle.
        mOContent.localScale = new Vector3(UWModernHudArt.PixelAspectX, 1f, 1f);

        UWUiKit.SetRect(mOParchment.rectTransform, 0f, 0f, MapWidth * liZoom, MapHeight * liZoom);

        Vector2 lOTiles = UWAutomapPictures.TilesTopLeft;
        float lfTiles = UWAutomapPictures.TilesSize;

        mOTiles.enabled = mOTiles.texture != null;
        UWUiKit.SetRect(mOTiles.rectTransform, lOTiles.x * liZoom, (MapHeight - lOTiles.y - lfTiles) * liZoom, lfTiles * liZoom, lfTiles * liZoom);

        List<UWAutomapPictures.Note> lONotes = pOMap.GetNotes();

        while (mONotes.Count < lONotes.Count)
        {
            RawImage lONote = UWUiKit.CreateRawImage(mOContent, "Note");

            UWPixelArtUI.Apply(lONote);
            mONotes.Add(lONote);
        }

        for (int liNote = 0; liNote < mONotes.Count; liNote++)
        {
            RawImage lOImage = mONotes[liNote];

            if (liNote >= lONotes.Count || lONotes[liNote].Texture == null)
            {
                lOImage.enabled = false;
                continue;
            }

            UWAutomapPictures.Note lONote = lONotes[liNote];

            lOImage.texture = lONote.Texture;
            lOImage.enabled = true;
            UWUiKit.SetRect(lOImage.rectTransform, lONote.TopLeft.x * liZoom,
                (MapHeight - lONote.TopLeft.y - lONote.Texture.height) * liZoom,
                lONote.Texture.width * liZoom, lONote.Texture.height * liZoom);
        }
    }

    private void fEnsureArt()
    {
        UWArtStamp lOStamp = UWVersionedTexture.Backs();

        if (mOFrameArt.IsCurrent(lOStamp))
            return;

        mOFrame.texture = mOFrameArt.Set(lOStamp,
            UWModernHudArt.BuildPartLeather(UWModernLayout.ElementEnum.Minimap, mOUi.mOUWData.Textures, Size, Size, mOUi.TextureFilterMode));

        if (mOArrowTexture == null)
            mOArrowTexture = fBuildArrow(64);

        if (mODiscTexture == null)
            mODiscTexture = fBuildDisc(64);

        mOArrow.texture = mOArrowTexture;

        foreach (RawImage lOButton in mOButtons)
            lOButton.texture = mODiscTexture;
    }

    /// <summary>The character's arrow, pointing up: a light head with a dark rim.</summary>
    private static Texture2D fBuildArrow(int piSize)
    {
        Color32[] lyPixels = new Color32[piSize * piSize];
        Vector2 lOTip = new Vector2(0.5f, 0.95f);
        Vector2 lOLeft = new Vector2(0.14f, 0.08f);
        Vector2 lONotch = new Vector2(0.5f, 0.3f);
        Vector2 lORight = new Vector2(0.86f, 0.08f);
        Color32 lOFill = new Color32(250, 236, 190, 255);
        Color32 lORim = new Color32(40, 22, 10, 255);

        for (int y = 0; y < piSize; y++)
        {
            for (int x = 0; x < piSize; x++)
            {
                Vector2 lOAt = new Vector2((x + 0.5f) / piSize, (y + 0.5f) / piSize);
                bool lbInside = fInArrow(lOAt, lOTip, lOLeft, lONotch, lORight, 0f);
                bool lbRim = !lbInside && fInArrow(lOAt, lOTip, lOLeft, lONotch, lORight, 0.06f);

                lyPixels[(y * piSize) + x] = lbInside ? lOFill : lbRim ? lORim : new Color32(0, 0, 0, 0);
            }
        }

        return UWTextureFactory.FromColours(lyPixels, piSize, piSize, FilterMode.Bilinear, TextureWrapMode.Clamp, "UWModernMinimap arrow");
    }

    /// <summary>Inside the arrow (two triangles meeting at the notch), grown by pfGrow.</summary>
    private static bool fInArrow(Vector2 pOAt, Vector2 pOTip, Vector2 pOLeft, Vector2 pONotch, Vector2 pORight, float pfGrow)
    {
        return fInTriangle(pOAt, pOTip, pOLeft, pONotch, pfGrow) || fInTriangle(pOAt, pOTip, pONotch, pORight, pfGrow);
    }

    private static bool fInTriangle(Vector2 pOAt, Vector2 pOA, Vector2 pOB, Vector2 pOC, float pfGrow)
    {
        float lfAB = fEdge(pOAt, pOA, pOB);
        float lfBC = fEdge(pOAt, pOB, pOC);
        float lfCA = fEdge(pOAt, pOC, pOA);
        bool lbClockwise = fEdge(pOC, pOA, pOB) < 0f;

        if (lbClockwise)
        {
            lfAB = -lfAB;
            lfBC = -lfBC;
            lfCA = -lfCA;
        }

        return lfAB >= -pfGrow && lfBC >= -pfGrow && lfCA >= -pfGrow;
    }

    /// <summary>The signed distance of a point from the line through two points.</summary>
    private static float fEdge(Vector2 pOAt, Vector2 pOA, Vector2 pOB)
    {
        Vector2 lODirection = (pOB - pOA).normalized;

        return (lODirection.x * (pOAt.y - pOA.y)) - (lODirection.y * (pOAt.x - pOA.x));
    }

    /// <summary>A round button: dark leather with a gold rim, as the bags' number badges.</summary>
    private static Texture2D fBuildDisc(int piSize)
    {
        Color32[] lyPixels = new Color32[piSize * piSize];
        float lfCentre = (piSize - 1) * 0.5f;
        float lfRadius = (piSize * 0.5f) - 3f;
        float lfRim = piSize * 0.055f;

        for (int y = 0; y < piSize; y++)
        {
            for (int x = 0; x < piSize; x++)
            {
                float lfDistance = Mathf.Sqrt(((x - lfCentre) * (x - lfCentre)) + ((y - lfCentre) * (y - lfCentre)));
                Color lOColour = Color.Lerp(UWModernPalette.BadgeFill, UWModernPalette.Gold, Mathf.Clamp01(lfDistance - (lfRadius - lfRim) + 0.5f));

                lOColour.a = 1f - Mathf.Clamp01(lfDistance - lfRadius + 0.5f);
                lyPixels[(y * piSize) + x] = lOColour;
            }
        }

        return UWTextureFactory.FromColours(lyPixels, piSize, piSize, FilterMode.Bilinear, TextureWrapMode.Clamp, "UWModernMinimap button");
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Modern minimap", UWUiSorting.ModernMinimap);

        RectTransform lORootRect = (RectTransform)mOCanvas.transform;

        mOFrame = UWUiKit.CreateRawImage(lORootRect, "Leather", true);
        UWPixelArtUI.Apply(mOFrame);

        mOViewport = UWUiKit.CreateClipArea(lORootRect, "Map window");

        // Only anchored here: its pivot follows the player's place, about which the map turns.
        GameObject lOContent = new GameObject("Map", typeof(RectTransform));
        lOContent.transform.SetParent(mOViewport, false);
        mOContent = (RectTransform)lOContent.transform;
        mOContent.anchorMin = Vector2.zero;
        mOContent.anchorMax = Vector2.zero;

        mOParchment = UWUiKit.CreateRawImage(mOContent, "Parchment");
        mOTiles = UWUiKit.CreateRawImage(mOContent, "Tiles");

        // Hard texels also when the map turns (UWPixelArtUI).
        UWPixelArtUI.Apply(mOParchment);
        UWPixelArtUI.Apply(mOTiles);

        mOArrow = UWUiKit.CreateRawImage(mOViewport, "Character");

        mONorth = UWUiKit.CreateText(mOViewport, "North", TextAnchor.MiddleCenter, UWModernPalette.Text, mOUi);
        mONorth.text = "N";

        mOLevel = UWUiKit.CreateText(mOViewport, "Level", TextAnchor.LowerRight, UWModernPalette.Text, mOUi);

        mONoMap = UWUiKit.CreateText(mOViewport, "No map", TextAnchor.MiddleCenter, UWModernPalette.Text, mOUi);
        mONoMap.text = UWText.T("hud.minimap.no_map");

        mOButtons = new RawImage[2];
        mOButtonTexts = new Text[2];

        string[] lsButtons = { "+", "−" };

        for (int liButton = 0; liButton < 2; liButton++)
        {
            mOButtons[liButton] = UWUiKit.CreateRawImage(mOViewport, "Zoom " + lsButtons[liButton]);
            mOButtonTexts[liButton] = UWUiKit.CreateText(mOViewport, "Zoom text", TextAnchor.MiddleCenter, UWModernPalette.Text, mOUi);
            mOButtonTexts[liButton].text = lsButtons[liButton];
            // The sign in the disc's middle by its ink, whatever the font (UWCentredGlyph).
            mOButtonTexts[liButton].gameObject.AddComponent<UWCentredGlyph>();
        }
    }
}
