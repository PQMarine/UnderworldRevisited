using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The classic original HUD: paper doll, flasks, power gem, weapon and the
/// pointer-driven movement cursor.
///
/// Runs on a UGUI canvas built at runtime (Image components under a
/// CanvasScaler with a fixed reference resolution of 320x200 - exactly the original resolution of
/// UW1) instead of OnGUI/IMGUI. Every original pixel coordinate is thereby taken over unchanged
/// as a RectTransform position, without the earlier manual
/// scale/liSizeToFill calculation - the CanvasScaler scales and centres automatically.
/// IMGUI, on the other hand, remained impractical for everything still to come (inventory drag&drop,
/// rune shelf): no editor visibility, no hover/drag support.
///
/// The pointer zone logic (fCursorMovement) independently stays in real
/// screen pixels, since it evaluates the real mouse position - see scale/gameArea*.
///
/// TAKEN APART on 2026-09-18 (7,633 lines were one class): this class keeps the layout, the
/// canvas build, Init/Start/Update, the HUD dragons, the cursors and the movement cursor, and
/// owns one object per section - UWHudInventory, UWHudMessageLog, UWHudPanel, UWHudRunes,
/// UWHudMap, UWHudPictures, UWHudCompass, UWHudOptions, UWHudConversation - each created on
/// first use through its accessor below. The sections reach the shared services through their
/// owner (mOUi: game data, frame, character, interaction, inventory, filter mode, the internal
/// helpers) and one another through the accessors; the Inspector fields of every section stay
/// on this MonoBehaviour so that scene values survive. The public entry points the rest of the
/// game calls stay here as forwards.
/// </summary>
public class UWGameUI : MonoBehaviour
{
    internal UWCharacter mCharacter;
    internal UWControlScheme mControlSchemeRef;
    private UWPlayerMovement mMovementRef;

    internal DataImport mOUWData;

    // The window picture in CS400.N01 is not drawn full screen, but sits
    // as a 172x112 block in the lower right corner of the 320x200 canvas (measured: x
    // 148..319, y 88..199). The hole in the frame MAIN.BYT, by contrast, lies at x 50..226,
    // y 17..138 (176x121, measured by flood fill, see gameAreaLeft), so it is 4 pixels
    // wider and 9 taller than the block. The block is therefore CENTRED IN THE HOLE instead of placed at its
    // corner (the first attempt used the corner, reported per user as "a bit too far left and
    // up" - exactly half the difference).
    internal const float windowHoleLeft = 50f;

    internal const float windowHoleTop = 17f;

    internal const float windowHoleWidth = 176f;

    internal const float windowHoleHeight = 121f;

    // Hole including both edge pixels: X 50..226, Y 17..138, so 177x122. For the
    // centring, the inclusive count is the right one - the painted area is
    // 172x112, so the difference comes out evenly at 2 pixels left/right and 5 top/bottom, which
    // matches the measurement in the original (user, 2026-08-29: "two to the left and 4-5
    // up"). With the exclusive count (176x121) the height would not work out.
    internal const float windowHoleWidthInclusive = 177f;

    private const float windowHoleHeightInclusive = 122f;

    /// <summary>Distance from the top edge of the hole to the painted area, measured in the original -
    /// not the centred half (that would be 5). For the 172x112 window picture the corner thus lands
    /// on (52, 19) before the fine nudge.</summary>
    internal const float windowContentInsetTop = 2f;

    internal Texture2D mOMainTexture;

    private bool lbIsInitialized;

    internal List<Texture2D> mOCursorTextures;

    public Texture2D UWBackground;

    public FilterMode TextureFilterMode = FilterMode.Point;

    [Tooltip("Original scheme: the camera renders only into the small 3D viewport (as in the " +
        "original, which likewise rendered only this window) instead of onto the full " +
        "screen with the UI simply on top - otherwise the window shows only a " +
        "narrow central cutout of the actual camera FOV and you do not see e.g. near " +
        "floor objects that would be visible in the original. This value is the HORIZONTAL " +
        "FOV of the original viewport (Unity's Camera.fieldOfView is always vertical - see " +
        "fApplyGameCamera for the conversion) - adjustable directly here in the Inspector in play mode " +
        "by comparison with a real DOSBox screenshot.")]
    [SerializeField]
    private float mfOriginalSchemeFov = 75f;

    private Camera mOCamera;
    private float mfDefaultFov;

    public UWCursors.CursorEnum CursorMovement { get; set; }
    public Vector3 LastCursorPositionInGameArea { get; set; }

    /// <summary>0..1 speed fraction for Forward/Backward/SlightLeft/SlightRight, analogue from the
    /// pointer distance to the respective zone border - see fCursorMovement().</summary>
    public float CursorWalkFactor { get; set; }

    /// <summary>0..1 turn rate fraction for TurnLeft/TurnRight/SlightLeft/SlightRight, analogue from
    /// the pointer distance to the screen centre - see fCursorMovement().</summary>
    public float CursorRotateFactor { get; set; }

    /// <summary>The pointer in the view's movement area in the original's pixels, x from the
    /// left, y from the BOTTOM (0 to UWPlayerMotion.PointerAreaWidth/Height - 1), as
    /// seg034_2F89_4E reads it - the motion core's pointer scheme takes these, see
    /// UWPlayerMotion.PointerCommand. Kept while the pointer is outside the area.</summary>
    public int CursorAreaX { get; private set; }

    public int CursorAreaY { get; private set; }

    // Original pixel coordinates (reference resolution 320x200) for the canvas elements -
    // unscaled, the CanvasScaler takes care of that.
    private const float wLeftOffset = 52f;
    private const float wTopOffset = 131f;
    private const float charLeftOffset = 260f;
    private const float charTopOffset = 11f;
    private const float helmetArmorLeftOffset = 267f;
    // One pixel higher than the 11 it had since the baseline: the headgear sat one pixel too
    // low against the original (per user, 2026-09-23, measured on an enlarged screenshot).
    private const float helmetArmorTopOffset = 10f;
    private const float glovesArmorLeftOffset = 261f;
    private const float glovesArmorTopOffset = 42f;
    private const float chestArmorLeftOffset = 262f;
    private const float chestArmorTopOffset = 22f;
    private const float legsArmorLeftOffset = 268f;
    private const float legsArmorTopOffset = 24f;
    private const float bootsArmorLeftOffset = 266f;
    private const float bootsArmorTopOffset = 66f;
    private const float powerGemLeft = 4f;
    private const float powerGemTop = 139f;
    private const float healthFlaskLeft = 248f;
    private const float manaFlaskLeft = 284f;
    private const float flaskTop = 125f;

    // Pointer zones, by contrast, work in real screen pixels, since they evaluate the real
    // mouse position (WarpCursorPosition, zone classification).
    internal float scale
    {
        // Fitted whole into the window (UWUiFit): by the height, or by the width when narrow.
        get { return UWUiFit.Scale; }
    }

    /// <summary>
    /// Horizontal size of one original pixel relative to its height. 1 shows square pixels;
    /// with UWSettings.DisplayAs4By3 it is 5/6, so the 320x200 picture appears at 4:3 like on
    /// the CRT the game was made for and like DOSBox shows it - for comparing our screenshots
    /// with DOSBox ones (per user, 2026-09-17). The whole GameFrame is squeezed sideways and
    /// every conversion between original pixels and screen pixels takes the factor on X.
    /// </summary>
    public static float HorizontalPixelFactor
    {
        get
        {
            // Blended to 4:3 while the help window is open (UWHelpLayout).
            return UWHelpLayout.PixelFactor(UWDisplayAspect.Enabled ? VgaPixelAspectCorrection : 1f);
        }
    }

    internal float scaleX
    {
        get { return scale * HorizontalPixelFactor; }
    }

    /// <summary>
    /// GameFrame sits centred in the canvas (anchorMin/Max 0.5/0.5, fixed 320x200 size -
    /// see fBuildCanvas) - on a screen wider than the 320:200 reference
    /// aspect ratio this creates a margin left/right (BackdropLeft/Right,
    /// see Update()). Without this margin, gameAreaLeft/Right (and with them camera rect,
    /// pointer zones, world raycast gating) would on such a screen be shifted to the left relative to the
    /// actually visible viewport.
    /// </summary>
    private float fGetLetterboxMarginRefUnits()
    {
        if (mCanvasRoot == null || mGameFrame == null)
            return 0f;

        // From the window itself, not from the canvas rect: that one follows the scaler, which may
        // run after this in the frame - while a window is dragged the bars lagged a frame behind.
        float lfCanvasWidth = UWUiFit.CanvasWidth;

        // The frame slides left while the help window is open (UWHelpLayout).
        return Mathf.Max(0f, ((lfCanvasWidth - (mGameFrame.sizeDelta.x * HorizontalPixelFactor)) / 2f)
            + UWHelpLayout.FrameShift(lfCanvasWidth));
    }

    internal float fGetLetterboxMarginPixels()
    {
        return fGetLetterboxMarginRefUnits() * scale;
    }

    /// <summary>
    /// A screen point in the original's own terms, as its click areas are registered (see
    /// UWClickRules): x the column from the left, y COUNTED FROM THE BOTTOM of the 200 line
    /// screen, 0 to 199, so that the row r from the top is y = 199 - r (the original clamps y at
    /// 199 and its "up" pointer key puts it there; until 2026-09-26 we read 200 - r). False
    /// outside the 320x200 frame.
    /// </summary>
    internal bool TryGetOriginalPoint(Vector2 pOScreen, out int piX, out int piY)
    {
        float lfX = (pOScreen.x - fGetLetterboxMarginPixels()) / scaleX;
        float lfY = (pOScreen.y - UWUiFit.BottomMarginPixels) / scale;

        piX = Mathf.FloorToInt(lfX);
        piY = Mathf.FloorToInt(lfY);

        return lfX >= 0f && lfX < UWUiFit.FrameWidthUnits && lfY >= 0f && lfY < UWUiFit.FrameHeight;
    }

    /// <summary>Is the pointer inside an area of the original's table - both ends included, as
    /// the dispatcher tests (UWClickRules)? The position relative to the corner comes back,
    /// as the original hands it to the area's handler.</summary>
    internal bool IsPointerInOriginalArea(Vector2 pOScreen, UWClickRules.Area pOArea, out int piX, out int piY)
    {
        piX = 0;
        piY = 0;

        if (!TryGetOriginalPoint(pOScreen, out int liX, out int liY) || !pOArea.Contains(liX, liY))
            return false;

        piX = liX - pOArea.X0;
        piY = liY - pOArea.Y0;

        return true;
    }

    /// <summary>
    /// Screen rectangle (pixels, origin bottom left) of a picture of the given size in original
    /// pixels, placed in the viewport exactly like the window and gravestone pictures:
    /// horizontally centred in the hole, windowContentInsetTop below its top edge, plus the
    /// fine nudge. Used by UWIntroPlayer for the viewport cutscenes.
    /// </summary>
    public Rect GetViewportPictureScreenRect(Vector2 pOSize)
    {
        return GetUiPictureScreenRect(GetViewportPictureCorner(pOSize), pOSize);
    }

    /// <summary>Top-left corner (original UI pixels, top down) of a picture of the given size
    /// placed like the window pictures - see GetViewportPictureScreenRect.</summary>
    public Vector2 GetViewportPictureCorner(Vector2 pOSize)
    {
        return new Vector2(
            windowHoleLeft + Mathf.Floor((windowHoleWidthInclusive - pOSize.x) * 0.5f) + mOWindowPictureNudge.x,
            windowHoleTop + windowContentInsetTop + mOWindowPictureNudge.y);
    }

    /// <summary>Screen rectangle (pixels, origin bottom left) of a picture whose top-left
    /// corner lies at the given original UI position (top down).</summary>
    public Rect GetUiPictureScreenRect(Vector2 pOCorner, Vector2 pOSize)
    {
        return new Rect(fGetLetterboxMarginPixels() + (pOCorner.x * scaleX),
            UWUiFit.BottomMarginPixels + ((200f - pOCorner.y - pOSize.y) * scale),
            pOSize.x * scaleX, pOSize.y * scale);
    }

    // Readjusted to 53/226/182/69 over several rounds of DOSBox screenshot comparison -
    // the height (113) stayed noticeably smaller than the actual hole, no
    // FOV combination could compensate for that, as it was caused by the aspect ratio (see
    // fApplyGameCamera). Instead measured pixel-exactly by flood fill directly from the transparent area in
    // Assets/UWExportedTextures/Main_0.png (320x200, alpha=0 in the hole):
    // hole = X 50..226, Y (image top-down) 17..138 -> 176x121, not 173x113. In
    // bottom-up screen coordinates (200 minus image Y) this gives 183/62.
    private float gameAreaLeft
    {
        get { return fGetLetterboxMarginPixels() + (50f * scaleX); }
    }

    private float gameAreaRight
    {
        get { return fGetLetterboxMarginPixels() + (226f * scaleX); }
    }

    private float gameAreaTop
    {
        get { return UWUiFit.BottomMarginPixels + (183f * scale); }
    }

    private float gameAreaBottom
    {
        get { return UWUiFit.BottomMarginPixels + (62f * scale); }
    }

    /// <summary>Pixel aspect ratio 5:6 of the 320x200 VGA mode on a 4:3 monitor - see
    /// fApplyGameCamera.</summary>
    public const float VgaPixelAspectCorrection = 5f / 6f;

    /// <summary>
    /// Original scheme: the camera renders only into the small 3D viewport
    /// (Camera.rect, normalised to Screen.width/height) instead of onto the full screen with
    /// the UI simply on top - exactly as in the original, which likewise rendered only this window.
    /// A simple "full screen + UI laid over it" shows in the hole only
    /// the narrow central cutout of the full camera FOV, so near floor objects
    /// (e.g. a skull directly in front of the feet) stay invisible, although in the original they
    /// were visible at the same position - CONFIRMED per user comparison with a real
    /// DOSBox screenshot. Modern: full area, original (Inspector) FOV.
    ///
    /// mfOriginalSchemeFov is meant as a HORIZONTAL FOV (the narrow viewport is
    /// wider than tall, and the original perspective is defined via the window width)
    /// - but in Unity Camera.fieldOfView is always VERTICAL, the horizontal one
    /// otherwise results automatically from vertical FOV+aspect. A value set directly as vertical FOV
    /// therefore made everything too wide (e.g. a wall plaque) - here instead
    /// the vertical FOV matching the desired horizontal FOV is computed back.
    ///
    /// What counts here is NOT the real (square) pixel aspect ratio of the 176x121 hole,
    /// but one emulated via VgaPixelAspectCorrection: classic 320x200 VGA mode
    /// had, on the 4:3 CRT monitor the original was designed for, NO
    /// square pixels (320x200 raw = 8:5 = 1.6, but displayed at 4:3 = 1.333 -> each
    /// pixel row effectively 20% taller than wide, pixel aspect ratio 5:6) - CONFIRMED per
    /// GOG forum discussion (see chat answer). The original rendering (and the
    /// uncorrected DOSBox screenshot it is compared against) is therefore itself slightly
    /// squashed vertically - no combination of FOV/camera height could fix that, because the
    /// aspect ratio problem was a different source of error than position/view angle.
    /// Camera.aspect is therefore deliberately overwritten with the corrected value (only for the
    /// projection - Camera.rect stays exactly the real pixel hole), Modern has to lift the override
    /// again via ResetAspect().
    /// </summary>
    private void fApplyGameCamera(bool pbOriginalScheme)
    {
        if (mOCamera == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        if (!pbOriginalScheme)
        {
            mOCamera.rect = new Rect(0f, 0f, 1f, 1f);
            mOCamera.ResetAspect();
            mOCamera.fieldOfView = mfDefaultFov;
            return;
        }

        mOCamera.rect = new Rect(
            gameAreaLeft / Screen.width,
            gameAreaBottom / Screen.height,
            (gameAreaRight - gameAreaLeft) / Screen.width,
            (gameAreaTop - gameAreaBottom) / Screen.height);

        // ResetAspect() BEFORE reading: a manual aspect override (see below) otherwise
        // persists across frames and from the second frame on Camera.aspect would
        // already return the corrected value from the last pass instead of the
        // real raw aspect ratio computed from Camera.rect - the correction
        // would otherwise add up frame by frame.
        // With DisplayAs4By3 the hole itself is already shown at the CRT proportion, so its raw
        // aspect is the corrected one and must not be corrected a second time.
        mOCamera.ResetAspect();
        // General form for every factor in between, as the help window blends it: 1 corrects by
        // 5/6, 5/6 by nothing.
        float lfCorrectedAspect = mOCamera.aspect * VgaPixelAspectCorrection / HorizontalPixelFactor;
        mOCamera.aspect = lfCorrectedAspect;

        float lfHorizontalFovRad = mfOriginalSchemeFov * Mathf.Deg2Rad;
        float lfVerticalFovRad = 2f * Mathf.Atan(Mathf.Tan(lfHorizontalFovRad / 2f) / lfCorrectedAspect);
        mOCamera.fieldOfView = lfVerticalFovRad * Mathf.Rad2Deg;
    }

    /// <summary>Is a projectile spell currently waiting for its target? As long as it is, the left
    /// button does not drive movement but fires the shot.</summary>
    public bool IsSpellTargeting
    {
        get { return mOInteraction != null && mOInteraction.IsSpellTargeting; }
    }

    /// <summary>For Interaction.cs: is a screen position inside the small
    /// 3D viewport (original scheme)? A world raycast from outside (e.g. over the
    /// paper doll/backpack panel) would otherwise hit invisible geometry behind the UI, since
    /// the UI does not block the physics query (no EventSystem/GraphicRaycaster).</summary>
    public bool IsScreenPositionInGameArea(Vector2 pOScreenPos)
    {
        return pOScreenPos.x > gameAreaLeft && pOScreenPos.x < gameAreaRight
            && pOScreenPos.y > gameAreaBottom && pOScreenPos.y < gameAreaTop;
    }

    /// <summary>For UWItemDrag (original: drop vs. throw on release in the
    /// 3D viewport) - 0 at the bottom, 1 at the top edge of the viewport. Only meaningful
    /// if IsScreenPositionInGameArea was already true for the same position.</summary>
    public float GetVerticalFractionInGameArea(Vector2 pOScreenPos)
    {
        float lfHeight = gameAreaTop - gameAreaBottom;

        return lfHeight > 0f ? Mathf.Clamp01((pOScreenPos.y - gameAreaBottom) / lfHeight) : 0f;
    }

    /// <summary>0 at the left, 1 at the right edge of the viewport. Counterpart to
    /// GetVerticalFractionInGameArea - needed for the target direction of
    /// spell projectiles, which the original computes from the horizontal pointer position.</summary>
    public float GetHorizontalFractionInGameArea(Vector2 pOScreenPos)
    {
        float lfWidth = gameAreaRight - gameAreaLeft;

        return lfWidth > 0f ? Mathf.Clamp01((pOScreenPos.x - gameAreaLeft) / lfWidth) : 0f;
    }

    // Canvas hierarchy (built once, see fBuildCanvas()).
    private GameObject mCanvasRoot;
    internal RectTransform mGameFrame;
    private RectTransform mBackdropLeftRect;
    private RectTransform mBackdropRightRect;

    /// <summary>The bars over and under the frame when the window is narrower than it (UWUiFit).
    /// </summary>
    private RectTransform mBackdropTopRect;

    private RectTransform mBackdropBottomRect;
    private Image mBackgroundImage;
    internal Image mMainFrameImage;
    private Image mWeaponImage;
    private Image mCharBackdropImage;
    private Image mCharImage;
    private Image mHelmetImage;
    private Image mGlovesImage;
    private Image mChestImage;
    private Image mLegsImage;
    private Image mBootsImage;
    private Image mPowerGemImage;
    internal Image mHealthFlaskImage;
    internal Image mManaFlaskImage;

    internal Interaction mOInteraction;

    internal UWInventory mOInventory;

    // Cached texture references, to call Sprite.Create only on a real change
    // instead of allocating anew every frame.
    private Texture2D mPrevWeaponTex;
    private Texture2D mPrevCharTex;
    private Texture2D mPrevHelmetTex;
    private Texture2D mPrevGlovesTex;
    private Texture2D mPrevChestTex;
    private Texture2D mPrevLegsTex;
    private Texture2D mPrevBootsTex;
    private Texture2D mPrevPowerGemTex;
    private Texture2D mPrevHealthFlaskTex;
    private Texture2D mPrevManaFlaskTex;

    public void Init(DataImport pOUWData)
    {
        mOUWData = pOUWData;

        Runes.fLoadSelectedRunes();
        var lOUWMain = mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);
        mOMainTexture = new Texture2D(lOUWMain.Width, lOUWMain.Height, TextureFormat.ARGB32, false);
        mOMainTexture.name = "UWGameUI.cs:465";
        mOMainTexture.filterMode = TextureFilterMode;
        mOMainTexture.SetPixels32(fGetTextureInvert(lOUWMain));
        mOMainTexture.Apply();

        mOCursorTextures = new List<Texture2D>();
        for (int i = 0; i < 15; i++)
        {
            UWTexture lOCursorTex = mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.CURSORS, i);
            Texture2D lOCursorTexture = new Texture2D(lOCursorTex.Width, lOCursorTex.Height, TextureFormat.RGBA32, false);
            lOCursorTexture.name = "UWGameUI.cs:474";
            lOCursorTexture.SetPixels32(fGetTextureInvert(lOCursorTex));
            lOCursorTexture.filterMode = TextureFilterMode;
            lOCursorTexture.Apply();
            mOCursorTextures.Add(lOCursorTexture);
        }

        LastCursorPositionInGameArea = new Vector2(gameAreaLeft + ((gameAreaRight - gameAreaLeft) / 2), gameAreaBottom + ((gameAreaTop - gameAreaBottom) / 2));

        if (mCanvasRoot == null)
            fInstantiateCanvas();

        mBackgroundImage.sprite = fToSprite(UWBackground);
        mMainFrameImage.sprite = fToSprite(mOMainTexture);

        Inventory.LoadSprites();
        MessageLog.LoadSprites();

        lbIsInitialized = true;

        // ONLY here: the runes from the save game have been in the list since Init, but their
        // pictures only exist once the interface is built. Without this they were
        // there but could not be seen (per user, 2026-09-05).
        Runes.fRefreshSelectedRunes();
    }

	void Start ()
    {
        mCharacter = GetComponentInParent<UWCharacter>();
        mControlSchemeRef = GetComponentInParent<UWControlScheme>();
        mMovementRef = GetComponentInParent<UWPlayerMovement>();
        lbIsInitialized = false;

        // No manual wiring in the scene needed - same self-creation pattern
        // as UWInventory in Interaction.Awake().
        mOInventory = GetComponent<UWInventory>();

        if (mOInventory == null)
            mOInventory = gameObject.AddComponent<UWInventory>();

        // The help window (F1), the same self-creation.
        if (GetComponent<UWHelpWindow>() == null)
            gameObject.AddComponent<UWHelpWindow>();

        Inventory.mBackpackItemsShown = new UWObject[UWHudInventory.BackpackSlotCount];
        Inventory.mBackpackSourcesShown = new UWTexture[UWHudInventory.BackpackSlotCount];
        Inventory.mEquipIconItemsShown = new UWObject[UWHudInventory.ArmorHitZoneSlots.Length - UWHudInventory.ArmorSlotCount];
        Inventory.mEquipIconSourcesShown = new UWTexture[UWHudInventory.ArmorHitZoneSlots.Length - UWHudInventory.ArmorSlotCount];
        Inventory.mContainerItemsShown = new UWObject[UWHudInventory.ContainerSlotCount];
        Inventory.mContainerSourcesShown = new UWTexture[UWHudInventory.ContainerSlotCount];

        // UWGameUI sits on the same GameObject as Camera.main (see UWLevelLoader.
        // fInitialiseUi) - mfDefaultFov remembers the Modern value set in the Inspector,
        // before the original scheme overwrites it (see fApplyGameCamera).
        mOCamera = GetComponent<Camera>();

        if (mOCamera != null)
            mfDefaultFov = mOCamera.fieldOfView;

        // The modern scheme's HUD and game menu - idle in the original scheme.
        if (GetComponent<UWModernHud>() == null)
            gameObject.AddComponent<UWModernHud>();

        if (GetComponent<UWModernBags>() == null)
            gameObject.AddComponent<UWModernBags>();

        if (GetComponent<UWModernPanel>() == null)
            gameObject.AddComponent<UWModernPanel>();

        if (GetComponent<UWModernMinimap>() == null)
            gameObject.AddComponent<UWModernMinimap>();

        if (GetComponent<UWModernActionBar>() == null)
            gameObject.AddComponent<UWModernActionBar>();

        if (GetComponent<UWModernRunePanel>() == null)
            gameObject.AddComponent<UWModernRunePanel>();

        if (GetComponent<UWModernQuestion>() == null)
            gameObject.AddComponent<UWModernQuestion>();

        if (GetComponent<UWModernConversation>() == null)
            gameObject.AddComponent<UWModernConversation>();

        if (GetComponent<UWModernLayoutEditor>() == null)
            gameObject.AddComponent<UWModernLayoutEditor>();
    }

	// Update is called once per frame
	void Update ()
    {
        if (!lbIsInitialized)
            return;

        // The key lock while typing, for all actions - see UWControls.IsTextEntryActive.
        UWControls.SetTextEntryActive(IsTextEntryActive || UWDebugTools.IsTyping);

        fCheckFontToggle();

        Runes.fRefreshSpellIconsIfChanged();

        // In Modern the 3D view should fill the whole screen instead of disappearing behind the
        // small original window with black bars. The original window
        // including paper doll belongs to the original scheme, not to the modern controls.
        //
        // THE BIG MAP is the exception (per user, 2026-10-04): in the original it fills the whole
        // screen, and it lives on the classic frame - opened in the modern scheme the frame shows
        // under it, 4:3 with dark bars, and everything works as there (notes, eraser, levels,
        // the quill); the modern UI hides meanwhile (UWModernHud.fIsCovered).
        bool lbShowClassicUi = mControlSchemeRef == null || mControlSchemeRef.Current == UWControlScheme.SchemeEnum.Original
            || Map.IsMapVisible;

        mCanvasRoot.SetActive(lbShowClassicUi);

        if (!lbShowClassicUi)
        {
            fApplyGameCamera(false);

            // THE MODERN SCHEME'S POINTER (the bags, the menu) is always the cross, whatever the
            // classic one showed last - the arrows, the red X (per user, 2026-10-03). The choice
            // below is never reached in this scheme, so the last classic pointer used to stay.
            // A spell waiting for its target shows the original's target pointer instead (per
            // user, 2026-10-04; with the pointer locked UWModernHud puts it in the crosshair's place).
            // A ranged weapon drawn far enough shows the same pointer as in the classic scheme
            // (IsRangedTargeting, per user the same day: so one sees when the charge suffices).
            bool lbWorldPointer = UWModernHud.Instance == null || !UWModernHud.Instance.IsOpen;
            int liModernCursor = mOInteraction != null && mOInteraction.IsSpellTargeting && lbWorldPointer
                ? (mOInteraction.IsTargetSpellPending ? UWHudRunes.TargetSpellCursor : UWHudRunes.SpellTargetCursor)
                : mOInteraction != null && mOInteraction.IsRangedTargeting && lbWorldPointer
                ? UWHudRunes.SpellTargetCursor
                : (int)UWCursors.CursorEnum.Center;

            if (Cursor.visible && mOCursorTextures != null && liModernCursor < mOCursorTextures.Count)
            {
                Texture2D lOCross = fGetScaledCursor(liModernCursor);

                Cursor.SetCursor(lOCross, new Vector2(lOCross.width * 0.5f, lOCross.height * 0.5f), CursorMode.Auto);
            }

            return;
        }

        fApplyGameCamera(true);

        // Bar width depends on the aspect ratio - the CanvasScaler already delivers the
        // actual canvas width in the same reference units as GameFrame.
        mGameFrame.localScale = new Vector3(HorizontalPixelFactor, 1f, 1f);

        // The frame slides left while the help window is open (UWHelpLayout): the left bar
        // shrinks by what the right one gains.
        float lfCanvasWidth = UWUiFit.CanvasWidth;
        float lfShift = UWHelpLayout.FrameShift(lfCanvasWidth);
        float lfMarginWidth = fGetLetterboxMarginRefUnits();
        float lfRightMargin = Mathf.Max(0f, lfCanvasWidth - lfMarginWidth - (mGameFrame.sizeDelta.x * HorizontalPixelFactor));

        mGameFrame.anchoredPosition = new Vector2(lfShift, 0f);
        mBackdropLeftRect.sizeDelta = new Vector2(lfMarginWidth, mBackdropLeftRect.sizeDelta.y);
        mBackdropRightRect.sizeDelta = new Vector2(lfRightMargin, mBackdropRightRect.sizeDelta.y);

        // A window narrower than the frame fits it by the width (UWUiFit): bars at the top and
        // the bottom instead.
        float lfVerticalMargin = Mathf.Max(0f, (UWUiFit.CanvasHeight - UWUiFit.FrameHeight) / 2f);

        mBackdropTopRect.sizeDelta = new Vector2(mBackdropTopRect.sizeDelta.x, lfVerticalMargin);
        mBackdropBottomRect.sizeDelta = new Vector2(mBackdropBottomRect.sizeDelta.x, lfVerticalMargin);

        mWeaponImage.gameObject.SetActive(mCharacter.DrawWWeapon);

        if (mCharacter.DrawWWeapon)
        {
            WeaponCoordinate lOOffset = mCharacter.GetWeaponOffset();
            fUpdateSprite(mWeaponImage, mCharacter.CurrentWeaponTexture, ref mPrevWeaponTex);

            float lfY = -wTopOffset + lOOffset.Y - (mCharacter.CurrentWeaponTexture.height * (1f - mCharacter.CurrentReadyWeaponTime));

            ((RectTransform)mWeaponImage.transform).anchoredPosition = new Vector2(wLeftOffset + lOOffset.X + GetWeaponSway(), lfY);
        }

        fUpdateSprite(mCharImage, mCharacter.CharTexture, ref mPrevCharTex);
        fMatchSize(mCharBackdropImage, mCharImage);
        fUpdateSprite(mHelmetImage, mCharacter.HelmetTexture, ref mPrevHelmetTex);
        fUpdateSprite(mGlovesImage, mCharacter.GlovesTexture, ref mPrevGlovesTex);
        fUpdateSprite(mChestImage, mCharacter.ChestArmorTexture, ref mPrevChestTex);
        fUpdateSprite(mLegsImage, mCharacter.LegsArmorTexture, ref mPrevLegsTex);
        fUpdateSprite(mBootsImage, mCharacter.BootsTexture, ref mPrevBootsTex);
        fUpdateSprite(mPowerGemImage, mCharacter.PowerGemTeture, ref mPrevPowerGemTex);
        fUpdateSprite(mHealthFlaskImage, mCharacter.HealthFlaskTeture, ref mPrevHealthFlaskTex);
        fUpdateSprite(mManaFlaskImage, mCharacter.ManaFlaskTeture, ref mPrevManaFlaskTex);

        Inventory.Update();
        MessageLog.Refresh();
        Panel.Update();
        Panel.fRefreshStatsIfChanged();

        // AT THE VERY END, after everything that makes up the frame. The compass is an
        // extra; if something goes wrong there, it should not drag down the paper doll, the equipment
        // and the backpack - those come later in the same pass.
        Compass.Update();
        fUpdateDragons();

        fCursorMovement();

        if (Cursor.visible)
        {
            // On the map you hold a quill, when erasing the eraser stone.
            // A projectile spell waits for its target and shows its own pointer for that.
            // When shooting, the same pointer as for the projectile spell shows as soon as the
            // weapon has its minimum charge - see Interaction.IsRangedTargeting.
            // In the main menu and in character creation only the cross (per user,
            // 2026-09-11) - the viewport below would otherwise show the arrows.
            // In conversation likewise only the cross (per user, 2026-09-13).
            // In a conversation the red X of look, get and use STAYS (per user on the original,
            // 2026-09-19, with a goal-10 NPC addressing the player in use mode) - only the
            // function is the usual one there.
            // Over the setup bar and its menus and dialogs, which fold out over the options
            // panel since 2026-10-06, only the cross as well (per user the same day).
            int liCursor = UWConversationScreen.IsAnyOpen && mOInteraction != null && mOInteraction.IsRedXCursorMode
                ? (int)UWCursors.CursorEnum.Use
                : UWScreenUi.IsScreenMenuOpen || UWConversationScreen.IsAnyOpen || !fIsFirstImageShown() || UWSetupMenu.IsCapturingInput
                ? (int)UWCursors.CursorEnum.Center
                : Map.IsMapVisible
                ? (Map.mbMapEraseMode ? UWHudMap.MapEraseCursor : UWHudMap.MapQuillCursor)
                : (mOInteraction != null && mOInteraction.IsSpellTargeting
                    ? (mOInteraction.IsTargetSpellPending ? UWHudRunes.TargetSpellCursor : UWHudRunes.SpellTargetCursor)
                    : (mOInteraction != null && mOInteraction.IsRangedTargeting
                        ? UWHudRunes.SpellTargetCursor
                        // Look, get and use: the red X instead of the movement arrows
                        // (per user on the original, 2026-09-19); talk keeps them.
                        : (mOInteraction != null && mOInteraction.IsRedXCursorMode
                            ? (int)UWCursors.CursorEnum.Use
                            : (int)CursorMovement)));

            if (liCursor >= 0 && liCursor < mOCursorTextures.Count)
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
                // 2026-09-03).
                Vector2 lOHotspot = Map.IsMapVisible && Map.mbMapEraseMode
                    ? new Vector2(0f, lOCursor.height * 0.5f)
                    : new Vector2(lOCursor.width * 0.5f, lOCursor.height * 0.5f);

                Cursor.SetCursor(lOCursor, lOHotspot, CursorMode.Auto);
            }
        }
	}

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

    /// <summary>
    /// Builds the classic UI canvas in code (fBuildCanvas) and adds the parts built at
    /// runtime (compass, dragons, options menu). Until 2026-09-15 the hierarchy came from a canvas prefab generated from
    /// fBuildCanvas; it was compared with the code-built canvas (identical apart from four
    /// exported original sprites, which Init now loads from the game data) and removed. The
    /// Find() re-wiring that dated from the prefab went on 2026-09-18: fBuildCanvas assigns
    /// every reference itself, and each section builds its own part (Inventory.BuildBackpackSlots,
    /// BuildContainerView and BuildEquipmentSlots, MessageLog.Build, Pictures.Build).
    /// </summary>
    private void fInstantiateCanvas()
    {
        fBuildCanvas();
        mCanvasRoot.name = "Classic UI Canvas";

        // BELOW the frame. For a short time it lay above, because in the original at the left dragon
        // a black outline appears that the frame itself does not have - that looked like
        // overdrawing. But it is not: laid above, thick black
        // bars appear (discarded immediately per user, 2026-08-29). The outline comes about differently,
        // entirely without overdrawing - the dragon reaches into the hole, and the black of the
        // picture fills the area right next to it there, where the 3D view otherwise is.
        Pictures.mWindowPictureImage.transform.SetSiblingIndex(mMainFrameImage.transform.GetSiblingIndex());

        Pictures.mWindowPictureImage.enabled = false;

        // THE COMPASS IS BUILT AT RUNTIME, not in the prefab. It consists of twenty
        // small pictures from COMPASS.GR, of which always exactly two are visible - storing that
        // in the prefab gained nothing, and on the first attempt the call was in the
        // editor generator (fBuildCanvas), which does not run in the game at all. The compass therefore
        // stayed invisible (per user, 2026-09-07).
        //
        // Attached as the LAST children they lie above the frame picture, in which the compass
        // without needle is already baked in.
        Compass.Build();

        // The HUD dragons: neck, head and tail pictures from DRAGONS.GR above the frame, see
        // UWHudDragons.
        mODragons = new UWHudDragons(mGameFrame, fGetDragonSprite);

        // The same applies to the options panel: it consists of nine pictures that constantly
        // change, and therefore does not belong in the prefab.
        Options.Build();
        Commands.Build();

    }

    /// <summary>
    /// Builds the canvas hierarchy once. The order of the children matches the
    /// drawing order of the earlier OnGUI: weapon behind the frame picture (so that in the
    /// original window it shines through its transparent centre, but is cut off at the edge by the
    /// frame), frame behind the paper doll.
    /// </summary>
    private void fBuildCanvas()
    {
        mCanvasRoot = new GameObject("Classic UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        mCanvasRoot.transform.SetParent(transform, false);

        Canvas lOCanvas = mCanvasRoot.GetComponent<Canvas>();
        lOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Fitted whole into the window - by the height, by the width when narrow (UWUiFit).
        UWUiFit.Apply(mCanvasRoot.GetComponent<CanvasScaler>());
        mCanvasRoot.AddComponent<UWFitCanvas>();

        RectTransform lOCanvasRect = (RectTransform)mCanvasRoot.transform;

        // Two narrow bars left/right of the 320 wide frame, not one full
        // area - otherwise the actual 3D picture behind the transparent centre
        // of MainFrame would be covered. Width is tracked in Update() from the actual
        // canvas width (in reference units), since it depends on the aspect
        // ratio.
        Image lOBackdropLeft = fCreateImage("BackdropLeft", lOCanvasRect, 0f, 0f, 0f, 0f, Color.black);
        mBackdropLeftRect = (RectTransform)lOBackdropLeft.transform;
        mBackdropLeftRect.anchorMin = new Vector2(0f, 0f);
        mBackdropLeftRect.anchorMax = new Vector2(0f, 1f);
        mBackdropLeftRect.pivot = new Vector2(0f, 0.5f);
        mBackdropLeftRect.anchoredPosition = Vector2.zero;
        mBackdropLeftRect.offsetMin = new Vector2(0f, 0f);
        mBackdropLeftRect.offsetMax = new Vector2(0f, 0f);

        Image lOBackdropRight = fCreateImage("BackdropRight", lOCanvasRect, 0f, 0f, 0f, 0f, Color.black);
        mBackdropRightRect = (RectTransform)lOBackdropRight.transform;
        mBackdropRightRect.anchorMin = new Vector2(1f, 0f);
        mBackdropRightRect.anchorMax = new Vector2(1f, 1f);
        mBackdropRightRect.pivot = new Vector2(1f, 0.5f);
        mBackdropRightRect.anchoredPosition = Vector2.zero;
        mBackdropRightRect.offsetMin = new Vector2(0f, 0f);
        mBackdropRightRect.offsetMax = new Vector2(0f, 0f);

        mBackdropTopRect = fCreateBar("BackdropTop", lOCanvasRect, 1f);
        mBackdropBottomRect = fCreateBar("BackdropBottom", lOCanvasRect, 0f);

        GameObject lOFrameObj = new GameObject("GameFrame", typeof(RectTransform));
        lOFrameObj.transform.SetParent(lOCanvasRect, false);
        mGameFrame = (RectTransform)lOFrameObj.transform;
        mGameFrame.anchorMin = new Vector2(0.5f, 0.5f);
        mGameFrame.anchorMax = new Vector2(0.5f, 0.5f);
        mGameFrame.pivot = new Vector2(0.5f, 0.5f);
        mGameFrame.sizeDelta = new Vector2(320f, 200f);
        mGameFrame.anchoredPosition = Vector2.zero;

        mWeaponImage = fCreateImage("Weapon", mGameFrame, wLeftOffset, -wTopOffset, 1f, 1f, Color.white);

        mBackgroundImage = fCreateImage("Background", mGameFrame, 0f, 0f, 320f, 200f, Color.white);
        fStretch(mBackgroundImage);

        Pictures.Build();

        mMainFrameImage = fCreateImage("MainFrame", mGameFrame, 0f, 0f, 320f, 200f, Color.white);
        fStretch(mMainFrameImage);

        mCharBackdropImage = fCreateImage("CharBackdrop", mGameFrame, charLeftOffset, -charTopOffset, 1f, 1f, Color.black);
        mCharImage = fCreateImage("CharBody", mGameFrame, charLeftOffset, -charTopOffset, 1f, 1f, Color.white);
        mHelmetImage = fCreateImage("Helmet", mGameFrame, helmetArmorLeftOffset, -helmetArmorTopOffset, 1f, 1f, Color.white);
        mGlovesImage = fCreateImage("Gloves", mGameFrame, glovesArmorLeftOffset, -glovesArmorTopOffset, 1f, 1f, Color.white);
        mLegsImage = fCreateImage("Legs", mGameFrame, legsArmorLeftOffset, -legsArmorTopOffset, 1f, 1f, Color.white);
        mChestImage = fCreateImage("Chest", mGameFrame, chestArmorLeftOffset, -chestArmorTopOffset, 1f, 1f, Color.white);
        mBootsImage = fCreateImage("Boots", mGameFrame, bootsArmorLeftOffset, -bootsArmorTopOffset, 1f, 1f, Color.white);
        mPowerGemImage = fCreateImage("PowerGem", mGameFrame, powerGemLeft, -powerGemTop, 1f, 1f, Color.white);
        mHealthFlaskImage = fCreateImage("HealthFlask", mGameFrame, healthFlaskLeft, -flaskTop, 1f, 1f, Color.white);
        mManaFlaskImage = fCreateImage("ManaFlask", mGameFrame, manaFlaskLeft, -flaskTop, 1f, 1f, Color.white);

        Inventory.BuildBackpackSlots();
        Inventory.BuildContainerView();
        Inventory.BuildEquipmentSlots();
        MessageLog.Build();
    }

    internal Image fCreateCenterPivotImage(string psName, float pfCenterX, float pfCenterTopY, float pfWidth, float pfHeight)
    {
        GameObject lOObj = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObj.transform.SetParent(mGameFrame, false);

        RectTransform lORect = (RectTransform)lOObj.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0.5f, 0.5f);
        lORect.anchoredPosition = new Vector2(pfCenterX, -pfCenterTopY);
        lORect.sizeDelta = new Vector2(pfWidth, pfHeight);

        return lOObj.GetComponent<Image>();
    }

    /// <summary>Builds a scroll arrow placeholder square with pivot bottom-right (grows
    /// up and left from a right-aligned anchor line) - unlike fCreateImage,
    /// which only knows the top-left convention.</summary>
    internal Image fCreateScrollArrowPlaceholder(string psName, float pfRightX)
    {
        GameObject lOObj = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObj.transform.SetParent(mGameFrame, false);

        RectTransform lORect = (RectTransform)lOObj.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(1f, 0f);
        lORect.anchoredPosition = new Vector2(pfRightX, -UWHudInventory.containerScrollTop);
        lORect.sizeDelta = new Vector2(UWHudInventory.containerScrollSize, UWHudInventory.containerScrollSize);

        Image lOImage = lOObj.GetComponent<Image>();
        lOImage.color = new Color(1f, 1f, 1f, 0.4f);
        lOImage.raycastTarget = false;
        lOImage.gameObject.SetActive(false);

        return lOImage;
    }

    /// <summary>Is typing going on anywhere - map note, save game name, answer in
    /// conversation, name, mantra, help notes or an input line? Then letter keys belong to the
    /// text: F does not toggle a font, J does not jump.</summary>
    public bool IsTextEntryActive
    {
        get
        {
            if (mOInteraction == null)
                mOInteraction = GetComponent<Interaction>();

            return Map.mbMapWriting || Options.miSaveSlotTyping > 0 || UWModernHud.IsTypingName || UWConversationScreen.IsTypingAnswer
                || UWCharacterCreationScreen.IsTypingName
                || UWInstrumentPlayer.IsPlaying || UWHelpWindow.BlocksGameKeys || UWModernBags.IsSplitting || UWModernBags.IsMenuOpen
                || (mOInteraction != null && (mOInteraction.IsChanting || mOInteraction.IsPromptActive));
        }
    }

    /// <summary>
    /// Text is being typed - a map note, a save game's name, a mantra or the input line, a
    /// conversation's answer - unlike IsTextEntryActive, which also holds the keys for the help,
    /// the instrument, the split box and the bags' menu. The gamepad's letter grid comes up for
    /// these (UWLetterGrid; the character's name it asks of the creation screen itself).
    /// </summary>
    public bool IsTypingText
    {
        get
        {
            if (mOInteraction == null)
                mOInteraction = GetComponent<Interaction>();

            // Not the yes/no question (answered by A and B, Interaction) nor the count prompt
            // (the d-pad turns the number, UWItemDrag; per user, 2026-10-07: no grid there).
            bool lbCountPrompt = UWScene.ItemDrag != null && UWScene.ItemDrag.IsAskingCount;

            return Map.mbMapWriting || Options.miSaveSlotTyping > 0 || UWModernHud.IsTypingName || UWConversationScreen.IsTypingAnswer
                || (mOInteraction != null && (mOInteraction.IsChanting || (mOInteraction.ShowsPromptTextCursor && !lbCountPrompt)));
        }
    }

    /// <summary>
    /// The two keys of our own: F switches all fonts between original and modern (see
    /// UWTextLabel), M opens the map. Both come from the Player map since 2026-09-17, so they can
    /// be changed in the menu bar (UWKeyBindings). Not while typing anywhere - map note, save
    /// game name, mantra, input line - there the letters belong to the text.
    /// </summary>
    private void fCheckFontToggle()
    {
        UWControls lOControls = mControlSchemeRef != null ? mControlSchemeRef.Controls : null;

        if (lOControls == null)
            return;

        if (mOInteraction == null)
            mOInteraction = GetComponent<Interaction>();

        // In the main menu and in creation the keys stay completely silent (per user,
        // 2026-09-11).
        if (IsTextEntryActive || UWScreenUi.IsScreenMenuOpen)
            return;

        // Ctrl+F and Ctrl+M are the original's option shortcuts (sound and music, with Ctrl+S,
        // R, D and Q; per user in the original, 2026-09-26: it is Ctrl, Alt does nothing) - not
        // ours.
        bool lbCtrl = UWControls.IsCtrlHeld;

        if (!lbCtrl && lOControls.Player.ToggleFont.WasPressedThisFrame())
            UWTextLabel.UseModernFont = !UWTextLabel.UseModernFont;

        // THE MODERN SCHEME has no classic frame: the options, the map and the command icons live
        // on it, and opened there they were invisible and held the game (found 2026-10-03). There
        // F1, Escape and the Ctrl shortcuts open the modern game menu (UWModernHud); the map, the
        // command keys and the rune shelf wait for their modern counterparts; F9 and F10 stay.
        if (mControlSchemeRef != null && mControlSchemeRef.Current == UWControlScheme.SchemeEnum.Modern)
        {
            fCheckModernKeys(lOControls);
            return;
        }

        // Not in a conversation: using the map is locked there, so the key (our addition) is too.
        // The gamepad's pointer takes the d-pad as its wheel - except to close the map again.
        if (!lbCtrl && lOControls.Player.ToggleMap.WasPerformedThisFrame() && !UWConversationScreen.IsAnyOpen
            && (Map.IsMapVisible || !UWGamepadPointer.Takes(lOControls.Player.ToggleMap)))
            fToggleMapByKey();

        fCheckOptionShortcuts(lOControls);
        fCheckOriginalKeys(lOControls);
    }

    /// <summary>The keys of the modern scheme that UWGameUI routes - see fCheckFontToggle.</summary>
    private void fCheckModernKeys(UWControls pOControls)
    {
        UWModernHud lOHud = UWModernHud.Instance;

        if (lOHud == null || UWConversationScreen.IsAnyOpen || UWRoamingSight.IsAnyRunning || mOInteraction == null)
            return;

        UWControls.PlayerActions lOPlayer = pOControls.Player;

        // The big map open: M or Escape close it (writing a note, the keys are the note's).
        if (Map.IsMapVisible)
        {
            // The gamepad's B closes it too (UWGamepad).
            if (!UWControls.IsCtrlHeld && (lOPlayer.ToggleMap.WasPerformedThisFrame() || lOPlayer.Menu.WasPressedThisFrame()
                || (lOPlayer.PadBack.WasPressedThisFrame() && !UWLetterGrid.IsShown)))
                Map.HideMap();

            return;
        }

        // THE RIGHT CTRL ONLY: the left one sinks in the modern scheme (per user, 2026-10-04) -
        // held while walking back it would open the save page with S, with R the restore page.
        if (UWControls.IsRightCtrlHeld && !UWControls.IsShiftHeld && !UWControls.IsAltHeld)
        {
            if (lOPlayer.OptSave.WasPressedThisFrame())
                lOHud.OpenMenu(UWModernHud.PageEnum.Save);
            else if (lOPlayer.OptRestore.WasPressedThisFrame())
                lOHud.OpenMenu(UWModernHud.PageEnum.Load);
            else if (lOPlayer.OptMusic.WasPressedThisFrame() || lOPlayer.OptSound.WasPressedThisFrame()
                || lOPlayer.OptDetail.WasPressedThisFrame() || lOPlayer.OptQuit.WasPressedThisFrame())
                lOHud.OpenMenu(UWModernHud.PageEnum.Main);

            return;
        }

        if (UWControls.IsShiftHeld || UWControls.IsAltHeld)
            return;

        // Escape first lets a waiting spell go (UWHudRunes.fAfterModernCast), then closes the bags
        // (UWModernBags), the character panel (UWModernPanel; a pinned one only leaves its Help
        // tab) and the rune panel (UWModernRunePanel); the menu comes after.
        UWModernBags lOBags = UWModernBags.Instance;
        UWModernPanel lOPanel = UWModernPanel.Instance;
        UWModernRunePanel lORunePanel = UWModernRunePanel.Instance;

        // The gamepad's B does what Escape does while something is open (PadBackCloses);
        // otherwise it jumps (UWPlayerMovement).
        bool lbBack = lOPlayer.Menu.WasPressedThisFrame()
            || (lOPlayer.PadBack.WasPressedThisFrame() && !UWLetterGrid.IsShown && !UWModernBags.IsMenuOpen && !UWModernBags.IsSplitting
                && PadBackCloses());

        if (lbBack && mOInteraction != null && mOInteraction.IsSpellTargeting && !lOHud.IsOpen
            && mControlSchemeRef != null && mControlSchemeRef.Current == UWControlScheme.SchemeEnum.Modern)
            mOInteraction.CancelPendingSpell();
        else if (lbBack && lOBags != null && lOBags.IsUsing && !lOHud.IsOpen)
            lOBags.CancelUse();
        else if (lbBack && lOBags != null && lOBags.IsOpen && !lOHud.IsOpen)
            lOBags.Close();
        else if (lbBack && lOPanel != null && lOPanel.IsClosable && !lOHud.IsOpen)
            lOPanel.Close();
        else if (lbBack && lORunePanel != null && lORunePanel.IsClosable && !lOHud.IsOpen)
            lORunePanel.Close();
        else if (lOPlayer.KeyOptions.WasPressedThisFrame() || lbBack)
            lOHud.ToggleMenu();
        else if (lOHud.IsOpen)
            return;
        else if (lOPlayer.KeyTrack.WasPressedThisFrame())
            Runes.TrackByKey();
        else if (lOPlayer.KeyCamp.WasPressedThisFrame() && UWScene.ItemDrag != null)
            UWScene.ItemDrag.TrySleep();
        else if (lOPlayer.ToggleMap.WasPerformedThisFrame() && !UWGamepadPointer.Takes(lOPlayer.ToggleMap))
            fToggleMapByKey();
    }

    /// <summary>The modern minimap's click (UWModernMinimap): the big map, with the map carried.</summary>
    public bool OpenBigMap()
    {
        if (!IsCarryingMap || Map.IsMapVisible)
            return false;

        fToggleMapByKey();
        return Map.IsMapVisible;
    }

    /// <summary>
    /// The original's Ctrl shortcuts (built 2026-09-26, see UWHudOptions.ChooseByShortcut):
    /// Ctrl+S save, Ctrl+R restore, Ctrl+M music, Ctrl+F sound, Ctrl+D detail, Ctrl+Q quit.
    /// Normal play only, as the original registers them. Partway through an action and on
    /// level 9 the options refuse to open - see UWHudOptions.RefusesToOpen.
    /// </summary>
    private void fCheckOptionShortcuts(UWControls pOControls)
    {
        if (!UWControls.IsCtrlHeld || UWControls.IsShiftHeld || UWControls.IsAltHeld)
            return;

        // Not while the view roams: the options are locked then, as in the original.
        if (UWConversationScreen.IsAnyOpen || Map.IsMapVisible || Options.IsOpen || mOInteraction == null
            || UWRoamingSight.IsAnyRunning)
            return;

        UWControls.PlayerActions lOPlayer = pOControls.Player;
        int liEntry = lOPlayer.OptSave.WasPressedThisFrame() ? UWHudOptions.TopSave
            : lOPlayer.OptRestore.WasPressedThisFrame() ? UWHudOptions.TopRestore
            : lOPlayer.OptMusic.WasPressedThisFrame() ? UWHudOptions.TopMusic
            : lOPlayer.OptSound.WasPressedThisFrame() ? UWHudOptions.TopSound
            : lOPlayer.OptDetail.WasPressedThisFrame() ? UWHudOptions.TopDetail
            : lOPlayer.OptQuit.WasPressedThisFrame() ? UWHudOptions.TopQuit
            : -1;

        if (liEntry < 0)
            return;

        // "Partway" and "between worlds" are refused by the options themselves.
        Options.ChooseByShortcut(liEntry);
    }

    private int miPadBackFrame = -1;

    private bool mbPadBackCloses;

    /// <summary>
    /// The gamepad's B (UWGamepad): whether it closes something - the menu, a waiting spell, the
    /// use mode, the bags, a closable panel - in the modern scheme. Worked out once a frame, before
    /// anything closes, so the jump (UWPlayerMovement) sees the same answer whichever runs first.
    /// </summary>
    public bool PadBackCloses()
    {
        if (miPadBackFrame == Time.frameCount)
            return mbPadBackCloses;

        miPadBackFrame = Time.frameCount;

        UWModernBags lOBags = UWModernBags.Instance;
        UWModernPanel lOPanel = UWModernPanel.Instance;
        UWModernRunePanel lORunePanel = UWModernRunePanel.Instance;
        UWModernHud lOHud = UWModernHud.Instance;

        mbPadBackCloses = mControlSchemeRef != null && mControlSchemeRef.Current == UWControlScheme.SchemeEnum.Modern
            && ((lOHud != null && lOHud.IsOpen) || (Map != null && Map.IsMapVisible)
                || (mOInteraction != null && mOInteraction.IsSpellTargeting)
                || (lOBags != null && (lOBags.IsUsing || lOBags.IsOpen))
                || (lOPanel != null && lOPanel.IsClosable)
                || (lORunePanel != null && lORunePanel.IsClosable));

        return mbPadBackCloses;
    }

    /// <summary>
    /// THE ORIGINAL'S FUNCTION KEYS (built 2026-09-26, see UWControls.KeyOptions): F1 options,
    /// F2 to F6 the command modes as a click on their icon, F7 turns the panel, F8 casts, F9
    /// tracks (UWMiscSpellRules.Track, since 2026-09-27), F10 makes camp. Not with Shift (our own keys) nor Alt (Alt+F4) nor Ctrl; not in a
    /// conversation - the original registers them for its normal mode only -, not over the
    /// map and not while the options panel is up.
    /// </summary>
    private void fCheckOriginalKeys(UWControls pOControls)
    {
        if (UWControls.IsShiftHeld || UWControls.IsAltHeld || UWControls.IsCtrlHeld)
            return;

        if (UWConversationScreen.IsAnyOpen || Map.IsMapVisible || mOInteraction == null)
            return;

        UWControls.PlayerActions lOPlayer = pOControls.Player;

        // WHILE THE VIEW ROAMS only F7 turns the panel - in the original too, as does the chain
        // (per user, 2026-09-27: F1 to F6 and F8 are refused there). F9 and F10 are refused as a
        // DELIBERATE DEVIATION (decided per user the same day): the original still tracks and
        // makes camp, and sleeping leaves the spell gone from the bar but the view stuck.
        if (UWRoamingSight.IsAnyRunning)
        {
            if (lOPlayer.KeyPanel.WasPressedThisFrame())
                Panel.TogglePanelSide();

            return;
        }

        if (lOPlayer.KeyOptions.WasPressedThisFrame())
        {
            Options.OpenByKey();
            return;
        }

        if (Options.IsOpen)
            return;

        if (lOPlayer.KeyTalk.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Talk);
        else if (lOPlayer.KeyGet.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Get);
        else if (lOPlayer.KeyLook.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Look);
        else if (lOPlayer.KeyFight.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Fight);
        else if (lOPlayer.KeyUse.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Use);
        else if (lOPlayer.KeyPanel.WasPressedThisFrame())
            Panel.TogglePanelSide();
        else if (lOPlayer.KeyCast.WasPressedThisFrame())
            Runes.CastByKey();
        else if (lOPlayer.KeyTrack.WasPressedThisFrame())
            Runes.TrackByKey();
        else if (lOPlayer.KeyCamp.WasPressedThisFrame() && UWScene.ItemDrag != null)
            UWScene.ItemDrag.TrySleep();
    }

    /// <summary>
    /// The map key (M by default): closes the map when it is up, otherwise opens it - but only
    /// if the character really carries a map (object 315), just as it can otherwise only be
    /// opened by using one (per user, 2026-09-17). The original has no key for it.
    /// </summary>
    private void fToggleMapByKey()
    {
        if (Map.mMapBackgroundImage != null && Map.mMapBackgroundImage.gameObject.activeSelf)
        {
            Map.HideMap();

            return;
        }

        if (IsCarryingMap)
            Map.ShowMap(Map.CurrentMapLevelIndex);
    }

    /// <summary>Whether the character carries the map (object 315) anywhere - in a slot or
    /// inside a container. The map key and the help window's map tab ask here.</summary>
    internal bool IsCarryingMap
    {
        get
        {
            UWInventory lOInventory = GetComponent<UWInventory>();

            if (lOInventory == null)
                return false;

            foreach (UWObject lOItem in lOInventory.EnumerateAll())
            {
                if (lOItem != null && lOItem.ID == UWObjectMechanics.MapObjectId)
                    return true;
            }

            return false;
        }
    }

    internal UWHudDragons mODragons;

    private readonly Dictionary<int, Sprite> mODragonSprites = new Dictionary<int, Sprite>();

    private bool mbDragonsHidden;

    /// <summary>
    /// Runs the dragons only on the main game screen (UW.EXE: the UI update is part of game mode 0,
    /// not of the map or the conversation). The conversation frame has its own dragons baked in, so
    /// the pictures are hidden there. Also the cover request of UW.EXE PlayerUpdateTick_seg024_24DC_3A4:
    /// damage this tick times four above the maximum, or any damage below 16 vitality.
    /// </summary>
    private void fUpdateDragons()
    {
        if (mODragons == null)
            return;

        // In conversation the pictures stay: CONV.BYT has the same resting dragons pixel for pixel,
        // and without them the selected runes lay over the right dragon (per user, 2026-09-14).
        // They only stand still there.
        bool lbHidden = false;

        if (lbHidden != mbDragonsHidden)
        {
            mbDragonsHidden = lbHidden;
            mODragons.SetVisible(!lbHidden);
        }

        if (Conversation.mbConversationFrame || Map.IsMapVisible)
            return;

        if (mCharacter != null)
        {
            int liDamage = mCharacter.TakeDamageThisTick();

            if (liDamage > 0 && ((liDamage * 4) > mCharacter.MaxHP || mCharacter.CurrentHP < CoverVitality))
                UWHudDragons.Request(UWHudDragons.CoverAnimation);
        }

        mODragons.Tick();
    }

    /// <summary>Below this vitality any damage makes a dragon cover its eyes.</summary>
    private const int CoverVitality = 16;

    private Sprite fGetDragonSprite(int piIndex)
    {
        Sprite lOSprite;

        if (mODragonSprites.TryGetValue(piIndex, out lOSprite))
            return lOSprite;

        UWTexture lOSource = null;

        try
        {
            lOSource = mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.DRAGONS, piIndex);
        }
        catch
        {
            lOSource = null;
        }

        if (lOSource == null || lOSource.Width <= 0 || lOSource.Height <= 0)
        {
            mODragonSprites[piIndex] = null;
            return null;
        }

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI dragon " + piIndex;
        lOTexture.filterMode = TextureFilterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(fGetTextureInvert(lOSource));
        lOTexture.Apply(false, false);

        lOSprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0f, 1f), 1f);
        mODragonSprites[piIndex] = lOSprite;

        return lOSprite;
    }

    internal UWControlScheme mOControlScheme;

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

    private Texture2D fGetScaledCursor(int piIndex)
    {
        int liScale = Mathf.Max(1, Mathf.FloorToInt(UWUiFit.Scale));

        if (liScale <= 1)
            return mOCursorTextures[piIndex];

        if (mOScaledCursors == null || miCursorScale != liScale)
        {
            mOScaledCursors = new Texture2D[mOCursorTextures.Count];
            miCursorScale = liScale;
        }

        if (mOScaledCursors[piIndex] != null)
            return mOScaledCursors[piIndex];

        Texture2D lOSource = mOCursorTextures[piIndex];

        int liWidth = lOSource.width * liScale;
        int liHeight = lOSource.height * liScale;

        Color32[] lOSourcePixels = lOSource.GetPixels32();
        Color32[] lOPixels = new Color32[liWidth * liHeight];

        for (int liY = 0; liY < liHeight; liY++)
        {
            int liSourceRow = (liY / liScale) * lOSource.width;

            for (int liX = 0; liX < liWidth; liX++)
                lOPixels[(liY * liWidth) + liX] = lOSourcePixels[liSourceRow + (liX / liScale)];
        }

        Texture2D lOScaled = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
        lOScaled.name = "UWGameUI.cs:5127";
        lOScaled.filterMode = FilterMode.Point;
        lOScaled.SetPixels32(lOPixels);
        lOScaled.Apply(false, false);

        mOScaledCursors[piIndex] = lOScaled;

        return lOScaled;
    }

    /// <summary>A black bar across the whole width at the top (1) or the bottom (0) of the
    /// canvas; its height is set every frame in Update.</summary>
    private static RectTransform fCreateBar(string psName, RectTransform pOParent, float pfEdge)
    {
        RectTransform lORect = (RectTransform)fCreateImage(psName, pOParent, 0f, 0f, 0f, 0f, Color.black).transform;

        lORect.anchorMin = new Vector2(0f, pfEdge);
        lORect.anchorMax = new Vector2(1f, pfEdge);
        lORect.pivot = new Vector2(0.5f, pfEdge);
        lORect.anchoredPosition = Vector2.zero;
        lORect.sizeDelta = Vector2.zero;

        return lORect;
    }

    internal static Image fCreateImage(string psName, RectTransform pOParent, float pfX, float pfY, float pfWidth, float pfHeight, Color pOColor)
    {
        GameObject lOObj = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObj.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObj.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(pfX, pfY);
        lORect.sizeDelta = new Vector2(pfWidth, pfHeight);

        Image lOImage = lOObj.GetComponent<Image>();
        lOImage.color = pOColor;
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private static void fStretch(Image pOImage)
    {
        RectTransform lORect = (RectTransform)pOImage.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.one;
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;
    }

    /// <summary>The weapon's jitter: the rolled offset and the drawn one - the same under
    /// Original, the drawn one gliding after the rolled one under Smooth.</summary>
    private float mfWeaponJitterTarget;

    private float mfWeaponJitter;

    private float mfWeaponJitterRollTimer;

    /// <summary>
    /// The weapon's jitter while moving, in pixels of the 320x200 frame, always to the left -
    /// the original's rule (UWHeadBobRules: the level from the speed, 0 to 4 or 0 to 9 pixels,
    /// kept above 85 per cent, 0 while swinging). Its own three-way switch
    /// (UWUserSettings.WeaponJitterMode, per user): Original rolls twenty times a second and
    /// jumps - the original as at twenty frames a second; Smooth rolls six times a second,
    /// glides and is scaled by its strength slider; Off draws the weapon still. Both are the
    /// same at every frame rate of ours.
    /// </summary>
    internal float GetWeaponSway()
    {
        UWPlayerMovement lOMovement = UWScene.PlayerMovement;
        UWHeadBobRules.ModeEnum leMode = UWUserSettings.WeaponJitterMode;

        if (leMode == UWHeadBobRules.ModeEnum.Off || lOMovement == null || mCharacter == null || !mCharacter.CanAttack)
        {
            mfWeaponJitterTarget = 0f;
            mfWeaponJitter = 0f;
            mfWeaponJitterRollTimer = 0f;
            return 0f;
        }

        bool lbSmooth = leMode == UWHeadBobRules.ModeEnum.Smooth;
        float lfRolls = lbSmooth ? UWHeadBobRules.WeaponSmoothRollsPerSecond : UWHeadBobRules.WeaponRollsPerSecond;

        mfWeaponJitterRollTimer -= Time.deltaTime;

        if (mfWeaponJitterRollTimer <= 0f)
        {
            mfWeaponJitterRollTimer += 1f / lfRolls;

            if (mfWeaponJitterRollTimer < 0f)
                mfWeaponJitterRollTimer = 0f;

            float lfFraction = lOMovement.NormalForwardSpeed > 0f && lOMovement.IsWalkingOnGround
                ? lOMovement.SmoothedHorizontalSpeed / lOMovement.NormalForwardSpeed
                : 0f;

            if (lfFraction < UWHeadBobRules.StandingFraction)
                lfFraction = 0f;

            int liRange = UWHeadBobRules.GetWeaponJitterRange(UWHeadBobRules.GetWeaponJitterLevel(lfFraction));

            if (liRange == 0)
                mfWeaponJitterTarget = 0f;
            else if (liRange > 0)
                mfWeaponJitterTarget = -UWRandom.Next(liRange) * (lbSmooth ? UWUserSettings.WeaponJitterStrength : 1f);
        }

        mfWeaponJitter = lbSmooth
            ? Mathf.Lerp(mfWeaponJitter, mfWeaponJitterTarget,
                1f - Mathf.Exp(-UWHeadBobRules.WeaponSmoothGlidePerSecond * Time.deltaTime))
            : mfWeaponJitterTarget;

        return mfWeaponJitter;
    }

    /// <summary>Sets sprite and size anew only on an actual texture change, instead of
    /// allocating a new sprite on every call.</summary>
    private static void fUpdateSprite(Image pOImage, Texture2D pOTexture, ref Texture2D pOCached)
    {
        if (pOTexture == null || pOTexture == pOCached)
            return;

        pOCached = pOTexture;
        pOImage.sprite = fToSprite(pOTexture);
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(pOTexture.width, pOTexture.height);
    }

    private static void fMatchSize(Image pOTarget, Image pOSource)
    {
        ((RectTransform)pOTarget.transform).sizeDelta = ((RectTransform)pOSource.transform).sizeDelta;
    }

    /// <summary>Sets a fixed graphic from the game data as the sprite of the Image on
    /// pOTarget (nothing happens if the object, the Image or the graphic is missing).</summary>
    internal void fSetStaticSprite(Transform pOTarget, UWTexture.TextureTypes peType, int piIndex)
    {
        Image lOImage = pOTarget != null ? pOTarget.GetComponent<Image>() : null;
        UWTexture lOSource = mOUWData.Textures.GetTextureByType(peType, piIndex);

        if (lOImage == null || lOSource == null)
            return;

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWGameUI." + peType + "_" + piIndex;
        lOTexture.filterMode = TextureFilterMode;
        lOTexture.SetPixels32(fGetTextureInvert(lOSource));
        lOTexture.Apply();
        lOImage.sprite = fToSprite(lOTexture);
    }

    internal static Sprite fToSprite(Texture2D pOTexture)
    {
        if (pOTexture == null)
            return null;

        return Sprite.Create(pOTexture, new Rect(0f, 0f, pOTexture.width, pOTexture.height), new Vector2(0.5f, 0.5f), 1f);
    }

    /// <summary>
    /// WHERE THE POINTER IS DRAWN this frame: the mouse position, or during a cursor movement
    /// the position it was clamped to (2026-10-06, per user with a screenshot: a ring on the
    /// pointer hung far to the right of it in the panel while turning right). After
    /// WarpCursorPosition the Input System keeps reporting the pushed-out position, so whoever
    /// reads Mouse.current.position - UWItemDrag for the thing on the pointer - draws beyond
    /// the edge the pointer itself is held at. This is the one position everyone draws with.
    /// </summary>
    public Vector2 PointerPosition { get; private set; }

    /// <summary>The 3D view's rectangle on the screen (pixels, bottom-left origin) - the gamepad's
    /// pointer keeps inside it during a held cursor movement (UWGamepadPointer).</summary>
    public Rect GameAreaRect => Rect.MinMaxRect(gameAreaLeft, gameAreaBottom, gameAreaRight, gameAreaTop);

    /// <summary>The classic message scroll's top edge on the screen (pixels, bottom-left origin) -
    /// the gamepad's letter grid stays above it, where the input line rolls (UWLetterGrid).</summary>
    public float MessageLogTopScreenY => GetUiPictureScreenRect(new Vector2(0f, UWHudMessageLog.messageLogTop), Vector2.zero).y;

    private void fCursorMovement()
    {
        Vector2 lOMousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        // During a held cursor drag (original scheme) the real pointer is
        // actively clamped to the small 3D viewport - via WarpCursorPosition, because
        // CursorLockMode.Confined only knows the border of the whole game window, not
        // this smaller rectangle. Outside a drag the pointer stays free, as in the
        // original from 1992, where it may also wander over paper doll & co.
        if (mMovementRef != null && mMovementRef.IsCursorMovementInProgress)
        {
            // The gamepad's pointer: its own position, the Input System's is stale after the
            // warps - with it the pointer stuck to the view's top and left edges (per user,
            // 2026-10-07; UWGamepadPointer.OwnsPosition).
            if (UWGamepadPointer.OwnsPosition)
                lOMousePos = UWGamepadPointer.Position;

            float lfClampedX = Mathf.Clamp(lOMousePos.x, gameAreaLeft, gameAreaRight);
            float lfClampedY = Mathf.Clamp(lOMousePos.y, gameAreaBottom, gameAreaTop);

            if (!Mathf.Approximately(lfClampedX, lOMousePos.x) || !Mathf.Approximately(lfClampedY, lOMousePos.y))
            {
                lOMousePos = new Vector2(lfClampedX, lfClampedY);
                Mouse.current.WarpCursorPosition(lOMousePos);
            }
        }

        PointerPosition = lOMousePos;

        // The border counts as inside: during a drag the pointer is clamped exactly onto it, and
        // with a strict check the zone was then not re-evaluated any more - at the top edge
        // (full walking speed) the movement stuck in the last zone and did not switch between
        // left/forward/right (per user, 2026-09-15).
        if (lOMousePos.x >= gameAreaLeft && lOMousePos.x <= gameAreaRight && lOMousePos.y >= gameAreaBottom && lOMousePos.y <= gameAreaTop)
        {
            LastCursorPositionInGameArea = lOMousePos;

            CursorWalkFactor = 0f;
            CursorRotateFactor = 0f;

            // Normalised to the viewport: relx 0 (left) to 1 (right), rely 0 (top) to 1 (bottom).
            // Y is flipped because Unity's mouse position starts at 0 at the bottom.
            //
            // THE ZONES FOLLOW UW.EXE (verified in the disassembly 2026-09-15): the view
            // registers its cursor regions in ovr134_6DB, the movement is decided in
            // seg034_2F89_4E, for a movement area of W = 172 by H = 113 pixels. With xm/ym the
            // position in that area (ym counted from the bottom):
            //   ym < H/5 (22):          strafe left / backward / strafe right by xm*3/W
            //                           (commands 9/8/0A from the table at DS:0768), constant
            //                           speeds, independent of the position;
            //   22 <= ym <= 45 (6H/15): turn left / centre / turn right, no walking;
            //   ym > 45:                slight left / forward / slight right, walking and turning.
            //   Left/right: xm < 57 (W*5/15), xm > 114.
            //   Turn = (57 - xm) * 384 / W resp. (xm - 114) * 384 / W, i.e. linear from the
            //   border (0) to the edge (1); walk = (ym - 45) * 192 / H, linear from the band
            //   border (0) to the top edge (1, about run-key speed).
            // Our area (gameArea*) is 176 x 121 and includes the frame border, therefore the
            // borders are applied as fractions.
            float relx = (lOMousePos.x - gameAreaLeft) / (gameAreaRight - gameAreaLeft);
            float rely = (gameAreaTop - lOMousePos.y) / (gameAreaTop - gameAreaBottom);

            CursorAreaX = Mathf.Clamp(Mathf.FloorToInt(relx * UWPlayerMotion.PointerAreaWidth), 0, UWPlayerMotion.PointerAreaWidth - 1);
            CursorAreaY = Mathf.Clamp(Mathf.FloorToInt((1f - rely) * UWPlayerMotion.PointerAreaHeight), 0, UWPlayerMotion.PointerAreaHeight - 1);

            const float lfLeftBorder = 57f / 172f;
            const float lfRightBorder = 115f / 172f;
            const float lfStrafeBorder = 1f - (22f / 113f);
            const float lfWalkBorder = 1f - (46f / 113f);

            if (rely > lfStrafeBorder)
            {
                // Bottom fifth: strafe sideways, backwards in the middle, at constant speed.
                if (relx < lfLeftBorder)
                    CursorMovement = UWCursors.CursorEnum.StrafeLeft;
                else if (relx >= lfRightBorder)
                    CursorMovement = UWCursors.CursorEnum.StrafeRight;
                else
                {
                    CursorMovement = UWCursors.CursorEnum.Backward;
                    CursorWalkFactor = 1f;
                }
            }
            else if (rely > lfWalkBorder)
            {
                // Middle band: pure turning, rest point in the middle.
                if (relx < lfLeftBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.TurnLeft;
                    CursorRotateFactor = Mathf.Clamp01((lfLeftBorder - relx) / lfLeftBorder);
                }
                else if (relx >= lfRightBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.TurnRight;
                    CursorRotateFactor = Mathf.Clamp01((relx - lfRightBorder) / (1f - lfRightBorder));
                }
                else
                    CursorMovement = UWCursors.CursorEnum.Center;
            }
            else
            {
                // Upper band: forwards, at the sides additionally with turning; walk and turn
                // factors are independent of each other, as in the original.
                CursorWalkFactor = Mathf.Clamp01((lfWalkBorder - rely) / lfWalkBorder);

                if (relx < lfLeftBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.SlightLeft;
                    CursorRotateFactor = Mathf.Clamp01((lfLeftBorder - relx) / lfLeftBorder);
                }
                else if (relx >= lfRightBorder)
                {
                    CursorMovement = UWCursors.CursorEnum.SlightRight;
                    CursorRotateFactor = Mathf.Clamp01((relx - lfRightBorder) / (1f - lfRightBorder));
                }
                else
                    CursorMovement = UWCursors.CursorEnum.Forward;
            }
        }
        else
        {
            if (mMovementRef != null && !mMovementRef.IsCursorMovementInProgress)
                CursorMovement = UWCursors.CursorEnum.Center;
        }
    }

    // ------------------------------------------------- Compass section (see UWHudCompass)

    private UWHudCompass mOHudCompass;

    internal UWHudCompass Compass
    {
        get { return mOHudCompass ?? (mOHudCompass = new UWHudCompass(this)); }
    }

    public void FreezeCompass(bool pbFrozen)
    {
        Compass.FreezeCompass(pbFrozen);
    }

    public bool IsScreenPositionOnCompass(Vector2 pOScreenPos)
    {
        return Compass.IsScreenPositionOnCompass(pOScreenPos);
    }

    /// <summary>The three arrows under the compass - see UWHudCompass.</summary>
    public bool TryGetEasyMovementCommand(Vector2 pOScreenPos, out int piCommand)
    {
        return Compass.TryGetEasyMovementCommand(pOScreenPos, out piCommand);
    }

    public void ReportStatus()
    {
        Compass.ReportStatus();
    }

    public void SetFrozenCompassStep(int piStep)
    {
        Compass.SetFrozenCompassStep(piStep);
    }

    /// <summary>The pixels of a game texture as Color32, flipped for Unity (bottom row first).</summary>
    /// <summary>An original picture as Unity pixels, rows turned upside down and the colour help
    /// applied. THE ONE place for it - UWCharacter used to carry a copy.</summary>
    internal static Color32[] fGetTextureInvert(UWTexture pOUWTexture)
    {
        UWColor32[] liTexture = pOUWTexture.GetUWColor32();

        Color32[] lOTex = new Color32[liTexture.Length];
        int k = 0;
        for (int i = pOUWTexture.Height - 1; i >= 0; i--)
        {
            for (int j = 0; j < pOUWTexture.Width; j++)
            {
                // These pictures are turned into colours right here instead of going through the
                // palette table, so the colour help has to be applied. Until 2026-09-19 only the
                // copy in UWCharacter did that: the flasks and the paperdoll followed the filter,
                // panels, compass, map and conversation did not (per user; found by the duplicate
                // audit, Todo 11.2 item 19).
                lOTex[k] = UWColourVision.Apply(new Color32(liTexture[i * pOUWTexture.Width + j].R,
                    liTexture[i * pOUWTexture.Width + j].G, liTexture[i * pOUWTexture.Width + j].B,
                    liTexture[i * pOUWTexture.Width + j].A));
                k++;
            }
        }
        return lOTex;
    }
    // ------------------------------------------------- Conversation section (see UWHudConversation)

    private UWHudConversation mOHudConversation;

    internal UWHudConversation Conversation
    {
        get { return mOHudConversation ?? (mOHudConversation = new UWHudConversation(this)); }
    }

    public void SetConversationFrame(bool pbOn)
    {
        Conversation.SetConversationFrame(pbOn);
    }
    // ------------------------------------------------- MessageLog section (see UWHudMessageLog)

    private UWHudMessageLog mOHudMessageLog;

    internal UWHudMessageLog MessageLog
    {
        get { return mOHudMessageLog ?? (mOHudMessageLog = new UWHudMessageLog(this)); }
    }

    public int LogLineCount
    {
        get { return MessageLog.LogLineCount; }
    }

    public System.Collections.Generic.List<string> WrapLogText(string psText)
    {
        return MessageLog.WrapLogText(psText);
    }
    // ------------------------------------------------- Options section (see UWHudOptions)

    private UWHudOptions mOHudOptions;

    /// <summary>The classic options panel is open - the menu bar shows itself then (UWSetupMenu).</summary>
    public bool IsOptionsOpen => mOHudOptions != null && mOHudOptions.IsOpen;

    internal UWHudOptions Options
    {
        get { return mOHudOptions ?? (mOHudOptions = new UWHudOptions(this)); }
    }

    // ------------------------------------------------- Command icons (see UWHudCommands)

    private UWHudCommands mOHudCommands;

    internal UWHudCommands Commands
    {
        get { return mOHudCommands ?? (mOHudCommands = new UWHudCommands(this)); }
    }

    public const string ProgressDots = UWHudOptions.ProgressDots;
    // ------------------------------------------------- Inspector fields of the Pictures section (see UWHudPictures)

    [Header("Window picture (original: CS400.N01)")]
    [SerializeField]
    [Tooltip("Fine offset of the window picture relative to the centred position in the viewport, " +
        "in original pixels (320x200). X positive = to the right, Y positive = down.")]
    internal Vector2 mOWindowPictureNudge = Vector2.zero;
    // ------------------------------------------------- Pictures section (see UWHudPictures)

    private UWHudPictures mOHudPictures;

    internal UWHudPictures Pictures
    {
        get { return mOHudPictures ?? (mOHudPictures = new UWHudPictures(this)); }
    }

    public void FlashWindowBlack(float pfSeconds)
    {
        Pictures.FlashWindowBlack(pfSeconds);
    }

    public void FlashWindowColour(int piPaletteIndex, float pfSeconds)
    {
        Pictures.FlashWindowColour(piPaletteIndex, pfSeconds);
    }

    public void HideWindowPicture()
    {
        Pictures.HideWindowPicture();
    }

    public bool IsWindowPictureVisible
    {
        get { return Pictures.IsWindowPictureVisible; }
    }

    public Sprite CurrentWindowPicture => Pictures.CurrentWindowPicture;

    public bool ShowGravePicture(int piFrame)
    {
        return Pictures.ShowGravePicture(piFrame);
    }

    public bool ShowScrollPicture()
    {
        return Pictures.ShowScrollPicture();
    }

    public bool ShowWindowPicture(int piLevelIndex)
    {
        return Pictures.ShowWindowPicture(piLevelIndex);
    }
    // ------------------------------------------------- Inspector fields of the Map section (see UWHudMap)

    /// <summary>Tints the CLOSE area so that it can be measured in. Only for
    /// seeing - should be switched off again as soon as the position is right.</summary>
    [SerializeField]
    [Tooltip("Shows the hit area of CLOSE on the map.")]
    internal bool mbShowMapCloseArea;
    // ------------------------------------------------- Map section (see UWHudMap)

    private UWHudMap mOHudMap;

    internal UWHudMap Map
    {
        get { return mOHudMap ?? (mOHudMap = new UWHudMap(this)); }
    }

    public int CurrentMapLevelIndex
    {
        get { return Map.CurrentMapLevelIndex; }
    }

    public void HideMap()
    {
        Map.HideMap();
    }

    public bool IsMapVisible
    {
        get { return Map.IsMapVisible; }
    }

    public bool ShowMap(int piLevelIndex)
    {
        return Map.ShowMap(piLevelIndex);
    }
    // ------------------------------------------------- Inspector fields of the Inventory section (see UWHudInventory)

    [SerializeField]
    [Tooltip("Where the carried load is shown, in original pixels from the top-left edge of the game frame.")]
    internal Vector2 mOWeightLabelPosition = new Vector2(305f, 62f);

    [SerializeField]
    [Tooltip("Colour of the numbers at the backpack slots. White in the original.")]
    internal Color mOCountColour = Color.white;

    [SerializeField]
    [Tooltip("Colour of the carrying capacity below the paper doll. A light orange in the original, #FFBE76.")]
    internal Color mOWeightColour = new Color(1f, 0.7451f, 0.4627f, 1f);
    // ------------------------------------------------- Inventory section (see UWHudInventory)

    private UWHudInventory mOHudInventory;

    internal UWHudInventory Inventory
    {
        get { return mOHudInventory ?? (mOHudInventory = new UWHudInventory(this)); }
    }

    public const int ContainerColumns = UWHudInventory.ContainerColumns;

    public UWArmorItemMap.BodySlot? GetArmorHitZoneUnderMouse(Vector2 pOScreenPos)
    {
        return Inventory.GetArmorHitZoneUnderMouse(pOScreenPos);
    }

    public UWObject GetBackpackItem(int piSlot)
    {
        return Inventory.GetBackpackItem(piSlot);
    }

    public int GetBackpackSlotUnderMouse(Vector2 pOScreenPos)
    {
        return Inventory.GetBackpackSlotUnderMouse(pOScreenPos);
    }

    public UWObject GetContainerItem(int piSlot)
    {
        return Inventory.GetContainerItem(piSlot);
    }

    public int GetContainerSlotUnderMouse(Vector2 pOScreenPos)
    {
        return Inventory.GetContainerSlotUnderMouse(pOScreenPos);
    }

    public bool IsContainerOpenIconUnderMouse(Vector2 pOScreenPos)
    {
        return Inventory.IsContainerOpenIconUnderMouse(pOScreenPos);
    }

    public bool IsContainerScrollDownUnderMouse(Vector2 pOScreenPos)
    {
        return Inventory.IsContainerScrollDownUnderMouse(pOScreenPos);
    }

    public bool IsContainerScrollUpUnderMouse(Vector2 pOScreenPos)
    {
        return Inventory.IsContainerScrollUpUnderMouse(pOScreenPos);
    }
    // ------------------------------------------------- Runes section (see UWHudRunes)

    private UWHudRunes mOHudRunes;

    internal UWHudRunes Runes
    {
        get { return mOHudRunes ?? (mOHudRunes = new UWHudRunes(this)); }
    }

    public bool CastSpellByClass(int piMajorClass, int piMinorClass)
    {
        return Runes.CastSpellByClass(piMajorClass, piMinorClass);
    }

    public bool CastSpellFromObject(int piSpellIndex, bool pbFireImmediately)
    {
        return Runes.CastSpellFromObject(piSpellIndex, pbFireImmediately);
    }

    public void ClearRuneShelf()
    {
        Runes.ClearRuneShelf();
    }

    public void RefreshSpellIcons()
    {
        Runes.RefreshSpellIcons();
    }

    public const int SelectedRuneCount = UWHudRunes.SelectedRuneCount;

    public System.Collections.Generic.IReadOnlyList<int> SelectedRunes
    {
        get { return Runes.SelectedRunes; }
    }
    // ------------------------------------------------- Inspector fields of the Panel section (see UWHudPanel)

    [SerializeField]
    [Tooltip("Duration of one rotation step in seconds.")]
    internal float mfPanelStepSeconds = 0.25f;

    [SerializeField]
    [Tooltip("First label row on the stats page, measured on the background picture.")]
    internal float mOStatsFirstRowY = 21f;

    [SerializeField]
    [Tooltip("Spacing of the label rows, measured on the background picture.")]
    internal float mOStatsRowHeight = 7f;

    [SerializeField]
    [Tooltip("Right edge of all values. A single one - see fSetStatsLine.")]
    internal float mOStatsValueRight = 76f;

    [SerializeField]
    [Tooltip("Right edge of the level in the header line.")]
    internal float mOStatsLevelRight = 76f;

    [SerializeField]
    [Tooltip("Left edge of class and labels.")]
    internal float mOStatsLabelLeft = 6f;

    /// <summary>
    /// Name and class are drawn by the game itself - the background picture has nothing
    /// at these spots, so they cannot be measured like the rest of the panel.
    ///
    /// Derived from UnderworldGodot: there the stats page is at exactly factor 4
    /// (332/83 wide, 456/114 tall), which lets its coordinates be computed back. The
    /// text boxes lie 4.5 pixels above the letters - read off at STR, whose height
    /// we measured ourselves at 21. With the same calculation all six
    /// labels come out at 21, 28, 35, 42, 49, 56, exactly our seven-pixel grid -
    /// which supports the conversion.
    ///
    /// The reference set these marks by hand, not disassembled. Class and level
    /// fell onto the grid at 13.5 and 14.0, but the name at 5.25 did not. Decided by the user's
    /// eye (2026-09-03): the name stands at 7 and thus also lies on
    /// the grid - the reference had placed it too high at this one spot.
    /// </summary>
    [SerializeField]
    [Tooltip("Height of the name, converted from the reference (factor 4).")]
    internal float mOStatsNameY = 7f;

    [SerializeField]
    [Tooltip("Height of class and level, converted from the reference (factor 4).")]
    internal float mOStatsClassY = 14f;

    [SerializeField]
    [Tooltip("Colour of the values on the stats page - black like the baked-in labels (per user, 2026-09-03).")]
    internal Color mOStatsColour = Color.black;

    [SerializeField]
    [Tooltip("Colour of the skill list - blue-grey, not black like the values above (per user, 2026-09-03).")]
    internal Color mOStatsSkillColour = new Color(0x62 / 255f, 0x64 / 255f, 0x7F / 255f, 1f);
    // ------------------------------------------------- Panel section (see UWHudPanel)

    private UWHudPanel mOHudPanel;

    internal UWHudPanel Panel
    {
        get { return mOHudPanel ?? (mOHudPanel = new UWHudPanel(this)); }
    }

    public void TogglePanelSide()
    {
        Panel.TogglePanelSide();
    }

    public void ToggleRunePanel()
    {
        Panel.ToggleRunePanel();
    }
}
