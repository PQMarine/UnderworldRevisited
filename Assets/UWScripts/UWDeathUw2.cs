using System.Collections;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// WHEN THE SECOND GAME'S PLAYER DIES (UW2 plan phase 4; player_is_dead, the decision and the numbers are
/// UWDeathRulesUw2's). The vitals raise Died as they cross zero, as for UWDeath. Pending from the first game's
/// list: jail, the Ethereal Void, and the pits' fighters standing down. No death music or cutscene before phase 5:
/// the final death holds the world a moment in the cutscene's stead and leaves for the main menu.
/// </summary>
public sealed class UWDeathUw2 : MonoBehaviour
{
    /// <summary>In the final death cutscene's stead, in seconds.</summary>
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

        Debug.Log("[Walk] The player dies on level " + liLevel + ".");

        UWPlayerRecordUw2.ClearPits(lOSession.PlayerFileUw2);
        mOVitals.ChangeExperience(-UWDeathRulesUw2.ExperienceLoss(mOVitals.Experience), liLevel);

        UWAttackUw2 lOAttack = GetComponent<UWAttackUw2>();

        if (lOAttack != null)
            lOAttack.ClearFight();

        if (UWWalkUw2View.Current != null)
            UWWalkUw2View.Current.DropCursorItem();

        if (UWDeathRulesUw2.Decide(liLevel) == UWDeathRulesUw2.OutcomeEnum.Gem)
            fWakeAtGem(lOLoader, lOSession);
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
        pOLoader.WalkTeleport(new UWTeleportUw2.Target { Level = UWDeathRulesUw2.GemLevel, TileX = liX, TileY = liY });

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
        UWGameScreenUw2.ClearScroll();
        UWGameScreenUw2.Say(UWText.Block(pOLoader.UWDataImporter, 1, UWDeathRulesUw2.RegainString + 1, string.Empty));
    }

    private IEnumerator fFinalDeath()
    {
        IsDying = true;
        UWGameClock.Hold(UWGameClock.DeathHold);

        yield return new WaitForSecondsRealtime(FinalPause);

        UWWalkUw2.Leave();
    }
}
