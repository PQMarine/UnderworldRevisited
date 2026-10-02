using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The frame tick of the mobile objects (seg034_2F89_406 and ProcessMobileObjects, spec
/// 1.1-1.2): once per frame the level's creature clock (UWScene.CritterClock) advances by the
/// elapsed PIT ticks - 256 per second, from the scaled frame time, so the clock holds of
/// UWGameClock pause it like everything else - halved under the player's Speed enchantment,
/// and then every creature runs as many updates as it is due (UWCritter.RunDueUpdates).
///
/// Freeze Time is not the clock's business: the clock runs on and the creatures are simply
/// not walked (115082), which is why they stand exactly where they were and carry on from
/// there when time runs again. A conversation is a modal loop in the original: nothing moves
/// meanwhile, so the walk is skipped there too (the clock hold already stops the frame time).
///
/// Attached to the level object next to UWCreatureRespawner (UWLevelLoader.Start).
/// </summary>
public class UWCritterDriver : MonoBehaviour
{
    /// <summary>The PIT ticks not yet handed to the clock: a frame is rarely a whole number
    /// of ticks.</summary>
    private float mfTickRemainder;

    /// <summary>A copy of the registry for the walk, because a creature may be removed
    /// during its own update.</summary>
    private readonly List<UWCritter> mOWalk = new List<UWCritter>();

    /// <summary>Whether the player's Speed enchantment halved the world's slot advances this
    /// frame (114998-115008). UWCritter reads it for the picture's interpolation time.</summary>
    public static bool PlayerHasSpeed { get; private set; }

    private void Update()
    {
        if (UWConversationScreen.IsAnyOpen)
            return;

        UWCritterClock lOClock = UWScene.CritterClock;

        if (lOClock == null)
            return;

        mfTickRemainder += Time.deltaTime * UWCritter.PitTicksPerSecond;

        int liTicks = Mathf.FloorToInt(mfTickRemainder);

        mfTickRemainder -= liTicks;

        PlayerHasSpeed = fPlayerHasSpeed();

        int liUnits = lOClock.Advance(liTicks, PlayerHasSpeed);

        if (liUnits == 0 || UWCharacter.TimeIsFrozen)
            return;

        mOWalk.Clear();
        mOWalk.AddRange(UWCritter.Active);

        for (int liAt = 0; liAt < mOWalk.Count; liAt++)
        {
            UWCritter lOCritter = mOWalk[liAt];

            if (lOCritter != null && lOCritter.isActiveAndEnabled)
                lOCritter.RunDueUpdates(lOClock);
        }
    }

    /// <summary>The Speed effect among the player's active spells (class 11, decoded in
    /// UWPlayerVitals.fApplyActiveSpells; the effect subclass is UWMiscSpellRules.SpeedEffectMinor).</summary>
    private static bool fPlayerHasSpeed()
    {
        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter == null || lOCharacter.ActiveSpells == null)
            return false;

        foreach (UWActiveSpellEffect lOSpell in lOCharacter.ActiveSpells)
        {
            if (lOSpell.MajorClass == UWMiscSpellRules.MajorClass && lOSpell.MinorClass == UWMiscSpellRules.SpeedEffectMinor)
                return true;
        }

        return false;
    }
}
