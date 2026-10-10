using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// CLASSIC WIDE (per user, 2026-10-10; the name theirs): the original's frame (MAIN.BYT) over the
/// whole screen in the modern scheme - pulled to the screen's width at fixed cuts, the columns put
/// in grown pixel by pixel (UWHudArt.BuildClassicWideFrame) -, the world in its hole, and the
/// pieces freed for Classic+ at the frame's own places: the compass on its base, the power gem on
/// its stand, the flasks on the right ledge, the rune hollow right of the compass, the active
/// spell icons on the left stone by the dragon (per user: as in the original), the message scroll
/// along the bottom, the original's panel at the right (the character page; the stats page on the
/// chain's pull between the flasks, as in the original; the rune tablet on the runes key), the
/// conversation in the original's look over the view, and the dragons on the bars (UWHudDragons).
/// The left command column stays as decoration (per user; only the combat mode is to work -
/// open). The modern parts - the heading strip, the minimap, the action bar, the bags' windows,
/// the modern panels, the backs - stay away.
///
/// GEOMETRY: a frame row is RowScale screen pixels (the screen's height over 200), a column
/// RowScale times the original's pixel proportion (UWModernHudArt.PixelAspectX); the columns put in
/// (Extra) fill the width. The elements take this as their size (UWModernLayout.Scale) and their
/// places from TryGetPlace (UWModernLayout.Place), in the ORIGINAL'S coordinates shifted by their
/// band's cuts (UWHudArt.ClassicWideShiftX), so they lie pixel for pixel on the frame. The camera
/// renders into the classic scheme's area (UWGameUI: x 50 to 226, rows 17 to 138), widened, and
/// the frame covers all of it but the hole.
/// </summary>
public class UWModernClassicFrame : MonoBehaviour
{
    public static UWModernClassicFrame Instance { get; private set; }

    /// <summary>The page in the frame's panel: the character page; the stats on the chain's pull;
    /// the rune tablet on the runes key (and back).</summary>
    public enum PageEnum
    {
        Inventory = 0,
        Stats = 1,
        Runes = 2
    }

    public static PageEnum Page { get; set; }

    /// <summary>The panel turns (eight steps, UWHudPanel's): the page shown is squeezed across
    /// (ApplySquash), the frame draws the black behind it, the edge and the chain.</summary>
    public static bool IsTurning => Instance != null && Instance.miStep >= 0;

    /// <summary>
    /// Squeezes a page's parts across about the panel's middle while the panel turns, as the
    /// classic scheme's pivot does - the page's content turns with it (per user's screenshot,
    /// 2026-10-10: the turning page was empty). The group's parts lie from its bottom left corner
    /// at the screen's; outside a turn the group stays as it is.
    /// </summary>
    public static void ApplySquash(RectTransform pOContent)
    {
        if (pOContent == null)
            return;

        float lfScale = IsActive && Instance != null && Instance.miStep >= 0 ? Instance.mfSquash : 1f;
        float lfCentre = (PanelX + Extra + (PanelWidth * 0.5f)) * ColumnScale;

        pOContent.localScale = new Vector3(lfScale, 1f, 1f);
        pOContent.anchoredPosition = new Vector2(lfCentre * (1f - lfScale), 0f);
    }

    /// <summary>Turns the frame's panel to a page (the rune bag: the tablet).</summary>
    public static void TurnTo(PageEnum pePage)
    {
        if (Instance != null && IsActive)
            Instance.fTurnTo(pePage);
    }

    private float mfSquash = 1f;

    /// <summary>The row the weapon's offsets count from (UWGameUI: wTopOffset).</summary>
    public const int WeaponRow = 131;

    /// <summary>Under the frame's picture: the weapon goes here, so the frame covers it below the hole.</summary>
    public Transform WeaponLayer { get; private set; }

    public static bool IsActive => UWModernLayout.Preset == UWModernLayout.PresetEnum.Classic;

    private const int FrameWidth = 320;

    private const int FrameHeight = 200;

    /// <summary>Screen pixels per frame row, and per frame column (the original's proportion).</summary>
    public static float RowScale => Screen.height / (float)FrameHeight;

    public static float ColumnScale => RowScale * UWModernHudArt.PixelAspectX;

    /// <summary>The columns put in to fill the screen's width, and how many of them left of the
    /// middle (UWHudArt.BuildClassicWideFrame takes half).</summary>
    public static int Extra => Mathf.Max(0, Mathf.RoundToInt(Screen.width / ColumnScale) - FrameWidth);

    public static int Left => Extra / 2;

    /// <summary>The camera's area, the classic scheme's widened (UWGameUI.fApplyGameCamera).</summary>
    public static Rect CameraRect => fSpan(50, 17, 226, 138);

    /// <summary>The view's hole in the frame: what the player sees of the world.</summary>
    public static Rect Hole => fSpan(UWHudArt.ClassicWideHole.X, UWHudArt.ClassicWideHole.Y,
        UWHudArt.ClassicWideHole.X + UWHudArt.ClassicWideHole.Width, UWHudArt.ClassicWideHole.Y + UWHudArt.ClassicWideHole.Height);

    /// <summary>Where the locked pointer aims: the camera area's middle - the screen's without the frame.</summary>
    public static Vector2 ViewCentre => IsActive ? CameraRect.center : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

    /// <summary>The panel (PANELS.GR 0 to 2, 83 x 114) and its edge picture (3), the chain's
    /// pictures (CHAINS.GR 0 to 7 between the flasks, 8 to 15 the short one above the panel) - as
    /// UWHudPanel.</summary>
    private const int PanelX = 236;

    private const int PanelY = 7;

    private const int PanelWidth = 83;

    private const int PanelHeight = 114;

    private const int ChainX = 272;

    private const int ChainY = 121;

    private const int ShortChainY = 3;

    private const int ChainFrames = 8;

    private const int ShortChainFirst = 8;

    private const int TurnSteps = 8;

    /// <summary>How far the active spells' row moves right on the widened left stone, at most.</summary>
    private const int SpellsRight = 24;

    /// <summary>The gargoyle's eyes (EYES.GR, 20 x 3) on its head in MAIN.BYT (UWHudArt: the head at
    /// 126/0, the eyes two columns in and three rows down).</summary>
    private const int EyesX = 126 + UWHudArt.EyesLeftInBrand;

    private const int EyesY = UWHudArt.EyesTopInBrand;

    /// <summary>The flask pictures' rows (UWFlasks, 24 x 33), under the panel at row 125 (UWGameUI).</summary>
    private const int FlaskTop = 125;

    private const int FlaskRows = 33;

    private UWGameUI mOUi;

    private Canvas mOCanvas;

    private RawImage mOImage;

    private RectTransform mODragonSpace;

    private UWHudDragons mODragons;

    private Texture2D mOTexture;

    private UWHudArt.ClassicWideFrame mOFrame;

    private int miExtra = -1;

    private int miArtVersion = -1;

    // The panel's turn: the step (-1 at rest), when the next comes, the page turned to.
    private int miStep = -1;

    private float mfStepAt;

    private PageEnum meTarget;

    private RawImage mOPanelBlack;

    private RawImage mOPanelPage;

    private RawImage mOPanelEdge;

    private RawImage mOChainBlack;

    private RawImage mOChain;

    private RawImage mOShortChainBlack;

    private RawImage mOShortChain;

    private Texture2D[] mOPages;

    private Texture2D mOEdgeTexture;

    private Texture2D[] mOChainTextures;

    private Texture2D[] mOShortChainTextures;

    /// <summary>The widened frame's picture (the scroll builds its paper from its bottom rows).</summary>
    public UWPicture Picture => mOFrame != null ? mOFrame.Picture : null;

    public void Init(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ------------------------------------------------- Places

    /// <summary>An original column's left edge on the screen, shifted by its band's cuts.</summary>
    private static float fX(int piX, int piY)
    {
        return (piX + UWHudArt.ClassicWideShiftX(piX, piY, Extra)) * ColumnScale;
    }

    /// <summary>The screen y (from the bottom) of an original row's top edge.</summary>
    private static float fTop(int piRow)
    {
        return Screen.height - (piRow * RowScale);
    }

    /// <summary>From the original's column piX0 to the column before piX1, rows piY0 to the row
    /// before piY1, both ends shifted by their band.</summary>
    private static Rect fSpan(int piX0, int piY0, int piX1, int piY1)
    {
        float lfLeft = fX(piX0, piY0);
        float lfRight = (piX1 + UWHudArt.ClassicWideShiftX(piX1, piY0, Extra)) * ColumnScale;

        return new Rect(lfLeft, fTop(piY1), lfRight - lfLeft, (piY1 - piY0) * RowScale);
    }

    /// <summary>A part's rect of its default size with its top left at an original pixel.</summary>
    private static Rect fAt(int piX, int piTop, Rect pODefault)
    {
        return new Rect(fX(piX, piTop), fTop(piTop) - pODefault.height, pODefault.width, pODefault.height);
    }

    /// <summary>
    /// Where a part goes in the frame (UWModernLayout.Place), its size kept: the pieces at the
    /// places they were cut from, the panel's pages at the panel, the active spells' row ending at
    /// x 100 on the left stone, row 138 (the original's first slot at x 84 to 99), the message
    /// scroll along the bottom, the conversation block over the view as CONV.BYT lies (x 43, the
    /// view's middle). The parts not named stay away in this preset (UWModernLayout).
    /// </summary>
    public static bool TryGetPlace(UWModernLayout.ElementEnum peElement, Rect pODefault, out Rect pOPlace)
    {
        pOPlace = pODefault;

        if (!IsActive)
            return false;

        switch (peElement)
        {
            case UWModernLayout.ElementEnum.Compass:
            {
                // The original's disc and needle pictures' box (UWHudCompass.BuildComposite).
                Vector2Int lOCorner = UWHudCompass.CompositeCorner();

                pOPlace = fAt(lOCorner.x, lOCorner.y, pODefault);
                return true;
            }

            case UWModernLayout.ElementEnum.PowerGem:
                pOPlace = fAt(UWHudArt.GemStandX, UWHudArt.GemStandY, pODefault);
                return true;

            case UWModernLayout.ElementEnum.HealthFlask:
            case UWModernLayout.ElementEnum.ManaFlask:
            {
                // The part's rect stands on the flask's bottom (its value above it, hidden here).
                int liX = peElement == UWModernLayout.ElementEnum.HealthFlask ? 248 : 284;

                pOPlace = new Rect(fX(liX, FlaskTop), fTop(FlaskTop + FlaskRows), pODefault.width, pODefault.height);
                return true;
            }

            case UWModernLayout.ElementEnum.RuneHollow:
            {
                // The outline's pixel off the hollow's own place, not off its column: the cut at 173
                // lies right before the hollow, and column 172 would move only half as far (per
                // user's screenshot, 2026-10-10: the hollow stood left of the frame's).
                int liPad = UWUserSettings.ModernRuneHollowOutline ? 1 : 0;
                Rect lOAt = fAt(UWHudArt.RuneHollowX, UWHudArt.RuneHollowY - liPad, pODefault);

                pOPlace = new Rect(lOAt.x - (liPad * ColumnScale), lOAt.y, lOAt.width, lOAt.height);
                return true;
            }

            case UWModernLayout.ElementEnum.Spells:
            {
                // Up to SpellsRight columns further right than the original's x 100 as the stone
                // widens, so three icons clear the left dragon's head (x 36 to 72; per user's
                // screenshot, 2026-10-10: "the active spells a little further right").
                float lfRight = (100 + Mathf.Min(Left, SpellsRight)) * ColumnScale;

                pOPlace = new Rect(lfRight - pODefault.width, fTop(138) - pODefault.height, pODefault.width, pODefault.height);
                return true;
            }

            case UWModernLayout.ElementEnum.Messages:
                pOPlace = new Rect(0f, fTop(FrameHeight), pODefault.width, pODefault.height);
                return true;

            case UWModernLayout.ElementEnum.StatsPanel:
            case UWModernLayout.ElementEnum.RuneTablet:
            case UWModernLayout.ElementEnum.CharacterPage:
                pOPlace = fAt(236, 7, pODefault);
                return true;

            case UWModernLayout.ElementEnum.Conversation:
                pOPlace = new Rect((43 + Left) * ColumnScale, Screen.height - pODefault.height, pODefault.width, pODefault.height);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Whether a screen point lies on the frame, not in its hole: no click goes into the
    /// world there (UWModernPointer.IsOverUi).</summary>
    public bool IsOverFrame(Vector2 pOPointer)
    {
        return IsActive && mOCanvas != null && mOCanvas.enabled && !Hole.Contains(pOPointer);
    }

    // ------------------------------------------------- Drawing

    /// <summary>Once a frame from UWModernHud: shown with the HUD and in a conversation (the
    /// original keeps its frame there).</summary>
    public void Tick(bool pbShown)
    {
        if (!pbShown || !IsActive || mOUi == null || mOUi.mOUWData == null)
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            return;
        }

        if (mOCanvas == null)
            fBuild();

        fEnsureTexture();

        if (mOTexture == null)
        {
            mOCanvas.enabled = false;
            return;
        }

        mOCanvas.enabled = true;

        RectTransform lORect = mOImage.rectTransform;

        lORect.anchoredPosition = Vector2.zero;
        lORect.sizeDelta = new Vector2((FrameWidth + Extra) * ColumnScale, FrameHeight * RowScale);

        // The dragons in the frame's own pixels: their pictures sized in them, the space scaled.
        mODragonSpace.localScale = new Vector3(ColumnScale, RowScale, 1f);
        mODragonSpace.anchoredPosition = Vector2.zero;

        if (mODragons != null && !mOUi.IsMapVisible && !UWConversationScreen.IsAnyOpen)
            mODragons.Tick();

        fUpdateInput();
        fUpdateTurn();
        fUpdateFightIcon();
    }

    /// <summary>The fight icon lit (LFTI.GR 9 at 8/80, UWHudCommands) while the weapon is drawn,
    /// however it was drawn; the idle one is the frame's own.</summary>
    private void fUpdateFightIcon()
    {
        bool lbLit = mOUi.mOInteraction != null && mOUi.mOInteraction.IsCombatModeActive;

        if (lbLit && mOFightLit == null)
            mOFightLit = fPicture(UWTexture.TextureTypes.LFTI, FightLitArt);

        if (!lbLit || mOFightLit == null)
        {
            mOFightIcon.enabled = false;
            return;
        }

        fPlace(mOFightIcon, mOFightLit, FightX, FightY, mOFightLit.width, mOFightLit.height);
    }

    private const int FightLitArt = 9;

    private const int FightX = 8;

    private const int FightY = 80;

    private RawImage mOFightIcon;

    private Texture2D mOFightLit;

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Classic wide frame", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Under the HUD's parts (40) and the conversation (39).
        mOCanvas.sortingOrder = 38;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        // The weapon's layer first, so the frame lies over it.
        GameObject lOWeapon = new GameObject("Weapon layer", typeof(RectTransform));
        lOWeapon.transform.SetParent(lORoot.transform, false);

        RectTransform lOWeaponRect = (RectTransform)lOWeapon.transform;
        lOWeaponRect.anchorMin = Vector2.zero;
        lOWeaponRect.anchorMax = Vector2.one;
        lOWeaponRect.offsetMin = Vector2.zero;
        lOWeaponRect.offsetMax = Vector2.zero;
        WeaponLayer = lOWeaponRect;

        GameObject lOImage = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
        lOImage.transform.SetParent(lORoot.transform, false);

        RectTransform lORect = (RectTransform)lOImage.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        mOImage = lOImage.GetComponent<RawImage>();
        mOImage.raycastTarget = false;
        UWPixelArtUI.Apply(mOImage);

        GameObject lOSpace = new GameObject("Dragons", typeof(RectTransform));
        lOSpace.transform.SetParent(lORoot.transform, false);

        // The dragons over the parts (the rune hollow lies under the right one's head, per user,
        // 2026-10-10): a canvas of their own above the HUD's.
        Canvas lODragonCanvas = lOSpace.AddComponent<Canvas>();
        lODragonCanvas.overrideSorting = true;
        lODragonCanvas.sortingOrder = 41;

        mODragonSpace = (RectTransform)lOSpace.transform;
        mODragonSpace.anchorMin = new Vector2(0f, 1f);
        mODragonSpace.anchorMax = new Vector2(0f, 1f);
        mODragonSpace.pivot = new Vector2(0f, 1f);
        mODragonSpace.sizeDelta = Vector2.zero;

        // The turn's pictures in a space of their own, in the frame's pixels as the dragons'.
        GameObject lOTurn = new GameObject("Panel turn", typeof(RectTransform));
        lOTurn.transform.SetParent(lORoot.transform, false);

        mOTurnSpace = (RectTransform)lOTurn.transform;
        mOTurnSpace.anchorMin = new Vector2(0f, 1f);
        mOTurnSpace.anchorMax = new Vector2(0f, 1f);
        mOTurnSpace.pivot = new Vector2(0f, 1f);
        mOTurnSpace.sizeDelta = Vector2.zero;

        mOPanelBlack = fTurnImage("Panel black", new Vector2(0f, 1f), Color.black);
        mOPanelPage = fTurnImage("Panel page", new Vector2(0.5f, 1f), Color.white);
        mOPanelEdge = fTurnImage("Panel edge", new Vector2(0.5f, 1f), Color.white);
        mOChainBlack = fTurnImage("Chain black", new Vector2(0f, 1f), Color.black);
        mOChain = fTurnImage("Chain", new Vector2(0f, 1f), Color.white);
        mOShortChainBlack = fTurnImage("Short chain black", new Vector2(0f, 1f), Color.black);
        mOShortChain = fTurnImage("Short chain", new Vector2(0f, 1f), Color.white);
        mOFightIcon = fTurnImage("Fight icon", new Vector2(0f, 1f), Color.white);
    }

    private RectTransform mOTurnSpace;

    private RawImage fTurnImage(string psName, Vector2 pOPivot, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(mOTurnSpace, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = pOPivot;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.color = pOColour;
        lOImage.enabled = false;
        UWPixelArtUI.Apply(lOImage);

        return lOImage;
    }

    /// <summary>A picture of the game's files as a texture (rows bottom-up), or null.</summary>
    private Texture2D fPicture(UWTexture.TextureTypes peType, int piIndex)
    {
        UWTexture lOSource;

        try
        {
            lOSource = mOUi.mOUWData.Textures.GetTextureByType(peType, piIndex);
        }
        catch
        {
            return null;
        }

        if (lOSource == null || lOSource.Width <= 0 || lOSource.Height <= 0)
            return null;

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernClassicFrame " + peType + " " + piIndex;
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
        lOTexture.Apply(false, false);

        return lOTexture;
    }

    private void fEnsureTurnArt()
    {
        if (mOPages != null)
            return;

        mOPages = new[] { fPicture(UWTexture.TextureTypes.PANELS, 0), fPicture(UWTexture.TextureTypes.PANELS, 2), fPicture(UWTexture.TextureTypes.PANELS, 1) };
        mOEdgeTexture = fPicture(UWTexture.TextureTypes.PANELS, 3);
        mOChainTextures = new Texture2D[ChainFrames];
        mOShortChainTextures = new Texture2D[ChainFrames];

        for (int liFrame = 0; liFrame < ChainFrames; liFrame++)
        {
            mOChainTextures[liFrame] = fPicture(UWTexture.TextureTypes.CHAINS, liFrame);
            mOShortChainTextures[liFrame] = fPicture(UWTexture.TextureTypes.CHAINS, ShortChainFirst + liFrame);
        }
    }

    /// <summary>Starts the panel's turn to a page, as the original's chain does (UWHudPanel).</summary>
    private void fTurnTo(PageEnum pePage)
    {
        if (miStep >= 0 || pePage == Page)
            return;

        meTarget = pePage;
        miStep = 0;
        mfStepAt = Time.unscaledTime;
    }

    /// <summary>
    /// THE PANEL'S TURN (per user's screenshot, 2026-10-10: the chain's pull had no animation),
    /// as the classic scheme's (UWHudPanel.fUpdatePanelRotation): eight steps of
    /// UWGameUI.mfPanelStepSeconds, the page's width the cosine over 180 degrees, in the middle
    /// step the panel's edge picture and the page turned; the chain turns along in its own
    /// pictures. Under the turning page the panel's place is black (the frame has the inventory's
    /// circles baked in). The pages themselves hide meanwhile (IsTurning).
    /// </summary>
    private void fUpdateTurn()
    {
        mOTurnSpace.localScale = new Vector3(ColumnScale, RowScale, 1f);
        mOTurnSpace.anchoredPosition = Vector2.zero;

        if (miStep < 0)
        {
            mOPanelBlack.enabled = false;
            mOPanelPage.enabled = false;
            mOPanelEdge.enabled = false;
            mOChainBlack.enabled = false;
            mOChain.enabled = false;
            mOShortChainBlack.enabled = false;
            mOShortChain.enabled = false;
            return;
        }

        fEnsureTurnArt();

        if (Time.unscaledTime >= mfStepAt)
        {
            if (miStep >= TurnSteps)
            {
                miStep = -1;
                mfSquash = 1f;
                fUpdateTurn();
                return;
            }

            mfStepAt = Time.unscaledTime + Mathf.Max(0.01f, mOUi.mfPanelStepSeconds);

            if (miStep == TurnSteps / 2)
                Page = meTarget;

            float lfWidth = Mathf.Abs(Mathf.Cos(miStep / (float)TurnSteps * Mathf.PI));
            bool lbEdge = miStep == TurnSteps / 2;
            float lfX = PanelX + Extra;

            // The page itself is squeezed (ApplySquash); on its edge it is gone.
            mfSquash = lbEdge ? 0f : lfWidth;

            fPlace(mOPanelBlack, null, lfX, PanelY, PanelWidth, PanelHeight);
            mOPanelPage.enabled = false;

            fPlace(mOPanelEdge, mOEdgeTexture, lfX + (PanelWidth * 0.5f), PanelY, mOEdgeTexture != null ? mOEdgeTexture.width : 0f,
                mOEdgeTexture != null ? mOEdgeTexture.height : 0f);
            mOPanelEdge.enabled = lbEdge && mOEdgeTexture != null;

            fPlaceChain(mOChainBlack, mOChain, mOChainTextures[miStep], ChainX + Extra, ChainY);
            fPlaceChain(mOShortChainBlack, mOShortChain, mOShortChainTextures[miStep], ChainX + Extra, ShortChainY);

            miStep++;
        }
    }

    private static void fPlace(RawImage pOImage, Texture2D pOTexture, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        if (pOTexture != null)
            pOImage.texture = pOTexture;

        pOImage.enabled = true;

        RectTransform lORect = pOImage.rectTransform;

        lORect.anchoredPosition = new Vector2(pfX, -pfY);
        lORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    private static void fPlaceChain(RawImage pOBlack, RawImage pOChain, Texture2D pOTexture, float pfX, float pfY)
    {
        if (pOTexture == null)
        {
            pOBlack.enabled = false;
            pOChain.enabled = false;
            return;
        }

        fPlace(pOBlack, null, pfX, pfY, pOTexture.width, pOTexture.height);
        fPlace(pOChain, pOTexture, pfX, pfY, pOTexture.width, pOTexture.height);
    }

    /// <summary>The frame for the screen's width, built anew when the width or the colour help
    /// changes; the dragons with it (the right one moves with the right bar).</summary>
    private void fEnsureTexture()
    {
        int liExtra = Extra;

        if (mOTexture != null && miExtra == liExtra && miArtVersion == UWColourVision.Version)
            return;

        if (mOTexture != null)
            Destroy(mOTexture);

        mOTexture = UWModernHudArt.BuildClassicWideFrame(mOUi.mOUWData.Textures, liExtra, mOUi.TextureFilterMode, out mOFrame);
        mOImage.texture = mOTexture;
        miArtVersion = UWColourVision.Version;

        if (miExtra != liExtra || mODragons == null)
        {
            for (int liAt = mODragonSpace.childCount - 1; liAt >= 0; liAt--)
                Destroy(mODragonSpace.GetChild(liAt).gameObject);

            mODragons = new UWHudDragons(mODragonSpace, mOUi.fGetDragonSprite, liExtra);
        }

        miExtra = liExtra;
    }

    /// <summary>The runes key turns the panel to the tablet and back; a click with the pointer free
    /// on the flasks' area does what the original's does (UWClickRules.Flasks, FlaskAt): the
    /// vitality's or the mana's message, the chain's pull the turn to the stats - and from any
    /// other page back to the inventory (per user, 2026-10-10: the flasks' clicks did not work).</summary>
    private void fUpdateInput()
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;
        UWModernHud lOHud = UWModernHud.Instance;

        if (lOScheme == null || lOScheme.IsUIModalOpen || UWControls.IsTextEntryActive || lOHud == null || lOHud.IsOpen
            || UWConversationScreen.IsAnyOpen)
            return;

        UWControls lOControls = lOScheme.Controls;

        if (lOControls != null && lOControls.Player.ModernRunes.WasPressedThisFrame())
            fTurnTo(Page == PageEnum.Runes ? PageEnum.Inventory : PageEnum.Runes);

        if (!UWModernPointer.IsFree || Mouse.current == null || (!UWMouseButtons.LeftPressed && !UWMouseButtons.RightPressed))
            return;

        // The original's point: right of both cuts in these rows, so Extra columns off; rows from
        // the bottom, as the click table counts.
        Vector2 lOPointer = Mouse.current.position.ReadValue();
        int liX = Mathf.FloorToInt(lOPointer.x / ColumnScale) - Extra;
        int liY = Mathf.FloorToInt(lOPointer.y / RowScale);

        // THE LEFT COMMAND COLUMN (per user, 2026-10-10: decoration, only the fight works): its
        // area as the original's (UWClickRules.CommandIcons, CommandIconAt), left of every cut;
        // the fight icon draws or sheathes the weapon as the combat key does, with the left button.
        int liLeftX = Mathf.FloorToInt(lOPointer.x / ColumnScale);
        UWClickRules.Area lOIcons = UWClickRules.CommandIcons;

        if (lOIcons.Contains(liLeftX, liY))
        {
            if (UWMouseButtons.LeftPressed && UWClickRules.CommandIconAt(liY - lOIcons.Y0) == UWCommandMode.Fight
                && mOUi.mOInteraction != null && !UWRoamingSight.IsAnyRunning)
                mOUi.mOInteraction.ToggleCombatMode();

            return;
        }

        UWClickRules.Area lOArea = UWClickRules.Flasks;

        if (!lOArea.Contains(liX, liY))
            return;

        UWClickRules.FlaskPart lePart = UWClickRules.FlaskAt(liX - lOArea.X0, liY - lOArea.Y0);

        if (lePart == UWClickRules.FlaskPart.Chain)
            fTurnTo(Page == PageEnum.Inventory ? PageEnum.Stats : PageEnum.Inventory);
        else if (mOUi.Runes != null)
            mOUi.Runes.DescribeFlask(lePart);
    }

    /// <summary>The gargoyle's eyes' place on the screen (UWModernHud draws them there).</summary>
    public static Rect EyesRect(int piWidth, int piHeight)
    {
        return new Rect(fX(EyesX, EyesY), fTop(EyesY + piHeight), piWidth * ColumnScale, piHeight * RowScale);
    }
}
