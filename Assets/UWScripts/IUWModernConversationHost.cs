using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// WHAT THE MODERN CONVERSATION STANDS ON (UW2 plan phase 3 step 8, per user 2026-10-31: one class for both games).
/// UWModernConversation draws a UWConversationView; which game runs it, where it gets its pictures and what opens
/// beside it lies behind this. UWModernConversationHostUw1 over UWGameUI, UWConversationController, the bags and
/// the panel, UWModernConversationHostUw2 over UWGameScreenUw2 and UWConversationScreenUw2.
/// </summary>
public interface IUWModernConversationHost
{
    DataImport Data { get; }

    FilterMode Filter { get; }

    /// <summary>The first game's interface, for the texts' fonts (UWUiKit.CreateText); null on the second.</summary>
    UWGameUI TextUi { get; }

    /// <summary>A conversation runs and this scheme draws it.</summary>
    bool IsTalking { get; }

    /// <summary>The conversation's view, null while none runs.</summary>
    UWConversationView OpenView { get; }

    /// <summary>The trade's places on a side.</summary>
    int TradeSlots { get; }

    /// <summary>Whether the original's look can be drawn (its measures are the first game's).</summary>
    bool HasOriginalLook { get; }

    /// <summary>Whether portraits and things go through the palette shader (UWIconPalette).</summary>
    bool UsesIconPalette { get; }

    /// <summary>The room's right edge on the screen, left of what stands beside the conversation.</summary>
    float RightEdge { get; }

    /// <summary>Changes when a portrait changed during the conversation.</summary>
    int PortraitVersion { get; }

    /// <summary>How many of the view's Lines the history shows.</summary>
    int HistoryLines(UWConversationView pOView);

    /// <summary>The answers that stand now.</summary>
    System.Collections.Generic.IReadOnlyList<string> ShownChoices(UWConversationView pOView);

    string PartnerName(UWConversationView pOView);

    string PlayerName(UWConversationView pOView);

    /// <summary>The conversation starts: what stands beside it opens, the pointer goes free.</summary>
    void Begin();

    /// <summary>It ended: all of that as it was.</summary>
    void End();

    Texture2D Portrait(UWConversationView pOView, bool pbPlayer);

    /// <summary>The head for a change of speaker in the history (switch_pic's number), null for none.</summary>
    Texture2D SpeakerPortrait(int piPicture);

    /// <summary>A thing's picture on a trade slot, null for none.</summary>
    Texture2D ItemPicture(UWObject pOItem);

    /// <summary>A panel's back; piVariant keeps the large panels' clouds apart.</summary>
    Texture2D Leather(int piWidth, int piHeight, int piVariant);

    Texture2D SlotPicture();
}
