using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME UNDER THE MODERN CONVERSATION (IUWModernConversationHost): UWConversationScreenUw2 runs the
/// conversation with its canvas hidden, the first game's bag windows stand beside it. Six places a side, no
/// original's look (its measures are the first game's), the screen's own pictures, and the backs in the slate
/// shades of the second game's own frame (per user 2026-10-31: the first game's ramp came out too dark here).
/// </summary>
public sealed class UWModernConversationHostUw2 : IUWModernConversationHost
{
    /// <summary>The blue greys that make up BYT.ARK's game screen around the view, darkest first: its most
    /// frequent ones, measured 2026-10-31.</summary>
    private static readonly List<UWColor32> StoneShades = new List<UWColor32>
    {
        new UWColor32(48, 48, 64, 255),
        new UWColor32(64, 64, 88, 255),
        new UWColor32(76, 76, 100, 255),
        new UWColor32(88, 88, 108, 255),
        new UWColor32(100, 100, 124, 255),
        new UWColor32(108, 108, 136, 255)
    };

    private readonly UWGameScreenUw2 mOScreen;

    private bool mbOpenedBags;

    public UWModernConversationHostUw2(UWGameScreenUw2 pOScreen)
    {
        mOScreen = pOScreen;
    }

    private static UWConversationScreenUw2 fTalk => UWConversationScreenUw2.Current;

    /// <summary>The game's data: the conversation screen's, else the game screen's - before the first conversation there is no
    /// conversation screen yet, and the layout editor's preview needs the data (per user 2026-11-24: the preview came only
    /// after a conversation).</summary>
    public DataImport Data => fTalk != null ? fTalk.Data : mOScreen != null ? mOScreen.ModernData : null;

    /// <summary>The conversation screen while it runs one: its names and pictures are the running conversation's; the
    /// screen stays after it, and the preview must not show the last partner.</summary>
    private UWConversationScreenUw2 fRunning => IsTalking ? fTalk : null;

    public FilterMode Filter => FilterMode.Point;

    public UWGameUI TextUi => null;

    public bool IsTalking => UWConversationScreenUw2.IsOpen && UWConversationScreenUw2.IsModern;

    public UWConversationView OpenView => IsTalking && fTalk != null ? fTalk.View : null;

    public int TradeSlots => UWConversationTrade.SlotCountUw2;

    /// <summary>The original's look from this game's conversation screen (UWConversationLook.Uw2; stage U7 step 3b).</summary>
    public bool HasOriginalLook => true;

    public bool UsesIconPalette => false;

    public int PortraitVersion => fTalk != null ? fTalk.PortraitVersion : 0;

    /// <summary>The player's name as this game's conversation screen takes it (UWPlayerRecordUw2.Name; the preview showed
    /// "Avatar", per user 2026-11-24), the first game's way without a player file.</summary>
    public string PreviewPlayerName
    {
        get
        {
            UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;

            if (lOFile != null && !string.IsNullOrEmpty(UWPlayerRecordUw2.Name(lOFile)))
                return UWPlayerRecordUw2.Name(lOFile);

            UWPlayerData lOPlayer = Data != null ? Data.InitialPlayer : null;

            return lOPlayer != null && !string.IsNullOrEmpty(lOPlayer.Name) ? lOPlayer.Name : "Avatar";
        }
    }

    /// <summary>The player's head in HEADS.GR as this game's conversation screen takes it (UWPlayerRecordUw2.BodyPortrait), the
    /// first game's rule without a player file.</summary>
    public int PreviewPlayerPortrait => UWGameSession.Current != null && UWGameSession.Current.PlayerFileUw2 != null
        ? UWPlayerRecordUw2.BodyPortrait(UWGameSession.Current.PlayerFileUw2)
        : UWConversationLayout.PlayerPortrait(Data != null ? Data.InitialPlayer : null);

    /// <summary>The preview's partner: Lord British (whoami 142, block 7's 159 by GetPartnerNameIndex), his name in the game's
    /// own text and his head CHARHEAD 141 (whoami less one, as strt_converse) - per user 2026-11-24, the first game's preview
    /// has Shak.</summary>
    public string PreviewPartnerName => Data != null
        ? UWText.Block(Data, UWConversations.PartnerNameStringBlock, UWConversations.GetPartnerNameIndex(PreviewPartnerWhoami), "Lord British")
        : "Lord British";

    public int PreviewPartnerPortrait => PreviewPartnerWhoami - 1;

    private const int PreviewPartnerWhoami = 142;

    /// <summary>Left of the bag windows and the character panel, as on the first game.</summary>
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

    /// <summary>The waits are the second game's: its history shows what has come out of them.</summary>
    public int HistoryLines(UWConversationView pOView)
    {
        return pOView.LinesRevealed;
    }

    private static readonly string[] NoChoices = new string[0];

    public IReadOnlyList<string> ShownChoices(UWConversationView pOView)
    {
        return pOView.IsTextWaiting ? NoChoices : pOView.Choices;
    }

    public string PartnerName(UWConversationView pOView)
    {
        return fRunning != null ? fRunning.ShownPartnerName : string.Empty;
    }

    public string PlayerName(UWConversationView pOView)
    {
        return fRunning != null ? fRunning.ShownPlayerName : string.Empty;
    }

    private bool mbFreedPointer;

    private bool mbPanelWasOpen;

    private UWModernPanel.TabEnum mePanelTab;

    /// <summary>The bags open, the character panel on its Character tab and the pointer free for the trade, remembered
    /// to give back at the end - as the first game's host (UWModernConversationHostUw1). The panel too since
    /// 2026-11-23 (per user on the test of the bag windows: shut, it should come out for a conversation as well).</summary>
    public void Begin()
    {
        mbFreedPointer = !UWModernPointer.IsFree;
        mbOpenedBags = !mOScreen.IsBagsOpen;
        mOScreen.OpenBags();

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
        if (mbOpenedBags)
            mOScreen.CloseBags();

        UWModernPanel lOPanel = UWModernPanel.Instance;

        if (lOPanel != null)
        {
            if (!mbPanelWasOpen && !UWModernPanel.Pinned)
                lOPanel.Close();
            else if (mePanelTab != UWModernPanel.TabEnum.Character)
                lOPanel.Open(mePanelTab);
        }

        // Locked again only where the conversation freed it.
        if (mbFreedPointer)
            UWModernPointer.Lock();

        mbOpenedBags = false;
        mbFreedPointer = false;
    }

    public Texture2D Portrait(UWConversationView pOView, bool pbPlayer)
    {
        if (fRunning != null)
            return fRunning.PortraitPicture(pbPlayer);

        // THE LAYOUT EDITOR'S PREVIEW (no conversation running): the view's own heads, opaque as the screen draws them.
        UWTexture lOHead = pOView != null && Data != null ? pOView.PortraitTexture(Data.Textures, pbPlayer) : null;

        if (lOHead == null || lOHead.Width <= 0 || lOHead.Height <= 0)
            return null;

        if (!mOPreviewHeads.TryGetValue(lOHead, out Texture2D lOTexture) || lOTexture == null)
        {
            lOTexture = UWTextureFactory.FromPicture(UWPicture.FromOpaque(lOHead), Filter, "UW2 conversation preview head");
            mOPreviewHeads[lOHead] = lOTexture;
        }

        return lOTexture;
    }

    private readonly Dictionary<UWTexture, Texture2D> mOPreviewHeads = new Dictionary<UWTexture, Texture2D>();

    public Texture2D SpeakerPortrait(int piPicture)
    {
        return fTalk != null ? fTalk.SpeakerPortrait(piPicture) : null;
    }

    public Texture2D ItemPicture(UWObject pOItem)
    {
        return pOItem != null && fTalk != null ? fTalk.ItemPicture(pOItem.ID) : null;
    }

    public Texture2D Leather(int piWidth, int piHeight, int piVariant)
    {
        return UWModernHudArt.BuildRampBack(Data.Textures, piWidth, piHeight, StoneShades, UWModernBacks.Get(UWModernLayout.ElementEnum.Conversation), Filter, piVariant);
    }

    public Texture2D SlotPicture()
    {
        return fCarvedSlot();
    }

    /// <summary>A round hollow in the stone, drawn pixel by pixel: the first game's slot is cut out of its own
    /// art (UWSlotBuilder), which came out torn on the second game's files (per user 2026-10-31). Its rim is dark
    /// to the top left and light to the bottom right, the light falling in from there.</summary>
    private static Texture2D fCarvedSlot()
    {
        int liSize = UWModernHudArt.SlotSize;
        float lfMiddle = (liSize - 1) * 0.5f;
        float lfRadius = liSize * 0.5f;
        Color32 lOClear = new Color32(0, 0, 0, 0);
        Color32[] laPixels = new Color32[liSize * liSize];

        for (int liRow = 0; liRow < liSize; liRow++)
        {
            for (int liColumn = 0; liColumn < liSize; liColumn++)
            {
                float lfX = liColumn - lfMiddle;
                float lfY = liRow - lfMiddle;
                float lfFrom = Mathf.Sqrt((lfX * lfX) + (lfY * lfY));
                UWColor32 lOShade = lfFrom >= lfRadius - 1.5f
                    ? StoneShades[lfX - lfY > 0f ? StoneShades.Count - 1 : 0]
                    : StoneShades[1];

                laPixels[(liRow * liSize) + liColumn] = lfFrom > lfRadius ? lOClear : UWColourVision.Apply(new Color32(lOShade.R, lOShade.G, lOShade.B, 255));
            }
        }

        return UWTextureFactory.FromColours(laPixels, liSize, liSize, FilterMode.Point, TextureWrapMode.Clamp, "UW2 trade slot");
    }
}
