/// <summary>
/// The 4:3 switch: the classic 320x200 screen shown at the proportion of the CRT the game was made
/// for, every original pixel 1.2 times taller than wide, as DOSBox shows it (see
/// UWGameUI.HorizontalPixelFactor).
///
/// ALWAYS ON since 2026-10-10 (per user: "without the 4:3 fix everything looks wrong"): the
/// checkbox in its place switches the classic frame's widescreen (UWClassicWide). Until then the
/// live value sat in UWSettings (the asset field DisplayAs4By3, removed 2026-10-11) with its
/// lasting copy in UWUserSettings (settings.json), where the field stays for the file's sake only
/// and is no longer read. Everything that asks for the switch still asks here, so the place where
/// a choice would be read is kept.
/// </summary>
public static class UWDisplayAspect
{
    public static bool Enabled
    {
        get { return true; }
    }
}
