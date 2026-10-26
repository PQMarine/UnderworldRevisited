using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// What is left of a slain creature.
///
/// The critter table in OBJECTS.DAT has its own field for this (BloodAndRemains): a
/// rat leaves a pool of blood, a skeleton bones, a golem rubble, a red worm
/// its carcass. Which object belongs to which code is in
/// UWObjectMechanics.GetRemainsObjectId.
///
/// The sequence: the creature falls (death animation, see UWCritterAnimator), and once that
/// has played through, it disappears and the remains lie in its place. They are
/// an ordinary world object - lookable, and depending on the kind also pickable.
///
/// NO WAITING HERE ANY MORE (cleanup step 4.12, 2026-10-19; B22): the death is one state machine
/// on the creature's actor (UWCreatureActor, section "Death"), which calls CompleteDeath once at
/// the brain's frame 3, on the creature's own updates. Until then this component listened for the
/// body's Destroyed, polled the animator every frame for the end of the death pictures and, three
/// seconds of real time after the death, removed the creature itself - with remains and loot but
/// without the special death, so a creature dying far from the player (the brain's distance cull)
/// or under Freeze Time lost its story consequences (Tybal, Garamon, the quest flags).
/// </summary>
[RequireComponent(typeof(UWDamageable))]
public class UWCritterRemains : MonoBehaviour
{
    private UWLevelLoader mOLevelLoader;

    private int miRemainsObjectId = -1;

    public void Initialise(UWObjectClassProperties.Critter pOStats, UWLevelLoader pOLevelLoader)
    {
        miRemainsObjectId = UWCritterCombat.GetBloodObjectId(pOStats.BloodIndex);
        mOLevelLoader = pOLevelLoader;
        mOStats = pOStats;
        mbHasStats = true;
    }

    private UWObjectClassProperties.Critter mOStats;

    private bool mbHasStats;

    /// <summary>
    /// Frame 3 of the death animation reached in the brain (UWCreatureActor's OnDeathFinished, through
    /// UWCritter.CompleteDeath): the remains, the loot, and the body goes - at the original's moment,
    /// once, from the actor's death machine.
    ///
    /// THE DATA STAYS WHILE IT FALLS (critter_ai, UnderworldExhumed AI.C: the dying critter,
    /// sequence 0x0C, stays in its tile list until frame 3, where it is taken out and the
    /// loot, the remains and the spill come). A save or a level change in between keeps it
    /// with 0 hit points and the dying sequence, and the death finishes after loading or on
    /// re-entry. Until 2026-10-15 ours dropped the data at the start of the death, so such a save
    /// lost the creature with its loot and remains (audit round 2, A25 / KR N2). It goes here.
    ///
    /// The death sound is the brain's since 2026-09-20 (Death_seg007_1798_35CB plays sound 6
    /// for kinds with table byte 8 &amp; 7 == 1, through ICritterHost.PlaySound).
    ///
    /// A DROWNED creature takes this path like any other (deviation 43, built 2026-09-20):
    /// the death branch at animation 0x0C frame 3 calls DropNPCLoot, DropNPCRemains and
    /// SpillCritterInventory whatever killed the creature (asm lines 53055-53080). That it
    /// leaves nothing in deep water is the WATER's doing, not the death code's - every object
    /// that comes to rest runs through UWLevelLoader.fSpawnObjectAt, which destroys what sinks
    /// on a water or lava tile, and blood (221), bones (198) and meat all sink. The user
    /// measured all three cases in the original on 2026-09-20: drowned in the middle of the
    /// water nothing is left at all, drowned close to the shore a piece of meat stayed lying
    /// there, and loot of a creature killed at the edge that falls in vanishes with a splash.
    ///
    /// IN THE ORIGINAL'S ORDER since cleanup step 4.12b (2026-10-19, the user's decision "as the
    /// original"): critter_ai's dying branch (UnderworldExhumed critter/AI.C:1140-1144) takes the
    /// creature out of its tile list, then generate_inventory rolls the loot INTO its chain,
    /// build_corpse leaves the blood and rolls and places the corpse, and ONE drop_some_objects
    /// scatters the chain - loot and carried items together, the loot first (it was added at the
    /// head). Until then ours left the blood and the corpse first, scattered the carried items,
    /// and rolled and scattered the loot after them on its own - other random draws in another
    /// order.
    /// </summary>
    public void CompleteDeath()
    {
        if (this == null || gameObject == null)
            return;

        fForgetData();
        fGenerateInventory();
        fLeaveRemains();
        fDropSomeObjects();

        Destroy(gameObject);
    }

    /// <summary>The creature leaves the level data at the end of its death (Obj_Rem at frame 3),
    /// its mobile slot back.</summary>
    private void fForgetData()
    {
        UWEntityInfo lOEntity = GetComponent<UWEntityInfo>();

        if (mOLevelLoader != null && lOEntity != null)
            mOLevelLoader.ForgetObjectData(lOEntity.ObjectData);
    }

    /// <summary>
    /// DropNPCRemains_seg006_5 (40801): the blood object 0xD8 + n inserted right at the
    /// creature's own position when its table says so, then the corpse 0xC0 + n with 7 in 16,
    /// put down AROUND the creature with a radius of 4 eighths (PlaceObjectAtNPC; read whole
    /// 2026-09-25 - until then the corpse lay on the body as well, see UWScatterRules). The
    /// corpse's owner carries the kind (item id &amp; 0x3F) - see UWCritterCombat.GetCorpseOwner.
    /// The corpse was missing until 2026-09-20 (deviation 51).
    ///
    /// build_corpse (UnderworldExhumed critter/PATHFIND.C:146-167): after the loot is rolled and
    /// before the chain is spilled (AI.C:1142, since cleanup step 4.12b, 2026-10-19), so the
    /// corpse roll and the corpse's spot come after the loot's draws and before the spill's.
    /// </summary>
    private void fLeaveRemains()
    {
        if (mOLevelLoader == null)
            return;

        Vector3 lOBody = fGetBodyPosition();
        UWTilePos lOTile = fGetDeathTile();
        Vector3 lOAt = new Vector3(lOBody.x, 0f, lOBody.z);

        // build_corpse Obj_Adds the blood: at the head of the tile's list (2026-10-18); the corpse
        // goes through near_mob_put_at (SpawnScatteredAround's order).
        if (miRemainsObjectId >= 0)
            mOLevelLoader.SpawnObjectById(miRemainsObjectId, lOTile.X, lOTile.Y, 0, -1, lOAt,
                UWWorldEdit.Arrival.Created);

        if (!mbHasStats || !UWCritterCombat.ShouldDropCorpse(mOStats.CorpseIndex, UWRandom.Next(UWRules.Current.Critter.Corpse.Roll)))
            return;

        UWEntityInfo lOEntity = GetComponent<UWEntityInfo>();
        int liKind = lOEntity != null && lOEntity.ObjectData != null ? lOEntity.ObjectData.ID : 0;

        mOLevelLoader.SpawnObjectByIdAround(UWCritterCombat.GetCorpseObjectId(mOStats.CorpseIndex), 0,
            -1, lOBody, UWScatterRules.RemainsRadius, lOTile, UWCritterCombat.GetCorpseOwner(liKind));
    }

    /// <summary>
    /// generate_inventory (UnderworldExhumed obj/TREASURE.C:202-216, called at AI.C:1141): the loot
    /// - money, food, equipment, odds and ends, computed by UWCritterLoot from the critter table -
    /// rolled INTO the creature's chain, each piece at its head (UWCritterLoot.AddToChain), before
    /// the remains; fDropSomeObjects spills it with the rest.
    ///
    /// TIMING: the original already rolls the loot when someone looks inside - when
    /// trading -, and remembers that it was generated. Trading does the same here
    /// (UWConversationTrade, NPCLootSpawned); otherwise the loot is only rolled on death.
    /// If it has already traded, its loot has long been rolled into it; then it is not rolled
    /// a second time.
    ///
    /// The pieces are the objects SpawnObjectByIdAround made until cleanup step 4.12b
    /// (UWLevelLoader.BuildObject); their slots are reserved when they are put down, as before.
    /// </summary>
    private void fGenerateInventory()
    {
        UWNpc lONpc = fGetNpcWithContents();

        if (lONpc == null || lONpc.NPCLootSpawned)
            return;

        lONpc.NPCLootSpawned = true;

        System.Collections.Generic.List<UWCritterLoot.Drop> lODrops = UWCritterLoot.Generate(
            mOStats, mOLevelLoader.UWDataImporter.CommonObjectProperties,
            mOLevelLoader.CurrentLevel.LevelNumber, mOLevelLoader.UWDataImporter.ObjectProperties);
        System.Collections.Generic.List<UWObject> lOLoot = new System.Collections.Generic.List<UWObject>();

        foreach (UWCritterLoot.Drop lODrop in lODrops)
            lOLoot.Add(mOLevelLoader.BuildObject(lODrop.ObjectId, lODrop.Quantity, lODrop.Quality));

        UWCritterLoot.AddToChain(lONpc.Contents, lOLoot);
    }

    /// <summary>
    /// drop_some_objects (TREASURE.C:83-86, called at AI.C:1143): the whole chain - the loot rolled
    /// in at its head and what the creature carried - goes down around its body in ONE pass
    /// (drop_link_chain's put_at, see UWScatterRules).
    /// </summary>
    private void fDropSomeObjects()
    {
        UWNpc lONpc = fGetNpcWithContents();

        if (lONpc == null)
            return;

        // Scattered around the body as in the original, not all in one spot (per user,
        // 2026-09-13) - within 6 eighths, which can reach the neighbouring tile
        // (drop_some_objects -> drop_link_chain, read 2026-09-25; see UWScatterRules). A
        // creature keeps nothing back, not even a lock or trigger in its pack
        // (UWSpillRules, corpse); the scatter is the same call a container's spill uses
        // (UWSpill, cleanup 2.6, 2026-10-12).
        UWSpill.Apply(mOLevelLoader, lONpc, UWSpillRules.Partition(lONpc.Contents),
            fGetBodyPosition(), fGetDeathTile(), false);
    }

    /// <summary>The creature's object data with its chain loaded, when it has a table row and the
    /// level is there; null otherwise, and then nothing is rolled or spilled. (A creature without
    /// object data rolled its loot on its own until cleanup step 4.12b; the original's
    /// generate_inventory rolls into the critter, and the spawner gives every creature its UWNpc.)</summary>
    private UWNpc fGetNpcWithContents()
    {
        if (!mbHasStats || mOLevelLoader == null || mOLevelLoader.UWDataImporter == null
            || mOLevelLoader.CurrentLevel == null)
            return null;

        UWEntityInfo lOEntity = GetComponent<UWEntityInfo>();
        UWNpc lONpc = lOEntity != null ? lOEntity.ObjectData as UWNpc : null;

        if (lONpc != null)
            lONpc.EnsureContentsLoaded(mOLevelLoader.CurrentLevel.Masterlist);

        return lONpc;
    }

    /// <summary>
    /// WHERE THE CREATURE DIED: its logical body (UWCritter.BodyPosition), not the picture.
    /// The picture trails the body by up to a step, and near a wall it could stand over the
    /// wall's tile - every item spawned there was refused as "in a solid tile" and silently
    /// lost; Tybal kept both his keys that way (per user, 2026-09-24, killed next to a wall).
    /// The original spills into the creature's own tile, which is always open.
    /// </summary>
    private Vector3 fGetBodyPosition()
    {
        UWCritter lOCritter = GetComponent<UWCritter>();

        return lOCritter != null ? lOCritter.BodyPosition : transform.position;
    }

    /// <summary>The tile of the body - and if that should ever be solid after all, the nearest
    /// open neighbour, so that nothing a creature carries can be lost on its death.</summary>
    private UWTilePos fGetDeathTile()
    {
        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(fGetBodyPosition());

        if (fIsOpen(lOTile.X, lOTile.Y))
            return lOTile;

        for (int liRing = 1; liRing <= 2; liRing++)
        {
            for (int liDy = -liRing; liDy <= liRing; liDy++)
            {
                for (int liDx = -liRing; liDx <= liRing; liDx++)
                {
                    if (fIsOpen(lOTile.X + liDx, lOTile.Y + liDy))
                        return new UWTilePos(lOTile.X + liDx, lOTile.Y + liDy);
                }
            }
        }

        return lOTile;
    }

    private bool fIsOpen(int piTileX, int piTileY)
    {
        UWTile lOTile = mOLevelLoader.CurrentLevel != null ? mOLevelLoader.CurrentLevel.GetTile(piTileX, piTileY) : null;

        return lOTile != null && lOTile.TileType != UWTile.TileTypeEnum.solid;
    }
}
