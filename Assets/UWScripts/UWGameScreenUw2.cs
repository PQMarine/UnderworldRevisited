using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S CLASSIC SCREEN AROUND THE SESSION (UW2 plan phase 3 step 1, 2026-10-29): its game
/// screen (BYT.ARK block 4, UWClassicLayoutUw2.Frame) over the 3D view, which renders into the
/// 208 x 128 window place_3d_view gives it at the original's angle - the first game's zoom, 74.7
/// degrees from top to bottom, about 91 across. The first game's classic HUD (UWGameUI and its
/// UWHud* parts) is built on the first game's numbers throughout and stays uninitialised in the
/// session; this is the second game's own owner, which grows with the steps of phase 3.
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
/// STEP 2 PART A: the character the session came from (the save's PLAYER.DAT, UWPlayerRecordUw2) - the
/// flasks filled to the twelfths of vitality and mana (red, green when poisoned, blue), the right
/// panel's inventory page (PANELS.GR 0) with the paperdoll (BODIES.GR by sex and body, the armour of
/// the five slots from ARMOR_F or ARMOR_M by type and condition), the objects of shoulders, hands,
/// rings and backpack (OBJECTS.GR by id, a stack's count over it) and the weight left in stones. A session
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

    private static CommandEnum meCommand = CommandEnum.None;

    /// <summary>The command the bar has chosen; None is the default, a look (mous_in_3d). The fight command is
    /// player->drawn: choosing it asks for the armed theme, leaving it for a walking one (pick_fightmode and
    /// punt_fightmode, INTERACT.C:683, :713, :726; UW2 plan phase 5 part A).</summary>
    public static CommandEnum Command
    {
        get
        {
            return meCommand;
        }

        private set
        {
            bool lbWasDrawn = meCommand == CommandEnum.Fight;

            meCommand = value;

            if (lbWasDrawn != (value == CommandEnum.Fight))
                UWAudioService.Uw2Drawn(!lbWasDrawn);
        }
    }

    /// <summary>A rule puts the weapon away (UWPlayerAvatar.LeaveCombatMode: the golem's stand-down).</summary>
    public static void LeaveFight()
    {
        if (Command == CommandEnum.Fight)
            Command = CommandEnum.None;
    }

    /// <summary>The lines the scroll holds, the newest last - as many as the first game's
    /// Interaction.ActiveStrings keeps (UWMessageLog.HistoryCount: the modern scroll at its tallest; the classic box
    /// shows its newest five). Five until UW2 plan stage U6 step 2b: the modern scroll set taller showed five (per user
    /// at the test of 200).</summary>
    private static readonly List<string> msMessages = new List<string>();

    public const int KeptMessages = UWMessageLog.HistoryCount;

    /// <summary>The lines the scroll ever took and the times it was wiped, only growing: the edges turn by them
    /// (UWMessageLogModel.Update's plSaid and plCleared; scroll_up turns them per line, whatever it says).</summary>
    private static long mlScrollSaid;

    private static long mlScrollCleared;

    public static long ScrollSaid => mlScrollSaid;

    public static long ScrollCleared => mlScrollCleared;

    /// <summary>A line into the session's message scroll.</summary>
    public static void Say(string psLine)
    {
        // The game's scroll is covered during a conversation: what the panel says (a look at the inventory)
        // stands in the answer scroll for its two seconds, as a look at a trade slot.
        if (UWConversationScreenUw2.ShowLook(psLine))
            return;

        if (string.IsNullOrEmpty(psLine))
            return;

        msfLastSaid = Time.unscaledTime;
        msMessages.Add(psLine);
        mlScrollSaid++;
        LastPageLines = 0;

        while (msMessages.Count > KeptMessages)
            msMessages.RemoveAt(0);
    }

    /// <summary>The lines the last page of a paged text put into the scroll (0 once a plain line followed): the
    /// modern box shows at least these, its three lines being fewer than a page's four.</summary>
    public static int LastPageLines { get; private set; }

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

    /// <summary>The compass's goal, the heading it turns to (set_screen_frame(2)); -1 before the first.</summary>
    private int miCompassGoal = -1;

    /// <summary>The time towards the compass's next step (adjust_compass on the slow update).</summary>
    private float mfCompassTime;

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

    /// <summary>The session's inventory (step 3), null without a character.</summary>
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


    /// <summary>The session's inventory for the view's buttons (UWWorldViewUw2: taking from and putting into
    /// the world, step 3 part B).</summary>
    public UWInventoryModel Inventory => mOInventory;

    // ------------------------------------------------- For the modern interface's host (UWModernSessionUw2)

    /// <summary>The game's data the screen draws from.</summary>
    internal DataImport ModernData => mOData;

    /// <summary>The scroll's lines, the newest last, and when the last was said (unscaled seconds).</summary>
    internal static IReadOnlyList<string> SaidLines => msMessages;

    internal static float LastSaidAt => msfLastSaid;

    /// <summary>A line is being typed into the scroll's last line (a count, a save's name).</summary>
    internal bool IsTypingLine => miTypingSlot != 0 || mbCountLine;

    /// <summary>A save game's name is being typed in the classic options (UWBack's ClassicSaveName).</summary>
    internal bool IsTypingSaveName => miTypingSlot != 0;

    /// <summary>The classic screen lies over the view (the classic scheme, or the options panel in the modern one).</summary>
    internal bool IsClassicCovering => IsClassicShown;

    /// <summary>The saved objects the inventory's links point into (an enchanted thing's spell).</summary>
    public List<UWObject> WorkspaceRecords => mOWorkspaceRecords;

    /// <summary>The workspace's records as read: what a link not yet resolved into contents points into.</summary>
    private List<UWObject> mOWorkspaceRecords;

    /// <summary>
    /// THE INVENTORY INTO THE SESSION'S PLAYER.DAT before a save (step 3 part D): the object on the pointer
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
            Debug.LogWarning("[Session] The object on the pointer is not saved: the pack is full.");

        UWLevel lOLevel = UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevel : null;

        return UWInventoryWorkspace.WriteInto(pOFile, mOInventory.GetSavegameEquipment(), mOInventory.Backpack,
            lOLevel != null ? lOLevel.Masterlist : null, mOWorkspaceRecords,
            mOData.CommonObjectProperties != null ? mOInventory.GetCarriedTenthStones(mOData.CommonObjectProperties) : UWPlayerRecordUw2.Weight(pOFile));
    }

    private bool mbInventoryDirty;

    /// <summary>The object on the pointer as a picture at the pointer's scale.</summary>
    private Texture2D mOCursorObject;

    private long mlCursorObjectKey = -1;

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

    private Image mOPowerGem;

    private Image mOEyes;

    private Sprite msEyesRest;

    private readonly Sprite[] maEyeSprites = new Sprite[UWClassicLayoutUw2.Eyes.Blink.Length * UWClassicLayoutUw2.Eyes.PicturesPerCondition + 1];

    /// <summary>The condition shown (setting[7]), 0 for none; the blink's frame, the hold and the step's time.</summary>
    private int miEyesSet;

    private int miEyesFrame;

    private int miEyesHold;

    private float mfEyesStep;

    /// <summary>do_damage: the creature hit, hit points against its table's average: 1 well to 3 near death.
    /// A blink running restarts at its middle, as adjust_eyes does. The name and the time are the modern heading
    /// strip's (ModernFoeEyes).</summary>
    public void ShowEyes(int piHitPoints, int piTableHitPoints, string psName = null)
    {
        int liShare = piTableHitPoints != 0 ? Mathf.Min((piHitPoints * 3) / piTableHitPoints, 2) : 0;

        if (miEyesSet != 0)
        {
            miEyesFrame = 2;
            miEyesHold = 0;
        }
        else
            miEyesFrame = 0;

        miEyesSet = 3 - liShare;
        miFoeSet = miEyesSet;
        msFoeName = psName;
        mfFoeShownAt = Time.time;
    }

    /// <summary>The last hit's condition, name and time, for the modern heading strip.</summary>
    private int miFoeSet;

    private string msFoeName;

    private float mfFoeShownAt = -100f;

    private readonly UWArtCache<int> mOFoeEyeTextures = new UWArtCache<int>();

    /// <summary>
    /// THE EYES FOR THE MODERN HEADING STRIP (IUWModernWorld.FoeEyes, UW2 plan part U4d step 2): lit and fading as the
    /// first game's (UWEyesDisplay: UWSettings.EyesSeconds after the hit, EyesFadeSeconds in and out), the picture the
    /// classic screen's blink shows while it runs, after it the condition's widest frame (adjust_eyes holds that one).
    /// </summary>
    public UWModernFoeEyes ModernFoeEyes()
    {
        if (miFoeSet == 0 || mOData == null)
            return default(UWModernFoeEyes);

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        float lfHold = lOSettings != null ? lOSettings.EyesSeconds : 4f;
        float lfFade = Mathf.Max(0.0001f, lOSettings != null ? lOSettings.EyesFadeSeconds : 0.3f);
        float lfSince = Time.time - mfFoeShownAt;

        if (lfSince >= lfHold + lfFade)
            return default(UWModernFoeEyes);

        float lfAlpha = lfSince < lfHold ? Mathf.Clamp01(lfSince / lfFade) : Mathf.Clamp01(1f - ((lfSince - lfHold) / lfFade));
        int liFrame = miEyesSet != 0 ? Mathf.Clamp(miEyesFrame - 1, 0, UWClassicLayoutUw2.Eyes.Blink.Length - 1) : UWClassicLayoutUw2.Eyes.HoldFrame - 1;
        int liPicture = UWClassicLayoutUw2.Eyes.Blink[liFrame] + ((miFoeSet - 1) * UWClassicLayoutUw2.Eyes.PicturesPerCondition);

        return new UWModernFoeEyes
        {
            IsLit = true,
            Picture = fFoeEyesPicture(liPicture),
            Alpha = lfAlpha,
            Name = msFoeName,
            Group = miFoeSet - 1,
            TopInHead = UWClassicLayoutUw2.Eyes.Top - UWClassicLayoutUw2.GargoyleHead.Top
        };
    }

    /// <summary>An EYES picture with its colour 0 clear, as the first game's eyes (UWEyesDisplay), anew with the colour help.</summary>
    private Texture2D fFoeEyesPicture(int piPicture)
    {
        UWArtStamp lOStamp = UWVersionedTexture.Colours();

        if (mOFoeEyeTextures.TryGet(piPicture, lOStamp, out Texture2D lOTexture))
            return lOTexture;

        UWTexture lOSource = mOData.Textures != null ? mOData.Textures.GetTextureByType(UWTexture.TextureTypes.EYES, piPicture) : null;

        if (lOSource == null || lOSource.Width <= 0 || lOSource.Height <= 0)
            return null;

        return mOFoeEyeTextures.Set(piPicture, lOStamp, UWTextureFactory.Upload(UWDataImport.Presentation.UWUploadPixels.FromIndexZeroKeyed(lOSource,
            UWColourVision.Filter), lOSource.Width, lOSource.Height, FilterMode.Point, TextureWrapMode.Clamp, "UW2 modern foe eyes " + piPicture));
    }

    /// <summary>set_compass's heading in sixteen steps, read from the view as the classic compass reads it.</summary>
    public int CompassStepNow
    {
        get
        {
            Camera lOView = UWViewpoint.Current;

            return lOView != null ? UWHeadings.HeadingByteToCompassStep(UWHeadings.DegreesToHeadingByte(lOView.transform.eulerAngles.y)) : 0;
        }
    }

    private void fUpdateEyes()
    {
        if (mOEyes == null || miEyesSet == 0)
            return;

        mfEyesStep += Time.deltaTime;

        while (mfEyesStep >= UWClassicLayoutUw2.Eyes.StepSeconds && miEyesSet != 0)
        {
            mfEyesStep -= UWClassicLayoutUw2.Eyes.StepSeconds;
            fStepEyes();
        }
    }

    private void fStepEyes()
    {
        if (miEyesFrame == UWClassicLayoutUw2.Eyes.HoldFrame && miEyesHold < UWClassicLayoutUw2.Eyes.HoldSteps)
        {
            miEyesHold++;

            return;
        }

        if (miEyesFrame >= UWClassicLayoutUw2.Eyes.Blink.Length)
        {
            miEyesSet = 0;
            miEyesFrame = 0;
            miEyesHold = 0;
            mOEyes.sprite = msEyesRest;

            return;
        }

        int liPicture = UWClassicLayoutUw2.Eyes.Blink[miEyesFrame] + ((miEyesSet - 1) * UWClassicLayoutUw2.Eyes.PicturesPerCondition);

        miEyesFrame++;

        if (maEyeSprites[liPicture] == null)
            maEyeSprites[liPicture] = fWholeSprite(fPicture(UWTexture.TextureTypes.EYES, liPicture, false));

        if (maEyeSprites[liPicture] != null)
            mOEyes.sprite = maEyeSprites[liPicture];
    }

    private enum WeaponStateEnum
    {
        Hidden,
        Drawing,
        Ready,
        Sheathing,

        /// <summary>A blow: 0 to 2 in setting[8].</summary>
        Swinging
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

    /// <summary>The screen of the running session, null without one.</summary>
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

    /// <summary>The screen on the camera the session looks through, built from the game's data.</summary>
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

        // A new session starts with an empty scroll and no command (the statics outlive the scene).
        msMessages.Clear();
        mlScrollCleared++;
        // Directly: the session's start picks its own theme (UWWorldViewUw2), no sheathing's.
        meCommand = CommandEnum.None;
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

    /// <summary>A line the next session's screen says when it starts: "Restore Game Complete." after a restore.</summary>
    private static string msAfterStart;

    /// <summary>The input hold's name while the panel is up (the clock's is UWGameClock.OptionsHold).</summary>
    private const string OptionsInputHold = "uw2 options";

    /// <summary>The options panel's modal hold, as the first game's panel holds "options" (UWHudOptions): the
    /// pointer stays free over it in the modern scheme too (per user, 2026-11-07: switched from Classic to
    /// Modern on the setup bar's Game page, the mouse look took the pointer and the page could not be closed).</summary>
    private const string OptionsModalHold = "uw2 options panel";

    /// <summary>Whether the options panel is up: the session's buttons and doors wait (UWWorldViewUw2).</summary>
    public static bool IsOptionsOpen => Current != null && Current.mbOptionsOpen;

    /// <summary>AskHowMany; the first game's prompt, its line in the scroll that is showing.</summary>
    private UWCountAskUw2 mOCountAsk;

    /// <summary>The prompt's line is the scroll's last.</summary>
    private bool mbCountLine;

    /// <summary>"Move how many?" is up, or was answered in this frame: the game has no input.</summary>
    public static bool IsAskingCount => Current != null && Current.mOCountAsk != null && Current.mOCountAsk.IsBusy;

    /// <summary>
    /// Asks how many of a stack on the classic screen (AskHowMany) and gives the count taken to pFTaken. False,
    /// with nothing asked, for a single thing and in the world on the modern scheme, where E takes the whole; the
    /// modern bags ask too, the line standing beside the flasks.
    /// </summary>
    public bool AskCount(UWObject pOItem, System.Action<int> pFTaken)
    {
        // The modern scheme asks in a conversation's trade only (the bags have their own split box, E takes the whole).
        if (!(IsClassicShown || UWConversationScreenUw2.IsOpen) || !UWStackRules.IsStack(pOItem))
            return false;

        if (mOCountAsk == null)
            mOCountAsk = new UWCountAskUw2(fShowCountLine, fEndCountLine);

        mfTypingStart = Time.unscaledTime;
        mOCountAsk.Begin(UWStackRules.Count(pOItem), pFTaken);

        return true;
    }

    private void fShowCountLine(string psLine)
    {
        if (UWConversationScreenUw2.IsOpen)
        {
            mOConversation.ShowCountLine(psLine);

            return;
        }

        msfLastSaid = Time.unscaledTime;

        if (mbCountLine)
            msMessages[msMessages.Count - 1] = psLine;
        else
        {
            mbCountLine = true;
            Say(psLine);
        }
    }

    private void fEndCountLine()
    {
        mbCountLine = false;

        if (UWConversationScreenUw2.IsOpen)
            mOConversation.EndCountLine();
    }

    // ------------------------------------------------- The yes/no question (wyorn, G3 part H2)

    private const string YesNoHold = "uw2 yes/no";

    /// <summary>The first game's question (UWYesNoPrompt) on the scroll's last line.</summary>
    private UWYesNoPrompt mOYesNo;

    private bool mbYesNoLine;

    private bool mbYesNoHeld;

    private int miYesNoEndedFrame = -1;

    /// <summary>A yes/no question is up, or was answered in this frame: the game has no input.</summary>
    public static bool IsAskingYesNo => Current != null && Current.mOYesNo != null
        && (Current.mOYesNo.IsOpen || Current.miYesNoEndedFrame == Time.frameCount);

    /// <summary>The question for the modern scheme's question box (UWModernQuestion), null while none is up.</summary>
    public string ModernYesNoQuestion => mOYesNo != null && mOYesNo.IsOpen ? mOYesNo.Question : null;

    /// <summary>The question box's OK or Cancel.</summary>
    public void ModernAnswerYesNo(bool pbYes)
    {
        if (mOYesNo != null)
            mOYesNo.AnswerNow(pbYes);
    }

    /// <summary>wyorn: the question on the scroll's last line, "Yes" preset, the game standing until it is answered
    /// with the first game's keys and buttons (UWYesNoPrompt). False when one is up already.</summary>
    public bool AskYesNo(string psQuestion, System.Action<bool> pFAnswered)
    {
        if (mOYesNo == null)
            mOYesNo = new UWYesNoPrompt(fShowYesNoLine, fEndYesNoLine);

        if (mOYesNo.IsOpen)
            return false;

        mbYesNoHeld = true;
        UWGameClock.Hold(YesNoHold);

        if (UWControlScheme.Shared != null)
            UWControlScheme.Shared.HoldInput(YesNoHold);

        mOYesNo.Ask(psQuestion, pbYes =>
        {
            miYesNoEndedFrame = Time.frameCount;
            pFAnswered?.Invoke(pbYes);
        });

        return true;
    }

    private void fShowYesNoLine(string psLine)
    {
        msfLastSaid = Time.unscaledTime;

        if (mbYesNoLine && msMessages.Count > 0)
            msMessages[msMessages.Count - 1] = psLine;
        else
        {
            mbYesNoLine = true;
            Say(psLine);
        }
    }

    private void fEndYesNoLine()
    {
        mbYesNoLine = false;
    }

    /// <summary>The question's tick; the hold goes a frame after the answer, with the press that gave it. True while it
    /// runs.</summary>
    private bool fUpdateYesNo()
    {
        if (mOYesNo == null)
            return false;

        if (mbYesNoHeld && !mOYesNo.IsOpen && Time.frameCount > miYesNoEndedFrame)
        {
            mbYesNoHeld = false;
            UWGameClock.Release(YesNoHold);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.ReleaseInput(YesNoHold);
        }

        return mOYesNo.Update();
    }

    /// <summary>Empties the scroll (scroll_clear).</summary>
    public static void ClearScroll()
    {
        msMessages.Clear();
        mlScrollCleared++;
        msPendingPage.Clear();
        msbPageRelease = false;
    }

    // ------------------------------------------------- [MORE] (scroll_more, uw2 ui/SCROLLIO.C)

    /// <summary>The lines of a long text not printed yet.</summary>
    private static readonly List<string> msPendingPage = new List<string>();

    /// <summary>The long text being paged (UWMessageLogModel.LastLineJoinsPage).</summary>
    private static string msPagedText;

    private static int msiPageStartFrame = -1;

    private static int msiPageEndedFrame = -1;

    /// <summary>[MORE] in colour 0x12 (scroll_more).</summary>
    private const int MoreColourIndex = 0x12;

    /// <summary>After the last page the buttons must come up first (scroll_wait's mouse_release): the click that paged
    /// is no look or use when it is let go.</summary>
    private static bool msbPageRelease;

    /// <summary>A long text holds the input: [MORE] waits (and the frame its last page came, and until the buttons are up,
    /// so the click that paged is no look or use) - for the world, the attack and the pad's owners.</summary>
    public static bool IsWaitingForPage => msPendingPage.Count > 0 || msiPageEndedFrame == Time.frameCount || msbPageRelease
        || UWConversationScreenUw2.IsLookPaging;

    /// <summary>
    /// [MORE] STANDS IN THE SCROLL: lines of a long text are still to come - what the modern scroll and message box show (the
    /// classic scroll asks msPendingPage itself). Apart from IsWaitingForPage, as the first game's log keeps the wait
    /// (UWMessageLog.IsWaitingForPage) apart from the press that paged (PageInputConsumedThisFrame): held for the
    /// buttons to come up, the modern scroll kept [MORE] and a line less after the last page and showed the text's end
    /// only on the release - which looked like a second page (per user, 2026-11-24, found with a one-line log per page).
    /// </summary>
    public static bool IsMoreShown => msPendingPage.Count > 0;

    /// <summary>
    /// A text that may be longer than the scroll (per user 2026-11-18 with the original: a book's text stops with
    /// [MORE] after four lines, the rest comes with a key or a click). The first game's paging (UWMessageLogModel
    /// .ShowNextPage): the lines come four at a time (the scroll's lines less the [MORE] row), the older lines scroll
    /// away - so a header printed before a long text, "You read the book...", has left the scroll before the player
    /// sees it, as in the original. While [MORE] waits the game stands and every key or click only pages on
    /// (scroll_wait).
    /// </summary>
    public static void SayPaged(string psText)
    {
        UWFont lOFont = Current != null && Current.mOData != null && Current.mOData.Fonts != null
            ? Current.mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

        // A conversation covers the session's scroll: the text joins the look in its answer scroll and pages there with
        // [MORE] (per user 2026-11-19: a scroll looked at in a conversation showed "You read the scroll..." alone, its
        // text went into the covered session scroll; 2026-11-20 on the original: a long book waits with [MORE] there too).
        if (UWConversationScreenUw2.ShowLookPaged(psText))
            return;

        if (lOFont == null || string.IsNullOrEmpty(psText))
        {
            Say(psText);

            return;
        }

        msPendingPage.Clear();
        // Wrapped at the scroll shown (IUWUiScheme.ShownLogWidth): a narrowed modern scroll wraps more lines, which the
        // page must count (per user, 2026-11-24: they scrolled away unread).
        msPendingPage.AddRange(UWMessageLogModel.WrapForPaging(lOFont, psText,
            IsModernScheme ? UWControlScheme.Ui.ShownLogWidth(UWClassicLayoutUw2.MessageLog.Width) : ClassicLogWidth));
        msPagedText = psText;
        msiPageStartFrame = Time.frameCount;
        Current.fShowNextPage(lOFont);
    }

    private void fShowNextPage(UWFont pOFont)
    {
        // THE PAGE FOLLOWS THE SCROLL SHOWN, as the first game's (UWMessageLog.fShowNextPage, decision 17): the classic box,
        // or in the modern scheme the modern scroll at its size or the message box (UWControlScheme.Ui.ShownLogLines).
        int liShown = IsModernScheme && UWControlScheme.Ui != null ? UWControlScheme.Ui.ShownLogLines
            : UWMessageLogModel.CapacityOf(pOFont, UWClassicLayoutUw2.MessageLog.Height);
        int liPerPage = UWMessageLogModel.LinesAboveMore(liShown);

        LastPageLines = 0;

        for (int liAt = 0; liAt < liPerPage && msPendingPage.Count > 0; liAt++)
        {
            msMessages.Add(msPendingPage[0]);
            mlScrollSaid++;
            msPendingPage.RemoveAt(0);
            LastPageLines++;
        }

        // The last line in the text's last piece passes without another [MORE] (scroll_print3's flag 0).
        if (msPendingPage.Count == 1 && UWMessageLogModel.LastLineJoinsPage(msPagedText, msPendingPage[0]))
        {
            msMessages.Add(msPendingPage[0]);
            mlScrollSaid++;
            msPendingPage.RemoveAt(0);
            LastPageLines++;
        }

        while (msMessages.Count > KeptMessages)
            msMessages.RemoveAt(0);

        msfLastSaid = Time.unscaledTime;

        bool lbWaiting = msPendingPage.Count > 0;

        if (lbWaiting == mbPagingHeld)
            return;

        mbPagingHeld = lbWaiting;

        if (lbWaiting)
        {
            UWGameClock.Hold(UWGameClock.PagingHold);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.HoldInput(UWGameClock.PagingHold);
        }
        else
        {
            msiPageEndedFrame = Time.frameCount;
            msbPageRelease = true;
            UWGameClock.Release(UWGameClock.PagingHold);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.ReleaseInput(UWGameClock.PagingHold);
        }
    }

    private bool mbPagingHeld;

    /// <summary>scroll_wait for [MORE]: any key, mouse button or pad button pages on. True while a text waits.</summary>
    private bool fUpdatePaging()
    {
        // A look's [MORE] in a conversation's answer scroll pages there (UWConversationScreenUw2.fUpdateLookPaging): the
        // conversation's frame runs from here, as the rest of Update does not (per user 2026-11-20: [MORE] took no input).
        if (UWConversationScreenUw2.IsLookPaging)
        {
            if (mOConversation != null)
                mOConversation.Tick();

            return true;
        }

        if (msPendingPage.Count == 0)
        {
            if (msbPageRelease && !UWMouseButtons.LeftHeld && !UWMouseButtons.RightHeld)
                msbPageRelease = false;

            return msbPageRelease;
        }

        if (Time.frameCount == msiPageStartFrame)
            return true;

        // Any key, mouse or pad button, one page a frame and a click's release no second one - the first game's routine
        // (UWPageInput; per user 2026-11-24: one click went over more than one [MORE]).
        if (UWPageInput.TryConfirm())
        {
            UWFont lOFont = mOData != null && mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

            if (lOFont != null)
                fShowNextPage(lOFont);
            else
                msPendingPage.Clear();
        }

        return true;
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

        mOScreenImage = lOScreen;

        if (lOMain != null)
        {
            mOScreenSprite = fWholeSprite(fScreenWithView(lOMain));
            lOScreen.sprite = mOScreenSprite;
        }
        else
            Debug.LogWarning("[Session] No game screen in BYT.ARK (block " + UWClassicLayoutUw2.Frame.GameScreenBlock + ").");

        fBuildViewOverlay();
        fBuildCharacter();
        fBuildScroll();
        fBuildCompass();
        fBuildCommands();
        fBuildOptions();

        // THE WIDESCREEN BOX (UW2 plan stage U8 step 1): the first game's mechanism (UWClassicWide) on this frame with this
        // game's cuts - every part moves by its place's band, the weapon's window with the view's middle (its left edge is
        // the view band's cut), the scroll's paper grows, the active spells move with their hollow.
        UWClassicWide.Attach(mOFrame, UWClassicWideGeometry.Uw2);

        // CURSORS.GR, drawn transparent: 0 the pointer, 1 to 8 the arrows of the view's regions, 9 the aim (0x1075), 10 the
        // target spell's pointer (0x1076; until 2026-11-12 the array ended at the aim, so the classic screen kept the plain
        // pointer for a target spell and the modern free pointer had none), 11 the red X of Use, Look and Get (0x1077).
        mOCursors = new Texture2D[(int)UWCursors.CursorEnum.Use + 1];

        for (int liAt = 0; liAt < mOCursors.Length; liAt++)
            mOCursors[liAt] = fPicture(UWTexture.TextureTypes.CURSORS, liAt, true);
    }

    /// <summary>The weapon's place, then the teeth over it (do_fbuf_bms' order): 3DWIN.GR 0 at its frame
    /// buffer place, cut to the rows inside the view. THE WEAPON IS CUT TO THE VIEW as fbshow cuts a picture
    /// to the window (per user, 2026-11-01): the hold's step of a sword or axe has its picture at 240, 144, right
    /// of the view and above it, which stood on the frame.</summary>
    private void fBuildViewOverlay()
    {
        RectTransform lOWindow = fCreateGroup(mOFrame, "WeaponWindow", UWClassicLayoutUw2.Frame.ViewWindowLeft, UWClassicLayoutUw2.Frame.ViewWindowTop,
            UWClassicLayoutUw2.Frame.ViewWindowWidth, UWClassicLayoutUw2.Frame.ViewWindowHeight);

        lOWindow.gameObject.AddComponent<RectMask2D>();
        mOWeapon = fImageIn(lOWindow, "Weapon", 0f, 0f, 1f, 1f, null);

        // The textures' look (UWTextureLook): smooth or sharp bilinear in Remastered.
        UWWeaponFilterUI.Apply(mOWeapon);

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

    /// <summary>The weapon is up and ready for a blow.</summary>
    public bool IsWeaponReady => meWeapon == WeaponStateEnum.Ready;

    /// <summary>A swing is on the screen.</summary>
    public bool IsSwinging => meWeapon == WeaponStateEnum.Swinging;

    /// <summary>The savegame slot of the weapon's hand: Inventory[8 - lefty] (PLAYDATA.C:371).</summary>
    public int WeaponHandSlot => UWClassicLayoutUw2.Weapon.RightHandSlot - (mOInventory != null && !mOInventory.IsLeftHanded ? 1 : 0);

    private int miSwingRow;

    private bool mbSwingHeld;

    private bool mbSwingBack;

    /// <summary>The flag bits of the swing's step (wframe[n] &amp; 0xE0), what player_attack reads; 0 before the first.</summary>
    public int SwingFlag
    {
        get
        {
            if (miWeaponFrame < 0)
                return 0;

            if (miWeaponKind < 0)
                return UWClassicLayoutUw2.Weapon.IdleSteps[Mathf.Min(miWeaponFrame, UWClassicLayoutUw2.Weapon.IdleSteps.Length - 1)]
                    & UWClassicLayoutUw2.Weapon.SwingFlagMask;

            UWWeaponAnimations.Uw2Record lORecord = fSwingRecord();

            return lORecord != null ? lORecord.Steps[fSwingStep] & UWClassicLayoutUw2.Weapon.SwingFlagMask : 0;
        }
    }

    /// <summary>The steps of the swing; a kind of none has the idle ones (load_weapon(-1)).</summary>
    private int fSwingTotal(UWWeaponAnimations.Uw2Record pORecord)
    {
        return miWeaponKind < 0 ? UWClassicLayoutUw2.Weapon.IdleSteps.Length : pORecord.FrameTotals[miSwingRow];
    }

    private int fSwingStep => (miSwingRow * UWClassicLayoutUw2.Weapon.StepsPerSwing) + miWeaponFrame;

    private UWWeaponAnimations.Uw2Record fSwingRecord()
    {
        UWWeaponAnimations lOAnimations = mOData.WeaponAnimations;
        int liRecord = ((mbWeaponLefty ? 0 : 1) * UWClassicLayoutUw2.Weapon.KindsPerHand) + miWeaponKind;

        return lOAnimations == null || lOAnimations.Uw2Records == null || miWeaponKind < 0 || liRecord >= lOAnimations.Uw2Records.Count
            ? null : lOAnimations.Uw2Records[liRecord];
    }

    /// <summary>A swing of this kind begins: the kind's row of steps, 0 slash, 1 bash, 2 stab (swing_kind, get_wfr).</summary>
    public void BeginSwing(AttackTypes peKind)
    {
        miSwingRow = (int)peKind;
        miWeaponFrame = -1;
        mfWeaponTime = UWClassicLayoutUw2.Weapon.StepSeconds;
        mbSwingHeld = false;
        mbSwingBack = false;
        meWeapon = WeaponStateEnum.Swinging;
    }

    /// <summary>The swing ends here and the weapon stands ready.</summary>
    public void StopSwing()
    {
        if (meWeapon == WeaponStateEnum.Swinging)
            meWeapon = WeaponStateEnum.Ready;
    }

    /// <summary>The button came up on the hold: the swing goes on to its blow.</summary>
    public void ResumeSwing()
    {
        mbSwingHeld = false;
    }

    /// <summary>Let go during the lead-in: the weapon goes on to the next step it can stop on, then ready (adjust_weapon).</summary>
    public void CancelSwing()
    {
        mbSwingBack = true;
        mbSwingHeld = false;
    }

    /// <summary>The screens in front of the session during which player_attack and the weapon's steps do not run (the game
    /// loop is theirs): a conversation, the map, the options, the count and yes/no prompts, [MORE].</summary>
    public static bool IsModalOpen => UWConversationScreenUw2.IsOpen || UWMapScreenUw2.IsOpen || IsOptionsOpen || IsAskingCount
        || IsAskingYesNo || IsWaitingForPage || IsCutscenePlaying;

    /// <summary>
    /// A CUTSCENE HAS THE GAME (show_cutscene, uw2 gfx/CUTS.C: mouse_hide, then show_anm's loop eats every input through
    /// gobble_input_events; per user 2026-11-23: during the ending the use pointer showed and the map button took a
    /// click): the ending, the credits, a book's or a dream's cutscene, the victory screens, and the frame one of them
    /// ended in (UWIntroPlayer.HoldsKeys - the click or key that ended it is not the game's).
    /// </summary>
    public static bool IsCutscenePlaying => UWScene.IntroPlayer != null && UWScene.IntroPlayer.HoldsKeys;

    /// <summary>A step each eighth of a second, one at a time so that the strike's step is never skipped. The
    /// hold's step stops the weapon until the button comes up (fast_adjust cleared at the 0x40 step). It stands while a
    /// screen of its own is up (IsModalOpen), as the original's swing does in a conversation.</summary>
    private void fUpdateSwing()
    {
        UWWeaponAnimations.Uw2Record lORecord = fSwingRecord();

        if (lORecord == null && miWeaponKind >= 0)
        {
            meWeapon = WeaponStateEnum.Ready;

            return;
        }

        if (mbSwingHeld || IsModalOpen)
            return;

        mfWeaponTime += Time.unscaledDeltaTime;

        if (mfWeaponTime < UWClassicLayoutUw2.Weapon.StepSeconds)
            return;

        mfWeaponTime -= UWClassicLayoutUw2.Weapon.StepSeconds;

        if (mbSwingBack && SwingFlag == UWClassicLayoutUw2.Weapon.LeadInFlag)
        {
            meWeapon = WeaponStateEnum.Ready;

            return;
        }

        miWeaponFrame++;

        if (miWeaponFrame > 0 && miWeaponFrame >= fSwingTotal(lORecord))
        {
            meWeapon = WeaponStateEnum.Ready;

            return;
        }

        if (SwingFlag == UWClassicLayoutUw2.Weapon.HoldFlag)
        {
            if (mbSwingBack)
                meWeapon = WeaponStateEnum.Ready;
            else
                mbSwingHeld = true;
        }
    }

    private int miPower;

    private readonly Sprite[] maPowerSprites = new Sprite[UWClassicLayoutUw2.PowerGem.Frames];

    /// <summary>The gem's picture: 0 dark, the charge in steps, the full one pulses (set_screen_frame(3, n)).</summary>
    public void ShowPower(int piStep)
    {
        int liStep = Mathf.Clamp(piStep, 0, UWClassicLayoutUw2.PowerGem.FullStep);

        if (liStep == UWClassicLayoutUw2.PowerGem.FullStep && miPower != UWClassicLayoutUw2.PowerGem.FullStep)
            mfPowerFullSince = Time.unscaledTime;

        miPower = liStep;
    }

    /// <summary>
    /// The gem's picture now, the full one's pulse in it. THE PULSE STARTS WITH ITS FIRST PICTURE (UW2 beta gate 220.6, per
    /// user 2026-11-25: not always the first frame when full): adjust_power's frame goes back to 9 whenever the gem leaves
    /// the full step (last == 9), so each full charge shows 9 first, then 10, 9 ...; ours took the pulse from the running
    /// clock, so it began with either. Now counted from the moment the charge became full.
    /// </summary>
    public int PowerFrame => miPower == UWClassicLayoutUw2.PowerGem.FullStep
        ? miPower + ((int)((Time.unscaledTime - mfPowerFullSince) / UWClassicLayoutUw2.PowerGem.PulseSeconds) & 1) : miPower;

    /// <summary>When the charge last became full (ShowPower), the pulse's start.</summary>
    private float mfPowerFullSince;

    private void fUpdatePower()
    {
        fUpdateEyes();

        if (mOPowerGem == null)
            return;

        int liFrame = PowerFrame;

        if (maPowerSprites[liFrame] == null)
            maPowerSprites[liFrame] = fWholeSprite(fPicture(UWTexture.TextureTypes.POWER, liFrame, true));

        mOPowerGem.sprite = maPowerSprites[liFrame];
    }

    /// <summary>
    /// THE WEAPON (adjust_weapon, a step each eighth of a second): the fight icon draws it - steps 2, 1
    /// and 0 from 0x1B, then ready at 0x1B -, leaving the icon sheathes it - 0, 1 and 2, then hidden. The
    /// kind is taken while it is sheathed, and another kind in the hand sheathes the ready weapon and draws
    /// the new one (adjust_weapon's setting 4 with weapid != last_weap). A kind of none draws nothing.
    /// </summary>
    private void fUpdateWeapon()
    {
        fUpdatePower();

        if (meWeapon == WeaponStateEnum.Swinging)
        {
            fUpdateSwing();
            fShowWeapon();

            return;
        }

        // The modern scheme has no fight icon: R chooses the fight command there (fModernKeys); a modern
        // conversation puts the weapon away while it is open.
        bool lbFight = Command == CommandEnum.Fight && !(IsModernScheme && UWConversationScreenUw2.IsOpen);

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
        else if (meWeapon == WeaponStateEnum.Ready && (!lbFight || fKindInHand() != miWeaponKind))
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
        miWeaponKind = fKindInHand();
    }

    /// <summary>What the hand holds now: the inventory, which goes on from the record at every move (per user,
    /// 2026-11-01: read from the record, the weapon of the last load stayed whatever was put in the hand).</summary>
    private int fKindInHand()
    {
        UWPlayerFile lOPlayer = mOShownPlayer;

        mbWeaponLefty = lOPlayer != null && UWPlayerRecordUw2.LeftyBit(lOPlayer);

        int liSlot = UWClassicLayoutUw2.Weapon.RightHandSlot - (mbWeaponLefty ? 1 : 0);
        int liId = -1;

        if (mOInventory != null)
        {
            UWObject lOHeld = mOInventory.GetEquipped(UWInventoryModel.FromSavegameSlot(liSlot));

            liId = lOHeld != null ? lOHeld.ID : -1;
        }
        else if (lOPlayer != null)
        {
            liId = UWPlayerRecordUw2.SlotItemId(lOPlayer, liSlot);
        }

        if (liId >= 0 && liId < 0x10)
        {
            if (mOData.ObjectClassProperties != null && mOData.ObjectClassProperties.TryGetMeleeWeapon(liId, out UWObjectClassProperties.MeleeWeapon lOWeapon))
            {
                return Mathf.Clamp(lOWeapon.SkillType, UWClassicLayoutUw2.Weapon.FirstSkill, UWClassicLayoutUw2.Weapon.LastSkill)
                    - UWClassicLayoutUw2.Weapon.FirstSkill;
            }
        }
        else if (liId >= 0x10 && liId < 0x20 && (liId & 0xF) > 7)
        {
            return -1;
        }

        return UWClassicLayoutUw2.Weapon.FistKind;
    }

    /// <summary>The step's picture at its place in the view (do_fbuf_bms: weap_x, and 127 - weap_y).</summary>
    private void fShowWeapon()
    {
        UWWeaponAnimations lOAnimations = mOData.WeaponAnimations;
        int liRecord = ((mbWeaponLefty ? 0 : 1) * UWClassicLayoutUw2.Weapon.KindsPerHand) + miWeaponKind;

        if (meWeapon == WeaponStateEnum.Hidden || miWeaponKind < 0 || lOAnimations == null || lOAnimations.Uw2Records == null
            || liRecord >= lOAnimations.Uw2Records.Count)
        {
            fHideWeapon();

            return;
        }

        UWWeaponAnimations.Uw2Record lORecord = lOAnimations.Uw2Records[liRecord];
        int liStep = meWeapon == WeaponStateEnum.Swinging ? fSwingStep
            : meWeapon == WeaponStateEnum.Ready ? UWClassicLayoutUw2.Weapon.ReadyStep
            : UWClassicLayoutUw2.Weapon.ReadyStep + Mathf.Max(0, miWeaponFrame);
        int liPicture = lORecord.Steps[liStep] & UWClassicLayoutUw2.Weapon.PictureMask;

        if (liPicture >= lORecord.PictureCount)
        {
            fHideWeapon();

            return;
        }

        int liIndex = ((mbWeaponLefty ? 0 : 1) * UWClassicLayoutUw2.Weapon.PicturesPerHand) + (miWeaponKind * UWClassicLayoutUw2.Weapon.PicturesPerKind) + liPicture;
        Sprite lOPicture = fWeaponPicture(liIndex);

        // THE SWAY WHILE MOVING (jiggle_weapon, do_fbuf_bms's weap_x + wxo; per user 2026-11-22: ours stood still): the
        // first game's rule and setting (UWWeaponSway), only while the weapon stands ready.
        if (mOWeaponSway == null)
            mOWeaponSway = new UWWeaponSway(() => meWeapon == WeaponStateEnum.Ready);

        float lfX = lORecord.X[liPicture] + mOWeaponSway.Offset();

        fPlace(mOWeapon, lOPicture, lfX, UWClassicLayoutUw2.ViewOverlay.TopY - lORecord.Y[liPicture]);

        mOModernWeapon = lOPicture;
        mfModernWeaponX = lfX;
        miModernWeaponY = lORecord.Y[liPicture];
    }

    private UWWeaponSway mOWeaponSway;

    /// <summary>The weapon's picture of this frame and its place in the view (weap_x, weap_y from the view's bottom)
    /// for the modern interface's host (UWModernSessionUw2); null while none is drawn.</summary>
    private Sprite mOModernWeapon;

    private float mfModernWeaponX;

    private int miModernWeaponY;

    internal bool TryGetModernWeapon(out Sprite pOPicture, out float pfX, out int piY)
    {
        pOPicture = mOModernWeapon;
        pfX = mfModernWeaponX;
        piY = miModernWeaponY;

        return pOPicture != null;
    }

    /// <summary>A click on a flask for the modern interface's host: the classic flask's words (flask_info).</summary>
    internal void SayFlask(bool pbVitality)
    {
        if (mOShownPlayer != null)
            fSayFlask(pbVitality);
    }

    private void fHideWeapon()
    {
        mOWeapon.enabled = false;
        mOModernWeapon = null;
    }

    /// <summary>A WEAP.GR picture in WEAP.CM's colours for the body (load_weapcm): its sixteen indices
    /// through the set's palette 0 indices, 0 transparent.</summary>
    private Sprite fWeaponPicture(int piIndex)
    {
        int liSet = mOShownPlayer != null && UWPlayerRecordUw2.Body(mOShownPlayer) == UWClassicLayoutUw2.Weapon.SecondColourMapBody ? 1 : 0;
        // The sprites' xBR (UWSpriteXbr.WeaponPasses, per user 2026-11-22): the scaled pictures under keys of their own.
        int liPasses = UnderworldRevisited.Build.UWSpriteXbr.WeaponPasses;
        int liKey = (liPasses << 20) | (liSet << 16) | piIndex;

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

        if (liPasses > 0)
        {
            // Scaled, with as many pixels per unit as the scale: the layout (fPlace, the host's weapon frame) divides by
            // them and keeps the original's size.
            int liScale = 1 << liPasses;
            Texture2D lOScaled = fOwn(UWTextureFactory.Upload(UnderworldRevisited.Build.UWSpriteXbr.ScaleRgba(lyRgba, liWidth, liHeight, liPasses),
                liWidth * liScale, liHeight * liScale, FilterMode.Point, TextureWrapMode.Clamp, "UW2 weapon " + piIndex + " (xBR)"));

            lOSprite = Sprite.Create(lOScaled, new Rect(0f, 0f, lOScaled.width, lOScaled.height), new Vector2(0.5f, 0.5f), liScale);
        }
        else
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

            // The bubbles over the liquid's top, the glass over them (adjust_flasks).
            mOBubbles[liFlask] = fImage("Flask" + liFlask + "Bubbles", liLeft, 0f, 1f, 1f, null);
            mOBubbles[liFlask].enabled = false;
            mOBubbleGlass[liFlask] = fImage("Flask" + liFlask + "BubbleGlass", liLeft, 0f, 1f, 1f, null);
            mOBubbleGlass[liFlask].enabled = false;
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

        // THE ARMOUR IN DisplayInvSpecial's ORDER (ArmorSlots 1, 3, 5, 4, 2; UW2 beta gate 220.1, 2026-11-25, the user's
        // throne room pair: the leather vest lay over the mail cowl's hem, in the original the cowl hangs over the vest):
        // the helmet last. Until then the displays lay in their numbers' order, the helmet under the chest.
        int liFirstArmour = mODisplays[1].transform.GetSiblingIndex();

        for (int liAt = 0; liAt < UWClassicLayoutUw2.Inventory.ArmourDisplays.Length; liAt++)
            mODisplays[UWClassicLayoutUw2.Inventory.ArmourDisplays[liAt]].transform.SetSiblingIndex(liFirstArmour + liAt);

        mOOpenBag =fImageIn(mOInventoryPage, "OpenBag", fDisplayLeft(UWClassicLayoutUw2.Inventory.OpenBagDisplay),
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

        // Through the palette shader, so the palette rotation moves them as the original's (per user 2026-11-22: the
        // daylight icon stood still; UWIconPalette, as the first game's shelf).
        for (int liAt = 0; liAt < mOSpellImages.Length; liAt++)
        {
            mOSpellImages[liAt] = fImage("ActiveSpell" + liAt, UWClassicLayoutUw2.Runes.SpellX[liAt], fRow(UWClassicLayoutUw2.Runes.SpellY), 1f, 1f, null);
            UWIconPalette.Apply(mOSpellImages[liAt]);

            // Their top lies above the hollow, in the view's band: they move with the hollow (UWClassicWide).
            UWClassicWide.PlacedAs(mOSpellImages[liAt].rectTransform, UWClassicLayoutUw2.SpellHollow.Left, UWClassicLayoutUw2.SpellHollow.Top);
        }
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

                // In a byte, as parse_aspells adds it: the motion class's 0xFF plus the subclass wraps to 0 to 5.
                liPicture = (UWClassicLayoutUw2.Runes.SpellClassFirst[liWord & 0xF] + ((liWord & 0xF0) >> 4)) & 0xFF;
            }

            fPlace(mOSpellImages[liAt], liPicture < UWClassicLayoutUw2.Runes.NoSpellPicture ? fSpellIcon(liPicture) : null,
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

        // Another input mode holds the game: nothing, not even the clear strip (mous_in_rune, uw2 RUNES.C:99).
        if (liRelX < 0 || piX > UWClassicLayoutUw2.Statistics.AreaRight || liRelY < 0 || mOShownPlayer == null || IsInAnotherInputMode)
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
            fLookAtRune(liRune);
        else
            fAddRune(liRune);
    }

    /// <summary>mous_in_rune's right button: the runestone's look.</summary>
    private void fLookAtRune(int piRune)
    {
        int liId = UWClassicLayoutUw2.Runes.FirstRunestone + piRune;

        Say(UWItemDescriptions.DescribeScenery(UWText.RawObjectName(mOData, liId), null, new UWObject((ushort)liId), mOData));
    }

    /// <summary>mous_in_rune's left button: the rune onto the shelf - the first after a cast empties it, a fourth pushes
    /// the oldest off.</summary>
    private void fAddRune(int piRune)
    {
        if (msbCastSinceRune)
            fSetShelf(new[] { UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune }, 0);

        msbCastSinceRune = false;

        int[] liShelf = (int[])miShelf.Clone();
        int liCount = miShelfCount;

        if (liCount == 3)
        {
            liShelf[0] = liShelf[1];
            liShelf[1] = liShelf[2];
            liCount--;
        }

        liShelf[liCount++] = piRune;
        fSetShelf(liShelf, liCount);
    }

    /// <summary>
    /// GameInputMode IS NOT 0, where mous_in_rune and try_cast return at once (uw2 RUNES.C:99, :172; the first game's
    /// UWHudRunes.InAnotherInputMode): a thing on the pointer (1), a target spell or a thing used on another (2; in the
    /// modern scheme the first game's bag use mode holds the use, UWAttackUw2.AskUseOn), a missile spell waiting for
    /// its aim (3), the roaming view (8). Found at part U4c's survey (2026-11-23): until then a rune click and a cast
    /// acted in any mode on this game.
    /// </summary>
    public bool IsInAnotherInputMode => fHoldsInputMode || UWRoamingSight.IsAnyRunning;

    /// <summary>GameInputMode 1 to 3: a thing on the pointer, a target spell or a thing used on another, a missile
    /// spell (try_clear returns for these, not for the roaming view's 8).</summary>
    private bool fHoldsInputMode => (mOInventory != null && mOInventory.CursorItem != null)
        || (UWAttackUw2.Current != null && UWAttackUw2.Current.IsAnySpellWaiting)
        || (!IsClassicScheme && UWModernBags.Instance != null && UWModernBags.Instance.IsUsing);

    /// <summary>The record the runes and the shelf are read from stands (the modern rune panel, IUWModernMagic.IsReady).</summary>
    public bool ModernRunesReady => mOShownPlayer != null;

    /// <summary>The modern rune panel's rune click: the classic page's (mous_in_rune), not over a conversation.</summary>
    public void ModernAddRune(int piRune)
    {
        if (mOShownPlayer != null && !IsInAnotherInputMode && !UWConversationScreenUw2.IsOpen && UWPlayerRecordUw2.HasRune(mOShownPlayer, piRune))
            fAddRune(piRune);
    }

    public void ModernLookAtRune(int piRune)
    {
        if (mOShownPlayer != null && !IsInAnotherInputMode && UWPlayerRecordUw2.HasRune(mOShownPlayer, piRune))
            fLookAtRune(piRune);
    }

    /// <summary>The modern rune panel's hollow and the pad's cast: try_cast, not over a conversation.</summary>
    public void ModernCastShelf()
    {
        if (mOShownPlayer != null && !UWConversationScreenUw2.IsOpen)
            fCastShelf();
    }

    /// <summary>The action bar's spell: the sequence's runes through the same cast as the shelf's, the shelf and the
    /// flag of the last cast left as they are (the first game's UWHudRunes.CastSequence).</summary>
    public void ModernCastSequence(int piSequence)
    {
        if (mOShownPlayer != null && !UWConversationScreenUw2.IsOpen)
            fCastRunes((piSequence >> 10) & 0x1F, (piSequence >> 5) & 0x1F, piSequence & 0x1F, false);
    }

    /// <summary>A rune after a cast starts a new shelf (try_cast's flag): set by every try, kept over a restore.</summary>
    private static bool msbCastSinceRune;

    /// <summary>try_cast: the shelf's runes, packed, through the game's rule (UWSpellCasting.CastRunes). Not while
    /// another input mode holds the game (uw2 RUNES.C:172), nor paralysed.</summary>
    private void fCastShelf()
    {
        fCastRunes(miShelf[0], miShelf[1], miShelf[2], true);
    }

    private void fCastRunes(int piFirst, int piSecond, int piThird, bool pbFromShelf)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;
        UWWorld lOWorld = UWWorld.Of(UWScene.LevelLoader);

        if (lOPlayer == null || lOPlayer.Vitals == null || lOWorld == null || UWSleepUw2.Current == null || lOPlayer.IsParalysed
            || IsInAnotherInputMode)
            return;

        if (pbFromShelf)
            msbCastSinceRune = true;

        UWSpellCasting.CastRunes(piFirst, piSecond, piThird,
            new UWSpellHostUw2(lOPlayer, UWScene.LevelLoader, UWSleepUw2.Current.ScrollPresentation(lOWorld)));
        lOPlayer.WriteToRecord();
        fShowRunes();
    }

    /// <summary>An inventory change: the panel redraws, and every change but opening, closing and scrolling a
    /// container is a status pass of the original (as UWCharacter.fOnInventoryChangedForStatusPass) - armour, light,
    /// motion and stealth at once, a worn curse strikes (G0 of the 2026-11-06 sweep: up to a tick late, no curse).</summary>
    /// <summary>Something changed a carried thing in place (Mending's quality, part D2; per user 2026-11-12 the armour's
    /// picture stayed until the next change): the panel redraws and the status pass runs, as for any inventory change.</summary>
    public void InventoryChanged()
    {
        fOnInventoryChanged();
    }

    private void fOnInventoryChanged()
    {
        mbInventoryDirty = true;

        if (mOInventory != null && !mOInventory.IsQuietChange && UWVitalsUw2.Current != null)
            UWVitalsUw2.Current.RequestStatusPass();
    }

    /// <summary>The debug panel's spell list (Shift+F10) on the second game: the spell cast without circle, mana
    /// and check through this game's spell host, as the shelf casts (per user, 2026-11-06: Fly cast from the list
    /// went to the first game's idle character - it flew, but the record and the icons never had it).</summary>
    public bool CastSpellFromObject(int piSpellIndex)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;
        UWWorld lOWorld = UWWorld.Of(UWScene.LevelLoader);

        if (lOPlayer == null || lOPlayer.Vitals == null || lOWorld == null || UWSleepUw2.Current == null)
            return false;

        bool lbCast = UWSpellCasting.CastFromObject(piSpellIndex, false,
            new UWSpellHostUw2(lOPlayer, UWScene.LevelLoader, UWSleepUw2.Current.ScrollPresentation(lOWorld)));

        lOPlayer.WriteToRecord();
        fShowRunes();

        return lbCast;
    }

    /// <summary>try_clear: the icon's spell, from the right. Not while a thing is on the pointer, a target or a missile
    /// spell or a thing used on another waits (GameInputMode 1 to 3, uw2 RUNES.C:142; the roaming view's 8 lets it).</summary>
    private void fClickActiveSpell(int piIndex, bool pbRight)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;

        if (lOPlayer == null || lOPlayer.Vitals == null || piIndex < 0 || piIndex >= lOPlayer.Vitals.ActiveSpells.Count || fHoldsInputMode)
            return;

        if (pbRight)
        {
            Say(ActiveSpellLine(piIndex));

            return;
        }

        lOPlayer.Vitals.CancelActiveSpell(piIndex);
        lOPlayer.WriteToRecord();
        fShowRunes();
    }

    /// <summary>try_clear's words for a spell: its name (block 6 from 0x180 by parse_aspells' picture) and how long it
    /// has left (0x97 to 0x99 by its stability); empty for no spell there.</summary>
    public string ActiveSpellLine(int piIndex)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;

        if (lOPlayer == null || lOPlayer.Vitals == null || mOShownPlayer == null || piIndex < 0 || piIndex >= lOPlayer.Vitals.ActiveSpells.Count)
            return string.Empty;

        int liWord = UWPlayerRecordUw2.ActiveSpell(mOShownPlayer, piIndex);
        int liIcon = (UWClassicLayoutUw2.Runes.SpellClassFirst[liWord & 0xF] + ((liWord & 0xF0) >> 4)) & 0xFF;
        int liStability = lOPlayer.Vitals.ActiveSpells[piIndex].Stability;
        int liWords = liStability <= UWClassicLayoutUw2.Runes.NearlyDoneUpTo ? 0 : liStability <= UWClassicLayoutUw2.Runes.UnstableUpTo ? 1 : 2;

        return UWText.Block(mOData, UWClassicLayoutUw2.Runes.SpellNamesBlock, liIcon + UWClassicLayoutUw2.Runes.ActiveSpellNames + 1, string.Empty)
            + UWText.Block(mOData, 1, UWClassicLayoutUw2.Runes.StabilityWords + liWords + 1, string.Empty);
    }

    /// <summary>The first game's active spells' row: the left button names a spell (the classic page's right), the
    /// right button ends it (the classic page's left) - UWModernHudInput's order.</summary>
    public void ModernDescribeActiveSpell(int piIndex)
    {
        fClickActiveSpell(piIndex, true);
    }

    public void ModernCancelActiveSpell(int piIndex)
    {
        fClickActiveSpell(piIndex, false);
    }

    /// <summary>print_info: the status lines, not during a conversation (the classic compass's click).</summary>
    private void fSayStatus()
    {
        if (mOShownPlayer != null && !UWConversationScreenUw2.IsOpen && UWScene.LevelLoader != null)
        {
            foreach (string lsLine in UWStatusReportUw2.Lines(mOData, mOShownPlayer, UWScene.LevelLoader.CurrentLevelIndex + 1))
                Say(lsLine);
        }
    }

    /// <summary>The modern heading strip's click (IUWModernCharacter.ReportStatus).</summary>
    public void ModernReportStatus()
    {
        fSayStatus();
    }

    /// <summary>clear_shelf (Armageddon): the shelf empty, in the record and on the screen.</summary>
    public void ClearShelf()
    {
        fSetShelf(new[] { UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune }, 0);
    }

    /// <summary>The bag, the shelf and the spells' icons anew.</summary>
    public void ShowRuneState()
    {
        fShowRunes();
    }

    /// <summary>The shelf into the record (the save keeps it) and onto the screen.</summary>
    /// <summary>The runes in the hollow now, in order (the help's spell list marks the spell lying there).</summary>
    public IReadOnlyList<int> ShelfSelected
    {
        get
        {
            List<int> lORunes = new List<int>(miShelfCount);

            for (int liAt = 0; liAt < miShelfCount; liAt++)
                lORunes.Add(miShelf[liAt]);

            return lORunes;
        }
    }

    /// <summary>The help's spell list lays a spell's runes into the hollow, replacing what lay there (as the
    /// first game's UWHudRunes.PutOnShelf).</summary>
    public void PutOnShelf(IReadOnlyList<int> pORunes)
    {
        int[] liShelf = { UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune, UWPlayerRecordUw2.NoRune };
        int liCount = Mathf.Min(pORunes.Count, liShelf.Length);

        for (int liAt = 0; liAt < liCount; liAt++)
            liShelf[liAt] = pORunes[liAt];

        // A laid spell counts as a whole spell: the next rune clicked by hand starts a new shelf, as after a cast - the same
        // rule as the first game's UWRuneShelfModel.Put (per user 2026-11-26).
        msbCastSinceRune = true;
        fSetShelf(liShelf, liCount);
    }

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

    /// <summary>The character of the session's save onto the screen when it changes (a session loaded, saved).</summary>
    private void fUpdateCharacter()
    {
        UWGameSession lOSession = UWGameSession.Current;
        UWPlayerFile lOPlayer = lOSession != null ? lOSession.PlayerFileUw2 : null;

        if (lOPlayer == mOShownPlayer && mbCharacterShown == (lOPlayer != null))
        {
            if (mbInventoryDirty)
                fShowInventory();

            fFollowFlasks();

            return;
        }

        mOShownPlayer = lOPlayer;
        mbCharacterShown = lOPlayer != null;
        mOInventory = null;
        mOFlaskGauge = new UWFlaskGauge();

        if (lOPlayer != null)
        {
            UWInventoryWorkspace lOWorkspace = UWInventoryWorkspace.Read(lOPlayer);

            lOWorkspace.AssignPictures(mOData.Textures);
            mOWorkspaceRecords = lOWorkspace.Records;

            mOInventory = new UWInventoryModel(() => UWScene.LevelLoader != null ? UWScene.LevelLoader.UWDataImporter : null,
                () => UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevel : null);
            mOInventory.LoadFromSlots(UWPlayerRecordUw2.IsLeftHanded(lOPlayer), lOWorkspace.Equipment, lOWorkspace.Backpack,
                UWPlayerRecordUw2.Weight(lOPlayer));
            mOInventory.InventoryChanged += fOnInventoryChanged;
            mOInventory.LearnRuneStone = fLearnRune;
            mOInventory.CarriedLightChanged += fSetCarriedLight;
            mOInventory.LightBurnedOut += pOLight => UWObjectMechanics.Extinguish(pOLight, mOData.Textures);
            mOInventory.ApplyCarriedLight();

            for (int liAt = 0; liAt < miShelf.Length; liAt++)
                miShelf[liAt] = UWPlayerRecordUw2.ShelfRune(lOPlayer, liAt);

            miShelfCount = UWPlayerRecordUw2.ShelfCount(lOPlayer);
        }

        fShowRunes();

        fShowFlasks();

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
        fClickDisplay(fInventoryHit(piX, piY), pbRight);
    }

    /// <summary>What the object of a display is (the classic page's displays).</summary>
    public UWObject DisplayItem(int piDisplay)
    {
        return fDisplayItem(piDisplay);
    }

    /// <summary>A press on a display, the classic page's or a cell of the modern bags.</summary>
    private void fClickDisplay(int piHit, bool pbRight)
    {
        int liHit = piHit;

        if (mOInventory == null || liHit < 1 || liHit > UWClassicLayoutUw2.Inventory.DownArrowDisplay)
            return;

        UWObject lOItem = fDisplayItem(liHit);

        // An arrow moves the contents only while it is shown (ScrollItemsUp and ScrollItemsDown return
        // without InvUpArrow or InvDownArrow; per user, 2026-10-29: the hidden arrow's place scrolled on).
        // WITH A THING ON THE POINTER TOO (per user, 2026-11-16): inv_hit sends a held thing's click on display 21
        // and up to DoSpecialActions (uw2 inv/INVPANEL.C:253), and 0x15 and 0x16 scroll there; only the displays
        // below 21 put it down (RearrangeInventory).
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

        if (lOItem == null)
        {
            // toggle_fightmode works in the game's own mode only (inplist->mode 1), not over a conversation.
            if (UWClassicLayoutUw2.Inventory.DisplayToSlot[liHit] == WeaponHandSlot && !UWConversationScreenUw2.IsOpen)
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
        bool lbHeld = lOMouse != null && (mbInventoryPressRight ? UWPointer.Held(lOMouse.rightButton) : UWPointer.Held(lOMouse.leftButton));
        UWObject lOItem = fDisplayItem(miInventoryPressHit);

        if (lbHeld && !UWDragDistance.Crossed(mOInventoryPressAt, UWPointer.Position))
            return;

        mbInventoryPress = false;

        if (lOItem == null)
            return;

        if (lbHeld)
        {
            fTakeFrom(miInventoryPressHit, lOItem);

            return;
        }

        // mous_in_inv: the right button looks unless the Use icon is lit in the game's own mode, the left looks under
        // the Look icon; everything else is DoSpecialActions' (found with the mouse findings of 2026-11-18; ours looked on every right press).
        bool lbLook = mbInventoryPressRight
            ? Command != CommandEnum.Use || UWConversationScreenUw2.IsOpen
            : Command == CommandEnum.Look;

        if (lbLook)
        {
            fLookAtCarried(lOItem);

            return;
        }

        // DoSpecialActions' case 8 and 9: a weapon or a launcher in the weapon hand draws or sheathes it instead of a
        // use (per user 2026-11-18 the click on the drawn weapon did nothing); toggle_fightmode in the game's own mode.
        if (UWClassicLayoutUw2.Inventory.DisplayToSlot[miInventoryPressHit] == WeaponHandSlot
            && UWClickRules.WeaponHandTogglesFight(lOItem.ID))
        {
            if (!UWConversationScreenUw2.IsOpen)
                fChoose(CommandEnum.Fight);

            return;
        }

        fUseCarried(lOItem);
    }

    /// <summary>inv_look: the look at a carried thing - its look triggers, the sentences, and a book's or scroll's text.</summary>
    private void fLookAtCarried(UWObject pOItem)
    {
        if (pOItem == null)
            return;

        // inv_look: checkTrap with mode 5 before the look (G3 part H4).
        if (UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null)
            UWTriggerSystem.CheckTrapUw2(pOItem, UWTrapRulesUw2.LookMode, pOItem.TileX, pOItem.TileY, UWScene.LevelLoader,
                UWTrapRules.Setter.Player, UWVitalsUw2.Current.Vitals.GetSkill(UWPlayerData.Skill.Search));

        if (UWAttackUw2.Current != null)
            UWAttackUw2.Current.Look.AddItemLookMessages(pOItem);

        // SpecialLook's BookLook: a carried book or scroll is read with its look (the inventory's look has detail).
        ShowReading(pOItem, UWReadRulesUw2.Look(pOItem));
    }

    // ------------------------------------------------- What the first game's bag windows ask of this game
    // (IUWModernInventory through UWModernSessionUw2; UW2 plan, the modern interface's part U4b step 3, 2026-11-23).
    // Each is the rule the classic page runs, so nothing about using, looking, eating or dropping differs.

    /// <summary>A carried thing is used as from the classic page. False when nothing is used now.</summary>
    public bool ModernUse(UWObject pOItem)
    {
        if (pOItem == null || mOInventory == null || UWConversationScreenUw2.IsOpen || UWVitalsUw2.PlayerParalysed || UWVitalsUw2.PlayerAsleepInVoid)
            return false;

        fUseCarried(pOItem);

        return true;
    }

    public void ModernLook(UWObject pOItem)
    {
        fLookAtCarried(pOItem);
    }

    /// <summary>The pointer's thing dropped on the head (ItemFitsSlot): eaten if it can be.</summary>
    public bool ModernEatFromPointer()
    {
        return mOInventory != null && fEatFromPointer();
    }

    /// <summary>
    /// The pointer's thing leaves for the world, dropped in front or thrown by where the pointer is (ReturnObject).
    /// Not in a conversation (the windows the bags ask first). The press is the drop's, no blow follows from it. True when
    /// it left the pointer.
    /// </summary>
    public bool ModernDrop(Vector2 pOPointer)
    {
        if (mOInventory == null || mOInventory.CursorItem == null || UWConversationScreenUw2.IsOpen || UWWorldViewUw2.Current == null)
            return false;

        if (UWAttackUw2.Current != null)
            UWAttackUw2.Current.TakeDropPress();

        UWWorldViewUw2.Current.DropHeld(mOInventory, pOPointer);

        return mOInventory.CursorItem == null;
    }

    /// <summary>A carried container was opened in a bag window: UseObj's checkTrap with mode 4 and checkSpell after
    /// UseCont, as on the classic page (Felix's box makes food when it is opened).</summary>
    public void ModernContainerOpened(UWObject pOContainer)
    {
        if (pOContainer == null || UWConversationScreenUw2.IsOpen)
            return;

        fCheckCarriedTrap(pOContainer);
        fAfterCarriedCast(UWCarriedSpellRulesUw2.CheckSpell(pOContainer, null, fCastClock, fCastCarriedSpell));
    }

    /// <summary>The weapon is drawn or put away (toggle_fightmode), not over a conversation.</summary>
    public void ModernToggleFight()
    {
        if (!UWConversationScreenUw2.IsOpen)
            fChoose(CommandEnum.Fight);
    }

    /// <summary>The frame the bag windows last drew the pointer's thing themselves (IUWModernWorld.ShowPointerForBags):
    /// the screen's own pointer shows nothing then, the thing alone says where one points.</summary>
    private int miThingIsPointerFrame = -10;

    public void ModernThingIsPointer(bool pbThingAlone)
    {
        miThingIsPointerFrame = pbThingAlone ? Time.frameCount : -10;
    }

    private Texture2D mOClearPointer;

    /// <summary>A pointer picture with nothing on it.</summary>
    private Texture2D fClearPointer()
    {
        if (mOClearPointer == null)
        {
            mOClearPointer = new Texture2D(ClearPointerSize, ClearPointerSize, TextureFormat.RGBA32, false);
            mOClearPointer.name = "UW2 clear pointer";
            mOClearPointer.SetPixels32(new Color32[ClearPointerSize * ClearPointerSize]);
            mOClearPointer.Apply(false, false);
        }

        return mOClearPointer;
    }

    private const int ClearPointerSize = 16;

    /// <summary>The use (DoSpecialActions' default, UseObj): a bag opens in the pack's rows, the rune bag (class
    /// 0xF) slides the panel to the runes (OpenTheBag) - in the modern scheme it opens the first game's rune panel on
    /// its Runes tab (UWModernBags.OpenRunes, part U4c step 2).</summary>
    private void fUseCarried(UWObject pOItem)
    {
        if (pOItem.ID == UWObjectIds.RuneBag)
        {
            if (!IsClassicScheme)
                UWModernBags.OpenRunes(pOItem);
            else if (miSlideFrame == 0 && miPage != UWClassicLayoutUw2.Runes.PanelPage)
                fStartSlide(UWClassicLayoutUw2.Runes.PanelPage);
        }
        // UseCont, then UseObj's checkTrap with mode 4 and checkSpell, opened or locked (per user 2026-11-20: Felix's box,
        // enchanted with Create Food, makes food when it is opened in the original; ours only opened it).
        else if (pOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
        {
            mOInventory.TryOpenContainer(pOItem);
            fCheckCarriedTrap(pOItem);
            fAfterCarriedCast(UWCarriedSpellRulesUw2.CheckSpell(pOItem, null, fCastClock, fCastCarriedSpell));
        }
        else if (pOItem.ID == UWClassicLayoutUw2.Inventory.MapObject)
        {
            Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Inventory.UnrollString + 1, string.Empty));
            fOpenMap();
        }
        else if (pOItem.ID == UWClassicLayoutUw2.Inventory.BitOfMapObject)
            fUseMapScrap(pOItem);
        else if (UWFoodUw2.IsEatenOnUse(pOItem.ID))
            fEatCarried(pOItem);
        else if (pOItem.ID == UWSleepRules.BedrollObjectId && UWSleepUw2.Current != null)
            UWSleepUw2.Current.InBedroll();
        else if (UWObjectMechanics.IsAnyLight(pOItem.ID))
            UWItemUse.ToggleLight(pOItem, mOData, mOInventory, piString => Say(UWText.Block(mOData, 1, piString, string.Empty)),
                () => UWAudioService.PlayEffect(UWObjectVariantUw2.LightSound));
        // UseKey: a key or a lockpick asks what to use it on (G4 part A).
        else if (UWLockUseUw2.IsKeyOrLockpick(pOItem))
            UWLockUseUw2.Begin(pOItem, mOData, mOInventory);
        else if (UWCarriedSpellRulesUw2.IsWand(pOItem.ID))
            fUseWand(pOItem);
        // UseMagic: leeches, the instruments, the fishing pole, oil and rock hammer (G4 rest part 2), then UseObj's
        // checkTrap with mode 4.
        else if (UWToolUseUw2.Handles(pOItem.ID))
        {
            UWToolUseUw2.Use(pOItem, mOData, mOInventory, gameObject);
            fCheckCarriedTrap(pOItem);
        }
        // A broken wand does nothing, its traps included (UseWand).
        else if (UWCarriedSpellRulesUw2.IsBrokenWand(pOItem.ID))
        {
        }
        // UseBook: a book or scroll is read, an enchanted scroll cast (its own traps and spell; UseObj runs none after).
        else if (UWReadRulesUw2.IsReadable(pOItem.ID))
            fReadCarried(pOItem);
        // UseWatch and UseCrystal, then UseObj's traps and spell as for any other thing.
        else
        {
            if (pOItem.ID == UWReadRulesUw2.Pocketwatch)
                Say(UWText.Block(mOData, 1, UWReadRulesUw2.WatchString + 1, string.Empty)
                    + UWReadRulesUw2.WatchTime(UWPlayerRecordUw2.GameClock(UWGameSession.Current.PlayerFileUw2))
                    + UWText.Block(mOData, 1, UWReadRulesUw2.FullStopString + 1, string.Empty));
            else if (pOItem.ID == UWReadRulesUw2.StorageCrystal)
                Say(UWText.Block(mOData, 1, UWReadRulesUw2.CrystalString + 1, string.Empty) + UWReadRulesUw2.CrystalSignature(pOItem.Quality));

            // ANY OTHER THING (2026-11-18: until then "Not yet in the walk"): UseObj's checkTrap with mode 4 and checkSpell,
            // the spell cast on use that a ring or a jewel may hold; nothing else happens, nothing is said.
            fCheckCarriedTrap(pOItem);
            fAfterCarriedCast(UWCarriedSpellRulesUw2.CheckSpell(pOItem, null, fCastClock, fCastCarriedSpell));
        }
    }

    /// <summary>UseBook from the pack (UWReadRulesUw2.Use): the text or the cutscene; an enchanted scroll fires its traps,
    /// casts its spell and is used up once cast (using_punt).</summary>
    private void fReadCarried(UWObject pOBook)
    {
        UWReadRulesUw2.Reading lORead = UWReadRulesUw2.Use(pOBook, true);

        if (lORead.Kind != UWReadRulesUw2.ReadEnum.Spell)
        {
            ShowReading(pOBook, lORead);

            return;
        }

        fCheckCarriedTrap(pOBook);

        UWCarriedSpellRulesUw2.CastEnum leCast = UWCarriedSpellRulesUw2.CheckSpell(pOBook, null, fCastClock, fCastCarriedSpell);

        fAfterCarriedCast(leCast);

        if (leCast == UWCarriedSpellRulesUw2.CastEnum.Cast)
            UWToolUseUw2.UseUp(pOBook, mOInventory);
    }

    /// <summary>
    /// What a read shows (UWReadRulesUw2): a cutscene, or "You read the &lt;name&gt;..." and the text from
    /// block 3 (text 6 sets quest 106), or the text alone. The use from the pack and the look with detail (BookLook) both
    /// come here.
    /// </summary>
    public void ShowReading(UWObject pOBook, UWReadRulesUw2.Reading pORead)
    {
        switch (pORead.Kind)
        {
            case UWReadRulesUw2.ReadEnum.Cutscene:
                if (UWScene.IntroPlayer != null)
                    UWScene.IntroPlayer.PlayCutscene(pORead.Cutscene);
                break;

            case UWReadRulesUw2.ReadEnum.HeaderAndText:
                Say(UWItemMessages.ReadHeader(mOData, pOBook.ID));

                if (pORead.SetsQuest && UWGameSession.Current != null)
                    UWPlayerRecordUw2.SetQuest(UWGameSession.Current.PlayerFileUw2, UWReadRulesUw2.QuestOfText, 1);

                SayPaged(mOData.GetBookText(pORead.Text + 1));
                break;

            case UWReadRulesUw2.ReadEnum.TextAlone:
                SayPaged(mOData.GetBookText(pORead.Text + 1));
                break;
        }
    }

    /// <summary>
    /// A LOOK AT A THING IN THE TRADE AREA (BARTER.C: LookAt(obj, lore), with 1 + the Lore roll the session made): the
    /// look sentence at that detail, SpecialLook's line and a book's or scroll's reading (BookLook) - all through Say, so
    /// they stand together in the answer scroll (per user 2026-11-19; until then the trade slot said only "You see a
    /// scroll." through the first game's scenery sentence). No checkTrap: BARTER.C does not search, inv_look does.
    /// </summary>
    public static void LookInTrade(UWObject pOItem, int piDetail)
    {
        if (Current == null || pOItem == null || UWAttackUw2.Current == null || UWAttackUw2.Current.Look == null)
            return;

        Say(UWAttackUw2.Current.Look.DescribeInventoryItem(pOItem, piDetail));

        string lsSpecial = UWItemDescriptions.SpecialLookLine(pOItem, Current.mOData);

        if (lsSpecial != null)
            Say(lsSpecial);

        Current.ShowReading(pOItem, UWReadRulesUw2.Look(pOItem));
    }

    /// <summary>UseWand from the pack (G4, 2026-11-16): its traps (checkTrap with mode 4), the spell with the wait
    /// between casts, one charge, and the crack when the spell object is gone.</summary>
    private void fUseWand(UWObject pOWand)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;

        if (lOPlayer == null || lOPlayer.Vitals == null)
            return;

        fCheckCarriedTrap(pOWand);

        UWCarriedSpellRulesUw2.CastEnum leCast = UWCarriedSpellRulesUw2.UseWand(pOWand, null, fCastClock, fCastCarriedSpell,
            out bool lbCracked);

        fAfterCarriedCast(leCast);

        if (lbCracked)
        {
            Say(UWText.Block(mOData, 1, UWCarriedSpellRulesUw2.WandCracksString, string.Empty));
            UWObjectAppearance.Apply(pOWand, mOData.Textures, null);
            mOInventory.NotifyChanged();
        }
    }

    /// <summary>
    /// The clock nextSpellTime is measured on: the original's game_clock goes on every frame by the time since the last
    /// (check_physics, uw2 motion/PLAYMOVE.C:318, at most 0x40 a frame), 256 units a second, while ours steps a whole
    /// player tick, 21 seconds, at a time (UWGameClock), so the wait of 0x2FD units - about three seconds - lasted
    /// until the next step (per user, 2026-11-16: far more than three seconds). The game's own time at the
    /// same rate instead; it stands while the game is paused, as the original's clock does. Not saved, as nextSpellTime.
    /// </summary>
    private static int fCastClock => (int)(Time.time * UWPlayerTick.PitTicksPerSecond);

    /// <summary>checkTrap with mode 4 on a thing used from the pack.</summary>
    private void fCheckCarriedTrap(UWObject pOThing)
    {
        if (UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null)
            UWTriggerSystem.CheckTrapUw2(pOThing, UWTrapRulesUw2.UseMode, pOThing.TileX, pOThing.TileY, UWScene.LevelLoader,
                UWTrapRules.Setter.Player, UWVitalsUw2.Current.Vitals.GetSkill(UWPlayerData.Skill.Search));
    }

    /// <summary>The same for a thing in the world that casts on the player who used it (checkSpell with how 0).</summary>
    public bool CastOnPlayer(int piMajor, int piEffect)
    {
        return fCastCarriedSpell(piMajor, piEffect);
    }

    /// <summary>inanimate_spell with the player as caster and target: by class and minor for a major of 0 or more, by
    /// the spell's number for -1 (cast).</summary>
    private bool fCastCarriedSpell(int piMajor, int piEffect)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;
        UWWorld lOWorld = UWWorld.Of(UWScene.LevelLoader);

        if (lOPlayer == null || lOPlayer.Vitals == null || lOWorld == null || UWSleepUw2.Current == null)
            return false;

        UWSpellHostUw2 lOHost = new UWSpellHostUw2(lOPlayer, UWScene.LevelLoader, UWSleepUw2.Current.ScrollPresentation(lOWorld));

        return piMajor >= 0 ? UWSpellCasting.CastByClass(piMajor, piEffect, lOHost)
            : UWSpellCasting.CastFromObject(piEffect, false, lOHost);
    }

    /// <summary>After a cast from a carried thing: the refusal's line and sound, or the record and the runes' icons.</summary>
    private void fAfterCarriedCast(UWCarriedSpellRulesUw2.CastEnum peCast)
    {
        if (peCast == UWCarriedSpellRulesUw2.CastEnum.NotReady)
        {
            UWAudioService.PlayEffect(UWCarriedSpellRulesUw2.NotReadySound);
            Say(UWText.Block(mOData, 1, UWCarriedSpellRulesUw2.NotReadyString, string.Empty));

            return;
        }

        if (peCast != UWCarriedSpellRulesUw2.CastEnum.Cast || UWVitalsUw2.Current == null)
            return;

        UWVitalsUw2.Current.WriteToRecord();
        fShowRunes();
    }

    /// <summary>The inventory's burning light reaches the view (as UWInventory's on the first game).</summary>
    private static void fSetCarriedLight(int piObjectId)
    {
        if (UWScene.Lighting != null)
            UWScene.Lighting.SetCarriedLight(piObjectId);
    }

    /// <summary>The head's slot among the eleven of the paperdoll.</summary>
    private const int HeadSlot = 0;

    /// <summary>The meal, into the record at once: the record is what comes back before the next tick.</summary>
    private UWFoodUw2.OutcomeEnum fEat(UWObject pOFood)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;

        if (lOPlayer == null || lOPlayer.Vitals == null)
            return UWFoodUw2.OutcomeEnum.NotFood;

        UWFoodUw2.OutcomeEnum leOutcome = UWFoodUw2.Eat(pOFood, mOData, lOPlayer.Vitals, Say, fPassOut,
            piSound => UWAudioService.PlayEffect(piSound));

        lOPlayer.WriteToRecord();

        // UseFood's end: the spell in the meal or the potion with no wait before or after (nextSpellTime = 0), then its
        // traps (G4, 2026-11-16).
        if (leOutcome == UWFoodUw2.OutcomeEnum.Eaten)
        {
            // The dream plant outside the Void: the next night dreams the Void (UseFood, uw2 obj/USEITEMS.C:294; G6).
            if (pOFood.ID == UWVoidRulesUw2.DreamPlantId && UWGameSession.Current != null)
                UWVoidRulesUw2.EatDreamPlant(UWGameSession.Current.PlayerFileUw2, UWRandom.Next(UWOriginalRandom.Range));

            UWCarriedSpellRulesUw2.ClearWait();
            fAfterCarriedCast(UWCarriedSpellRulesUw2.CheckSpell(pOFood, null, fCastClock, fCastCarriedSpell));
            UWCarriedSpellRulesUw2.ClearWait();
            fCheckCarriedTrap(pOFood);
        }

        return leOutcome;
    }

    private static void fPassOut()
    {
        if (UWSleepUw2.Current != null)
            UWSleepUw2.Current.PassOut();
    }

    /// <summary>UseFood from the pack: a single piece turns into what is left of it where it lies; one off a stack
    /// leaves that on the pointer.</summary>
    private void fEatCarried(UWObject pOFood)
    {
        int liLeft = UWFoodUw2.LeftoverOf(pOFood.ID);

        if (fEat(pOFood) != UWFoodUw2.OutcomeEnum.Eaten)
            return;

        if (liLeft > 0 && !UWStackRules.IsStack(pOFood))
        {
            pOFood.ID = (ushort)liLeft;
            UWObjectAppearance.Apply(pOFood, mOData.Textures, null);
            mOInventory.NotifyChanged();

            return;
        }

        if (UWStackRules.TryTakeOne(pOFood))
            mOInventory.NotifyChanged();
        else
            mOInventory.RemoveItem(pOFood);

        if (liLeft > 0 && mOInventory.CursorItem == null)
            fPutOnPointer(liLeft);
    }

    private void fPutOnPointer(int piId)
    {
        UWObject lOItem = UWObjectFactory.Create(piId, mOData);

        UWObjectAppearance.Apply(lOItem, mOData.Textures, null);
        mOInventory.PickUpToCursor(lOItem);
    }

    /// <summary>Dropped on the head (ItemFitsSlot): a single thing that can be eaten is. True when the drop is over;
    /// too full, it stays on the pointer.</summary>
    private bool fEatFromPointer()
    {
        UWObject lOFood = mOInventory.CursorItem;

        if (lOFood == null || !UWFoodUw2.IsEatenFromHead(lOFood.ID) || UWStackRules.IsStack(lOFood))
            return false;

        int liLeft = UWFoodUw2.LeftoverOf(lOFood.ID);
        UWFoodUw2.OutcomeEnum leOutcome = fEat(lOFood);

        if (leOutcome == UWFoodUw2.OutcomeEnum.Eaten)
        {
            mOInventory.ReleaseCursorItem();

            if (liLeft > 0)
                fPutOnPointer(liLeft);
        }

        return leOutcome != UWFoodUw2.OutcomeEnum.NotFood;
    }

    // ------------------------------------------------- The map

    private UWMapScreenUw2 mOMap;

    private bool mbMapHeld;

    // ------------------------------------------------- The conversation

    private UWConversationScreenUw2 mOConversation;

    private bool mbConversationHeld;

    private bool mbModal;

    /// <summary>The options, the map and a conversation have the pointer: the modern scheme lets go the mouse
    /// look while one is up, as for the first game's conversation (UWControlScheme.IsConversationOpen).</summary>
    private void fFollowModal()
    {
        bool lbModal = mbOptionsOpen || UWConversationScreenUw2.IsOpen || UWMapScreenUw2.IsOpen;

        if (lbModal == mbModal || UWControlScheme.Shared == null)
            return;

        mbModal = lbModal;
        UWControlScheme.Shared.IsConversationOpen = lbModal;
    }

    /// <summary>The modern scheme's own screens are not on the second game yet: Escape opens the options
    /// panel of this screen (save, restore, music, detail). The HUD is the first game's (UWModernHud).</summary>
    private void fModernKeys()
    {
        UWControls lOControls = UWControlScheme.SharedControls;
        // Not while the bags' split box has the keys either: its digits are the count's, not the action bar's.
        bool lbFree = !UWSetupMenu.IsCapturingInput && !UWSetupMenu.HasOpenPanel && !UWModernBags.IsSplitting;
        bool lbHolding = mOInventory != null && mOInventory.CursorItem != null;

        fEnsureModernHud();

        if (UWModernUi.Session != mOModernSession)
            UWModernUi.Session = mOModernSession;

        // THE ROAMING VIEW (part D3): only the spells' icons take a click (the first game's row since part U4d step 2,
        // UWModernHudInput); Escape is UWRoamingSight's (UWBack) and must not open the options with the press that ended
        // the view. The right button still frees the pointer for the icons, and the HUD goes on (per user, 2026-11-12:
        // its icons stood as before the cast).
        if (UWRoamingSight.IsAnyRunning || UWRoamingSight.EndedByEscapeThisFrame)
        {
            if (UWModernPointer.IsFree || !UWPointer.IsLookLocked)
                fUpdatePointer();

            return;
        }

        // B AND C ARE THE BAG WINDOWS' since part U4b step 3 (2026-11-23), THE RUNE KEY THE RUNE PANEL'S since part
        // U4c step 2: the first game's windows run on this game and take their keys themselves (UWModernBags.Update,
        // UWModernRunePanel.Update), as each window of the first game brings its own input. H AND THE RIGHT BUTTON'S
        // POINTER SWITCH ARE THE HUD'S since part U4e step 1 (UWModernHud.Update), as on the first game.
        // ESCAPE as the first game's UI keys route it (UWGameUiKeys' CloseChain, then the menu): the first game's chain
        // of closing - a waiting spell lets go first (IUWModernWorld.CancelSpellAim), then the bags' use mode, the bags (a
        // thing on the pointer goes back), the character panel, the rune panel.
        // THE KEYS AS THE FIRST GAME'S (2026-11-25, the gamepad on UW2): Escape, Start and the pad's B through the back chain
        // (UWBack: ModernChain, then ModernMenu - the rows UWGameUiKeys routes on the first game); until then Escape alone,
        // read here, so Start and B neither closed the chain nor opened the menu. The bags' menu takes its own press first.
        bool lbChain = lbFree && UWBack.Takes(UWBackOwner.ModernChain);
        bool lbMenu = lbFree && !lbChain && UWBack.Takes(UWBackOwner.ModernMenu);

        if (lbChain || lbMenu)
        {
            if (lbChain && UWModernUi.CloseNext())
            {
            }
            // At the chain's end the first game's game menu (part U4d step 4; this game's options panel until then). It
            // refuses with a thing on the pointer or in the roaming view and says so (ModernRefusesMenu).
            else if (UWModernUi.Menu != null && UWModernUi.Has(UWModernPartEnum.GameMenu))
                UWModernUi.Menu.ToggleMenu();
            else if (!lbHolding)
                fBeginOptions();
        }

        // The cast key casts the shelf, as a click on the hollow does (magic stage 2 part A).
        if (lOControls != null && lbFree && !UWConversationScreenUw2.IsOpen && UWControlScheme.SharedControls[UWAct.KeyCast].WasPressedThisFrame())
            fCastShelf();

        // THE WEAPON ON R (per user, 2026-11-06; found 2026-11-05 against CONTROLS.md): as on the first game's
        // modern screen (UWModernWorldInput), R draws and puts away the weapon - the fight command, so
        // fUpdateWeapon's draw and sheathe steps run as on the classic icon; the gamepad's attack trigger
        // draws it too. Until then the weapon was always out in the modern scheme.
        if (lOControls != null && lbFree && !UWConversationScreenUw2.IsOpen)
        {
            UnityEngine.InputSystem.InputAction lOAttack = lOControls[UWAct.ModernAttack];
            bool lbPadDraws = Command != CommandEnum.Fight && lOAttack.WasPressedThisFrame()
                && UWGamepad.IsFromGamepad(lOAttack) && !UWInput.PadPointerTakes(lOAttack);

            if (lOControls[UWAct.ModernReady].WasPressedThisFrame() || lbPadDraws)
                fChoose(CommandEnum.Fight);
        }

        fFollowInventoryPress();

        // Nothing of this screen's own takes a click with the free pointer any more: the pack, the paperdoll, the action
        // bar, the runes and the active spells are the first game's windows' (the spells since part U4d step 2). A window
        // that frees the pointer itself (the debug panel's spell list, Shift+F10: per user 2026-11-12 the locked pointer
        // stayed hidden over it) shows it as the right button's free pointer does.
        if (UWModernPointer.IsFree || !UWPointer.IsLookLocked)
            fUpdatePointer();

        // The keys 1 to 0 are the first game's action bar's since part U4b step 4 (UWModernActionBar). The stopgap HUD
        // this screen ticked here (UWModernHudUw2) went in part U4e step 4: every piece of it is the first game's window.
    }

    /// <summary>The modern windows' host and the conversation's, made once (with the stopgap HUD until part U4e step 4).</summary>
    private void fEnsureModernHud()
    {
        if (mOModernSession != null)
            return;

        mOModernSession = new UWModernSessionUw2(this);

        UWModernConversation lOTalk = GetComponent<UWModernConversation>();

        if (lOTalk == null)
            lOTalk = gameObject.AddComponent<UWModernConversation>();

        lOTalk.SetHost(new UWModernConversationHostUw2(this));
    }

    /// <summary>The first game's bag windows are open (UWModernBags; this screen's own cells until part U4b step 3).
    /// No modern window of this screen is modal any more since part U4c step 2 (the rune page was, until the first
    /// game's rune panel took over): the world goes on beside the windows, as on the first game.</summary>
    public bool IsBagsOpen => UWModernBags.Instance != null && UWModernBags.Instance.IsOpen;

    /// <summary>Whether a screen point lies on a window of the modern scheme (UWModernUi.IsOverUi; this screen's own
    /// windows were asked too until the last of them, the active spells' row, went in part U4d step 2).</summary>
    public bool IsOverModernWindow(Vector2 pOPointer)
    {
        return UWModernUi.IsOverUi(pOPointer);
    }

    /// <summary>
    /// A thing came onto the pointer that the pack would not take, or a conversation began: the bag windows open and
    /// the pointer goes free, for it to be put down - as the first game's anvil frees the pointer for its target.
    /// </summary>
    public void OpenBags()
    {
        if (UWModernBags.Instance != null)
            UWModernBags.Instance.Open();

        if (!UWModernPointer.IsFree)
            UWModernPointer.Free();
    }

    public void CloseBags()
    {
        if (UWModernBags.Instance != null)
            UWModernBags.Instance.Close();
    }

    private static float msfLastSaid = -100f;

    /// <summary>The second game behind the modern windows' entry (IUWModernSession), made with the stopgap HUD and
    /// set on UWModernUi.Session whenever the modern scheme's frame runs here: the scene's UWGameUI makes the first
    /// game's host at its Start, which has nothing to answer with on this game.</summary>
    private UWModernSessionUw2 mOModernSession;

    /// <summary>TalkTo (phase 3 step 7): the thing pointed at answers, or the scroll says why not.</summary>
    public void TalkTo(UWEntityInfo pOEntity)
    {
        if (pOEntity == null || pOEntity.ObjectData == null)
        {
            Say(UWText.Block(mOData, 1, UWTalkRulesUw2.NothingFirstString + UWTalkRulesUw2.NothingTalk + 1, string.Empty));

            return;
        }

        UWObject lOThing = pOEntity.ObjectData;

        if (UWTalkRulesUw2.IsWisp(lOThing))
            lOThing = UWTalkRulesUw2.WispSpeaker();

        // TimeStop: no one answers while time stands (uw2 conv/CONVERSE.C:114; duplication audit K16, 2026-11-26 - it passed
        // false, so Freeze Time never kept a talk off).
        UWTalkRulesUw2.AnswerEnum leAnswer = UWTalkRulesUw2.Answer(lOThing, mOData.Conversations, UWCharacter.TimeIsFrozen,
            UWGameProfiles.Uw2.Conversation, out int liSlot);

        if (leAnswer == UWTalkRulesUw2.AnswerEnum.NotACreature || leAnswer == UWTalkRulesUw2.AnswerEnum.NoResponse)
        {
            Say(UWText.Block(mOData, UWTalkRulesUw2.RefusalBlock, (leAnswer == UWTalkRulesUw2.AnswerEnum.NotACreature
                ? UWTalkRulesUw2.NotACreatureString : UWTalkRulesUw2.NoResponseString) + 1, string.Empty));

            return;
        }

        if (mOConversation == null)
            mOConversation = gameObject.AddComponent<UWConversationScreenUw2>();

        if (mOConversation.Open(mOData, mOFrame, (UWNpc)lOThing, liSlot))
            fHoldForConversation(true);
    }

    private void fHoldForConversation(bool pbHeld)
    {
        if (mbConversationHeld == pbHeld)
            return;

        mbConversationHeld = pbHeld;

        // RightPanelSaved (uw2 conv/CONVERSE.C:178, :236): a conversation shows the inventory on the right, the page
        // before it comes back afterwards, drawn anew (per user, 2026-11-16: the statistics stood unchanged through
        // a trainer's lessons).
        if (pbHeld)
        {
            miPageBeforeTalk = miSlideFrame != 0 ? miTargetPage : miPage;
            fSetPageAtOnce(0);
        }
        else if (miPageBeforeTalk >= 0)
        {
            fSetPageAtOnce(miPageBeforeTalk);
            miPageBeforeTalk = -1;
        }

        if (pbHeld)
        {
            UWGameClock.Hold(UWGameClock.ConversationHold);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.HoldInput(OptionsInputHold);
        }
        else
        {
            UWGameClock.Release(UWGameClock.ConversationHold);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.ReleaseInput(OptionsInputHold);
        }
    }

    /// <summary>True while a conversation runs: it has the input, and the pointer is the plain one.</summary>
    private bool fConversationInput()
    {
        if (!UWConversationScreenUw2.IsOpen)
        {
            fHoldForConversation(false);

            return false;
        }

        mOConversation.Tick();

        // THE CLICK THAT ENDS A CONVERSATION IS NO LOOK (per user 2026-11-20 on the original: a right click on a book while
        // the conversation ends is taken by the end, which comes at once; ours showed the text first): while it closes, the
        // panels take no press.
        if (UWConversationScreenUw2.IsClosing)
        {
            mbInventoryPress = false;
            fUpdatePointer();

            return true;
        }

        fFollowInventoryPress();

        if (IsClassicScheme)
        {
            fClickPanel();
            fSlide();
        }
        else
        {
            fEnsureModernHud();
        }

        fUpdatePointer();

        return true;
    }

    /// <summary>The modern minimap's map: carried, and the automap on (UWAutomapStateUw2).</summary>
    public bool ModernCarriesMap => mOInventory != null && UWAutomapStateUw2.IsOn(mOShownPlayer)
        && mOInventory.FindCarried(lOItem => lOItem.ID == UWClassicLayoutUw2.Inventory.MapObject) != null;

    /// <summary>A click on the modern minimap: the big map, as the carried map's use opens it (without the unrolling's
    /// line, as the first game's minimap opens its map); false without the map.</summary>
    public bool ModernOpenMap()
    {
        if (!ModernCarriesMap || UWConversationScreenUw2.IsOpen)
            return false;

        fOpenMap();

        return UWMapScreenUw2.IsOpen;
    }

    /// <summary>The map over everything (phase 3 step 6): the game and its input stand while it lies
    /// open, as the screen changes in the original.</summary>
    private void fOpenMap()
    {
        if (mOMap == null)
            mOMap = gameObject.AddComponent<UWMapScreenUw2>();

        mOMap.Open(mOData);

        if (UWMapScreenUw2.IsOpen && !mbMapHeld)
        {
            mbMapHeld = true;
            UWGameClock.Hold(UWGameClock.OptionsHold);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.HoldInput(OptionsInputHold);
        }
    }

    /// <summary>UseBook's bit of a map, which needs the map itself: the sections it still holds go to the map,
    /// after that it opens the map on the level it covers (UWAutomapStateUw2).</summary>
    private void fUseMapScrap(UWObject pOScrap)
    {
        if (mOInventory.FindCarried(lOItem => lOItem.ID == UWClassicLayoutUw2.Inventory.MapObject) == null)
        {
            Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Inventory.NoMapString + 1, string.Empty));

            return;
        }

        UWGameSession lOSession = UWGameSession.Current;
        int liSections = pOScrap.Quantity & UWClassicLayoutUw2.Inventory.ScrapSectionsMask;

        if (liSections == 0)
        {
            UWAutomapStateUw2.OpenOnScrap(lOSession.PlayerFileUw2, pOScrap.Owner);
            fOpenMap();

            return;
        }

        UWSessionWorld lOWorld = lOSession.World;
        int liScrapIndex = pOScrap.Quality + UWClassicLayoutUw2.Inventory.ScrapFirstLevel - 1;
        int liLevelIndex = pOScrap.Owner - 1;
        UWLevel lOLevel = liLevelIndex >= 0 && liLevelIndex < lOWorld.Levels.Count ? lOWorld.Levels[liLevelIndex] : null;
        byte[] lyScrapMap = lOWorld.Ark.ReadBlock(UWLevArkFile.Uw2.AutomapBlock(liScrapIndex));

        pOScrap.Quantity = UWClassicLayoutUw2.Inventory.ScrapCopiedLink;
        Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Inventory.ScrapCopiedString + 1, string.Empty));

        // A level never entered has no automap block yet (DATA\LEV.ARK holds none): the scrap makes its map, as
        // SaveAutoMapLevel's put_arc makes the block in the original.
        if (lOLevel != null)
            lOLevel.EnsureAutomap();

        if (lOLevel == null || lOLevel.AutomapTiles == null || lyScrapMap.Length != lOLevel.AutomapTiles.Length)
        {
            Debug.LogWarning("[Map] A bit of a map for level " + pOScrap.Owner + " with page " + (liScrapIndex + 1) + ", which holds no map.");

            return;
        }

        int liExperience = UWAutomapStateUw2.CopyScrap(lyScrapMap, lOWorld.GetMapNotes(liScrapIndex), liSections, pOScrap.Owner,
            lOLevel.AutomapTiles, lOWorld.GetMapNotes(liLevelIndex));

        if (UWVitalsUw2.Current != null && UWVitalsUw2.Current.Vitals != null)
            UWVitalsUw2.Current.Vitals.ChangeExperience(liExperience, liLevelIndex + 1);
    }

    /// <summary>True while the map lies open; the session's M key opens it and closes it. PERFORMED, not pressed
    /// (2026-11-25, the gamepad on UW2), as the first game's rows take it (UWHotkeys): the pad's View tapped is the map,
    /// held the help - on its press the map opened under the help.</summary>
    private bool fMapInput()
    {
        UWControls lOControls = UWControlScheme.SharedControls;
        bool lbKey = lOControls != null && !UWSetupMenu.IsCapturingInput && !UWMapScreenUw2.IsWriting
            && !UWControls.IsShortcutModifierHeld && lOControls[UWAct.ToggleMap].WasPerformedThisFrame();

        if (UWMapScreenUw2.IsOpen)
        {
            if (lbKey)
                mOMap.Close();
        }
        else if (lbKey)
            fOpenMap();

        if (UWMapScreenUw2.IsOpen)
        {
            fMapPointer();

            return true;
        }

        if (mbMapHeld)
        {
            mbMapHeld = false;
            UWGameClock.Release(UWGameClock.OptionsHold);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.ReleaseInput(OptionsInputHold);
        }

        return false;
    }

    /// <summary>The map's pointer: the pen by its tip, the bottom-left corner, the eraser by its middle.</summary>
    private void fMapPointer()
    {
        Texture2D lOPicture = mOMap.PointerTexture(Mathf.Max(1, Mathf.FloorToInt(UWUiFit.Scale)));

        if (lOPicture != null)
            UWPointer.SetPicture(lOPicture, mOMap.IsErasing ? new Vector2(lOPicture.width * 0.5f, lOPicture.height * 0.5f) : new Vector2(0f, lOPicture.height));
    }

    /// <summary>The object of a display onto the pointer (SetCursorObj): from the paperdoll, the pack or the
    /// open bag; a stack asks how many first. A bag that is open stays where it is.</summary>
    private void fTakeFrom(int piHit, UWObject pOItem)
    {
        if (mOInventory.IsContainerOpen(pOItem))
            return;

        if (!AskCount(pOItem, piCount => fTakeCountFrom(piHit, pOItem, piCount)))
            fTakeCountFrom(piHit, pOItem, UWStackRules.CountAtLeastOne(pOItem));
    }

    /// <summary>Part of a stack is counted off, the stack stays where it is; the whole leaves its place.</summary>
    private void fTakeCountFrom(int piHit, UWObject pOItem, int piCount)
    {
        if (piCount < UWStackRules.CountAtLeastOne(pOItem) && mOInventory.TakeFromStack(pOItem, piCount))
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
        // THE REFUSALS SAID AS IN THE FIRST GAME (UW2 beta gate 221.3, per user 2026-11-25 with a picture of the original:
        // "The pack is too full.", "That item does not fit." - ours said nothing): ItemFitsSlot's sentences through the
        // shared rules and words (UWInventoryView.PutOneLevelUp, UWItemMessages.ContainerRejection, UWClassicDrop's).
        mOInventory.ForgetContainerRejection();

        if (piHit == UWClassicLayoutUw2.Inventory.OpenBagDisplay)
        {
            if (mOInventory.OpenContainer == null)
                return;

            // Into the bag around, or the pack's first free place ("There is no place to put that.", ItemFitsSlot 0x112).
            UWInventoryView.PutOutcomeEnum leUp = UWInventoryView.PutOneLevelUp(mOInventory);

            if (leUp == UWInventoryView.PutOutcomeEnum.Refused)
                fSayContainerRejection();
            else if (leUp == UWInventoryView.PutOutcomeEnum.NoPlace)
                Say(UWText.General(mOData, UWStringIds.InGame(UWGame.Uw2, UWInventoryView.NoPlaceMessage)));

            return;
        }

        if (piHit >= UWClassicLayoutUw2.Inventory.DisplayToSlot.Length)
            return;

        int liSlot = UWClassicLayoutUw2.Inventory.DisplayToSlot[piHit];
        int liPack = liSlot - UWInventoryWorkspace.EquipmentSlotCount;
        bool lbIntoBag = pOThere != null && pOThere.GetCategory() == UWObject.ObjectCategoryEnum.Containers && !mOInventory.IsContainerOpen(pOThere);

        if (liSlot == HeadSlot && fEatFromPointer())
            return;

        bool lbPlaced = true;

        if (liSlot < UWInventoryWorkspace.EquipmentSlotCount)
            lbPlaced = mOInventory.DropCursorItemInEquip(UWInventoryModel.FromSavegameSlot(liSlot));
        else if (lbIntoBag)
            lbPlaced = mOInventory.DropCursorItemIntoContainerItem(pOThere, mOInventory.OpenContainer != null);
        else if (mOInventory.OpenContainer != null)
            lbPlaced = mOInventory.DropCursorItemInContainer(liPack);
        else if (liPack < mOInventory.Backpack.Length)
            lbPlaced = mOInventory.DropCursorItemInBackpack(liPack);

        if (!lbPlaced)
            fSayContainerRejection();
    }

    /// <summary>Why a container refused the thing on the pointer, in the game's words (UWItemMessages.ContainerRejection,
    /// the first game's UWClassicDrop.ReportContainerRejection); nothing when no container refused it.</summary>
    private void fSayContainerRejection()
    {
        string lsMessage = UWItemMessages.ContainerRejection(mOInventory.LastContainerRejection, mOInventory.LastContainerRejectionContainer, mOData);

        if (!string.IsNullOrEmpty(lsMessage))
            Say(lsMessage);
    }

    /// <summary>What the statistics page shows of the numbers that move: experience, vitality, mana.</summary>
    private long mlStatsExperience = -1;

    private int miStatsVitals = -1;

    private int fStatsVitals => UWPlayerRecordUw2.Vitality(mOShownPlayer) | (UWPlayerRecordUw2.Mana(mOShownPlayer) << 8);

    /// <summary>The open page follows them (player_get_exp's redraw, panel_check_hpmp).</summary>
    private void fFollowExperience()
    {
        if (mOShownPlayer != null && mOStatisticsPage.gameObject.activeSelf
            && (UWPlayerRecordUw2.Experience(mOShownPlayer) != mlStatsExperience || fStatsVitals != miStatsVitals))
            fUpdateStatistics();
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
        mlStatsExperience = UWPlayerRecordUw2.Experience(lOPlayer);
        miStatsVitals = fStatsVitals;
        fSetText(mOStatExperience, leFont, (mlStatsExperience / 10).ToString(), liHeader, AlignEnum.Right, lfRight);

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

    /// <summary>What the flasks show, to see when it moved.</summary>
    private long mlFlasksShown = -1;

    /// <summary>The flasks follow the record (per user, 2026-11-02: drawn when a character came on screen only, they
    /// stayed full and red through blows and poison in the classic scheme; the modern HUD reads them every frame). THE
    /// SHARED GAUGE (UWFlaskGauge, 2026-11-25, UW2 beta gate 220.7, per user: the flasks did not bubble, and one path for
    /// both games): the shown levels step and the bubbles run on the original's slow update, as in the first game; the
    /// modern scheme reads the same gauge (FlaskGauge).</summary>
    private void fFollowFlasks()
    {
        UWPlayerFile lOPlayer = mOShownPlayer;
        bool lbMoved = lOPlayer != null && mOFlaskGauge.Advance(Time.deltaTime, UWPlayerRecordUw2.Vitality(lOPlayer),
            UWPlayerRecordUw2.MaxVitality(lOPlayer), UWPlayerRecordUw2.Mana(lOPlayer), UWPlayerRecordUw2.MaxMana(lOPlayer), false);

        if (lbMoved || fFlaskState() != mlFlasksShown)
            fShowFlasks();
    }

    /// <summary>The flasks' shown levels and bubbles, both schemes (fFollowFlasks); anew for each character, so a loaded
    /// one starts at its real level.</summary>
    internal UWFlaskGauge FlaskGauge => mOFlaskGauge;

    private UWFlaskGauge mOFlaskGauge = new UWFlaskGauge();

    private long fFlaskState()
    {
        UWPlayerFile lOPlayer = mOShownPlayer;

        if (lOPlayer == null)
            return 0;

        return UWPlayerRecordUw2.Vitality(lOPlayer) | ((long)UWPlayerRecordUw2.MaxVitality(lOPlayer) << 8) | ((long)UWPlayerRecordUw2.Mana(lOPlayer) << 16)
            | ((long)UWPlayerRecordUw2.MaxMana(lOPlayer) << 24) | ((UWPlayerRecordUw2.IsPoisoned(lOPlayer) ? 1L : 0L) << 32)
            | ((UWUserSettings.MarkPoison ? 1L : 0L) << 33);
    }

    private void fShowFlasks()
    {
        UWPlayerFile lOPlayer = mOShownPlayer;

        mlFlasksShown = fFlaskState();

        if (lOPlayer != null && mOFlaskGauge.ShownHealthLevel < 0)
            mOFlaskGauge.Advance(0f, UWPlayerRecordUw2.Vitality(lOPlayer), UWPlayerRecordUw2.MaxVitality(lOPlayer),
                UWPlayerRecordUw2.Mana(lOPlayer), UWPlayerRecordUw2.MaxMana(lOPlayer), false);

        fShowFlask(0, lOPlayer != null ? mOFlaskGauge.ShownHealthLevel : 0, lOPlayer != null ? mOFlaskGauge.HealthBubbleFrame : -1,
            lOPlayer != null && UWPlayerRecordUw2.IsPoisoned(lOPlayer) ? UWClassicLayoutUw2.Flasks.PoisonFirst : UWClassicLayoutUw2.Flasks.HealthFirst);
        fShowFlask(1, lOPlayer != null ? mOFlaskGauge.ShownManaLevel : 0, lOPlayer != null ? mOFlaskGauge.ManaBubbleFrame : -1,
            UWClassicLayoutUw2.Flasks.ManaFirst);
    }

    /// <summary>A flask at its twelfths (set_flask): the empty flask, then a slice for each twelfth filled; over the
    /// topmost the bubble picture piBubble (adjust_flasks, -1 none) with the glass's rows over it.</summary>
    private void fShowFlask(int piFlask, int piLevel, int piBubble, int piFirst)
    {
        int liLevel = Mathf.Clamp(piLevel, 0, UWClassicLayoutUw2.Flasks.Levels);
        int liLeft = piFlask == 0 ? UWClassicLayoutUw2.Flasks.HealthLeft : UWClassicLayoutUw2.Flasks.ManaLeft;

        mOFlaskEmpty[piFlask].enabled = mOFlaskEmpty[piFlask].sprite != null && mOShownPlayer != null;

        for (int liSlice = 0; liSlice < UWClassicLayoutUw2.Flasks.Levels; liSlice++)
        {
            // THE HATCH WHILE POISONED (the colour help's switch, UWUserSettings.MarkPoison; the first game's flask has
            // it since 2026-09-17, the second game's had none until 2026-11-23): the poisoned slices with every third
            // diagonal of their liquid in the hatch colour (UWFlasksUw2.HatchedSlice).
            bool lbHatch = piFirst == UWClassicLayoutUw2.Flasks.PoisonFirst && UWUserSettings.MarkPoison;
            Sprite lOSlice = liSlice >= liLevel ? null
                : lbHatch ? fHatchedSlice(liSlice) : fCachedPicture(UWTexture.TextureTypes.FLASKS, piFirst + liSlice);

            fPlace(mOFlaskSlices[piFlask][liSlice], lOSlice, liLeft, UWUiFit.FrameHeight - 1 - UWClassicLayoutUw2.Flasks.LiquidY[liSlice + 1]);
        }

        if (piBubble <= 0 || liLevel <= 0 || mOBubbles[piFlask] == null)
        {
            if (mOBubbles[piFlask] != null)
            {
                mOBubbles[piFlask].enabled = false;
                mOBubbleGlass[piFlask].enabled = false;
            }

            return;
        }

        float lfTop = UWUiFit.FrameHeight - 1 - UWClassicLayoutUw2.Flasks.LiquidY[liLevel];

        fPlace(mOBubbles[piFlask], fCachedPicture(UWTexture.TextureTypes.FLASKS, piFirst + UWClassicLayoutUw2.Flasks.BubbleOffset + piBubble), liLeft, lfTop);
        fPlace(mOBubbleGlass[piFlask], fBubbleGlass(UWFlasksUw2.SliceTop(liLevel - 1)), liLeft, lfTop);
    }

    // ------------------------------------------------- The flasks' bubbles (adjust_flasks, UW2 beta gate 220.7)

    private readonly Image[] mOBubbles = new Image[2];

    private readonly Image[] mOBubbleGlass = new Image[2];

    /// <summary>The glass over the bubbles at each level: the rows of FLASKS.GR 0x4C at the liquid's top.</summary>
    private readonly Dictionary<int, Sprite> mOBubbleGlassRows = new Dictionary<int, Sprite>();

    /// <summary>The glass's rows from piRow down (set_yoff), BubbleRows high and BubbleWidth wide, drawn transparent.</summary>
    private Sprite fBubbleGlass(int piRow)
    {
        if (!mOBubbleGlassRows.TryGetValue(piRow, out Sprite lOSprite))
        {
            Texture2D lOGlass = fPicture(UWTexture.TextureTypes.FLASKS, UWClassicLayoutUw2.Flasks.BubbleGlass, true);

            lOSprite = lOGlass != null && piRow >= 0 && piRow < lOGlass.height
                ? fSprite(lOGlass, 0, piRow, Mathf.Min(UWClassicLayoutUw2.Flasks.BubbleWidth, lOGlass.width),
                    Mathf.Min(UWClassicLayoutUw2.Flasks.BubbleRows, lOGlass.height - piRow))
                : null;
            mOBubbleGlassRows[piRow] = lOSprite;
        }

        return lOSprite;
    }

    /// <summary>A picture once per kind and number: the flasks' slices opaque, the rest drawn transparent.</summary>
    /// <summary>The active spells' pictures for the palette shader (SPELLS.GR, index 0 clear), made once each.</summary>
    private readonly Dictionary<int, Sprite> mOSpellIcons = new Dictionary<int, Sprite>();

    private Sprite fSpellIcon(int piIndex)
    {
        if (!mOSpellIcons.TryGetValue(piIndex, out Sprite lOSprite))
        {
            UWTexture lOPicture = mOData.Textures != null ? mOData.Textures.GetTextureByType(UWTexture.TextureTypes.SPELLS, piIndex) : null;

            lOSprite = lOPicture != null && lOPicture.Width > 0 && lOPicture.Height > 0
                ? fWholeSprite(fOwn(UWTextureFactory.Upload(UWUploadPixels.IconIndicesZeroClear(lOPicture), lOPicture.Width, lOPicture.Height,
                    FilterMode.Point, TextureWrapMode.Clamp, "UW2 spell icon " + piIndex, true)))
                : null;
            mOSpellIcons[piIndex] = lOSprite;
        }

        return lOSprite;
    }

    /// <summary>A poisoned slice with the hatch on it, made once each.</summary>
    private readonly Dictionary<int, Sprite> mOHatchedSlices = new Dictionary<int, Sprite>();

    private Sprite fHatchedSlice(int piSlice)
    {
        if (!mOHatchedSlices.TryGetValue(piSlice, out Sprite lOSprite))
        {
            UWTexture lOPicture = mOData.Textures != null ? UWFlasksUw2.HatchedSlice(mOData.Textures, UWClassicLayoutUw2.Flasks.PoisonFirst, piSlice) : null;

            lOSprite = lOPicture != null ? fWholeSprite(fPictureOf(lOPicture, false, "UW2 hatched slice " + piSlice)) : null;
            mOHatchedSlices[piSlice] = lOSprite;
        }

        return lOSprite;
    }

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
        Image lOPaperImage = UWUiKit.CreateFrameImage(mOFrame, "ScrollPaper", UWClassicLayoutUw2.MessageLog.Left, -UWClassicLayoutUw2.MessageLog.Top,
            UWClassicLayoutUw2.MessageLog.Width, UWClassicLayoutUw2.MessageLog.Height - 1, lOPaper);

        // As wide as the widened scroll (UWClassicWide).
        UWClassicWide.Widened(lOPaperImage.rectTransform);

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

        mOPowerGem = fImage("PowerGem", UWClassicLayoutUw2.PowerGem.Left, UWClassicLayoutUw2.PowerGem.Top, fWidth(lOGem), fHeight(lOGem), lOGem);

        Sprite lOEyes = fWholeSprite(fPicture(UWTexture.TextureTypes.EYES, 0, false));

        mOEyes = fImage("Eyes", UWClassicLayoutUw2.Eyes.Left, UWClassicLayoutUw2.Eyes.Top, fWidth(lOEyes), fHeight(lOEyes), lOEyes);
        msEyesRest = lOEyes;
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
    /// The picture paints the window in index 0 and edges it in index 1; the view shows in rows 17 to
    /// 143 (place_3d_view's window from 17, UWClassicLayoutUw2.Frame.RenderTop; the black row 16 above it
    /// stays, as in the original - UW2 beta gate 2026-11-25), the frame's line under it stays.</summary>
    private Texture2D fScreenWithView(UWTexture pOMain)
    {
        return fFollowColours(fOwn(UWTextureFactory.Upload(fScreenWithViewBytes(pOMain), pOMain.Width, pOMain.Height, FilterMode.Point,
            TextureWrapMode.Clamp, "UW2 game screen")), () => fScreenWithViewBytes(pOMain));
    }

    private static byte[] fScreenWithViewBytes(UWTexture pOMain)
    {
        byte[] lyRgba = UWUploadPixels.FromTexture(pOMain, UWColourVision.Filter);
        int liWidth = pOMain.Width;
        int liHeight = pOMain.Height;

        for (int liAt = 3; liAt < lyRgba.Length; liAt += UWUploadPixels.BytesPerPixel)
            lyRgba[liAt] = 255;

        for (int liRow = UWClassicLayoutUw2.Frame.RenderTop;
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

        return lyRgba;
    }

    // ------------------------------------------------- The colour help, followed in place

    /// <summary>
    /// THE CLASSIC SCREEN'S PICTURES FOLLOW THE COLOUR HELP AT ONCE (per user 2026-11-23: a change on the colour
    /// page showed only after the game was loaded again - the pictures are made once, with the help of that moment
    /// baked in). Every picture made through the help keeps how its bytes are made; when the help changes
    /// (UWColourVision.Version) the bytes are made again into the same texture (UWTextureFactory.Reupload, as the
    /// first game's UWGrTextures do), so every sprite on it shows the new colours without being placed again. The
    /// enlarged pointers are copies and are made again when next asked for.
    /// </summary>
    private readonly List<KeyValuePair<Texture2D, System.Func<byte[]>>> mOColourFollowers = new List<KeyValuePair<Texture2D, System.Func<byte[]>>>();

    private int miColoursShown = -1;

    private Texture2D fFollowColours(Texture2D pOTexture, System.Func<byte[]> pFBytes)
    {
        if (pOTexture != null)
            mOColourFollowers.Add(new KeyValuePair<Texture2D, System.Func<byte[]>>(pOTexture, pFBytes));

        return pOTexture;
    }

    private void fUpdateColours()
    {
        if (miColoursShown == UWColourVision.Version)
            return;

        bool lbFirst = miColoursShown < 0;

        miColoursShown = UWColourVision.Version;

        // Made with this version already.
        if (lbFirst)
            return;

        foreach (KeyValuePair<Texture2D, System.Func<byte[]>> lOFollower in mOColourFollowers)
        {
            if (lOFollower.Key == null)
                continue;

            byte[] lyRgba = lOFollower.Value();

            if (lyRgba != null && lyRgba.Length == lOFollower.Key.width * lOFollower.Key.height * UWUploadPixels.BytesPerPixel)
                UWTextureFactory.Reupload(lOFollower.Key, lyRgba);
        }

        fReleaseScaledCursors();
    }

    /// <summary>A picture of a .GR file in palette 0: opaque, or with index 0 cleared where the original
    /// draws it transparent (GRLIBL.ASM _5467: colour 0 is skipped).</summary>
    private Texture2D fPicture(UWTexture.TextureTypes peType, int piIndex, bool pbTransparent)
    {
        UWTexture lOPicture = mOData.Textures != null ? mOData.Textures.GetTextureByType(peType, piIndex) : null;

        if (lOPicture == null || lOPicture.PaletteIndices == null || lOPicture.Width <= 0 || lOPicture.Height <= 0)
            return null;

        return fPictureOf(lOPicture, pbTransparent, "UW2 " + peType + " " + piIndex);
    }

    /// <summary>A picture's texture through the colour help, which it follows (fFollowColours).</summary>
    private Texture2D fPictureOf(UWTexture pOPicture, bool pbTransparent, string psName)
    {
        return fFollowColours(fOwn(UWTextureFactory.Upload(fPictureBytes(pOPicture, pbTransparent), pOPicture.Width, pOPicture.Height, FilterMode.Point,
            TextureWrapMode.Clamp, psName)), () => fPictureBytes(pOPicture, pbTransparent));
    }

    private static byte[] fPictureBytes(UWTexture pOPicture, bool pbTransparent)
    {
        byte[] lyRgba = UWUploadPixels.FromTexture(pOPicture, UWColourVision.Filter);
        byte[] lyIndices = pOPicture.PaletteIndices;
        int liWidth = pOPicture.Width;
        int liHeight = pOPicture.Height;

        for (int liRow = 0; liRow < liHeight; liRow++)
        {
            for (int liColumn = 0; liColumn < liWidth; liColumn++)
            {
                int liIndex = (liRow * liWidth) + liColumn;
                bool lbClear = pbTransparent && liIndex < lyIndices.Length && lyIndices[liIndex] == 0;

                lyRgba[((((liHeight - 1 - liRow) * liWidth) + liColumn) * UWUploadPixels.BytesPerPixel) + 3] = lbClear ? (byte)0 : (byte)255;
            }
        }

        return lyRgba;
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

        // In the original's pixels: a scaled picture (the weapon with the sprites' xBR) has as many pixels per unit as its scale.
        UWUiKit.SetRect(pOImage.rectTransform, pfLeft, -pfTop, pOSprite.rect.width / pOSprite.pixelsPerUnit, pOSprite.rect.height / pOSprite.pixelsPerUnit);
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
    private static bool IsClassicScheme => !UWControlScheme.Ui.Policy.ClassicScreenOnlyUnderMap;

    /// <summary>The classic scheme, or what the modern one borrows from it: the options stand on this screen
    /// until the modern ones run on the second game (UW2 plan phase 3 step 8), as the first game's big map does
    /// in the modern scheme.</summary>
    private bool IsClassicShown => IsClassicScheme || mbOptionsOpen;

    /// <summary>The modern scheme is in force.</summary>
    public static bool IsModernScheme => !IsClassicScheme;

    private void Update()
    {
        fUpdateColours();

        // The key lock while typing (UWControls.IsTextEntryActive), which UWGameUI sets in the first game: the
        // second game's interface does not run it.
        // With the fields every game shares (G0 of the 2026-11-06 sweep, UWTextEntryState's list): a window's text
        // field on the kit, the help's keys, the debug tools' jump line and spell search - typing in them walked and
        // fired the game's keys.
        UWControls.SetTextEntryActive(miTypingSlot != 0 || UWMapScreenUw2.IsWriting || UWConversationScreenUw2.IsTyping
            || (mOCountAsk != null && mOCountAsk.IsAsking) || UWKitRows.IsTyping || UWHelpWindow.BlocksGameKeys
            || UWDebugTools.IsTyping || (mOYesNo != null && mOYesNo.IsOpen));

        // A cutscene has every input (IsCutscenePlaying): no command, no panel, no key, no pointer picture.
        if (IsCutscenePlaying)
            return;

        // [MORE] waits: nothing else runs (scroll_wait).
        if (fUpdatePaging())
        {
            fUpdatePointer();

            return;
        }

        if (fUpdateYesNo())
        {
            fUpdatePointer();

            return;
        }

        if (mOCountAsk != null)
        {
            bool lbAsked = mOCountAsk.IsAsking;

            mOCountAsk.Update();

            if (lbAsked)
            {
                fUpdatePointer();

                return;
            }
        }

        if (mOCanvasRoot == null)
        {
            // The panel is the classic screen's: it goes with it.
            fCloseOptions();

            return;
        }

        fFollowModal();

        if (mbOptionsOpen)
        {
            fOptionsInput();
            fUpdatePointer();

            return;
        }

        if (fConversationInput() || fMapInput())
            return;

        // The modern scheme's game menu is the first game's (part U4d step 4); the options panel is the classic scheme's.
        if (!IsClassicScheme && fModernMenuKeys())
            return;

        if (fOptionShortcuts())
            return;

        if (!IsClassicScheme)
        {
            fModernKeys();

            return;
        }

        // The modern scheme's free pointer does not outlive the switch to the classic one (UWModernPointer.Tick
        // releases it there on the first game).
        UWModernPointer.Release();

        if (fEndTargetSpellOnFrame())
        {
            fSlide();
            fUpdatePointer();

            return;
        }

        // ROAMING SIGHT'S GameInputMode 8 (part D3): the command bar (deal_with_icons) and the inventory (mous_in_inv)
        // take nothing while the view roams; fClickPanel keeps the flasks, the compass and the spells' icons.
        if (!UWRoamingSight.IsAnyRunning)
        {
            fChooseCommand();

            // mous_in_panel returns while paralysed (part E2) or asleep in the Void (G6).
            if (!UWVitalsUw2.PlayerParalysed && !UWVitalsUw2.PlayerAsleepInVoid)
                fFollowInventoryPress();
        }

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
    /// <summary>
    /// A TARGET SPELL ENDS BY A CLICK ON THE FRAME (per user on the original, 2026-11-12: a click left or right of the view
    /// lets it go; uw2 ui/INTERACT.C mous_in_inv's case 2 ends GameInputMode 2 by a click in the panel, the spell unpaid):
    /// either button outside the view window ends it, its mana unpaid, and the click does nothing else. True when it did.
    /// </summary>
    private bool fEndTargetSpellOnFrame()
    {
        UnityEngine.InputSystem.Mouse lOMouse = UWMouseButtons.GameMouse;

        if (UWAttackUw2.Current == null || !UWAttackUw2.Current.IsTargetSpellWaiting || lOMouse == null
            || !(UWPointer.Pressed(lOMouse.leftButton) || UWPointer.Pressed(lOMouse.rightButton)) || IsInViewWindow(UWPointer.Position))
            return false;

        // THE INVENTORY PAGE (mous_in_inv's case 2, part D2): a thing there takes the spell, the arrows page on and the
        // spell keeps waiting, an empty place ends it.
        if (miPage == 0 && miSlideFrame == 0 && mOInventory != null && TryGetOriginalPoint(UWPointer.Position, out float lfColumn, out float lfRow))
        {
            int liHit = fInventoryHit(Mathf.FloorToInt(lfColumn), (int)UWUiFit.FrameHeight - 1 - Mathf.FloorToInt(lfRow));

            if (liHit == UWClassicLayoutUw2.Inventory.UpArrowDisplay || liHit == UWClassicLayoutUw2.Inventory.DownArrowDisplay)
                return false;

            UWObject lOItem = liHit > 0 && liHit < UWClassicLayoutUw2.Inventory.UpArrowDisplay ? fDisplayItem(liHit) : null;

            if (lOItem != null && UWAttackUw2.Current.ApplyTargetToItem(lOItem))
                return true;
        }

        UWAttackUw2.Current.CancelSpellTarget();

        return true;
    }

    private void fClickPanel()
    {
        UnityEngine.InputSystem.Mouse lOMouse = UWMouseButtons.GameMouse;

        fHoldSkillScroll(lOMouse);

        if (lOMouse == null || !(UWPointer.Pressed(lOMouse.leftButton) || UWPointer.Pressed(lOMouse.rightButton))
            || !TryGetOriginalPoint(UWPointer.Position, out float lfColumn, out float lfRow))
            return;

        int liX = Mathf.FloorToInt(lfColumn);
        int liY = (int)UWUiFit.FrameHeight - 1 - Mathf.FloorToInt(lfRow);

        // flask_info (UWClickRules.FlaskAtUw2, the rule Classic Wide asks too).
        if (UWClickRules.IsInFlasksUw2(liX, liY))
        {
            UWClickRules.FlaskPart lePart = UWClickRules.FlaskAtUw2(liX - UWClassicLayoutUw2.FlaskClicks.Left,
                liY - UWClassicLayoutUw2.FlaskClicks.BottomY);

            if (lePart == UWClickRules.FlaskPart.Chain)
            {
                if (miSlideFrame == 0)
                    fStartSlide(miPage == 0 ? 2 : 0);
            }
            else if (lePart != UWClickRules.FlaskPart.None && mOShownPlayer != null && !UWConversationScreenUw2.IsOpen)
            {
                // Not during a conversation (per user on the original, 2026-10-31).
                fSayFlask(lePart == UWClickRules.FlaskPart.Vitality);
            }

            return;
        }

        // The compass (print_info), not during a conversation, as the flasks' words.
        if (liX >= UWClassicLayoutUw2.Compass.ClickLeft && liX <= UWClassicLayoutUw2.Compass.ClickRight
            && liY >= UWClassicLayoutUw2.Compass.ClickBottomY && liY <= UWClassicLayoutUw2.Compass.ClickTopY)
        {
            fSayStatus();

            return;
        }

        // The cast area over the shelf (try_cast): the right button does nothing there.
        if (liX >= UWClassicLayoutUw2.Runes.CastLeft && liX <= UWClassicLayoutUw2.Runes.CastRight
            && liY >= UWClassicLayoutUw2.Runes.CastBottomY && liY <= UWClassicLayoutUw2.Runes.CastTopY)
        {
            // Not while the view roams (try_cast returns while GameInputMode is set).
            if (mOShownPlayer != null && !UWConversationScreenUw2.IsOpen && !UWPointer.Pressed(lOMouse.rightButton)
                && !UWRoamingSight.IsAnyRunning && !UWVitalsUw2.PlayerParalysed)
                fCastShelf();

            return;
        }

        // The active spells' icons (try_clear): the right button says how stable, the left dispels.
        if (liX >= UWClassicLayoutUw2.Runes.ActiveLeft && liX <= UWClassicLayoutUw2.Runes.ActiveRight
            && liY >= UWClassicLayoutUw2.Runes.CastBottomY && liY <= UWClassicLayoutUw2.Runes.CastTopY)
        {
            if (mOShownPlayer != null)
                fClickActiveSpell(2 - ((liX - UWClassicLayoutUw2.Runes.ActiveLeft) >> 4), UWPointer.Pressed(lOMouse.rightButton));

            return;
        }

        // The pages take nothing while the view roams (mous_in_inv and mous_in_rune under GameInputMode 8) or the player
        // is paralysed or asleep in the Void (mous_in_panel returns).
        if (UWRoamingSight.IsAnyRunning || UWVitalsUw2.PlayerParalysed || UWVitalsUw2.PlayerAsleepInVoid)
            return;

        if (miPage == 0 && miSlideFrame == 0 && mOInventory != null)
        {
            fClickInventory(liX, liY, UWPointer.Pressed(lOMouse.rightButton));

            return;
        }

        if (miPage == UWClassicLayoutUw2.Runes.PanelPage && miSlideFrame == 0)
        {
            fClickRunes(liX, liY, UWPointer.Pressed(lOMouse.rightButton));

            return;
        }

        if (fScrollSkills(liX, liY))
        {
            mbSkillScrollHeld = true;
            mfSkillScrollTime = 0f;
        }
    }

    /// <summary>mous_in_stat's scroll strip: a row back left of the split, on right of it, within 0 to LastSkillTop. False
    /// when the point is not in the strip of the shown statistics page.</summary>
    private bool fScrollSkills(int piX, int piY)
    {
        if (miPage != 2 || miSlideFrame != 0 || mOShownPlayer == null || piX < UWClassicLayoutUw2.Statistics.Left
            || piX > UWClassicLayoutUw2.Statistics.AreaRight || piY < UWClassicLayoutUw2.Statistics.AreaBottomY
            || piY >= UWClassicLayoutUw2.Statistics.AreaBottomY + UWClassicLayoutUw2.Statistics.ScrollStrip)
            return false;

        int liTop = miSkillTop + (piX - UWClassicLayoutUw2.Statistics.Left < UWClassicLayoutUw2.Statistics.ScrollSplit ? -1 : 1);

        if (liTop >= 0 && liTop <= UWClassicLayoutUw2.Statistics.LastSkillTop)
        {
            miSkillTop = liTop;
            fUpdateSkillRows();
        }

        return true;
    }

    /// <summary>
    /// THE HELD BUTTON SCROLLS ON (UWClassicLayoutUw2.Statistics.HoldStepSeconds; UW2 beta gate 220.2, per user on the
    /// original 2026-11-25): while either button stays down after a click in the strip, a row every step wherever the
    /// pointer stands in the strip - out of it nothing, back in it goes on, as the original takes the held button as a new
    /// click each time.
    /// </summary>
    private void fHoldSkillScroll(UnityEngine.InputSystem.Mouse pOMouse)
    {
        if (!mbSkillScrollHeld)
            return;

        if (pOMouse == null || !(UWPointer.Held(pOMouse.leftButton) || UWPointer.Held(pOMouse.rightButton)))
        {
            mbSkillScrollHeld = false;

            return;
        }

        // The pages take nothing while the view roams or the player is paralysed or asleep in the Void (fClickPanel).
        if (UWRoamingSight.IsAnyRunning || UWVitalsUw2.PlayerParalysed || UWVitalsUw2.PlayerAsleepInVoid)
            return;

        mfSkillScrollTime += Time.deltaTime;

        while (mfSkillScrollTime >= UWClassicLayoutUw2.Statistics.HoldStepSeconds)
        {
            mfSkillScrollTime -= UWClassicLayoutUw2.Statistics.HoldStepSeconds;

            if (TryGetOriginalPoint(UWPointer.Position, out float lfColumn, out float lfRow))
                fScrollSkills(Mathf.FloorToInt(lfColumn), (int)UWUiFit.FrameHeight - 1 - Mathf.FloorToInt(lfRow));
        }
    }

    /// <summary>A click in the skill list's scroll strip whose button is still down (fHoldSkillScroll).</summary>
    private bool mbSkillScrollHeld;

    /// <summary>The time towards the held button's next row.</summary>
    private float mfSkillScrollTime;

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
        // A conversation keeps the inventory on the right (RightPanel 0 until free_converse; per user, 2026-11-16: the
        // chain still slid to the statistics).
        if (UWConversationScreenUw2.IsOpen)
            return;

        miTargetPage = piPage;
        miSlideFrame = 0;
        mfSlideTime = UWClassicLayoutUw2.PanelSlide.FrameSeconds;

        if (piPage == 2)
            fUpdateStatistics();

        fSlideStep();
    }

    /// <summary>The page shown before a conversation, -1 outside one.</summary>
    private int miPageBeforeTalk = -1;

    /// <summary>A page at once, without the slide (a conversation's RightPanel switch); the statistics drawn anew.</summary>
    private void fSetPageAtOnce(int piPage)
    {
        if (mOInventoryPage == null)
            return;

        miSlideFrame = 0;
        mfSlideTime = 0f;
        miPage = piPage;
        miTargetPage = piPage;

        RectTransform lOShown = fPageGroup(piPage);

        foreach (RectTransform lOPage in new[] { mOInventoryPage, mOStatisticsPage, mORunesPage })
        {
            if (lOPage != null && lOPage != lOShown)
                lOPage.gameObject.SetActive(false);
        }

        fShowPage(lOShown, 0);

        if (piPage == 2)
            fUpdateStatistics();
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
        bool lbDriving = !mbOptionsOpen && !UWConversationScreenUw2.IsOpen && lOMovement != null && lOMovement.Input != null && lOMovement.Input.IsCursorMovementInProgress;
        Vector2 lOAt = lbDriving ? UWPointer.ClampTo(lOWindow) : UWPointer.Position;
        UWCursors.CursorEnum leCursor = lbDriving ? meCursor : UWCursors.CursorEnum.Center;

        // The panel's pointer is the plain one everywhere (the view's regions are mous_player's, which the
        // options' loop does not call). The modern scheme has no regions: its free pointer keeps the crosshair.
        if (IsClassicScheme && !mbOptionsOpen && !UWConversationScreenUw2.IsOpen && lOWindow.width > 0f && lOWindow.height > 0f && lOAt.x >= lOWindow.xMin && lOAt.x <= lOWindow.xMax
            && lOAt.y >= lOWindow.yMin && lOAt.y <= lOWindow.yMax)
        {
            int liX = Mathf.Clamp(Mathf.FloorToInt((lOAt.x - lOWindow.xMin) / lOWindow.width * CursorAreaWidth), 0, CursorAreaWidth - 1);
            int liY = Mathf.Clamp(Mathf.FloorToInt((lOAt.y - lOWindow.yMin) / lOWindow.height * CursorAreaHeight), 0, CursorAreaHeight - 1);

            CursorAreaX = liX;
            CursorAreaY = liY;
            leCursor = RegionCursor(liX, liY);
        }

        meCursor = leCursor;

        // THE RED X OF USE, LOOK AND GET (deal_with_icons' force_mouse_cursor(0x1077), uw2 ui/INTERACT.C; per user
        // 2026-11-13): with one of the three lit on the command bar the pointer is the X everywhere, the view's arrows
        // included (a forced pointer stands over the regions, MOUSE.C), until the mode ends. Talk and Fight keep the
        // arrows. What forces a pointer after it - the aim, a target spell, a thing on the pointer - lies over it.
        if (IsClassicScheme && !mbOptionsOpen
            && (Command == CommandEnum.Use || Command == CommandEnum.Look || Command == CommandEnum.Get))
            leCursor = UWCursors.CursorEnum.Use;

        // A drawn bow aims (force_mouse_cursor(0x1075), the picture a waiting spell has), and so does a waiting
        // missile spell (do_spell's GameInputMode 3).
        if (UWAttackUw2.Current != null && (UWAttackUw2.Current.IsAiming || UWAttackUw2.Current.IsSpellWaiting))
            leCursor = UWCursors.CursorEnum.Aim;

        // A target spell's pointer (nail_1area's force_mouse_cursor(0x1076)).
        if (UWAttackUw2.Current != null && UWAttackUw2.Current.IsTargetSpellWaiting)
            leCursor = UWCursors.CursorEnum.Enchant;

        // An object on the pointer is the pointer (SetCursorObj), and so is a key or a lockpick waiting for its lock
        // (UseThing's force_mouse_cursor(item), G4 part A).
        UWObject lOUseOn = UWAttackUw2.Current != null ? UWAttackUw2.Current.UseOnItem : null;
        // THE BAG WINDOWS DRAW THE THING THEMSELVES in the modern scheme (UWModernBagDrag.LayoutPointerItem, part U4b
        // step 3): while they do, this pointer shows nothing, or the thing would stand there twice.
        // So do they the thing of their use mode (a key waiting for its lock, UWAttackUw2.AskUseOn).
        bool lbBagsDrawThing = !IsClassicScheme && mOInventory != null && Time.frameCount - miThingIsPointerFrame <= 1;
        Texture2D lOPicture = lbBagsDrawThing ? fClearPointer()
            : mOInventory != null && mOInventory.CursorItem != null
                ? fCursorObjectPicture(mOInventory.CursorItem.ID)
                : lOUseOn != null ? fCursorObjectPicture(lOUseOn.ID) : fScaledCursor((int)leCursor);

        // The layout editor's arrows over all of it, as on the first game (UWPointerCursor.TryShowEditorPointer).
        if (!IsClassicScheme && UWPointerCursor.TryShowEditorPointer())
            return;

        if (lOPicture != null)
            UWPointer.SetPicture(lOPicture, new Vector2(lOPicture.width * 0.5f, lOPicture.height * 0.5f));
    }

    /// <summary>
    /// An object's picture on the pointer AS LARGE AS IN THE PACK (per user 2026-11-18: a dragged thing looked smaller
    /// than where it was put down): the frame's rows are UWUiFit.Scale screen pixels tall and its columns that times the
    /// pixel factor (5/6 with the 4:3 picture), while the pointer's own arrows keep their whole-number scale. Kept while
    /// the size stays the same.
    /// </summary>
    private Texture2D fCursorObjectPicture(int piId)
    {
        float lfRow = UWUiFit.Scale;
        float lfColumn = UWUiFit.Scale * UWClassicScreen.HorizontalPixelFactor;
        Sprite lOSprite = fCachedPicture(UWTexture.TextureTypes.OBJECTS, piId);

        if (lOSprite == null)
            return null;

        int liWidth = Mathf.Max(1, Mathf.RoundToInt(lOSprite.texture.width * lfColumn));
        int liHeight = Mathf.Max(1, Mathf.RoundToInt(lOSprite.texture.height * lfRow));
        long llKey = ((long)liWidth << 40) | ((long)liHeight << 20) | (long)piId;

        if (mlCursorObjectKey == llKey && mOCursorObject != null)
            return mOCursorObject;

        if (mOCursorObject != null)
            Destroy(mOCursorObject);

        mOCursorObject = ScaledCopy(lOSprite.texture, liWidth, liHeight, "UW2 pointer object " + piId);
        mlCursorObjectKey = llKey;

        return mOCursorObject;
    }

    /// <summary>A picture resized to piWidth by piHeight, nearest pixel.</summary>
    public static Texture2D ScaledCopy(Texture2D pOSource, int piWidth, int piHeight, string psName)
    {
        Color32[] lyFrom = pOSource.GetPixels32();
        Color32[] lyTo = new Color32[piWidth * piHeight];

        for (int liY = 0; liY < piHeight; liY++)
        {
            int liFromY = Mathf.Min(pOSource.height - 1, liY * pOSource.height / piHeight);

            for (int liX = 0; liX < piWidth; liX++)
                lyTo[(liY * piWidth) + liX] = lyFrom[(liFromY * pOSource.width) + Mathf.Min(pOSource.width - 1, liX * pOSource.width / piWidth)];
        }

        return UWTextureFactory.FromColours(lyTo, piWidth, piHeight, FilterMode.Point, TextureWrapMode.Clamp, psName);
    }

    /// <summary>A picture enlarged by a whole number, pixel for pixel.</summary>
    public static Texture2D ScaledCopy(Texture2D pOSource, int piScale, string psName)
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
            {
                // Unity gives the picture back before it goes (UWPointer.ReleasePicture).
                UWPointer.ReleasePicture(lOScaled);
                Destroy(lOScaled);
            }
        }

        mOScaledCursors = null;
    }

    /// <summary>Whether a screen point lies in the view's window on the classic screen - where a press
    /// of the left button starts the walk (UWPlayerInput).</summary>
    /// <summary>How high in the view a point stands, 0 the bottom.</summary>
    public float ViewHeightOf(Vector2 pOScreen)
    {
        return mOCamera != null ? Mathf.InverseLerp(mOCamera.pixelRect.yMin, mOCamera.pixelRect.yMax, pOScreen.y) : 0.5f;
    }

    /// <summary>How far across the view a point stands, 0 the left.</summary>
    public float ViewAcrossOf(Vector2 pOScreen)
    {
        return mOCamera != null ? Mathf.InverseLerp(mOCamera.pixelRect.xMin, mOCamera.pixelRect.xMax, pOScreen.x) : 0.5f;
    }

    /// <summary>A point in player_settr's window (UWClassicLayoutUw2.MissileAim): Y from the bottom.</summary>
    public void PointerInMissileWindow(Vector2 pOScreen, out int piX, out int piY)
    {
        piX = Mathf.Clamp(Mathf.FloorToInt(ViewAcrossOf(pOScreen) * UWClassicLayoutUw2.Frame.ViewWindowWidth), 0, UWClassicLayoutUw2.MissileAim.MaxX);
        piY = Mathf.Clamp(Mathf.FloorToInt(ViewHeightOf(pOScreen) * UWClassicLayoutUw2.Frame.ViewWindowHeight)
            + UWClassicLayoutUw2.MissileAim.RowsUnderView, 0, UWClassicLayoutUw2.MissileAim.MaxY);
    }

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

        // The projection follows the field of view again; the classic views shift it to the original's centre below.
        mOCamera.ResetProjectionMatrix();

        // The hidden interface (H) comes back by the HUD's rule since part U4e step 1 (UWModernHud.Update: the classic
        // screen, a window, a conversation, the map, typing, the free pointer), as on the first game.

        if (!lbShown)
        {
            // No columns put in while the classic screen is not shown (the help's slide reads them, UWHelpLayout).
            UWClassicWide.Update(false, false);

            // The classic pointer goes with the screen (the modern scheme looks with the mouse).
            if (mbShown)
                UWPointer.SetPicture(null, Vector2.zero);

            // CLASSIC WIDE (UW2 plan stage U7 step 2): the camera in the frame's view window, widened, at the
            // original's vertical angle over the window's rows - the hole is shown at the CRT's proportion already, so
            // the area's own aspect; the projection's centre is the window's middle, as the classic screen has it
            // (UWModernClassicFrame.ViewCentre). Else the whole screen at the modern view's angle.
            if (UWModernClassicFrame.IsActive)
            {
                Rect lOArea = UWModernClassicFrame.CameraRect;

                mOCamera.rect = new Rect(lOArea.x / Screen.width, lOArea.y / Screen.height, lOArea.width / Screen.width, lOArea.height / Screen.height);
                mOCamera.ResetAspect();
                mOCamera.fieldOfView = UWViewAngle.Vertical(mOCamera.aspect, OriginalVerticalAngle);
                fToProjectionCentre(UWClassicLayoutUw2.Frame.ViewWindowTop, UWClassicLayoutUw2.Frame.ViewWindowHeight);
            }
            else
            {
                mOCamera.rect = new Rect(0f, 0f, 1f, 1f);
                mOCamera.ResetAspect();
                mOCamera.fieldOfView = UWViewAngle.Vertical(mOCamera.aspect, mfModernFov);
            }

            mbShown = false;

            // The character is read from the record and the inventory made here, for the modern HUD too, and the
            // weapon runs here as well: the modern blow waits for it, and the HUD draws it (ShowWeapon).
            fUpdateCharacter();
            fUpdateWeapon();

            return;
        }

        mbShown = true;

        // THE WIDESCREEN BOX (stage U8 step 1): the columns put in, the first game's rule (UWClassicWide.Update; none while
        // the conversation screen or the map shows - they keep the original's 320, as the first game's conversation frame and
        // map); the frame as wide, its picture widened by this game's cuts.
        UWClassicWide.Update(true, UWConversationScreenUw2.IsOpen || UWMapScreenUw2.IsOpen);
        fEnsureWideScreen(UWClassicWide.Extra);
        mOFrame.sizeDelta = new Vector2(UWUiFit.FrameWidthUnits + UWClassicWide.Extra, UWUiFit.FrameHeight);

        float lfFactor = UWClassicScreen.HorizontalPixelFactor;
        float lfScale = UWUiFit.Scale;
        float lfSide = fLeftUnits(lfFactor);
        float lfVertical = Mathf.Max(0f, (UWUiFit.CanvasHeight - UWUiFit.FrameHeight) / 2f);

        // THE HELP SLIDES THE FRAME TO THE LEFT as in the first game (per user 2026-11-18: the second game's help lay
        // over the picture): squeezed to 4:3 by the pixel factor, moved by UWHelpLayout.FrameShift.
        float lfShift = UWHelpLayout.FrameShift(UWUiFit.CanvasWidth);

        mOFrame.localScale = new Vector3(lfFactor, 1f, 1f);
        mOFrame.anchoredPosition = new Vector2(lfShift, mOFrame.anchoredPosition.y);
        mOLeftBar.sizeDelta = new Vector2(lfSide, mOLeftBar.sizeDelta.y);
        mORightBar.sizeDelta = new Vector2(Mathf.Max(0f, fSideUnits(lfFactor) - lfShift), mORightBar.sizeDelta.y);
        mOTopBar.sizeDelta = new Vector2(mOTopBar.sizeDelta.x, lfVertical);
        mOBottomBar.sizeDelta = new Vector2(mOBottomBar.sizeDelta.x, lfVertical);

        // The view's window in screen pixels (origin bottom left), as UWClassicGeometry places the
        // first game's hole.
        float lfLeft = (lfSide * lfScale) + (UWClassicLayoutUw2.Frame.ViewWindowLeft * lfScale * lfFactor);
        float lfBottom = UWUiFit.BottomMarginPixels
            + ((UWUiFit.FrameHeight - UWClassicLayoutUw2.Frame.ViewWindowTop - UWClassicLayoutUw2.Frame.ViewWindowHeight) * lfScale);
        // Widened by the columns put in (its left edge is the view band's first cut: it stays).
        float lfWidth = (UWClassicLayoutUw2.Frame.ViewWindowWidth + UWClassicWide.Extra) * lfScale * lfFactor;
        float lfHeight = UWClassicLayoutUw2.Frame.ViewWindowHeight * lfScale;

        mOCamera.rect = new Rect(lfLeft / Screen.width, lfBottom / Screen.height, lfWidth / Screen.width, lfHeight / Screen.height);

        // The aspect of the CRT's pixels, not the window's (UWViewportCamera.Apply).
        mOCamera.ResetAspect();

        float lfAspect = mOCamera.aspect * UWClassicScreen.VgaPixelAspectCorrection / lfFactor;

        mOCamera.aspect = lfAspect;
        mOCamera.fieldOfView = UWViewAngle.Vertical(lfAspect, OriginalVerticalAngle);
        fToProjectionCentre(UWClassicLayoutUw2.Frame.ViewWindowTop, UWClassicLayoutUw2.Frame.ViewWindowHeight);

        fUpdateCharacter();
        fFollowExperience();
        fUpdateWeapon();
        fUpdateScroll();
        fUpdateCompass();
        fUpdateCommandIcon();
    }

    /// <summary>
    /// THE ORIGINAL'S PROJECTION CENTRE (UWClassicLayoutUw2.Frame.ProjectionCentreRow, UW2 beta gate 2026-11-25) instead of
    /// the camera area's middle, as the first game's view (UWViewportCamera.fToProjectionCentre): the content moved down by
    /// the difference in rows (OpenGL convention: ndc = p * pos / -z - p[i, 2]). Across, the area's middle is the
    /// original's centre already (biasx = (right + left + 1) / 2 over the 0xD0 columns, the area widened on both sides
    /// alike).
    /// </summary>
    private void fToProjectionCentre(int piTop, int piRows)
    {
        float lfDown = UWClassicLayoutUw2.Frame.ProjectionCentreRow - (piTop + (piRows * 0.5f));
        Matrix4x4 lOProjection = mOCamera.projectionMatrix;

        lOProjection[1, 2] += 2f * lfDown / piRows;
        mOCamera.projectionMatrix = lOProjection;
    }

    /// <summary>The bar beside the frame in canvas units.</summary>
    private static float fSideUnits(float pfFactor)
    {
        return Mathf.Max(0f, (UWUiFit.CanvasWidth - ((UWUiFit.FrameWidthUnits + UWClassicWide.Extra) * pfFactor)) / 2f);
    }

    /// <summary>
    /// The frame's picture for the columns put in (stage U8 step 1): the screen with its view cut out at 0, else this game's
    /// frame widened (UWClassicWideBuilder: the right block moved whole, the left part grown, the view's window clear),
    /// made anew when the width changes, through the colour help as the screen's.
    /// </summary>
    private void fEnsureWideScreen(int piExtra)
    {
        if (mOScreenImage == null || piExtra == miScreenExtra)
            return;

        miScreenExtra = piExtra;

        if (mOWideScreen != null)
        {
            Destroy(mOWideScreen);
            mOWideScreen = null;
        }

        if (piExtra <= 0 || mOData.Textures == null)
        {
            mOScreenImage.sprite = mOScreenSprite;
            return;
        }

        int liExtra = piExtra;
        System.Func<byte[]> lFBytes = () => UWUploadPixels.FromPicture(UWClassicWideBuilder.Build(mOData.Textures, liExtra).Picture, UWColourVision.Filter);
        int liWidth = UWClassicLayout.Screen.Width + liExtra;

        mOWideScreen = fFollowColours(UWTextureFactory.Upload(lFBytes(), liWidth, UWClassicLayout.Screen.Height, FilterMode.Point,
            TextureWrapMode.Clamp, "UW2 game screen widened " + liExtra), lFBytes);
        mOScreenImage.sprite = fWholeSprite(mOWideScreen);
    }

    private Image mOScreenImage;

    /// <summary>The screen's own sprite, the original's 320 columns.</summary>
    private Sprite mOScreenSprite;

    private Texture2D mOWideScreen;

    private int miScreenExtra;

    /// <summary>The frame's left edge from the canvas' left, with the help's slide (UWHelpLayout.FrameShift).</summary>
    private static float fLeftUnits(float pfFactor)
    {
        return Mathf.Max(0f, fSideUnits(pfFactor) + UWHelpLayout.FrameShift(UWUiFit.CanvasWidth));
    }

    /// <summary>The classic scroll's width for the text: the original's, longer in the widened frame (UW2 plan stage U8
    /// step 2, the first game's UWHudMessageLog.fLogWidth).</summary>
    private static int ClassicLogWidth => UWClassicLayoutUw2.MessageLog.Width + UWClassicWide.Extra;

    /// <summary>The text, wrapped and scrolled by the first game's model; the edges turn a frame with
    /// every scroll (draw_edges).</summary>
    private void fUpdateScroll()
    {
        UWFont lOFont = mOData.Fonts != null ? mOData.Fonts.Get(UWFonts.FontType.Normal) : null;

        if (lOFont == null || mOLogText == null)
            return;

        int liCapacity = UWMessageLogModel.CapacityOf(lOFont, UWClassicLayoutUw2.MessageLog.Height);

        // The description's box blinks after the typed line (wdialog's cursor block).
        bool lbCursor = (miTypingSlot != 0 || mbCountLine) && UWMessageLogModel.IsCursorOn(Time.unscaledTime - mfTypingStart);

        if (!mOLog.Update(lOFont, msMessages, ClassicLogWidth, liCapacity, msPendingPage.Count > 0, lbCursor, mlScrollSaid, mlScrollCleared))
            return;

        if (mOLog.Scrolled > 0)
            fApplyEdges();

        UWTextLabel.Get(mOLogText).SetText(lOFont, mOLog.ShownLines, mOLog.Width, fLogColours(),
            UWTextRaster.ColoursEnum.Codes, false, FilterMode.Point, mOLog.CursorOn);

        fDrawMore(lOFont);
    }

    private RawImage mOMoreText;

    /// <summary>scroll_more's "[MORE]" at the start of the scroll's last row, in colour 0x12, while a text waits.</summary>
    private void fDrawMore(UWFont pOFont)
    {
        if (!mOLog.MoreShown)
        {
            if (mOMoreText != null)
                UWTextLabel.Hide(mOMoreText);

            return;
        }

        if (mOMoreText == null)
        {
            GameObject lOObject = new GameObject("ScrollMore", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mOLogText.transform.parent, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0f, 1f);

            mOMoreText = lOObject.GetComponent<RawImage>();
            mOMoreText.raycastTarget = false;
        }

        UWPalette lOPalette = mOData.Palettes != null ? mOData.Palettes.GetPalette(0) : null;
        UWColor32 lOColour = lOPalette != null ? lOPalette.GetUWColor(MoreColourIndex) : new UWColor32(255, 255, 255, 255);
        string lsMore = UWMessageLogModel.MoreMarkerText;

        UWTextLabel.Get(mOMoreText).SetLine(pOFont, lsMore, UWTextRaster.TightWidth(pOFont, lsMore),
            new Color32(lOColour.R, lOColour.G, lOColour.B, 255), false, FilterMode.Point, FontStyle.Normal, UWTextLabel.MoreMarkerRaise);

        if (UWTextLabel.Get(mOMoreText).Picture == null)
            return;

        ((RectTransform)mOMoreText.transform).anchoredPosition = new Vector2(UWClassicLayoutUw2.MessageLog.Left,
            -(UWClassicLayoutUw2.MessageLog.Top + (mOLog.MoreRowShown * (pOFont.Height + UWTextLayout.LineSpacing))));
        mOMoreText.transform.SetAsLastSibling();
        UWTextLabel.Get(mOMoreText).Show();
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

    /// <summary>
    /// The face by heading and 3, the needle by heading at nedl_x/nedl_y (set_compass) - the heading in sixteen steps as the
    /// first game's compass reads the view. ITS TURN (UW2 beta gate, per user on the original 2026-11-25: after the Void it
    /// stepped back into place, not at once): the heading is only the compass's goal (display_scr's set_screen_frame(2)),
    /// which it follows one step the short way on each slow update (adjust_compass), and in the Ethereal Void the goal is
    /// not set at all - the compass stands still there. The first picture is the heading itself.
    /// </summary>
    private void fUpdateCompass()
    {
        Camera lOView = UWViewpoint.Current;

        if (lOView == null || mOCompassPictures == null)
            return;

        int liLevel = UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevelIndex + 1 : 1;

        if ((liLevel - 1) / UWClassicLayoutUw2.Compass.LevelsPerWorld != UWClassicLayoutUw2.Compass.StillWorld || miCompassGoal < 0)
            miCompassGoal = UWHeadings.HeadingByteToCompassStep(UWHeadings.DegreesToHeadingByte(lOView.transform.eulerAngles.y));

        int liStep = miCompassStep < 0 ? miCompassGoal : miCompassStep;

        mfCompassTime += Time.deltaTime;

        if (liStep == miCompassGoal)
            mfCompassTime = 0f;

        while (liStep != miCompassGoal && mfCompassTime >= UWClassicLayoutUw2.Compass.StepSeconds)
        {
            mfCompassTime -= UWClassicLayoutUw2.Compass.StepSeconds;

            int liAhead = (miCompassGoal - liStep + 16) & 0xF;

            liStep = (liStep + (liAhead <= 8 ? 1 : -1) + 16) & 0xF;
        }

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

        // The keys alone, as the first game's hotkey rows want them (G0 of the 2026-11-06 sweep: Shift+F1, the
        // debug overlay, opened the options too, Alt+F4 chose Look). F7 pulls the chain and F8 casts, as UW2.EXE
        // binds them (game/PLAYER.C: pull_chain, try_cast); the modern scheme's F8 is fModernKeys'.
        if (lOControls != null && !UWSetupMenu.IsCapturingInput && UWHotkeyHost.HeldMods == UWHotkeyMods.None)
        {
            if (lOControls[UWAct.KeyPanel].WasPressedThisFrame())
            {
                if (miSlideFrame == 0)
                    fStartSlide(miPage == 0 ? 2 : 0);
            }
            else if (lOControls[UWAct.KeyCast].WasPressedThisFrame() && !UWConversationScreenUw2.IsOpen)
                fCastShelf();
            else if (lOControls[UWAct.KeyFight].WasPressedThisFrame())
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

        UnityEngine.InputSystem.Mouse lOMouse = UWMouseButtons.GameMouse;

        if (lOMouse == null || !UWPointer.Pressed(lOMouse.leftButton))
            return;

        if (!TryGetOriginalPoint(UWPointer.Position, out float lfColumn, out float lfRow))
            return;

        // get_iconreg_button (UWClickRules.CommandIconAtUw2, the rule Classic Wide asks too).
        int liIcon = UWClickRules.CommandIconAtUw2(lfColumn, lfRow);

        if (liIcon >= 0 && liIcon <= (int)CommandEnum.Options)
            fChoose((CommandEnum)liIcon);
    }

    /// <summary>punt_fightmode (water_set, G2): the weapon is put away - the fight command ends.</summary>
    public static void PuntFightMode()
    {
        if (Command == CommandEnum.Fight)
            Command = CommandEnum.None;
    }

    private void fChoose(CommandEnum peCommand)
    {
        // AN OBJECT ON THE POINTER: no icon answers (deal_with_icons returns while GameInputMode is set, INTERACT.C:647) -
        // per user in the original 2026-11-17, with a blackrock gem on the pointer every icon stayed still. Until then
        // ours held back the options icon alone.
        if (mOInventory != null && mOInventory.CursorItem != null)
            return;

        // Paralysed, asleep in the Void or swimming, fight is not chosen and NO command stays lit: the old one was
        // unselected before the test sets the mode to none (deal_with_icons, uw2 ui/INTERACT.C:668-678; duplication audit
        // U14, 2026-11-26 - ours returned and left the old command lit).
        if (peCommand == CommandEnum.Fight && (UWVitalsUw2.PlayerParalysed || UWVitalsUw2.PlayerAsleepInVoid
            || (UWScene.PlayerTerrain != null && UWScene.PlayerTerrain.IsSwimming)))
        {
            Command = CommandEnum.None;

            return;
        }

        // The options icon opens the panel's game page.
        if (peCommand == CommandEnum.Options)
        {
            fBeginOptions();
            mOOptions.Open(UWOptionsPanelUw2.GroupEnum.Game);
            fShowOptions();

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
    /// game stands (UWGameClock.OptionsHold, as the first game's panel) and the session's input waits (a hold
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
        {
            UWControlScheme.Shared.HoldInput(OptionsInputHold);
            UWControlScheme.Shared.HoldUiModal(OptionsModalHold);
        }
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
        {
            UWControlScheme.Shared.ReleaseInput(OptionsInputHold);
            UWControlScheme.Shared.ReleaseUiModal(OptionsModalHold);
        }
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

        // The scheme's shortcut modifier (UWControls.IsShortcutModifierHeld: the modern right Ctrl, the classic
        // Ctrl) - G0 of the 2026-11-06 sweep: the modern scheme's left Ctrl sinks, and sinking with S, R, D, Q, F or
        // M opened option pages.
        if (lOControls == null || lOKeys == null || UWSetupMenu.IsCapturingInput || !UWControls.IsShortcutModifierHeld)
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

        // do_option_shortcut refuses while GameInputMode is set: an object on the pointer, the roaming view.
        if ((mOInventory != null && mOInventory.CursorItem != null) || UWRoamingSight.IsAnyRunning)
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

        if (lOMouse != null && !mbOptionsPress && !UWMouseButtons.IsBlocked
            && (UWPointer.Pressed(lOMouse.leftButton) || UWPointer.Pressed(lOMouse.rightButton) || lOMouse.middleButton.wasPressedThisFrame))
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

        bool lbHeld = pOMouse != null && (UWPointer.Held(pOMouse.leftButton) || UWPointer.Held(pOMouse.rightButton) || pOMouse.middleButton.isPressed);

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
    /// the first game's panel opens it), the session left for the main menu (quit, yes: the port leaves to
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
                UWGameStartUw2.Leave();
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

    // ------------------------------------------------- The first game's game menu on this game (part U4d step 4)

    /// <summary>do_option_shortcut's and the options icon's refusal: a thing on the pointer or the roaming view (GameInputMode)
    /// - "You cannot select options partway through an action.".</summary>
    public bool ModernRefusesMenu()
    {
        if ((mOInventory == null || mOInventory.CursorItem == null) && !UWRoamingSight.IsAnyRunning)
            return false;

        Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.NotNowString + 1, string.Empty));

        return true;
    }

    /// <summary>check_save (uw2 GAMESCR.C:178): not while GameInputMode is set, string 0xAE.</summary>
    public bool ModernRefusesToSave()
    {
        if (!IsInAnotherInputMode)
            return false;

        Say(UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.NotNowString + 1, string.Empty));

        return true;
    }

    public string ModernSaveSlotName(int piSlot)
    {
        return UWSavegameSlots.GetName(SavegameRoot, piSlot);
    }

    public bool ModernHasSave(int piSlot)
    {
        return UWSavegameSlots.Exists(SavegameRoot, piSlot);
    }

    /// <summary>SaveGame: an empty description becomes " ", as the panel's; null when it went well.</summary>
    public string ModernWriteSave(int piSlot, string psName)
    {
        msiLastSlot = piSlot;

        string lsDescription = string.IsNullOrEmpty(psName) ? " " : psName;

        if (lsDescription.Length > UWClassicLayoutUw2.Options.DescriptionLength)
            lsDescription = lsDescription.Substring(0, UWClassicLayoutUw2.Options.DescriptionLength);

        return UWWorldViewUw2.Save(piSlot, lsDescription);
    }

    public void ModernReportSave(string psError)
    {
        if (psError != null)
            Debug.LogWarning("[Session] Not saved: " + psError);

        fSayOutcome(psError == null ? OutcomeSaved : OutcomeSaveFailed);
    }

    public void ModernReportNoSave()
    {
        fSayOutcome(OutcomeNoSave);
    }

    /// <summary>RestoreGame from the slot, as the panel's restore (its screen then says "Restore Game Complete.").</summary>
    public void ModernLoadSave(int piSlot)
    {
        string lsRoot = SavegameRoot;

        if (!UWSavegameSlots.Exists(lsRoot, piSlot))
        {
            fSayOutcome(OutcomeNoSave);

            return;
        }

        msiLastSlot = piSlot;
        msAfterStart = UWText.Block(mOData, 1, UWClassicLayoutUw2.Options.FirstOutcomeString + OutcomeRestored + 1, string.Empty);
        UWGameStartUw2.Request(UWSavegameSlots.GetSlotPath(lsRoot, piSlot));
    }

    public void ModernQuit()
    {
        UWGameStartUw2.Leave();
    }

    /// <summary>
    /// THE GAME MENU'S KEYS IN THE MODERN SCHEME, as the first game's UI keys route them (UWHotkeys group 2 and 3,
    /// UWGameUiKeys; that host does not run on this game): with the menu open the back press and F1 close it or go back
    /// to its main page, and nothing else of this screen takes a key; closed, F1 opens it, the shortcut modifier's S and R
    /// open its save and load pages, M, F, D, Q and O its main page. Escape's chain opens it at its end (fModernKeys).
    /// Until part U4d step 4 this game's options panel came up over the view instead. True when the keys were the menu's.
    /// </summary>
    private bool fModernMenuKeys()
    {
        UWModernGameMenu lOMenu = UWModernUi.Menu;
        UWControls lOControls = UWControlScheme.SharedControls;

        if (lOMenu == null || lOControls == null || !UWModernUi.Has(UWModernPartEnum.GameMenu))
            return false;

        if (lOMenu.IsOpen)
        {
            if (UWBack.Takes(UWBackOwner.ModernMenu) || lOControls[UWAct.KeyOptions].WasPressedThisFrame())
                lOMenu.ToggleMenu();

            fUpdatePointer();

            return true;
        }

        if (UWSetupMenu.IsCapturingInput || UWSetupMenu.HasOpenPanel || UWControls.IsTextEntryActive)
            return false;

        if (lOControls[UWAct.KeyOptions].WasPressedThisFrame())
        {
            lOMenu.ToggleMenu();

            return true;
        }

        UnityEngine.InputSystem.Keyboard lOKeys = UnityEngine.InputSystem.Keyboard.current;

        if (!UWControls.IsShortcutModifierHeld || lOKeys == null)
            return false;

        if (lOControls[UWAct.OptSave].WasPressedThisFrame())
            lOMenu.OpenMenu(UWModernGameMenu.PageEnum.Save);
        else if (lOControls[UWAct.OptRestore].WasPressedThisFrame())
            lOMenu.OpenMenu(UWModernGameMenu.PageEnum.Load);
        else if (lOControls[UWAct.OptMusic].WasPressedThisFrame() || lOControls[UWAct.OptSound].WasPressedThisFrame()
            || lOControls[UWAct.OptDetail].WasPressedThisFrame() || lOControls[UWAct.OptQuit].WasPressedThisFrame() || lOKeys.oKey.wasPressedThisFrame)
            lOMenu.OpenMenu(UWModernGameMenu.PageEnum.Main);
        else
            return false;

        return true;
    }

    /// <summary>
    /// A RESTORE (DoSaveRest, RestoreGame): an empty slot says "No save game there." and the panel closes;
    /// a save starts the session again from it (UWGameStartUw2.Request, Journey Onward's path), whose screen then
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
        UWGameStartUw2.Request(UWSavegameSlots.GetSlotPath(lsRoot, piSlot));
    }

    /// <summary>
    /// THE DESCRIPTION TYPED (SaveGame, wdialog): the scroll cleared, "Please enter a save file
    /// description:" in green, below it ">" and the slot's description - "&lt;not used yet&gt;" for an empty
    /// one, as the original hands it over - which the first typed character replaces; Backspace edits it,
    /// up to 30 characters. Enter or a mouse button saves, Escape aborts. As the first game's panel
    /// (UWHudOptions): the session's controls off meanwhile, the text read through UWTextEntry, no cursor keys
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
        mlScrollSaid++;
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

        string lsError = UWWorldViewUw2.Save(liSlot, lsDescription);

        if (lsError != null)
            Debug.LogWarning("[Session] Not saved: " + lsError);

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
    /// and stands in the window's middle. THE WIDESCREEN BOX (UW2 plan stage U8 step 2, 2026-11-24): the
    /// widened frame's column back to the original's by this game's cuts (UWClassicWide.OriginalX, the
    /// first game's rule) - the columns put in belong to no area of the original's.</summary>
    public bool TryGetOriginalPoint(Vector2 pOScreen, out float pfColumn, out float pfRow)
    {
        float lfFactor = UWClassicScreen.HorizontalPixelFactor;
        float lfScale = UWUiFit.Scale;
        float lfWide = (pOScreen.x - (fLeftUnits(lfFactor) * lfScale)) / (lfScale * lfFactor);

        pfRow = UWUiFit.FrameHeight - ((pOScreen.y - UWUiFit.BottomMarginPixels) / lfScale);
        pfColumn = lfWide;

        if (!mbShown || lfWide < 0f || lfWide >= UWUiFit.FrameWidthUnits + UWClassicWide.Extra || pfRow < 0f || pfRow >= UWUiFit.FrameHeight)
            return false;

        int liWide = Mathf.FloorToInt(lfWide);
        int liColumn = UWClassicWide.OriginalX(liWide, Mathf.FloorToInt(pfRow));

        // The fraction kept (the command bar's icons read it).
        pfColumn = liColumn + (lfWide - liWide);

        return liColumn >= 0;
    }

    /// <summary>The original's angle from the view's top to its bottom edge, in degrees (74.7).</summary>
    public static float OriginalVerticalAngle => 2f * Mathf.Atan(UWClassicLayoutUw2.Frame.VerticalViewTangent) * Mathf.Rad2Deg;

    /// <summary>Whether a screen point lies in the 3D view - everywhere in the modern scheme, inside
    /// the window on the classic screen (a click on the frame uses nothing).</summary>
    /// <summary>The camera of the view window.</summary>
    public Camera ViewCamera => mOCamera;

    public bool IsInView(Vector2 pOScreen)
    {
        return !mbShown || mOCamera == null || mOCamera.pixelRect.Contains(pOScreen);
    }

    private void OnDestroy()
    {
        if (mOModernSession != null && UWModernUi.Session == mOModernSession)
            UWModernUi.Session = null;

        fCloseOptions();

        if (Current == this)
            Current = null;

        fReleaseScaledCursors();

        if (mOCursorObject != null)
            Destroy(mOCursorObject);

        foreach (Texture2D lOTexture in mOOwned)
        {
            if (lOTexture != null)
            {
                // One of them may be the pointer's (the map's quill): UWPointer.ReleasePicture.
                UWPointer.ReleasePicture(lOTexture);
                Destroy(lOTexture);
            }
        }

        mOOwned.Clear();
    }
}
