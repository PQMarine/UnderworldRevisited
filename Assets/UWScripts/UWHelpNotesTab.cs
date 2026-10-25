using UnityEngine;
using UWDataImport.UWData;

/// <summary>The help's NOTES tab: the player's free notes, kept with the save game (UWHelpNotes).
/// While the player types, the game's keys stay silent (CapturesKeys, see
/// UWTextEntryState.IsTextEntryActive). Moved out of UWHelpWindow (cleanup 8.6, 2026-10-24); on the
/// kit a text area of several lines on UWTextEntry and UWTypedKeys (cleanup 8b.3, OnDrawKit).
/// </summary>
public sealed class UWHelpNotesTab : UWHelpTab
{
    private const string NotesControl = "UWHelpNotes";

    private bool mbTyping;

    public override UWHelpTabId Id => UWHelpTabId.Notes;

    public override string TitleId => "help.tab.notes";

    public override bool CapturesKeys => mbTyping;

    public override bool Scrolls => false;

    /// <summary>
    /// THE NOTES ON THE KIT (cleanup 8b.3, 2026-10-24): the same line and room as the IMGUI form (gone
    /// since 8b.4, it took Tab before its TextArea, which would otherwise have typed one), then the notes
    /// as a text area of several lines filling the room (UWKitRows.TextArea): a click gives it the keys,
    /// Enter breaks the line, the arrows, Home and End move the cursor, the wheel and the bar scroll and
    /// the cursor stays in view. TAB CLOSES THE HELP while typing too, without typing a tab (the game's
    /// keys are silent then; with Alt it is the switch to another program and left alone). Typing on
    /// Linux comes through UWTypedKeys, where no text events arrive.
    /// </summary>
    protected override void OnDrawKit(UWHelpKitFlow pOFlow)
    {
        pOFlow.Line(UWText.T("help.notes.intro"), UWHelpLookEnum.Dim);
        pOFlow.Space(6f);

        UWKitRows lOKit = pOFlow.Kit;
        Vector2 lOScroll = Scroll;
        UWKitRows.HelpLook lOLook = UWHelpKitFlow.Look(UWHelpLookEnum.Text);

        // The notes as typed: no tags read (IMGUI's Notes style had richText off).
        lOLook.Rich = false;

        UWHelpNotes.Notes = lOKit.TextArea(pOFlow.Rest(), NotesControl, UWHelpNotes.Notes ?? string.Empty, lOLook,
            UWGuiTheme.HelpNotesPadding, ref lOScroll);
        Scroll = lOScroll;

        if (lOKit.TabIn(NotesControl))
            mOContext.Window.CloseByPlayer();

        mbTyping = lOKit.AreaHasKeys;
    }

    /// <summary>The tab left the screen: no more typing (the kit's area lets go in the window).</summary>
    public override void OnHidden()
    {
        mbTyping = false;
    }
}
