using UWDataImport.UWData;

/// <summary>
/// THE BACKS OF THE MODERN INTERFACE'S PARTS (per user, 2026-10-09): which shape, colour, pattern
/// and border a part's generated back has (UWBackdropArt). Kept per part in the settings
/// (UWUserSettings.ModernBacks, "part:shape:colour:pattern:border" by semicolons), so every part can
/// have one; the layout editor's inspector offers it for every part. A part that was never given
/// one has none - the Modern default stays as it is.
///
/// FOR EVERY PART (per user, 2026-10-09, after the compass: "the compass works, go on"): the parts
/// with their own leather (IsLeatherPart) get the back IN THE LEATHER'S PLACE - a rectangle in the
/// colour and pattern, its border instead of the leather's; none keeps the leather. The others
/// (compass, heading, vitals, messages, spells) get it BEHIND them, their own art staying on top
/// (per user: "replace the leather" and "the back behind").
/// </summary>
public static class UWModernBacks
{
    public struct Back
    {
        public UWBackdropArt.ShapeEnum Shape;

        public UWBackdropArt.ColourEnum Colour;

        public UWBackdropArt.PatternEnum Pattern;

        public bool Border;

        /// <summary>A number per distinct back, for the parts' picture caches.</summary>
        public int Key => ((int)Shape * 1000) + ((int)Colour * 100) + ((int)Pattern * 10) + (Border ? 1 : 0);
    }

    /// <summary>Counts every change, for the parts' picture caches.</summary>
    public static int Version { get; private set; }

    /// <summary>The colour help and the backs together: changes when either does (both only grow).</summary>
    public static int ArtVersion => UWColourVision.Version + Version;

    /// <summary>The parts that have their own leather, which a back replaces.</summary>
    public static bool IsLeatherPart(UWModernLayout.ElementEnum peElement)
    {
        switch (peElement)
        {
            case UWModernLayout.ElementEnum.ActionBar:
            case UWModernLayout.ElementEnum.Minimap:
            case UWModernLayout.ElementEnum.Bags:
            case UWModernLayout.ElementEnum.CharacterPanel:
            case UWModernLayout.ElementEnum.RunePanel:
            case UWModernLayout.ElementEnum.Conversation:
                return true;

            default:
                return false;
        }
    }

    /// <summary>What a part shows: none, leather clouds with a border once a shape is chosen.</summary>
    public static Back Get(UWModernLayout.ElementEnum peElement)
    {
        Back lOBack = new Back { Shape = UWBackdropArt.ShapeEnum.None, Border = true };

        foreach (string lsEntry in (UWUserSettings.ModernBacks ?? string.Empty).Split(';'))
        {
            string[] lsParts = lsEntry.Split(':');

            if (lsParts.Length != 5 || !int.TryParse(lsParts[0], out int liElement) || liElement != (int)peElement)
                continue;

            int.TryParse(lsParts[1], out int liShape);
            int.TryParse(lsParts[2], out int liColour);
            int.TryParse(lsParts[3], out int liPattern);

            lOBack.Shape = (UWBackdropArt.ShapeEnum)UnityEngine.Mathf.Clamp(liShape, 0, 2);
            lOBack.Colour = (UWBackdropArt.ColourEnum)UnityEngine.Mathf.Clamp(liColour, 0, 3);
            lOBack.Pattern = (UWBackdropArt.PatternEnum)UnityEngine.Mathf.Clamp(liPattern, 0, 3);
            lOBack.Border = lsParts[4] == "1";
        }

        return lOBack;
    }

    public static void Set(UWModernLayout.ElementEnum peElement, Back pOBack)
    {
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();

        foreach (string lsEntry in (UWUserSettings.ModernBacks ?? string.Empty).Split(';'))
        {
            string[] lsParts = lsEntry.Split(':');

            if (lsParts.Length != 5 || !int.TryParse(lsParts[0], out int liElement) || liElement == (int)peElement)
                continue;

            lOText.Append(lsEntry).Append(';');
        }

        lOText.Append((int)peElement).Append(':').Append((int)pOBack.Shape).Append(':').Append((int)pOBack.Colour).Append(':')
            .Append((int)pOBack.Pattern).Append(':').Append(pOBack.Border ? "1" : "0");

        UWUserSettings.ModernBacks = lOText.ToString();
        UWUserSettings.Save();
        Version++;
    }
}
