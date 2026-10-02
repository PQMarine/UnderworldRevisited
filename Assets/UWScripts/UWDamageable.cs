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

    private readonly UWHealth mOHealth = new UWHealth();

    /// <summary>The engine-free hit points.</summary>
    public UWHealth Health => mOHealth;

    public int MaxHealth => mOHealth.MaxHealth;

    public int CurrentHealth => mOHealth.CurrentHealth;

    public int Armour => mOHealth.Armour;

    public int Defence => mOHealth.Defence;

    public int QualityType => mOHealth.QualityType;

    public bool IsDestroyed => mOHealth.IsDestroyed;

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

    /// <summary>Subtracts damage and reports whether the target was destroyed by it - see
    /// UWHealth.ApplyDamage. Damaged fires on every blow that lands, Destroyed after it.</summary>
    public bool ApplyDamage(int piDamage, int piDamageType = UWDamageTypes.None)
    {
        if (!mOHealth.CanTakeDamage)
            return false;

        bool lbDestroyed = mOHealth.ApplyDamage(piDamage, piDamageType);

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
