using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// The HUD section "the five command icons of the left bar": talk, get, look, fight and use,
/// LFTI.GR pictures 2 to 11 - the even one idle, the odd one lit. The options button above
/// them (pictures 0 and 1) stays with UWHudOptions, it opens a panel of its own.
///
/// Built 2026-09-19 on the action layer of Interaction: an icon only sets
/// Interaction.CommandMode, and the mode changes what the right-button gesture over its kind
/// of target calls - for talk the release over a creature is TalkTo, without the look on the
/// press (measured by the user on the original, 2026-09-19); the left button keeps walking and
/// the cursor stays. The rules of the mode itself (the lit icon is switched off by a second
/// click, fight is the weapon, the weapon goes away when another icon replaces it) are
/// documented on UWCommandMode and live in Interaction.SetCommandMode.
///
/// POSITIONS AND SIZES: from the reference scene (Underworld.tscn, stored at four times the
/// size), divided by four - talk 32 by 19 at 7/27, get 32 by 16 at 6/47, look 27 by 14 at
/// 6/65, fight 31 by 19 at 8/80, use 30 by 19 at 8/99; the options button sits at 8/10, the
/// same measure. NO ICON IS LIT when no mode is chosen: use is a mode of its own (per user on
/// the original, 2026-09-19: look, get and use turn the pointer into the red X), not the
/// default - the reference lights it by default, which is its guess, not the game's.
///
/// The icons are locked while a conversation is open, like the options button (per user,
/// 2026-09-18: the options on the left are locked too).
/// </summary>
public sealed class UWHudCommands
{
    private readonly UWGameUI mOUi;

    internal UWHudCommands(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private struct Entry
    {
        public UWCommandMode Mode;
        public int IdleArt;
        public float Left;
        public float Top;
    }

    private static readonly Entry[] msEntries =
    {
        new Entry { Mode = UWCommandMode.Talk, IdleArt = 2, Left = 7f, Top = 27f },
        new Entry { Mode = UWCommandMode.Get, IdleArt = 4, Left = 6f, Top = 47f },
        new Entry { Mode = UWCommandMode.Look, IdleArt = 6, Left = 6f, Top = 65f },
        new Entry { Mode = UWCommandMode.Fight, IdleArt = 8, Left = 8f, Top = 80f },
        new Entry { Mode = UWCommandMode.Use, IdleArt = 10, Left = 8f, Top = 99f },
    };

    private Image[] mOImages;

    internal void Build()
    {
        if (mOUi.mGameFrame == null)
            return;

        mOImages = new Image[msEntries.Length];

        for (int liAt = 0; liAt < msEntries.Length; liAt++)
        {
            mOImages[liAt] = UWGameUI.fCreateImage("Command " + msEntries[liAt].Mode, mOUi.mGameFrame,
                msEntries[liAt].Left, -msEntries[liAt].Top, 1f, 1f, Color.white);

            mOUi.Options.fSetUiSprite(mOImages[liAt], UWTexture.TextureTypes.LFTI, msEntries[liAt].IdleArt);
        }
    }

    /// <summary>The options panel covers the icon column while it is open (UWHudOptions).</summary>
    internal void SetVisible(bool pbVisible)
    {
        if (mOImages == null)
            return;

        foreach (Image lOImage in mOImages)
            lOImage.enabled = pbVisible;
    }

    /// <summary>Lights the icon of the current mode and takes a left click on an icon. True
    /// when the click was used up here.</summary>
    internal bool Update()
    {
        if (mOImages == null || mOUi.mOInteraction == null)
            return false;

        for (int liAt = 0; liAt < msEntries.Length; liAt++)
        {
            mOUi.Options.fSetUiSprite(mOImages[liAt], UWTexture.TextureTypes.LFTI,
                msEntries[liAt].IdleArt + (fIsLit(msEntries[liAt].Mode) ? 1 : 0));
        }

        // NOT WHILE THE VIEW ROAMS (per user, 2026-09-27: the modes are locked in the original
        // during Roaming Sight).
        if (UWConversationScreen.IsAnyOpen || !UWMouseButtons.LeftPressed || UWRoamingSight.IsAnyRunning)
            return false;

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null)
            return false;

        // THE ORIGINAL'S AREA, not the pictures (2026-09-26): the whole column is one area and
        // the icon follows from the height inside it (UWClickRules.CommandIconAt) - the pictures
        // are of different sizes and left gaps and overlaps of two or three rows. The options
        // button, the sixth step, keeps its own handling in UWHudOptions.
        Vector2 lOPosition = lOMouse.position.ReadValue();

        if (!mOUi.IsPointerInOriginalArea(lOPosition, UWClickRules.CommandIcons, out int _, out int liY))
            return false;

        UWCommandMode leMode = UWClickRules.CommandIconAt(liY);

        if (leMode == UWCommandMode.None || leMode == UWCommandMode.Options)
            return false;

        mOUi.mOInteraction.SetCommandMode(leMode);

        return true;
    }

    /// <summary>Fight follows the drawn weapon, however it was drawn; the others light while
    /// they are the mode.</summary>
    private bool fIsLit(UWCommandMode peMode)
    {
        Interaction lOInteraction = mOUi.mOInteraction;

        if (peMode == UWCommandMode.Fight)
            return lOInteraction.IsCombatModeActive;

        return lOInteraction.CommandMode == peMode;
    }
}
