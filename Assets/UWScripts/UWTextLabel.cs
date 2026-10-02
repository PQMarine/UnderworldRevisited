using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// A text field that shows either the original font or a modern one.
///
/// The original font is a texture (UWFontRenderer) held in a RawImage. For
/// the modern font the same RawImage gets a child with a Unity Text that fills the
/// area and shows the same lines in the same colours - just at
/// screen resolution. Exactly one of the two is always visible. The choice applies to all
/// fields at once (key F, see UWGameUI) and persists via PlayerPrefs. Reading
/// the original font is tiring (per user, 2026-09-11).
///
/// THE LINES STAY THOSE OF THE ORIGINAL: wrapping still uses the metrics of
/// font5x6p, and the modern font gets exactly the line spacing of the original font.
/// That way line count, paging and click areas match in both fonts, and
/// the modern font, which runs narrower, has room in every line.
///
/// THE MODERN FONT IS LEXEND EXA (per user, 2026-09-28; SIL Open Font License, see
/// THIRD_PARTY_NOTICES.md): Unity's built-in font filled only about two thirds of a line the
/// original font fills. Measured on a sample sentence against font5x6p (whose glyph widths
/// already hold the gap, CharacterSpacing 0), both squeezed alike by the 4:3 frame: at the
/// capital height of the original (5 of its 6 rows) Lexend Exa runs to 99 percent of the
/// original's line length, the built-in font to about 70. So its size follows the capital
/// height, not the line pitch (ModernSizePerPitch); the lines keep the original's pitch.
/// Lexend Tera, taken first on a measurement that counted a gap too many, ran a fifth too wide
/// (per user with screenshots).
///
/// Every place that draws text calls Set with texture and lines, and Hide to
/// hide it - no longer RawImage.enabled, otherwise the switching would get mixed up.
/// </summary>
public class UWTextLabel : MonoBehaviour
{
    private const string PrefsKey = "UWModernFont";

    /// <summary>The modern font under Resources, and the built-in one if it is missing.</summary>
    private const string ModernFontResource = "Fonts/LexendExa";

    private const string FallbackFontFile = "LegacyRuntime.ttf";

    /// <summary>Font size per unit of the original line pitch: font5x6p's capitals take 5 of
    /// its 6 rows, Lexend's capitals 0.7 of its size, so 5 / 6 / 0.7 = 1.19 - at the pitch of 6
    /// that is size 7.</summary>
    private const float ModernSizePerPitch = 5f / 6f / 0.7f;

    /// <summary>
    /// How much of its size one line of Lexend Exa inks from the top of an ascender to the bottom
    /// of a descender: 'l' reaches 740, 'g' and 'y' down to -230, of 1000 units (glyph boxes read
    /// from the font file, 2026-09-30). A line must not ink more than the original's pitch, or
    /// the descenders of one line run into the next line's ascenders (per user, 2026-09-30, on the
    /// conversation scroll: at size 7 on the pitch of 6 the letters of neighbouring lines
    /// touched, and the [MORE] under the last line stuck out of the box). So the size is also
    /// held to pitch / 0.97 - 6 at the pitch of 6.
    /// </summary>
    private const float ModernInkPerSize = 0.97f;

    private static bool mbLoaded;

    private static bool mbModern;

    private static Font mOModernFont;

    /// <summary>Line pitch per font size: what Unity uses for a value of 1,
    /// measured once.</summary>
    private static readonly Dictionary<int, float> mOLinePitch = new Dictionary<int, float>();

    /// <summary>Modern font instead of the original font, for all fields.</summary>
    public static bool UseModernFont
    {
        get
        {
            if (!mbLoaded)
            {
                mbModern = PlayerPrefs.GetInt(PrefsKey, 0) != 0;
                mbLoaded = true;
            }

            return mbModern;
        }
        set
        {
            mbModern = value;
            mbLoaded = true;
            PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
        }
    }

    private RawImage mOImage;
    private Text mOText;
    private bool mbShown;
    private bool mbAppliedModern;

    /// <summary>The field for a RawImage - created the first time.</summary>
    public static UWTextLabel Get(RawImage pOImage)
    {
        if (pOImage == null)
            return null;

        UWTextLabel lOLabel = pOImage.GetComponent<UWTextLabel>();

        if (lOLabel == null)
        {
            lOLabel = pOImage.gameObject.AddComponent<UWTextLabel>();
            lOLabel.mOImage = pOImage;
        }

        return lOLabel;
    }

    /// <summary>Hides the field for a RawImage, if there is one.</summary>
    public static void Hide(RawImage pOImage)
    {
        if (pOImage == null)
            return;

        UWTextLabel lOLabel = pOImage.GetComponent<UWTextLabel>();

        if (lOLabel != null)
            lOLabel.Hide();
        else
            pOImage.enabled = false;
    }

    /// <summary>Whether the field is currently showing something - in whichever font.</summary>
    public bool IsShown
    {
        get { return mbShown; }
    }

    /// <summary>
    /// Shows lines. pOTexture is the fully drawn original font (the RawImage
    /// gets it and its size), pOLines are the same lines for the modern font,
    /// pOColours their colours - one per line, or a single one for all. pfExtraRaise moves the
    /// modern font up by so many canvas units on top of the measured capital gap, pfShiftRight
    /// to the right.
    /// </summary>
    public void Set(UWFont pOFont, Texture2D pOTexture, List<string> pOLines, List<Color32> pOColours,
        bool pbCentred, FontStyle peStyle = FontStyle.Normal, float pfExtraRaise = ModernExtraRaise,
        float pfShiftRight = 0f)
    {
        if (mOImage == null)
            mOImage = GetComponent<RawImage>();

        if (pOTexture == null || pOLines == null || pOLines.Count == 0)
        {
            Hide();
            return;
        }

        mOImage.texture = pOTexture;
        ((RectTransform)mOImage.transform).sizeDelta = new Vector2(pOTexture.width, pOTexture.height);

        fEnsureText();

        int liPitch = pOFont != null ? pOFont.Height + UWFontRenderer.LineSpacing : 6;

        mOText.fontSize = Mathf.Max(1, Mathf.Min(Mathf.Min(Mathf.RoundToInt(liPitch * ModernSizePerPitch),
            Mathf.FloorToInt(liPitch / ModernInkPerSize)), fGetWidthLimitedSize(pOFont, mOText)));
        mOText.fontStyle = peStyle;
        mOText.alignment = pbCentred ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
        mOText.lineSpacing = fGetLineSpacing(mOText, liPitch);
        mOText.text = fBuildRichText(pOLines, pOColours);

        // THE CAPITALS START WHERE THE ORIGINAL'S DO (per user, 2026-09-28: Lexend sat a little
        // too low): Unity puts the first line under the font's full ascender, which in Lexend
        // leaves a third of the size empty above the capitals. The text moves up by that gap
        // minus the original's own blank rows above its capitals.
        float lfShift = fGetCapitalTopGap(mOText) - fGetOriginalCapitalTop(pOFont) + pfExtraRaise;
        RectTransform lOTextRect = (RectTransform)mOText.transform;

        lOTextRect.offsetMin = new Vector2(pfShiftRight, lfShift);
        lOTextRect.offsetMax = new Vector2(pfShiftRight, lfShift);

        mbShown = true;
        fApply();
    }

    /// <summary>The correction on top of the measured capital gap, judged by eye in canvas units
    /// (per user, 2026-09-28, in three rounds on the message scroll): the text lines sit best
    /// half a unit LOWER, the [MORE] marker half a unit HIGHER (MoreMarkerRaise) - one value for
    /// both left either the lines too high or the marker too low.</summary>
    public const float ModernExtraRaise = -0.5f;

    public const float MoreMarkerRaise = 0.5f;

    private static TextGenerator msOCapitalProbe;

    /// <summary>The scale the capital gap is measured at - see fGetCapitalTopGap.</summary>
    private const float CapitalProbeScale = 16f;

    /// <summary>How far below the top of its area the modern font draws the top of a capital,
    /// in canvas units - asked of Unity's own text generator with the field's settings.</summary>
    private static float fGetCapitalTopGap(Text pOText)
    {
        if (msOCapitalProbe == null)
            msOCapitalProbe = new TextGenerator();

        TextGenerationSettings lOSettings = pOText.GetGenerationSettings(new Vector2(1000f, 1000f));

        lOSettings.pivot = new Vector2(0f, 1f);
        lOSettings.textAnchor = TextAnchor.UpperLeft;
        lOSettings.richText = false;

        // A FIXED, FINE SCALE (per user, 2026-09-28: the weight sat differently right after
        // loading than after it changed): the canvas' own factor is not settled on the first
        // frames, and at a factor near 1 the generator rounds a 7 point glyph to whole pixels -
        // so the first measurement of a field differed from every later one.
        lOSettings.scaleFactor = CapitalProbeScale;
        lOSettings.generationExtents = new Vector2(1000f, 1000f) * CapitalProbeScale;

        if (!msOCapitalProbe.PopulateWithErrors("H", lOSettings, pOText.gameObject))
            return 0f;

        IList<UIVertex> lOVertices = msOCapitalProbe.verts;

        if (lOVertices.Count == 0)
            return 0f;

        float lfTop = float.MinValue;

        foreach (UIVertex lOVertex in lOVertices)
            lfTop = Mathf.Max(lfTop, lOVertex.position.y);

        // The generator works in screen pixels; the Text divides by the same factor.
        float lfScale = lOSettings.scaleFactor > 0f ? lOSettings.scaleFactor : 1f;

        return Mathf.Max(0f, -lfTop / lfScale);
    }

    /// <summary>A sentence both fonts are measured on for the width limit.</summary>
    private const string WidthSample = "The quick brown fox jumps over the lazy dog. Welcome to the Stygian Abyss, Avatar!";

    /// <summary>The size the width is measured at - see fGetWidthLimitedSize.</summary>
    private const int WidthProbeSize = 100;

    /// <summary>Per original font: the largest modern size whose line is no longer than the
    /// original's, or int.MaxValue where the font cannot be measured.</summary>
    private static readonly Dictionary<UWFont, int> mOWidthLimitedSize = new Dictionary<UWFont, int>();

    /// <summary>
    /// THE MODERN LINE NO WIDER THAN THE ORIGINAL'S (per user, 2026-09-29: in the cutscenes the
    /// modern font ran "a little too wide"). The size from the capital height (ModernSizePerPitch)
    /// was measured on font5x6p, where Lexend at that size fills 99 percent of the line. FONTBIG
    /// (cutscenes, main menu, map) is narrower for its height - 626 units for the sample against
    /// 370 of font5x6p at more than twice the capital height (11 rows against 5) - so the same
    /// rule drew its lines about half again as long as the original's, past the wrapping that
    /// still follows the original's metrics. The width rule gives the size at which the sample
    /// sentence is exactly as long in both fonts; the smaller of the two sizes is used, so
    /// font5x6p keeps its size 7.
    ///
    /// Fonts without all glyphs of the sample (FONT4X5P, FONTBUTN: digits and capitals only)
    /// cannot be measured this way and keep the height rule.
    /// </summary>
    private static int fGetWidthLimitedSize(UWFont pOFont, Text pOText)
    {
        if (pOFont == null || !pOFont.IsLoaded)
            return int.MaxValue;

        if (mOWidthLimitedSize.TryGetValue(pOFont, out int liCached))
            return liCached;

        int liSize = int.MaxValue;
        bool lbComplete = true;

        foreach (char lcChar in WidthSample)
        {
            if (lcChar != ' ' && (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph) || lOGlyph.Width <= 0))
            {
                lbComplete = false;
                break;
            }
        }

        if (lbComplete)
        {
            int liOriginal = pOFont.MeasureText(WidthSample, 0, UWFontRenderer.CharacterSpacing);

            TextGenerationSettings lOSettings = pOText.GetGenerationSettings(new Vector2(100000f, 1000f));

            lOSettings.fontSize = WidthProbeSize;
            lOSettings.fontStyle = FontStyle.Normal;
            lOSettings.richText = false;
            lOSettings.scaleFactor = 1f;

            float lfModern = new TextGenerator().GetPreferredWidth(WidthSample, lOSettings);

            if (liOriginal > 0 && lfModern > 0f)
                liSize = Mathf.RoundToInt(WidthProbeSize * liOriginal / lfModern);

            // Once per font.
            Debug.Log("UWTextLabel: modern font for a " + pOFont.Height + " row font limited to size "
                + liSize + " by width (sample " + liOriginal + " original, " + lfModern.ToString("0")
                + " modern at " + WidthProbeSize + ").");
        }

        mOWidthLimitedSize[pOFont] = liSize;

        return liSize;
    }

    /// <summary>The blank rows above a capital in the original font (the first row of 'H'
    /// that holds a pixel).</summary>
    private static int fGetOriginalCapitalTop(UWFont pOFont)
    {
        if (pOFont == null || !pOFont.TryGetGlyph('H', out UWFont.Glyph lOGlyph)
            || lOGlyph.Pixels == null || lOGlyph.Width <= 0)
            return 0;

        for (int liRow = 0; liRow < pOFont.Height; liRow++)
        {
            for (int liColumn = 0; liColumn < lOGlyph.Width; liColumn++)
            {
                int liAt = (liRow * lOGlyph.Width) + liColumn;

                if (liAt < lOGlyph.Pixels.Length && lOGlyph.Pixels[liAt] != 0)
                    return liRow;
            }
        }

        return 0;
    }

    public void Hide()
    {
        mbShown = false;
        fApply();
    }

    /// <summary>Shows again whatever was last set.</summary>
    public void Show()
    {
        if (mOImage == null)
            mOImage = GetComponent<RawImage>();

        if (mOImage == null || mOImage.texture == null)
            return;

        mbShown = true;
        fApply();
    }

    private void LateUpdate()
    {
        if (mbShown && mbAppliedModern != UseModernFont)
            fApply();
    }

    private void fApply()
    {
        if (mOImage == null)
            mOImage = GetComponent<RawImage>();

        bool lbModern = UseModernFont;

        mbAppliedModern = lbModern;

        if (mOImage != null)
            mOImage.enabled = mbShown && !lbModern;

        if (mOText != null)
            mOText.enabled = mbShown && lbModern;
    }

    private void fEnsureText()
    {
        if (mOText != null)
            return;

        if (mOModernFont == null)
            mOModernFont = Resources.Load<Font>(ModernFontResource);

        if (mOModernFont == null)
            mOModernFont = Resources.GetBuiltinResource<Font>(FallbackFontFile);

        GameObject lOObject = new GameObject("Modern Font", typeof(RectTransform), typeof(Text));
        lOObject.transform.SetParent(transform, false);

        // The whole area of the RawImage - that way position and lines match.
        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.one;
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;

        mOText = lOObject.GetComponent<Text>();
        mOText.font = mOModernFont;
        mOText.supportRichText = true;
        mOText.horizontalOverflow = HorizontalWrapMode.Overflow;
        mOText.verticalOverflow = VerticalWrapMode.Overflow;
        mOText.raycastTarget = false;
        mOText.color = Color.white;
        mOText.enabled = false;
    }

    /// <summary>
    /// The line spacing at which a line of the modern font becomes exactly as tall as
    /// one of the original font. Measured rather than estimated: Unity itself says how tall
    /// one and how tall two lines are at spacing 1.
    /// </summary>
    private static float fGetLineSpacing(Text pOText, int piPitch)
    {
        float lfNatural;

        if (!mOLinePitch.TryGetValue(pOText.fontSize, out lfNatural))
        {
            string lsText = pOText.text;
            float lfSpacing = pOText.lineSpacing;

            pOText.lineSpacing = 1f;
            pOText.text = "Ag";
            float lfOne = pOText.preferredHeight;
            pOText.text = "Ag\nAg";
            float lfTwo = pOText.preferredHeight;

            pOText.text = lsText;
            pOText.lineSpacing = lfSpacing;

            lfNatural = lfTwo - lfOne;

            if (lfNatural <= 0f)
                lfNatural = pOText.fontSize;

            mOLinePitch[pOText.fontSize] = lfNatural;
        }

        return piPitch / lfNatural;
    }

    /// <summary>The colour codes of the game texts (see UWFont.IsColourCode) do not belong in
    /// the modern font; their colour comes per line from pOColours.</summary>
    private static string fStripColourCodes(string psText)
    {
        if (psText.IndexOf('\\') < 0)
            return psText;

        System.Text.StringBuilder lOClean = new System.Text.StringBuilder(psText.Length);

        for (int liAt = 0; liAt < psText.Length; liAt++)
        {
            if (UWFont.IsColourCode(psText, liAt))
            {
                liAt++;
                continue;
            }

            lOClean.Append(psText[liAt]);
        }

        return lOClean.ToString();
    }

    private static string fBuildRichText(List<string> pOLines, List<Color32> pOColours)
    {
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();

        for (int liLine = 0; liLine < pOLines.Count; liLine++)
        {
            if (liLine > 0)
                lOText.Append('\n');

            Color32 lOColour = pOColours == null || pOColours.Count == 0
                ? new Color32(255, 255, 255, 255)
                : pOColours[liLine < pOColours.Count ? liLine : pOColours.Count - 1];

            // Unity Text treats angle brackets as markup; they do not occur in the game
            // text, but the names of save games could contain some.
            // A LINE BREAK INSIDE A LINE draws nothing in the original font, which has no glyph for
            // it, but Unity breaks the line there - an extra line that pushed [MORE] out of the
            // box (per user, 2026-09-30: Ketchaval's "the Gray tribe.\n Years ago,"). The lines
            // are already wrapped, so it goes.
            string lsLine = fStripColourCodes(pOLines[liLine] ?? string.Empty).Replace("\r", string.Empty)
                .Replace("\n", string.Empty).Replace("<", "‹").Replace(">", "›");

            lOText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(lOColour)).Append('>')
                .Append(lsLine).Append("</color>");
        }

        return lOText.ToString();
    }
}
