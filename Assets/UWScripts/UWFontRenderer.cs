using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Renders text in an original font (see UWFont) into a texture. That way
/// subtitles and screen texts look like in the original, instead of being imitated with a
/// Unity font.
///
/// The fonts are 1-bit bitmaps: a pixel is set or not. The colour therefore comes
/// from outside - in the original from the palette, with the cutscene scripts supplying the
/// colour index per text line themselves.
/// </summary>
public static class UWFontRenderer
{
    /// <summary>See UWTextLayout.CharacterSpacing.</summary>
    public const int CharacterSpacing = UWTextLayout.CharacterSpacing;

    /// <summary>See UWTextLayout.LineSpacing.</summary>
    public const int LineSpacing = UWTextLayout.LineSpacing;

    /// <summary>See UWTextLayout.CleanText.</summary>
    public static string CleanText(string psText)
    {
        return UWTextLayout.CleanText(psText);
    }

    /// <summary>See UWTextLayout.WrapText.</summary>
    public static List<string> WrapText(UWFont pOFont, string psText, int piMaxWidth)
    {
        return UWTextLayout.WrapText(pOFont, psText, piMaxWidth);
    }

    /// <summary>Renders lines into a new texture. Centred unless requested otherwise - the
    /// cutscenes need it that way, the message log on the other hand left-aligned. The
    /// background stays transparent so the text can be laid over an image.</summary>
    public static Texture2D RenderLines(UWFont pOFont, List<string> pOLines, int piWidth, Color32 pOColour, FilterMode peFilter, bool pbCentred = true)
    {
        return RenderLines(pOFont, pOLines, piWidth, new Color32[] { pOColour }, peFilter, pbCentred, null);
    }

    /// <summary>
    /// As above, with the colour codes of the game texts: a backslash and a digit in the text
    /// switch to the colour pyColourTable[digit], 0 is the normal colour, and
    /// the switch remains in effect until the next code - even across lines, as
    /// in the original ("\\6     Please enter a save file description:" also colours the typed
    /// line below it). The codes are never drawn. pOLineColours receives, if
    /// given, the colour at the start of each line - for the modern font.
    /// </summary>
    public static Texture2D RenderLines(UWFont pOFont, List<string> pOLines, int piWidth, Color32[] pyColourTable,
        FilterMode peFilter, bool pbCentred, List<Color32> pOLineColours)
    {
        if (pOFont == null || !pOFont.IsLoaded || pOLines == null || pOLines.Count == 0)
            return null;

        Color32 lOColour = pyColourTable != null && pyColourTable.Length > 0
            ? pyColourTable[0]
            : new Color32(255, 255, 255, 255);

        int liLineHeight = pOFont.Height + LineSpacing;
        int liHeight = (liLineHeight * pOLines.Count) - LineSpacing;

        Texture2D lOTexture = new Texture2D(piWidth, liHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWFontRenderer.cs:135";
        lOTexture.filterMode = peFilter;
        lOTexture.wrapMode = TextureWrapMode.Clamp;

        Color32[] lOPixels = new Color32[piWidth * liHeight];

        for (int liIndex = 0; liIndex < lOPixels.Length; liIndex++)
            lOPixels[liIndex] = new Color32(0, 0, 0, 0);

        lOTexture.SetPixels32(lOPixels);

        for (int liLine = 0; liLine < pOLines.Count; liLine++)
        {
            string lsLine = pOLines[liLine];
            int liLineWidth = pOFont.MeasureText(lsLine, 0, CharacterSpacing);
            int liCursor = pbCentred ? Mathf.Max(0, (piWidth - liLineWidth) / 2) : 0;
            int liTop = liLine * liLineHeight;

            if (pOLineColours != null)
                pOLineColours.Add(lOColour);

            for (int liAt = 0; liAt < lsLine.Length; liAt++)
            {
                char lcChar = lsLine[liAt];

                if (UWFont.IsColourCode(lsLine, liAt))
                {
                    int liCode = lsLine[liAt + 1] - '0';

                    if (pyColourTable != null && liCode < pyColourTable.Length)
                        lOColour = pyColourTable[liCode];

                    liAt++;
                    continue;
                }

                if (lcChar == ' ')
                {
                    liCursor += pOFont.SpaceWidth + CharacterSpacing;
                    continue;
                }

                // The character set starts at ASCII 0 - see UWFont.
                if (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph))
                    continue;

                for (int liY = 0; liY < pOFont.Height; liY++)
                {
                    for (int liX = 0; liX < lOGlyph.Width; liX++)
                    {
                        if (lOGlyph.Pixels[(liY * lOGlyph.Width) + liX] == 0)
                            continue;

                        int liTargetX = liCursor + liX;
                        int liTargetY = liHeight - 1 - (liTop + liY);

                        if (liTargetX >= 0 && liTargetX < piWidth && liTargetY >= 0 && liTargetY < liHeight)
                            lOTexture.SetPixel(liTargetX, liTargetY, lOColour);
                    }
                }

                liCursor += lOGlyph.Width + CharacterSpacing;
            }
        }

        lOTexture.Apply();

        return lOTexture;
    }
}
