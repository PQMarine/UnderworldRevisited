using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME UNDER THE MODERN CONVERSATION (IUWModernConversationHost): UWConversationScreenUw2 runs the
/// conversation with its canvas hidden, the bags of UWModernHudUw2 stand beside it. Six places a side, no
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

    public DataImport Data => fTalk != null ? fTalk.Data : null;

    public FilterMode Filter => FilterMode.Point;

    public UWGameUI TextUi => null;

    public bool IsTalking => UWConversationScreenUw2.IsOpen && UWConversationScreenUw2.IsModern;

    public UWConversationView OpenView => IsTalking && fTalk != null ? fTalk.View : null;

    public int TradeSlots => UWConversationTrade.SlotCountUw2;

    public bool HasOriginalLook => false;

    public bool UsesIconPalette => false;

    public int PortraitVersion => fTalk != null ? fTalk.PortraitVersion : 0;

    public float RightEdge => mOScreen.BagsLeftEdge;

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
        return fTalk != null ? fTalk.ShownPartnerName : string.Empty;
    }

    public string PlayerName(UWConversationView pOView)
    {
        return fTalk != null ? fTalk.ShownPlayerName : string.Empty;
    }

    public void Begin()
    {
        mbOpenedBags = !mOScreen.IsBagsOpen;
        mOScreen.OpenBags();
    }

    public void End()
    {
        if (mbOpenedBags)
            mOScreen.CloseBags();

        mbOpenedBags = false;
    }

    public Texture2D Portrait(UWConversationView pOView, bool pbPlayer)
    {
        return fTalk != null ? fTalk.PortraitPicture(pbPlayer) : null;
    }

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
