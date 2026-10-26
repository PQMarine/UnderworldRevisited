using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE ONE PLACE WHERE TEXT IN THE ORIGINAL FONTS BECOMES A UNITY TEXTURE (cleanup step 5.5,
/// 2026-10-19; CLEANUP-PLAN 8.1). The pixels are drawn engine-free (UWTextRaster, decision 26 of
/// the cleanup plan: the interface must not hang on Unity) and only uploaded here, through
/// UWTextureFactory. It replaced UWFontRenderer.RenderLines - a new texture per call, a SetPixel
/// per glyph pixel - and the nine ad-hoc label caches around it, and with them the leaks of B33:
/// the stats lines (one texture per HP or mana tick while the stats page was open), the map's
/// level number, and every caller that forgot to destroy the texture it replaced.
///
/// A TEXT IS ASKED FOR BY WHAT IT SHOWS (font, lines, width, colours, alignment, filter, input
/// box) and handed out as a Handle, counted: labels showing the same text share one texture, and
/// the texture of a text nobody shows any more is kept among the last few idle ones (a stats value
/// going back and forth, the blinking input box) and destroyed once more than IdleKept wait. THE
/// SERVICE OWNS the textures: nobody destroys one it handed out, and a texture handed out is
/// never changed afterwards - the input box is part of what is asked for, not drawn into a
/// finished texture (UWHudMessageLog.fDrawPromptCursor until 2026-10-19).
///
/// Labels hold their text through a Slot (UWTextLabel does, and the modern scroll and the
/// minimap's notes): it compares what is asked with what it holds without allocating, so a text
/// set every frame (the weight, the stack counts) costs a comparison and no key string. A Slot is
/// released when its owner goes (UWTextLabel.OnDestroy), so a scene reload leaves nothing behind.
///
/// Static like UWIconCache rather than a member of UWGameUI: the main menu, the character creation
/// and the cutscenes show text in the original fonts as well.
/// </summary>
public static class UWTextTextures
{
    /// <summary>How many textures of texts nobody shows are kept for a quick return.</summary>
    private const int IdleKept = 32;

    /// <summary>One text's texture, never changed after it is made.</summary>
    public sealed class Handle
    {
        internal readonly UWFont mOFont;

        internal readonly string[] mlsLines;

        internal readonly Color32[] myColours;

        internal readonly int miWidth;

        internal readonly UWTextRaster.ColoursEnum meColours;

        internal readonly bool mbCentred;

        internal readonly FilterMode meFilter;

        internal readonly bool mbCursor;

        internal readonly (UWFont, string) mOKey;

        internal int miRefs;

        internal Handle(UWFont pOFont, string[] plsLines, Color32[] pyColours, int piWidth, UWTextRaster.ColoursEnum peColours,
            bool pbCentred, FilterMode peFilter, bool pbCursor, (UWFont, string) pOKey)
        {
            mOFont = pOFont;
            mlsLines = plsLines;
            myColours = pyColours;
            miWidth = piWidth;
            meColours = peColours;
            mbCentred = pbCentred;
            meFilter = peFilter;
            mbCursor = pbCursor;
            mOKey = pOKey;
        }

        /// <summary>The picture; its size is what the label takes as its own.</summary>
        public Texture2D Texture { get; internal set; }

        /// <summary>The lines drawn.</summary>
        public IReadOnlyList<string> Lines => mlsLines;

        /// <summary>The colour at the start of each line - the modern font colours its lines with it.</summary>
        public List<Color32> LineColours { get; } = new List<Color32>();

        /// <summary>Whether this is the text asked for (compared field by field, nothing allocated).</summary>
        public bool Matches(UWFont pOFont, IReadOnlyList<string> pOLines, int piWidth, Color32[] pyColours,
            UWTextRaster.ColoursEnum peColours, bool pbCentred, FilterMode peFilter, bool pbCursor)
        {
            if (!ReferenceEquals(mOFont, pOFont) || miWidth != piWidth || meColours != peColours || mbCentred != pbCentred
                || meFilter != peFilter || mbCursor != pbCursor || pOLines == null || pOLines.Count != mlsLines.Length
                || (pyColours?.Length ?? 0) != myColours.Length)
                return false;

            for (int liAt = 0; liAt < myColours.Length; liAt++)
            {
                Color32 lOMine = myColours[liAt];
                Color32 lOAsked = pyColours[liAt];

                if (lOMine.r != lOAsked.r || lOMine.g != lOAsked.g || lOMine.b != lOAsked.b || lOMine.a != lOAsked.a)
                    return false;
            }

            for (int liAt = 0; liAt < mlsLines.Length; liAt++)
            {
                if (!string.Equals(mlsLines[liAt], pOLines[liAt] ?? string.Empty))
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// The text one label shows: Set asks the service only when the text changed, releases the one
    /// it held, and says whether it changed; Release lets go of it (when the label goes).
    /// </summary>
    public sealed class Slot
    {
        public Handle Current { get; private set; }

        public Handle Set(UWFont pOFont, IReadOnlyList<string> pOLines, int piWidth, Color32[] pyColours,
            UWTextRaster.ColoursEnum peColours, bool pbCentred, FilterMode peFilter, bool pbCursor, out bool pbChanged)
        {
            if (Current != null && Current.Texture != null
                && Current.Matches(pOFont, pOLines, piWidth, pyColours, peColours, pbCentred, peFilter, pbCursor))
            {
                pbChanged = false;

                return Current;
            }

            Handle lONew = Acquire(pOFont, pOLines, piWidth, pyColours, peColours, pbCentred, peFilter, pbCursor);

            UWTextTextures.Release(Current);
            Current = lONew;
            pbChanged = true;

            return lONew;
        }

        public void Release()
        {
            UWTextTextures.Release(Current);
            Current = null;
        }
    }

    private static readonly Dictionary<(UWFont, string), Handle> msHandles = new Dictionary<(UWFont, string), Handle>();

    /// <summary>The handles nobody holds, the longest unused first.</summary>
    private static readonly List<Handle> msIdle = new List<Handle>();

    /// <summary>Texts held by at least one label.</summary>
    public static int HeldCount => msHandles.Count - msIdle.Count;

    /// <summary>Texts nobody shows, kept for a quick return.</summary>
    public static int IdleCount => msIdle.Count;

    /// <summary>
    /// The texture of a text, counted once more (give it back with Release), or null when there is
    /// nothing to draw. pyColours: the code table (Codes) or a colour per line (PerLine), see
    /// UWTextRaster; null or empty draws white.
    /// </summary>
    public static Handle Acquire(UWFont pOFont, IReadOnlyList<string> pOLines, int piWidth, Color32[] pyColours,
        UWTextRaster.ColoursEnum peColours, bool pbCentred, FilterMode peFilter, bool pbCursor = false)
    {
        if (pOFont == null || !pOFont.IsLoaded || pOLines == null || pOLines.Count == 0 || piWidth <= 0)
            return null;

        string[] lsLines = new string[pOLines.Count];

        for (int liAt = 0; liAt < lsLines.Length; liAt++)
            lsLines[liAt] = pOLines[liAt] ?? string.Empty;

        Color32[] lyColours = pyColours != null ? (Color32[])pyColours.Clone() : new Color32[0];
        (UWFont, string) lOKey = (pOFont, fKey(lsLines, piWidth, lyColours, peColours, pbCentred, peFilter, pbCursor));

        if (!msHandles.TryGetValue(lOKey, out Handle lOHandle))
        {
            lOHandle = new Handle(pOFont, lsLines, lyColours, piWidth, peColours, pbCentred, peFilter, pbCursor, lOKey);
            msHandles[lOKey] = lOHandle;
        }

        // A texture destroyed behind the service's back (none should be) is drawn anew.
        if (lOHandle.Texture == null && !fDraw(lOHandle))
        {
            msHandles.Remove(lOKey);
            msIdle.Remove(lOHandle);

            return null;
        }

        if (lOHandle.miRefs == 0)
            msIdle.Remove(lOHandle);

        lOHandle.miRefs++;

        return lOHandle;
    }

    /// <summary>Gives a text back; the last one to let go puts it among the idle ones.</summary>
    public static void Release(Handle pOHandle)
    {
        if (pOHandle == null || pOHandle.miRefs <= 0)
            return;

        pOHandle.miRefs--;

        if (pOHandle.miRefs > 0)
            return;

        msIdle.Add(pOHandle);

        while (msIdle.Count > IdleKept)
        {
            Handle lOOldest = msIdle[0];

            msIdle.RemoveAt(0);
            msHandles.Remove(lOOldest.mOKey);

            if (lOOldest.Texture != null)
                Object.Destroy(lOOldest.Texture);

            lOOldest.Texture = null;
        }
    }

    private static bool fDraw(Handle pOHandle)
    {
        UWColor32[] lyColours = new UWColor32[pOHandle.myColours.Length];

        for (int liAt = 0; liAt < lyColours.Length; liAt++)
        {
            Color32 lOColour = pOHandle.myColours[liAt];

            lyColours[liAt] = new UWColor32(lOColour.r, lOColour.g, lOColour.b, lOColour.a);
        }

        List<UWColor32> lOLineColours = new List<UWColor32>();
        UWPicture lOPicture = UWTextRaster.Render(pOHandle.mOFont, pOHandle.mlsLines, pOHandle.miWidth, lyColours, pOHandle.meColours,
            pOHandle.mbCentred, pOHandle.mbCursor, lOLineColours);

        if (lOPicture == null)
            return false;

        // The text's colours are its own: no colour help here (the callers pick them, as before).
        pOHandle.Texture = UWTextureFactory.Upload(
            UWUploadPixels.FromColours(lOPicture.Pixels, lOPicture.Width, lOPicture.Height, null, UWUploadPixels.TransparentEnum.Keep),
            lOPicture.Width, lOPicture.Height, pOHandle.meFilter, TextureWrapMode.Clamp, "UWTextTextures " + pOHandle.mlsLines[0]);

        pOHandle.LineColours.Clear();

        foreach (UWColor32 lOColour in lOLineColours)
            pOHandle.LineColours.Add(new Color32(lOColour.R, lOColour.G, lOColour.B, lOColour.A));

        return true;
    }

    private static string fKey(string[] plsLines, int piWidth, Color32[] pyColours, UWTextRaster.ColoursEnum peColours, bool pbCentred,
        FilterMode peFilter, bool pbCursor)
    {
        StringBuilder lOKey = new StringBuilder();

        lOKey.Append(piWidth).Append('|').Append((int)peColours).Append(pbCentred ? 'C' : 'L').Append((int)peFilter)
            .Append(pbCursor ? 'I' : '-').Append('|');

        foreach (Color32 lOColour in pyColours)
            lOKey.Append(((lOColour.r << 24) | (lOColour.g << 16) | (lOColour.b << 8) | lOColour.a).ToString("X8")).Append(',');

        lOKey.Append('|').Append(plsLines.Length);

        foreach (string lsLine in plsLines)
            lOKey.Append('\0').Append(lsLine);

        return lOKey.ToString();
    }
}
