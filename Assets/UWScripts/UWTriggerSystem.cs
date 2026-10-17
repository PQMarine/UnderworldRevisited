using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// The Unity side of the trap chain. The rules - which trigger fires what, the chain along
/// the special links, the one-shot cleanup and every trap that works on the tile data, the
/// variables or the player - live engine-free in UWTrapRules since 2026-09-18 (P3 of the
/// engine separation). This facade keeps the old public API for Interaction, UWMoveTrigger,
/// UWInventory and the rest, builds a UWTrapHost per firing and does what needs the
/// engine: the sound of a switch, the flight of a trap arrow, a spell, the remote camera,
/// the search over the creature bodies, doors and levers.
/// </summary>
public static class UWTriggerSystem
{
    /// <summary>See UWTrapRules.RevealSearchSkill.</summary>
    public const int RevealSearchSkill = UWTrapRules.RevealSearchSkill;

    /// <summary>For Interaction.cs: the player has USED pOObject (switch, double door,
    /// ...). Looks up the chain object -> a_use trigger -> trap and fires it.</summary>
    public static bool TryFireSwitch(UWObject pOObject, UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        bool lbFired = UWTrapRules.TryFireSwitch(pOObject, new UWTrapHost(pOLevelLoader, pOInteraction));

        // A BUTTON CLICKS, nothing else does (UWTrapRules.ClicksWhenUsed). Until 2026-10-12 every
        // object whose trigger fired clicked - levers, the talking door, the bridge - and a
        // button without a trigger stayed silent.
        if (pOObject != null && UWTrapRules.ClicksWhenUsed(pOObject.ID))
        {
            UWEntityInfo lOEntity;

            if (pOLevelLoader != null && pOLevelLoader.TryGetEntity(pOObject, out lOEntity) && lOEntity != null)
                UWSoundEffects.PlayAt(UWSoundEffects.Lockpick, lOEntity.transform.position);
            else
                UWSoundEffects.PlayAtAvatar(UWSoundEffects.Lockpick);
        }

        return lbFired;
    }

    /// <summary>For Interaction.cs: the player has LOOKED AT pOObject. Looks up the chain
    /// object -> a_look trigger -> trap.</summary>
    /// <param name="piSearchSkill">The player's Search skill. Only hidden
    /// triggers ask for it, see UWTrapRules.</param>
    public static bool TryFireLookTrigger(UWObject pOObject, UWLevelLoader pOLevelLoader, Interaction pOInteraction, int piSearchSkill)
    {
        return UWTrapRules.TryFireLookTrigger(pOObject, new UWTrapHost(pOLevelLoader, pOInteraction), piSearchSkill);
    }

    /// <summary>A thing in the world casts the charged spell it carries, with itself as the
    /// caster, as a spell trap does (inanimate_spell; UWItemSpellRules.TryCastFromWorldObject,
    /// 2026-10-15). Only for a class that acts with a thing as the caster
    /// (UWTrapRules.SpellTrapCastsFor); false otherwise.</summary>
    public static bool CastSpellFromThing(UWObject pOThing, int piMajorClass, int piMinorClass,
        UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        if (pOThing == null || piMajorClass < 0 || !UWTrapRules.SpellTrapCastsFor(piMajorClass, false, false))
            return false;

        return new UWTrapHost(pOLevelLoader, pOInteraction).FireSpellTrap(pOThing, pOThing.TileX, pOThing.TileY,
            piMajorClass, piMinorClass);
    }

    /// <summary>One action of the bullfrog puzzle without a trap object - the wand of the Frog
    /// resets it. See UWTrapRules.FireBullfrog.</summary>
    public static void FireBullfrog(int piMode, UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        UWTrapRules.FireBullfrog(piMode, new UWTrapHost(pOLevelLoader, pOInteraction));
    }

    /// <summary>an_open trigger: fires when a door is opened. See UWTrapRules.TryFireOpenTrigger.</summary>
    public static bool TryFireOpenTrigger(UWObject pODoorData, UWLevelLoader pOLevelLoader, Interaction pOInteraction,
        UWTrapRules.Setter peOpener)
    {
        return UWTrapRules.TryFireOpenTrigger(pODoorData, new UWTrapHost(pOLevelLoader, pOInteraction), peOpener);
    }

    /// <summary>For UWInventory: the player has PICKED UP pOObject. See
    /// UWTrapRules.TryFirePickUpTrigger.</summary>
    public static bool TryFirePickUpTrigger(UWObject pOObject, UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        return UWTrapRules.TryFirePickUpTrigger(pOObject, new UWTrapHost(pOLevelLoader, pOInteraction));
    }

    /// <summary>Fires a move trigger (a_move trigger) - it lies in the tile itself and fires on
    /// entering (see UWMoveTrigger).</summary>
    public static bool TryFireMoveTrigger(UWObject pOTrigger, UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        return UWTrapRules.TryFireMoveTrigger(pOTrigger, new UWTrapHost(pOLevelLoader, pOInteraction));
    }

    /// <summary>A move trigger set off by a creature (pOCritter) or a thing in flight (piThingIndex,
    /// 0 for none) - the original's Trigger with that mover as the one that set it off (see
    /// UWProjectileWorld.TriggerMove). Damage and teleport go to the creature; a damage trap
    /// destroys a worn-out thing (UWTrapRules).</summary>
    public static bool TryFireMoveTriggerBy(UWObject pOTrigger, UWLevelLoader pOLevelLoader, Interaction pOInteraction,
        UWCritter pOCritter, int piThingIndex)
    {
        return UWTrapRules.TryFireMoveTrigger(pOTrigger, new UWTrapHost(pOLevelLoader, pOInteraction, pOCritter, piThingIndex));
    }

    /// <summary>Fires the trap of a trigger, chain included. See UWTrapRules.FireTraps.</summary>
    public static bool FireTraps(UWObject pOTrigger, UWObject pOSwitchObject, UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        return UWTrapRules.FireTraps(pOTrigger, pOSwitchObject, new UWTrapHost(pOLevelLoader, pOInteraction));
    }

    /// <summary>A bumbled disarm sets the trap off - see UWTrapRules.SetOffDisarmableTrap.</summary>
    public static bool SetOffDisarmableTrap(UWObject pOTrap, UWObject pOTrigger, UWObject pOItem,
        int piTileX, int piTileY, UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        return UWTrapRules.SetOffDisarmableTrap(pOTrap, pOTrigger, pOItem, piTileX, piTileY,
            new UWTrapHost(pOLevelLoader, pOInteraction));
    }

    /// <summary>Fires a create-object trap WITHOUT a trigger in front of it - for the
    /// periodic respawn (see UWCreatureRespawner).</summary>
    public static bool FireTrapInTile(UWObject pOTrap, int piTileX, int piTileY, UWLevelLoader pOLevelLoader)
    {
        return UWTrapRules.FireTrapInTile(pOTrap, piTileX, piTileY, new UWTrapHost(pOLevelLoader, null));
    }

    /// <summary>
    /// Angers every member of a race around a tile and reports how many there were.
    ///
    /// Used by THEFT (see UWWorldPickup.ReportTheft): whoever picks up someone else's
    /// property upsets those present of the same race - within seven tiles back and eight
    /// forward of the item, the original's area (UWCritterRules.TheftAreaBack/Forward; until
    /// 2026-09-23 seven in each direction after the reference).
    ///
    /// SIGHT RANGE AND LINE OF SIGHT are checked as well since 2026-09-16
    /// (UWCritter.CanSeeTheftAt): the area alone let every goblin of a camp complain at
    /// once, three messages where the original prints one (per user). The line of sight is the
    /// original's tile line (UWTilePath.TestBetweenPoints) since 2026-09-23, for trespassing too.
    ///
    /// WHO MINDS IT AND WHAT HE SAYS follow the original since 2026-09-23
    /// (UWCritterRules.MindsTheft, AngerNPCByIllegalAction_ovr104_C37): the owner's bit 5 and the
    /// creatures with a locked attitude, the knights once the player is one of them, one step
    /// of goodwill less for everyone who sees it - also for one already hostile - and the
    /// message by the NEW attitude, "angered", "annoyed" or "notes". piOwner is the full
    /// six-bit owner value.
    /// </summary>
    /// <remarks>WHERE THEY LOOK: at the item itself - its sub-tile spot on the centre tile and
    /// its zpos plus COMOBJ height plus 12 (UWCritterRules.GetTheftSightZ), since
    /// 2026-09-23.</remarks>
    public static int AngerRaceAround(int piOwner, UWObject pOItem, UWTilePos pOCentreTile,
        UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        if (pOItem == null)
            return 0;

        int liZ = UWCritterRules.GetTheftSightZ(pOItem.ZPos, fGetHeight(pOItem.ID, pOLevelLoader));

        return fAngerRace(piOwner, pOCentreTile, UWCritterRules.TheftAreaBack, UWCritterRules.TheftAreaForward,
            (pOCentreTile.X << 3) + pOItem.XPos, (pOCentreTile.Y << 3) + pOItem.YPos, liZ,
            pOLevelLoader, pOInteraction);
    }

    /// <summary>An object's height from COMOBJ.DAT, 0 if unknown.</summary>
    private static int fGetHeight(int piObjectId, UWLevelLoader pOLevelLoader)
    {
        UWCommonObjectProperties.Entry lOEntry;

        return pOLevelLoader != null && pOLevelLoader.UWDataImporter != null
            && pOLevelLoader.UWDataImporter.CommonObjectProperties != null
            && pOLevelLoader.UWDataImporter.CommonObjectProperties.TryGet(piObjectId, out lOEntry)
            ? lOEntry.Height : 0;
    }

    /// <summary>The shared search of theft and trespassing: every creature that minds the
    /// owner (UWCritterRules.MindsTheft), whose tile lies within piTilesBack below and
    /// piTilesForward above the centre on both axes, and who sees the centre tile, loses
    /// goodwill and says so by its new attitude. Returns how many were angered.</summary>
    private static int fAngerRace(int piOwner, UWTilePos pOCentreTile, int piTilesBack, int piTilesForward,
        int piSeenX8, int piSeenY8, int piSeenZ, UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        if (piOwner == 0 || pOLevelLoader == null || pOInteraction == null)
            return 0;

        int liKnightQuest = UWQuestFlags.Get(UWCritterRules.TheftKnightQuest);

        UWCritter[] lOCritters = UWScene.FindCritters();

        int liAngered = 0;

        for (int liAt = 0; lOCritters != null && liAt < lOCritters.Length; liAt++)
        {
            UWCritter lOCritter = lOCritters[liAt];

            if (lOCritter == null)
                continue;

            UWEntityInfo lOInfo = lOCritter.GetComponentInChildren<UWEntityInfo>();

            if (lOInfo == null || lOInfo.ObjectData == null)
                continue;

            if (!UWCritterRules.MindsTheft(piOwner, fGetRace(lOInfo.ObjectData.ID, pOLevelLoader),
                    lOCritter.IsAttitudeLocked, liKnightQuest))
                continue;

            UWTilePos lOTile = pOLevelLoader.WorldPositionToTile(lOCritter.transform.position);

            int liDeltaX = lOTile.X - pOCentreTile.X;
            int liDeltaY = lOTile.Y - pOCentreTile.Y;

            if (liDeltaX < -piTilesBack || liDeltaX > piTilesForward
                || liDeltaY < -piTilesBack || liDeltaY > piTilesForward)
                continue;

            // Only whoever actually SEES it gets angry - see UWCritter.CanSeeTheftAt. The
            // area above is just the coarse pre-selection, as in the reference.
            if (!lOCritter.CanSeeTheftAt(piSeenX8, piSeenY8, piSeenZ))
                continue;

            int liAttitude = lOCritter.Anger();

            if (liAttitude < 0)
                continue;

            liAngered++;

            pOInteraction.AddMessage(fGetCritterName(pOLevelLoader, lOInfo.ObjectData.ID)
                + pOInteraction.GetGeneralMessage(UWCritterRules.TheftMessageBase + liAttitude));
        }

        return liAngered;
    }

    /// <summary>
    /// The creature's name as the message scroll prints it: "a goblin", not the raw
    /// "a_goblin" of the string block. The reference prints the same a_name, but its
    /// strings already come resolved (thief.AngerNPCByIllegalAction). Noticed on the
    /// theft message, which in the original reads "a goblin is angered by your action."
    /// (per user, 2026-09-16).
    /// </summary>
    private static string fGetCritterName(UWLevelLoader pOLevelLoader, int piObjectId)
    {
        return UWText.ObjectName(pOLevelLoader != null ? pOLevelLoader.UWDataImporter : null, piObjectId, false, true);
    }

    /// <summary>Does the creature belong to this race? The race is the shared name from
    /// CRIT.DAT - all goblins share it, whatever object number they carry.</summary>
    /// <summary>A creature's race, the critter table's general type (byte 9); -1 unknown.</summary>
    private static int fGetRace(int piObjectId, UWLevelLoader pOLevelLoader)
    {
        if (pOLevelLoader.UWDataImporter == null || pOLevelLoader.UWDataImporter.ObjectClassProperties == null)
            return -1;

        UWObjectClassProperties.Critter lOCritter;

        return pOLevelLoader.UWDataImporter.ObjectClassProperties.TryGetCritter(piObjectId, out lOCritter)
            ? lOCritter.GenericNameIndex
            : -1;
    }

    /// <summary>
    /// Opens or closes the door or portcullis on a tile, without a switch
    /// and without a trigger. This is used by the built-in conversation function
    /// "gronk_door" (uw-formats.txt 7.6, Id 0x25): an NPC lets the player in or
    /// locks him out.
    ///
    /// A lock is deliberately NOT touched here - unlike with the door trap. The
    /// function means "open the door", not "change its lock", and the user has confirmed in the
    /// original that a switch likewise leaves the lock state untouched.
    ///
    /// The mode is gronk_door's: open, close or toggle (UWConversationSession.GronkDoorOpen ...).
    /// Where the door is heading decides, not only whether it is shut: a door still swinging
    /// shut counts as closed, so "open" turns it round (the talking door closes itself at the
    /// start of its conversation and opens again at "Very well ..." - ours stayed shut, per
    /// user, 2026-10-01). The rule is UWDoorRules.GronkTogglesDoor (2026-10-12): the original's
    /// OpenDoor, CloseDoor and ToggleDoor, which the door trap uses as well.
    /// </summary>
    public static bool TrySetDoorState(int piTileX, int piTileY, int piMode, UWLevelLoader pOLevelLoader)
    {
        if (pOLevelLoader == null)
            return false;

        UWObject lODoorData = UWTrapRules.FindDoorInTile(piTileX, piTileY, pOLevelLoader.CurrentLevel);

        if (lODoorData == null)
            return false;

        if (!pOLevelLoader.TryGetEntity(lODoorData, out UWEntityInfo lOEntity))
            return false;

        UWDoor lODoor = lOEntity.GetComponentInParent<UWDoor>();

        if (lODoor == null)
            return false;

        // gronk_door passes no opener (OpenDoor(0, door)).
        if (UWDoorRules.GronkTogglesDoor(piMode, lODoor.IsHeadingClosed, lODoor.IsMoving))
            lODoor.Toggle(UWTrapRules.Setter.Thing);

        return true;
    }

    // ------------------------------------------------- The host of one firing

    /// <summary>
    /// What one firing of the trap rules sees of the game: the level loader, the message
    /// channel of this firing (null for a silent one, such as the respawn) and, for the rune
    /// of warding, the creature that stepped on the trigger.
    /// </summary>
    private sealed class UWTrapHost : IUWTrapHost
    {
        private readonly UWLevelLoader mOLoader;

        private readonly Interaction mOInteraction;

        private readonly UWCritter mOTriggeringCritter;

        private UWCharacter mOPlayer;

        private bool mbPlayerLookedUp;

        private readonly int miTriggeringThing;

        public UWTrapHost(UWLevelLoader pOLoader, Interaction pOInteraction, UWCritter pOTriggeringCritter = null,
            int piTriggeringThing = 0)
        {
            mOLoader = pOLoader;
            mOInteraction = pOInteraction;
            mOTriggeringCritter = pOTriggeringCritter;
            miTriggeringThing = piTriggeringThing;
        }

        public bool HasTriggeringThing => miTriggeringThing != 0;

        /// <summary>A damage trap set off by a thing in flight: destroyed if the hit wears it out,
        /// untouched otherwise (UWProjectileWorld.StrikeThing).</summary>
        public void StrikeTriggeringThing(int piDamage)
        {
            if (miTriggeringThing == 0 || mOLoader == null)
                return;

            UWProjectileWorld lOWorld = UWProjectileWorld.Ensure(mOLoader);

            if (lOWorld != null)
                lOWorld.StrikeThing(miTriggeringThing, piDamage);
        }

        /// <summary>A damage trap set off by a creature: the quality on its hit points
        /// (DamageTrap_ovr107_CB2 -> DamageObject, type 4).</summary>
        public void DamageTriggeringCreature(int piDamage)
        {
            // whack_thing: damage_item with no attacker (UWCritter.ApplyDamageWithoutAttacker).
            if (mOTriggeringCritter != null)
                mOTriggeringCritter.ApplyDamageWithoutAttacker(piDamage, UWDamageTypes.Physical);
        }

        private UWCharacter fPlayer
        {
            get
            {
                if (!mbPlayerLookedUp)
                {
                    mbPlayerLookedUp = true;
                    mOPlayer = UWScene.Character;
                }

                return mOPlayer;
            }
        }

        public UWDataImport.DataImport Data => mOLoader != null ? mOLoader.UWDataImporter : null;

        public UWLevel CurrentLevel => mOLoader != null ? mOLoader.CurrentLevel : null;

        public int CurrentLevelIndex => mOLoader != null ? mOLoader.CurrentLevelIndex : 0;

        public int LevelCount => mOLoader != null ? mOLoader.LevelCount : 0;

        public bool AlwaysFireCreateObjectTraps => mOLoader != null && mOLoader.AlwaysFireCreateObjectTraps;

        /// <summary>The crowding check of a creation trap: a creature created during play
        /// (UWObject.IsSpawned) within the area around the template's tile.</summary>
        public bool IsSpawnedCreatureNear(int piTileX, int piTileY, int piTilesBack, int piTilesForward)
        {
            UWCritter[] lOCritters = UWScene.FindCritters();

            if (lOCritters == null || mOLoader == null)
                return false;

            foreach (UWCritter lOCritter in lOCritters)
            {
                if (lOCritter == null)
                    continue;

                UWEntityInfo lOEntity = lOCritter.GetComponentInChildren<UWEntityInfo>();

                if (lOEntity == null || lOEntity.ObjectData == null || !lOEntity.ObjectData.IsSpawned)
                    continue;

                UWTilePos lOTile = mOLoader.WorldPositionToTile(lOCritter.transform.position);
                int liDx = lOTile.X - piTileX;
                int liDy = lOTile.Y - piTileY;

                if (liDx >= -piTilesBack && liDx <= piTilesForward && liDy >= -piTilesBack && liDy <= piTilesForward)
                    return true;
            }

            return false;
        }

        // ------------------------------------------------- Switches and levers

        /// <summary>Advances an eight-position lever by one position, if the used
        /// object is one - see UWHeightLever.</summary>
        public void AdvanceLever(UWObject pOSwitchObject)
        {
            if (mOLoader == null || !mOLoader.TryGetEntity(pOSwitchObject, out UWEntityInfo lOEntity))
                return;

            UWHeightLever lOLever = lOEntity.GetComponent<UWHeightLever>();

            if (lOLever != null)
                lOLever.Advance();
        }

        /// <summary>Original (confirmed by user): the switch itself changes its appearance on every
        /// successful use, in sync with the door opening/closing (see
        /// UWSwitchVisual).</summary>
        public void ToggleSwitchVisual(UWObject pOSwitchObject)
        {
            // Move triggers have no clicked object and therefore no visual either.
            if (pOSwitchObject == null || mOLoader == null)
                return;

            if (mOLoader.TryGetEntity(pOSwitchObject, out UWEntityInfo lOEntity))
                lOEntity.GetComponent<UWSwitchVisual>()?.Toggle();
        }

        // ------------------------------------------------- The world

        public void RebuildGeometry()
        {
            if (mOLoader != null)
                mOLoader.RebuildGeometry();
        }

        public void OnObjectHeightChanged(UWObject pOObject, int piOldZPos)
        {
            if (pOObject != null && mOLoader != null && mOLoader.TryGetEntity(pOObject, out UWEntityInfo lOEntity))
                lOEntity.transform.position += Vector3.up * (UWUnits.ZPosToWorld(pOObject.ZPos) - UWUnits.ZPosToWorld(piOldZPos));
        }

        public void MoveCreaturesWithFloor(int piTileX, int piTileY, int piOldZPos, int piNewZPos)
        {
            foreach (UWCritter lOCritter in UWCritter.Active)
            {
                UWCritterRecord lORecord = lOCritter != null ? lOCritter.Record : null;

                if (lORecord == null || lORecord.TileX != piTileX || lORecord.TileY != piTileY)
                    continue;

                bool lbMoves = piNewZPos > piOldZPos ? lORecord.ZPos < piNewZPos : lORecord.ZPos == piOldZPos;

                if (!lbMoves)
                    continue;

                int liWas = lORecord.ZPos;

                lORecord.ZPos = piNewZPos;
                lOCritter.transform.position += Vector3.up * (UWUnits.ZPosToWorld(piNewZPos) - UWUnits.ZPosToWorld(liWas));
            }
        }

        public void SpawnPendingObjectsInTile(int piTileX, int piTileY)
        {
            if (mOLoader != null)
                mOLoader.SpawnPendingObjectsInTile(piTileX, piTileY);
        }

        public bool SpawnObjectInTileAtTemplatePosition(UWObject pOCopy, int piTileX, int piTileY)
        {
            return mOLoader != null && mOLoader.SpawnObjectInTileAtTemplatePosition(pOCopy, piTileX, piTileY);
        }

        public bool SpawnObjectById(int piObjectId, int piTileX, int piTileY)
        {
            return mOLoader != null && mOLoader.SpawnObjectById(piObjectId, piTileX, piTileY);
        }

        public bool RemoveObjectFromWorld(UWObject pOObject)
        {
            return mOLoader != null && mOLoader.RemoveObjectFromWorld(pOObject);
        }

        /// <summary>The teleport trap's move: do_teleport's own search first on the same level
        /// (UWLevelLoader.TeleportTo, 2026-10-15).</summary>
        public void TravelTo(int piLevelIndex, int piTileX, int piTileY)
        {
            if (mOLoader != null)
                mOLoader.TeleportTo(piLevelIndex, piTileX, piTileY);
        }

        /// <summary>
        /// The door side of an a_door trap - the rule is UWDoorRules.DoorTrap (2026-10-12): on a
        /// door at rest the lock is replaced first, then the leaf acts by the trap's quality; on a
        /// door in motion the lock stays and only a swing against the trap's wish is turned round.
        ///
        /// THE LOCK IS REPLACED, NOT TOGGLED (DoorTrap_loc_8F8DD labels 943 to 9F5, read
        /// 2026-10-06): an a_lock on the door is taken off and freed, then a copy of the template
        /// the trap links to is put on - the same lock however often it fires. Until 2026-10-06 a
        /// lock was removed when there was one and the template applied when there was none,
        /// which a repeated close trap turned into locked, unlocked, locked.
        ///
        /// A DOOR IN MOTION: from 2026-10-06 to 2026-10-12 the trap skipped it altogether, after an
        /// IDA reading that saw only the door object branch - the byte-exact decompilation
        /// (UnderworldExhumed TRIGGER.C, TRAP_DOOR) has a second branch for the moving door 0x1CF.
        /// What the user saw on 2026-10-06 (the close trap before Drog's grate, level 1, 15/33,
        /// fired again while the grate came down and turned it round: down and up, over and over)
        /// was "close unless closed" on a grate that is not closed while it closes; the original's
        /// CloseDoor leaves a closing door alone (B10).
        /// </summary>
        public void FireDoorTrap(UWObject pODoor, int piAction, UWObject pOLockTemplate)
        {
            if (mOLoader == null || mOLoader.CurrentLevel == null
                || !mOLoader.TryGetEntity(pODoor, out UWEntityInfo lOEntity))
                return;

            UWDoor lODoor = lOEntity.GetComponentInParent<UWDoor>();

            if (lODoor == null)
                return;

            UWDoorRules.TrapOutcome lOOutcome = UWDoorRules.DoorTrap(piAction, lODoor.IsHeadingClosed, lODoor.IsMoving);

            if (lOOutcome.ReplaceLock)
                lODoor.ReplaceLock(pOLockTemplate);

            // OpenDoor(CharacterThatTriggeredTrap, door): whoever set the chain off opens it.
            if (lOOutcome.Toggle)
                lODoor.Toggle(mOTriggeringCritter != null ? UWTrapRules.Setter.Creature
                    : miTriggeringThing != 0 ? UWTrapRules.Setter.Thing
                    : UWTrapRules.Setter.Player);
        }

        // ------------------------------------------------- The player

        public bool HasPlayer => fPlayer != null;

        /// <summary>The tile under the eye, as the trespass search takes it.</summary>
        public UWTilePos PlayerTile
        {
            get
            {
                Camera lOCamera = Camera.main;

                return lOCamera != null && mOLoader != null
                    ? mOLoader.WorldPositionToTile(lOCamera.transform.position)
                    : new UWTilePos(-1, -1);
            }
        }

        public UWInventoryModel Inventory
        {
            get
            {
                UWInventory lOInventory = UWScene.Inventory;

                return lOInventory != null ? lOInventory.Model : null;
            }
        }

        public void DamagePlayer(int piDamage)
        {
            if (fPlayer != null)
                fPlayer.ApplyDamage(piDamage);
        }

        public void PoisonPlayer(int piStrength)
        {
            if (fPlayer != null)
                fPlayer.ApplyPoison(piStrength);
        }

        public int PlayerCastingSkill => fPlayer != null ? fPlayer.GetSkill(UWPlayerData.Skill.Casting) : 0;

        public int PlayerDamageTypeProof => fPlayer != null ? fPlayer.DamageTypeProof : 0;

        // ------------------------------------------------- Messages

        public bool HasMessages => mOInteraction != null;

        public void AddMessage(string psMessage)
        {
            if (mOInteraction != null)
                mOInteraction.AddMessage(psMessage);
        }

        public void AddGeneralMessage(int piIndex)
        {
            if (mOInteraction != null)
                mOInteraction.AddGeneralMessage(piIndex);
        }

        // ------------------------------------------------- Traps that are engine work

        public bool FireArrowTrap(UWObject pOTrap, int piTileX, int piTileY)
        {
            return UWTriggerSystem.fFireArrowTrap(pOTrap, piTileX, piTileY, mOLoader, mOInteraction);
        }

        /// <summary>See UWTrapRules.fFireSpellTrap for the two special classes.</summary>
        public bool FireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, int piMajorClass, int piMinorClass)
        {
            // The move trigger only knows the Interaction if it finds it on the touching collider.
            // When it was missing, the projectile stayed pending and the player got the
            // targeting cursor and could throw the fireball himself (per user, level 5, 14/45,
            // 2026-09-13).
            Interaction lOInteraction = mOInteraction != null
                ? mOInteraction : UWScene.Interaction;

            if (piMajorClass == UWTrapRules.CutsceneMajorClass)
            {
                UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

                if (lOPlayer == null)
                    return false;

                lOPlayer.PlayCutscene(piMinorClass);

                return true;
            }

            UWGameUI lOUi = UWScene.GameUi;

            if (lOUi == null || !lOUi.CastSpellByClass(piMajorClass, piMinorClass))
                return false;

            if (piMajorClass != UWTrapRules.ProjectileMajorClass || lOInteraction == null)
                return true;

            if (fLaunchTrapProjectile(pOTrap, piTileX, piTileY, lOInteraction))
                return true;

            lOInteraction.CancelPendingProjectile();

            return false;
        }

        /// <summary>Sends the pending projectile from the trap along the trap's heading - see
        /// UWTrapRules.SpellTrapMissileHeading. As with the arrow trap, the start point is the
        /// trigger's target tile with the trap's sub-tile position and height. The trap itself
        /// need not lie in a tile list, its TileX/TileY is not reliable.</summary>
        private static bool fLaunchTrapProjectile(UWObject pOTrap, int piTileX, int piTileY, Interaction pOInteraction)
        {
            return pOInteraction.LaunchPendingSpellFromSpot(piTileX, piTileY, pOTrap.XPos & 7, pOTrap.YPos & 7, pOTrap.ZPos,
                UWTrapRules.SpellTrapMissileHeading(pOTrap));
        }

        /// <summary>See UWTrapRules.fFireCameraTrap: the tile has been decided there.</summary>
        public bool FireCameraTrap(UWObject pOTrap, int piTileX, int piTileY)
        {
            Vector3 lOPosition = UWViewpoint.SubTileToWorld(piTileX, piTileY, pOTrap);

            // The vision is lit at level 6 (crystal_ball, PLAYER.C: set_light(6) before it;
            // UWLighting.BeginCrystalBallLight, 2026-10-15). When the vision ends the original
            // runs its status pass (FixPlayerEquips after cameras_fade), which gives the player
            // his own light back and where a worn curse strikes (2026-10-13).
            UWLighting lOLighting = UWScene.Lighting;

            if (lOLighting != null)
                lOLighting.BeginCrystalBallLight();

            return UWRemoteCamera.Ensure().Begin(UWRemoteCamera.TrapPriority,
                lOPosition, UWHeadings.EighthToDegrees(pOTrap.Heading), 0f, true,
                () =>
                {
                    if (lOLighting != null)
                        lOLighting.EndCrystalBallLight();

                    UWScene.Character?.OnStatusPassDue();
                }) > 0;
        }

        /// <summary>Trespassing: the search area is centred on the PLAYER (see
        /// UWTrapRules.fFireTrespassTrap for why it is lopsided).</summary>
        public int AngerRaceNearPlayer(int piOwner, int piTilesBack, int piTilesForward)
        {
            Camera lOCamera = Camera.main;

            if (lOCamera == null || mOLoader == null)
                return 0;

            Vector3 lOAt = lOCamera.transform.position;
            UWTilePos lOPlayer = mOLoader.WorldPositionToTile(lOAt);

            // TrespassTrap_ovr107_11B9 hands the routine the PLAYER object, so they look at the
            // player's spot in eighths and at his zpos plus the player object's height plus 12,
            // as for a stolen item (UWCritterRules.GetTheftSightZ).
            // The cell the point lies in, the original's (pos >> 5) & 7 - not the nearest stored
            // spot (UWTileQueries.WorldToEighthFloor, decision 21 of Docs/CLEANUP-PLAN.md).
            int liX8 = (lOPlayer.X << 3) + UWTileQueries.WorldToEighthFloor(lOAt.x);
            int liY8 = (lOPlayer.Y << 3) + UWTileQueries.WorldToEighthFloor(lOAt.z);
            int liZPos = Mathf.Clamp(UWUnits.WorldToZPos(mOLoader.GetFloorHeightAt(lOAt)), 0, 127);

            // The player object WHILE PLAYING is 127, "an adventurer", height 23 - on disk slot 1
            // holds 63 with height 0 (UWObjectMechanics.AdventurerObjectId).
            return fAngerRace(piOwner, lOPlayer, piTilesBack, piTilesForward, liX8, liY8,
                UWCritterRules.GetTheftSightZ(liZPos, fGetHeight(UWObjectMechanics.AdventurerObjectId, mOLoader)),
                mOLoader, mOInteraction);
        }

        public bool HasTriggeringCreature => mOTriggeringCritter != null;

        /// <summary>The rune of warding hits the creature; the message names the direction
        /// in which it happened, seen from the player.</summary>
        public void ApplyWardDamage(int piDamage, int piWardMessage, int piFirstDirectionMessage)
        {
            if (mOTriggeringCritter == null)
                return;

            // The ward hands its damage to whack_thing, which has no attacker: the creature does
            // not learn who laid the rune (UWCritter.ApplyDamageWithoutAttacker).
            mOTriggeringCritter.ApplyDamageWithoutAttacker(piDamage, UWDamageTypes.Physical);

            if (mOInteraction == null || fPlayer == null)
                return;

            Vector3 lOAway = mOTriggeringCritter.transform.position - fPlayer.transform.position;

            lOAway.y = 0f;

            mOInteraction.AddMessage(mOInteraction.GetGeneralMessage(piWardMessage)
                + mOInteraction.GetGeneralMessage(
                    piFirstDirectionMessage + UWLevelLoader.HeadingFromDirection(lOAway)));
        }

        public bool BeginConversation(int piSlot)
        {
            return mOInteraction != null && mOInteraction.BeginConversation(piSlot);
        }

        public bool TalkTo(UWObject pONpcData)
        {
            return mOInteraction != null && mOInteraction.TryTalkToNpcData(pONpcData);
        }

        public bool PlayVictory()
        {
            return UWEndgame.PlayVictory();
        }

        /// <summary>The class 9 curse of the exploding book - the same arithmetic the worn
        /// curses use, three dice here.</summary>
        public void CursePlayer(int piDice)
        {
            UWCharacter lOCharacter = UWScene.Character;

            if (lOCharacter != null)
                lOCharacter.ApplyCurse(piDice);
        }

        /// <summary>Cutscene 3 for the freeing of Arial.</summary>
        public void PlayCutscene(int piCutscene)
        {
            UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

            if (lOPlayer != null)
                lOPlayer.PlayCutscene(piCutscene);
        }

        /// <summary>The quake trap's first bit: the screen shakes on the large channel for 0x1E,
        /// whatever the owner field says - DoTrapScreenShake_seg008_DD6 takes no argument
        /// (read 2026-09-25; until then the owner was the length).</summary>
        public void ShakeScreen(int piIntensity)
        {
            UWScreenShake lOShake = UWScreenShake.Ensure();

            if (lOShake != null)
                lOShake.Shake(UWScreenShake.LargeChannel, QuakeShakeDuration);
        }

        private const int QuakeShakeDuration = 0x1E;

        /// <summary>The quake trap's second bit: the player is thrown up
        /// (BouncePlayer_seg008_D9E).</summary>
        public void BouncePlayer(int piIntensity)
        {
            // The movement sits on the character's parent (until 2026-10-13 never found here, so
            // the quake never threw the player up).
            UWPlayerMovement lOMovement = UWScene.Character != null
                ? UWScene.Character.GetComponentInParent<UWPlayerMovement>()
                : null;

            if (lOMovement != null)
                lOMovement.BounceUp(piIntensity);
        }
    }

    // ------------------------------------------------- The arrow trap

    /// <summary>
    /// an_arrow trap: fires a projectile over the trigger's target tile.
    ///
    /// The ammunition is stored in two fields of the trap (see
    /// UWObjectMechanics.GetArrowTrapAmmunitionId), the DIRECTION in its heading - eight steps
    /// of 45 degrees each, plus two 256ths (seg025_73C: HeadingBase 0, MissileHeading 2), the
    /// pitch two steps up, the speed the trap's fixed 0x14 for every ammunition. A launcher of
    /// height 0: the missile starts at the trap's own spot, no offset, no placement test - the
    /// user sees the level 8 bolt come out of the southern wall (2026-09-07). On the motion
    /// core since 2026-10-05 (UWProjectileWorld.LaunchFromSpot): it falls, bounces and lies.
    ///
    /// THE DAMAGE is the original's, boulder, skull and bone included (checked 2026-09-24,
    /// Todo.md section 0b row 9): the missile's hit (seg029_29EE_AD) reads the base damage from
    /// the ranged table at the LOW FOUR BITS of the object id, whatever the object is - exactly
    /// what GetRangedDamage does; the Missile skill counts only when the launcher is the
    /// player, never for a trap.
    /// </summary>
    private static bool fFireArrowTrap(UWObject pOTrap, int piTileX, int piTileY,
        UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        if (pOLevelLoader == null || pOLevelLoader.UWDataImporter == null
            || pOLevelLoader.UWDataImporter.ObjectProperties == null)
            return false;

        int liAmmunition = UWObjectMechanics.GetArrowTrapAmmunitionId(pOTrap);

        if (liAmmunition <= 0)
            return false;

        int liDamage = UWCombat.ComputeDamage(
            pOLevelLoader.UWDataImporter.ObjectProperties.GetRangedDamage(liAmmunition),
            false, 0, 0, UWCombat.NeutralCharge, 0);

        int liHeading = (((pOTrap.Heading & 7) << 5) + TrapHeadingOffset) & 0xFF;

        UWProjectileFlight lOFlight = UWProjectileWorld.Ensure(pOLevelLoader).LaunchFromSpot(liAmmunition, piTileX, piTileY,
            pOTrap.XPos, pOTrap.YPos, pOTrap.ZPos, liHeading, TrapPitch, TrapSpeedByte, liDamage, UWDamageTypes.Missile, -1,
            pOInteraction);

        if (lOFlight == null)
            return false;

        // The start point belongs in the log too: on short flight paths you otherwise see too
        // little of the bolt to judge whether it is placed correctly (per user,
        // 2026-09-07: "I could not tell where our bolt spawns.").
        UWTrapLog.Detail = string.Format("-> {0} ({1} damage) direction {2}, start {3}/{4} xpos {5} ypos {6}, height {7} above floor {8}",
            UWTrapLog.GetName(pOLevelLoader.UWDataImporter, liAmmunition), liDamage, pOTrap.Heading & 7,
            piTileX, piTileY, pOTrap.XPos, pOTrap.YPos,
            Mathf.RoundToInt((pOTrap.ZPos * UWWorldScale.ZPosStep) - fGetFloorHeight(pOLevelLoader, piTileX, piTileY)),
            Mathf.RoundToInt(fGetFloorHeight(pOLevelLoader, piTileX, piTileY)));

        return true;
    }

    /// <summary>Floor height of a tile in world units, or zero if the tile
    /// does not exist - only for the trap log.</summary>
    private static float fGetFloorHeight(UWLevelLoader pOLevelLoader, int piTileX, int piTileY)
    {
        UWTile lOTile = pOLevelLoader.CurrentLevel != null
            ? pOLevelLoader.CurrentLevel.GetTile(piTileX, piTileY) : null;

        return lOTile != null ? lOTile.FloorHeight : 0f;
    }

    /// <summary>What an_arrow_trap hands the launch (seg025_73C): the heading offset of two
    /// 256ths, the pitch of two steps up and the ammunition type 0x14, which is the speed byte.</summary>
    private const int TrapHeadingOffset = 2;

    private const int TrapPitch = 2;

    private const int TrapSpeedByte = 0x14;
}
