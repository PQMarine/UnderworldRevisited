using System.Collections;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE VICTORY SCREENS after the end sequence (cleanup step 8.7, 2026-10-24: out of
/// UWIntroPlayer): WIN1.BYT, then WIN2.BYT with the character's stats, each until a key or a
/// click. UWIntroPlayer.PlayVictoryScreens starts it (the input lock and the pointer stay
/// there); the pictures go through UWCutsceneView. Moved as it was.
/// </summary>
public sealed class UWVictoryScreens
{
    private readonly UWIntroPlayer mOPlayer;

    private readonly UWCutsceneView mOView;

    public UWVictoryScreens(UWIntroPlayer pOPlayer, UWCutsceneView pOView)
    {
        mOPlayer = pOPlayer;
        mOView = pOView;
    }

    /// <summary>
    /// The two victory screens after UW.EXE EndGameFunctions_ovr143_1186: WIN1.BYT until a
    /// key press or click, then WIN2.BYT with the character's stats, again until a
    /// key press. Both with palette 7 (see UWTextures).
    /// </summary>
    public IEnumerator Run()
    {
        mOView.SetBrightness(1f);

        if (mOView.DrawFullScreenBitmap(UWTexture.TextureTypes.WIN1))
        {
            mOView.ShowPicture();
            yield return fWaitForKeyOrClick();
        }

        if (mOView.DrawFullScreenBitmap(UWTexture.TextureTypes.WIN2))
        {
            fDrawVictoryStats();
            mOView.ShowPicture();
            yield return fWaitForKeyOrClick();
        }

        mOPlayer.Finish();
    }

    /// <summary>Waits for a key or a click. First wait one frame so that the
    /// click that led here does not count right away.</summary>
    private IEnumerator fWaitForKeyOrClick()
    {
        yield return null;

        mOPlayer.AdvanceRequested = false;

        while (!mOPlayer.AdvanceRequested && !UWIntroPlayer.WasSkipPressed())
            yield return null;

        mOPlayer.AdvanceRequested = false;
    }

    /// <summary>Height of the first line on WIN2, in pixels from the bottom (UW.EXE: di = 0xB4).
    /// </summary>
    private const int VictoryFirstLineY = 0xB4;

    /// <summary>Horizontal centre around which the four header lines are placed.</summary>
    private const int VictoryCentreX = 0xA0;

    /// <summary>Left column of the six attributes, right column, offset to the value.</summary>
    private const int VictoryAttributeLeftX = 0x50;

    private const int VictoryAttributeRightX = 0xBE;

    private const int VictoryAttributeValueOffset = 0x2D;

    /// <summary>The skills: three columns from 50 spaced 74 apart, value right-aligned at
    /// column plus 70.</summary>
    private const int VictorySkillFirstX = 0x32;

    private const int VictorySkillColumnWidth = 0x4A;

    private const int VictorySkillValueRight = 0x46;

    /// <summary>Font colour 0x5C (UW.EXE FontColour), with the palette of the pictures.</summary>
    private const int VictoryFontColour = 0x5C;

    private const int VictoryPalette = 7;

    /// <summary>
    /// The stats on WIN2 - line by line after UW.EXE VictoryScreen_ovr143_76F, font
    /// fontchar.sys:
    ///   Name                                                  centred
    ///   "A level " level " " class                            centred
    ///   "Banished the Slasher of Veils"                       centred
    ///   "after " days " days in the Abyss"                    centred
    ///   Str/Dex/Int left at 80, Vit/Mana/Exp right at 190, value 45 beside it, Exp divided by 10
    ///   (one line lower than the header lines)
    ///   the twenty skills in three columns, directly below the attributes
    /// Days = clock / 0x1C2000 / 12. In the original the lines count from the bottom; whether the height
    /// means the foot or the head of a line is not stated there. First built as foot - then
    /// the name sat a little too high (per user, 2026-09-14). Now as head, everything one
    /// font height lower (assumption, not exactly visible in the original because of DOSBox).
    /// </summary>
    private void fDrawVictoryStats()
    {
        DataImport lOData = mOPlayer.Data;
        UWFont lOFont = lOData.Fonts.Get(UWFonts.FontType.CharacterGeneration);
        UWCharacter lOCharacter = UWScene.Character;
        UWPlayerData lOPlayer = lOData.InitialPlayer;

        if (lOFont == null || !lOFont.IsLoaded || lOCharacter == null)
            return;

        Color32 lOColour = fGetPaletteColour(VictoryPalette, VictoryFontColour);
        int liHeight = lOFont.Height;
        int liY = VictoryFirstLineY;

        fDrawCentred(lOFont, lOPlayer != null ? lOPlayer.Name : string.Empty, liY, lOColour);

        liY -= liHeight;
        fDrawCentred(lOFont, fGetString(1, 0xBB) + lOCharacter.Vitals.Level + " "
            + fGetString(2, 0x17 + (lOPlayer != null ? lOPlayer.CharacterClass : 0)), liY, lOColour);

        liY -= liHeight;
        fDrawCentred(lOFont, fGetString(1, 0xBC), liY, lOColour);

        int liDays = (int)((uint)lOCharacter.Vitals.ClockValue / 0x1C2000u) / 12;

        liY -= liHeight;
        fDrawCentred(lOFont, fGetString(1, 0xBD) + liDays + fGetString(1, 0xBE), liY, lOColour);

        liY -= liHeight;

        int[] liValues =
        {
            lOCharacter.Vitals.Strength,
            lOCharacter.Vitals.Dexterity,
            lOCharacter.Vitals.Intelligence,
            lOCharacter.Vitals.MaxHP,
            lOCharacter.Vitals.MaxMana,
            lOCharacter.Vitals.Experience / 10
        };

        for (int liAt = 0; liAt < liValues.Length; liAt++)
        {
            int liX = liAt / 3 != 0 ? VictoryAttributeRightX : VictoryAttributeLeftX;
            int liRowY = liY - ((liAt % 3) * liHeight);

            fDrawText(lOFont, fGetString(2, 0x11 + liAt), liX, liRowY, lOColour);
            fDrawText(lOFont, liValues[liAt].ToString(), liX + VictoryAttributeValueOffset, liRowY, lOColour);
        }

        liY -= 2 * liHeight;

        for (int liAt = 0; liAt < UWPlayerVitals.SkillNumberCount; liAt++)
        {
            if (liAt % 3 == 0)
                liY -= liHeight;

            int liX = ((liAt % 3) * VictorySkillColumnWidth) + VictorySkillFirstX;
            string lsValue = lOCharacter.Vitals.GetSkillByNumber(liAt).ToString();

            fDrawText(lOFont, fGetString(2, 0x1F + liAt), liX, liY, lOColour);
            fDrawText(lOFont, lsValue,
                liX + VictorySkillValueRight - lOFont.MeasureText(lsValue, 0, UWFontRenderer.CharacterSpacing), liY, lOColour);
        }

        mOView.UploadPicture();
    }

    /// <summary>A string by game numbering - our export is one higher.</summary>
    private string fGetString(int piBlock, int piIndex)
    {
        return UWText.Block(mOPlayer.Data, piBlock, piIndex + 1);
    }

    private Color32 fGetPaletteColour(int piPalette, int piIndex)
    {
        try
        {
            byte[] lyRgb = mOPlayer.Data.Palettes.GetPalette(piPalette).GetRGB((byte)piIndex);

            return new Color32(lyRgb[0], lyRgb[1], lyRgb[2], 255);
        }
        catch
        {
            return new Color32(255, 255, 255, 255);
        }
    }

    private void fDrawCentred(UWFont pOFont, string psText, int piY, Color32 pOColour)
    {
        int liWidth = pOFont.MeasureText(psText, 0, UWFontRenderer.CharacterSpacing);

        fDrawText(pOFont, psText, VictoryCentreX - (liWidth / 2), piY, pOColour);
    }

    /// <summary>Writes text into the picture track; piX/piY is the top-left corner, counted from
    /// the bottom as in the original.</summary>
    private void fDrawText(UWFont pOFont, string psText, int piX, int piY, Color32 pOColour)
    {
        Color32[] lOBuffer = mOView.PictureBuffer;

        if (string.IsNullOrEmpty(psText) || lOBuffer == null)
            return;

        int liCursor = piX;

        for (int liAt = 0; liAt < psText.Length; liAt++)
        {
            char lcChar = psText[liAt];

            if (lcChar == ' ')
            {
                liCursor += pOFont.SpaceWidth + UWFontRenderer.CharacterSpacing;
                continue;
            }

            if (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph))
                continue;

            for (int liRow = 0; liRow < pOFont.Height; liRow++)
            {
                int liTargetY = piY - liRow;

                if (liTargetY < 0 || liTargetY >= UWCutsceneView.ScreenHeight)
                    continue;

                for (int liColumn = 0; liColumn < lOGlyph.Width; liColumn++)
                {
                    int liTargetX = liCursor + liColumn;

                    if (lOGlyph.Pixels[(liRow * lOGlyph.Width) + liColumn] == 0 || liTargetX < 0 || liTargetX >= UWCutsceneView.ScreenWidth)
                        continue;

                    lOBuffer[(liTargetY * UWCutsceneView.ScreenWidth) + liTargetX] = pOColour;
                }
            }

            liCursor += lOGlyph.Width + UWFontRenderer.CharacterSpacing;
        }
    }
}
