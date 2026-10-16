using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Spell class 7 - the spells that go onto a clicked target (the reference:
/// spellcasting_class_7, uw1 branch). In uw1 there are five, all against creatures:
///
///   1  Cause Fear     the creature flees
///   2  Smite Undead   kills undead
///   3  Ally           the creature becomes an ally and fights for you
///   4  Poison         one hit of poison and magic, 5 to 20
///   5  Paralyse       the creature stands still
///
/// THREE OF THEM SET AN AI STATE - goal, attitude and for Ally the ally bit of the creature's
/// record, see the methods on UWCritter.
/// </summary>
public static class UWTargetSpell
{
    /// <summary>Spell class 7.</summary>
    public const int MajorClass = 7;

    private const int CauseFearSpell = 1;
    private const int SmiteUndeadSpell = 2;
    private const int AllySpell = 3;
    private const int PoisonSpell = 4;
    private const int ParalyseSpell = 5;

    /// <summary>
    /// What Smite Undead inflicts: 0xFF, more than any creature can take
    /// (SmiteUndead_seg038_3307_7EA).
    /// </summary>
    private const int SmiteDamage = 0xFF;

    /// <summary>
    /// Casts a class 7 spell on a target. Missed if there is nothing there it could affect;
    /// Spent or Shown say whether the spell picture belongs on it (UWTargetSpellRules.Result).
    /// </summary>
    public static UWTargetSpellRules.Result Cast(int piMinorClass, UWEntityInfo pOTarget)
    {
        if (pOTarget == null)
            return UWTargetSpellRules.Result.Missed;

        UWCritter lOCritter = pOTarget.GetComponentInParent<UWCritter>();
        UWDamageable lODamageable = pOTarget.GetComponentInParent<UWDamageable>();

        // ALL FIVE TARGET CREATURES. The reference checks major class 1 for this, i.e.
        // NPC; for us that is the critter component.
        if (lOCritter == null || lODamageable == null || lODamageable.IsDestroyed)
            return UWTargetSpellRules.Result.Missed;

        switch (piMinorClass)
        {
            case CauseFearSpell:
                return lOCritter.Flee();

            case SmiteUndeadSpell:
                // Never a picture (sp_ward_undead).
                return fSmiteUndead(lODamageable) ? UWTargetSpellRules.Result.Spent : UWTargetSpellRules.Result.Missed;

            case AllySpell:
                // A real ally since 2026-09-23 (UWCritter.Befriend, Ally_seg038_3307_8F8).
                return lOCritter.Befriend();

            case PoisonSpell:
                // Always the picture, resisted or not (sp_poison puts it before the damage).
                fPoison(lODamageable);

                return UWTargetSpellRules.Result.Shown;

            case ParalyseSpell:
                return lOCritter.Paralyse();

            default:
                return UWTargetSpellRules.Result.Missed;
        }
    }

    /// <summary>
    /// Smite Undead: lethal against undead, without effect against everything else.
    ///
    /// Whether something is undead is told by bit 0x80 in the resistance byte from COMOBJ.DAT (see
    /// UWDamageTypes). The DAMAGE however is then MAGICAL, not "undead": the reference
    /// uses the 0x80 only for the check and then strikes with damage type 3.
    ///
    /// THAT IS EXACTLY WHERE THE BUG WAS. I had passed the check as the damage type - and
    /// because a set resistance bit means "bounces off", of all things the undead creature took
    /// no damage. It showed on the dread spirit (per user, 2026-09-07): it carries
    /// 0x90, so it is undead AND poison-proof - but not immune to magic, the blow now
    /// goes fully through.
    ///
    /// NO LICH CASE, and that is right for UW1 (checked 2026-09-24): SmiteUndead_seg038_3307_7EA
    /// asks only the undead bit (ScaleDamageAgainstObject with type 0x80) and then deals 0xFF of
    /// type 3; UW1 has no lich at all, the name is in none of its strings (per user, liches are
    /// UW2's). The reference's half-life-force case belongs to UW2.
    /// </summary>
    private static bool fSmiteUndead(UWDamageable pODamageable)
    {
        if (!pODamageable.IsUndead)
            return false;

        pODamageable.ApplyDamage(SmiteDamage, UWDamageTypes.Magic);

        return true;
    }

    /// <summary>Poison: one hit of 5 to 20, poison and magic together - see
    /// UWTargetSpellRules. The resistances are asked by UWDamageTypes.Scale.</summary>
    private static bool fPoison(UWDamageable pODamageable)
    {
        pODamageable.ApplyDamage(UWTargetSpellRules.RollPoisonDamage(), UWTargetSpellRules.PoisonDamageType);

        return true;
    }
}
