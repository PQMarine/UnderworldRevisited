using UnityEngine;
using UnityEngine.UI;
using UWDataImport.Presentation;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// THE CLASSIC HUD'S PARTS IN ONE OWNER (cleanup 5.18, 2026-10-23: out of UWGameUI, the code as it
/// was): the sections of the original's screen - UWHudInventory, UWHudMessageLog, UWHudPanel,
/// UWHudRunes, UWHudMap, UWHudPictures, UWHudCompass, UWHudOptions, UWHudConversation,
/// UWHudCommands, the map's pictures (UWAutomapPictures) and the HUD dragons (UWHudDragons) -
/// each created on first use through its accessor here, the paperdoll with its armour, the
/// weapon, the power gem and the flasks drawn directly, the frame's picture for the width
/// (Classic Wide) and the sprites these pictures share.
///
/// THE ORDER IS LOAD-BEARING and stays as UWGameUI had it: the parts are built in the order of
/// the drawing (BuildWeapon before the frame's background, BuildWindowPicture before the frame
/// picture, the paperdoll and the panels after it, the compass, the dragons, the options and the
/// command icons last, above everything), and updated in the order of UWGameUI.Update (the
/// weapon, the paperdoll, the inventory, the scroll, the panel, then the compass and the dragons
/// at the very end). The sections reach the shared services through UWGameUI (mOUi: game data,
/// frame, character, interaction, inventory, filter mode) as before, and one another through
/// UWGameUI's accessors, which forward here.
/// </summary>
public sealed class UWClassicHud
{
    public UWClassicHud(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    // Original pixel coordinates (reference resolution 320x200) for the canvas elements -
    // unscaled, the CanvasScaler takes care of that.
    private const float wLeftOffset = UWClassicLayout.Frame.WeaponLeft;
    private const float wTopOffset = UWClassicLayout.Frame.WeaponTop;
    // The paper doll: UWPaperdollLayout (cleanup 5.1, 2026-10-19), shared with the modern
    // character panel and the modern original page.
    private const float charLeftOffset = UWPaperdollLayout.BodyX;
    private const float charTopOffset = UWPaperdollLayout.BodyY;
    private const float helmetArmorLeftOffset = UWPaperdollLayout.HelmetX;
    private const float helmetArmorTopOffset = UWPaperdollLayout.HelmetY;
    private const float glovesArmorLeftOffset = UWPaperdollLayout.GlovesX;
    private const float glovesArmorTopOffset = UWPaperdollLayout.GlovesY;
    private const float chestArmorLeftOffset = UWPaperdollLayout.ChestX;
    private const float chestArmorTopOffset = UWPaperdollLayout.ChestY;
    private const float legsArmorLeftOffset = UWPaperdollLayout.LegsX;
    private const float legsArmorTopOffset = UWPaperdollLayout.LegsY;
    private const float bootsArmorLeftOffset = UWPaperdollLayout.BootsX;
    private const float bootsArmorTopOffset = UWPaperdollLayout.BootsY;
    private const float powerGemLeft = UWClassicLayout.PowerGem.Left;
    private const float powerGemTop = UWClassicLayout.PowerGem.Top;
    private const float healthFlaskLeft = UWClassicLayout.Flasks.HealthLeft;
    private const float manaFlaskLeft = UWClassicLayout.Flasks.ManaLeft;
    private const float flaskTop = UWClassicLayout.Flasks.Top;

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

    private RectTransform fFrame()
    {
        return mOUi.mGameFrame;
    }

    // ------------------------------------------------- Building, in the drawing order

    /// <summary>The weapon, first on the frame: behind the frame picture (so that in the
    /// original window it shines through its transparent centre, but is cut off at the edge by
    /// the frame) - UWGameUI.fBuildCanvas creates the frame's background right after.</summary>
    internal void BuildWeapon()
    {
        mWeaponImage = UWUiKit.CreateFrameImage(fFrame(), "Weapon", wLeftOffset, -wTopOffset, 1f, 1f, Color.white);

        // The widened frame moves the parts (UWClassicWide); the weapon with the hole's middle.
        UWClassicWide.Attach(fFrame());
        UWClassicWide.InHole((RectTransform)mWeaponImage.transform);
    }

    /// <summary>The window picture, after the background and before the frame picture.</summary>
    internal void BuildWindowPicture()
    {
        Pictures.Build();

        if (Pictures.mWindowPictureImage != null)
            UWClassicWide.InHole((RectTransform)Pictures.mWindowPictureImage.transform);
    }

    /// <summary>The paper doll with its armour, the gem, the flasks, the backpack, the container
    /// view, the equipment slots and the message scroll - above the frame picture.</summary>
    internal void BuildPaperdollAndPanels()
    {
        mCharBackdropImage = UWUiKit.CreateFrameImage(fFrame(), "CharBackdrop", charLeftOffset, -charTopOffset, 1f, 1f, Color.black);
        mCharImage = UWUiKit.CreateFrameImage(fFrame(), "CharBody", charLeftOffset, -charTopOffset, 1f, 1f, Color.white);
        mHelmetImage = UWUiKit.CreateFrameImage(fFrame(), "Helmet", helmetArmorLeftOffset, -helmetArmorTopOffset, 1f, 1f, Color.white);
        mGlovesImage = UWUiKit.CreateFrameImage(fFrame(), "Gloves", glovesArmorLeftOffset, -glovesArmorTopOffset, 1f, 1f, Color.white);
        mLegsImage = UWUiKit.CreateFrameImage(fFrame(), "Legs", legsArmorLeftOffset, -legsArmorTopOffset, 1f, 1f, Color.white);
        mChestImage = UWUiKit.CreateFrameImage(fFrame(), "Chest", chestArmorLeftOffset, -chestArmorTopOffset, 1f, 1f, Color.white);
        mBootsImage = UWUiKit.CreateFrameImage(fFrame(), "Boots", bootsArmorLeftOffset, -bootsArmorTopOffset, 1f, 1f, Color.white);
        mPowerGemImage = UWUiKit.CreateFrameImage(fFrame(), "PowerGem", powerGemLeft, -powerGemTop, 1f, 1f, Color.white);
        mHealthFlaskImage = UWUiKit.CreateFrameImage(fFrame(), "HealthFlask", healthFlaskLeft, -flaskTop, 1f, 1f, Color.white);
        mManaFlaskImage = UWUiKit.CreateFrameImage(fFrame(), "ManaFlask", manaFlaskLeft, -flaskTop, 1f, 1f, Color.white);

        Inventory.BuildBackpackSlots();
        Inventory.BuildContainerView();
        Inventory.BuildEquipmentSlots();
        MessageLog.Build();
    }

    /// <summary>
    /// The parts built at runtime after the canvas (compass, dragons, options menu, command
    /// icons). Until 2026-09-15 the hierarchy came from a canvas prefab generated from
    /// fBuildCanvas; it was compared with the code-built canvas (identical apart from four
    /// exported original sprites, which Init now loads from the game data) and removed. The
    /// Find() re-wiring that dated from the prefab went on 2026-09-18: fBuildCanvas assigns
    /// every reference itself, and each section builds its own part (Inventory.BuildBackpackSlots,
    /// BuildContainerView and BuildEquipmentSlots, MessageLog.Build, Pictures.Build).
    /// </summary>
    internal void BuildAfterCanvas()
    {
        // BELOW the frame. For a short time it lay above, because in the original at the left dragon
        // a black outline appears that the frame itself does not have - that looked like
        // overdrawing. But it is not: laid above, thick black
        // bars appear (discarded immediately per user, 2026-08-29). The outline comes about differently,
        // entirely without overdrawing - the dragon reaches into the hole, and the black of the
        // picture fills the area right next to it there, where the 3D view otherwise is.
        Pictures.mWindowPictureImage.transform.SetSiblingIndex(mOUi.mMainFrameImage.transform.GetSiblingIndex());

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
        Dragons = new UWHudDragons(fFrame(), GetDragonSprite);

        // The same applies to the options panel: it consists of nine pictures that constantly
        // change, and therefore does not belong in the prefab.
        Options.Build();
        Commands.Build();
    }

    internal Image fCreateCenterPivotImage(string psName, float pfCenterX, float pfCenterTopY, float pfWidth, float pfHeight)
    {
        GameObject lOObj = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObj.transform.SetParent(fFrame(), false);

        RectTransform lORect = (RectTransform)lOObj.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0.5f, 0.5f);
        lORect.anchoredPosition = new Vector2(pfCenterX, -pfCenterTopY);
        lORect.sizeDelta = new Vector2(pfWidth, pfHeight);

        return lOObj.GetComponent<Image>();
    }

    /// <summary>Builds a scroll arrow placeholder square with pivot bottom-right (grows
    /// up and left from a right-aligned anchor line) - unlike UWUiKit.CreateFrameImage,
    /// which only knows the top-left convention.</summary>
    internal Image fCreateScrollArrowPlaceholder(string psName, float pfRightX)
    {
        GameObject lOObj = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObj.transform.SetParent(fFrame(), false);

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

    // ------------------------------------------------- The frame's parts, once a frame

    /// <summary>Every frame while the classic frame shows, after UWGameUI has laid out the frame
    /// and its bars: the weapon, the paper doll, the gem and the flasks, then the sections.</summary>
    internal void Update()
    {
        UWCharacter lOCharacter = mOUi.mCharacter;

        mWeaponImage.gameObject.SetActive(lOCharacter.Weapon.DrawWWeapon);

        if (lOCharacter.Weapon.DrawWWeapon)
        {
            WeaponCoordinate lOOffset = lOCharacter.Weapon.GetWeaponOffset();
            fUpdateSprite(mWeaponImage, lOCharacter.Weapon.CurrentWeaponTexture, ref mPrevWeaponTex);

            float lfY = -wTopOffset + lOOffset.Y - (lOCharacter.Weapon.CurrentWeaponTexture.height * (1f - lOCharacter.Weapon.CurrentReadyWeaponTime));

            ((RectTransform)mWeaponImage.transform).anchoredPosition = new Vector2(wLeftOffset + lOOffset.X + mOUi.WeaponSway.Offset(), lfY);
        }

        fUpdateSprite(mCharImage, lOCharacter.Paperdoll.CharTexture, ref mPrevCharTex);
        fMatchSize(mCharBackdropImage, mCharImage);
        fUpdateSprite(mHelmetImage, lOCharacter.Paperdoll.HelmetTexture, ref mPrevHelmetTex);
        fUpdateSprite(mGlovesImage, lOCharacter.Paperdoll.GlovesTexture, ref mPrevGlovesTex);
        fUpdateSprite(mChestImage, lOCharacter.Paperdoll.ChestArmorTexture, ref mPrevChestTex);
        fUpdateSprite(mLegsImage, lOCharacter.Paperdoll.LegsArmorTexture, ref mPrevLegsTex);
        fUpdateSprite(mBootsImage, lOCharacter.Paperdoll.BootsTexture, ref mPrevBootsTex);
        fUpdateSprite(mPowerGemImage, lOCharacter.Weapon.PowerGemTeture, ref mPrevPowerGemTex);
        fUpdateSprite(mHealthFlaskImage, lOCharacter.Flasks.HealthFlaskTeture, ref mPrevHealthFlaskTex);
        fUpdateSprite(mManaFlaskImage, lOCharacter.Flasks.ManaFlaskTeture, ref mPrevManaFlaskTex);

        Inventory.Update();
        MessageLog.Refresh();
        Panel.Update();
        Panel.fRefreshStatsIfChanged();

        // AT THE VERY END, after everything that makes up the frame. The compass is an
        // extra; if something goes wrong there, it should not drag down the paper doll, the equipment
        // and the backpack - those come later in the same pass.
        Compass.Update();
        fUpdateDragons();
    }

    // ------------------------------------------------- The dragons

    /// <summary>The HUD dragons' pictures and state machine (UWHudDragons), built after the canvas;
    /// the runes are put below them (UWHudRunes).</summary>
    internal UWHudDragons Dragons { get; private set; }

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
        if (Dragons == null)
            return;

        // In conversation the pictures stay: CONV.BYT has the same resting dragons pixel for pixel,
        // and without them the selected runes lay over the right dragon (per user, 2026-09-14).
        // They only stand still there.
        bool lbHidden = false;

        if (lbHidden != mbDragonsHidden)
        {
            mbDragonsHidden = lbHidden;
            Dragons.SetVisible(!lbHidden);
        }

        if (Conversation.mbConversationFrame || Map.IsMapVisible)
            return;

        UWCharacter lOCharacter = mOUi.mCharacter;

        if (lOCharacter != null)
        {
            int liDamage = lOCharacter.Vitals.TakeDamageThisTick();

            if (liDamage > 0 && ((liDamage * 4) > lOCharacter.Vitals.MaxHP || lOCharacter.Vitals.CurrentHP < CoverVitality))
                UWHudDragons.Request(UWHudDragons.CoverAnimation);
        }

        Dragons.Tick();
    }

    /// <summary>Below this vitality any damage makes a dragon cover its eyes.</summary>
    private const int CoverVitality = 16;

    /// <summary>A dragon picture from DRAGONS.GR as a sprite, kept per index - the classic frame's
    /// dragons and Classic Wide's (UWModernClassicFrame) share them.</summary>
    internal Sprite GetDragonSprite(int piIndex)
    {
        Sprite lOSprite;

        if (mODragonSprites.TryGetValue(piIndex, out lOSprite))
            return lOSprite;

        UWTexture lOSource = null;

        try
        {
            lOSource = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.DRAGONS, piIndex);
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

        Texture2D lOTexture = UWTextureFactory.FromTexture(lOSource, mOUi.TextureFilterMode, "UWGameUI dragon " + piIndex, TextureWrapMode.Clamp);

        lOSprite = Sprite.Create(lOTexture, new Rect(0f, 0f, lOTexture.width, lOTexture.height), new Vector2(0f, 1f), 1f);
        mODragonSprites[piIndex] = lOSprite;

        return lOSprite;
    }

    // ------------------------------------------------- Sprites

    /// <summary>Sets sprite and size anew only on an actual texture change, instead of
    /// allocating a new sprite on every call.</summary>
    private void fUpdateSprite(Image pOImage, Texture2D pOTexture, ref Texture2D pOCached)
    {
        if (pOTexture == null || pOTexture == pOCached)
            return;

        pOCached = pOTexture;
        pOImage.sprite = fSpriteOf(pOTexture);
        ((RectTransform)pOImage.transform).sizeDelta = new Vector2(pOTexture.width, pOTexture.height);
    }

    /// <summary>
    /// ONE SPRITE PER TEXTURE for the pictures fUpdateSprite shows (2026-10-19, found by cleanup
    /// step 5.4): the weapon's frames, the paperdoll, the gem and the flasks. Until then every
    /// change made a new sprite and none was ever destroyed - in combat one per weapon frame. The
    /// textures are their owners' caches' (UWWeaponView, UWPaperdollView, UWFlaskView), which
    /// destroy the ones they replace; a sprite whose texture is gone is destroyed on the next new
    /// one, and all of them with the interface.
    /// </summary>
    private readonly Dictionary<Texture2D, Sprite> mOSpritesByTexture = new Dictionary<Texture2D, Sprite>();

    private readonly List<Texture2D> mOGoneTextures = new List<Texture2D>();

    private Sprite fSpriteOf(Texture2D pOTexture)
    {
        if (mOSpritesByTexture.TryGetValue(pOTexture, out Sprite lOSprite) && lOSprite != null)
            return lOSprite;

        // Unity's own null test: a destroyed texture compares equal to null.
        foreach (KeyValuePair<Texture2D, Sprite> lOEntry in mOSpritesByTexture)
        {
            if (lOEntry.Key == null)
                mOGoneTextures.Add(lOEntry.Key);
        }

        foreach (Texture2D lOGone in mOGoneTextures)
        {
            if (mOSpritesByTexture[lOGone] != null)
                Object.Destroy(mOSpritesByTexture[lOGone]);

            mOSpritesByTexture.Remove(lOGone);
        }

        mOGoneTextures.Clear();

        lOSprite = UWGameUI.fToSprite(pOTexture);
        mOSpritesByTexture[pOTexture] = lOSprite;

        return lOSprite;
    }

    /// <summary>With the interface (UWGameUI.OnDestroy): the sprites go.</summary>
    internal void Release()
    {
        foreach (Sprite lOSprite in mOSpritesByTexture.Values)
        {
            if (lOSprite != null)
                Object.Destroy(lOSprite);
        }

        mOSpritesByTexture.Clear();
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
        UWTexture lOSource = mOUi.mOUWData.Textures.GetTextureByType(peType, piIndex);

        if (lOImage == null || lOSource == null)
            return;

        lOImage.sprite = UWGameUI.fToSprite(UWTextureFactory.FromTexture(lOSource, mOUi.TextureFilterMode, "UWGameUI." + peType + "_" + piIndex));
    }

    // ------------------------------------------------- The frame's picture for the width

    private int miWideExtraShown;

    private Sprite mOWideSprite;

    private Sprite mOMainSprite;

    /// <summary>The colour help the widened frame was uploaded with (UWColourVision.Version).</summary>
    private int miWideColourShown = -1;

    /// <summary>The widened frame's picture and the width it was built for: a change of the
    /// colour help only uploads it anew, the build itself is slow (millions of colour distances
    /// at a large Extra, CLEANUP-PLAN 8.4).</summary>
    private UWPicture mOWidePicture;

    private int miWidePictureExtra = -1;

    /// <summary>
    /// The frame picture for the width (UWClassicWide): the original's at 320, else MAIN.BYT
    /// widened by Extra columns (UWHudArt.BuildClassicWideFrame), built when the width changes.
    /// The conversation frame swaps the picture itself (UWHudConversation), so nothing is set
    /// while it shows, and after it the picture is set anew.
    ///
    /// THE COLOUR HELP (decision 6 of the cleanup plan, B30, per user 2026-10-19): the widened
    /// frame follows UWColourVision like every other picture and is uploaded anew when it
    /// changes. Until then it was the one picture of the classic scheme drawn without it, so with
    /// the filter on its frame and its panels did not match (the modern Classic+ frame,
    /// UWModernClassicFrame, always followed it). With the filter off nothing changes.
    /// </summary>
    internal void ApplyWideFrame()
    {
        if (mOUi.mMainFrameImage == null || mOUi.mOUWData == null || mOUi.mOMainTexture == null)
            return;

        if (Conversation.mbConversationFrame)
        {
            miWideExtraShown = -1;
            return;
        }

        int liExtra = UWClassicWide.Extra;

        if (liExtra == miWideExtraShown && (liExtra <= 0 || miWideColourShown == UWColourVision.Version))
            return;

        miWideExtraShown = liExtra;

        // The background behind the frame is a 320 x 200 picture: stretched over the widened
        // frame its hole would not fit the frame's, and the frame covers all but its own hole
        // (per user's screenshot, 2026-10-10: the view did not widen).
        if (mOUi.mBackgroundImage != null)
            mOUi.mBackgroundImage.enabled = liExtra <= 0;

        if (liExtra <= 0)
        {
            if (mOMainSprite == null)
                mOMainSprite = UWGameUI.fToSprite(mOUi.mOMainTexture);

            mOUi.mMainFrameImage.sprite = mOMainSprite;
            return;
        }

        if (mOWideSprite != null)
        {
            Object.Destroy(mOWideSprite.texture);
            Object.Destroy(mOWideSprite);
        }

        if (mOWidePicture == null || miWidePictureExtra != liExtra)
        {
            mOWidePicture = UWHudArt.BuildClassicWideFrame(mOUi.mOUWData.Textures, liExtra).Picture;
            miWidePictureExtra = liExtra;
        }

        // Rows turned bottom up, the transparent pixels keeping their colours as MAIN.BYT's do,
        // the colour help applied (see above).
        Texture2D lOTexture = UWTextureFactory.FromPictureKeepingColours(mOWidePicture, mOUi.TextureFilterMode, TextureWrapMode.Clamp,
            "UWGameUI classic wide " + liExtra);

        miWideColourShown = UWColourVision.Version;
        mOWideSprite = UWGameUI.fToSprite(lOTexture);
        mOUi.mMainFrameImage.sprite = mOWideSprite;
    }

    // ------------------------------------------------- Compass section (see UWHudCompass)

    private UWHudCompass mOHudCompass;

    internal UWHudCompass Compass
    {
        get { return mOHudCompass ?? (mOHudCompass = new UWHudCompass(mOUi)); }
    }

    // ------------------------------------------------- Conversation section (see UWHudConversation)

    private UWHudConversation mOHudConversation;

    internal UWHudConversation Conversation
    {
        get { return mOHudConversation ?? (mOHudConversation = new UWHudConversation(mOUi)); }
    }

    // ------------------------------------------------- MessageLog section (see UWHudMessageLog)

    private UWHudMessageLog mOHudMessageLog;

    internal UWHudMessageLog MessageLog
    {
        get { return mOHudMessageLog ?? (mOHudMessageLog = new UWHudMessageLog(mOUi)); }
    }

    // ------------------------------------------------- Options section (see UWHudOptions)

    private UWHudOptions mOHudOptions;

    /// <summary>The classic options panel is open - the menu bar shows itself then (UWSetupMenu).
    /// Asked without creating the panel.</summary>
    public bool IsOptionsOpen => mOHudOptions != null && mOHudOptions.IsOpen;

    internal UWHudOptions Options
    {
        get { return mOHudOptions ?? (mOHudOptions = new UWHudOptions(mOUi)); }
    }

    // ------------------------------------------------- Command icons (see UWHudCommands)

    private UWHudCommands mOHudCommands;

    internal UWHudCommands Commands
    {
        get { return mOHudCommands ?? (mOHudCommands = new UWHudCommands(mOUi)); }
    }

    // ------------------------------------------------- Pictures section (see UWHudPictures)

    private UWHudPictures mOHudPictures;

    internal UWHudPictures Pictures
    {
        get { return mOHudPictures ?? (mOHudPictures = new UWHudPictures(mOUi)); }
    }

    // ------------------------------------------------- Map section (see UWHudMap)

    private UWHudMap mOHudMap;

    internal UWHudMap Map
    {
        get { return mOHudMap ?? (mOHudMap = new UWHudMap(mOUi)); }
    }

    private UWAutomapPictures mOAutomapPictures;

    /// <summary>The map's pictures (tiles, parchment, notes) for the map, the help window's map tab
    /// and the modern minimap - the latter two no longer go through the classic map (cleanup
    /// 5.11, 2026-10-19).</summary>
    internal UWAutomapPictures AutomapPictures
    {
        get { return mOAutomapPictures ?? (mOAutomapPictures = new UWAutomapPictures(mOUi)); }
    }

    // ------------------------------------------------- Inventory section (see UWHudInventory)

    private UWHudInventory mOHudInventory;

    internal UWHudInventory Inventory
    {
        get { return mOHudInventory ?? (mOHudInventory = new UWHudInventory(mOUi)); }
    }

    // ------------------------------------------------- Runes section (see UWHudRunes)

    private UWHudRunes mOHudRunes;

    internal UWHudRunes Runes
    {
        get { return mOHudRunes ?? (mOHudRunes = new UWHudRunes(mOUi)); }
    }

    // ------------------------------------------------- Panel section (see UWHudPanel)

    private UWHudPanel mOHudPanel;

    internal UWHudPanel Panel
    {
        get { return mOHudPanel ?? (mOHudPanel = new UWHudPanel(mOUi)); }
    }
}
