using UnityEngine;
using UWDataImport.UWData;

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
///
/// The rules themselves - the anchors, Place, Move, the text in the settings - are engine-free in
/// UWHudLayout (moved there 2026-10-05, per user: reusable for UW2 or another engine); here are
/// the screen, the UI size, the settings and where the parts were drawn.
/// </summary>
public static class UWModernLayout
{
    /// <summary>
    /// THE INTERFACE PRESETS (per user, 2026-10-09; under "Edit layout" in the game menu): Modern -
    /// the modern interface at its default places; Custom - the own layout of the layout editor,
    /// kept while another preset is chosen; Classic+ - the original's pieces freed as elements;
    /// Classic - the classic frame brought to the whole screen. The last two are planned (Todo).
    /// </summary>
    public enum PresetEnum
    {
        Modern = 0,
        Custom = 1,
        ClassicPlus = 2,
        Classic = 3
    }

    public static PresetEnum Preset
    {
        get
        {
            int liPreset = UWUserSettings.InterfacePreset;

            return liPreset >= 0 && liPreset <= (int)PresetEnum.Classic ? (PresetEnum)liPreset : PresetEnum.Modern;
        }
    }

    /// <summary>Chooses a preset; the parts take their places from it at once.</summary>
    public static void ChoosePreset(PresetEnum pePreset)
    {
        UWUserSettings.InterfacePreset = (int)pePreset;
        UWUserSettings.Save();
        msLayout = null;
    }

    /// <summary>The own layout is in force: only then are places read from and written to it.</summary>
    public static bool IsCustom => Preset == PresetEnum.Custom;

    /// <summary>The minimap hidden - a choice of the own layout only.</summary>
    public static bool IsMinimapHidden => IsCustom && UWUserSettings.MinimapHidden;

    /// <summary>The original's compass shown (UWModernCompass) - switched on in the layout editor
    /// for the own layout; Classic+ will bring it by itself.</summary>
    public static bool IsCompassShown => (IsCustom && UWUserSettings.ModernCompass) || Preset == PresetEnum.ClassicPlus;

    /// <summary>The original's message scroll in the messages' place (UWModernScroll) - as the
    /// compass: the own layout's choice, Classic+ will bring it.</summary>
    public static bool IsScrollShown => (IsCustom && UWUserSettings.ModernScroll) || Preset == PresetEnum.ClassicPlus;

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
        Conversation,
        Compass
    }

    public const int ElementCount = 11;

    public static readonly string[] Names =
    {
        "Action bar", "Minimap", "Heading", "Active spells", "Messages", "Vitality and mana", "Bags", "Character panel", "Rune panel",
        "Conversation", "Compass"
    };

    /// <summary>A panel left at its edge: it slides out and in on its handle.</summary>
    public static bool IsDocked(ElementEnum peElement)
    {
        return (peElement == ElementEnum.CharacterPanel || peElement == ElementEnum.RunePanel) && !IsPlaced(peElement);
    }

    public const int MinPercent = UWHudLayout.MinPercent;

    public const int MaxPercent = UWHudLayout.MaxPercent;

    public const int PercentStep = 10;

    private static UWHudLayout msLayout;

    private static readonly Rect[] msRects = new Rect[ElementCount];

    // As long as the elements, each 'long ago' (until 2026-10-09 ten written out - the eleventh
    // element, the compass, ran past them and took the layout editor down).
    private static readonly int[] miRectFrames = fLongAgo();

    private static int[] fLongAgo()
    {
        int[] liFrames = new int[ElementCount];

        for (int liAt = 0; liAt < liFrames.Length; liAt++)
            liFrames[liAt] = -10;

        return liFrames;
    }

    /// <summary>The layout editor is open (the game menu's Layout page).</summary>
    public static bool IsEditing => UWModernHud.Instance != null && UWModernHud.Instance.Page == UWModernHud.PageEnum.Layout;

    public static int Percent(ElementEnum peElement)
    {
        return fLayout().Get((int)peElement).Percent;
    }

    /// <summary>The part's own size factor, on top of the UI size.</summary>
    public static float Scale(ElementEnum peElement)
    {
        return Percent(peElement) / 100f;
    }

    public static bool IsPlaced(ElementEnum peElement)
    {
        return fLayout().Get((int)peElement).Placed;
    }

    /// <summary>Where the part goes: its default rect (screen pixels from the bottom left, already
    /// at its own size) unless it was moved; kept on the screen.</summary>
    public static Rect Place(ElementEnum peElement, Rect pODefault)
    {
        if (!IsPlaced(peElement))
            return pODefault;

        return fLayout().Place((int)peElement, pODefault.ToUW(), Screen.width, Screen.height, UWModernHud.PixelScale).ToUnity();
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
        fLayout().Move((int)peElement, pORect.ToUW(), Screen.width, Screen.height, UWModernHud.PixelScale);

        if (pbSave)
            fSave();
    }

    /// <summary>A new size: the part stays at its anchor point (where it was drawn when it had
    /// none yet) and grows away from it.</summary>
    public static void SetPercent(ElementEnum peElement, int piPercent, Rect pOCurrent, bool pbSave = true)
    {
        fLayout().SetPercent((int)peElement, piPercent, pOCurrent.ToUW(), Screen.width, Screen.height, UWModernHud.PixelScale);

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
        fLayout().Reset((int)peElement);
        fSave();
    }

    public static void ResetAll()
    {
        fLayout().ResetAll();
        fSave();
    }

    // ------------------------------------------------- Kept in the settings

    private static UWHudLayout fLayout()
    {
        // Every preset but Custom keeps the default places: an empty layout.
        return msLayout ??= UWHudLayout.Parse(IsCustom ? UWUserSettings.ModernLayout : null, ElementCount);
    }

    private static void fSave()
    {
        if (!IsCustom)
            return;

        UWUserSettings.ModernLayout = fLayout().ToString();
        UWUserSettings.Save();
    }
}

/// <summary>Unity's Rect and the data layer's UWRect (both from the bottom left on the screen).</summary>
public static class UWRectConversion
{
    public static UWRect ToUW(this Rect pORect)
    {
        return new UWRect(pORect.x, pORect.y, pORect.width, pORect.height);
    }

    public static Rect ToUnity(this UWRect pORect)
    {
        return new Rect(pORect.X, pORect.Y, pORect.Width, pORect.Height);
    }
}
