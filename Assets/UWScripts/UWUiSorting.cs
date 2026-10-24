/// <summary>
/// EVERY CANVAS SORTING ORDER, in one table (cleanup 5.1, 2026-10-19). Until then each canvas
/// wrote its own literal (38-47, 60, 80, 90, 95, 96, 100, 500, 1000) and the layering could only
/// be read by visiting every file. The values are exactly those literals: nothing moves on screen.
/// A higher order lies over a lower one.
///
/// SIX CANVASES SHARE 41 (ModernParts): the character panel, the rune panel, the action bar, the
/// minimap, the original character page and the Classic Wide dragons. Among canvases of the same
/// order Unity draws in creation order (UWGameUI.Start, UWModernHud.fBuild), so their overlap
/// depends on that order; they are listed under their own names, all on the shared value, so a
/// later step can part them without hunting for the literal.
///
/// The IMGUI windows (GUI.depth) and the debug overlay are not canvases and stay outside.
/// </summary>
public static class UWUiSorting
{
    /// <summary>The classic frame and HUD (UWGameUI): Unity's default order, under everything.</summary>
    public const int Classic = 0;

    /// <summary>The Classic Wide frame of the modern classic look (UWModernClassicFrame): under the
    /// HUD's parts and the conversation.</summary>
    public const int ClassicWideFrame = 38;

    /// <summary>The modern conversation (UWModernConversation): under the character panel, the
    /// bags and the thing on the pointer.</summary>
    public const int ModernConversation = 39;

    /// <summary>The modern HUD (UWModernHud).</summary>
    public const int ModernHud = 40;

    /// <summary>The order the six modern parts share (see the class comment).</summary>
    public const int ModernParts = 41;

    public const int ModernCharacterPanel = ModernParts;

    public const int ModernRunePanel = ModernParts;

    public const int ModernActionBar = ModernParts;

    public const int ModernMinimap = ModernParts;

    public const int ModernInventoryPage = ModernParts;

    /// <summary>The Classic Wide dragons over the parts (the rune hollow lies under the right one's
    /// head, per user, 2026-10-10): a canvas of their own above the HUD's.</summary>
    public const int ClassicWideDragons = ModernParts;

    /// <summary>The modern bags (UWModernBags).</summary>
    public const int ModernBags = 42;

    /// <summary>The minimap in the pinned character panel's head: over the panel, which shares
    /// its order otherwise.</summary>
    public const int ModernMinimapInHead = 42;

    /// <summary>The spell dragged off the modern rune panel.</summary>
    public const int ModernRuneGhost = 43;

    /// <summary>The modern question box (UWModernQuestion).</summary>
    public const int ModernQuestion = 44;

    /// <summary>The modern HUD's view picture (a window or grave picture in its leather frame).</summary>
    public const int ModernViewPicture = 45;

    /// <summary>The gamepad hint strip (UWPadHintStrip).</summary>
    public const int PadHints = 45;

    /// <summary>The layout editor's conversation preview: over every part, under the editor's own
    /// frames.</summary>
    public const int ModernConversationPreview = 46;

    /// <summary>The modern layout editor (UWModernLayoutEditor).</summary>
    public const int LayoutEditor = 47;

    /// <summary>The modern scheme's black or coloured flash (UWHudPictures).</summary>
    public const int ModernFlash = 60;

    /// <summary>The eyes of the classic frame (UWEyesDisplay): below the conversation display, but
    /// above the game world.</summary>
    public const int Eyes = 80;

    /// <summary>The classic conversation screen (UWClassicConversationRenderer, driven by UWConversationController).</summary>
    public const int ConversationScreen = 90;

    /// <summary>The main menu (UWMainMenu).</summary>
    public const int MainMenu = 95;

    /// <summary>The character creation (UWCharacterCreationScreen).</summary>
    public const int CharacterCreation = 96;

    /// <summary>The intro and the cutscenes (UWIntroPlayer).</summary>
    public const int Intro = 100;

    /// <summary>The effects screen's own frame in the game (UWSetupMenu): above the game's
    /// interfaces.</summary>
    public const int EffectsScreen = 500;

    /// <summary>The thing on the classic pointer (UWItemDrag).</summary>
    public const int ItemDrag = 1000;

    /// <summary>The loading cover (UWScreenUi): above everything, including the intro.</summary>
    public const int LoadingCover = 1000;
}
