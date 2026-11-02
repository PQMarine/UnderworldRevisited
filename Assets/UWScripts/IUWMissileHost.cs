using UnityEngine;

/// <summary>
/// WHO SHOOTS AND THROWS: what the player's missiles (UWPlayerMissiles, UWMissiles, UWPlayerThrow) ask of the
/// game around them beyond a blow's questions. Interaction answers for the first game, UWAttackUw2 for the
/// second (UW2 plan phase 4, list item 2).
/// </summary>
public interface IUWMissileHost : IUWMeleeHost
{
    /// <summary>Where the shot goes: heading in 256ths of a turn, 0 north, and the missile's pitch.</summary>
    void AimMissile(int piSpeedByte, out int piHeading, out int piPitch);

    /// <summary>A line of block 1 in the scroll.</summary>
    void AddGeneralMessage(int piIndex);

    /// <summary>The player's missile has hit (UWHitFeedback.ReportSpellHit).</summary>
    void ReportSpellHit(UWEntityInfo pOTarget, UWDamageable pODamageable, Vector3 pOPoint);

    int GetThrownMissileBaseDamage(int piObjectId);

    int GetThrownMissileDamageType(int piObjectId);
}
