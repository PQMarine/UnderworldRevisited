using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.5 (2026-10-17): the missile's hit maths (UWMissileHitRules), moved out of
    /// UWProjectileFlight. Pinned over the rule itself: which flying things hurt, which detonate,
    /// the armour, the easy halving and the shake level for the player, the second hit sound's
    /// volume, the trace, the music and the shake by launcher, the flash height on an object and
    /// a door, and the orb rock. Draw-free: the player's body part is asked only in the two clear
    /// cases, which never reach UWRandom.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckMissileHit()
        {
            Check.Group("Missile hit (UWMissileHitRules, the maths of MissileAttackHit_seg022_14BF)");

            fExpectBool("missile: 0x0F, just below the missiles, does not hurt what it hits",
                UWMissileHitRules.HurtsWhatItHits(0x0F), false);
            fExpectBool("missile: 0x10, the first missile object, does", UWMissileHitRules.HurtsWhatItHits(0x10), true);
            fExpectBool("missile: 0x1F, the last one, does", UWMissileHitRules.HurtsWhatItHits(0x1F), true);
            fExpectBool("missile: 0x20 does not", UWMissileHitRules.HurtsWhatItHits(0x20), false);
            fExpectBool("missile: the orb rock does not (it smashes the orb only)",
                UWMissileHitRules.HurtsWhatItHits(UWObjectMechanics.OrbRockObjectId), false);

            fExpectBool("detonates: the fireball", UWMissileHitRules.Detonates(UWObjectDamageRules.FireballObjectId), true);
            fExpectBool("detonates: the lightning bolt", UWMissileHitRules.Detonates(UWObjectDamageRules.LightningBoltObjectId), true);
            fExpectBool("detonates: an arrow does not", UWMissileHitRules.Detonates(0x10), false);

            fExpectInt("armour: 7 damage less armour 3 leaves 4", UWMissileHitRules.DamageAfterArmour(7, 3), 4);
            fExpectInt("armour: armour equal to the damage swallows it", UWMissileHitRules.DamageAfterArmour(5, 5), 0);
            fExpectInt("armour: more armour than damage leaves 0, not less", UWMissileHitRules.DamageAfterArmour(2, 9), 0);
            fExpectBool("trace: none when the armour swallows the hit", UWMissileHitRules.LeavesTrace(0), false);
            fExpectBool("trace: blood or flash from 1 damage", UWMissileHitRules.LeavesTrace(1), true);
            fExpectInt("sound: the second hit sound is louder by four times the damage before the armour",
                UWMissileHitRules.HitSoundVolume(6), 24);

            // The player: feet 10, body 30 high. A missile whose middle is below the feet + 1
            // strikes the legs, one whose middle is above the top - 1 the head (pickloc).
            fExpectInt("player: a low missile strikes the legs",
                UWMissileHitRules.PickPlayerBodyPart(10, 30, 0, 4), UWArmourProtection.PartLegs);
            fExpectInt("player: a high missile strikes the head",
                UWMissileHitRules.PickPlayerBodyPart(10, 30, 50, 4), UWArmourProtection.PartHead);

            int liShake;

            fExpectInt("player: armour comes off first", UWMissileHitRules.PlayerDamage(9, 2, false, out liShake), 7);
            fExpectInt("player: ... the shake level is the same then", liShake, 7);
            fExpectInt("player: easy halves what the armour leaves (7 >> 1)", UWMissileHitRules.PlayerDamage(9, 2, true, out liShake), 3);
            fExpectInt("player: ... the shake level is taken before the halving (C5)", liShake, 7);
            fExpectInt("player: armour that swallows it leaves 0 on easy too", UWMissileHitRules.PlayerDamage(3, 4, true, out liShake), 0);

            fExpectBool("music: a creature's missile starts the combat music", UWMissileHitRules.StartsCombatMusic(true), true);
            fExpectBool("music: the player's own or a trap's does not", UWMissileHitRules.StartsCombatMusic(false), false);
            fExpectBool("shake: a creature's missile shakes the view", UWMissileHitRules.ShakesPlayer(true, false), true);
            fExpectBool("shake: the player's own rebounding missile does", UWMissileHitRules.ShakesPlayer(false, true), true);
            fExpectBool("shake: a trap's missile (no attacker) does not", UWMissileHitRules.ShakesPlayer(false, false), false);

            fExpectInt("flash: on an object at the missile's z plus half its height",
                UWMissileHitRules.ObjectFlashZ(20, 6, false, 40), 23);
            fExpectInt("flash: on a door standing above that, at the door's z + 2",
                UWMissileHitRules.ObjectFlashZ(20, 6, true, 40), 42);
            fExpectInt("flash: on a door below it, at the missile's height still",
                UWMissileHitRules.ObjectFlashZ(20, 6, true, 10), 23);

            fExpectBool("orb: the orb rock smashes Tybal's orb",
                UWMissileHitRules.SmashesOrb(UWObjectMechanics.OrbRockObjectId, UWObjectMechanics.TyballOrbObjectId), true);
            fExpectBool("orb: ... nothing else",
                UWMissileHitRules.SmashesOrb(UWObjectMechanics.OrbRockObjectId, UWObjectMechanics.ChestObjectId), false);
            fExpectBool("orb: an arrow does not smash the orb",
                UWMissileHitRules.SmashesOrb(0x10, UWObjectMechanics.TyballOrbObjectId), false);
        }
    }
}
