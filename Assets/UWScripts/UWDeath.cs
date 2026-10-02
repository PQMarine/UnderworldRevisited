using System.Collections;
using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// What happens when the player's vitality reaches zero - after UW.EXE
/// DeathAndResurrection_ovr143_1610 and Resurrection_ovr143_1521 (built with the silver tree,
/// per user, 2026-09-14). Until then nothing happened at all at zero vitality.
///
///   Talisman counter (quest 36) is 0     vitality 4, no death (UW.EXE checks PLAYER.DAT 0x6D).
///   Always                               death music, experience minus 1/8, the object in
///                                        hand is dropped, bones (0xC2 + 0..4) where the player
///                                        died.
///   Silver tree planted, not on level 9  cutscene 0x102 (skulls and tree), then to the tree's
///                                        level and onto the tree; vitality = max - 2 - (0..2)
///                                        (max if max <= 8), mana = max - (2 + max/8) (max if
///                                        max <= 8), poison and running spells end.
///   Otherwise                            final death: cutscene 0x103, then the main menu
///                                        (DeathEndGame(1); reference: ReturnToMainMenu).
///
/// Not rebuilt: the palette effect before the cutscene (stub114_48). The death cutscenes are in
/// normal colours in the original, not red (per user, 2026-09-14), and play inside the viewport.
/// </summary>
public class UWDeath : MonoBehaviour
{
    private const int DeathCutscene = 0x102;

    private const int FinalDeathCutscene = 0x103;

    /// <summary>First bones object; UW.EXE adds RNG % 5.</summary>
    private const int FirstBonesObjectId = UWEndgameRules.FirstBonesObjectId;

    private const int TalismanQuestFlag = UWPlayerData.TalismanQuestFlag;

    /// <summary>Vitality set when the talisman counter is 0 instead of dying.</summary>
    private const int NoDeathVitality = 4;

    /// <summary>Music theme after the resurrection (UW.EXE ChangeThemeMusic(4)).</summary>
    private const int ResurrectionTheme = 4;

    /// <summary>Is a death being handled right now?</summary>
    public static bool IsDying { get; private set; }

    private UWLevelLoader mOLoader;

    private UWCharacter mOCharacter;

    private void OnDestroy()
    {
        IsDying = false;
    }

    private void Update()
    {
        if (IsDying)
            return;

        if (mOLoader == null)
            mOLoader = GetComponent<UWLevelLoader>();

        if (mOCharacter == null)
            mOCharacter = UWScene.Character;

        if (mOLoader == null || mOCharacter == null || !mOLoader.HasWorld || UWEndgame.IsVictoryRunning)
            return;

        if (mOCharacter.CurrentHP > 0f)
            return;

        if (UWQuestFlags.Get(TalismanQuestFlag) == 0)
        {
            mOCharacter.ApplyConversationValues(mOCharacter.Hunger, NoDeathVitality,
                Mathf.RoundToInt(mOCharacter.CurrentMana), mOCharacter.Poison);
            return;
        }

        StartCoroutine(fDie());
    }

    private IEnumerator fDie()
    {
        IsDying = true;

        Interaction lOInteraction = UWScene.Interaction;
        UWInventory lOInventory = UWScene.Inventory;

        UWMusic.ChangeTheme(UWMusic.DeathTheme);

        mOCharacter.ChangeExperience(-(mOCharacter.Experience >> 3), mOLoader.CurrentLevelIndex + 1);

        if (lOInteraction != null)
            lOInteraction.IsCombatModeActive = false;

        Vector3 lOPlace = mOCharacter.transform.position;
        UWTilePos lOTile = mOLoader.WorldPositionToTile(lOPlace);
        Vector3 lOFloor = new Vector3(lOPlace.x, mOLoader.GetFloorHeightAt(lOPlace), lOPlace.z);

        if (lOInventory != null && lOInventory.CursorItem != null)
            lOInventory.DropCursorItemInWorld(lOFloor, false);

        mOLoader.SpawnObjectById(FirstBonesObjectId + Random.Range(0, 5), lOTile.X, lOTile.Y, 0, -1, lOFloor);

        int liTreeLevel = UWSilverTree.TreeLevel;
        bool lbResurrect = liTreeLevel > 0 && liTreeLevel <= mOLoader.LevelCount
            && mOLoader.CurrentLevelIndex != UWEndgame.VoidLevelIndex;

        // The world stands still while the cutscene runs.
        UWGameClock.Hold(UWGameClock.DeathHold);

        UWIntroPlayer lOPlayer = UWScene.IntroPlayer;
        bool lbDone = false;

        if (lOPlayer != null)
        {
            lOPlayer.PlayCutscene(lbResurrect ? DeathCutscene : FinalDeathCutscene, () => lbDone = true);

            while (!lbDone)
                yield return null;
        }

        if (!lbResurrect || !fResurrectAtTree(liTreeLevel - 1))
        {
            fFinalDeath();
            yield break;
        }

        UWGameClock.Release(UWGameClock.DeathHold);
        IsDying = false;
    }

    /// <summary>To the tree and back on the feet. False if no tree is found on its level.</summary>
    private bool fResurrectAtTree(int piLevelIndex)
    {
        UWLevel lOLevel = mOLoader.UWDataImporter != null && piLevelIndex >= 0 && piLevelIndex < mOLoader.LevelCount
            ? mOLoader.UWDataImporter.Levels[piLevelIndex] : null;

        if (lOLevel == null || lOLevel.TileData == null)
            return false;

        for (int liAt = 0; liAt < lOLevel.TileData.Length; liAt++)
        {
            UWTile lOTile = lOLevel.TileData[liAt];

            if (lOTile == null || lOTile.ObjectsInTile == null)
                continue;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                if (lOObject == null || lOObject.ID != UWSilverTree.TreeObjectId)
                    continue;

                int liTileX = liAt % UWLevelMeshBuilder.TilesPerAxis;
                int liTileY = liAt / UWLevelMeshBuilder.TilesPerAxis;

                UWGameClock.Release(UWGameClock.DeathHold);
                mOLoader.TravelTo(piLevelIndex, liTileX, liTileY);

                fRestore();

                return true;
            }
        }

        return false;
    }

    /// <summary>Vitality, mana, poison and spells after the resurrection (Resurrection_ovr143_1521).
    /// </summary>
    private void fRestore()
    {
        int liMaxHp = Mathf.RoundToInt(mOCharacter.MaxHP);
        int liMaxMana = Mathf.RoundToInt(mOCharacter.MaxMana);

        // RNG * 3 / 0x8000: 0 to 2.
        int liHp = liMaxHp > 8 ? liMaxHp - 2 - Random.Range(0, 3) : liMaxHp;
        int liMana = liMaxMana > 8 ? liMaxMana - (2 + (liMaxMana / 8)) : liMaxMana;

        mOCharacter.ApplyConversationValues(mOCharacter.Hunger, liHp, liMana, 0);
        mOCharacter.EndAllSpells();
        mOCharacter.StartFlaskFill();

        UWMusic.ChangeTheme(ResurrectionTheme);
    }

    /// <summary>The game is over: main menu, the world stays frozen behind it.</summary>
    private void fFinalDeath()
    {
        UWMainMenu lOMenu = UWScene.MainMenu;

        if (lOMenu != null)
        {
            lOMenu.ShowAfterVictory();
            return;
        }

        UWGameClock.Release(UWGameClock.DeathHold);
        IsDying = false;
    }
}
