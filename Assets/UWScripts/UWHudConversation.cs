using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The HUD section "the conversation frame and the parchment edge".
/// Its own class since 2026-09-18 (stage two of the HUD rebuild); the owner hands in the game
/// data, the frame, the character, the interaction and the inventory through mOUi. UWGameUI
/// keeps the public entry points as forwards.
/// </summary>
public sealed class UWHudConversation
{
    private readonly UWGameUI mOUi;

    internal UWHudConversation(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    // ------------------------------------------------------------------
    // Conversation screen
    // ------------------------------------------------------------------

    /// <summary>While true, the frame shows CONV.BYT instead of MAIN.BYT and the scroll
    /// stays free for the answers.</summary>
    internal bool mbConversationFrame;

    private Texture2D mOConversationTexture;

    /// <summary>Where the name frames from CONVERSE.GR (picture 0, 94x9) sit in CONV.BYT - left
    /// the conversation partner, right the player character. Measured on CONV.BYT: there 798
    /// of 846 pixels match the frame, the rest is the sample name (2026-09-11).
    /// </summary>
    private const int convNameFrameNpcLeft = 43;

    private const int convNameFramePlayerLeft = 139;

    /// <summary>The empty container background (CONVERSE.GR 5) - the same position as the
    /// panel (see the constants of the rotatable panel further up, PanelLeft/PanelTop).</summary>
    private const int convPanelLeft = 236;

    private const int convPanelTop = 7;

    /// <summary>The colour in which CONV.BYT carries its sample text - in the parchment as in
    /// the scroll. It occurs nowhere else in either area.</summary>
    private const byte convSampleTextIndex = 0x2E;

    /// <summary>
    /// Switches the frame to the conversation screen or back.
    ///
    /// In conversation the original shows CONV.BYT as the whole screen: at the top names and
    /// portraits, in the middle the parchment, at the bottom the scroll for the answers;
    /// paper doll, compass and flasks stay where they are. In our version only the
    /// frame is swapped for mOUi, everything else lies above it anyway.
    /// </summary>
    public void SetConversationFrame(bool pbOn)
    {
        if (mbConversationFrame == pbOn || mOUi.mMainFrameImage == null || mOUi.mOUWData == null)
            return;

        if (pbOn && mOConversationTexture == null)
            mOConversationTexture = fBuildConversationTexture();

        if (pbOn && mOConversationTexture == null)
            return;

        mbConversationFrame = pbOn;
        mOUi.mMainFrameImage.sprite = UWGameUI.fToSprite(pbOn ? mOConversationTexture : mOUi.mOMainTexture);

        fShowParchmentEdge(pbOn);
    }

    /// <summary>The strip of the conversation frame that lies over the compass - see
    /// fShowParchmentEdge.</summary>
    private RawImage mParchmentEdgeImage;

    private const int ParchmentEdgeLeft = 100;

    private const int ParchmentEdgeTop = 131;

    private const int ParchmentEdgeRight = 220;

    private const int ParchmentEdgeBottom = 135;

    /// <summary>
    /// In conversation the parchment's lower edge (rows 131-135 of CONV.BYT) covers the top of
    /// the compass, as in the original. The compass pictures are separate images above the frame,
    /// so without mOUi they lay on top of the parchment (per user, 2026-09-14). The strip is cut
    /// from the conversation frame texture and placed right above the compass images.
    /// </summary>
    private void fShowParchmentEdge(bool pbOn)
    {
        if (!pbOn)
        {
            if (mParchmentEdgeImage != null)
                mParchmentEdgeImage.enabled = false;

            return;
        }

        if (mOConversationTexture == null || mOUi.mGameFrame == null)
            return;

        if (mParchmentEdgeImage == null)
        {
            GameObject lOObject = new GameObject("ParchmentEdge", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mOUi.mGameFrame, false);

            RectTransform lORect = (RectTransform)lOObject.transform;
            lORect.anchorMin = new Vector2(0f, 1f);
            lORect.anchorMax = new Vector2(0f, 1f);
            lORect.pivot = new Vector2(0f, 1f);
            lORect.anchoredPosition = new Vector2(ParchmentEdgeLeft, -ParchmentEdgeTop);
            lORect.sizeDelta = new Vector2(ParchmentEdgeRight - ParchmentEdgeLeft + 1,
                ParchmentEdgeBottom - ParchmentEdgeTop + 1);

            mParchmentEdgeImage = lOObject.GetComponent<RawImage>();
            mParchmentEdgeImage.raycastTarget = false;
        }

        // The frame texture is stored bottom-up (fGetTextureInvert).
        float lfWidth = mOConversationTexture.width;
        float lfHeight = mOConversationTexture.height;

        mParchmentEdgeImage.texture = mOConversationTexture;
        mParchmentEdgeImage.uvRect = new Rect(ParchmentEdgeLeft / lfWidth,
            (lfHeight - ParchmentEdgeBottom - 1) / lfHeight,
            (ParchmentEdgeRight - ParchmentEdgeLeft + 1) / lfWidth,
            (ParchmentEdgeBottom - ParchmentEdgeTop + 1) / lfHeight);

        // Directly above the compass pictures, below everything created later (labels, map, ...).
        int liAbove = -1;

        if (mOUi.Compass.mCompassNeedles != null)
            foreach (Image lONeedle in mOUi.Compass.mCompassNeedles)
                if (lONeedle != null)
                    liAbove = Mathf.Max(liAbove, lONeedle.transform.GetSiblingIndex());

        if (mOUi.Compass.mCompassBackgrounds != null)
            foreach (Image lOBackground in mOUi.Compass.mCompassBackgrounds)
                if (lOBackground != null)
                    liAbove = Mathf.Max(liAbove, lOBackground.transform.GetSiblingIndex());

        if (liAbove >= 0)
            mParchmentEdgeImage.transform.SetSiblingIndex(liAbove + 1);
        else
            mParchmentEdgeImage.transform.SetAsLastSibling();

        mParchmentEdgeImage.enabled = true;
    }

    /// <summary>
    /// CONV.BYT, freed of the sample content. The file is a screen template including
    /// a sample: "Derek" talks to "Tyrone Pop", the parchment holds a greeting, the
    /// scroll two answers. The name frames are covered with the empty frame from
    /// CONVERSE.GR, the portraits later by the real picture. The sample text is
    /// replaced pixel by pixel: every pixel in text colour gets the pixel from a
    /// text-free row of the same area, so the grain of the parchment is preserved.
    /// </summary>
    private Texture2D fBuildConversationTexture()
    {
        UWTexture lOConv;
        UWTexture lONameFrame;

        try
        {
            lOConv = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.CONV, 0);
            lONameFrame = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.CONVERSE, 0);
        }
        catch
        {
            return null;
        }

        if (lOConv == null || lOConv.PaletteIndices == null || lOConv.Width != 320 || lOConv.Height != 200)
            return null;

        byte[] lyIndices = (byte[])lOConv.PaletteIndices.Clone();

        // Parchment: the sample text is in rows 52 to 81, replacement comes from the
        // rows 42 below (93 to 130), which are free.
        fCleanSampleText(lyIndices, 57, 51, 220, 88, 42);

        // Scroll: two answers in rows 169 to 180, replacement 14 below.
        fCleanSampleText(lyIndices, 15, 169, 305, 181, 14);

        if (lONameFrame != null && lONameFrame.PaletteIndices != null)
        {
            fPasteIndices(lyIndices, lONameFrame, convNameFrameNpcLeft, 0);
            fPasteIndices(lyIndices, lONameFrame, convNameFramePlayerLeft, 0);
        }

        // On the right the template also has the sample inventory baked in - lantern, scroll,
        // sword, shield, sacks. Over empty slots of the own character they shone
        // through (per user, 2026-09-11). The empty container background from CONVERSE.GR
        // (picture 5, 84x114) sits in MAIN.BYT pixel-exactly at 236/7 - it goes there
        // here too.
        UWTexture lOPanel = null;

        try
        {
            lOPanel = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.CONVERSE, 5);
        }
        catch
        {
        }

        if (lOPanel != null && lOPanel.PaletteIndices != null)
            fPasteIndices(lyIndices, lOPanel, convPanelLeft, convPanelTop);

        // Two sample runes (In Mani) are also baked into the shelf, at 176-204 / 138-151
        // (compared against MAIN.BYT). The three shelf slots come from MAIN.BYT, where they are empty
        // (per user, 2026-09-13).
        fCopyIndicesFromMain(lyIndices, 176, 138, 220, 152);

        // Two more samples of the template (per user, 2026-09-14): the carried weight "10.2" in
        // red below the command icons (text and its black box at 13-35 / 126-134) and the yellow
        // crosshair pointer right of the mana flask (rows 121-127; its upper part lies under the
        // container background pasted above). MAIN.BYT is plain black at both places.
        fCopyIndicesFromMain(lyIndices, 13, 126, 35, 134);
        fCopyIndicesFromMain(lyIndices, 305, 121, 319, 127);

        UWTexture lOCleaned = new UWTexture("CONV.BYT", lyIndices, UWTexture.TextureTypes.CONV,
            lOConv.Palettes, lOConv.MainPaletteIndex);

        Texture2D lOTexture = new Texture2D(lOCleaned.Width, lOCleaned.Height, TextureFormat.ARGB32, false);
        lOTexture.name = "UWGameUI.cs:7324";
        lOTexture.filterMode = mOUi.TextureFilterMode;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOCleaned));
        lOTexture.Apply();

        return lOTexture;
    }

    private static void fCleanSampleText(byte[] pyIndices, int piLeft, int piTop, int piRight, int piBottom, int piSourceRowOffset)
    {
        for (int liY = piTop; liY <= piBottom; liY++)
        {
            for (int liX = piLeft; liX <= piRight; liX++)
            {
                int liAt = (liY * 320) + liX;

                if (pyIndices[liAt] != convSampleTextIndex)
                    continue;

                int liSource = ((liY + piSourceRowOffset) * 320) + liX;

                if (liSource >= 0 && liSource < pyIndices.Length)
                    pyIndices[liAt] = pyIndices[liSource];
            }
        }
    }

    /// <summary>Takes over a rectangle (including the bounds) from MAIN.BYT.</summary>
    private void fCopyIndicesFromMain(byte[] pyTarget, int piLeft, int piTop, int piRight, int piBottom)
    {
        UWTexture lOMain;

        try
        {
            lOMain = mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);
        }
        catch
        {
            return;
        }

        if (lOMain == null || lOMain.PaletteIndices == null || lOMain.Width != 320 || lOMain.Height != 200)
            return;

        for (int liY = piTop; liY <= piBottom; liY++)
            for (int liX = piLeft; liX <= piRight; liX++)
                pyTarget[(liY * 320) + liX] = lOMain.PaletteIndices[(liY * 320) + liX];
    }

    private static void fPasteIndices(byte[] pyTarget, UWTexture pOSource, int piLeft, int piTop)
    {
        for (int liY = 0; liY < pOSource.Height; liY++)
        {
            for (int liX = 0; liX < pOSource.Width; liX++)
            {
                int liTargetX = piLeft + liX;
                int liTargetY = piTop + liY;

                if (liTargetX < 0 || liTargetX >= 320 || liTargetY < 0 || liTargetY >= 200)
                    continue;

                pyTarget[(liTargetY * 320) + liTargetX] = pOSource.PaletteIndices[(liY * pOSource.Width) + liX];
            }
        }
    }

}
