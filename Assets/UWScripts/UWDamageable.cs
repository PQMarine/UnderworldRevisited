using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Everything that can take damage - critters, doors, barrels and chests. The hit points and the
/// damage rules live engine-free in UWHealth (since 2026-09-17, P2 of the engine separation); this
/// component attaches them to the GameObject and raises the events.
/// </summary>
public class UWDamageable : MonoBehaviour
{
    /// <summary>Called when the hit points drop to 0. Doors then burst open, critters die - what
    /// happens in each case is decided by the subscriber, not by this class.</summary>
    public System.Action<UWDamageable> Destroyed;

    /// <summary>On every hit, even if it does not kill - a door writes its condition into the data
    /// (UWDoor). A creature hears of its blows through CreatureTakesBlow instead.</summary>
    public System.Action<UWDamageable> Damaged;

    /// <summary>
    /// THE CREATURE BEHIND THIS BODY TAKES EACH BLOW WITH ITS ATTACKER (cleanup step 4.8,
    /// 2026-10-18): called on every blow that lands, before Damaged, with what of it reached the
    /// body (after the resistances) and who dealt it - a creature's index, the player's
    /// (UWCritterBrain.PlayerIndex) or nobody (UWBlow.Nobody). UWCritter hands it to DamageNPC
    /// (UWCritterBrain.OnDamaged), which stores the attacker in byte 0x12 and takes the damage off
    /// the record's hit points - the body subtracts nothing itself then (see ApplyDamage). Until
    /// then the attacker waited in UWCritter as a pending index (AnnounceAttacker) for the next
    /// Damaged event.
    /// </summary>
    public System.Action<int, int> CreatureTakesBlow;

    /// <summary>Something in front of the thing that takes the blow before its hit points - the
    /// spike of a closed door (UWDoor.TryDamageSpike, 2026-10-12). Called with the damage as it
    /// reaches the thing - after the resistances and the quality class shift - and its type; true
    /// means the blow is used up and the hit points stay. Asked only after CanTakeDamage and only
    /// with something left of the blow, in damage_object's order (DAMAGE.C: bit 13 and class 3
    /// leave at once, then the shift, nothing left leaves, then the spike's strength). Until
    /// 2026-10-12 the spike took the full blow, on a protected or class 3 door too.</summary>
    public System.Func<int, int, bool> TakesBlowFirst;

    private readonly UWHealth mOHealth = new UWHealth();

    /// <summary>The engine-free hit points.</summary>
    public UWHealth Health => mOHealth;

    public int MaxHealth => mOHealth.MaxHealth;

    public int CurrentHealth => mOHealth.CurrentHealth;

    public int Armour => mOHealth.Armour;

    public int Defence => mOHealth.Defence;

    public int QualityType => mOHealth.QualityType;

    public bool IsDestroyed => mOHealth.IsDestroyed;

    /// <summary>
    /// A BROKEN DOOR STAYS A DOOR (per user on the original, 2026-10-13: a closed broken door
    /// bursts open at the next blow, and it can still be spiked). In the original a door's
    /// condition is its quality, and damage_object (DAMAGE.C) lets every blow that is left after
    /// the class shift take it to 0 again and destroy it again - remove_object then opens a
    /// closed door and takes a lock off. Ours took a door at 0 out of play: blows went through
    /// it as through air. Set by UWDoor; a creature, barrel or chest stays out of play at 0.
    /// </summary>
    public bool TakesBlowsWhenBroken;

    /// <summary>
    /// A CREATURE'S BODY ASKS ITS DEATH MACHINE (cleanup step 4.12c, 2026-10-19, the user's decision
    /// "as the original"): UWCritter sets this to UWCreatureActor.TakesBlows - every blow lands until
    /// the creature is removed at frame 3, the dying one included. Null for a door, a barrel or a
    /// chest, which go by their hit points and TakesBlowsWhenBroken.
    /// </summary>
    public System.Func<bool> TakesBlowsRule;

    /// <summary>Whether a blow can land on this at all: a creature until its death machine removes
    /// it (TakesBlowsRule); a thing when not broken, or broken but still taking blows
    /// (TakesBlowsWhenBroken). Not indestructible is asked by ApplyDamage itself. The one test of
    /// the world's blow routing (UWWorld's CreatureTakesBlows), ApplyDamage and the sites before
    /// them.</summary>
    public bool TakesBlows => TakesBlowsRule != null ? TakesBlowsRule() : !IsDestroyed || TakesBlowsWhenBroken;

    public float HealthFraction => mOHealth.HealthFraction;

    public int InitialHealth => mOHealth.InitialHealth;

    public int QualityClass => mOHealth.QualityClass;

    public bool IsIndestructible => mOHealth.IsIndestructible;

    /// <summary>Word 0 bit 13 set - no blow and no blast harms it (UWHealth.IsProtected).</summary>
    public bool IsProtected
    {
        get => mOHealth.IsProtected;
        set => mOHealth.IsProtected = value;
    }

    public int Resistances => mOHealth.Resistances;

    public bool IsUndead => mOHealth.IsUndead;

    public void Initialise(int piMaxHealth, int piArmour, int piQualityType, int piDefence = 0, int piResistances = 0,
        int piCurrentHealth = -1, int piQualityClass = 0)
    {
        mOHealth.Initialise(piMaxHealth, piArmour, piQualityType, piDefence, piResistances, piCurrentHealth, piQualityClass);
    }

    /// <summary>The damage type of the last blow that landed - for a Destroyed handler, as
    /// remove_object receives the type (UWContainerDamage: a nightstand under fire smokes).</summary>
    public int LastDamageType { get; private set; }

    /// <summary>Subtracts damage and reports whether the target was destroyed by it - see
    /// UWHealth.ApplyDamage. CreatureTakesBlow and Damaged fire on every blow that lands, Destroyed
    /// after them. piAttacker: whom a creature holds responsible (UWBlow.Attacker); a door, a
    /// barrel or a chest keeps none.</summary>
    public bool ApplyDamage(int piDamage, int piDamageType, int piAttacker)
    {
        bool lbBrokenAlready = mOHealth.IsDestroyed;

        // TakesBlows: for a thing broken and not TakesBlowsWhenBroken (the test here until cleanup
        // step 4.12c, 2026-10-19), for a creature removed by its death machine.
        if (mOHealth.IsIndestructible || !TakesBlows)
            return false;

        int liDamage = mOHealth.ScaleDamage(piDamage, piDamageType);

        if (liDamage > 0 && TakesBlowFirst != null && TakesBlowFirst(liDamage, piDamageType))
            return false;

        // Broken already: nothing left after the shift leaves it alone (damage_object returns
        // before anything when the damage is 0). Not a dying creature (cleanup step 4.12c,
        // 2026-10-19): damage_critter runs with 0 too and records the attacker (UnderworldExhumed
        // combat/DAMAGE.C:187-195, critter/AI.C:1458-1513), as on a living one.
        if (lbBrokenAlready && liDamage <= 0 && CreatureTakesBlow == null)
            return false;

        LastDamageType = piDamageType;

        // A CREATURE'S MIND TAKES THE BLOW (cleanup step 4.8, 2026-10-18): DamageNPC subtracts it
        // from the record, which holds the hit points (UWHealth.KeepHitPointsIn), and commits the
        // death or lets a named creature refuse it - MarkDestroyed raises Destroyed then. Until
        // then the body subtracted its own copy first and the mind worked out the amount from
        // the difference; a refused death gave the copy at least 1 hit point back
        // (UWCritter.fKeepBodyAlive), which is what the save then wrote (B23).
        if (CreatureTakesBlow != null)
        {
            CreatureTakesBlow(liDamage, piAttacker);
            Damaged?.Invoke(this);

            return !lbBrokenAlready && mOHealth.IsDestroyed;
        }

        bool lbDestroyed = mOHealth.TakeScaledDamage(liDamage);

        Damaged?.Invoke(this);

        if (lbDestroyed)
            Destroyed?.Invoke(this);

        return lbDestroyed;
    }

    /// <summary>A creature's death is committed (cleanup step 4.8, 2026-10-18): UWCritter when the
    /// brain starts it or a drowning or a load ends in it. Destroyed fires once, as on the blow
    /// that broke a door. Until then UWCritter drained the body with a blow of its own.</summary>
    public void MarkDestroyed()
    {
        if (mOHealth.IsDestroyed)
            return;

        mOHealth.MarkDestroyed();
        Destroyed?.Invoke(this);
    }

    public int GetConditionStringIndex()
    {
        return mOHealth.GetConditionStringIndex();
    }
}
