using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// THE MODERN SCHEME'S OWN LAYOUT (per user on a mockup, 2026-10-04): the parts of the modern UI
/// can be moved and sized in the layout editor (UWModernLayoutEditor, "Edit layout" in the game
/// menu) - the action bar, the minimap, the heading strip, the active spells, the messages, the
/// vitality and mana shelf and the bags (the backpack; the bag windows stack from it, away from the
/// nearer screen edges). EVERYTHING, per user the same day ("it works so well that anything left
/// out looks inconsistent"): the character panel and the rune panel too. Left where they are they
/// stay DOCKED at their edges with their handles, sliding out and in; MOVED they are free windows
/// without a handle that show and hide at once (C, Z, Escape) - per user, sliding only while not
/// moved. The right button in the editor docks them again. (The minimap no longer goes into the
/// character panel's head; placed freely it sits wherever one wants it - per user.) The
/// CONVERSATION as well,
/// through a preview with sample text the editor shows on its Dialog button (UWModernConversation;
/// its text size stays its own, the - and + in its history).
///
///   - Each part computes its default place and size as before and asks Place for where it goes;
///     its own SIZE factor (Scale, 50 to 200 % of the UI size) it applies itself, so pictures and
///     text are drawn at that size, not stretched.
///   - A moved part is kept as an ANCHOR - the nearest corner, edge or the centre of the screen,
///     per axis by thirds - and its distance from it in original pixels at the UI size, so the
///     layout keeps its sense at another resolution or aspect.
///   - Kept in the settings (UWUserSettings.ModernLayout), for every game alike.
///   - Each part reports where it was drawn (Report), for the editor's frames and its snapping.
/// </summary>
public static class UWModernLayout
{
    public enum ElementEnum
    {
        ActionBar,
        Minimap,
        Heading,
        Spells,
        Messages,
        Vitals,
        Bags,
        CharacterPanel,
        RunePanel,
        Conversation
    }

    public const int ElementCount = 10;

    public static readonly string[] Names =
    {
        "Action bar", "Minimap", "Heading", "Active spells", "Messages", "Vitality and mana", "Bags", "Character panel", "Rune panel",
        "Conversation"
    };

    /// <summary>A panel left at its edge: it slides out and in on its handle.</summary>
    public static bool IsDocked(ElementEnum peElement)
    {
        return (peElement == ElementEnum.CharacterPanel || peElement == ElementEnum.RunePanel) && !IsPlaced(peElement);
    }

    public const int MinPercent = 50;

    public const int MaxPercent = 200;

    public const int PercentStep = 10;

    private sealed class Placement
    {
        public bool Placed;

        public Vector2 Anchor;

        /// <summary>From the screen's anchor point to the part's own, original pixels at the UI size.</summary>
        public Vector2 Offset;

        public int Percent = 100;
    }

    private static Placement[] msPlacements;

    private static readonly Rect[] msRects = new Rect[ElementCount];

    private static readonly int[] miRectFrames = { -10, -10, -10, -10, -10, -10, -10, -10, -10, -10 };

    /// <summary>The layout editor is open (the game menu's Layout page).</summary>
    public static bool IsEditing => UWModernHud.Instance != null && UWModernHud.Instance.Page == UWModernHud.PageEnum.Layout;

    public static int Percent(ElementEnum peElement)
    {
        return fGet(peElement).Percent;
    }

    /// <summary>The part's own size factor, on top of the UI size.</summary>
    public static float Scale(ElementEnum peElement)
    {
        return Percent(peElement) / 100f;
    }

    public static bool IsPlaced(ElementEnum peElement)
    {
        return fGet(peElement).Placed;
    }

    /// <summary>Where the part goes: its default rect (screen pixels from the bottom left, already
    /// at its own size) unless it was moved; kept on the screen.</summary>
    public static Rect Place(ElementEnum peElement, Rect pODefault)
    {
        Placement lOPlacement = fGet(peElement);

        if (!lOPlacement.Placed)
            return pODefault;

        Vector2 lOAnchorPoint = new Vector2(lOPlacement.Anchor.x * Screen.width, lOPlacement.Anchor.y * Screen.height)
            + (lOPlacement.Offset * UWModernHud.PixelScale);
        float lfX = lOAnchorPoint.x - (lOPlacement.Anchor.x * pODefault.width);
        float lfY = lOAnchorPoint.y - (lOPlacement.Anchor.y * pODefault.height);

        lfX = Mathf.Clamp(lfX, 0f, Mathf.Max(0f, Screen.width - pODefault.width));
        lfY = Mathf.Clamp(lfY, 0f, Mathf.Max(0f, Screen.height - pODefault.height));

        return new Rect(Mathf.Round(lfX), Mathf.Round(lfY), pODefault.width, pODefault.height);
    }

    /// <summary>Where the part was drawn this frame.</summary>
    public static void Report(ElementEnum peElement, Rect pORect)
    {
        msRects[(int)peElement] = pORect;
        miRectFrames[(int)peElement] = Time.frameCount;
    }

    /// <summary>Where the part was drawn last - false when it did not show in the last frames.</summary>
    public static bool TryGetRect(ElementEnum peElement, out Rect pORect)
    {
        pORect = msRects[(int)peElement];

        return Time.frameCount - miRectFrames[(int)peElement] <= 2 && pORect.width > 0f;
    }

    /// <summary>The part moved to this rect: its anchor by thirds, its distance from it.</summary>
    public static void Move(ElementEnum peElement, Rect pORect, bool pbSave = true)
    {
        Placement lOPlacement = fGet(peElement);
        Vector2 lOAnchor = new Vector2(fThird(pORect.center.x, Screen.width), fThird(pORect.center.y, Screen.height));
        Vector2 lOOwn = new Vector2(pORect.x + (lOAnchor.x * pORect.width), pORect.y + (lOAnchor.y * pORect.height));
        Vector2 lOScreen = new Vector2(lOAnchor.x * Screen.width, lOAnchor.y * Screen.height);

        lOPlacement.Placed = true;
        lOPlacement.Anchor = lOAnchor;
        lOPlacement.Offset = (lOOwn - lOScreen) / Mathf.Max(0.01f, UWModernHud.PixelScale);

        if (pbSave)
            fSave();
    }

    /// <summary>A new size: the part stays at its anchor point (where it was drawn when it had
    /// none yet) and grows away from it.</summary>
    public static void SetPercent(ElementEnum peElement, int piPercent, Rect pOCurrent, bool pbSave = true)
    {
        Placement lOPlacement = fGet(peElement);

        if (!lOPlacement.Placed)
            Move(peElement, pOCurrent, false);

        lOPlacement.Percent = Mathf.Clamp(piPercent, MinPercent, MaxPercent);

        if (pbSave)
            fSave();
    }

    /// <summary>Writes the layout - after a drag, which moves without writing every frame.</summary>
    public static void Save()
    {
        fSave();
    }

    public static void Reset(ElementEnum peElement)
    {
        fGetAll()[(int)peElement] = new Placement();
        fSave();
    }

    public static void ResetAll()
    {
        for (int liAt = 0; liAt < ElementCount; liAt++)
            fGetAll()[liAt] = new Placement();

        fSave();
    }

    private static float fThird(float pfAt, float pfLength)
    {
        if (pfAt < pfLength / 3f)
            return 0f;

        return pfAt > pfLength * 2f / 3f ? 1f : 0.5f;
    }

    // ------------------------------------------------- Kept in the settings

    private static Placement fGet(ElementEnum peElement)
    {
        return fGetAll()[(int)peElement];
    }

    private static Placement[] fGetAll()
    {
        if (msPlacements != null)
            return msPlacements;

        msPlacements = new Placement[ElementCount];

        for (int liAt = 0; liAt < ElementCount; liAt++)
            msPlacements[liAt] = new Placement();

        // "element:anchorX:anchorY:offsetX:offsetY:percent;" per part that was changed.
        string lsStored = UWUserSettings.ModernLayout ?? string.Empty;

        foreach (string lsEntry in lsStored.Split(';'))
        {
            string[] lsParts = lsEntry.Split(':');

            if (lsParts.Length != 6 || !int.TryParse(lsParts[0], out int liElement) || liElement < 0 || liElement >= ElementCount)
                continue;

            float[] lfValues = new float[4];
            bool lbValid = true;

            for (int liAt = 0; liAt < 4; liAt++)
                lbValid &= float.TryParse(lsParts[liAt + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out lfValues[liAt]);

            if (!lbValid || !int.TryParse(lsParts[5], out int liPercent))
                continue;

            Placement lOPlacement = msPlacements[liElement];

            lOPlacement.Placed = lfValues[0] >= 0f;
            lOPlacement.Anchor = new Vector2(Mathf.Clamp01(lfValues[0]), Mathf.Clamp01(lfValues[1]));
            lOPlacement.Offset = new Vector2(lfValues[2], lfValues[3]);
            lOPlacement.Percent = Mathf.Clamp(liPercent, MinPercent, MaxPercent);
        }

        return msPlacements;
    }

    private static void fSave()
    {
        StringBuilder lOText = new StringBuilder();
        Placement[] lOAll = fGetAll();

        for (int liAt = 0; liAt < ElementCount; liAt++)
        {
            Placement lOPlacement = lOAll[liAt];

            if (!lOPlacement.Placed && lOPlacement.Percent == 100)
                continue;

            lOText.Append(liAt).Append(':')
                .Append((lOPlacement.Placed ? lOPlacement.Anchor.x : -1f).ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(lOPlacement.Anchor.y.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(lOPlacement.Offset.x.ToString("0.##", CultureInfo.InvariantCulture)).Append(':')
                .Append(lOPlacement.Offset.y.ToString("0.##", CultureInfo.InvariantCulture)).Append(':')
                .Append(lOPlacement.Percent).Append(';');
        }

        UWUserSettings.ModernLayout = lOText.ToString();
        UWUserSettings.Save();
    }
}
