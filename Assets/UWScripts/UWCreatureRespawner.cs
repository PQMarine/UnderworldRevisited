using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The periodic respawning of monsters on the Unity side. The rule - which creation traps
/// respawn, only those out of the player's way, the one-in-four roll - is engine-free in
/// UWRespawnRules (P3 of the engine separation, 2026-09-18; after the original since
/// 2026-09-23); this component keeps the timer. The crowding check belongs to the trap
/// itself (UWTrapRules, the host's IsSpawnedCreatureNear).
/// </summary>
public class UWCreatureRespawner : MonoBehaviour
{
    /// <summary>Twenty-four player ticks, the five-minute block - the same tick that spell
    /// durations run down with (see UWRespawnRules.IntervalSeconds and UWPlayerTick).</summary>
    [SerializeField]
    private float mfIntervalSeconds = UWRespawnRules.IntervalSeconds;

    private float mfNextCheck;

    private UWLevelLoader mOLoader;

    private Transform mOPlayer;

    private void Start()
    {
        // Not immediately on entering a level, otherwise something would already be standing there on load.
        mfNextCheck = Time.time + mfIntervalSeconds;
    }

    private void Update()
    {
        if (Time.time < mfNextCheck)
            return;

        mfNextCheck = Time.time + mfIntervalSeconds;

        if (!UWRespawnRules.RollOpportunity())
            return;

        Run(false);
    }

    /// <summary>
    /// One pass. Public so that sleeping can trigger it as well (UWSleep): the original calls
    /// the same routine there with 0, which switches the distance test off - every respawn
    /// trap on the level fires (Sleep_ovr143_D1F).
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
