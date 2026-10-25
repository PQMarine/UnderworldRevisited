using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// ONE TAB OF THE HELP WINDOW (cleanup 8.6, 2026-10-24): until then the seven tabs were one
/// switch in UWHelpWindow's OnGUI and about 1,100 of its lines. Now each tab is a class of its
/// own (UWHelpStatsTab, UWHelpMapTab, UWHelpNotesTab, UWHelpSpellsTab, UWHelpMantrasTab,
/// UWHelpManualTab, UWHelpControlsTab), and the window keeps the frame around them: the tab row,
/// the place in each scheme and preset, the scaling, the reading mode's state and the keys'
/// hold; it draws the active tab into the area it laid out (UWHelpContext).
///
/// The drawing code moved as it was - the same styles (UWGuiTheme.Help), the same rects, the
/// same order of the IMGUI calls; the move to the uGUI kit is phase 8b.
///
/// ON THE KIT (cleanup 8b.3, 2026-10-24): each tab drew in two forms - DrawKit on the kit (UWKitRows,
/// laid out by UWHelpKitFlow), Draw with IMGUI for the side-by-side switch. The tabs that are lists
/// (Stats, Spells, Mantras, Controls) say their list once (OnFlow, IUWHelpFlow); the map, the notes and
/// the manual draw their pictures and text area themselves. The IMGUI form went in cleanup 8b.4.
/// </summary>
public abstract class UWHelpTab
{
    /// <summary>Room below each spell or mantra entry.</summary>
    protected const float EntrySpacing = 8f;

    /// <summary>The tab's saved number (settings.json HelpTab) - never renumbered.</summary>
    public abstract UWHelpTabId Id { get; }

    /// <summary>The tab button's text id ("help.tab.spells"); the window shows it through UWText.T
    /// (cleanup 8.5, 2026-10-24).</summary>
    public abstract string TitleId { get; }

    /// <summary>Whether the tab has the keyboard right now and the game hears no keys: the
    /// notes while the player types, the manual while it is read large.</summary>
    public virtual bool CapturesKeys => false;

    /// <summary>Whether the window draws the tab inside the tab's scroll list. The notes, the map
    /// and the manual lay their space out themselves.</summary>
    public virtual bool Scrolls => true;

    /// <summary>The scroll position of the tab's list, back at the top whenever the tab is
    /// chosen (the window shared one position before, reset on every choice).</summary>
    public Vector2 Scroll { get; set; }

    /// <summary>The window's area of this pass, set before DrawKit.</summary>
    protected UWHelpContext mOContext;

    /// <summary>The height the tab's list took on the kit last frame - its scroll list's content
    /// (IMGUI's scroll view measured it in its layout pass).</summary>
    public float KitContentHeight { get; set; }

    /// <summary>The tab was chosen - by its button, from the save or by the minimap.</summary>
    public virtual void OnShown()
    {
        Scroll = Vector2.zero;
    }

    /// <summary>Draws the tab on the kit below the tab row; pOFlow is started at the tab's room
    /// (cleanup 8b.3).</summary>
    public void DrawKit(UWHelpContext pOContext, UWHelpKitFlow pOFlow)
    {
        mOContext = pOContext;
        OnDrawKit(pOFlow);
    }

    /// <summary>The tab on the kit: by default its list (OnFlow).</summary>
    protected virtual void OnDrawKit(UWHelpKitFlow pOFlow)
    {
        OnFlow(pOFlow);
    }

    /// <summary>A list tab's content, said once (IUWHelpFlow; for both forms until cleanup 8b.4).</summary>
    protected virtual void OnFlow(IUWHelpFlow pOFlow)
    {
    }

    /// <summary>The tab leaves the screen or the window closes: a text area lets go of the keys.</summary>
    public virtual void OnHidden()
    {
    }

    protected static DataImport fData()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        return lOLoader != null ? lOLoader.UWDataImporter : null;
    }
}
