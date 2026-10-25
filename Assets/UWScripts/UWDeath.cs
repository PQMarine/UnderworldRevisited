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
///
/// AN EVENT, NOT A POLL (cleanup step 4.3, 2026-10-17): the vitals raise Died when the hit points
/// cross to zero; this marks the death pending and handles it on its next frame, once the world
/// is there and no victory runs - as the poll waited for them. The decision and the numbers are
/// UWDeathRules' (engine-free).
/// </summary>
// After the frame's damage and the victory check: a death is handled in its frame (UWUpdateOrder,
// cleanup 4.7).
[DefaultExecutionOrder(UWUpdateOrder.Death)]
public class UWDeath : MonoBehaviour
{
    private const int DeathCutscene = 0x102;

    private const int FinalDeathCutscene = 0x103;

    private const int TalismanQuestFlag = UWPlayerData.TalismanQuestFlag;

    /// <summary>Music theme after the resurrection (UW.EXE ChangeThemeMusic(4)).</summary>
    private const int ResurrectionTheme = 4;

    /// <summary>Is a death being handled right now?</summary>
    public static bool IsDying { get; private set; }

    private UWLevelLoader mOLoader;

    private UWCharacter mOCharacter;

    /// <summary>The vitals listened to (the character's), so OnDestroy can let go of them.</summary>
    private UWPlayerVitals mOVitals;

    /// <summary>The vitals said Died and it is not handled yet.</summary>
    private bool mbDeathPending;

    private void OnDestroy()
    {
        IsDying = false;

        if (mOVitals != null)
            mOVitals.Died -= fOnDied;
    }

    /// <summary>The vitals' Died: the hit points crossed to zero.</summary>
    private void fOnDied()
    {
        mbDeathPending = true;
    }

    private void Update()
    {
        if (mOLoader == null)
            mOLoader = GetComponent<UWLevelLoader>();

        if (mOCharacter == null)
            mOCharacter = UWScene.Character;

        fListen();

        if (IsDying || !mbDeathPending)
            return;

        if (mOLoader == null || mOCharacter == null || !mOLoader.HasWorld || UWEndgame.IsVictoryRunning)
            return;

        mbDeathPending = false;

        // Healed in the meantime (the world or a victory held it): no death, as the poll had none.
        if (mOCharacter.Vitals.CurrentHP > 0f)
            return;

        UWDeathRules.Outcome leOutcome = UWDeathRules.Decide(UWQuestFlags.Get(TalismanQuestFlag), UWSilverTree.TreeLevel,
            mOLoader.CurrentLevelIndex, mOLoader.LevelCount);

        if (leOutcome == UWDeathRules.Outcome.Spared)
        {
            mOCharacter.Vitals.ApplyConversationValues(mOCharacter.Vitals.Hunger, UWDeathRules.SparedVitality,
                Mathf.RoundToInt(mOCharacter.Vitals.CurrentMana), mOCharacter.Vitals.Poison);
            return;
        }

        StartCoroutine(fDie(leOutcome == UWDeathRules.Outcome.Resurrect));
    }

    /// <summary>Listens to the character's vitals once they are there. Zero hit points already
    /// at that moment (a save game with none) count as a death, as the poll found it.</summary>
    private void fListen()
    {
        if (mOVitals != null || mOCharacter == null)
            return;

        mOVitals = mOCharacter.Vitals;
        mOVitals.Died += fOnDied;

        if (mOVitals.CurrentHP <= 0f)
            mbDeathPending = true;
    }

    private IEnumerator fDie(bool pbResurrect)
    {
        IsDying = true;

        Interaction lOInteraction = UWScene.Interaction;
        UWInventory lOInventory = UWScene.Inventory;

        UWAudioService.ChangeTheme(UWMusicSelector.DeathTheme);

        mOCharacter.Vitals.ChangeExperience(-UWDeathRules.ExperienceLoss(mOCharacter.Vitals.Experience), mOLoader.CurrentLevelIndex + 1);

        if (lOInteraction != null)
            lOInteraction.IsCombatModeActive = false;

        Vector3 lOPlace = mOCharacter.transform.position;
        UWTilePos lOTile = mOLoader.WorldPositionToTile(lOPlace);
        Vector3 lOFloor = new Vector3(lOPlace.x, mOLoader.GetFloorHeightAt(lOPlace), lOPlace.z);

        if (lOInventory != null && lOInventory.CursorItem != null)
            lOInventory.DropCursorItemInWorld(lOFloor, false);

        // put_at at the player's own spot (SKILLS.C): drop_around_place's Obj_AddEnd, the tail of
        // the tile's list (2026-10-18).
        mOLoader.SpawnObjectById(UWEndgameRules.RollBonesObjectId(), lOTile.X, lOTile.Y, 0, -1, lOFloor,
            UWWorldEdit.Arrival.PutDown);

        int liTreeLevel = UWSilverTree.TreeLevel;
        bool lbResurrect = pbResurrect;

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

                int liTileX = liAt % UWWorldScale.TilesPerAxis;
                int liTileY = liAt / UWWorldScale.TilesPerAxis;

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
        // RNG * 3 / 0x8000: 0 to 2 (UWDeathRules, do_resurrect).
        int liHp = UWDeathRules.ResurrectionHitPoints(Mathf.RoundToInt(mOCharacter.Vitals.MaxHP));
        int liMana = UWDeathRules.ResurrectionMana(Mathf.RoundToInt(mOCharacter.Vitals.MaxMana));

        mOCharacter.Vitals.ApplyConversationValues(mOCharacter.Vitals.Hunger, liHp, liMana, 0);
        mOCharacter.Vitals.EndAllSpells();
        mOCharacter.Flasks.StartFlaskFill();

        UWAudioService.ChangeTheme(ResurrectionTheme);
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
