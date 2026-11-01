using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE FIRST GAME UNDER THE MODERN CONVERSATION (IUWModernConversationHost): the controller's view, the bags open and
/// the panel on its Character tab for the trade, the pointer free meanwhile.
/// </summary>
public sealed class UWModernConversationHostUw1 : IUWModernConversationHost
{
    private readonly UWGameUI mOUi;

    private bool mbFreedPointer;

    private bool mbOpenedBags;

    private bool mbPanelWasOpen;

    private UWModernPanel.TabEnum mePanelTab;

    public UWModernConversationHostUw1(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    public DataImport Data => mOUi != null ? mOUi.mOUWData : null;

    public FilterMode Filter => mOUi.TextureFilterMode;

    public UWGameUI TextUi => mOUi;

    private static UWConversationController fScreen()
    {
        UWConversationController lOScreen = UWScene.ConversationController;

        return lOScreen != null && lOScreen.IsOpen && lOScreen.Session != null ? lOScreen : null;
    }

    public bool IsTalking => UWModernUi.IsTalking && fScreen() != null;

    public UWConversationView OpenView => fScreen() != null ? fScreen().View : null;

    public int TradeSlots => UWConversationTrade.SlotCount;

    public bool HasOriginalLook => true;

    public bool UsesIconPalette => true;

    public int PortraitVersion => 0;

    public float RightEdge
    {
        get
        {
            float lfRight = Screen.width;

            if (UWModernPanel.Instance != null && UWModernPanel.Instance.IsOpen)
                lfRight = Mathf.Min(lfRight, UWModernPanel.Instance.LeftEdge);

            if (UWModernBags.Instance != null)
                lfRight = Mathf.Min(lfRight, UWModernBags.Instance.LeftEdge);

            return lfRight;
        }
    }

    public int HistoryLines(UWConversationView pOView)
    {
        return pOView.Lines.Count;
    }

    public System.Collections.Generic.IReadOnlyList<string> ShownChoices(UWConversationView pOView)
    {
        return pOView.Choices;
    }

    public string PartnerName(UWConversationView pOView)
    {
        return pOView.PartnerName;
    }

    public string PlayerName(UWConversationView pOView)
    {
        return pOView.PlayerName;
    }

    /// <summary>The bags open, the panel on its Character tab, the pointer free - remembered, to
    /// give back at the end.</summary>
    public void Begin()
    {
        mbFreedPointer = !UWModernPointer.IsFree;

        if (mbFreedPointer)
            UWModernPointer.Free();

        UWModernBags lOBags = UWModernBags.Instance;

        mbOpenedBags = lOBags != null && !lOBags.IsOpen;

        if (mbOpenedBags)
            lOBags.Open();

        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOPanel != null)
        {
            mbPanelWasOpen = lOPanel.IsOpen;
            mePanelTab = lOPanel.Tab;

            if (!lOPanel.IsOpen || lOPanel.Tab != UWModernPanel.TabEnum.Character)
                lOPanel.Open(UWModernPanel.TabEnum.Character);
        }
    }

    public void End()
    {
        UWModernBags lOBags = UWModernBags.Instance;

        if (mbOpenedBags && lOBags != null && lOBags.IsOpen)
            lOBags.Close();

        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOPanel != null)
        {
            if (!mbPanelWasOpen && !UWModernPanel.Pinned)
                lOPanel.Close();
            else if (mePanelTab != UWModernPanel.TabEnum.Character)
                lOPanel.Open(mePanelTab);
        }

        // Locked again only where the conversation freed it - the screen fades meanwhile and shows
        // the pointer until it is back (UWControlScheme.IsConversationOpen).
        if (mbFreedPointer)
            UWModernPointer.Lock();

        mbFreedPointer = false;
        mbOpenedBags = false;
    }

    public Texture2D Portrait(UWConversationView pOView, bool pbPlayer)
    {
        return UWIconCache.Texture(pOView.PortraitTexture(Data.Textures, pbPlayer), Filter);
    }

    /// <summary>The first game's conversations do not change the speaker.</summary>
    public Texture2D SpeakerPortrait(int piPicture)
    {
        return null;
    }

    public Texture2D ItemPicture(UWObject pOItem)
    {
        UWTexture lOSource = pOItem != null ? pOItem.Icon ?? pOItem.Texture : null;

        return lOSource != null ? UWIconCache.Texture(lOSource, Filter) : null;
    }

    public Texture2D Leather(int piWidth, int piHeight, int piVariant)
    {
        return UWModernHudArt.BuildPartLeather(UWModernLayout.ElementEnum.Conversation, Data.Textures, piWidth, piHeight, Filter, piVariant);
    }

    public Texture2D SlotPicture()
    {
        return UWModernHudArt.BuildPartSlot(UWModernLayout.ElementEnum.Conversation, Data.Textures, Filter);
    }
}
