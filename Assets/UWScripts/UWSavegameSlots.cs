using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The four savegame slots and loading from within the game (the reference:
/// uimanager_options, branch RestoreMenu, and savegame/SaveDescription).
///
/// HOW A SAVEGAME IS NAMED: its folder holds, next to LEV.ARK and PLAYER.DAT, a
/// file DESC. It is plain ASCII text without a terminator and exactly as long as what
/// was typed in - at most thirty characters. If the folder or the file is missing, the
/// slot is unused; the original shows "&lt;not used yet&gt;" for it.
///
/// LOADING HAPPENS VIA A SCENE RESTART, not by adjusting the running game.
/// This is deliberate: practically every piece of state in the project hangs on a
/// savegame - level, character, inventory, quest flags, game variables, clock, active
/// spells, the tile lists of all nine levels. Each of these places reads its data on
/// start; resetting them all individually once more would mean a dozen new paths, each
/// of which can be forgotten on its own. The restart takes exactly the path that is
/// used every day anyway.
///
/// What survives the restart is this class's static state (PendingPath, IsRestoring) - that is why the chosen folder
/// lives here and not in the settings: those are an asset, and a write to them
/// would persist permanently in the editor.
/// </summary>
public static class UWSavegameSlots
{
    /// <summary>This many slots the original has.</summary>
    public const int SlotCount = 4;

    /// <summary>The file holding the savegame's name.</summary>
    private const string DescriptionFile = "DESC";

    /// <summary>How a used slot is recognised.</summary>
    private const string LevelFile = "LEV.ARK";

    /// <summary>What the original shows for an empty slot.</summary>
    public const string UnusedSlotName = "<not used yet>";

    /// <summary>The original cuts off anything longer.</summary>
    private const int MaxNameLength = 30;

    /// <summary>
    /// The folder to load from on the next start - survives the scene restart
    /// because it is static. Null means: whatever is in the settings applies.
    /// </summary>
    public static string PendingPath { get; private set; }

    /// <summary>We are just coming out of a load. This decides that logos and
    /// intro are skipped.</summary>
    public static bool IsRestoring { get; private set; }

    /// <summary>The folder of a slot, whether it is used or not.</summary>
    public static string GetSlotPath(string psRoot, int piSlot)
    {
        if (string.IsNullOrEmpty(psRoot) || piSlot < 1 || piSlot > SlotCount)
            return null;

        return Path.Combine(psRoot, "SAVE" + piSlot);
    }

    /// <summary>Is there anything in this slot at all?</summary>
    public static bool Exists(string psRoot, int piSlot)
    {
        string lsPath = GetSlotPath(psRoot, piSlot);

        return !string.IsNullOrEmpty(lsPath) && File.Exists(Path.Combine(lsPath, LevelFile));
    }

    /// <summary>
    /// The name of a slot, or the placeholder for an empty one.
    ///
    /// The DESC file has no terminator and no header - it IS the text. An
    /// empty slot has none at all, which is not the same as an empty file: that
    /// happens when someone just presses Enter while saving.
    /// </summary>
    public static string GetName(string psRoot, int piSlot)
    {
        if (!Exists(psRoot, piSlot))
            return UnusedSlotName;

        string lsFile = Path.Combine(GetSlotPath(psRoot, piSlot), DescriptionFile);

        if (!File.Exists(lsFile))
            return UnusedSlotName;

        try
        {
            byte[] lyRaw = File.ReadAllBytes(lsFile);

            System.Text.StringBuilder lOText = new System.Text.StringBuilder();

            for (int liAt = 0; liAt < lyRaw.Length && liAt < MaxNameLength; liAt++)
            {
                // Printable ASCII only - that is how the original writes it, and anything
                // else could not be displayed in our font anyway.
                if (lyRaw[liAt] >= 0x20 && lyRaw[liAt] <= 0x7E)
                    lOText.Append((char)lyRaw[liAt]);
            }

            return lOText.Length == 0 ? UnusedSlotName : lOText.ToString();
        }
        catch
        {
            return UnusedSlotName;
        }
    }

    /// <summary>
    /// Loads the savegame: remembers the folder and restarts the scene.
    ///
    /// Returns false if nothing is there - the caller then reports "There is no saved
    /// game there.", just like the original.
    /// </summary>
    public static bool Restore(string psRoot, int piSlot)
    {
        if (!Exists(psRoot, piSlot))
            return false;

        PendingPath = GetSlotPath(psRoot, piSlot);
        IsRestoring = true;

        Debug.Log("[Savegame] Loading " + PendingPath + " - rebuilding scene.");

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        return true;
    }
}
