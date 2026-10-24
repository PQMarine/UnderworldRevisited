using System.Collections.Generic;
using UWDataImport.UWData;

/// <summary>
/// Text in an original font (see UWFont), so that subtitles and screen texts look like in the
/// original, instead of being imitated with a Unity font.
///
/// The fonts are 1-bit bitmaps: a pixel is set or not. The colour therefore comes
/// from outside - in the original from the palette, with the cutscene scripts supplying the
/// colour index per text line themselves.
///
/// Only the layout forwards are left here: since cleanup step 5.5 (2026-10-19) the lines are
/// drawn engine-free (UWTextRaster) and uploaded by UWTextTextures, which the labels ask
/// (UWTextLabel.SetText); RenderLines, a new texture per call, is gone.
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
}
