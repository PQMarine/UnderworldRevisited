using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// Writes a savegame into one of the four slots - from the options panel (see
/// UWGameUI, section "Options panel").
///
/// Until 2026-09-10 this ran via F9 in UWDebugTools, as a tool for relocating the character.
/// The core moved here from there and was extended by what a real save
/// needs: attributes, skills, experience, rune bag and the INVENTORY.
///
/// WHAT GOES IN
///
///   DESC           the typed-in name, raw ASCII text
///   PLAYER.DAT     position, heading, level, hunger, fatigue, clock, vitality, mana,
///                  poison, intoxication, active spells, quest flags, game variables, rune shelf,
///                  attributes, skills, experience, rune bag, inventory including contents
///   LEV.ARK        the levels (tiles and objects), map notes and automap
///
/// THE STATE OF THE WORLD goes in as well: DataImport.SaveMapData writes the tiles and
/// objects of all nine levels back via UWLevelWriter (since 2026-09-10). Creatures, door
/// states, locks and switches are carried from their components into the data first
/// (see UWWorldSync.Capture).
///
/// INTO AN EMPTY SLOT the loaded savegame is copied first and then
/// overwritten - the same base as when saving into its own slot. Without a
/// loaded savegame there is no base - EXCEPT for a newly created character
/// (since 2026-09-11, see UWSavegameStore): then LEV.ARK from the data folder and the
/// PLAYER.DAT from character creation are the base, and the slot becomes the loaded state.
///
/// BACKUPS: the first write into a slot creates PLAYER.DAT.bak and
/// LEV.ARK.bak. The slots are the original game's savegames.
///
/// SINCE 2026-09-17 (P2 of the engine separation) this class only reads the camera and the
/// components into a snapshot; the files are written by UWSavegameStore.
/// </summary>
public static class UWSavegameWriter
{
    /// <summary>Returns null on success, otherwise an error message.</summary>
    public static string Save(int piSlot, string psDescription)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null || lOLoader.UWDataImporter == null)
            return "No level loaded.";

        DataImport lOData = lOLoader.UWDataImporter;

        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;
        string lsTarget = UWSavegameSlots.GetSlotPath(lsRoot, piSlot);

        Camera lOCamera = Camera.main;

        if (lOCamera == null)
            return "No camera found.";

        Vector3 lOAt = lOCamera.transform.position;
        UWTilePos lOTile = lOLoader.WorldPositionToTile(lOAt);

        int liFineX = Mathf.Clamp(Mathf.RoundToInt(
            ((lOAt.x - (lOTile.X * UWLevelMeshBuilder.TileSpacing)) + UWLevelMeshBuilder.TileHalfSize)
                * 256f / UWLevelMeshBuilder.TileSpacing), 0, 255);

        int liFineY = Mathf.Clamp(Mathf.RoundToInt(
            ((lOAt.z - (lOTile.Y * UWLevelMeshBuilder.TileSpacing)) + UWLevelMeshBuilder.TileHalfSize)
                * 256f / UWLevelMeshBuilder.TileSpacing), 0, 255);

        // Eighths of a zpos step - and from the FLOOR below the character, not from the camera.
        int liZ = Mathf.Clamp(Mathf.RoundToInt(
            lOLoader.GetFloorHeightAt(lOAt) * 8f / UWObjectSpawner.HeightScale), 0, 1023);

        int liHeading = Mathf.RoundToInt(
            Mathf.Repeat(lOCamera.transform.eulerAngles.y, 360f) * 65536f / 360f) & 0xFFFF;

        // Creatures, doors, locks and switches live in their components at runtime -
        // put them into the data first, then write (see UWWorldSync).
        UWWorldSync.Capture(lOLoader);

        // The files themselves are written engine-free (UWSavegameStore, since 2026-09-17).
        string lsError = UWSavegameStore.Write(lOData, lsTarget, new UWSavegameStore.Snapshot
        {
            LevelIndex = lOLoader.CurrentLevelIndex,
            PositionX = (lOTile.X << 8) | liFineX,
            PositionY = (lOTile.Y << 8) | liFineY,
            PositionZ = liZ,
            Heading = liHeading,
            State = fGetSavedState(lOData)
        }, psDescription);

        if (lsError != null)
            return lsError;

        // Our own file beside the original's (the help's notes), see UWHelpNotes.
        UWHelpNotes.SaveTo(lsTarget);

        Debug.Log(string.Format("[Savegame] SAVE{0} \"{1}\" written: level {2}, tile {3}/{4}.",
            piSlot, psDescription, lOLoader.CurrentLevelIndex + 1, lOTile.X, lOTile.Y));

        return null;
    }

    /// <summary>Everything besides the position that belongs in PLAYER.DAT.</summary>
    /// <summary>The swim counter of the character, see UWPlayerTerrain.SwimCounter.</summary>
    private static int fGetSwimCounter()
    {
        UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

        return lOTerrain != null ? lOTerrain.SwimCounter : -1;
    }

    private static UWPlayerData.SavedState fGetSavedState(DataImport pOData)
    {
        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter == null)
            return null;

        UWGameUI lOUi = UWScene.GameUi;
        UWInventory lOInventory = UWScene.Inventory;

        List<UWPlayerData.ActiveSpell> lOSpells = new List<UWPlayerData.ActiveSpell>();

        foreach (UWActiveSpellEffect lOSpell in lOCharacter.ActiveSpells)
        {
            lOSpells.Add(new UWPlayerData.ActiveSpell
            {
                MajorClass = lOSpell.MajorClass,
                MinorClass = lOSpell.MinorClass,
                Stability = lOSpell.Stability
            });
        }

        // The whole range, not only what is set - a cleared flag is zero and
        // belongs in the file as zero.
        int[] liFlags = new int[UWPlayerData.QuestFlagCount];

        for (int liAt = 0; liAt < liFlags.Length; liAt++)
            liFlags[liAt] = UWQuestFlags.Get(liAt);

        int[] liVariables = new int[UWPlayerData.GameVariableCount];

        for (int liAt = 0; liAt < liVariables.Length; liAt++)
            liVariables[liAt] = UWGameVariables.Get(liAt);

        int[] liSkills = new int[UWPlayerData.SkillCount];

        for (int liAt = 0; liAt < liSkills.Length; liAt++)
            liSkills[liAt] = lOCharacter.GetSkillByNumber(UWCharacter.FirstNamedSkillNumber + liAt);

        bool[] lbRunes = new bool[UWPlayerData.RuneCount];

        for (int liAt = 0; liAt < lbRunes.Length && pOData.InitialPlayer != null; liAt++)
            lbRunes[liAt] = pOData.InitialPlayer.HasRune((UWPlayerData.Rune)liAt);

        return new UWPlayerData.SavedState
        {
            MoonstoneLevel = UWGameFlags.MoonstoneLevel,
            SilverTreeLevel = UWGameFlags.SilverTreeLevel,
            IncenseCounter = pOData.InitialPlayer != null ? pOData.InitialPlayer.IncenseCounter : (int?)null,
            TalismansDestroyable = UWGameFlags.TalismansDestroyable,
            GaramonBuried = UWGameFlags.GaramonBuried,
            CupOfWonderFound = UWGameFlags.CupOfWonderFound,
            KeyOfTruthGiven = UWGameFlags.KeyOfTruthGiven,
            OrbDestroyed = UWGameFlags.OrbDestroyed,
            OrbManaBackup = UWGameFlags.OrbManaBackup,
            Hunger = lOCharacter.Hunger,
            Fatigue = lOCharacter.Fatigue,
            MealHealCounter = lOCharacter.MealHealCounter,
            Counter3C = lOCharacter.Counter3C,
            // The original's running total: our sum plus what the load captured beside it
            // (UWInventoryModel.WeightOffsetTenthStones). Until 2026-09-25 the field kept the
            // value of loading, so the next load hid every change made in between.
            CarriedWeight = lOInventory != null && pOData.CommonObjectProperties != null
                ? Mathf.Max(0, lOInventory.Model.GetCarriedTenthStones(pOData.CommonObjectProperties)) : -1,
            ClockValue = lOCharacter.ClockValue,
            CurrentVitality = Mathf.RoundToInt(lOCharacter.CurrentHP),
            MaxVitality = Mathf.RoundToInt(lOCharacter.MaxHP),
            CurrentMana = Mathf.RoundToInt(lOCharacter.CurrentMana),
            MaxMana = Mathf.RoundToInt(lOCharacter.MaxMana),
            Poison = lOCharacter.Poison,
            Intoxication = lOCharacter.Intoxication,
            Hallucination = lOCharacter.Hallucination,
            SwimCounter = fGetSwimCounter(),
            // Music and sound belong to the SAVE in the original, not to the program - see
            // UWPlayerData.SettingsOffset and UWSoundOptions.
            SoundEnabled = UWSoundOptions.SoundEnabled ? 1 : 0,
            MusicEnabled = UWSoundOptions.MusicEnabled ? 1 : 0,
            ActiveSpells = lOSpells,
            QuestFlags = liFlags,
            GameVariables = liVariables,
            SelectedRunes = lOUi != null ? lOUi.SelectedRunes : null,

            HasCharacter = true,
            Strength = lOCharacter.Strength,
            Dexterity = lOCharacter.Dexterity,
            Intelligence = lOCharacter.Intelligence,
            Attack = lOCharacter.Attack,
            Defense = lOCharacter.Defence,
            Skills = liSkills,
            Level = lOCharacter.Level,
            Experience = lOCharacter.Experience,
            AvailableSkillPoints = lOCharacter.SkillPoints,
            TotalSkillPoints = lOCharacter.SkillPointsTotal,
            Runes = lbRunes,

            Equipment = lOInventory != null ? lOInventory.GetSavegameEquipment() : null,
            Backpack = lOInventory != null ? (UWObject[])lOInventory.Backpack.Clone() : null
        };
    }
}
