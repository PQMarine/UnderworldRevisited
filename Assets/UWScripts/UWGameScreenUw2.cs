using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S CLASSIC SCREEN AROUND THE WALK (UW2 plan phase 3 step 1, 2026-10-29): its game
/// screen (BYT.ARK block 4, UWClassicLayoutUw2.Frame) over the 3D view, which renders into the
/// 208 x 128 window place_3d_view gives it at the original's angle - the first game's zoom, 74.7
/// degrees from top to bottom, about 91 across. The first game's classic HUD (UWGameUI and its
/// UWHud* parts) is built on the first game's numbers throughout and stays uninitialised in the
/// walk; this is the second game's own owner, which grows with the steps of phase 3.
///
/// As the first game's: the frame fitted whole into the window (UWUiFit), at the CRT's 4:3 when the
/// classic screen is shown that way (UWClassicScreen.HorizontalPixelFactor), black bars beside or
/// above and below it, the camera's aspect corrected by the VGA pixel's 5/6 (UWViewportCamera). In the
/// modern scheme the screen hides and the view fills the window at the scene camera's angle - both
/// with the Graphics menu's view angle when one is set (UWViewAngle).
///
/// PART B: the screen's furniture as the original draws it - the message scroll (paper 0x71, the
/// text in FONT_5X6P through the first game's scroll model, the rolled edges turning a frame each
/// time it scrolls), the compass (a face by heading and 3 and one of sixteen needles), the power gem
/// at rest, the gargoyle's eyes open, the command bar with the chosen command lit. UW2's pictures
/// are opaque except index 0 where the original draws them transparent: its palette's index 1, the
/// (0, 0, 4) the first game's art marks transparency with (UWPalette), is a colour here - the eyes'
/// sockets, the bar's shadows.
///
/// PART C: the second game's own pointer in place of the provisional system arrow - our captured
/// pointer (UWPointer) with CURSORS.GR's pictures at the frame's whole-number scale, picked by
/// mous_player's eight regions around the view (UWClassicLayoutUw2.PointerZones; the first game's
/// rule over the second game's window), and the left button's walk: a press begun in the view drives
/// by the pointer's pixel in its 208 x 128 (CursorAreaX/Y, UWPlayerInput), the pointer held in the view
/// meanwhile (UWPointer.ClampTo, mouse_constrain).
///
/// STEP 2 PART A: the character the walk came from (the save's PLAYER.DAT, UWPlayerRecordUw2) - the
/// flasks filled to the twelfths of vitality and mana (red, green when poisoned, blue), the right
/// panel's inventory page (PANELS.GR 0) with the paperdoll (BODIES.GR by sex and body, the armour of
/// the five slots from ARMOR_F or ARMOR_M by type and condition), the objects of shoulders, hands,
/// rings and backpack (OBJECTS.GR by id, a stack's count over it) and the weight left in stones. A walk
/// begun without a save has no character: the screen's own empty flasks and page show.
///
/// STEP 2 PART B: the chain between the flasks pulls the panel from the inventory to the statistics
/// page and back (pull_chain), sliding over twenty frames of an eighth of a second (do_panel_frame:
/// the old page out to the right, the new one in from the right, clipped to the panel, the chain's
/// four pictures on the way); the statistics page (PANELS.GR 2) with the header, the attributes,
/// vitality, mana, experience and six skill rows that scroll from the panel's bottom strip; a click
/// low on a flask says its value (flask_info).
///
/// STEP 2 PART C: over the view as do_fbuf_bms draws into its frame buffer - the first-person weapon
/// while the fight icon is chosen (drawn in three steps, ready, sheathed in three when it is left; the
/// kind from the hand, WEAP.DAT and WEAP.GR by hand and kind, coloured by WEAP.CM's set for the body),
/// and the gargoyle's teeth (3DWIN.GR 0), which hang into the view's first row on every frame.
///
/// STEP 3 PART A: the inventory is the first game's model (UWInventoryModel) loaded from the save's
/// workspace (UWInventoryWorkspace), its places checked by the second game's rules (IUWInventoryVariant).
/// On the inventory page a left click takes an object onto the pointer, or puts the one there into the
/// place - fitting, stacking, combining, swapping as the model does (DoInventoryMouse, RearrangeInventory);
/// an empty weapon hand toggles the fight; a right click looks (inv_look's name). The object on the
/// pointer is the pointer's picture (SetCursorObj).
///
/// STEP 4: the options panel (UWOptionsPanelUw2, UWClassicLayoutUw2.Options) from the bar's options icon
/// or the Ctrl shortcuts, over the right panel while the game stands: save and restore through the file
/// page with the descriptions in the scroll, music and sound, the detail level, quit.
/// </summary>
public sealed class UWGameScreenUw2 : MonoBehaviour
{
    /// <summary>The commands of the bar, by icon (button_to_mode: icon 0 fight, 1 use, 2 get, 3 talk, 4
    /// look; 5 the options, no command).</summary>
    public enum CommandEnum
    {
        None = -1,
        Fight = 0,
        Use = 1,
        Get = 2,
        Talk = 3,
        Look = 4,
        Options = 5
    }

    /// <summary>The command the bar has chosen; None is the default, a look (mous_in_3d).</summary>
    public static CommandEnum Command { get; private set; } = CommandEnum.None;

    /// <summary>The lines the scroll holds, the newest last - as many as the first game's
    /// Interaction.ActiveStrings keeps.</summary>
    private static readonly List<string> msMessages = new List<string>();

    public const int KeptMessages = 5;

    /// <summary>A line into the walk's message scroll.</summary>
    public static void Say(string psLine)
    {
        if (string.IsNullOrEmpty(psLine))
            return;

        msMessages.Add(psLine);

        while (msMessages.Count > KeptMessages)
            msMessages.RemoveAt(0);
    }

    private DataImport mOData;

    private Camera mOCamera;

    /// <summary>The scene camera's own vertical angle, the modern view's (UWViewportCamera's
    /// mfDefaultFov).</summary>
    private float mfModernFov;

    private GameObject mOCanvasRoot;

    private RectTransform mOFrame;

    private RectTransform mOLeftBar;

    private RectTransform mORightBar;

    private RectTransform mOTopBar;

    private RectTransform mOBottomBar;

    /// <summary>The textures made here, released with the screen.</summary>
    private readonly List<Texture2D> mOOwned = new List<Texture2D>();

    /// <summary>Whether the classic screen showed last frame.</summary>
    private bool mbShown;

    // The scroll.
    private readonly UWMessageLogModel mOLog = new UWMessageLogModel();

    private RawImage mOLogText;

    private Image mOEdgeLeft;

    private Image mOEdgeRight;

    private Sprite[] mOEdges;

    private Color32[] myLogColours;

    // The compass.
    private Image mOCompassFace;

    private Image mOCompassNeedle;

    private Sprite[] mOCompassPictures;

    private int miCompassStep = -1;

    // The command bar.
    private Image mOIconLit;

    private Sprite[] mOLitIcons;

    private CommandEnum meShownCommand = CommandEnum.None;

    // The character (step 2).
    private UWPlayerFile mOShownPlayer;

    private bool mbCharacterShown;

    private Image mOPanel;

    private Image mOBody;

    private Image[] mODisplays;

    private RawImage[] mOCounts;

    private RawImage mOWeight;

    private Image[] mOFlaskEmpty;

    private Image[][] mOFlaskSlices;

    private readonly Dictionary<int, Sprite> mOObjectPictures = new Dictionary<int, Sprite>();

    /// <summary>The walk's inventory (step 3), null without a character.</summary>
    private UWInventoryModel mOInventory;

    // The open bag (step 3 part C).
    private Image mOBagBack;

    private Image mOOpenBag;

    private Image mOUpArrow;

    private Image mODownArrow;

    /// <summary>A press on the inventory page waiting to become a click (use, look) or a drag (take).</summary>
    private bool mbInventoryPress;

    private int miInventoryPressHit;

    private bool mbInventoryPressRight;

    private Vector2 mOInventoryPressAt;

    /// <summary>How far the pointer moves, in screen pixels, before a held button takes (mouse_dragged).</summary>
    public const float DragToTakePixels = 6f;

    /// <summary>The walk's inventory for the view's buttons (UWWalkUw2View: taking from and putting into
    /// the world, step 3 part B).</summary>
    public UWInventoryModel Inventory => mOInventory;

    /// <summary>The workspace's records as read: what a link not yet resolved into contents points into.</summary>
    private List<UWObject> mOWorkspaceRecords;

    /// <summary>
    /// THE INVENTORY INTO THE WALK'S PLAYER.DAT before a save (step 3 part D): the object on the pointer
    /// into the pack's first free place first - the original keeps no held object in a save (UE
    /// docs/FORMATS.md "PLAYER.DAT": the flag is not in the file) -, then the places, the pack and what
    /// hangs off them, and the running weight (UWInventoryWorkspace.WriteInto). The runes and the shelf are
    /// in the record already. Null, or what failed.
    /// </summary>
    public string WriteInventoryInto(UWPlayerFile pOFile)
    {
        if (mOInventory == null || pOFile == null || pOFile != mOShownPlayer)
            return null;

        if (mOInventory.CursorItem != null && !mOInventory.DropCursorItemInFirstFreeBackpackSlot())
            Debug.LogWarning("[Walk] The object on the pointer is not saved: the pack is full.");

        UWLevel lOLevel = UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevel : null;

        return UWInventoryWorkspace.WriteInto(pOFile, mOInventory.GetSavegameEquipment(), mOInventory.Backpack,
            lOLevel != null ? lOLevel.Masterlist : null, mOWorkspaceRecords,
            mOData.CommonObjectProperties != null ? mOInventory.GetCarriedTenthStones(mOData.CommonObjectProperties) : UWPlayerRecordUw2.Weight(pOFile));
    }

    private bool mbInventoryDirty;

    /// <summary>The object on the pointer as a picture at the pointer's scale.</summary>
    private Texture2D mOCursorObject;

    private int miCursorObjectKey = -1;

    // The panel's pages and slide (step 2 part B).
    private RectTransform mOInventoryPage;

    private RectTransform mOStatisticsPage;

    // The rune page, the shelf and the active spells (step 3 part C2).
    private RectTransform mORunesPage;

    private Image[] mORuneImages;

    private Image[] mOShelfImages;

    private Image[] mOSpellImages;

    /// <summary>The shelf's runes (RUNE_NONE for an empty place) and how many there are (nrunes).</summary>
    private readonly int[] miShelf = { UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune };

    private int miShelfCount;

    private Image mOChain;

    /// <summary>The page shown (PANELS.GR's number: 0 the inventory, 2 the statistics) and the one a
    /// slide brings in.</summary>
    private int miPage;

    private int miTargetPage;

    /// <summary>The slide's frame, 0 for none (pbuf.frame).</summary>
    private int miSlideFrame;

    private float mfSlideTime;

    private RawImage mOStatName;

    private RawImage mOStatClass;

    private RawImage mOStatLevel;

    private RawImage[] mOStatAttributes;

    private RawImage mOStatVitality;

    private RawImage mOStatMana;

    private RawImage mOStatExperience;

    private RawImage[] mOSkillNames;

    private RawImage[] mOSkillValues;

    /// <summary>The first skill row shown (skill_top): 0 starts with the skill points.</summary>
    private int miSkillTop;

    // Over the view (step 2 part C).
    private Image mOWeapon;

    private enum WeaponStateEnum
    {
        Hidden,
        Drawing,
        Ready,
        Sheathing
    }

    private WeaponStateEnum meWeapon = WeaponStateEnum.Hidden;

    /// <summary>weap_frame: the step within drawing or sheathing.</summary>
    private int miWeaponFrame;

    private float mfWeaponTime;

    /// <summary>The kind and hand the weapon was drawn with (do_weapload's weapid and lefty).</summary>
    private int miWeaponKind = -1;

    private bool mbWeaponLefty;

    private readonly Dictionary<int, Sprite> mOWeaponPictures = new Dictionary<int, Sprite>();

    // The pointer.
    private Texture2D[] mOCursors;

    private Texture2D[] mOScaledCursors;

    private int miCursorScale;

    private UWCursors.CursorEnum meCursor = UWCursors.CursorEnum.Center;

    /// <summary>The screen of the running walk, null without one.</summary>
    public static UWGameScreenUw2 Current { get; private set; }

    /// <summary>The second game's classic screen is up: its pointer is ours, captured (UWPointer).</summary>
    public static bool IsClassicActive => Current != null && Current.mbShown;

    /// <summary>The pointer in the view's window in the original's pixels, x from the left and y from
    /// the BOTTOM (0 to 207, 0 to 127), as parse_playin reads it; kept while it is outside.</summary>
    public int CursorAreaX { get; private set; }

    public int CursorAreaY { get; private set; }

    /// <summary>The window the left button drives in: place_3d_view's 208 x 128.</summary>
    public const int CursorAreaWidth = UWClassicLayoutUw2.Frame.ViewWindowWidth;

    public const int CursorAreaHeight = UWClassicLayoutUw2.Frame.ViewWindowHeight;

    /// <summary>The screen on the camera the walk looks through, built from the game's data.</summary>
    public static UWGameScreenUw2 Attach(Camera pOCamera, DataImport pOData)
    {
        if (pOCamera == null || pOData == null)
            return null;

        UWGameScreenUw2 lOScreen = pOCamera.GetComponent<UWGameScreenUw2>();

        if (lOScreen == null)
        {
            lOScreen = pOCamera.gameObject.AddComponent<UWGameScreenUw2>();
            lOScreen.fInit(pOCamera, pOData);
        }

        return lOScreen;
    }

    private void fInit(Camera pOCamera, DataImport pOData)
    {
        mOData = pOData;
        mOCamera = pOCamera;
        mfModernFov = pOCamera.fieldOfView;

        // A new walk starts with an empty scroll and no command (the statics outlive the scene).
        msMessages.Clear();
        Command = CommandEnum.None;
        Current = this;

        if (msAfterStart != null)
        {
            Say(msAfterStart);
            msAfterStart = null;
        }

        fBuildCanvas();
    }

    // ------------------------------------------------- The options panel (step 4)

    /// <summary>The options panel's state (UWOptionsPanelUw2): its group, the lit button, the message.</summary>
    private readonly UWOptionsPanelUw2 mOOptions = new UWOptionsPanelUw2();

    private bool mbOptionsOpen;

    private RectTransform mOOptionsGroup;

    private Image mOOptionsBase;

    private Image mOOptionsTitle;

    private Image mOOptionsMessage;

    private Image mOOptionsLit;

    /// <summary>OPTBTNS.GR's pictures by index, opaque, and the parts of them shown, by picture, row and width.</summary>
    private readonly Dictionary<int, Texture2D> mOOptionsPictures = new Dictionary<int, Texture2D>();

    private readonly Dictionary<int, Sprite> mOOptionsSprites = new Dictionary<int, Sprite>();

    private Image mOCommandBar;

    private Sprite mOBarPlain;

    private Sprite mOBarOpen;

    /// <summary>A press on the panel followed until its buttons come up (better_mouse_release): the button
    /// lit before it and the one under the pointer.</summary>
    private bool mbOptionsPress;

    private int miOptionsPressOld;

    private int miOptionsPressAt;

    /// <summary>The slot whose description is typed (SaveGame's wdialog), 0 for none; the text, whether it
    /// is still the old one that the first key replaces, and when the typing began (the box's blink).</summary>
    private int miTypingSlot;

    private string msTyped = string.Empty;

    private bool mbTypedFresh;

    private float mfTypingStart;

    private UWTextEntry mOTypedEntry;

    /// <summary>The slot last saved or restored (last_sr_file_num), lit when the file page opens - kept
    /// over the scene's start a restore makes, as the original keeps it over RestoreGame.</summary>
    private static int msiLastSlot = 1;

    /// <summary>A line the next walk's screen says when it starts: "Restore Game Complete." after a restore.</summary>
    private static string msAfterStart;

    /// <summary>The input hold's name while the panel is up (the clock's is UWGameClock.OptionsHold).</summary>
    private const string OptionsInputHold = "uw2 options";

    /// <summary>Whether the options panel is up: the walk's buttons and doors wait (UWWalkUw2View).</summary>
    public static bool IsOptionsOpen => Current != null && Current.mbOptionsOpen;

    /// <summary>Empties the scroll (scroll_clear).</summary>
    public static void ClearScroll()
    {
        msMessages.Clear();
    }

    // ------------------------------------------------- Building

    /// <summary>The canvas as the first game's (UWGameUI.fBuildCanvas): bars on all four sides, the
    /// 320 x 200 frame centred between them, the screen's picture filling it, the furniture over it
    /// in the original's drawing order.</summary>
    private void fBuildCanvas()
    {
        mOCanvasRoot = UWUiKit.CreateClassicCanvas(transform, "UW2 Classic Canvas", UWUiSorting.Classic).gameObject;

        RectTransform lOCanvas = (RectTransform)mOCanvasRoot.transform;

        mOLeftBar = fCreateSideBar("BackdropLeft", lOCanvas, 0f);
        mORightBar = fCreateSideBar("BackdropRight", lOCanvas, 1f);
        mOTopBar = fCreateBar("BackdropTop", lOCanvas, 1f);
        mOBottomBar = fCreateBar("BackdropBottom", lOCanvas, 0f);

        GameObject lOFrameObject = new GameObject("GameFrame", typeof(RectTransform));
        lOFrameObject.transform.SetParent(lOCanvas, false);
        mOFrame = (RectTransform)lOFrameObject.transform;
        mOFrame.anchorMin = new Vector2(0.5f, 0.5f);
        mOFrame.anchorMax = new Vector2(0.5f, 0.5f);
        mOFrame.pivot = new Vector2(0.5f, 0.5f);
        mOFrame.sizeDelta = new Vector2(UWUiFit.FrameWidthUnits, UWUiFit.FrameHeight);
        mOFrame.anchoredPosition = Vector2.zero;

        UWTexture lOMain = mOData.Textures != null ? mOData.Textures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0) : null;
        Image lOScreen = UWUiKit.CreateFrameImage(mOFrame, "GameScreen", 0f, 0f, UWUiFit.FrameWidthUnits, UWUiFit.FrameHeight, Color.white);

        UWUiKit.Stretch(lOScreen.rectTransform);

        if (lOMain != null)
            lOScreen.sprite = fWholeSprite(fScreenWithView(lOMain));
        else
            Debug.LogWarning("[Walk] No game screen in BYT.ARK (block " + UWClassicLayoutUw2.Frame.GameScreenBlock + ").");

        fBuildViewOverlay();
        fBuildCharacter();
        fBuildScroll();
        fBuildCompass();
        fBuildCommands();
        fBuildOptions();

        // CURSORS.GR, drawn transparent: 0 the pointer, 1 to 8 the arrows of the view's regions.
        mOCursors = new Texture2D[(int)UWCursors.CursorEnum.SlightRight + 1];

        for (int liAt = 0; liAt < mOCursors.Length; liAt++)
            mOCursors[liAt] = fPicture(UWTexture.TextureTypes.CURSORS, liAt, true);
    }

    /// <summary>The weapon's place, then the teeth over it (do_fbuf_bms' order): 3DWIN.GR 0 at its frame
    /// buffer place, cut to the rows inside the view.</summary>
    private void fBuildViewOverlay()
    {
        mOWeapon = fImage("Weapon", 0f, 0f, 1f, 1f, null);

        Texture2D lOTeeth = fPicture(UWTexture.TextureTypes.THRREDWIN, 0, true);
        int liTop = UWClassicLayoutUw2.Frame.ViewWindowTop + UWClassicLayoutUw2.ViewOverlay.TopY - UWClassicLayoutUw2.ViewOverlay.TeethY;
        int liCut = Mathf.Max(0, UWClassicLayoutUw2.Frame.ViewWindowTop - liTop);

        if (lOTeeth != null && liCut < lOTeeth.height)
        {
            Sprite lOSprite = fSprite(lOTeeth, 0, liCut, lOTeeth.width, lOTeeth.height - liCut);

            fImage("ViewTeeth", UWClassicLayoutUw2.Frame.ViewWindowLeft + UWClassicLayoutUw2.ViewOverlay.TeethX, liTop + liCut,
                fWidth(lOSprite), fHeight(lOSprite), lOSprite);
        }
    }

    /// <summary>
    /// THE WEAPON (adjust_weapon, a step each eighth of a second): the fight icon draws it - steps 2, 1
    /// and 0 from 0x1B, then ready at 0x1B -, leaving the icon sheathes it - 0, 1 and 2, then hidden. The
    /// kind is taken while it is sheathed. A kind of none draws nothing.
    /// </summary>
    private void fUpdateWeapon()
    {
        bool lbFight = Command == CommandEnum.Fight;

        if (meWeapon == WeaponStateEnum.Hidden)
        {
            if (lbFight)
            {
                fTakeWeaponKind();
                meWeapon = WeaponStateEnum.Drawing;
                miWeaponFrame = UWClassicLayoutUw2.Weapon.DrawSteps;
                mfWeaponTime = UWClassicLayoutUw2.Weapon.StepSeconds;
            }
        }
        else if (meWeapon == WeaponStateEnum.Ready && !lbFight)
        {
            meWeapon = WeaponStateEnum.Sheathing;
            miWeaponFrame = -1;
            mfWeaponTime = UWClassicLayoutUw2.Weapon.StepSeconds;
        }

        mfWeaponTime += Time.unscaledDeltaTime;

        while (mfWeaponTime >= UWClassicLayoutUw2.Weapon.StepSeconds && (meWeapon == WeaponStateEnum.Drawing || meWeapon == WeaponStateEnum.Sheathing))
        {
            mfWeaponTime -= UWClassicLayoutUw2.Weapon.StepSeconds;

            if (meWeapon == WeaponStateEnum.Drawing && --miWeaponFrame < 0)
                meWeapon = WeaponStateEnum.Ready;
            else if (meWeapon == WeaponStateEnum.Sheathing && ++miWeaponFrame >= UWClassicLayoutUw2.Weapon.DrawSteps)
                meWeapon = WeaponStateEnum.Hidden;
        }

        if (meWeapon == WeaponStateEnum.Ready || meWeapon == WeaponStateEnum.Hidden)
            mfWeaponTime = 0f;

        fShowWeapon();
    }

    /// <summary>The kind in the hand (the game's PLAYDATA.C): a melee weapon by its skill, 0 to 2; the
    /// fist for an empty hand, anything not a weapon and a missile weapon of class 0 to 7; none for the
    /// other missile weapons.</summary>
    private void fTakeWeaponKind()
    {
        UWPlayerFile lOPlayer = mOShownPlayer;

        mbWeaponLefty = lOPlayer != null && UWPlayerRecordUw2.IsLeftHanded(lOPlayer);

        int liId = lOPlayer != null ? UWPlayerRecordUw2.SlotItemId(lOPlayer, UWClassicLayoutUw2.Weapon.RightHandSlot - (mbWeaponLefty ? 1 : 0)) : -1;

        miWeaponKind = UWClassicLayoutUw2.Weapon.FistKind;

        if (liId >= 0 && liId < 0x10)
        {
            if (mOData.ObjectClassProperties != null && mOData.ObjectClassProperties.TryGetMeleeWeapon(liId, out UWObjectClassProperties.MeleeWeapon lOWeapon))
            {
                miWeaponKind = Mathf.Clamp(lOWeapon.SkillType, UWClassicLayoutUw2.Weapon.FirstSkill, UWClassicLayoutUw2.Weapon.LastSkill)
                    - UWClassicLayoutUw2.Weapon.FirstSkill;
            }
        }
        else if (liId >= 0x10 && liId < 0x20 && (liId & 0xF) > 7)
        {
            miWeaponKind = -1;
        }
    }

    /// <summary>The step's picture at its place in the view (do_fbuf_bms: weap_x, and 127 - weap_y).</summary>
    private void fShowWeapon()
    {
        UWWeaponAnimations lOAnimations = mOData.WeaponAnimations;
        int liRecord = ((mbWeaponLefty ? 0 : 1) * UWClassicLayoutUw2.Weapon.KindsPerHand) + miWeaponKind;

        if (meWeapon == WeaponStateEnum.Hidden || miWeaponKind < 0 || lOAnimations == null || lOAnimations.Uw2Records == null
            || liRecord >= lOAnimations.Uw2Records.Count)
        {
            mOWeapon.enabled = false;

            return;
        }

        UWWeaponAnimations.Uw2Record lORecord = lOAnimations.Uw2Records[liRecord];
        int liStep = meWeapon == WeaponStateEnum.Ready ? UWClassicLayoutUw2.Weapon.ReadyStep
            : UWClassicLayoutUw2.Weapon.ReadyStep + Mathf.Max(0, miWeaponFrame);
        int liPicture = lORecord.Steps[liStep] & UWClassicLayoutUw2.Weapon.PictureMask;

        if (liPicture >= lORecord.PictureCount)
        {
            mOWeapon.enabled = false;

            return;
        }

        int liIndex = ((mbWeaponLefty ? 0 : 1) * UWClassicLayoutUw2.Weapon.PicturesPerHand) + (miWeaponKind * UWClassicLayoutUw2.Weapon.PicturesPerKind) + liPicture;

        fPlace(mOWeapon, fWeaponPicture(liIndex), UWClassicLayoutUw2.Frame.ViewWindowLeft + lORecord.X[liPicture],
            UWClassicLayoutUw2.Frame.ViewWindowTop + UWClassicLayoutUw2.ViewOverlay.TopY - lORecord.Y[liPicture]);
    }

    /// <summary>A WEAP.GR picture in WEAP.CM's colours for the body (load_weapcm): its sixteen indices
    /// through the set's palette 0 indices, 0 transparent.</summary>
    private Sprite fWeaponPicture(int piIndex)
    {
        int liSet = mOShownPlayer != null && UWPlayerRecordUw2.Body(mOShownPlayer) == UWClassicLayoutUw2.Weapon.SecondColourMapBody ? 1 : 0;
        int liKey = (liSet << 16) | piIndex;

        if (mOWeaponPictures.TryGetValue(liKey, out Sprite lOSprite))
            return lOSprite;

        UWTexture lOPicture = mOData.Textures != null ? mOData.Textures.GetTextureByType(UWTexture.TextureTypes.WEAPONS, piIndex) : null;
        byte[] lyMap = mOData.MiscDataFiles != null && mOData.MiscDataFiles.WeaponAttackPalettes != null
            ? mOData.MiscDataFiles.WeaponAttackPalettes.Get(liSet) : null;
        UWPalette lOPalette = mOData.Palettes != null ? mOData.Palettes.GetPalette(0) : null;

        if (lOPicture == null || lOPicture.PaletteIndices == null || lyMap == null || lOPalette == null || lOPicture.Width <= 0)
        {
            mOWeaponPictures[liKey] = null;

            return null;
        }

        int liWidth = lOPicture.Width;
        int liHeight = lOPicture.Height;
        byte[] lyRgba = new byte[liWidth * liHeight * UWUploadPixels.BytesPerPixel];

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            for (int liColumn = 0; liColumn < liWidth; liColumn++)
            {
                int liFrom = (liRow * liWidth) + liColumn;
                int liAux = liFrom < lOPicture.PaletteIndices.Length ? lOPicture.PaletteIndices[liFrom] & 0xF : 0;
                int liAt = ((((liHeight - 1 - liRow) * liWidth) + liColumn) * UWUploadPixels.BytesPerPixel);

                if (liAux == 0)
                    continue;

                UWColor32 lOColour = lOPalette.GetUWColor(lyMap[liAux]);

                lyRgba[liAt + 0] = lOColour.R;
                lyRgba[liAt + 1] = lOColour.G;
                lyRgba[liAt + 2] = lOColour.B;
                lyRgba[liAt + 3] = 255;
            }
        }

        lOSprite = fWholeSprite(fOwn(UWTextureFactory.Upload(lyRgba, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Clamp, "UW2 weapon " + piIndex)));
        mOWeaponPictures[liKey] = lOSprite;

        return lOSprite;
    }

    /// <summary>The flasks' pictures and the panel's page with its places, empty until a character
    /// shows (fUpdateCharacter).</summary>
    private void fBuildCharacter()
    {
        mOFlaskEmpty = new Image[2];
        mOFlaskSlices = new Image[2][];

        for (int liFlask = 0; liFlask < 2; liFlask++)
        {
            int liLeft = liFlask == 0 ? UWClassicLayoutUw2.Flasks.HealthLeft : UWClassicLayoutUw2.Flasks.ManaLeft;
            Sprite lOEmpty = fWholeSprite(fPicture(UWTexture.TextureTypes.FLASKS, UWClassicLayoutUw2.Flasks.Empty, false));

            mOFlaskEmpty[liFlask] = fImage("FlaskEmpty" + liFlask, liLeft, UWClassicLayoutUw2.Flasks.Top, fWidth(lOEmpty), fHeight(lOEmpty), lOEmpty);
            mOFlaskEmpty[liFlask].enabled = false;
            mOFlaskSlices[liFlask] = new Image[UWClassicLayoutUw2.Flasks.Levels];

            for (int liSlice = 0; liSlice < UWClassicLayoutUw2.Flasks.Levels; liSlice++)
            {
                mOFlaskSlices[liFlask][liSlice] = fImage("Flask" + liFlask + "Slice" + liSlice, liLeft,
                    UWUiFit.FrameHeight - 1 - UWClassicLayoutUw2.Flasks.LiquidY[liSlice + 1], 1f, 1f, null);
            }
        }

        // The panel: a window clipping its pages as they slide, the strip they leave in colour 1 under
        // them, each page a group placed in the frame's columns and rows.
        Sprite lOPage = fWholeSprite(fPicture(UWTexture.TextureTypes.PANELS, 0, false));
        RectTransform lOWindow = fCreateGroup(mOFrame, "PanelWindow", UWClassicLayoutUw2.Panel.Left, UWClassicLayoutUw2.Panel.Top,
            fWidth(lOPage), fHeight(lOPage));

        lOWindow.gameObject.AddComponent<RectMask2D>();
        UWUiKit.CreateFrameImage(lOWindow, "PanelStrip", 0f, 0f, fWidth(lOPage), fHeight(lOPage), fPaletteColour(UWClassicLayoutUw2.PanelSlide.StripColour));

        mOInventoryPage = fCreateGroup(lOWindow, "InventoryPage", -UWClassicLayoutUw2.Panel.Left, -UWClassicLayoutUw2.Panel.Top,
            UWUiFit.FrameWidthUnits, UWUiFit.FrameHeight);
        mOStatisticsPage = fCreateGroup(lOWindow, "StatisticsPage", -UWClassicLayoutUw2.Panel.Left, -UWClassicLayoutUw2.Panel.Top,
            UWUiFit.FrameWidthUnits, UWUiFit.FrameHeight);
        mOStatisticsPage.gameObject.SetActive(false);

        mOPanel = fImageIn(mOInventoryPage, "PanelPage", UWClassicLayoutUw2.Panel.Left, UWClassicLayoutUw2.Panel.Top, fWidth(lOPage), fHeight(lOPage), lOPage);
        mOBody = fImageIn(mOInventoryPage, "Body", fDisplayLeft(0), fDisplayTop(0), 1f, 1f, null);

        Sprite lOBagBack = fWholeSprite(fPicture(UWTexture.TextureTypes.INV, UWClassicLayoutUw2.Inventory.OpenBagBackPicture, false));

        mOBagBack = fImageIn(mOInventoryPage, "OpenBagBack", UWClassicLayoutUw2.Inventory.OpenBagBackLeft, UWClassicLayoutUw2.Inventory.OpenBagBackTop,
            fWidth(lOBagBack), fHeight(lOBagBack), lOBagBack);
        mOBagBack.enabled = false;

        mODisplays = new Image[UWClassicLayoutUw2.Inventory.LastItemDisplay + 1];
        mOCounts = new RawImage[mODisplays.Length];

        for (int liDisplay = 1; liDisplay < mODisplays.Length; liDisplay++)
            mODisplays[liDisplay] = fImageIn(mOInventoryPage, "Display" + liDisplay, fDisplayLeft(liDisplay), fDisplayTop(liDisplay), 1f, 1f, null);

        mOOpenBag = fImageIn(mOInventoryPage, "OpenBag", fDisplayLeft(UWClassicLayoutUw2.Inventory.OpenBagDisplay),
            fDisplayTop(UWClassicLayoutUw2.Inventory.OpenBagDisplay), 1f, 1f, null);
        mOUpArrow = fImageIn(mOInventoryPage, "UpArrow", fDisplayLeft(UWClassicLayoutUw2.Inventory.UpArrowDisplay),
            fDisplayTop(UWClassicLayoutUw2.Inventory.UpArrowDisplay), 1f, 1f, null);
        mODownArrow = fImageIn(mOInventoryPage, "DownArrow", fDisplayLeft(UWClassicLayoutUw2.Inventory.DownArrowDisplay),
            fDisplayTop(UWClassicLayoutUw2.Inventory.DownArrowDisplay), 1f, 1f, null);

        for (int liDisplay = UWClassicLayoutUw2.Inventory.FirstItemDisplay; liDisplay < mODisplays.Length; liDisplay++)
            mOCounts[liDisplay] = fTextImage(mOInventoryPage, "Count" + liDisplay, fDisplayLeft(liDisplay) + 3, fDisplayTop(liDisplay) + 1);

        mOWeight = fTextImage(mOInventoryPage, "WeightLeft", UWClassicLayoutUw2.Inventory.WeightCentreX, UWClassicLayoutUw2.Inventory.WeightTop);

        fBuildStatistics();
        fBuildRunes(lOWindow);

        mOChain = fImage("Chain", UWClassicLayoutUw2.PanelSlide.ChainLeft, UWClassicLayoutUw2.PanelSlide.ChainTop, 1f, 1f, null);
    }

    /// <summary>The rune page (PANELS.GR 1 and the 24 runes' places), the shelf's three places and the
    /// active spells' three on the frame.</summary>
    private void fBuildRunes(RectTransform pOWindow)
    {
        mORunesPage = fCreateGroup(pOWindow, "RunesPage", -UWClassicLayoutUw2.Panel.Left, -UWClassicLayoutUw2.Panel.Top,
            UWUiFit.FrameWidthUnits, UWUiFit.FrameHeight);
        mORunesPage.gameObject.SetActive(false);

        Sprite lOPage = fWholeSprite(fPicture(UWTexture.TextureTypes.PANELS, UWClassicLayoutUw2.Runes.PanelPage, false));

        fImageIn(mORunesPage, "RunesPicture", UWClassicLayoutUw2.Panel.Left, UWClassicLayoutUw2.Panel.Top, fWidth(lOPage), fHeight(lOPage), lOPage);
        mORuneImages = new Image[UWPlayerRecordUw2.RuneCount];

        for (int liRune = 0; liRune < mORuneImages.Length; liRune++)
        {
            Sprite lORune = fCachedPicture(UWTexture.TextureTypes.OBJECTS, UWClassicLayoutUw2.Runes.FirstRunestone + liRune);

            mORuneImages[liRune] = fImageIn(mORunesPage, "Rune" + liRune, UWClassicLayoutUw2.Runes.Left + ((liRune & 3) * UWClassicLayoutUw2.Runes.StepX),
                fRow(UWClassicLayoutUw2.Runes.TopY - ((liRune >> 2) * UWClassicLayoutUw2.Runes.StepY)), fWidth(lORune), fHeight(lORune), lORune);
            mORuneImages[liRune].enabled = false;
        }

        mOShelfImages = new Image[UWClassicLayoutUw2.Runes.ShelfX.Length];
        mOSpellImages = new Image[UWClassicLayoutUw2.Runes.SpellX.Length];

        for (int liAt = 0; liAt < mOShelfImages.Length; liAt++)
            mOShelfImages[liAt] = fImage("Shelf" + liAt, UWClassicLayoutUw2.Runes.ShelfX[liAt], fRow(UWClassicLayoutUw2.Runes.ShelfY), 1f, 1f, null);

        for (int liAt = 0; liAt < mOSpellImages.Length; liAt++)
            mOSpellImages[liAt] = fImage("ActiveSpell" + liAt, UWClassicLayoutUw2.Runes.SpellX[liAt], fRow(UWClassicLayoutUw2.Runes.SpellY), 1f, 1f, null);
    }

    /// <summary>The runes in the bag, the shelf and the active spells (RedispRune, set_runes,
    /// active_spells).</summary>
    private void fShowRunes()
    {
        UWPlayerFile lOPlayer = mOShownPlayer;

        for (int liRune = 0; liRune < mORuneImages.Length; liRune++)
            mORuneImages[liRune].enabled = lOPlayer != null && UWPlayerRecordUw2.HasRune(lOPlayer, liRune) && mORuneImages[liRune].sprite != null;

        for (int liAt = 0; liAt < mOShelfImages.Length; liAt++)
        {
            int liRune = lOPlayer != null ? miShelf[liAt] : UWPlayerRecordUw2.NoRune;

            fPlace(mOShelfImages[liAt], liRune < UWPlayerRecordUw2.RuneCount
                ? fCachedPicture(UWTexture.TextureTypes.OBJECTS, UWClassicLayoutUw2.Runes.FirstRunestone + liRune) : null,
                UWClassicLayoutUw2.Runes.ShelfX[liAt], fRow(UWClassicLayoutUw2.Runes.ShelfY));
        }

        // parse_aspells: the class's first picture plus the subclass, for as many as are active.
        for (int liAt = 0; liAt < mOSpellImages.Length; liAt++)
        {
            int liPicture = UWClassicLayoutUw2.Runes.NoSpellPicture;

            if (lOPlayer != null && liAt < UWPlayerRecordUw2.ActiveSpellCount(lOPlayer))
            {
                int liWord = UWPlayerRecordUw2.ActiveSpell(lOPlayer, liAt);

                liPicture = UWClassicLayoutUw2.Runes.SpellClassFirst[liWord & 0xF] + ((liWord & 0xF0) >> 4);
            }

            fPlace(mOSpellImages[liAt], liPicture < UWClassicLayoutUw2.Runes.NoSpellPicture
                ? fCachedPicture(UWTexture.TextureTypes.SPELLS, liPicture) : null,
                UWClassicLayoutUw2.Runes.SpellX[liAt], fRow(UWClassicLayoutUw2.Runes.SpellY));
        }
    }

    /// <summary>A rune into the bag (add_rune: the runestone is gone, its bit set) - the model's hook
    /// for a runestone put on the rune bag.</summary>
    private bool fLearnRune(int piObjectId)
    {
        int liRune = piObjectId - UWClassicLayoutUw2.Runes.FirstRunestone;

        if (mOShownPlayer == null || liRune < 0 || liRune >= UWPlayerRecordUw2.RuneCount)
            return false;

        UWPlayerRecordUw2.SetRune(mOShownPlayer, liRune);
        fShowRunes();

        return true;
    }

    /// <summary>
    /// A click on the rune page (mous_in_rune): the bottom strip clears the shelf; on a rune the bag holds
    /// the left button puts it on the shelf (a fourth pushes the oldest off), the right looks at it.
    /// </summary>
    private void fClickRunes(int piX, int piY, bool pbRight)
    {
        int liRelX = piX - UWClassicLayoutUw2.Statistics.Left;
        int liRelY = piY - UWClassicLayoutUw2.Statistics.AreaBottomY;

        if (liRelX < 0 || piX > UWClassicLayoutUw2.Statistics.AreaRight || liRelY < 0 || mOShownPlayer == null)
            return;

        if (liRelY < UWClassicLayoutUw2.Runes.ClearStrip)
        {
            fSetShelf(new[] { UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune }, 0);

            return;
        }

        int liRune = UWClassicLayoutUw2.Runes.TopRowFirst - (((liRelY - UWClassicLayoutUw2.Runes.ClearStrip) / UWClassicLayoutUw2.Runes.StepY) << 2)
            + (liRelX / UWClassicLayoutUw2.Runes.StepX);

        if (liRune < 0 || liRune >= UWPlayerRecordUw2.RuneCount || !UWPlayerRecordUw2.HasRune(mOShownPlayer, liRune))
            return;

        if (pbRight)
        {
            int liId = UWClassicLayoutUw2.Runes.FirstRunestone + liRune;

            Say(UWItemDescriptions.DescribeScenery(UWText.RawObjectName(mOData, liId), null, new UWObject((ushort)liId), mOData));

            return;
        }

        int[] liShelf = (int[])miShelf.Clone();
        int liCount = miShelfCount;

        if (liCount == 3)
        {
            liShelf[0] = liShelf[1];
            liShelf[1] = liShelf[2];
            liCount--;
        }

        liShelf[liCount++] = liRune;
        fSetShelf(liShelf, liCount);
    }

    /// <summary>The shelf into the record (the save keeps it) and onto the screen.</summary>
    private void fSetShelf(int[] piRunes, int piCount)
    {
        System.Array.Copy(piRunes, miShelf, miShelf.Length);
        miShelfCount = piCount;

        if (mOShownPlayer != null)
            UWPlayerRecordUw2.WriteShelf(mOShownPlayer, miShelf, miShelfCount);

        fShowRunes();
    }

    /// <summary>The page group of a PANELS.GR page.</summary>
    private RectTransform fPageGroup(int piPage)
    {
        return piPage == 2 ? mOStatisticsPage : piPage == UWClassicLayoutUw2.Runes.PanelPage ? mORunesPage : mOInventoryPage;
    }

    /// <summary>The statistics page: PANELS.GR 2 and the places of its lines.</summary>
    private void fBuildStatistics()
    {
        Sprite lOPage = fWholeSprite(fPicture(UWTexture.TextureTypes.PANELS, 2, false));

        fImageIn(mOStatisticsPage, "StatisticsPicture", UWClassicLayoutUw2.Panel.Left, UWClassicLayoutUw2.Panel.Top, fWidth(lOPage), fHeight(lOPage), lOPage);

        mOStatName = fTextImage(mOStatisticsPage, "StatName", UWClassicLayoutUw2.Statistics.Left, fRow(UWClassicLayoutUw2.Statistics.NameY));
        mOStatClass = fTextImage(mOStatisticsPage, "StatClass", UWClassicLayoutUw2.Statistics.Left, fRow(UWClassicLayoutUw2.Statistics.ClassY));
        mOStatLevel = fTextImage(mOStatisticsPage, "StatLevel", UWClassicLayoutUw2.Statistics.Right, fRow(UWClassicLayoutUw2.Statistics.ClassY));
        mOStatAttributes = new RawImage[3];

        for (int liAt = 0; liAt < mOStatAttributes.Length; liAt++)
        {
            mOStatAttributes[liAt] = fTextImage(mOStatisticsPage, "StatAttribute" + liAt, UWClassicLayoutUw2.Statistics.Right,
                fRow(((2 - liAt) * UWClassicLayoutUw2.Statistics.RowStep) + UWClassicLayoutUw2.Statistics.AttributeBaseY));
        }

        mOStatVitality = fTextImage(mOStatisticsPage, "StatVitality", UWClassicLayoutUw2.Statistics.Right, fRow(UWClassicLayoutUw2.Statistics.VitalityY));
        mOStatMana = fTextImage(mOStatisticsPage, "StatMana", UWClassicLayoutUw2.Statistics.Right, fRow(UWClassicLayoutUw2.Statistics.ManaY));
        mOStatExperience = fTextImage(mOStatisticsPage, "StatExperience", UWClassicLayoutUw2.Statistics.Right, fRow(UWClassicLayoutUw2.Statistics.ExperienceY));
        mOSkillNames = new RawImage[UWClassicLayoutUw2.Statistics.SkillRows];
        mOSkillValues = new RawImage[UWClassicLayoutUw2.Statistics.SkillRows];

        for (int liRow = 0; liRow < mOSkillNames.Length; liRow++)
        {
            float lfTop = fRow(UWClassicLayoutUw2.Statistics.SkillY - (liRow * UWClassicLayoutUw2.Statistics.RowStep));

            mOSkillNames[liRow] = fTextImage(mOStatisticsPage, "SkillName" + liRow, UWClassicLayoutUw2.Statistics.Left, lfTop);
            mOSkillValues[liRow] = fTextImage(mOStatisticsPage, "SkillValue" + liRow, UWClassicLayoutUw2.Statistics.Right, lfTop);
        }
    }

    /// <summary>A text's top row for the original's y counted up.</summary>
    private static float fRow(int piY) => UWUiFit.FrameHeight - 1 - piY;

    /// <summary>A group of images at a column and row (top down) of its parent, anchored at its top left.</summary>
    private static RectTransform fCreateGroup(RectTransform pOParent, string psName, float pfLeft, float pfTop, float pfWidth, float pfHeight)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(pfLeft, -pfTop);
        lORect.sizeDelta = new Vector2(pfWidth, pfHeight);

        return lORect;
    }

    /// <summary>A colour of palette 0, opaque.</summary>
    private Color fPaletteColour(int piIndex)
    {
        UWPalette lOPalette = mOData.Palettes != null ? mOData.Palettes.GetPalette(0) : null;
        UWColor32 lOColour = lOPalette != null ? lOPalette.GetUWColor(piIndex) : new UWColor32(0, 0, 0, 255);

        return new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
    }

    private static float fDisplayLeft(int piDisplay) => UWClassicLayoutUw2.Inventory.PictureX[piDisplay];

    /// <summary>A display's top row: its y counted up turned around.</summary>
    private static float fDisplayTop(int piDisplay) => UWUiFit.FrameHeight - 1 - UWClassicLayoutUw2.Inventory.PictureY[piDisplay];

    private RawImage fTextImage(RectTransform pOParent, string psName, float pfLeft, float pfTop)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(pfLeft, -pfTop);

        RawImage lOImage = lOObject.GetComponent<RawImage>();

        lOImage.raycastTarget = false;
        lOImage.enabled = false;

        return lOImage;
    }

    /// <summary>The character of the walk's save onto the screen when it changes (a walk loaded, saved).</summary>
    private void fUpdateCharacter()
    {
        UWGameSession lOSession = UWGameSession.Current;
        UWPlayerFile lOPlayer = lOSession != null ? lOSession.PlayerFileUw2 : null;

        if (lOPlayer == mOShownPlayer && mbCharacterShown == (lOPlayer != null))
        {
            if (mbInventoryDirty)
                fShowInventory();

            return;
        }

        mOShownPlayer = lOPlayer;
        mbCharacterShown = lOPlayer != null;
        mOInventory = null;

        if (lOPlayer != null)
        {
            UWInventoryWorkspace lOWorkspace = UWInventoryWorkspace.Read(lOPlayer);

            lOWorkspace.AssignPictures(mOData.Textures);
            mOWorkspaceRecords = lOWorkspace.Records;

            mOInventory = new UWInventoryModel(() => UWScene.LevelLoader != null ? UWScene.LevelLoader.UWDataImporter : null,
                () => UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevel : null);
            mOInventory.LoadFromSlots(UWPlayerRecordUw2.IsLeftHanded(lOPlayer), lOWorkspace.Equipment, lOWorkspace.Backpack,
                UWPlayerRecordUw2.Weight(lOPlayer));
            mOInventory.InventoryChanged += () => mbInventoryDirty = true;
            mOInventory.LearnRuneStone = fLearnRune;

            for (int liAt = 0; liAt < miShelf.Length; liAt++)
                miShelf[liAt] = UWPlayerRecordUw2.ShelfRune(lOPlayer, liAt);

            miShelfCount = UWPlayerRecordUw2.ShelfCount(lOPlayer);
        }

        fShowRunes();

        fShowFlask(0, lOPlayer != null ? UWPlayerRecordUw2.Vitality(lOPlayer) : 0, lOPlayer != null ? UWPlayerRecordUw2.MaxVitality(lOPlayer) : 0,
            lOPlayer != null && UWPlayerRecordUw2.IsPoisoned(lOPlayer) ? UWClassicLayoutUw2.Flasks.PoisonFirst : UWClassicLayoutUw2.Flasks.HealthFirst);
        fShowFlask(1, lOPlayer != null ? UWPlayerRecordUw2.Mana(lOPlayer) : 0, lOPlayer != null ? UWPlayerRecordUw2.MaxMana(lOPlayer) : 0,
            UWClassicLayoutUw2.Flasks.ManaFirst);

        mOPanel.enabled = mOPanel.sprite != null;

        // The paperdoll: BODIES.GR female * 5 + body (load_inventory_pix), then the armour.
        Sprite lOBody = lOPlayer != null
            ? fCachedPicture(UWTexture.TextureTypes.BODIES, (UWPlayerRecordUw2.IsFemale(lOPlayer) ? 5 : 0) + UWPlayerRecordUw2.Body(lOPlayer))
            : null;

        fPlace(mOBody, lOBody, fDisplayLeft(0), fDisplayTop(0));

        fShowInventory();
        fUpdateStatistics();
    }

    /// <summary>The object a display shows: DisplayToSlot's Inventory[] place in the model - the eleven of
    /// the paperdoll in the save's order, then the backpack's eight, or the open bag's contents from its
    /// first shown (Inventory[20] to [27]); display 20 the open bag itself.</summary>
    private UWObject fDisplayItem(int piDisplay)
    {
        if (mOInventory == null || piDisplay < 1)
            return null;

        if (piDisplay == UWClassicLayoutUw2.Inventory.OpenBagDisplay)
            return mOInventory.OpenContainer;

        if (piDisplay >= UWClassicLayoutUw2.Inventory.DisplayToSlot.Length)
            return null;

        int liSlot = UWClassicLayoutUw2.Inventory.DisplayToSlot[piDisplay];

        if (liSlot < UWInventoryWorkspace.EquipmentSlotCount)
            return mOInventory.EquipSlots[(int)UWInventoryModel.FromSavegameSlot(liSlot)];

        int liPack = liSlot - UWInventoryWorkspace.EquipmentSlotCount;

        if (mOInventory.OpenContainer != null)
            return mOInventory.GetContainerItem(liPack);

        return liPack < mOInventory.Backpack.Length ? mOInventory.Backpack[liPack] : null;
    }

    /// <summary>The paperdoll's armour, the objects with their counts and the weight left, from the model
    /// (DisplayInvSpecial, displayInventoryArray, displayEnc).</summary>
    private void fShowInventory()
    {
        mbInventoryDirty = false;

        UWPlayerFile lOPlayer = mOShownPlayer;

        for (int liDisplay = 1; liDisplay < mODisplays.Length; liDisplay++)
        {
            UWObject lOItem = fDisplayItem(liDisplay);
            Sprite lOPicture = null;

            if (lOItem != null && liDisplay < UWClassicLayoutUw2.Inventory.FirstItemDisplay)
            {
                // DisplayInvSpecial: the type and the condition, condition 3 above type 14.
                int liType = lOItem.ID & 0x1F;
                int liCondition = liType > UWClassicLayoutUw2.Inventory.LastConditionedType ? 3 : lOItem.Quality >> 4;

                lOPicture = fCachedPicture(UWPlayerRecordUw2.IsFemale(lOPlayer) ? UWTexture.TextureTypes.ARMOR_F : UWTexture.TextureTypes.ARMOR_M,
                    liType + (liCondition * UWClassicLayoutUw2.Inventory.ArmourPicturesPerCondition));
            }
            else if (lOItem != null)
            {
                lOPicture = fCachedPicture(UWTexture.TextureTypes.OBJECTS, lOItem.ID);
            }

            fPlace(mODisplays[liDisplay], lOPicture, fDisplayLeft(liDisplay), fDisplayTop(liDisplay));

            if (mOCounts[liDisplay] != null)
            {
                int liCount = lOItem != null ? UWStackRules.Count(lOItem) : 1;

                fSetText(mOCounts[liDisplay], UWFonts.FontType.Small, liCount > 1 ? liCount.ToString() : null,
                    UWClassicLayoutUw2.Inventory.CountColour, AlignEnum.Left, fDisplayLeft(liDisplay) + 3);
            }
        }

        // The open bag: INV.GR 6 over the backpack's rows, the bag's open picture on display 20, the
        // arrows while there is more to show either way.
        UWObject lOOpen = mOInventory != null ? mOInventory.OpenContainer : null;
        Sprite lOOpenPicture = null;

        // The open item's picture (OpenTheBag: the even class turns odd; UWObjectAppearance.OpenContainerPicture).
        if (lOOpen != null)
            lOOpenPicture = fCachedPicture(UWTexture.TextureTypes.OBJECTS, UWObjectIds.OpenPicture(lOOpen.ID));

        mOBagBack.enabled = lOOpen != null && mOBagBack.sprite != null;
        fPlace(mOOpenBag, lOOpenPicture, fDisplayLeft(UWClassicLayoutUw2.Inventory.OpenBagDisplay), fDisplayTop(UWClassicLayoutUw2.Inventory.OpenBagDisplay));
        fPlace(mOUpArrow, lOOpen != null && mOInventory.CanIncreaseContainerScrollOffset
                ? fCachedPicture(UWTexture.TextureTypes.BUTTONS, UWClassicLayoutUw2.Inventory.UpArrowPicture) : null,
            fDisplayLeft(UWClassicLayoutUw2.Inventory.UpArrowDisplay), fDisplayTop(UWClassicLayoutUw2.Inventory.UpArrowDisplay));
        fPlace(mODownArrow, lOOpen != null && mOInventory.CanDecreaseContainerScrollOffset
                ? fCachedPicture(UWTexture.TextureTypes.BUTTONS, UWClassicLayoutUw2.Inventory.DownArrowPicture) : null,
            fDisplayLeft(UWClassicLayoutUw2.Inventory.DownArrowDisplay), fDisplayTop(UWClassicLayoutUw2.Inventory.DownArrowDisplay));

        // The weight left: the most the character carries less the model's running total.
        int liCarried = mOInventory != null && mOData.CommonObjectProperties != null
            ? mOInventory.GetCarriedTenthStones(mOData.CommonObjectProperties) : 0;

        fSetText(mOWeight, UWFonts.FontType.Normal, lOPlayer != null ? ((UWPlayerRecordUw2.MaxWeight(lOPlayer) - liCarried) / 10).ToString() : null,
            UWClassicLayoutUw2.Inventory.WeightColour, AlignEnum.Centre, UWClassicLayoutUw2.Inventory.WeightCentreX);
    }

    /// <summary>The display under a point of the original (y counted up): the first InvDisplay rectangle
    /// holding it (FindInventoryHit), -1 for none.</summary>
    private static int fInventoryHit(int piX, int piY)
    {
        for (int liAt = 0; liAt < UWClassicLayoutUw2.Inventory.HitLeft.Length; liAt++)
        {
            if (UWClassicLayoutUw2.Inventory.HitLeft[liAt] <= piX && UWClassicLayoutUw2.Inventory.HitRight[liAt] >= piX
                && UWClassicLayoutUw2.Inventory.HitBottom[liAt] <= piY && UWClassicLayoutUw2.Inventory.HitTop[liAt] >= piY)
                return liAt;
        }

        return -1;
    }

    /// <summary>
    /// A press on the inventory page (DoInventoryMouse, mous_in_inv). With something on the pointer it goes
    /// into the place at once (RearrangeInventory: the model's fitting, stacking, combining, swapping; a bag
    /// there takes it; on the open bag's own place it goes back into the pack or the bag around). With
    /// nothing on it: an empty weapon hand toggles the fight, the open bag's place closes it, the arrows
    /// move its contents; on an object the press waits - dragged it takes the object (mouse_dragged), let go
    /// it is a click: the left button uses (a bag opens; the rest is the second game's rules), the right
    /// looks (inv_look). The Look icon makes the left button look too.
    /// </summary>
    private void fClickInventory(int piX, int piY, bool pbRight)
    {
        int liHit = fInventoryHit(piX, piY);

        if (liHit < 1 || liHit > UWClassicLayoutUw2.Inventory.DownArrowDisplay)
            return;

        UWObject lOItem = fDisplayItem(liHit);

        if (mOInventory.CursorItem != null)
        {
            fPutInto(liHit, lOItem);

            return;
        }

        if (liHit == UWClassicLayoutUw2.Inventory.OpenBagDisplay)
        {
            mOInventory.CloseContainer();

            return;
        }

        // An arrow moves the contents only while it is shown (ScrollItemsUp and ScrollItemsDown return
        // without InvUpArrow or InvDownArrow; per user, 2026-10-29: the hidden arrow's place scrolled on).
        if (liHit == UWClassicLayoutUw2.Inventory.UpArrowDisplay)
        {
            if (mOInventory.OpenContainer != null && mOInventory.CanIncreaseContainerScrollOffset)
                mOInventory.ScrollContainer(UWClassicLayoutUw2.Inventory.ScrollStep);

            return;
        }

        if (liHit == UWClassicLayoutUw2.Inventory.DownArrowDisplay)
        {
            if (mOInventory.OpenContainer != null && mOInventory.CanDecreaseContainerScrollOffset)
                mOInventory.ScrollContainer(-UWClassicLayoutUw2.Inventory.ScrollStep);

            return;
        }

        if (lOItem == null)
        {
            if (UWClassicLayoutUw2.Inventory.DisplayToSlot[liHit] == UWClassicLayoutUw2.Weapon.RightHandSlot - (mOInventory.IsLeftHanded ? 1 : 0))
                fChoose(CommandEnum.Fight);

            return;
        }

        mbInventoryPress = true;
        miInventoryPressHit = liHit;
        mbInventoryPressRight = pbRight;
        mOInventoryPressAt = UWPointer.Position;
    }

    /// <summary>The waiting press, each frame: dragged it takes the object, let go it uses or looks.</summary>
    private void fFollowInventoryPress()
    {
        if (!mbInventoryPress || mOInventory == null)
        {
            mbInventoryPress = false;

            return;
        }

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;
        bool lbHeld = lOMouse != null && (mbInventoryPressRight ? lOMouse.rightButton.isPressed : lOMouse.leftButton.isPressed);
        UWObject lOItem = fDisplayItem(miInventoryPressHit);

        if (lbHeld && (UWPointer.Position - mOInventoryPressAt).magnitude < DragToTakePixels)
            return;

        mbInventoryPress = false;

        if (lOItem == null)
            return;

        if (lbHeld)
        {
            fTakeFrom(miInventoryPressHit, lOItem);

            return;
        }

        if (mbInventoryPressRight || Command == CommandEnum.Look)
        {
            Say(UWItemDescriptions.DescribeScenery(UWText.RawObjectName(mOData, lOItem.ID), null, lOItem, mOData));

            return;
        }

        // The use (DoSpecialActions' default, UseObj): a bag opens in the pack's rows, the rune bag (class
        // 0xF) slides the panel to the runes (OpenTheBag).
        if (lOItem.ID == UWObjectIds.RuneBag)
        {
            if (miSlideFrame == 0 && miPage != UWClassicLayoutUw2.Runes.PanelPage)
                fStartSlide(UWClassicLayoutUw2.Runes.PanelPage);
        }
        else if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
            mOInventory.TryOpenContainer(lOItem);
        else
            Say(UWText.T("hud.walk.not_yet"));
    }

    /// <summary>The object of a display onto the pointer (SetCursorObj): from the paperdoll, the pack or the
    /// open bag. A bag that is open stays where it is.</summary>
    private void fTakeFrom(int piHit, UWObject pOItem)
    {
        if (mOInventory.IsContainerOpen(pOItem))
            return;

        int liSlot = UWClassicLayoutUw2.Inventory.DisplayToSlot[piHit];
        int liPack = liSlot - UWInventoryWorkspace.EquipmentSlotCount;

        if (liSlot < UWInventoryWorkspace.EquipmentSlotCount)
            mOInventory.BeginDragFromEquip(UWInventoryModel.FromSavegameSlot(liSlot));
        else if (mOInventory.OpenContainer != null)
            mOInventory.BeginDragFromContainer(liPack);
        else if (liPack < mOInventory.Backpack.Length)
            mOInventory.BeginDragFromBackpack(liPack);
    }

    /// <summary>The object on the pointer into a display's place (RearrangeInventory, PutObjectInBag): a
    /// bag there takes it; the open bag's own place gives it to the pack or to the bag around.</summary>
    private void fPutInto(int piHit, UWObject pOThere)
    {
        if (piHit == UWClassicLayoutUw2.Inventory.OpenBagDisplay)
        {
            UWObject lOParent = mOInventory.ParentContainer;

            if (mOInventory.OpenContainer == null)
                return;

            if (lOParent == null)
                mOInventory.DropCursorItemInFirstFreeBackpackSlot();
            else
                mOInventory.DropCursorItemInContainerAt(lOParent, lOParent.Contents != null ? lOParent.Contents.Count : 0);

            return;
        }

        if (piHit >= UWClassicLayoutUw2.Inventory.DisplayToSlot.Length)
            return;

        int liSlot = UWClassicLayoutUw2.Inventory.DisplayToSlot[piHit];
        int liPack = liSlot - UWInventoryWorkspace.EquipmentSlotCount;
        bool lbIntoBag = pOThere != null && pOThere.GetCategory() == UWObject.ObjectCategoryEnum.Containers && !mOInventory.IsContainerOpen(pOThere);

        if (liSlot < UWInventoryWorkspace.EquipmentSlotCount)
            mOInventory.DropCursorItemInEquip(UWInventoryModel.FromSavegameSlot(liSlot));
        else if (lbIntoBag)
            mOInventory.DropCursorItemIntoContainerItem(pOThere, mOInventory.OpenContainer != null);
        else if (mOInventory.OpenContainer != null)
            mOInventory.DropCursorItemInContainer(liPack);
        else if (liPack < mOInventory.Backpack.Length)
            mOInventory.DropCursorItemInBackpack(liPack);
    }

    /// <summary>The statistics page's lines (RedispStat): FONT_5X6I, the header and numbers in 0xC4, the
    /// skill rows in 0xC9.</summary>
    private void fUpdateStatistics()
    {
        UWPlayerFile lOPlayer = mOShownPlayer;
        int liHeader = UWClassicLayoutUw2.Statistics.HeaderColour;
        float lfRight = UWClassicLayoutUw2.Statistics.Right;
        UWFonts.FontType leFont = UWFonts.FontType.Italic;

        if (lOPlayer == null)
        {
            foreach (RawImage lOImage in mOStatisticsPage.GetComponentsInChildren<RawImage>(true))
                UWTextLabel.Hide(lOImage);

            return;
        }

        string lsName = lOPlayer.Name ?? string.Empty;

        if (lsName.Length > UWClassicLayoutUw2.Statistics.NameLength)
            lsName = lsName.Substring(0, UWClassicLayoutUw2.Statistics.NameLength);

        fSetText(mOStatName, leFont, lsName.ToUpperInvariant(), liHeader, AlignEnum.Centre,
            UWClassicLayoutUw2.Statistics.Left + (UWClassicLayoutUw2.Statistics.NameWidth / 2));
        fSetText(mOStatClass, leFont, fChargenString(UWClassicLayoutUw2.Statistics.FirstClassString + UWPlayerRecordUw2.CharacterClass(lOPlayer)),
            liHeader, AlignEnum.Left, UWClassicLayoutUw2.Statistics.Left);

        int liLevel = UWPlayerRecordUw2.CharacterLevel(lOPlayer);
        string lsLevel = liLevel == 1 ? UWText.F("hud.uw2.level_1", liLevel)
            : liLevel == 2 ? UWText.F("hud.uw2.level_2", liLevel)
            : liLevel == 3 ? UWText.F("hud.uw2.level_3", liLevel)
            : UWText.F("hud.uw2.level_n", liLevel);

        fSetText(mOStatLevel, leFont, lsLevel, liHeader, AlignEnum.Right, lfRight);
        fSetText(mOStatAttributes[0], leFont, UWPlayerRecordUw2.Strength(lOPlayer).ToString(), liHeader, AlignEnum.Right, lfRight);
        fSetText(mOStatAttributes[1], leFont, UWPlayerRecordUw2.Dexterity(lOPlayer).ToString(), liHeader, AlignEnum.Right, lfRight);
        fSetText(mOStatAttributes[2], leFont, UWPlayerRecordUw2.Intelligence(lOPlayer).ToString(), liHeader, AlignEnum.Right, lfRight);
        fSetText(mOStatVitality, leFont, UWPlayerRecordUw2.Vitality(lOPlayer) + "/" + UWPlayerRecordUw2.MaxVitality(lOPlayer), liHeader, AlignEnum.Right, lfRight);
        fSetText(mOStatMana, leFont, UWPlayerRecordUw2.Mana(lOPlayer) + "/" + UWPlayerRecordUw2.MaxMana(lOPlayer), liHeader, AlignEnum.Right, lfRight);
        fSetText(mOStatExperience, leFont, (UWPlayerRecordUw2.Experience(lOPlayer) / 10).ToString(), liHeader, AlignEnum.Right, lfRight);

        fUpdateSkillRows();
    }

    /// <summary>The six skill rows from skill_top (sp_skill): the skill points first at the top.</summary>
    private void fUpdateSkillRows()
    {
        if (mOShownPlayer == null)
            return;

        for (int liRow = 0; liRow < mOSkillNames.Length; liRow++)
        {
            int liSkill = liRow + miSkillTop - 1;
            bool lbPoints = liRow + miSkillTop == 0;
            int liString = UWClassicLayoutUw2.Statistics.FirstSkillString + (lbPoints ? UWClassicLayoutUw2.Statistics.SkillPointsRow : liSkill);
            int liValue = lbPoints ? UWPlayerRecordUw2.SkillPoints(mOShownPlayer) : UWPlayerRecordUw2.Skill(mOShownPlayer, liSkill);

            fSetText(mOSkillNames[liRow], UWFonts.FontType.Italic, fChargenString(liString), UWClassicLayoutUw2.Statistics.SkillColour,
                AlignEnum.Left, UWClassicLayoutUw2.Statistics.Left);
            fSetText(mOSkillValues[liRow], UWFonts.FontType.Italic, liValue.ToString(), UWClassicLayoutUw2.Statistics.SkillColour,
                AlignEnum.Right, UWClassicLayoutUw2.Statistics.Right);
        }
    }

    /// <summary>A string of block 2 by the original's number, upper-cased (seg039_3452_814); the port's
    /// strings count one higher.</summary>
    private string fChargenString(int piOriginalIndex)
    {
        return UWText.Block(mOData, 2, piOriginalIndex + 1, string.Empty).ToUpperInvariant();
    }

    /// <summary>A flask at its twelfths (set_flask): the empty flask, then a slice for each twelfth filled.</summary>
    private void fShowFlask(int piFlask, int piValue, int piMaximum, int piFirst)
    {
        int liLevel = piMaximum > 0 ? Mathf.Min(UWClassicLayoutUw2.Flasks.Levels, piValue * UWClassicLayoutUw2.Flasks.Levels / piMaximum) : 0;
        int liLeft = piFlask == 0 ? UWClassicLayoutUw2.Flasks.HealthLeft : UWClassicLayoutUw2.Flasks.ManaLeft;

        mOFlaskEmpty[piFlask].enabled = mOFlaskEmpty[piFlask].sprite != null && mOShownPlayer != null;

        for (int liSlice = 0; liSlice < UWClassicLayoutUw2.Flasks.Levels; liSlice++)
        {
            Sprite lOSlice = liSlice < liLevel ? fCachedPicture(UWTexture.TextureTypes.FLASKS, piFirst + liSlice) : null;

            fPlace(mOFlaskSlices[piFlask][liSlice], lOSlice, liLeft, UWUiFit.FrameHeight - 1 - UWClassicLayoutUw2.Flasks.LiquidY[liSlice + 1]);
        }
    }

    /// <summary>A picture once per kind and number: the flasks' slices opaque, the rest drawn transparent.</summary>
    private Sprite fCachedPicture(UWTexture.TextureTypes peType, int piIndex)
    {
        int liKey = ((int)peType << 16) | (piIndex & 0xFFFF);

        if (!mOObjectPictures.TryGetValue(liKey, out Sprite lOSprite))
        {
            lOSprite = fWholeSprite(fPicture(peType, piIndex, peType != UWTexture.TextureTypes.FLASKS));
            mOObjectPictures[liKey] = lOSprite;
        }

        return lOSprite;
    }

    private enum AlignEnum
    {
        Left,
        Centre,
        Right
    }

    /// <summary>A line in one of the original's fonts and a palette colour at a column - its left, its
    /// middle (x - width / 2) or its right end (x - width); hidden for no text.</summary>
    private void fSetText(RawImage pOImage, UWFonts.FontType peFont, string psText, int piColour, AlignEnum peAlign, float pfAt)
    {
        UWFont lOFont = mOData.Fonts != null ? mOData.Fonts.Get(peFont) : null;

        if (pOImage == null)
            return;

        if (lOFont == null || string.IsNullOrEmpty(psText))
        {
            UWTextLabel.Hide(pOImage);

            return;
        }

        UWPalette lOPalette = mOData.Palettes != null ? mOData.Palettes.GetPalette(0) : null;
        UWColor32 lOColour = lOPalette != null ? lOPalette.GetUWColor(piColour) : new UWColor32(255, 255, 255, 255);
        int liWidth = UWTextRaster.TightWidth(lOFont, psText);

        RectTransform lORect = (RectTransform)pOImage.transform;
        float lfLeft = peAlign == AlignEnum.Centre ? pfAt - (liWidth / 2) : peAlign == AlignEnum.Right ? pfAt - liWidth : pfAt;

        lORect.anchoredPosition = new Vector2(lfLeft, lORect.anchoredPosition.y);

        UWTextLabel.Get(pOImage).SetLine(lOFont, psText, liWidth, new Color32(lOColour.R, lOColour.G, lOColour.B, 255), false, FilterMode.Point);
        UWTextLabel.Get(pOImage).Show();
    }

    /// <summary>The paper, the text over it and the two rolled edges (SCRLEDGE.GR's first left and
    /// right frames, cut to the 28 rows draw_edges shows).</summary>
    private void fBuildScroll()
    {
        UWPalette lOPalette = mOData.Palettes != null ? mOData.Palettes.GetPalette(0) : null;
        Color lOPaper = Color.black;

        if (lOPalette != null)
        {
            UWColor32 lOColour = lOPalette.GetUWColor(UWClassicLayoutUw2.MessageLog.PaperColour);

            lOPaper = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
        }

        // The paper over the rows the screen leaves in index 1; its last row is the screen's own, paper
        // already, with the corners in a darker brown.
        UWUiKit.CreateFrameImage(mOFrame, "ScrollPaper", UWClassicLayoutUw2.MessageLog.Left, -UWClassicLayoutUw2.MessageLog.Top,
            UWClassicLayoutUw2.MessageLog.Width, UWClassicLayoutUw2.MessageLog.Height - 1, lOPaper);

        GameObject lOText = new GameObject("ScrollText", typeof(RectTransform), typeof(RawImage));
        lOText.transform.SetParent(mOFrame, false);

        RectTransform lORect = (RectTransform)lOText.transform;
        lORect.anchorMin = new Vector2(0f, 1f);
        lORect.anchorMax = new Vector2(0f, 1f);
        lORect.pivot = new Vector2(0f, 1f);
        lORect.anchoredPosition = new Vector2(UWClassicLayoutUw2.MessageLog.Left, -UWClassicLayoutUw2.MessageLog.Top);

        mOLogText = lOText.GetComponent<RawImage>();
        mOLogText.raycastTarget = false;
        mOLogText.enabled = false;

        mOEdges = new Sprite[UWClassicLayoutUw2.MessageLog.EdgeRightFirst + UWClassicLayoutUw2.MessageLog.EdgeFrames];

        for (int liAt = 0; liAt < mOEdges.Length; liAt++)
        {
            Texture2D lOEdge = fPicture(UWTexture.TextureTypes.SCRLEDGE, liAt, false);

            mOEdges[liAt] = lOEdge != null ? fSprite(lOEdge, 0, 0, lOEdge.width, Mathf.Min(lOEdge.height, UWClassicLayoutUw2.MessageLog.EdgeHeight)) : null;
        }

        mOEdgeLeft = fImage("ScrollEdgeLeft", UWClassicLayoutUw2.MessageLog.EdgeLeft, UWClassicLayoutUw2.MessageLog.EdgeTop,
            UWClassicLayoutUw2.MessageLog.EdgeWidth, UWClassicLayoutUw2.MessageLog.EdgeHeight, null);
        mOEdgeRight = fImage("ScrollEdgeRight", UWClassicLayoutUw2.MessageLog.EdgeRight, UWClassicLayoutUw2.MessageLog.EdgeTop,
            UWClassicLayoutUw2.MessageLog.EdgeWidth, UWClassicLayoutUw2.MessageLog.EdgeHeight, null);
        fApplyEdges();
    }

    /// <summary>The compass's face and needle (opaque, as their sprites are drawn), the power gem
    /// (transparent) and the eyes (opaque) - each at its picture's own size.</summary>
    private void fBuildCompass()
    {
        mOCompassPictures = new Sprite[UWClassicLayoutUw2.Compass.FirstNeedle + UWClassicLayoutUw2.Compass.NeedleX.Length];

        for (int liAt = 0; liAt < mOCompassPictures.Length; liAt++)
            mOCompassPictures[liAt] = fWholeSprite(fPicture(UWTexture.TextureTypes.COMPASS, liAt, false));

        mOCompassFace = fImage("CompassFace", UWClassicLayoutUw2.Compass.Left, UWClassicLayoutUw2.Compass.Top, 1f, 1f, null);
        mOCompassNeedle = fImage("CompassNeedle", 0f, 0f, 1f, 1f, null);

        Sprite lOGem = fWholeSprite(fPicture(UWTexture.TextureTypes.POWER, 0, true));

        fImage("PowerGem", UWClassicLayoutUw2.PowerGem.Left, UWClassicLayoutUw2.PowerGem.Top, fWidth(lOGem), fHeight(lOGem), lOGem);

        Sprite lOEyes = fWholeSprite(fPicture(UWTexture.TextureTypes.EYES, 0, false));

        fImage("Eyes", UWClassicLayoutUw2.Eyes.Left, UWClassicLayoutUw2.Eyes.Top, fWidth(lOEyes), fHeight(lOEyes), lOEyes);
    }

    /// <summary>The bar plain (OPTB.GR 0) and one icon's rectangle of the lit bar (OPTB.GR 1) over it
    /// for the chosen command.</summary>
    private void fBuildCommands()
    {
        Sprite lOPlain = fWholeSprite(fPicture(UWTexture.TextureTypes.OPTB, 0, false));

        mOCommandBar = fImage("CommandBar", UWClassicLayoutUw2.Commands.Left, UWClassicLayoutUw2.Commands.Top, fWidth(lOPlain), fHeight(lOPlain), lOPlain);
        mOBarPlain = lOPlain;
        mOBarOpen = fWholeSprite(fPicture(UWTexture.TextureTypes.OPTBTNS, UWClassicLayoutUw2.Options.OpenBarPicture, false));

        Texture2D lOLit = fPicture(UWTexture.TextureTypes.OPTB, 1, false);

        mOLitIcons = new Sprite[(int)CommandEnum.Options + 1];

        for (int liIcon = 0; lOLit != null && liIcon < mOLitIcons.Length; liIcon++)
        {
            RectInt lORect = fIconRect(liIcon, lOLit.width, lOLit.height);

            mOLitIcons[liIcon] = lORect.width > 0 && lORect.height > 0 ? fSprite(lOLit, lORect.x, lORect.y, lORect.width, lORect.height) : null;
        }

        mOIconLit = fImage("CommandLit", 0f, 0f, 1f, 1f, null);
        mOIconLit.enabled = false;
    }

    /// <summary>An icon's rectangle in the bar's picture (top down): 26 x 15, the last column and row
    /// one pixel short where the 77 x 29 picture ends (new_IconSelect's urectangle).</summary>
    private static RectInt fIconRect(int piIcon, int piWidth, int piHeight)
    {
        int liX = (piIcon % 3) * UWClassicLayoutUw2.Commands.IconWidth;
        int liY = (piIcon / 3) * UWClassicLayoutUw2.Commands.IconHeight;

        return new RectInt(liX, liY, Mathf.Min(UWClassicLayoutUw2.Commands.IconWidth, piWidth - liX),
            Mathf.Min(UWClassicLayoutUw2.Commands.IconHeight, piHeight - liY));
    }

    // ------------------------------------------------- Pictures

    /// <summary>The game screen opaque, with the view's window cleared so the camera shows through it.
    /// The picture paints the window in index 0 and edges it in index 1; the view lies over rows 16 to
    /// 143, the frame's line under it stays.</summary>
    private Texture2D fScreenWithView(UWTexture pOMain)
    {
        byte[] lyRgba = UWUploadPixels.FromTexture(pOMain, UWColourVision.Filter);
        int liWidth = pOMain.Width;
        int liHeight = pOMain.Height;

        for (int liAt = 3; liAt < lyRgba.Length; liAt += UWUploadPixels.BytesPerPixel)
            lyRgba[liAt] = 255;

        for (int liRow = UWClassicLayoutUw2.Frame.ViewWindowTop;
            liRow < UWClassicLayoutUw2.Frame.ViewWindowTop + UWClassicLayoutUw2.Frame.ViewWindowHeight; liRow++)
        {
            // Rows bottom up in the upload.
            int liLine = (liHeight - 1 - liRow) * liWidth;

            for (int liColumn = UWClassicLayoutUw2.Frame.ViewWindowLeft;
                liColumn < UWClassicLayoutUw2.Frame.ViewWindowLeft + UWClassicLayoutUw2.Frame.ViewWindowWidth; liColumn++)
            {
                int liAt = (liLine + liColumn) * UWUploadPixels.BytesPerPixel;

                lyRgba[liAt + 0] = 0;
                lyRgba[liAt + 1] = 0;
                lyRgba[liAt + 2] = 0;
                lyRgba[liAt + 3] = 0;
            }
        }

        return fOwn(UWTextureFactory.Upload(lyRgba, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Clamp, "UW2 game screen"));
    }

    /// <summary>A picture of a .GR file in palette 0: opaque, or with index 0 cleared where the original
    /// draws it transparent (GRLIBL.ASM _5467: colour 0 is skipped).</summary>
    private Texture2D fPicture(UWTexture.TextureTypes peType, int piIndex, bool pbTransparent)
    {
        UWTexture lOPicture = mOData.Textures != null ? mOData.Textures.GetTextureByType(peType, piIndex) : null;

        if (lOPicture == null || lOPicture.PaletteIndices == null || lOPicture.Width <= 0 || lOPicture.Height <= 0)
            return null;

        byte[] lyRgba = UWUploadPixels.FromTexture(lOPicture, UWColourVision.Filter);
        byte[] lyIndices = lOPicture.PaletteIndices;
        int liWidth = lOPicture.Width;
        int liHeight = lOPicture.Height;

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            for (int liColumn = 0; liColumn < liWidth; liColumn++)
            {
                int liIndex = (liRow * liWidth) + liColumn;
                bool lbClear = pbTransparent && liIndex < lyIndices.Length && lyIndices[liIndex] == 0;

                lyRgba[((((liHeight - 1 - liRow) * liWidth) + liColumn) * UWUploadPixels.BytesPerPixel) + 3] = lbClear ? (byte)0 : (byte)255;
            }
        }

        return fOwn(UWTextureFactory.Upload(lyRgba, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Clamp,
            "UW2 " + peType + " " + piIndex));
    }

    private Texture2D fOwn(Texture2D pOTexture)
    {
        if (pOTexture != null)
            mOOwned.Add(pOTexture);

        return pOTexture;
    }

    private static Sprite fWholeSprite(Texture2D pOTexture)
    {
        return pOTexture != null ? fSprite(pOTexture, 0, 0, pOTexture.width, pOTexture.height) : null;
    }

    /// <summary>A part of a picture as a sprite, the rectangle counted from the picture's top.</summary>
    private static Sprite fSprite(Texture2D pOTexture, int piX, int piTop, int piWidth, int piHeight)
    {
        return Sprite.Create(pOTexture, new Rect(piX, pOTexture.height - piTop - piHeight, piWidth, piHeight), new Vector2(0.5f, 0.5f), 1f);
    }

    private static float fWidth(Sprite pOSprite)
    {
        return pOSprite != null ? pOSprite.rect.width : 1f;
    }

    private static float fHeight(Sprite pOSprite)
    {
        return pOSprite != null ? pOSprite.rect.height : 1f;
    }

    /// <summary>An image on the frame at the original's column and row (top down).</summary>
    private Image fImage(string psName, float pfLeft, float pfTop, float pfWidth, float pfHeight, Sprite pOSprite)
    {
        return fImageIn(mOFrame, psName, pfLeft, pfTop, pfWidth, pfHeight, pOSprite);
    }

    /// <summary>An image in a group at the group's column and row.</summary>
    private static Image fImageIn(RectTransform pOParent, string psName, float pfLeft, float pfTop, float pfWidth, float pfHeight, Sprite pOSprite)
    {
        Image lOImage = UWUiKit.CreateFrameImage(pOParent, psName, pfLeft, -pfTop, pfWidth, pfHeight, Color.white);

        lOImage.sprite = pOSprite;
        lOImage.enabled = pOSprite != null;

        return lOImage;
    }

    /// <summary>Places an image at a column and row with its sprite's own size.</summary>
    private static void fPlace(Image pOImage, Sprite pOSprite, float pfLeft, float pfTop)
    {
        pOImage.sprite = pOSprite;
        pOImage.enabled = pOSprite != null;

        if (pOSprite == null)
            return;

        UWUiKit.SetRect(pOImage.rectTransform, pfLeft, -pfTop, pOSprite.rect.width, pOSprite.rect.height);
    }

    private static RectTransform fCreateSideBar(string psName, RectTransform pOParent, float pfEdge)
    {
        RectTransform lORect = (RectTransform)UWUiKit.CreateFrameImage(pOParent, psName, 0f, 0f, 0f, 0f, Color.black).transform;

        lORect.anchorMin = new Vector2(pfEdge, 0f);
        lORect.anchorMax = new Vector2(pfEdge, 1f);
        lORect.pivot = new Vector2(pfEdge, 0.5f);
        lORect.anchoredPosition = Vector2.zero;
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;

        return lORect;
    }

    private static RectTransform fCreateBar(string psName, RectTransform pOParent, float pfEdge)
    {
        RectTransform lORect = (RectTransform)UWUiKit.CreateFrameImage(pOParent, psName, 0f, 0f, 0f, 0f, Color.black).transform;

        lORect.anchorMin = new Vector2(0f, pfEdge);
        lORect.anchorMax = new Vector2(1f, pfEdge);
        lORect.pivot = new Vector2(0.5f, pfEdge);
        lORect.anchoredPosition = Vector2.zero;
        lORect.sizeDelta = Vector2.zero;

        return lORect;
    }

    // ------------------------------------------------- Each frame

    /// <summary>The modern scheme hides the classic screen (UWGameUI.Update's rule).</summary>
    private static bool IsClassicShown => !UWControlScheme.Ui.Policy.ClassicScreenOnlyUnderMap;

    private void Update()
    {
        if (mOCanvasRoot == null || !IsClassicShown)
        {
            // The panel is the classic screen's: it goes with it.
            fCloseOptions();

            return;
        }

        if (mbOptionsOpen)
        {
            fOptionsInput();
            fUpdatePointer();

            return;
        }

        if (fOptionShortcuts())
            return;

        fChooseCommand();
        fFollowInventoryPress();
        fClickPanel();
        fSlide();
        fUpdatePointer();
    }

    /// <summary>
    /// THE FLASKS' AND THE PANEL'S CLICKS: the chain (flask_info's middle columns, above its bottom ten
    /// rows) slides the inventory page to the statistics and any other back (pull_chain, not while a
    /// slide runs); low on a flask its value goes to the scroll; the statistics page's bottom strip
    /// scrolls the skill list (mous_in_stat).
    /// </summary>
    private void fClickPanel()
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null || !(lOMouse.leftButton.wasPressedThisFrame || lOMouse.rightButton.wasPressedThisFrame)
            || !TryGetOriginalPoint(UWPointer.Position, out float lfColumn, out float lfRow))
            return;

        int liX = Mathf.FloorToInt(lfColumn);
        int liY = (int)UWUiFit.FrameHeight - 1 - Mathf.FloorToInt(lfRow);

        if (liX >= UWClassicLayoutUw2.FlaskClicks.Left && liX <= UWClassicLayoutUw2.FlaskClicks.Right
            && liY >= UWClassicLayoutUw2.FlaskClicks.BottomY && liY <= UWClassicLayoutUw2.FlaskClicks.TopY)
        {
            int liRelX = liX - UWClassicLayoutUw2.FlaskClicks.Left;
            int liRelY = liY - UWClassicLayoutUw2.FlaskClicks.BottomY;

            if (liRelX > UWClassicLayoutUw2.FlaskClicks.ChainFrom && liRelX < UWClassicLayoutUw2.FlaskClicks.ChainTo)
            {
                if (liRelY > UWClassicLayoutUw2.FlaskClicks.ChainAbove && miSlideFrame == 0)
                    fStartSlide(miPage == 0 ? 2 : 0);
            }
            else if (liRelY <= UWClassicLayoutUw2.FlaskClicks.InfoUpTo && mOShownPlayer != null)
            {
                fSayFlask(liRelX < UWClassicLayoutUw2.FlaskClicks.ManaFrom);
            }

            return;
        }

        // The cast area over the shelf (try_cast): casting is the second game's magic (phase 4).
        if (liX >= UWClassicLayoutUw2.Runes.CastLeft && liX <= UWClassicLayoutUw2.Runes.CastRight
            && liY >= UWClassicLayoutUw2.Runes.CastBottomY && liY <= UWClassicLayoutUw2.Runes.CastTopY)
        {
            if (mOShownPlayer != null && miShelfCount > 0)
                Say(UWText.T("hud.walk.not_yet"));

            return;
        }

        if (miPage == 0 && miSlideFrame == 0 && mOInventory != null)
        {
            fClickInventory(liX, liY, lOMouse.rightButton.wasPressedThisFrame);

            return;
        }

        if (miPage == UWClassicLayoutUw2.Runes.PanelPage && miSlideFrame == 0)
        {
            fClickRunes(liX, liY, lOMouse.rightButton.wasPressedThisFrame);

            return;
        }

        if (miPage == 2 && miSlideFrame == 0 && mOShownPlayer != null && liX >= UWClassicLayoutUw2.Statistics.Left
            && liX <= UWClassicLayoutUw2.Statistics.AreaRight && liY >= UWClassicLayoutUw2.Statistics.AreaBottomY
            && liY < UWClassicLayoutUw2.Statistics.AreaBottomY + UWClassicLayoutUw2.Statistics.ScrollStrip)
        {
            int liTop = miSkillTop + (liX - UWClassicLayoutUw2.Statistics.Left < UWClassicLayoutUw2.Statistics.ScrollSplit ? -1 : 1);

            if (liTop >= 0 && liTop <= UWClassicLayoutUw2.Statistics.LastSkillTop)
            {
                miSkillTop = liTop;
                fUpdateSkillRows();
            }
        }
    }

    /// <summary>A flask's value into the scroll (flask_info): the game's "Your current vitality is" or
    /// "...mana points are", now and most; the vitality after how poisoned the character is.</summary>
    private void fSayFlask(bool pbVitality)
    {
        UWPlayerFile lOPlayer = mOShownPlayer;

        if (pbVitality && UWPlayerRecordUw2.IsPoisoned(lOPlayer))
        {
            // "You are " "barely" .. "critically" " poisoned." (game strings 0x68, 0x61 + (poison - 1) / 3,
            // 0x69 in the original's numbers).
            Say(UWText.Block(mOData, 1, 0x68 + 1, string.Empty) + UWText.Block(mOData, 1, 0x61 + ((UWPlayerRecordUw2.Poison(lOPlayer) - 1) / 3) + 1, string.Empty)
                + UWText.Block(mOData, 1, 0x69 + 1, string.Empty));
        }

        string lsFrame = UWText.Block(mOData, 1, (pbVitality ? 0x66 : 0x67) + 1, string.Empty).TrimEnd();
        int liNow = pbVitality ? UWPlayerRecordUw2.Vitality(lOPlayer) : UWPlayerRecordUw2.Mana(lOPlayer);
        int liMost = pbVitality ? UWPlayerRecordUw2.MaxVitality(lOPlayer) : UWPlayerRecordUw2.MaxMana(lOPlayer);

        Say(UWText.F("hud.uw2.flask_value", lsFrame, liNow, liMost));
    }

    private void fStartSlide(int piPage)
    {
        miTargetPage = piPage;
        miSlideFrame = 0;
        mfSlideTime = UWClassicLayoutUw2.PanelSlide.FrameSeconds;

        if (piPage == 2)
            fUpdateStatistics();

        fSlideStep();
    }

    /// <summary>The slide's frames on the unscaled clock, one each eighth of a second.</summary>
    private void fSlide()
    {
        if (miSlideFrame == 0)
            return;

        mfSlideTime += Time.unscaledDeltaTime;

        while (miSlideFrame != 0 && mfSlideTime >= UWClassicLayoutUw2.PanelSlide.FrameSeconds)
            fSlideStep();
    }

    /// <summary>One frame of do_panel_frame: the old page right by eight columns a frame for ten frames,
    /// then the new one in from the right; the chain's pictures on frames 1, 4, 13 and 16.</summary>
    private void fSlideStep()
    {
        mfSlideTime -= UWClassicLayoutUw2.PanelSlide.FrameSeconds;
        miSlideFrame++;

        RectTransform lOOld = fPageGroup(miPage);
        RectTransform lONew = fPageGroup(miTargetPage);
        int liChain = System.Array.IndexOf(UWClassicLayoutUw2.PanelSlide.ChainFrames, miSlideFrame);

        if (liChain >= 0)
            fPlace(mOChain, fCachedPicture(UWTexture.TextureTypes.CHAINS, liChain), UWClassicLayoutUw2.PanelSlide.ChainLeft, UWClassicLayoutUw2.PanelSlide.ChainTop);

        if (miSlideFrame <= UWClassicLayoutUw2.PanelSlide.OutFrames)
        {
            fShowPage(lOOld, miSlideFrame * UWClassicLayoutUw2.PanelSlide.StepColumns);
            lONew.gameObject.SetActive(lONew == lOOld);
            return;
        }

        if (lOOld != lONew)
            lOOld.gameObject.SetActive(false);

        fShowPage(lONew, (UWClassicLayoutUw2.PanelSlide.Frames - miSlideFrame) * UWClassicLayoutUw2.PanelSlide.StepColumns);

        if (miSlideFrame >= UWClassicLayoutUw2.PanelSlide.Frames)
        {
            miPage = miTargetPage;
            miSlideFrame = 0;
            mfSlideTime = 0f;
        }
    }

    /// <summary>A page shown moved so many columns to the right of its place.</summary>
    private static void fShowPage(RectTransform pOPage, int piColumns)
    {
        pOPage.gameObject.SetActive(true);
        pOPage.anchoredPosition = new Vector2(-UWClassicLayoutUw2.Panel.Left + piColumns, UWClassicLayoutUw2.Panel.Top);
    }

    /// <summary>
    /// THE POINTER IN THE VIEW (mous_player, parse_playin): while the left button drives, the pointer is
    /// held in the window; inside it its pixel is kept for the walk and its picture is the region's -
    /// the bottom three fifteenths slide or back, up to six turn or stand, above that walk; the outer
    /// five fifteenths left and right, the rest the middle. Off the window the plain pointer, unless a
    /// walk holds the last arrow.
    /// </summary>
    private void fUpdatePointer()
    {
        if (mOCamera == null || mOCursors == null)
            return;

        Rect lOWindow = mOCamera.pixelRect;
        UWPlayerMovement lOMovement = UWScene.Player != null ? UWScene.Player.GetComponent<UWPlayerMovement>() : null;
        bool lbDriving = !mbOptionsOpen && lOMovement != null && lOMovement.Input != null && lOMovement.Input.IsCursorMovementInProgress;
        Vector2 lOAt = lbDriving ? UWPointer.ClampTo(lOWindow) : UWPointer.Position;
        UWCursors.CursorEnum leCursor = lbDriving ? meCursor : UWCursors.CursorEnum.Center;

        // The panel's pointer is the plain one everywhere (the view's regions are mous_player's, which the
        // options' loop does not call).
        if (!mbOptionsOpen && lOWindow.width > 0f && lOWindow.height > 0f && lOAt.x >= lOWindow.xMin && lOAt.x <= lOWindow.xMax
            && lOAt.y >= lOWindow.yMin && lOAt.y <= lOWindow.yMax)
        {
            int liX = Mathf.Clamp(Mathf.FloorToInt((lOAt.x - lOWindow.xMin) / lOWindow.width * CursorAreaWidth), 0, CursorAreaWidth - 1);
            int liY = Mathf.Clamp(Mathf.FloorToInt((lOAt.y - lOWindow.yMin) / lOWindow.height * CursorAreaHeight), 0, CursorAreaHeight - 1);

            CursorAreaX = liX;
            CursorAreaY = liY;
            leCursor = RegionCursor(liX, liY);
        }

        meCursor = leCursor;

        // An object on the pointer is the pointer (SetCursorObj).
        Texture2D lOPicture = mOInventory != null && mOInventory.CursorItem != null
            ? fCursorObjectPicture(mOInventory.CursorItem.ID)
            : fScaledCursor((int)leCursor);

        if (lOPicture != null)
            UWPointer.SetPicture(lOPicture, new Vector2(lOPicture.width * 0.5f, lOPicture.height * 0.5f));
    }

    /// <summary>An object's picture at the pointer's whole-number scale, kept while it stays the same.</summary>
    private Texture2D fCursorObjectPicture(int piId)
    {
        int liScale = Mathf.Max(1, Mathf.FloorToInt(UWUiFit.Scale));
        int liKey = (liScale << 16) | piId;

        if (miCursorObjectKey == liKey && mOCursorObject != null)
            return mOCursorObject;

        Sprite lOSprite = fCachedPicture(UWTexture.TextureTypes.OBJECTS, piId);

        if (mOCursorObject != null)
            Destroy(mOCursorObject);

        mOCursorObject = lOSprite != null ? fScaledCopy(lOSprite.texture, liScale, "UW2 pointer object " + piId) : null;
        miCursorObjectKey = liKey;

        return mOCursorObject;
    }

    /// <summary>A picture enlarged by a whole number, pixel for pixel.</summary>
    private static Texture2D fScaledCopy(Texture2D pOSource, int piScale, string psName)
    {
        int liWidth = pOSource.width * piScale;
        int liHeight = pOSource.height * piScale;
        Color32[] lyFrom = pOSource.GetPixels32();
        Color32[] lyTo = new Color32[liWidth * liHeight];

        for (int liY = 0; liY < liHeight; liY++)
        {
            for (int liX = 0; liX < liWidth; liX++)
                lyTo[(liY * liWidth) + liX] = lyFrom[((liY / piScale) * pOSource.width) + (liX / piScale)];
        }

        return UWTextureFactory.FromColours(lyTo, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Clamp, psName);
    }

    /// <summary>The picture of mous_player's region a pixel of the window lies in (y from the bottom).</summary>
    public static UWCursors.CursorEnum RegionCursor(int piX, int piY)
    {
        int liSide = CursorAreaWidth * UWClassicLayoutUw2.PointerZones.SideFifteenths / 15;
        bool lbLeft = piX < liSide;
        bool lbRight = piX >= CursorAreaWidth - liSide;

        if (piY < CursorAreaHeight * UWClassicLayoutUw2.PointerZones.BottomFifteenths / 15)
            return lbLeft ? UWCursors.CursorEnum.StrafeLeft : lbRight ? UWCursors.CursorEnum.StrafeRight : UWCursors.CursorEnum.Backward;

        if (piY < CursorAreaHeight * UWClassicLayoutUw2.PointerZones.MiddleFifteenths / 15)
            return lbLeft ? UWCursors.CursorEnum.TurnLeft : lbRight ? UWCursors.CursorEnum.TurnRight : UWCursors.CursorEnum.Center;

        return lbLeft ? UWCursors.CursorEnum.SlightLeft : lbRight ? UWCursors.CursorEnum.SlightRight : UWCursors.CursorEnum.Forward;
    }

    /// <summary>A pointer at the frame's whole-number scale (UWPointerCursor's rule for the classic
    /// scheme), rebuilt when the window's size changes it.</summary>
    private Texture2D fScaledCursor(int piIndex)
    {
        if (piIndex < 0 || piIndex >= mOCursors.Length || mOCursors[piIndex] == null)
            return null;

        int liScale = Mathf.Max(1, Mathf.FloorToInt(UWUiFit.Scale));

        if (mOScaledCursors == null || miCursorScale != liScale)
        {
            fReleaseScaledCursors();
            mOScaledCursors = new Texture2D[mOCursors.Length];
            miCursorScale = liScale;
        }

        if (mOScaledCursors[piIndex] != null)
            return mOScaledCursors[piIndex];

        Texture2D lOSource = mOCursors[piIndex];
        int liWidth = lOSource.width * liScale;
        int liHeight = lOSource.height * liScale;
        Color32[] lyFrom = lOSource.GetPixels32();
        Color32[] lyTo = new Color32[liWidth * liHeight];

        for (int liY = 0; liY < liHeight; liY++)
        {
            for (int liX = 0; liX < liWidth; liX++)
                lyTo[(liY * liWidth) + liX] = lyFrom[((liY / liScale) * lOSource.width) + (liX / liScale)];
        }

        mOScaledCursors[piIndex] = UWTextureFactory.FromColours(lyTo, liWidth, liHeight, FilterMode.Point, TextureWrapMode.Clamp,
            "UW2 pointer " + piIndex);

        return mOScaledCursors[piIndex];
    }

    private void fReleaseScaledCursors()
    {
        if (mOScaledCursors == null)
            return;

        foreach (Texture2D lOScaled in mOScaledCursors)
        {
            if (lOScaled != null)
                Destroy(lOScaled);
        }

        mOScaledCursors = null;
    }

    /// <summary>Whether a screen point lies in the view's window on the classic screen - where a press
    /// of the left button starts the walk (UWPlayerInput).</summary>
    public bool IsInViewWindow(Vector2 pOScreen)
    {
        return mbShown && mOCamera != null && mOCamera.pixelRect.Contains(pOScreen);
    }

    private void LateUpdate()
    {
        if (mOCamera == null || mOCanvasRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        bool lbShown = IsClassicShown;

        mOCanvasRoot.SetActive(lbShown);

        if (!lbShown)
        {
            // The classic pointer goes with the screen (the modern walk looks with the mouse).
            if (mbShown)
                UWPointer.SetPicture(null, Vector2.zero);

            mOCamera.rect = new Rect(0f, 0f, 1f, 1f);
            mOCamera.ResetAspect();
            mOCamera.fieldOfView = UWViewAngle.Vertical(mOCamera.aspect, mfModernFov);
            mbShown = false;

            return;
        }

        mbShown = true;

        float lfFactor = UWClassicScreen.HorizontalPixelFactor;
        float lfScale = UWUiFit.Scale;
        float lfSide = fSideUnits(lfFactor);
        float lfVertical = Mathf.Max(0f, (UWUiFit.CanvasHeight - UWUiFit.FrameHeight) / 2f);

        mOFrame.localScale = new Vector3(lfFactor, 1f, 1f);
        mOLeftBar.sizeDelta = new Vector2(lfSide, mOLeftBar.sizeDelta.y);
        mORightBar.sizeDelta = new Vector2(lfSide, mORightBar.sizeDelta.y);
        mOTopBar.sizeDelta = new Vector2(mOTopBar.sizeDelta.x, lfVertical);
        mOBottomBar.sizeDelta = new Vector2(mOBottomBar.sizeDelta.x, lfVertical);

        // The view's window in screen pixels (origin bottom left), as UWClassicGeometry places the
        // first game's hole.
        float lfLeft = (lfSide * lfScale) + (UWClassicLayoutUw2.Frame.ViewWindowLeft * lfScale * lfFactor);
        float lfBottom = UWUiFit.BottomMarginPixels
            + ((UWUiFit.FrameHeight - UWClassicLayoutUw2.Frame.ViewWindowTop - UWClassicLayoutUw2.Frame.ViewWindowHeight) * lfScale);
        float lfWidth = UWClassicLayoutUw2.Frame.ViewWindowWidth * lfScale * lfFactor;
        float lfHeight = UWClassicLayoutUw2.Frame.ViewWindowHeight * lfScale;

        mOCamera.rect = new Rect(lfLeft / Screen.width, lfBottom / Screen.height, lfWidth / Screen.width, lfHeight / Screen.height);

        // The aspect of the CRT's pixels, not the window's (UWViewportCamera.Apply).
        mOCamera.ResetAspect();

        float lfAspect = mOCamera.aspect * UWClassicScreen.VgaPixelAspectCorrection / lfFactor;

        mOCamera.aspect = lfAspect;
        mOCamera.fieldOfView = UWViewAngle.Vertical(lfAspect, OriginalVerticalAngle);

        fUpdateCharacter();
        fUpdateWeapon();
        fUpdateScroll();
        fUpdateCompass();
        fUpdateCommandIcon();
    }

    /// <summary>The bar beside the frame in canvas units.</summary>
    private static float fSideUnits(float pfFactor)
    {
        return Mathf.Max(0f, (UWUiFit.CanvasWidth - (UWUiFit.FrameWidthUnits * pfFactor)) / 2f);
    }

    /// <summary>The text, wrapped and scrolled by the first game's model; the edges turn a frame with
    /// every scroll (draw_edges).</summary>
    private void fUpdateScroll()
    {
        UWFont lOFont = mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (lOFont == null || mOLogText == null)
            return;

        int liCapacity = UWMessageLogModel.CapacityOf(lOFont, UWClassicLayoutUw2.MessageLog.Height);

        // The description's box blinks after the typed line (wdialog's cursor block).
        bool lbCursor = miTypingSlot != 0 && UWMessageLogModel.IsCursorOn(Time.unscaledTime - mfTypingStart);

        if (!mOLog.Update(lOFont, msMessages, UWClassicLayoutUw2.MessageLog.Width, liCapacity, false, lbCursor))
            return;

        if (mOLog.Scrolled > 0)
            fApplyEdges();

        UWTextLabel.Get(mOLogText).SetText(lOFont, mOLog.ShownLines, mOLog.Width, fLogColours(),
            UWTextRaster.ColoursEnum.Codes, false, FilterMode.Point, mOLog.CursorOn);
    }

    private void fApplyEdges()
    {
        if (mOEdges == null || mOEdgeLeft == null)
            return;

        int liFrame = ((mOLog.EdgeFrame % UWClassicLayoutUw2.MessageLog.EdgeFrames) + UWClassicLayoutUw2.MessageLog.EdgeFrames)
            % UWClassicLayoutUw2.MessageLog.EdgeFrames;

        mOEdgeLeft.sprite = mOEdges[UWClassicLayoutUw2.MessageLog.EdgeLeftFirst + liFrame];
        mOEdgeLeft.enabled = mOEdgeLeft.sprite != null;
        mOEdgeRight.sprite = mOEdges[UWClassicLayoutUw2.MessageLog.EdgeRightFirst + liFrame];
        mOEdgeRight.enabled = mOEdgeRight.sprite != null;
    }

    /// <summary>The scroll's colours by code from palette 0 (UWClassicLayoutUw2.MessageLog.ColourIndices).</summary>
    private Color32[] fLogColours()
    {
        if (myLogColours != null)
            return myLogColours;

        int[] liIndices = UWClassicLayoutUw2.MessageLog.ColourIndices;
        UWPalette lOPalette = mOData.Palettes != null ? mOData.Palettes.GetPalette(0) : null;
        Color32[] lyColours = new Color32[liIndices.Length];

        for (int liAt = 0; liAt < liIndices.Length; liAt++)
        {
            UWColor32 lOColour = lOPalette != null ? lOPalette.GetUWColor(liIndices[liAt]) : new UWColor32(255, 255, 255, 255);

            lyColours[liAt] = new Color32(lOColour.R, lOColour.G, lOColour.B, 255);
        }

        if (lOPalette != null)
            myLogColours = lyColours;

        return lyColours;
    }

    /// <summary>The face by heading and 3, the needle by heading at nedl_x/nedl_y (set_compass) - the
    /// heading in sixteen steps as the first game's compass reads the view.</summary>
    private void fUpdateCompass()
    {
        Camera lOView = UWViewpoint.Current;

        if (lOView == null || mOCompassPictures == null)
            return;

        int liStep = UWHeadings.HeadingByteToCompassStep(UWHeadings.DegreesToHeadingByte(lOView.transform.eulerAngles.y));

        if (liStep == miCompassStep)
            return;

        miCompassStep = liStep;

        fPlace(mOCompassFace, mOCompassPictures[liStep & 3], UWClassicLayoutUw2.Compass.Left, UWClassicLayoutUw2.Compass.Top);
        fPlace(mOCompassNeedle, mOCompassPictures[UWClassicLayoutUw2.Compass.FirstNeedle + liStep],
            UWClassicLayoutUw2.Compass.NeedleBaseX + UWClassicLayoutUw2.Compass.NeedleX[liStep],
            UWUiFit.FrameHeight - 1 - (UWClassicLayoutUw2.Compass.NeedleBaseY + UWClassicLayoutUw2.Compass.NeedleY[liStep]));
    }

    private void fUpdateCommandIcon()
    {
        if (mbOptionsOpen || meShownCommand == Command || mOIconLit == null || mOLitIcons == null)
            return;

        meShownCommand = Command;

        int liIcon = (int)Command;

        if (liIcon < 0 || liIcon >= mOLitIcons.Length || mOLitIcons[liIcon] == null)
        {
            mOIconLit.enabled = false;

            return;
        }

        fPlace(mOIconLit, mOLitIcons[liIcon], UWClassicLayoutUw2.Commands.Left + ((liIcon % 3) * UWClassicLayoutUw2.Commands.IconWidth),
            UWClassicLayoutUw2.Commands.Top + ((liIcon / 3) * UWClassicLayoutUw2.Commands.IconHeight));
    }

    // ------------------------------------------------- The commands

    /// <summary>
    /// A click on the bar or a command's key chooses (deal_with_icons): the same command again takes
    /// it back to none, the options icon opens the options' game page (step 4). The keys are the
    /// first game's command actions by meaning; UW2.EXE's own F-keys (F1 use, F2 fight, F3 talk, F4
    /// look, F5 get, F6 options; game/PLAYER.C:241-246) come with the second game's key defaults.
    /// </summary>
    private void fChooseCommand()
    {
        UWControls lOControls = UWControlScheme.SharedControls;

        if (lOControls != null && !UWSetupMenu.IsCapturingInput)
        {
            if (lOControls[UWAct.KeyFight].WasPressedThisFrame())
                fChoose(CommandEnum.Fight);
            else if (lOControls[UWAct.KeyUse].WasPressedThisFrame())
                fChoose(CommandEnum.Use);
            else if (lOControls[UWAct.KeyGet].WasPressedThisFrame())
                fChoose(CommandEnum.Get);
            else if (lOControls[UWAct.KeyTalk].WasPressedThisFrame())
                fChoose(CommandEnum.Talk);
            else if (lOControls[UWAct.KeyLook].WasPressedThisFrame())
                fChoose(CommandEnum.Look);
            else if (lOControls[UWAct.KeyOptions].WasPressedThisFrame())
                fChoose(CommandEnum.Options);
        }

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null || !lOMouse.leftButton.wasPressedThisFrame)
            return;

        if (!TryGetOriginalPoint(UWPointer.Position, out float lfColumn, out float lfRow))
            return;

        // get_iconreg_button: the column by 26, the row by 15 from the bar's top.
        if (lfColumn < UWClassicLayoutUw2.Commands.AreaLeft || lfColumn > UWClassicLayoutUw2.Commands.AreaRight
            || lfRow < UWClassicLayoutUw2.Commands.AreaTop)
            return;

        int liColumn = (int)(lfColumn - UWClassicLayoutUw2.Commands.AreaLeft) / UWClassicLayoutUw2.Commands.IconWidth;
        int liRow = (int)(lfRow - UWClassicLayoutUw2.Commands.AreaTop) / UWClassicLayoutUw2.Commands.IconHeight;
        int liIcon = (Mathf.Min(liRow, 1) * 3) + liColumn;

        if (liColumn >= 0 && liColumn < 3 && liIcon >= 0 && liIcon <= (int)CommandEnum.Options)
            fChoose((CommandEnum)liIcon);
    }

    private void fChoose(CommandEnum peCommand)
    {
        // The options icon opens the panel's game page - not with an object on the pointer
        // (deal_with_icons returns while GameInputMode is set).
        if (peCommand == CommandEnum.Options)
        {
            if (mOInventory == null || mOInventory.CursorItem == null)
            {
                fBeginOptions();
                mOOptions.Open(UWOptionsPanelUw2.GroupEnum.Game);
                fShowOptions();
            }

            return;
        }

        Command = Command == peCommand ? CommandEnum.None : peCommand;
    }

    // ------------------------------------------------- The options panel

    /// <summary>The panel's images over the right panel, hidden until it opens: the group's picture, the
    /// sound page's title over button 1, the message row over button 0 and the lit button.</summary>
    private void fBuildOptions()
    {
        mOOptionsGroup = fCreateGroup(mOFrame, "Options", UWClassicLayoutUw2.Options.Left, UWClassicLayoutUw2.Options.Top,
            UWClassicLayoutUw2.Options.Width, UWClassicLayoutUw2.Options.Height);
        mOOptionsBase = fImageIn(mOOptionsGroup, "Group", 0f, 0f, 1f, 1f, null);
        mOOptionsTitle = fImageIn(mOOptionsGroup, "SoundTitle", 0f, 0f, 1f, 1f, null);
        mOOptionsMessage = fImageIn(mOOptionsGroup, "Message", 0f, 0f, 1f, 1f, null);
        mOOptionsLit = fImageIn(mOOptionsGroup, "Lit", 0f, 0f, 1f, 1f, null);
        mOOptionsGroup.gameObject.SetActive(false);
    }

    /// <summary>Rows piTop to piTop + piHeight - 1 of an OPTBTNS.GR picture, its first piWidth columns.</summary>
    private Sprite fOptionsSprite(int piPicture, int piTop, int piWidth, int piHeight)
    {
        int liKey = (piPicture << 20) | (piTop << 10) | piWidth;

        if (mOOptionsSprites.TryGetValue(liKey, out Sprite lOSprite))
            return lOSprite;

        if (!mOOptionsPictures.TryGetValue(piPicture, out Texture2D lOPicture))
        {
            lOPicture = fPicture(UWTexture.TextureTypes.OPTBTNS, piPicture, false);
            mOOptionsPictures[piPicture] = lOPicture;
        }

        lOSprite = lOPicture != null && piTop + piHeight <= lOPicture.height && piWidth <= lOPicture.width
            ? fSprite(lOPicture, 0, piTop, piWidth, piHeight) : null;
        mOOptionsSprites[liKey] = lOSprite;

        return lOSprite;
    }

    /// <summary>Button b's first row in the group's picture and below the panel's top.</summary>
    private static int fButtonTop(int piButton)
    {
        return UWClassicLayoutUw2.Options.ButtonOffset + (piButton * UWClassicLayoutUw2.Options.ButtonStep);
    }

    /// <summary>The panel as the model has it: the group's unlit picture (install_buttongroup), on the sound
    /// page OPTBTNS.GR 14's title over button 1 (load_buttongroup_images), the message row over button 0
    /// (load_message) and the lit button from the group's lit picture (draw_button).</summary>
    private void fShowOptions()
    {
        int liGroup = (int)mOOptions.Group;
        int liMessagePicture = UWClassicLayoutUw2.Options.MessagePictures[liGroup];
        int liLit = mOOptions.Highlight;

        fPlace(mOOptionsBase, fOptionsSprite(UWClassicLayoutUw2.Options.FirstUnlitPicture + liGroup, 0,
            UWClassicLayoutUw2.Options.Width, UWClassicLayoutUw2.Options.Height), 0f, 0f);
        fPlace(mOOptionsTitle, mOOptions.Group == UWOptionsPanelUw2.GroupEnum.MusicSound && mOOptions.SoundPage
            ? fOptionsSprite(UWClassicLayoutUw2.Options.SoundTitlePicture, UWClassicLayoutUw2.Options.SoundTitleRow,
                UWClassicLayoutUw2.Options.ButtonWidth, UWClassicLayoutUw2.Options.ButtonHeight) : null,
            0f, fButtonTop(UWClassicLayoutUw2.Options.SoundTitleButton));
        fPlace(mOOptionsMessage, mOOptions.MessageRow != UWOptionsPanelUw2.None && liMessagePicture >= 0
            ? fOptionsSprite(liMessagePicture, mOOptions.MessageRow * UWClassicLayoutUw2.Options.MessageStep,
                UWClassicLayoutUw2.Options.ButtonWidth, UWClassicLayoutUw2.Options.ButtonHeight) : null,
            0f, fButtonTop(0));
        fPlace(mOOptionsLit, liLit != UWOptionsPanelUw2.None
            ? fOptionsSprite(UWClassicLayoutUw2.Options.FirstLitPicture + liGroup, fButtonTop(liLit),
                UWClassicLayoutUw2.Options.ButtonWidth, UWClassicLayoutUw2.Options.ButtonHeight) : null,
            0f, fButtonTop(liLit));
    }

    /// <summary>
    /// THE PANEL OPENS (busywaiting_new_options): the bar shows OPTBTNS.GR 2 without its lit icon, the
    /// game stands (UWGameClock.OptionsHold, as the first game's panel) and the walk's input waits (a hold
    /// on the controls that leaves our pointer captured). The caller opens a group in the model; the
    /// groups' pages show the game's settings now (the detail, music and sound).
    /// </summary>
    private void fBeginOptions()
    {
        mOOptions.DetailLevel = (int)UWGraphicsDetail.CurrentLevel;
        mOOptions.MusicOn = UWSoundOptions.MusicEnabled;
        mOOptions.SoundOn = UWSoundOptions.SoundEnabled;
        mOOptions.LastSlot = msiLastSlot;

        if (mbOptionsOpen)
            return;

        mbOptionsOpen = true;
        mbOptionsPress = false;
        mOOptionsGroup.gameObject.SetActive(true);

        if (mOCommandBar != null && mOBarOpen != null)
            mOCommandBar.sprite = mOBarOpen;

        if (mOIconLit != null)
            mOIconLit.enabled = false;

        UWGameClock.Hold(UWGameClock.OptionsHold);

        if (UWControlScheme.Shared != null)
            UWControlScheme.Shared.HoldInput(OptionsInputHold);
    }

    /// <summary>The panel closes: the bar plain again with the chosen command lit (new_IconSelect), the
    /// game and its input free.</summary>
    private void fCloseOptions()
    {
        if (miTypingSlot != 0)
            fEndTyping();

        if (!mbOptionsOpen)
            return;

        mbOptionsOpen = false;
        mbOptionsPress = false;
        mOOptionsGroup.gameObject.SetActive(false);

        if (mOCommandBar != null)
            mOCommandBar.sprite = mOBarPlain;

        // fUpdateCommandIcon lights the chosen command again.
        meShownCommand = CommandEnum.None;

        UWGameClock.Release(UWGameClock.OptionsHold);

        if (UWControlScheme.Shared != null)
            UWControlScheme.Shared.ReleaseInput(OptionsInputHold);
    }

    /// <summary>
    /// THE CTRL SHORTCUTS (do_option_shortcut): Ctrl+S and Ctrl+R open the file page for saving or
    /// restoring with the saves listed, Ctrl+M and Ctrl+F the music's or the sound's page, Ctrl+D the
    /// detail, Ctrl+Q the quit page, Ctrl+O the game page. With an object on the pointer they say "You
    /// cannot select options partway through an action." (GameInputMode). The letters are the first
    /// game's option actions (UWAct.OptSave ...), O the key. True when one was pressed.
    /// </summary>
    private bool fOptionShortcuts()
    {
        UWControls lOControls = UWControlScheme.SharedControls;
        UnityEngine.InputSystem.Keyboard lOKeys = UnityEngine.InputSystem.Keyboard.current;

        if (lOControls == null || lOKeys == null || UWSetupMenu.IsCapturingInput || !(lOKeys.leftCtrlKey.isPressed || lOKeys.rightCtrlKey.isPressed))
            return false;

        bool lbSave = lOControls[UWAct.OptSave].WasPressedThisFrame();
        bool lbRestore = lOControls[UWAct.OptRestore].WasPressedThisFrame();
        bool lbMusic = lOControls[UWAct.OptMusic].WasPressedThisFrame();
        bool lbSound = lOControls[UWAct.OptSound].WasPressedThisFrame();
        bool lbDetail = lOControls[UWAct.OptDetail].WasPressedThisFrame();
        bool lbQuit = lOControls[UWAct.OptQuit].WasPressedThisFrame();
        bool lbGame = lOKeys.oKey.wasPressedThisFrame;

        if (!(lbSave || lbRestore || lbMusic || lbSound || lbDetail || lbQuit || lbGame))
            return false;

        if (mOInventory != null && mOInventory.CursorItem != null)
        {
            Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.NotNowString + 1, string.Empty));

            return true;
        }

        fBeginOptions();

        if (lbSave || lbRestore)
        {
            mOOptions.OpenFile(lbRestore);
            fListSaves();
        }
        else if (lbMusic || lbSound)
        {
            mOOptions.OpenMusicSound(lbSound);
        }
        else
        {
            mOOptions.Open(lbDetail ? UWOptionsPanelUw2.GroupEnum.Detail : lbQuit ? UWOptionsPanelUw2.GroupEnum.Quit : UWOptionsPanelUw2.GroupEnum.Game);
        }

        fShowOptions();

        return true;
    }

    /// <summary>
    /// THE PANEL'S INPUT (busywaiting_new_options' loop): a press follows the pointer, lighting the button
    /// under it or the one lit before, and presses the button it comes up on; Up and Left light the
    /// previous button, Space, Right and Down the next, Enter presses the lit one, Escape closes and
    /// clears the scroll, Alt+X says yes on the quit page, F6 returns from the game page. A setup window
    /// over the game (the effects window Very High opens) has the input meanwhile.
    /// </summary>
    private void fOptionsInput()
    {
        if (UWSetupMenu.HasOpenPanel || UWSetupMenu.IsEffectsScreenOpen || UWSetupMenu.IsCapturingInput)
            return;

        if (miTypingSlot != 0)
        {
            fTypeDescription();

            return;
        }

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse != null && !mbOptionsPress
            && (lOMouse.leftButton.wasPressedThisFrame || lOMouse.rightButton.wasPressedThisFrame || lOMouse.middleButton.wasPressedThisFrame))
        {
            mbOptionsPress = true;
            miOptionsPressOld = mOOptions.Highlight;
        }

        if (mbOptionsPress)
        {
            fFollowOptionsPress(lOMouse);

            return;
        }

        UnityEngine.InputSystem.Keyboard lOKeys = UnityEngine.InputSystem.Keyboard.current;

        if (UWBack.Takes(UWBackOwner.ClassicOptions))
        {
            ClearScroll();
            fCloseOptions();

            return;
        }

        if (lOKeys == null)
            return;

        if (lOKeys.upArrowKey.wasPressedThisFrame || lOKeys.leftArrowKey.wasPressedThisFrame)
            mOOptions.Move(-1);
        else if (lOKeys.spaceKey.wasPressedThisFrame || lOKeys.rightArrowKey.wasPressedThisFrame || lOKeys.downArrowKey.wasPressedThisFrame)
            mOOptions.Move(1);
        else if (lOKeys.enterKey.wasPressedThisFrame || lOKeys.numpadEnterKey.wasPressedThisFrame)
            fPressOption(mOOptions.Highlight);
        else if (lOKeys.altKey.isPressed && lOKeys.xKey.wasPressedThisFrame && mOOptions.Group == UWOptionsPanelUw2.GroupEnum.Quit)
            fPressOption(2);
        else if (lOKeys.f6Key.wasPressedThisFrame && mOOptions.Group == UWOptionsPanelUw2.GroupEnum.Game)
            fPressOption(6);
        else
            return;

        if (mbOptionsOpen)
            fShowOptions();
    }

    /// <summary>The held press (better_mouse_release): the button under the pointer lit while it is one,
    /// else the one lit before; when every button is up, the last button under it pressed.</summary>
    private void fFollowOptionsPress(UnityEngine.InputSystem.Mouse pOMouse)
    {
        int liButton = UWOptionsPanelUw2.None;

        if (TryGetOriginalPoint(UWPointer.Position, out float lfColumn, out float lfRow))
        {
            int liX = Mathf.FloorToInt(lfColumn) - UWClassicLayoutUw2.Options.AreaLeft;
            int liY = (int)UWUiFit.FrameHeight - 1 - Mathf.FloorToInt(lfRow) - UWClassicLayoutUw2.Options.AreaBottomY;

            liButton = UWOptionsPanelUw2.ButtonAt(liX, liY);
        }

        if (liButton != UWOptionsPanelUw2.None && mOOptions.IsButton(liButton))
            mOOptions.Light(liButton);
        else if (miOptionsPressOld != UWOptionsPanelUw2.None)
            mOOptions.Light(miOptionsPressOld);

        miOptionsPressAt = liButton;

        bool lbHeld = pOMouse != null && (pOMouse.leftButton.isPressed || pOMouse.rightButton.isPressed || pOMouse.middleButton.isPressed);

        if (!lbHeld)
        {
            mbOptionsPress = false;

            if (miOptionsPressAt != UWOptionsPanelUw2.None)
                fPressOption(miOptionsPressAt);
        }

        if (mbOptionsOpen)
            fShowOptions();
    }

    /// <summary>
    /// A BUTTON PRESSED (deal_with_button) and what it asks of the game: the saves listed in the scroll
    /// (ShowSaveRest), a save's description typed (SaveGame), a save restored, music or sound on or off
    /// (UWSoundOptions), the detail level (UWGraphicsDetail.Choose, the effects window for Very High as
    /// the first game's panel opens it), the walk left for the main menu (quit, yes: the port leaves to
    /// the menu as the first game's Quit does, not to DOS).
    /// </summary>
    private void fPressOption(int piButton)
    {
        UWOptionsPanelUw2.Outcome lOOutcome = mOOptions.Press(piButton);

        switch (lOOutcome.Action)
        {
            case UWOptionsPanelUw2.ActionEnum.Close:
                ClearScroll();
                fCloseOptions();
                break;
            case UWOptionsPanelUw2.ActionEnum.ListSaves:
                fListSaves();
                break;
            case UWOptionsPanelUw2.ActionEnum.Save:
                msiLastSlot = lOOutcome.Value;
                fBeginTyping(lOOutcome.Value);
                break;
            case UWOptionsPanelUw2.ActionEnum.Restore:
                msiLastSlot = lOOutcome.Value;
                fRestore(lOOutcome.Value);
                break;
            case UWOptionsPanelUw2.ActionEnum.SetMusic:
                UWSoundOptions.MusicEnabled = lOOutcome.Value != 0;
                break;
            case UWOptionsPanelUw2.ActionEnum.SetSound:
                UWSoundOptions.SoundEnabled = lOOutcome.Value != 0;
                break;
            case UWOptionsPanelUw2.ActionEnum.SetDetail:
                UWGraphicsDetail.Choose((UWGraphicsDetail.LevelEnum)lOOutcome.Value, null, UWSetupMenu.OpenEffectsWindow);
                break;
            case UWOptionsPanelUw2.ActionEnum.Quit:
                fCloseOptions();
                UWWalkUw2.Leave();
                break;
        }
    }

    private static string SavegameRoot => UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

    private static readonly string[] msSlotTexts = { "hud.uw2.save_slot_1", "hud.uw2.save_slot_2", "hud.uw2.save_slot_3", "hud.uw2.save_slot_4" };

    /// <summary>The saves' descriptions in the scroll (ShowSaveRest): the heading, then "I- " to "IV- " with
    /// each slot's DESC or "&lt;not used yet&gt;".</summary>
    private void fListSaves()
    {
        string lsRoot = SavegameRoot;

        ClearScroll();
        Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.DescriptionsString + 1, string.Empty));

        for (int liSlot = 1; liSlot <= msSlotTexts.Length; liSlot++)
            Say(UWText.F(msSlotTexts[liSlot - 1], UWSavegameSlots.GetName(lsRoot, liSlot)));
    }

    /// <summary>A save outcome (DoSaveRest: string 0xAE + n - 1 no save there, 2 restored, 4 failed, 5
    /// succeeded, 6 aborted) alone in the scroll.</summary>
    private void fSayOutcome(int piOutcome)
    {
        ClearScroll();
        Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.FirstOutcomeString + piOutcome + 1, string.Empty));
    }

    public const int OutcomeNoSave = 1;

    public const int OutcomeRestored = 2;

    public const int OutcomeSaveFailed = 4;

    public const int OutcomeSaved = 5;

    public const int OutcomeAborted = 6;

    /// <summary>
    /// A RESTORE (DoSaveRest, RestoreGame): an empty slot says "No save game there." and the panel closes;
    /// a save starts the walk again from it (UWWalkUw2.Request, Journey Onward's path), whose screen then
    /// says "Restore Game Complete.".
    /// </summary>
    private void fRestore(int piSlot)
    {
        string lsRoot = SavegameRoot;

        if (!UWSavegameSlots.Exists(lsRoot, piSlot))
        {
            fSayOutcome(OutcomeNoSave);
            fCloseOptions();

            return;
        }

        msAfterStart = UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.FirstOutcomeString + OutcomeRestored + 1, string.Empty);
        fCloseOptions();
        UWWalkUw2.Request(UWSavegameSlots.GetSlotPath(lsRoot, piSlot));
    }

    /// <summary>
    /// THE DESCRIPTION TYPED (SaveGame, wdialog): the scroll cleared, "Please enter a save file
    /// description:" in green, below it ">" and the slot's description - "&lt;not used yet&gt;" for an empty
    /// one, as the original hands it over - which the first typed character replaces; Backspace edits it,
    /// up to 30 characters. Enter or a mouse button saves, Escape aborts. As the first game's panel
    /// (UWHudOptions): the walk's controls off meanwhile, the text read through UWTextEntry, no cursor keys
    /// inside the line.
    /// </summary>
    private void fBeginTyping(int piSlot)
    {
        string lsOld = UWSavegameSlots.GetName(SavegameRoot, piSlot);

        miTypingSlot = piSlot;
        msTyped = lsOld.Length > UWClassicLayoutUw2.Options.DescriptionLength ? lsOld.Substring(0, UWClassicLayoutUw2.Options.DescriptionLength) : lsOld;
        mbTypedFresh = true;
        mfTypingStart = Time.unscaledTime;

        if (mOTypedEntry == null)
            mOTypedEntry = new UWTextEntry(fOnTyped);

        if (UWControlScheme.SharedControls != null)
            UWControlScheme.SharedControls.Disable();

        mOTypedEntry.Begin();

        ClearScroll();
        Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.DescriptionPromptString + 1, string.Empty));
        msMessages.Add(string.Empty);
        fShowTyped();
    }

    /// <summary>The typed line, the last of the scroll, in the prompt's green (wdialog writes in the
    /// scroll's colour, which the prompt left green).</summary>
    private void fShowTyped()
    {
        if (msMessages.Count > 0)
            msMessages[msMessages.Count - 1] = TypedLinePrefix + msTyped;
    }

    public const string TypedLinePrefix = "\\6>";

    private void fOnTyped(char pcChar)
    {
        // A backslash would start a colour code in the scroll.
        if (miTypingSlot == 0 || pcChar == '\\')
            return;

        if (mbTypedFresh)
        {
            msTyped = string.Empty;
            mbTypedFresh = false;
        }

        if (msTyped.Length >= UWClassicLayoutUw2.Options.DescriptionLength)
            return;

        msTyped += pcChar;
        fShowTyped();
    }

    private void fTypeDescription()
    {
        if (UWPointer.HasMouse && (UWMouseButtons.LeftPressed || UWMouseButtons.RightPressed))
        {
            fSaveTyped();

            return;
        }

        if (UWBack.Takes(UWBackOwner.ClassicSaveName))
        {
            fEndTyping();
            fSayOutcome(OutcomeAborted);
            fCloseOptions();

            return;
        }

        if (UWTextEntry.BackspacePressed)
        {
            mbTypedFresh = false;

            if (msTyped.Length > 0)
                msTyped = msTyped.Substring(0, msTyped.Length - 1);

            fShowTyped();

            return;
        }

        if (UWTextEntry.EnterPressed)
        {
            fSaveTyped();

            return;
        }

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys).
        mOTypedEntry.ReadKeys();
    }

    /// <summary>The save made (SaveGame): an empty description becomes " "; the outcome alone in the scroll,
    /// the panel closed.</summary>
    private void fSaveTyped()
    {
        int liSlot = miTypingSlot;
        string lsDescription = msTyped.Length == 0 ? " " : msTyped;

        fEndTyping();

        string lsError = UWWalkUw2View.Save(liSlot, lsDescription);

        if (lsError != null)
            Debug.LogWarning("[Walk] Not saved: " + lsError);

        fSayOutcome(lsError == null ? OutcomeSaved : OutcomeSaveFailed);
        fCloseOptions();
    }

    private void fEndTyping()
    {
        if (mOTypedEntry != null)
            mOTypedEntry.End();

        if (UWControlScheme.SharedControls != null)
            UWControlScheme.SharedControls.Enable();

        miTypingSlot = 0;
    }

    /// <summary>A screen point as the original's column and row (top down), false off the frame. Clicks
    /// ask with our pointer's position (UWPointer.Position): the system mouse is captured on this screen
    /// and stands in the window's middle.</summary>
    public bool TryGetOriginalPoint(Vector2 pOScreen, out float pfColumn, out float pfRow)
    {
        float lfFactor = UWClassicScreen.HorizontalPixelFactor;
        float lfScale = UWUiFit.Scale;

        pfColumn = (pOScreen.x - (fSideUnits(lfFactor) * lfScale)) / (lfScale * lfFactor);
        pfRow = UWUiFit.FrameHeight - ((pOScreen.y - UWUiFit.BottomMarginPixels) / lfScale);

        return mbShown && pfColumn >= 0f && pfColumn < UWUiFit.FrameWidthUnits && pfRow >= 0f && pfRow < UWUiFit.FrameHeight;
    }

    /// <summary>The original's angle from the view's top to its bottom edge, in degrees (74.7).</summary>
    public static float OriginalVerticalAngle => 2f * Mathf.Atan(UWClassicLayoutUw2.Frame.VerticalViewTangent) * Mathf.Rad2Deg;

    /// <summary>Whether a screen point lies in the 3D view - everywhere in the modern scheme, inside
    /// the window on the classic screen (a click on the frame uses nothing).</summary>
    public bool IsInView(Vector2 pOScreen)
    {
        return !mbShown || mOCamera == null || mOCamera.pixelRect.Contains(pOScreen);
    }

    private void OnDestroy()
    {
        fCloseOptions();

        if (Current == this)
            Current = null;

        fReleaseScaledCursors();

        if (mOCursorObject != null)
            Destroy(mOCursorObject);

        foreach (Texture2D lOTexture in mOOwned)
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        mOOwned.Clear();
    }
}
