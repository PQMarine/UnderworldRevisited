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
/// </summary>
public static class UWInterfaceFont
{
    private const string InterfaceResource = "Fonts/LiberationSans-Regular";

    private static Font msOInterface;

    public static Font Font
    {
        get
        {
            if (msOInterface == null)
            {
                msOInterface = Resources.Load<Font>(InterfaceResource);

                if (msOInterface == null)
                    msOInterface = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return msOInterface;
        }
    }
}
