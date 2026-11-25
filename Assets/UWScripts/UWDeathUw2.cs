using System.Collections;
using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// WHEN THE SECOND GAME'S PLAYER DIES (UW2 plan phase 4; player_is_dead, the decision and the numbers are
/// UWDeathRulesUw2's). The vitals raise Died as they cross zero, as for UWDeath. Pending from the first game's
/// list: jail, the Ethereal Void, and the pits' fighters standing down. The death theme 7 (phase 5 part B); the final
/// death plays cutscene 0x103 in the view's window (phase 5 part D) and leaves for the main menu.
/// </summary>
public sealed class UWDeathUw2 : MonoBehaviour
{
    /// <summary>In the final death cutscene's stead without a cutscene player, in seconds.</summary>
    private const float FinalPause = 1.5f;

    public static bool IsDying { get; private set; }

    private UWPlayerVitals mOVitals;

    private bool mbPending;

    private void OnDestroy()
    {
        IsDying = false;

        if (mOVitals != null)
            mOVitals.Died -= fOnDied;
    }

    private void fOnDied()
    {
        mbPending = true;
    }

    private void Update()
    {
        UWPlayerVitals lOVitals = UWVitalsUw2.Current != null ? UWVitalsUw2.Current.Vitals : null;

        if (lOVitals != mOVitals)
        {
            if (mOVitals != null)
                mOVitals.Died -= fOnDied;

            mOVitals = lOVitals;

            if (mOVitals != null)
            {
                mOVitals.Died += fOnDied;
                mbPending = mOVitals.CurrentHP <= 0;
            }
        }

        if (!mbPending || IsDying || mOVitals == null || UWConversationScreenUw2.IsOpen)
            return;

        mbPending = false;

        // Healed in the meantime.
        if (mOVitals.CurrentHP <= 0)
            fDie();
    }

    private void fDie()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWGameSession lOSession = UWGameSession.Current;

        if (lOLoader == null || lOSession == null)
            return;

        int liLevel = lOLoader.CurrentLevelIndex + 1;

        Debug.Log("[Session] The player dies on level " + liLevel + ".");

        // KILLED BY THE CASTLE'S PEOPLE ON LEVEL 1: jail instead (G6 part 3). The killer is the player object's last_hit.
        UWPlayerFile lOFile = lOSession.PlayerFileUw2;
        UWLevel lOLevel = lOLoader.CurrentLevel;
        int liKiller = UWDeathRulesUw2.PlayerLastHit(lOFile);
        UWNpc lOKiller = lOLevel != null && liKiller > 0 && liKiller < lOLevel.Masterlist.Count ? lOLevel.Masterlist[liKiller] as UWNpc : null;

        if (UWDeathRulesUw2.Decide(liLevel, lOKiller) == UWDeathRulesUw2.OutcomeEnum.Jail)
        {
            fPutInJail(lOLoader, lOSession, liLevel);

            return;
        }

        // IN THE VOID: waking (punt_void), and no more - the hit points stay as they are, so the death comes again
        // where the player lay, as player_is_dead is asked again on the next frame in the original (G6, the Void).
        if (UWPlayerRecordUw2.IsInVoid(lOFile))
        {
            UWSleepUw2.WakeFromVoid();
            mbPending = mOVitals.CurrentHP <= 0;

            return;
        }

        // The pits lost: the fighters stand down, then the list, the record and Jospur's debt go.
        UWDeathRulesUw2.PitFightersStandDown(lOLevel, lOFile);
        UWPlayerRecordUw2.ClearPits(lOFile);

        // The death theme at once (load_new_music(MUSIC_DEATH), SKILLS.C:843; UW2 plan phase 5 part B).
        UWAudioService.Uw2Load(UWMusicSelectorUw2.DeathTheme);
        mOVitals.ChangeExperience(-UWDeathRulesUw2.ExperienceLoss(mOVitals.Experience), liLevel);

        UWAttackUw2 lOAttack = GetComponent<UWAttackUw2>();

        if (lOAttack != null)
            lOAttack.ClearFight();

        if (UWWorldViewUw2.Current != null)
            UWWorldViewUw2.Current.DropCursorItem();

        // player_is_dead's fadeout3d(5): the way to the gem goes through the black (per user in the original 2026-11-17,
        // test 70.2: a moment of black, then the gem; ours moved at once until then).
        if (UWDeathRulesUw2.Decide(liLevel) == UWDeathRulesUw2.OutcomeEnum.Gem)
        {
            if (UWSleepUw2.Current == null || !UWSleepUw2.Current.Blink(() => fWakeAtGem(lOLoader, lOSession)))
                fWakeAtGem(lOLoader, lOSession);
        }
        else
            StartCoroutine(fFinalDeath());
    }

    /// <summary>do_gem: the level, the place by a random heading, the free tile near it, the numbers; poison and
    /// spells gone, a blood stain on the place if it is open, the automap on.</summary>
    private void fWakeAtGem(UWLevelLoader pOLoader, UWGameSession pOSession)
    {
        int liX = UWDeathRulesUw2.GemTileX;
        int liY = UWDeathRulesUw2.GemTileY;

        UWDeathRulesUw2.MoveAlong(UWRandom.Next(UWOriginalRandom.Range), UWDeathRulesUw2.GemDistance, ref liX, ref liY);
        pOLoader.Uw2Teleport(new UWTeleportUw2.Target { Level = UWDeathRulesUw2.GemLevel, TileX = liX, TileY = liY });

        mOVitals.ApplyConversationValues(mOVitals.Hunger, UWDeathRules.ResurrectionHitPoints(mOVitals.MaxHP),
            UWDeathRules.ResurrectionMana(mOVitals.MaxMana), 0);
        mOVitals.EndAllSpells();

        UWLevel lOLevel = pOLoader.CurrentLevel;
        UWTile lOTile = lOLevel != null ? lOLevel.GetTile(liX, liY) : null;

        if (lOTile != null && lOTile.TileType == UWTile.TileTypeEnum.open)
        {
            Vector3 lOAt = new Vector3(UWUnits.TileCentreToWorldAxis(liX), 0f, UWUnits.TileCentreToWorldAxis(liY));

            lOAt.y = pOLoader.GetFloorHeightAt(lOAt);
            pOLoader.SpawnObjectById(UWDeathRulesUw2.WakingStain, liX, liY, 0, -1, lOAt, UWWorldEdit.Arrival.PutDown);
        }

        UWAutomapStateUw2.TurnOn(pOSession.PlayerFileUw2);

        // do_gem's set_new_music(10) (SKILLS.C:696), after the level's walking choice.
        UWAudioService.Uw2Request(UWMusicSelectorUw2.FollowTheme);
        UWGameScreenUw2.ClearScroll();
        UWGameScreenUw2.Say(UWText.Block(pOLoader.UWDataImporter, 1, UWDeathRulesUw2.RegainString + 1, string.Empty));
    }

    /// <summary>
    /// put_player_in_jail (uw2 event/WORLDEV.C:2283): the hit points set to the strength, a ninth of the experience gone,
    /// up to forty minutes passed (the schedules catch up), the cell at 0x2A/0x26, the guards calmed (attitude 2, goal 1)
    /// and quest 112, the level's creatures moved on as by a night, Lord British outside the cell, the cell's trigger,
    /// the fight left, theme 10. Not here: the door hack at the cell, which finds no door in the
    /// shipped level.
    /// </summary>
    private void fPutInJail(UWLevelLoader pOLoader, UWGameSession pOSession, int piLevel)
    {
        UWPlayerFile lOFile = pOSession.PlayerFileUw2;
        UWWorld lOWorld = UWWorld.Of(pOLoader);

        Debug.Log("[Session] Killed by the castle's people: the player wakes in jail.");

        mOVitals.ApplyConversationValues(mOVitals.Hunger, mOVitals.Strength, mOVitals.CurrentMana, mOVitals.Poison);
        mOVitals.ChangeExperience(-UWDeathRulesUw2.ExperienceLoss(mOVitals.Experience), piLevel);
        // At once: pass_time reads the record next (the day clock), and the record is what comes back on the next tick.
        if (UWVitalsUw2.Current != null)
            UWVitalsUw2.Current.WriteToRecord();

        UWDeathRulesUw2.PassTime(lOFile, UWDeathRulesUw2.JailSeconds(UWPlayerRecordUw2.GameClock(lOFile), UWRandom.Next(UWOriginalRandom.Range)));

        // The vitals keep the clock and write it back on every tick: they take the new one.
        mOVitals.TakeClock(UWPlayerRecordUw2.GameClock(lOFile));
        UWScheduleHostUw2.RunAll();

        if (UWWorldViewUw2.Current != null)
            UWWorldViewUw2.Current.DropCursorItem();

        pOLoader.Uw2Teleport(new UWTeleportUw2.Target { Level = UWDeathRulesUw2.JailLevel, TileX = UWDeathRulesUw2.JailTileX, TileY = UWDeathRulesUw2.JailTileY });

        UWLevel lOLevel = pOLoader.CurrentLevel;
        UWObjectClassProperties lOProperties = pOLoader.UWDataImporter != null ? pOLoader.UWDataImporter.ObjectClassProperties : null;

        UWDeathRulesUw2.CalmTheGuards(lOLevel, lOProperties, lOFile);

        if (lOWorld != null)
            lOWorld.LetTimePassForCreatures();

        UWNpc lOBritish = UWDeathRulesUw2.FindBritish(lOLevel);

        if (lOBritish != null && lOWorld != null)
        {
            bool lbFlier = lOProperties != null && lOProperties.TryGetCritter(lOBritish.ID, out UWObjectClassProperties.Critter lORow) && lORow.IsFlier;

            UWConversationRulesUw2.TeleportCritter(lOWorld, lOBritish, UWDeathRulesUw2.BritishTileX, UWDeathRulesUw2.BritishTileY, lbFlier);
            UWDeathRulesUw2.SendBritishHome(lOBritish);
        }

        IUWScheduleHost lOHost = UWScheduleHostUw2.ForTraps(pOLoader);

        if (lOHost != null && lOHost.Traps != null)
            UWTrapRulesUw2.FireTriggerAt(UWDeathRulesUw2.JailTriggerX, UWDeathRulesUw2.JailTriggerY, lOHost.Traps);

        UWAttackUw2 lOAttack = GetComponent<UWAttackUw2>();

        if (lOAttack != null)
            lOAttack.ClearFight();

        // put_player_in_jail's set_new_music(10) (WORLDEV.C:2291; UW2 plan phase 5 part B).
        UWAudioService.Uw2Request(UWMusicSelectorUw2.FollowTheme);
    }

    /// <summary>real_death(1) (UWEDIT.C:417): the final death's cutscene 0x103 in the view's window (CS403, the skulls;
    /// UW2 plan phase 5 part D), then the menu. Without a cutscene player the world holds a moment in its stead.</summary>
    private IEnumerator fFinalDeath()
    {
        IsDying = true;
        UWGameClock.Hold(UWGameClock.DeathHold);

        UWIntroPlayer lOPlayer = UWScene.IntroPlayer;
        bool lbDone = false;

        if (lOPlayer != null)
            lOPlayer.PlayCutscene(UWCutsceneRunnerUw2.FinalDeathCutscene, () => lbDone = true);

        if (lOPlayer != null && lOPlayer.IsPlaying)
        {
            while (!lbDone && lOPlayer.IsPlaying)
                yield return null;
        }
        else
            yield return new WaitForSecondsRealtime(FinalPause);

        UWGameStartUw2.Leave();
    }
}
