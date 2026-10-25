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
/// No IMGUI is left in the player since cleanup 8c (2026-10-25): the debug panel and the debug
/// tools' labels in the world are canvases here too.
/// </summary>
public static class UWUiSorting
{
    /// <summary>The classic frame and HUD (UWGameUI): Unity's default order, under everything.</summary>
    public const int Classic = 0;

    /// <summary>
    /// THE DEBUG TOOLS' LABELS IN THE WORLD (cleanup 8c, 2026-10-25; UWDebugLabels): the names of the
    /// invisible traps and triggers in full-bright mode and the lines to their target tiles, where the
    /// world is - over the view and the classic frame, under the Classic Wide frame, the modern HUD and
    /// every window. IMGUI drew them over everything, so a label could cover a window.
    /// </summary>
    public const int DebugLabels = 37;

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

    /// <summary>
    /// THE HELP WINDOW ON THE KIT (cleanup 8b.3, 2026-10-24; UWHelpWindow): where its IMGUI form stood -
    /// over the game, the classic frame (the help lies behind it while it slides, by its clip, not by
    /// this order), the modern parts, the conversation and the intro - with the setup bar and its windows
    /// over it (their clicks are theirs: the help takes no pointer while the bar captures it). IMGUI drew
    /// it over every canvas; the thing on the classic pointer, the effects screen and the loading cover
    /// now lie over it.
    /// </summary>
    public const int HelpWindow = 300;

    /// <summary>
    /// THE LAYOUT EDITOR'S WINDOW ON THE KIT (cleanup 8b.4, 2026-10-24; UWModernLayoutEditor): its window
    /// and save question, which IMGUI drew over every canvas - over the editor's frames (LayoutEditor),
    /// the HUD, the panels, the bags and the help (which takes no pointer while the game menu, the
    /// editor's page included, is open); under the setup bar's windows, which are not offered while
    /// the editor is open.
    /// </summary>
    public const int LayoutEditorWindow = 350;

    /// <summary>
    /// THE MODERN GAME MENU ON THE KIT (cleanup 8b.4, 2026-10-24; UWModernGameMenu): where its IMGUI form
    /// stood - over the game, the HUD, the panels and the help (which takes no pointer while the menu is
    /// open); under the setup bar and its windows (the menu is not drawn while one of them is open) and
    /// the letter grid. It shares the order with the editor's window: the editor is a page of the menu
    /// on which no page is drawn.
    /// </summary>
    public const int ModernGameMenu = 350;

    /// <summary>
    /// THE WINDOWS ON THE KIT (cleanup 8b.1, 2026-10-24): the setup bar and the windows moved off IMGUI
    /// (UWKitRows). Where the IMGUI windows stood: over the game, the HUD, the conversation, the main
    /// menu, the character creation and the intro; under the effects screen (the bar is not drawn
    /// while it is open, as before), the thing on the classic pointer and the loading cover. IMGUI
    /// drew over every canvas, the cover included; the cover stands while a scene starts or a
    /// conversation fades out, when the bar is not offered (UWMainMenu.AcceptsMenuBar, the in-game
    /// menus), so nothing it covered shows differently.
    /// </summary>
    public const int KitWindows = 400;

    /// <summary>
    /// THE GAMEPAD'S LETTER GRID ON THE KIT (cleanup 8b.4, 2026-10-24; UWLetterGrid): IMGUI drew it over
    /// every other IMGUI window (GUI.depth -1000) and every canvas - here over the game menu with its
    /// save name, the conversation, the character creation and the setup bar's windows; under the
    /// effects screen, the thing on the classic pointer and the loading cover, which do not stand while
    /// text is typed.
    /// </summary>
    public const int LetterGrid = 450;

    /// <summary>The effects screen's own frame in the game (UWSetupMenu): above the game's
    /// interfaces.</summary>
    public const int EffectsScreen = 500;

    /// <summary>
    /// THE DEBUG PANEL ON THE KIT (cleanup 8c, 2026-10-25; UWDebugPanel: the F1 report and the debug
    /// tools' tab): IMGUI drew the overlay over everything (GUI.depth -1000), so its numbers stay readable
    /// over every window - the HUD, the help, the game menu, the setup bar and its windows, the letter
    /// grid and the effects screen; under the thing on the classic pointer, which must stay visible where
    /// it is dragged, and the loading cover, which stands only while a scene starts.
    /// </summary>
    public const int DebugPanel = 900;

    /// <summary>The thing on the classic pointer (UWItemDrag).</summary>
    public const int ItemDrag = 1000;

    /// <summary>The loading cover (UWScreenUi): above everything, including the intro.</summary>
    public const int LoadingCover = 1000;

    /// <summary>OUR POINTER while the game captures the mouse (UWPointerDriver, cleanup step 7.7B,
    /// 2026-10-25): over everything, the loading cover and the thing on the classic pointer included,
    /// as the system pointer was drawn.</summary>
    public const int Pointer = 2000;
}
