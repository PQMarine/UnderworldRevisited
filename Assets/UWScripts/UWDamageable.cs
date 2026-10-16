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

    /// <summary>On every hit, even if it does not kill. A peaceful critter becomes hostile through
    /// this (see UWCritter).</summary>
    public System.Action<UWDamageable> Damaged;

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

    /// <summary>Whether a blow can land on this at all: not broken, or broken but still taking
    /// blows (TakesBlowsWhenBroken). Not indestructible is asked by ApplyDamage itself.</summary>
    public bool TakesBlows => !IsDestroyed || TakesBlowsWhenBroken;

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
    /// UWHealth.ApplyDamage. Damaged fires on every blow that lands, Destroyed after it.</summary>
    public bool ApplyDamage(int piDamage, int piDamageType = UWDamageTypes.None)
    {
        bool lbBrokenAlready = mOHealth.IsDestroyed;

        if (mOHealth.IsIndestructible || (lbBrokenAlready && !TakesBlowsWhenBroken))
            return false;

        int liDamage = mOHealth.ScaleDamage(piDamage, piDamageType);

        if (liDamage > 0 && TakesBlowFirst != null && TakesBlowFirst(liDamage, piDamageType))
            return false;

        // Broken already: nothing left after the shift leaves it alone (damage_object returns
        // before anything when the damage is 0).
        if (lbBrokenAlready && liDamage <= 0)
            return false;

        bool lbDestroyed = mOHealth.TakeScaledDamage(liDamage);

        LastDamageType = piDamageType;

        Damaged?.Invoke(this);

        // A Damaged handler may have refused the death and given the hit points back - the
        // golem and the nameless one of UWSpecialDeaths (UWCritter.fKeepBodyAlive).
        lbDestroyed = lbDestroyed && mOHealth.IsDestroyed;

        if (lbDestroyed)
            Destroyed?.Invoke(this);

        return lbDestroyed;
    }

    public int GetConditionStringIndex()
    {
        return mOHealth.GetConditionStringIndex();
    }
}
