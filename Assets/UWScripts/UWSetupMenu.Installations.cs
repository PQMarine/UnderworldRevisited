using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UWDataImport.Install;
using UWDataImport.UWData;
using Kind = UWSetupRow.KindEnum;

/// <summary>
/// THE INSTALLATIONS IN THE GAME WINDOW (2026-11-12, per user: "GOG Galaxy, Steam, EA App, Manual" and the save path the
/// port takes): for the running game every installation found (UWInstallations over the search's folders), one row each
/// with its store and folder, the one in use lit; a press takes it, as the folder dialog did, and the scene starts again.
/// "Other folder..." opens that dialog; a folder of one's own in use stands in the list as Other. Under it the save
/// folder in use, with the installation's own folder beneath where cloud_saves is read as DOSBox's overlay.
///
/// ON A FIRST START (no settings, no folder chosen) the list is the search's, and the one in use is the search's first
/// find, as the game loads it (UWSettings.FindGogImage). Nothing found means the first-run screen, which has only the
/// folder dialog. The list is made when the page is built; what is in use and the save line are asked while drawn,
/// at most once a second, as the save folder follows the saves.
/// </summary>
public partial class UWSetupMenu
{
    /// <summary>How often the save folder in use is worked out again while the window shows (it reads a few files).</summary>
    private const float InstallationRefreshSeconds = 1f;

    private List<UWInstallations.Installation> mOInstallations = new List<UWInstallations.Installation>();

    private float mfInstallationAskedAt = float.NegativeInfinity;

    private string msSavesInUse;

    private string msInstallationInUse;

    /// <summary>The rows: a heading, an item per installation, the folder in use when it is none of them, the button
    /// for another folder, the save line.</summary>
    private void fAddInstallationRows(UWSetupPage pOPage)
    {
        IUWGameProfile lOGame = UnderworldRevisited.UWSettings.Game;
        bool lbSecondGame = lOGame.Game == UWGame.Uw2;

        mOInstallations = UWInstallations.Find(lOGame, fInstallationFolders(lbSecondGame));
        mfInstallationAskedAt = float.NegativeInfinity;

        List<string> lsFound = mOInstallations.ConvertAll(pOAt => pOAt.Store + " " + pOAt.Folder);

        Debug.Log("[Setup] Installations of " + lOGame.Game + ": " + (lsFound.Count == 0 ? "none" : string.Join(" | ", lsFound)));

        pOPage.Add(new UWSetupRow
        {
            Kind = Kind.Info, Id = "game.installation", Text = UWText.T("setup.game.installation"), Look = UWSetupRow.LookEnum.Heading, Advance = 26f
        });

        foreach (UWInstallations.Installation lOInstallation in mOInstallations)
        {
            string lsFolder = lOInstallation.Folder;

            pOPage.Add(new UWSetupRow
            {
                Kind = Kind.Item, Id = "game.installation.item", Text = fStoreName(lOInstallation.Store) + " - " + lsFolder, Height = 26f, Advance = 28f,
                EnabledWhen = fFolderReachable, IsCurrent = () => UWInstallations.SameFolder(fInstallationInUse(), lsFolder),
                Press = () => fChooseInstallation(lsFolder, lbSecondGame)
            });
        }

        // A folder of one's own in use (the folder dialog's, an extracted DATA folder's): lit, not pressed.
        pOPage.Add(new UWSetupRow
        {
            Kind = Kind.Item, Id = "game.installation.own", Height = 26f, Advance = 28f,
            TextOf = () => fStoreName(UWInstallations.StoreEnum.Other) + " - " + fInstallationInUse(),
            VisibleWhen = () => fInstallationInUse() != null && UWInstallations.IndexOf(mOInstallations, fInstallationInUse()) < 0,
            IsCurrent = () => true, Press = () => { }
        });

        pOPage.Add(new UWSetupRow
        {
            Kind = Kind.Action, Id = "game.other_folder", Text = UWText.T("setup.game.other_folder"), Width = 200f, Height = 30f, Advance = 38f,
            EnabledWhen = fFolderReachable, Press = () => fOpenFolderDialog(lbSecondGame)
        });

        pOPage.Add(new UWSetupRow { Kind = Kind.Hint, Id = "game.saves", TextOf = fSavesLine, Advance = 10f });
    }

    /// <summary>Where to look: the search's folders, and the folders the player chose, the running game's first.</summary>
    private static List<string> fInstallationFolders(bool pbSecondGame)
    {
        List<string> lOFolders = new List<string>();

        if (pbSecondGame && !string.IsNullOrEmpty(UWUserSettings.GogInstallPathUw2))
            lOFolders.Add(UWUserSettings.GogInstallPathUw2);

        if (!string.IsNullOrEmpty(UWUserSettings.GogInstallPath))
            lOFolders.Add(UWUserSettings.GogInstallPath);

        lOFolders.AddRange(UnderworldRevisited.UWSettings.GetCandidateGogPaths());

        return lOFolders;
    }

    private static string fStoreName(UWInstallations.StoreEnum peStore)
    {
        switch (peStore)
        {
            case UWInstallations.StoreEnum.Gog:
                return UWText.T("setup.game.store.gog");
            case UWInstallations.StoreEnum.Steam:
                return UWText.T("setup.game.store.steam");
            case UWInstallations.StoreEnum.EaApp:
                return UWText.T("setup.game.store.ea");
            default:
                return UWText.T("setup.game.store.other");
        }
    }

    /// <summary>The installation the running game's saves belong to, or null.</summary>
    private string fInstallationInUse()
    {
        fAskInstallationInUse();

        return msInstallationInUse;
    }

    /// <summary>"Saves: ..." for the save folder in use, with the installation's own beneath under cloud_saves.</summary>
    private string fSavesLine()
    {
        fAskInstallationInUse();

        if (string.IsNullOrEmpty(msSavesInUse))
            return UWText.T("setup.game.saves_none");

        string lsBeneath = UWGogInstall.OverlayBaseOf(msSavesInUse);

        return lsBeneath == null ? UWText.F("setup.game.saves", msSavesInUse) : UWText.F("setup.game.saves_overlay", msSavesInUse, lsBeneath);
    }

    private void fAskInstallationInUse()
    {
        if (Time.unscaledTime - mfInstallationAskedAt < InstallationRefreshSeconds)
            return;

        mfInstallationAskedAt = Time.unscaledTime;

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        // On the first-run screen nothing was loaded; asking would start the search and the extraction here.
        string lsSaves = lOSettings != null && !mbFirstRun ? lOSettings.SavegameRoot : null;

        if (lsSaves != msSavesInUse)
            Debug.Log("[Setup] Save folder in use: " + (lsSaves ?? "none"));

        msSavesInUse = lsSaves;
        msInstallationInUse = UWInstallations.InstallationOfSaves(msSavesInUse);
    }

    /// <summary>Takes an installation for the running game, as the folder dialog does: the first game's is the game
    /// folder, the second's its own folder; the scene starts again with it.</summary>
    private void fChooseInstallation(string psFolder, bool pbSecondGame)
    {
        if (UWInstallations.SameFolder(fInstallationInUse(), psFolder))
            return;

        if (pbSecondGame)
            UWUserSettings.GogInstallPathUw2 = psFolder;
        else
            UWUserSettings.GogInstallPath = psFolder;

        Debug.Log("[Setup] Installation chosen: " + psFolder);
        fRestartWithNewFolder();
    }

    /// <summary>
    /// A NEW FOLDER MEANS A NEW START (2026-11-12, found in the user's log: after choosing the EA installation the
    /// restart loaded the save the run had come from, D:\Ultima Underworld\UNDEROM1\SAVE3): what this run was started
    /// from stays behind, as at a switch of game (UWGameSwitch.Switch), then the scene starts again.
    /// </summary>
    private static void fRestartWithNewFolder()
    {
        UWSavegameSlots.ForgetPending();
        UWCharacterCreationScreen.ForgetStart();
        UWGameClock.ReleaseAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
