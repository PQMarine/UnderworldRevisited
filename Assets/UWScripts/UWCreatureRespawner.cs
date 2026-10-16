using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The periodic respawning of monsters on the Unity side. The rule - which creation traps
/// respawn, only those out of the player's way, the one-in-four roll - is engine-free in
/// UWRespawnRules (P3 of the engine separation, 2026-09-18; after the original since
/// 2026-09-23). The crowding check belongs to the trap itself (UWTrapRules, the host's
/// IsSpawnedCreatureNear).
///
/// NO TIMER OF ITS OWN SINCE 2026-10-15: the roll sits in the player's five-minute block
/// (UWCharacter.OnFiveMinuteBlock, duration_check in the original), which runs on across
/// levels; until then this component counted 504 real seconds from the level's start (audit
/// round 2, A26 / KR N4). The night calls Run as well (UWSleep).
/// </summary>
public class UWCreatureRespawner : MonoBehaviour
{
    private UWLevelLoader mOLoader;

    private Transform mOPlayer;

    /// <summary>
    /// One pass: from the five-minute block when its roll came up, with the distance test, and
    /// from the night (UWSleep), where the original calls the same routine with 0, which switches
    /// the distance test off - every respawn trap on the level fires (Sleep_ovr143_D1F).
    /// </summary>
    /// <returns>How many traps were fired.</returns>
    public int Run(bool pbAnyDistance)
    {
        if (mOLoader == null)
            mOLoader = GetComponent<UWLevelLoader>();

        if (mOLoader == null || mOLoader.CurrentLevel == null || mOLoader.CurrentLevel.TileData == null)
            return 0;

        if (!fEnsurePlayer())
            return 0;

        UWTilePos lOPlayerTile = mOLoader.WorldPositionToTile(mOPlayer.position);

        return UWRespawnRules.Run(mOLoader.CurrentLevel, lOPlayerTile, pbAnyDistance,
            (pOTrap, piTileX, piTileY) => UWTriggerSystem.FireTrapInTile(pOTrap, piTileX, piTileY, mOLoader));
    }

    private bool fEnsurePlayer()
    {
        if (mOPlayer != null)
            return true;

        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter == null)
            return false;

        mOPlayer = lOCharacter.transform;

        return true;
    }
}
