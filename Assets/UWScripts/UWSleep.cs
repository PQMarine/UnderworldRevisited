using System.Collections.Generic;
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
        UWLevelLoader pOLevelLoader, bool pbPassedOut = false, bool pbBedroll = false)
    {
        if (pOData == null || pOCharacter == null || pOInteraction == null || pOLevelLoader == null)
            return false;

        return UWSleepRules.Sleep(pOCharacter.Vitals, pOLevelLoader.CurrentLevelIndex + 1,
            new UWSleepHost(pOCharacter, pOInteraction, pOLevelLoader), pbPassedOut, pbBedroll);
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
        /// Water and lava forbid a chosen sleep: the original refuses in motion states swimming
        /// and lava (player_sleep's motion_state &amp; 0x1B) - the same states that kill whoever
        /// passes out (IsInWater, IsOnLava), so the shore refuses and water walking does not.
        /// Floating is IsOnGround's.
        /// </summary>
        public bool CanSleepHere => !IsInWater && !IsOnLava;

        /// <summary>
        /// The original's MS_SWIM: the motion core's swimming state, which it takes in water and
        /// at the water's edge unless the player walks on water (ProcessPlayerTileState). Without
        /// the core, the terrain's swimming. Until 2026-10-15 the water contact alone, so the shore
        /// counted as dry (audit round 2, A31).
        /// </summary>
        public bool IsInWater
        {
            get
            {
                UWPlayerMovement lOMovement = fMovement();

                if (lOMovement != null && lOMovement.UsesMotionCore)
                    return lOMovement.Motion.CurrentState == UWPlayerMotion.State.Swimming;

                UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

                return lOTerrain != null && lOTerrain.IsSwimming;
            }
        }

        /// <summary>The original's MS_LAVA: the core's lava state - standing on it, not floating
        /// above it.</summary>
        public bool IsOnLava
        {
            get
            {
                UWPlayerMovement lOMovement = fMovement();

                if (lOMovement != null && lOMovement.UsesMotionCore)
                    return lOMovement.Motion.CurrentState == UWPlayerMotion.State.Lava;

                UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

                return lOTerrain != null && lOTerrain.IsOnLava;
            }
        }

        /// <summary>UWCharacter sits on the Main Camera, a child of the controller with
        /// UWPlayerMovement - GetComponent found nothing until 2026-10-13.</summary>
        private UWPlayerMovement fMovement()
        {
            return mOCharacter != null ? mOCharacter.GetComponentInParent<UWPlayerMovement>() : null;
        }

        public void DamagePlayer(int piDamage)
        {
            if (mOCharacter != null)
                mOCharacter.ApplyDamage(piDamage);
        }

        /// <summary>The night's closing doors (DoClosingDoors(0)): every open door of the level,
        /// wherever it is, unless its tile carries the tile's door bit, with the chance given -
        /// the rule is UWDoorRules.NightCloses. Until 2026-10-11 every door within eight tiles
        /// (the reference's reading); until 2026-10-12 the swinging doors only, never a
        /// portcullis (B11: DoClosingDoors walks every door object of the open range, the raised
        /// grate 0x14E included - UnderworldExhumed event/TRIGGER.C).</summary>
        public void CloseDoorsOnLevel(int piChanceInTen)
        {
            UWLevel lOLevel = mOLoader != null ? mOLoader.CurrentLevel : null;

            // Walked over a copy: a door closing from here raises its state into the data, and
            // the registry must not change under the loop.
            foreach (UWDoor lODoor in new List<UWDoor>(UWDoor.All))
            {
                if (lODoor == null || lODoor.Data == null)
                    continue;

                UWTile lOTileData = lOLevel != null ? lOLevel.GetTile(lODoor.Data.TileX, lODoor.Data.TileY) : null;
                bool lbTileHasDoorBit = lOTileData != null && lOTileData.HasDoor;

                // The roll is drawn for a candidate only, as the original's && does.
                if (!UWDoorRules.IsNightCandidate(!lODoor.IsClosed, lODoor.IsMoving, lbTileHasDoorBit))
                    continue;

                if (!UWDoorRules.NightCloses(!lODoor.IsClosed, lODoor.IsMoving, lbTileHasDoorBit,
                    UWRandom.Next(10), piChanceInTen))
                    continue;

                lODoor.Close();
            }
        }

        public int CollectGarbage(int piRange, int piLimit)
        {
            return mOLoader != null && mOLoader.CurrentLevel != null ? mOLoader.WorldEdit.CollectGarbage(piRange, piLimit) : 0;
        }

        public bool IsOnGround
        {
            get
            {
                // See fMovement: the movement sits on the parent (until 2026-10-13 never found,
                // so the sleeper always counted as standing).
                UWPlayerMovement lOMovement = fMovement();

                return lOMovement == null || (!lOMovement.IsJumping && lOMovement.IsWalkingOnGround);
            }
        }

        // ------------------------------------------------- The creatures around the sleeper

        /// <summary>The creatures of the last CollectCreatures, in the original's order.</summary>
        private readonly System.Collections.Generic.List<UWCritter> mOCreatures =
            new System.Collections.Generic.List<UWCritter>();

        /// <summary>
        /// The living creatures on the tiles of the area around the player's tile (UWSleepRules.InArea), ordered as
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

                if (!UWSleepRules.InArea(lOCritter.Record.TileX - lOPlayer.X, lOCritter.Record.TileY - lOPlayer.Y, piTileRadius))
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

        public bool TryGetCreaturePath(int piIndex, UWTilePos pOTo, int piToHeight, System.Collections.Generic.List<UWTilePos> pOPath)
        {
            return fCreature(piIndex) != null && fCreature(piIndex).TryGetPathTo(pOTo, piToHeight, pOPath);
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

        /// <summary>The level-exit pass on the living level (UWLevelExit.PassTime, 2026-10-15):
        /// the creatures stay in the scene, so their bodies follow what the pass wrote.</summary>
        public void LetTimePassForCreatures()
        {
            if (mOLoader == null || mOLoader.CurrentLevel == null)
                return;

            int liSentHome = UWLevelExit.PassTime(mOLoader.WorldEdit, new UWNightCreatureHost(mOLoader), out int liRemoved);

            if (liSentHome > 0 || liRemoved > 0)
                Debug.Log(string.Format("The night passed: {0} creatures sent home, {1} removed.", liSentHome, liRemoved));
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

    /// <summary>What the night's time pass asks of the scene: a temporary creature vanishes at
    /// once - inactive before the respawner looks for crowding, as the original frees it before
    /// DoWanderingMonsters - and a changed creature's body and health follow its record.</summary>
    private sealed class UWNightCreatureHost : IUWCreatureTimeHost
    {
        private readonly UWLevelLoader mOLoader;

        public UWNightCreatureHost(UWLevelLoader pOLoader)
        {
            mOLoader = pOLoader;
        }

        public void RemoveCreature(UWNpc pONpc)
        {
            if (mOLoader.TryGetEntity(pONpc, out UWEntityInfo lOEntity) && lOEntity != null)
                lOEntity.gameObject.SetActive(false);

            UWLevelExit.RemoveFromLevel(mOLoader.WorldEdit, pONpc);
        }

        public void CreatureChanged(UWNpc pONpc)
        {
            if (!mOLoader.TryGetEntity(pONpc, out UWEntityInfo lOEntity) || lOEntity == null)
                return;

            UWCritter lOCritter = lOEntity.GetComponent<UWCritter>();

            if (lOCritter != null)
                lOCritter.FollowRecord();
        }
    }
}
