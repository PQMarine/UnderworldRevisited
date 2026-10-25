using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.9 (2026-10-20): the turning panel's pages and steps (UWPanelTurn) left the
    /// classic UWHudPanel and the Classic Wide frame (UWModernClassicFrame). Pinned with what those
    /// copies computed: the eight steps with the edge and the chain in Unity's floats, the chain's
    /// and the rune bag's targets, and a conversation holding the inventory page and giving the
    /// page back.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPanelTurn()
        {
            Check.Group("Panel turn (UWPanelTurn - cleanup step 5.9)");

            UWPanelTurn lOTurn = new UWPanelTurn();

            fExpectBool("turn: at rest on the inventory", lOTurn.IsAtRestOn(UWPanelTurn.SideEnum.Inventory) && !lOTurn.IsTurning, true);
            fExpectBool("turn: the chain goes to the stats from the inventory", lOTurn.ChainTarget == UWPanelTurn.SideEnum.Stats, true);
            fExpectBool("turn: the rune bag goes to the runes", lOTurn.RuneBagTarget == UWPanelTurn.SideEnum.Runes, true);
            fExpectBool("turn: no turn to the page already shown", lOTurn.Begin(UWPanelTurn.SideEnum.Inventory), false);
            fExpectBool("turn: a turn to the stats starts", lOTurn.Begin(UWPanelTurn.SideEnum.Stats), true);
            fExpectBool("turn: none starts while one runs", lOTurn.Begin(UWPanelTurn.SideEnum.Runes), false);
            fExpectBool("turn: no page at rest while turning", lOTurn.IsAtRestOn(UWPanelTurn.SideEnum.Inventory), false);

            // The eight steps as UWHudPanel.fUpdatePanelRotation drew them: |cos(step/8 * pi)| in
            // Unity's floats, 0 and the edge picture on step 4, the chain's picture per step, the
            // flip there; the ninth call ends the turn.
            string lsScales = string.Empty;
            string lsChain = string.Empty;
            string lsEdge = string.Empty;
            int liFlippedAt = -1;
            bool lbSameFloats = true;

            for (int liCall = 0; liCall < UWPanelTurn.Steps; liCall++)
            {
                UWPanelTurn.Frame lOFrame = lOTurn.Advance();
                float lfUnity = (float)Math.Abs((float)Math.Cos((liCall / (float)UWPanelTurn.Steps) * (float)Math.PI));

                lbSameFloats &= lOFrame.OnEdge ? lOFrame.Scale == 0f : lOFrame.Scale == lfUnity;
                lsScales += lOFrame.Scale.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ";
                lsChain += lOFrame.ChainFrame + " ";
                lsEdge += lOFrame.OnEdge ? "E" : "-";

                if (lOFrame.Flipped)
                    liFlippedAt = lOFrame.Step;

                fExpectBool("turn: step " + liCall + " shows " + (liCall < UWPanelTurn.EdgeStep ? "the old page" : "the new page"),
                    lOTurn.Side == (liCall < UWPanelTurn.EdgeStep ? UWPanelTurn.SideEnum.Inventory : UWPanelTurn.SideEnum.Stats), true);
            }

            Check.Str("turn: the widths", lsScales.Trim(), "1.000 0.924 0.707 0.383 0.000 0.383 0.707 0.924");
            fExpectBool("turn: the widths in Unity's float steps", lbSameFloats, true);
            Check.Str("turn: the chain's pictures, one per step", lsChain.Trim(), "0 1 2 3 4 5 6 7");
            Check.Str("turn: the edge on step 4 only", lsEdge, "----E---");
            fExpectInt("turn: the page flips on the edge step", liFlippedAt, 4);
            fExpectBool("turn: still turning after the eighth step (its own dwell time)", lOTurn.IsTurning, true);

            UWPanelTurn.Frame lOEnd = lOTurn.Advance();

            fExpectBool("turn: the ninth call ends it, at full width", lOEnd.Finished && lOEnd.Scale == 1f && !lOTurn.IsTurning, true);
            fExpectBool("turn: at rest on the stats", lOTurn.IsAtRestOn(UWPanelTurn.SideEnum.Stats), true);
            fExpectBool("turn: from the stats the chain goes back to the inventory", lOTurn.ChainTarget == UWPanelTurn.SideEnum.Inventory, true);

            lOTurn.Show(UWPanelTurn.SideEnum.Runes);
            fExpectBool("turn: from the rune shelf the chain goes to the inventory, not the stats (per user 2026-09-03)",
                lOTurn.ChainTarget == UWPanelTurn.SideEnum.Inventory, true);
            fExpectBool("turn: from the rune shelf the rune bag goes back to the inventory", lOTurn.RuneBagTarget == UWPanelTurn.SideEnum.Inventory, true);

            // A conversation: the inventory without a turn, the page from before it back after.
            UWPanelTurn.ConversationChange lOHold = lOTurn.HoldForConversation();

            fExpectBool("conversation: the rune shelf gives way to the inventory, no turn cut",
                lOHold.SideChanged && !lOHold.TurnCut && lOTurn.Side == UWPanelTurn.SideEnum.Inventory, true);
            lOHold = lOTurn.HoldForConversation();
            fExpectBool("conversation: only the first frame changes anything", lOHold.SideChanged || lOHold.TurnCut, false);
            fExpectBool("conversation: afterwards the rune shelf is back",
                lOTurn.ReleaseAfterConversation() && lOTurn.Side == UWPanelTurn.SideEnum.Runes && lOTurn.SideBeforeConversation == null, true);
            fExpectBool("conversation: outside one nothing to give back", lOTurn.ReleaseAfterConversation(), false);

            // A turn under way is cut short and its target comes back after the conversation.
            lOTurn.Show(UWPanelTurn.SideEnum.Inventory);
            lOTurn.Begin(UWPanelTurn.SideEnum.Stats);
            lOTurn.Advance();
            lOTurn.Advance();
            lOHold = lOTurn.HoldForConversation();
            fExpectBool("conversation: a turn before its edge is cut, the inventory stays",
                lOHold.TurnCut && !lOHold.SideChanged && !lOTurn.IsTurning && lOTurn.Side == UWPanelTurn.SideEnum.Inventory, true);
            fExpectBool("conversation: ... and the stats come after it, the turn's target",
                lOTurn.ReleaseAfterConversation() && lOTurn.Side == UWPanelTurn.SideEnum.Stats, true);

            lOTurn.Begin(UWPanelTurn.SideEnum.Inventory);

            for (int liCall = 0; liCall <= UWPanelTurn.EdgeStep; liCall++)
                lOTurn.Advance();

            lOHold = lOTurn.HoldForConversation();
            fExpectBool("conversation: a turn to the inventory past its edge is cut, nothing to switch",
                lOHold.TurnCut && !lOHold.SideChanged && lOTurn.Side == UWPanelTurn.SideEnum.Inventory, true);
            fExpectBool("conversation: ... and the inventory stays after it", lOTurn.ReleaseAfterConversation(), false);

            fExpectBool("cancel: nothing to cut at rest", lOTurn.Cancel(), false);
            fExpectBool("advance: at rest it reports the end", lOTurn.Advance().Finished, true);
        }
    }
}
