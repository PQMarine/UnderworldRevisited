using UnityEngine;

/// <summary>
/// THE INTERFACE FONT of the port's own windows - the menu bar and its dialogs, the help window,
/// the effects screen, the message log - and the fallback where Lexend Exa is missing. (UWFonts is the original's pixel fonts.)
///
/// Until 2026-10-09 this was Unity's built-in LegacyRuntime.ttf, which is "Arial": on Windows
/// Arial itself, but Linux has no Arial and takes a substitute (DejaVu Sans in the WSL Ubuntu),
/// noticeably wider - on the user's Kubuntu laptop text ran over the buttons. Liberation Sans
/// (SIL OFL, ThirdParty/Liberation) has Arial's metrics by design and ships with the game, so
/// every layout made against Arial stays as it is, on every system.
///
/// A language that names installed fonts of its own (grammar.rule.font, translation wave 6) gets them
/// behind both fonts as Unity's fallback for the characters they lack (UWFallbackFont.Follow).
/// </summary>
public static class UWInterfaceFont
{
    private const string InterfaceResource = "Fonts/LiberationSans-Regular";

    /// <summary>The modern scheme's font (Lexend Exa, SIL OFL) under Resources.</summary>
    private const string ModernResource = "Fonts/LexendExa";

    private static Font msOInterface;

    private static Font msOModern;

    public static Font Font
    {
        get
        {
            if (msOInterface == null)
            {
                msOInterface = Resources.Load<Font>(InterfaceResource);

                if (msOInterface == null)
                    msOInterface = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                // The language's installed fonts behind it (translation wave 6).
                UWFallbackFont.Follow(msOInterface);
            }

            return msOInterface;
        }
    }

    /// <summary>
    /// THE MODERN FONT, loaded in one place (cleanup 5.1, 2026-10-19): until then eleven classes
    /// - the modern parts, the layout editor, the pad hint strip and UWTextLabel's font toggle -
    /// each called Resources.Load for the same file. Null when the file is missing, exactly as
    /// the load answered: every caller keeps its own fallback (UWTextLabel takes the interface
    /// font, the modern parts their own), so nothing changes where it is absent.
    /// </summary>
    public static Font Modern
    {
        get
        {
            if (msOModern == null)
            {
                msOModern = Resources.Load<Font>(ModernResource);

                // The language's installed fonts behind Lexend Exa (translation wave 6).
                UWFallbackFont.Follow(msOModern);
            }

            return msOModern;
        }
    }

    /// <summary>
    /// THE FONT A MODERN TEXT STARTS WITH: the modern font, the interface font where its file is
    /// missing (cleanup 5.2, 2026-10-20). Until then the modern parts, the pad hint strip, the game
    /// menu's styles and UWTextLabel each kept a copy of the modern font and wrote this fallback
    /// themselves; now they all ask here, and the texts of the parts get it through UWUiKit.
    /// </summary>
    public static Font ModernOrInterface => Modern != null ? Modern : Font;
}
