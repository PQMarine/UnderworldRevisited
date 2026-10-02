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
/// </summary>
[RequireComponent(typeof(UWDamageable))]
public class UWCritterRemains : MonoBehaviour
{
    private UWDamageable mODamageable;
    private UWCritterAnimator mOAnimator;
    private UWLevelLoader mOLevelLoader;

    private int miRemainsObjectId = -1;
    private bool mbDying;
    private float mfRemoveAt;

    /// <summary>How long the body stays if there is no death animation -
    /// then its end cannot be waited for. Also the upper limit when there is one
    /// (see fOnDestroyed). Longer than the brain's four death updates of a quarter second
    /// (plus the slot it waits for), so that CompleteDeath at the original's frame 3 is the
    /// normal end and this only catches a creature without a brain.</summary>
    private const float FallbackDeathSeconds = 3f;

    public void Initialise(UWObjectClassProperties.Critter pOStats, UWLevelLoader pOLevelLoader)
    {
        miRemainsObjectId = UWCritterCombat.GetBloodObjectId(pOStats.BloodIndex);
        mOLevelLoader = pOLevelLoader;
        mOStats = pOStats;
        mbHasStats = true;
    }

    private UWObjectClassProperties.Critter mOStats;

    private bool mbHasStats;

    private void Awake()
    {
        mODamageable = GetComponent<UWDamageable>();
        mOAnimator = GetComponent<UWCritterAnimator>();

        mODamageable.Destroyed += fOnDestroyed;
    }

    private void OnDestroy()
    {
        if (mODamageable != null)
            mODamageable.Destroyed -= fOnDestroyed;
    }

    private void Update()
    {
        if (!mbDying)
            return;

        // Wait for the end of the death animation, so the creature does not vanish in the
        // middle of falling. If there is none, the fallback time applies.
        bool lbFinished = mOAnimator != null ? mOAnimator.DeathFinished : Time.time >= mfRemoveAt;

        if (!lbFinished && Time.time < mfRemoveAt)
            return;

        mbDying = false;

        fLeaveRemains();
        fDropLoot();

        Destroy(gameObject);
    }

    private void fOnDestroyed(UWDamageable pODamageable)
    {
        mbDying = true;

        // In the data it is gone from now on, even while it is still falling - otherwise it
        // would stand there again when the level is re-entered and in the save game.
        UWEntityInfo lOEntity = GetComponent<UWEntityInfo>();

        if (mOLevelLoader != null && lOEntity != null)
            mOLevelLoader.ForgetObjectData(lOEntity.ObjectData);

        // The death sound is the brain's since 2026-09-20 (Death_seg007_1798_35CB plays sound 6
        // for kinds with table byte 8 & 7 == 1, through ICritterHost.PlaySound).

        // Upper limit even if there is an animation - a creature without death frames
        // would otherwise stay standing forever.
        mfRemoveAt = Time.time + FallbackDeathSeconds;
    }

    /// <summary>
    /// Frame 3 of the death animation reached in the brain (UWCritter.OnDeathFinished):
    /// the remains, the loot, and the body goes - the same as the end of the wait in Update,
    /// only at the original's moment.
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
    /// </summary>
    public void CompleteDeath()
    {
        if (this == null || gameObject == null)
            return;

        mbDying = false;

        UWEntityInfo lOEntity = GetComponent<UWEntityInfo>();

        if (mOLevelLoader != null && lOEntity != null)
            mOLevelLoader.ForgetObjectData(lOEntity.ObjectData);

        fLeaveRemains();
        fDropLoot();

        Destroy(gameObject);
    }

    /// <summary>
    /// DropNPCRemains_seg006_5 (40801): the blood object 0xD8 + n inserted right at the
    /// creature's own position when its table says so, then the corpse 0xC0 + n with 7 in 16,
    /// put down AROUND the creature with a radius of 4 eighths (PlaceObjectAtNPC; read whole
    /// 2026-09-25 - until then the corpse lay on the body as well, see UWScatterRules). The
    /// corpse's quality carries the kind (item id &amp; 0x3F), which is how "a_dead X" names it.
    /// The corpse was missing until 2026-09-20 (deviation 51).
    /// </summary>
    private void fLeaveRemains()
    {
        if (mOLevelLoader == null)
            return;

        Vector3 lOBody = fGetBodyPosition();
        UWTilePos lOTile = fGetDeathTile();
        Vector3 lOAt = new Vector3(lOBody.x, 0f, lOBody.z);

        if (miRemainsObjectId >= 0)
            mOLevelLoader.SpawnObjectById(miRemainsObjectId, lOTile.X, lOTile.Y, 0, -1, lOAt);

        if (!mbHasStats || !UWCritterCombat.ShouldDropCorpse(mOStats.CorpseIndex, UWRandom.Next(UWCritterRules.CorpseRoll)))
            return;

        UWEntityInfo lOEntity = GetComponent<UWEntityInfo>();
        int liKind = lOEntity != null && lOEntity.ObjectData != null ? lOEntity.ObjectData.ID : 0;

        mOLevelLoader.SpawnObjectByIdAround(UWCritterCombat.GetCorpseObjectId(mOStats.CorpseIndex), 0,
            UWCritterCombat.GetCorpseQuality(liKind), lOBody, UWScatterRules.RemainsRadius, lOTile);
    }

    /// <summary>
    /// What the creature carried drops onto the same tile, each piece at a
    /// random spot within it (UWLevelLoader.GetRandomPositionInTile) - money, food,
    /// equipment, odds and ends. What exactly is computed by UWCritterLoot from the critter
    /// table.
    ///
    /// TIMING: the original already rolls the loot when someone looks inside - when
    /// trading -, and remembers that it was generated. Trading does the same here
    /// (UWConversationTrade, NPCLootSpawned); otherwise the loot is only rolled on death.
    /// </summary>
    private void fDropLoot()
    {
        if (!mbHasStats || mOLevelLoader == null || mOLevelLoader.UWDataImporter == null
            || mOLevelLoader.CurrentLevel == null)
            return;

        UWTilePos lOTile = fGetDeathTile();

        // What the creature carries - its object chain - drops first. If it has already
        // traded, its loot has long been rolled into it (NPCLootSpawned, see
        // UWConversationTrade); then it is not rolled a second time.
        UWEntityInfo lOEntity = GetComponent<UWEntityInfo>();
        UWNpc lONpc = lOEntity != null ? lOEntity.ObjectData as UWNpc : null;

        if (lONpc != null)
        {
            lONpc.EnsureContentsLoaded(mOLevelLoader.CurrentLevel.Masterlist);

            if (lONpc.Contents != null)
            {
                foreach (UWObject lOItem in lONpc.Contents.ToArray())
                {
                    if (lOItem == null)
                        continue;

                    lOItem.Link = 0;
                    // Scattered around the body as in the original, not all in one spot (per
                    // user, 2026-09-13) - within 6 eighths, which can reach the neighbouring
                    // tile (SpillInventory, read 2026-09-25; see UWScatterRules).
                    fScatter(lOItem, lOTile);
                }

                lONpc.Contents.Clear();
            }

            if (lONpc.NPCLootSpawned)
                return;

            lONpc.NPCLootSpawned = true;
        }

        System.Collections.Generic.List<UWCritterLoot.Drop> lODrops = UWCritterLoot.Generate(
            mOStats, mOLevelLoader.UWDataImporter.CommonObjectProperties,
            mOLevelLoader.CurrentLevel.LevelNumber, mOLevelLoader.UWDataImporter.ObjectProperties);

        // The original rolls the loot into the creature's inventory and spills it with the
        // rest (DropNPCLoot, then SpillInventory), so it goes down the same way.
        foreach (UWCritterLoot.Drop lODrop in lODrops)
            mOLevelLoader.SpawnObjectByIdAround(lODrop.ObjectId, lODrop.Quantity, lODrop.Quality,
                fGetBodyPosition(), UWScatterRules.LootRadius, lOTile);
    }

    /// <summary>Puts a carried object down around the body; the open death tile is the fallback
    /// when even the body's own spot is refused (see UWLevelLoader.SpawnScatteredAround).</summary>
    private void fScatter(UWObject pOItem, UWTilePos pODeathTile)
    {
        mOLevelLoader.SpawnScatteredAround(pOItem, fGetBodyPosition(), UWScatterRules.LootRadius, pODeathTile);
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
