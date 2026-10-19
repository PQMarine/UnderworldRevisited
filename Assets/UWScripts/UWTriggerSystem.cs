using System.Collections.Generic;
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
    /// UWMotionHost.TriggerMove). Damage and teleport go to the creature; a damage trap
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
    /// periodic respawn (see RespawnCreatures).</summary>
    public static bool FireTrapInTile(UWObject pOTrap, int piTileX, int piTileY, UWLevelLoader pOLevelLoader)
    {
        return UWTrapRules.FireTrapInTile(pOTrap, piTileX, piTileY, new UWTrapHost(pOLevelLoader, null));
    }

    /// <summary>
    /// The periodic respawning of monsters on the Unity side. The rule - which creation traps
    /// respawn, only those out of the player's way, the one-in-four roll - is engine-free in
    /// UWRespawnRules (P3 of the engine separation, 2026-09-18; after the original since
    /// 2026-09-23). The crowding check belongs to the trap itself (UWTrapRules, the host's
    /// IsSpawnedCreatureNear).
    ///
    /// NO TIMER OF ITS OWN SINCE 2026-10-15: the roll sits in the player's five-minute block
    /// (UWCharacter.OnFiveMinuteBlock, duration_check in the original), which runs on across
    /// levels; until then UWCreatureRespawner counted 504 real seconds from the level's start (audit
    /// round 2, A26 / KR N4). The night calls it as well (UWSleep).
    ///
    /// One pass: from the five-minute block when its roll came up, with the distance test, and
    /// from the night (UWSleep), where the original calls the same routine with 0, which switches
    /// the distance test off - every respawn trap on the level fires (Sleep_ovr143_D1F).
    ///
    /// NO COMPONENT SINCE 2026-10-17 (cleanup step 3.4): this was UWCreatureRespawner.Run, a
    /// MonoBehaviour the level loader added only to hold it. The respawn stays INSIDE the
    /// five-minute block rather than on UWWorldScheduler.EveryPlayerTicks(24): the block's order
    /// (the hunger roll before the respawn roll, the checkup and the natural healing after it) and
    /// its tick counter are the original's, and a separate 24-tick count would draw in another
    /// order and run out of phase with it.
    /// </summary>
    /// <returns>How many traps were fired.</returns>
    public static int RespawnCreatures(UWLevelLoader pOLevelLoader, bool pbAnyDistance)
    {
        if (pOLevelLoader == null || pOLevelLoader.CurrentLevel == null || pOLevelLoader.CurrentLevel.TileData == null)
            return 0;

        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter == null)
            return 0;

        UWTilePos lOPlayerTile = pOLevelLoader.WorldPositionToTile(lOCharacter.transform.position);

        return UWRespawnRules.Run(pOLevelLoader.CurrentLevel, lOPlayerTile, pbAnyDistance,
            (pOTrap, piTileX, piTileY) => FireTrapInTile(pOTrap, piTileX, piTileY, pOLevelLoader));
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

        // The creatures of the area through the world (cleanup step 3.2, 2026-10-17; before, a scene
        // search here): every one with its object whose BODY's tile lies within piTilesBack below
        // and piTilesForward above the centre on both axes - the transform's tile, as this search
        // always measured it, not the record's.
        List<UWCritter> lOCritters = new List<UWCritter>();

        UWWorld.Of(pOLevelLoader).FindCritters(
            UWTileArea.Around(pOCentreTile, piTilesBack, piTilesForward, UWCreatureTile.Body),
            UWCreatureFilter.HasObject, lOCritters);

        int liAngered = 0;

        for (int liAt = 0; liAt < lOCritters.Count; liAt++)
        {
            UWCritter lOCritter = lOCritters[liAt];

            // Its object, as the search found it (UWWorld.RefOf: the entity on its own object).
            UWEntityInfo lOInfo = lOCritter.GetComponent<UWEntityInfo>();

            if (!UWCritterRules.MindsTheft(piOwner, fGetRace(lOInfo.ObjectData.ID, pOLevelLoader),
                    lOCritter.IsAttitudeLocked, liKnightQuest))
                continue;

            // Only whoever actually SEES it gets angry - see UWCritter.CanSeeTheftAt. The
            // area of the search is just the coarse pre-selection, as in the reference.
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

    // ------------------------------------------------- The host of one firing

    /// <summary>
    /// What one firing of the trap rules sees of the game: the world with this firing's message
    /// channel (null for a silent one, such as the respawn), who set it off and, for the rune of
    /// warding and the damage trap, the creature or thing that did.
    ///
    /// The level, the travel, the player's tile, the messages, the cutscenes and the shake went to
    /// the world (UWWorld) and its presentation in cleanup step 3.1 (2026-10-17); since step 3.6
    /// (the same day) the rules ask the world for them themselves, together with the player's
    /// numbers, the objects, the crowding check and the door of a door trap, and this host keeps
    /// what belongs to the firing (37 -> 19 members). World is the world with the firing's
    /// presentation: the silent scope when there is no channel.
    ///
    /// PER FIRING: it carries the channel and the one who set it off; the long-lived world keeps
    /// none of it.
    /// </summary>
    private sealed class UWTrapHost : IUWTrapHost
    {
        private readonly UWLevelLoader mOLoader;

        private readonly Interaction mOInteraction;

        /// <summary>The world with this firing's presentation (its lines go to mOInteraction, or
        /// nowhere for a silent firing - UWSilentPresentation); null without a loader.</summary>
        private readonly IUWWorld mOWorld;

        private readonly UWCritter mOTriggeringCritter;

        private readonly int miTriggeringThing;

        public UWTrapHost(UWLevelLoader pOLoader, Interaction pOInteraction, UWCritter pOTriggeringCritter = null,
            int piTriggeringThing = 0)
        {
            mOLoader = pOLoader;
            mOInteraction = pOInteraction;
            mOTriggeringCritter = pOTriggeringCritter;
            miTriggeringThing = piTriggeringThing;

            UWWorld lOWorld = UWWorld.Of(pOLoader);

            mOWorld = UWScopedWorld.With(lOWorld, UWPresentation.For(lOWorld, pOInteraction));
        }

        public IUWWorld World => mOWorld;

        public UWTrapFiring Firing => new UWTrapFiring(mOTriggeringCritter != null, miTriggeringThing != 0);

        public bool AlwaysFireCreateObjectTraps => mOLoader != null && mOLoader.AlwaysFireCreateObjectTraps;

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

        // ------------------------------------------------- The terrain change

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

        // ------------------------------------------------- The one who set it off

        /// <summary>A damage trap set off by a thing in flight: destroyed if the hit wears it out,
        /// untouched otherwise (UWMissiles.StrikeThing, through IUWWorld.Damage since cleanup step
        /// 3.3, 2026-10-17).</summary>
        public void StrikeTriggeringThing(int piDamage)
        {
            if (miTriggeringThing == 0 || mOWorld == null)
                return;

            mOWorld.Damage(UWDamageTarget.InFlight(miTriggeringThing), UWBlow.Trap(piDamage, UWDamageTypes.Physical));
        }

        /// <summary>A damage trap set off by a creature: the quality on its hit points
        /// (DamageTrap_ovr107_CB2 -> DamageObject, type 4).</summary>
        public void DamageTriggeringCreature(int piDamage)
        {
            // whack_thing: damage_item with no attacker (UWBlow.Trap).
            if (mOTriggeringCritter != null)
                fDamage(UWWorld.TargetOf(mOTriggeringCritter, mOTriggeringCritter.Damageable),
                    UWBlow.Trap(piDamage, UWDamageTypes.Physical));
        }

        /// <summary>A blow of this firing through the world (cleanup step 3.3, 2026-10-17).</summary>
        private void fDamage(UWDamageTarget pOTarget, UWBlow pOBlow)
        {
            if (mOWorld != null)
                mOWorld.Damage(pOTarget, pOBlow);
        }

        /// <summary>The rune of warding hits the creature; the message names the direction
        /// in which it happened, seen from the player.</summary>
        public void ApplyWardDamage(int piDamage, int piWardMessage, int piFirstDirectionMessage)
        {
            if (mOTriggeringCritter == null)
                return;

            // The ward hands its damage to whack_thing, which has no attacker: the creature does
            // not learn who laid the rune (UWBlow.Ward).
            fDamage(UWWorld.TargetOf(mOTriggeringCritter, mOTriggeringCritter.Damageable), UWBlow.Ward(piDamage));

            UWCharacter lOPlayer = UWScene.Character;
            IUWPresentation lOShown = mOWorld != null ? mOWorld.Presentation : null;

            if (lOShown == null || !lOShown.ShowsMessages || lOPlayer == null)
                return;

            Vector3 lOAway = mOTriggeringCritter.transform.position - lOPlayer.transform.position;

            lOAway.y = 0f;

            lOShown.AddMessage(lOShown.GetGeneralMessage(piWardMessage)
                + lOShown.GetGeneralMessage(piFirstDirectionMessage + UWLevelLoader.HeadingFromDirection(lOAway)));
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

            // False when no cutscene player is there to show it.
            if (piMajorClass == UWTrapRules.CutsceneMajorClass)
                return mOWorld != null && mOWorld.Presentation.PlayCutscene(piMinorClass);

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
            // TrespassTrap_ovr107_11B9 hands the routine the PLAYER object, so they look at the
            // player's spot in eighths and at his zpos plus the player object's height plus 12,
            // as for a stolen item (UWCritterRules.GetTheftSightZ).
            // The cell the point lies in, the original's (pos >> 5) & 7 - not the nearest stored
            // spot (UWTileQueries.WorldToEighthFloor, decision 21 of Docs/CLEANUP-PLAN.md); the zpos
            // is the floor's under the eye, clamped. That is the avatar's FloorSpot since cleanup
            // step 3.1 (2026-10-17), Nowhere without a view, as this answered 0 without a camera.
            UWSpot lOPlayer = mOWorld != null ? mOWorld.Player.FloorSpot : UWSpot.Nowhere;

            if (!lOPlayer.IsOnMap)
                return 0;

            // The player object WHILE PLAYING is 127, "an adventurer", height 23 - on disk slot 1
            // holds 63 with height 0 (UWObjectMechanics.AdventurerObjectId).
            return fAngerRace(piOwner, lOPlayer.Tile, piTilesBack, piTilesForward, lOPlayer.EighthsX, lOPlayer.EighthsY,
                UWCritterRules.GetTheftSightZ(lOPlayer.ZPos, fGetHeight(UWObjectMechanics.AdventurerObjectId, mOLoader)),
                mOLoader, mOInteraction);
        }

        // ------------------------------------------------- Through this firing's channel

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
    /// core since 2026-10-05 (UWMissiles.LaunchFromSpot): it falls, bounces and lies.
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

        // The base: its dice are rolled at the hit (UWAttackResolution.Missile, missile_thwack ->
        // do_damage; since cleanup step 4.9b, 2026-10-18, rolled here until then).
        int liDamage = pOLevelLoader.UWDataImporter.ObjectProperties.GetRangedDamage(liAmmunition);

        int liHeading = (((pOTrap.Heading & 7) << 5) + TrapHeadingOffset) & 0xFF;

        UWProjectileFlight lOFlight = UWMissiles.Ensure(pOLevelLoader).LaunchFromSpot(liAmmunition, piTileX, piTileY,
            pOTrap.XPos, pOTrap.YPos, pOTrap.ZPos, liHeading, TrapPitch, TrapSpeedByte, liDamage, UWDamageTypes.Missile, -1);

        if (lOFlight == null)
            return false;

        // The start point belongs in the log too: on short flight paths you otherwise see too
        // little of the bolt to judge whether it is placed correctly (per user,
        // 2026-09-07: "I could not tell where our bolt spawns.").
        UWTrapLog.Detail = string.Format("-> {0} ({1} base damage) direction {2}, start {3}/{4} xpos {5} ypos {6}, height {7} above floor {8}",
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
