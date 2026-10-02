using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Sleeping in the bedroll on the Unity side. The sequence, the recovery and the dreams are
/// engine-free in UWSleepRules since 2026-09-18 (P3 of the engine separation); this class
/// keeps the old entry point and answers what needs the scene: the ground under the
/// player, the creatures around him, the music, the respawner, the cutscene and the black
/// view window.
/// </summary>
public static class UWSleep
{
    /// <summary>The bedroll.</summary>
    public const int BedrollObjectId = UWSleepRules.BedrollObjectId;

    /// <summary>
    /// Goes to sleep. Returns false if it is not possible at all - then a message
    /// is already in the message scroll.
    /// </summary>
    public static bool Sleep(DataImport pOData, UWCharacter pOCharacter, Interaction pOInteraction,
        UWLevelLoader pOLevelLoader, bool pbPassedOut = false)
    {
        if (pOData == null || pOCharacter == null || pOInteraction == null || pOLevelLoader == null)
            return false;

        return UWSleepRules.Sleep(pOCharacter.Vitals, pOLevelLoader.CurrentLevelIndex + 1,
            new UWSleepHost(pOCharacter, pOInteraction, pOLevelLoader), pbPassedOut);
    }

    /// <summary>What the rules ask of the scene during one night.</summary>
    private sealed class UWSleepHost : IUWSleepHost
    {
        private readonly UWCharacter mOCharacter;

        private readonly Interaction mOInteraction;

        private readonly UWLevelLoader mOLoader;

        public UWSleepHost(UWCharacter pOCharacter, Interaction pOInteraction, UWLevelLoader pOLoader)
        {
            mOCharacter = pOCharacter;
            mOInteraction = pOInteraction;
            mOLoader = pOLoader;
        }

        /// <summary>
        /// Water and lava forbid sleeping. The reference checks the tile state for this
        /// and additionally gravity, i.e. whether the player is hanging in the air - the latter is left
        /// out here, because our player never stays floating anyway.
        /// </summary>
        public bool CanSleepHere
        {
            get
            {
                UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

                return lOTerrain == null || (!lOTerrain.IsSwimming && !lOTerrain.IsOnLava);
            }
        }

        /// <summary>See IUWSleepHost: only whoever PASSES OUT gets this far, and then the
        /// water drowns him.</summary>
        public bool IsInWater
        {
            get
            {
                UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

                return lOTerrain != null && lOTerrain.IsSwimming;
            }
        }

        public bool IsOnLava
        {
            get
            {
                UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

                return lOTerrain != null && lOTerrain.IsOnLava;
            }
        }

        /// <summary>Levitate, Fly or Water Walk - the mask 0x16 of the original's
        /// MagicMotionAbilities.</summary>
        public bool HasMagicMotion
        {
            get
            {
                UWPlayerMovement lOMovement = mOCharacter != null
                    ? mOCharacter.GetComponent<UWPlayerMovement>()
                    : null;

                return lOMovement != null && (lOMovement.MagicalMotionAbilities & MagicMotionMask) != 0;
            }
        }

        public void DamagePlayer(int piDamage)
        {
            if (mOCharacter != null)
                mOCharacter.ApplyDamage(piDamage);
        }

        /// <summary>Closes every door within the radius, as the original does before the night
        /// (FindAndCloseDoors_ovr153_15C4).</summary>
        public void CloseDoorsAround(int piTileRadius)
        {
            if (mOCharacter == null)
                return;

            float lfRange = piTileRadius * UWWorldScale.TileSize;

            foreach (DoorMover lODoor in Object.FindObjectsByType<DoorMover>(FindObjectsSortMode.None))
            {
                if (lODoor == null || lODoor.IsClosed || lODoor.IsMoving)
                    continue;

                if ((lODoor.transform.position - mOCharacter.transform.position).sqrMagnitude
                    > lfRange * lfRange)
                    continue;

                // The same entry point a click uses - so the leaf swings, the sound plays and
                // the tile blocker comes back exactly as it does otherwise.
                lODoor.StartUsingDoor();
            }
        }

        /// <summary>The bits of MagicMotionAbilities that spare the sleeper the lava: levitate,
        /// fly and water walk (mask 0x16 in SleepingOnDamagingSurface_ovr143_C7A).</summary>
        private const int MagicMotionMask = 0x16;

        // ------------------------------------------------- The creatures around the sleeper

        /// <summary>The creatures of the last CollectCreatures, in the original's order.</summary>
        private readonly System.Collections.Generic.List<UWCritter> mOCreatures =
            new System.Collections.Generic.List<UWCritter>();

        /// <summary>
        /// The living creatures on the tiles of the square around the player's tile, ordered as
        /// RunCodeOnObjectsInArea_seg038_3307_9C7 walks them: column by column (x outer, y inner).
        /// Their tile is the one in their record, where the original keeps it too.
        /// </summary>
        public int CollectCreatures(int piTileRadius)
        {
            mOCreatures.Clear();

            UWCritter[] lOCritters = UWScene.FindCritters();

            if (lOCritters == null)
                return 0;

            UWTilePos lOPlayer = PlayerTile;

            foreach (UWCritter lOCritter in lOCritters)
            {
                if (lOCritter == null || lOCritter.Record == null || lOCritter.IsDead)
                    continue;

                if (System.Math.Abs(lOCritter.Record.TileX - lOPlayer.X) > piTileRadius
                    || System.Math.Abs(lOCritter.Record.TileY - lOPlayer.Y) > piTileRadius)
                    continue;

                mOCreatures.Add(lOCritter);
            }

            mOCreatures.Sort((pOLeft, pORight) => pOLeft.Record.TileX != pORight.Record.TileX
                ? pOLeft.Record.TileX.CompareTo(pORight.Record.TileX)
                : pOLeft.Record.TileY.CompareTo(pORight.Record.TileY));

            return mOCreatures.Count;
        }

        public UWCritterRecord CreatureRecord(int piIndex)
        {
            return fCreature(piIndex) != null ? fCreature(piIndex).Record : null;
        }

        public int CreatureTravelRange(int piIndex)
        {
            return fCreature(piIndex) != null ? fCreature(piIndex).TravelRange : 0;
        }

        public bool TryGetCreaturePath(int piIndex, UWTilePos pOTo, System.Collections.Generic.List<UWTilePos> pOPath)
        {
            return fCreature(piIndex) != null && fCreature(piIndex).TryGetPathTo(pOTo, pOPath);
        }

        /// <summary>Every move trigger on the tile whose trap is a rune of warding goes off for the
        /// creature (ovr104_71C: class 6 subclass 2 objects linking to trap 9).</summary>
        public void FireWardTriggers(int piIndex, UWTilePos pOTile)
        {
            UWCritter lOCritter = fCreature(piIndex);
            UWLevel lOLevel = mOLoader.CurrentLevel;
            UWTile lOTile = lOLevel != null ? lOLevel.GetTile(pOTile.X, pOTile.Y) : null;

            if (lOCritter == null || lOTile == null || lOTile.ObjectsInTile == null)
                return;

            foreach (UWObject lOObject in lOTile.ObjectsInTile.ToArray())
            {
                if (lOObject == null || lOObject.ID != UWObjectMechanics.MoveTriggerId)
                    continue;

                UWObject lOTrap = UWObjectMechanics.GetLinkedObject(lOObject, lOLevel.Masterlist);

                if (lOTrap != null && lOTrap.ID == UWObjectMechanics.WardTrapId)
                    UWTriggerSystem.TryFireWardTrigger(lOObject, lOCritter, mOLoader, mOInteraction);
            }
        }

        public bool PlaceCreature(int piIndex, UWTilePos pOTile, int piSubX, int piSubY)
        {
            return fCreature(piIndex) != null && fCreature(piIndex).PlaceAtSpot(pOTile, piSubX, piSubY);
        }

        public UWTile.TileTypeEnum TileTypeAt(UWTilePos pOTile)
        {
            UWTile lOTile = mOLoader.CurrentLevel != null ? mOLoader.CurrentLevel.GetTile(pOTile.X, pOTile.Y) : null;

            return lOTile != null ? lOTile.TileType : UWTile.TileTypeEnum.solid;
        }

        public UWTilePos PlayerTile => mOLoader.WorldPositionToTile(mOCharacter.transform.position);

        /// <summary>The player's zpos (0 to 127) shifted by three, as the record keeps a height.</summary>
        public int PlayerFloorLevel
        {
            get
            {
                Transform lOCamera = Camera.main != null ? Camera.main.transform : mOCharacter.transform;

                return (UWViewpoint.WorldToOriginalZ(UWPlayerThrow.GetFeet(lOCamera).y) >> 3) >> 3;
            }
        }

        private UWCritter fCreature(int piIndex)
        {
            return piIndex >= 0 && piIndex < mOCreatures.Count ? mOCreatures[piIndex] : null;
        }

        public void PlaySleepTheme()
        {
            UWMusic.ChangeTheme(UWMusic.MapsAndLegendsTheme);
        }

        /// <summary>The respawner sits on the same object as the level loader.</summary>
        public void RunRespawner()
        {
            UWCreatureRespawner lORespawner = mOLoader.GetComponent<UWCreatureRespawner>();

            // Asleep, the distance test is off: every respawn trap fires (Sleep_ovr143_D1F
            // calls TriggerCreateObjectTrap_ovr153_1517 with 0).
            if (lORespawner != null)
                lORespawner.Run(true);
        }

        public void BurnCarriedLights(int piTicks)
        {
            UWInventory lOInventory = UWScene.Inventory;

            if (lOInventory != null)
                lOInventory.Model.BurnTick(piTicks, 0);
        }

        public void AddGeneralMessage(int piIndex)
        {
            mOInteraction.AddGeneralMessage(piIndex);
        }

        public bool TryPlayDream(int piCutscene)
        {
            UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

            if (lOPlayer == null)
                return false;

            lOPlayer.PlayCutscene(piCutscene);

            return true;
        }

        public void ShowNightBlack(float pfSeconds)
        {
            UWGameUI lOUi = UWScene.GameUi;

            if (lOUi != null)
                lOUi.FlashWindowBlack(pfSeconds);
        }
    }
}
