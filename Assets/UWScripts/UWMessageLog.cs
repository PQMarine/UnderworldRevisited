using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE MESSAGE LIST OF THE GAME (cleanup step 5.13, 2026-10-22: out of Interaction, which keeps
/// AddMessage, AddGeneralMessage and the rest of the surface as forwarders for its about 110
/// callers): the lines both schemes draw (Interaction.ActiveStrings, read by UWHudMessageLog,
/// UWModernScroll and UWModernHud), the running line a prompt keeps replacing, the paging of a
/// long text on UWMessageLogModel with the clock held while it waits, and the capping of the
/// list. The sink the rules and hosts write to is IUWMessageSink.
/// </summary>
public sealed class UWMessageLog : IUWMessageSink
{
    /// <summary>Messages the list keeps: as many as the modern scroll's tallest setting
    /// (UWModernScroll.MaxLines).</summary>
    public const int HistoryCount = UWModernScroll.MaxLines;

    /// <summary>Messages the modern message box shows: the original's five, or the layout editor's
    /// Lines (UWUserSettings.ModernMessageLines; issue 4, 2026-10-21), as few as the scroll's
    /// fewest and as many as the list keeps.</summary>
    public static int ModernShownCount => Mathf.Clamp(
        UWUserSettings.ModernMessageLines > 0 ? UWUserSettings.ModernMessageLines : DefaultModernShownCount,
        UWModernScroll.MinLines, HistoryCount);

    public const int DefaultModernShownCount = 5;

    private readonly Interaction mOInteraction;

    private readonly List<string> mOLines = new List<string>();

    /// <summary>Line in the message box that keeps changing - for the quantity question
    /// when dragging. -1 means: none at the moment.</summary>
    private int miPromptLine = -1;

    public UWMessageLog(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    /// <summary>For UWGameUI (Original: message log in the lower frame box, see
    /// fBuildCanvas/MessageLog) - the same list that AddMessage fills.</summary>
    public IReadOnlyList<string> Lines => mOLines;

    private UWDataImport.DataImport fData()
    {
        return mOInteraction != null ? mOInteraction.GameData : null;
    }

    private UWGameUI fUi()
    {
        return mOInteraction != null ? mOInteraction.GameUi : null;
    }

    // ------------------------------------------------- the sink

    /// <summary>A general message as text, without adding it to the box right
    /// away - for messages assembled from several pieces (see
    /// UWMiscSpell.fDetectMonsters). Empty if the number does not exist.</summary>
    public string GetGeneralMessage(int piIndex)
    {
        return UWText.General(fData(), piIndex);
    }

    /// <summary>A missing entry is not an error (UWText gives nothing for it), and nothing
    /// is added for it.</summary>
    public void AddGeneralMessage(int piIndex)
    {
        string lsMessage = UWText.General(fData(), piIndex);

        if (!string.IsNullOrWhiteSpace(lsMessage))
            mOLines.Add(lsMessage);
    }

    public void AddMessage(string psMessage)
    {
        if (!string.IsNullOrEmpty(psMessage))
            mOLines.Add(psMessage);
    }

    /// <summary>The line as it is, an empty one too - the look wrote its lines straight into the
    /// list (Interaction.LookAtTarget until 2026-10-22), and does so still through this.</summary>
    public void AddLine(string psLine)
    {
        mOLines.Add(psLine);
    }

    /// <summary>
    /// Wipes the message box and starts with a blank line.
    ///
    /// That is what the original does before the compass text (uimanager_compass: first scroll.Clear,
    /// then a line break). The point is that nothing from before remains - the
    /// user noticed it at the same place (2026-09-07).
    ///
    /// The blank line is a SPACE, not an empty string: AddMessage deliberately drops empty
    /// messages, and the display would otherwise have nothing to draw.
    /// </summary>
    public void ClearMessages()
    {
        mOLines.Clear();
        mOLines.Add(" ");
    }

    /// <summary>Wipes the box WITHOUT the blank line: the original's save game messages
    /// ("Restoring Game ...", the name prompt, "Save Game Succeeded.") appear in the
    /// top line (per user, 2026-09-12).</summary>
    public void ResetMessages()
    {
        mOLines.Clear();
        miPromptLine = -1;
    }

    /// <summary>
    /// Original: the log in the lower frame box shows the last 5 messages, new ones
    /// appended at the bottom, old ones dropped at the top (see UWGameUI.fRefreshMessageLog for the
    /// actual Original display - here only the capping of the list itself). The list keeps
    /// HistoryCount, as many as the modern interface's scroll can be made tall
    /// (per user, 2026-10-09: eight lines showed only five); the classic box keeps the newest
    /// that fit, the modern message box the newest ModernShownCount.
    ///
    /// Called where Interaction.Update ends - not while a modal gate (a conversation, a question,
    /// a page) returns early, as before.
    /// </summary>
    public void Trim()
    {
        if (mOLines.Count <= HistoryCount)
            return;

        mOLines.RemoveAt(0);

        // A running input line moves up with the rest. Until 2026-09-26 its index stayed,
        // so with a full scroll it pointed past the end, the next refresh appended a new
        // line and the old one stayed behind - "Move how many? 1" repeated line after line
        // while a stack was dragged (per user with a screenshot).
        if (miPromptLine >= 0)
            miPromptLine--;

        // The message scroll moves on: a dragon scrolls along (UW.EXE seg043_37F0_1CA).
        UWHudDragons.Request(UWHudDragons.ScrollAnimation);
    }

    // ------------------------------------------------- the running line

    public bool IsPromptActive
    {
        get { return miPromptLine >= 0; }
    }

    /// <summary>Sets the running line, or creates it. Unlike AddMessage this does not stack
    /// but replaces - otherwise the box would be full after typing three times.</summary>
    public void SetPromptMessage(string psMessage)
    {
        if (miPromptLine >= 0 && miPromptLine < mOLines.Count)
        {
            mOLines[miPromptLine] = psMessage;
            return;
        }

        mOLines.Add(psMessage);
        miPromptLine = mOLines.Count - 1;
    }

    /// <summary>Ends the running line. The text stays, it is just no longer
    /// replaced from now on.</summary>
    public void EndPromptMessage()
    {
        miPromptLine = -1;
    }

    /// <summary>Takes the running line out of the messages altogether - the modern scheme's
    /// cancelled mantra leaves no trace (UWPromptController).</summary>
    public void DropPromptMessage()
    {
        if (miPromptLine >= 0 && miPromptLine < mOLines.Count)
            mOLines.RemoveAt(miPromptLine);

        miPromptLine = -1;
    }

    // ------------------------------------------------- paging a long text

    /// <summary>
    /// What is still pending of a long text.
    ///
    /// The original shows a text that does not fit into the box at once in steps
    /// and waits for a confirmation in between (per user, 2026-09-03).
    /// </summary>
    private readonly List<string> mOPendingPageLines = new List<string>();

    public bool IsWaitingForPage
    {
        get { return mOPendingPageLines.Count > 0; }
    }

    /// <summary>The frame in which the page-forward input was swallowed. UWItemDrag
    /// queries this because the order of the Update calls is not fixed: otherwise
    /// the same click that confirms the last page would immediately start a drag.</summary>
    private int miPageInputFrame = -1;

    public bool PageInputConsumedThisFrame
    {
        get { return miPageInputFrame == Time.frameCount; }
    }

    /// <summary>
    /// Shows a text page by page, BELOW what the box already holds: the box is not cleared
    /// (per user on the original, 2026-09-25, reading a scroll - ours cleared it until then),
    /// the old lines count as printed and scroll up as the text comes in.
    ///
    /// psLeading are lines that come BEFORE the text and count as already printed - they
    /// do not count as a pending page. With a short text they therefore stay, with
    /// a long one the text pushes them out right away, without a confirmation being
    /// needed (observed by user on the original with throttled CPU speed,
    /// 2026-09-03).
    /// </summary>
    public void ShowPagedText(string psText, params string[] psLeading)
    {
        mOPendingPageLines.Clear();
        mOPrintedPageLines.Clear();

        if (miPromptLine >= 0 && miPromptLine < mOLines.Count)
            mOLines.RemoveAt(miPromptLine);

        mOPrintedPageLines.AddRange(mOLines);
        miPromptLine = -1;

        UWGameUI lOUi = fUi();

        if (lOUi == null || string.IsNullOrEmpty(psText))
            return;

        if (psLeading != null)
        {
            foreach (string lsLeading in psLeading)
            {
                if (!string.IsNullOrEmpty(lsLeading))
                    mOPrintedPageLines.AddRange(lOUi.MessageLog.WrapLogText(UWFontRenderer.CleanText(lsLeading)));
            }
        }

        mOPendingPageLines.AddRange(lOUi.MessageLog.WrapLogText(UWFontRenderer.CleanText(psText)));

        fShowNextPage();
    }

    /// <summary>
    /// Prints the next page (UWMessageLogModel.ShowNextPage, cleanup step 5.7, 2026-10-20: the
    /// log scrolls, it is not cleared - per page the scroll's lines less one for [MORE], four in
    /// the classic box, as measured by the user on the Lakshi letter).
    ///
    /// The page follows the scroll shown NOW (decision 17, UWGameUI.ShownLogLines): the classic box,
    /// the modern scroll at its size or the modern message box - asked per page, so a scheme switch
    /// in the middle of a text pages the rest by the new one (B28: the classic five held for all).
    /// </summary>
    private void fShowNextPage()
    {
        UWGameUI lOUi = fUi();
        int liCapacity = lOUi != null ? lOUi.ShownLogLines : UWMessageLogModel.FallbackCapacity;

        // Paging moves the message scroll too (UW.EXE seg043_37F0_8EB).
        if (UWMessageLogModel.ShowNextPage(mOPrintedPageLines, mOPendingPageLines, liCapacity, mOLines))
            UWHudDragons.Request(UWHudDragons.ScrollAnimation);

        fUpdatePagedTimeScale();
    }

    /// <summary>What has already been printed of this text - the scroll from which the box
    /// shows its last lines.</summary>
    private readonly List<string> mOPrintedPageLines = new List<string>();

    /// <summary>
    /// While the game waits for paging, time stands still: in the original
    /// the water animation stops and the creatures do not move (per user,
    /// 2026-09-03).
    ///
    /// A named hold on UWGameClock, so that another pause is not overwritten.
    /// </summary>
    private void fUpdatePagedTimeScale()
    {
        bool lbWaiting = IsWaitingForPage;

        if (lbWaiting == mbPagingHoldsTime)
            return;

        mbPagingHoldsTime = lbWaiting;

        if (lbWaiting)
            UWGameClock.Hold(UWGameClock.PagingHold);
        else
            UWGameClock.Release(UWGameClock.PagingHold);
    }

    private bool mbPagingHoldsTime;

    /// <summary>While pages are still pending, nothing happens in the world - every key and every
    /// click only pages forward. True while pages are pending (Interaction.Update returns then).</summary>
    public bool UpdatePaging()
    {
        if (!IsWaitingForPage)
            return false;

        Mouse lOMouse = Mouse.current;
        Keyboard lOKeyboard = Keyboard.current;

        bool lbConfirm = (lOMouse != null
                && (UWMouseButtons.LeftPressed || UWMouseButtons.RightPressed))
            || (lOKeyboard != null && lOKeyboard.anyKey.wasPressedThisFrame);

        // Any gamepad button as well (UWGamepad).
        if (!lbConfirm && UWGamepad.AnyPressed(false))
        {
            UWGamepad.ConsumePress();
            lbConfirm = true;
        }

        if (lbConfirm)
        {
            miPageInputFrame = Time.frameCount;
            fShowNextPage();
        }

        return true;
    }
}
