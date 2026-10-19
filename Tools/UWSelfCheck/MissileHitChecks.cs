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

            // The armour and the halving of a missile on the player: the damage pipeline's since
            // cleanup step 4.2 (2026-10-17), see the DamagePipeline group.
            FakeVitalsHost lOArmoured = new FakeVitalsHost();

            lOArmoured.Armour[UWArmourProtection.PartLegs] = 2;

            UWDamageOutcome lOHit = UWDamagePipeline.Apply(fPipelineVitals(lOArmoured, null),
                UWDamageRequest.Missile(9, UWDamageTypes.Missile, UWArmourProtection.PartLegs, 6));

            fExpectInt("player: armour comes off first", lOHit.Applied, 7);
            fExpectInt("player: ... the shake level is the same then (7 / 4)", lOHit.ShakeLevel, 1);

            UWPlayerVitals.StartValues lOEasyStart = UWPlayerVitals.StartValues.Default;

            lOEasyStart.EasyDifficulty = true;

            UWPlayerVitals lOEasy = new UWPlayerVitals(lOArmoured);

            lOEasy.Initialise(null, lOEasyStart);
            lOHit = UWDamagePipeline.Apply(lOEasy, UWDamageRequest.Missile(9, UWDamageTypes.Missile, UWArmourProtection.PartLegs, 6));
            fExpectInt("player: easy halves what the armour leaves (7 >> 1)", lOHit.Applied, 3);
            fExpectInt("player: ... the shake level is taken before the halving (C5)", lOHit.AfterArmour, 7);

            lOArmoured.Armour[UWArmourProtection.PartLegs] = 4;
            lOHit = UWDamagePipeline.Apply(lOEasy, UWDamageRequest.Missile(3, UWDamageTypes.Missile, UWArmourProtection.PartLegs, 6));
            fExpectInt("player: armour that swallows it leaves 0 on easy too", lOHit.Applied, 0);

            // damage_critter's who (UnderworldExhumed critter/AI.C:1458-1515) is the missile's
            // last_hit: a creature's index, the player's 1, or 0 for a trap's (2026-10-19; the
            // player's own was silent until then, per user on the original it starts the music).
            fExpectBool("music: a creature's missile tells it as a creature's", UWMissileHitRules.MusicFor(6) == UWDamageMusic.Creature, true);
            fExpectBool("music: the player's own (last_hit 1) as his own", UWMissileHitRules.MusicFor(1) == UWDamageMusic.Player, true);
            fExpectBool("music: a trap's (last_hit 0) tells nothing", UWMissileHitRules.MusicFor(0) == UWDamageMusic.None, true);

            // The theme (AI.C:1500-1515): ratio hp * 64 / (avghit + 1) against 0x10. 30 of 60 is 31;
            // 10 of 60 is 10.
            fExpectInt("theme: the player well, hurt by a creature: MUSIC_COMBAT",
                UWMusicSelector.PlayerHurtTheme(30, 60, false), UWMusicSelector.EvenCombatTheme);
            fExpectInt("theme: the player low, hurt by a creature: MUSIC_DANGER",
                UWMusicSelector.PlayerHurtTheme(10, 60, false), UWMusicSelector.LosingCombatTheme);
            fExpectInt("theme: the player well, hurt by his own missile: MUSIC_COMBAT",
                UWMusicSelector.PlayerHurtTheme(30, 60, true), UWMusicSelector.EvenCombatTheme);
            fExpectInt("theme: the player low, hurt by his own missile: MUSIC_FOE_HURT",
                UWMusicSelector.PlayerHurtTheme(10, 60, true), UWMusicSelector.WinningCombatTheme);
            fExpectInt("theme: 15 of 60 is ratio 15, just below", UWMusicSelector.PlayerHurtTheme(15, 60, false),
                UWMusicSelector.LosingCombatTheme);
            fExpectInt("theme: 16 of 63 is ratio 16, just at it", UWMusicSelector.PlayerHurtTheme(16, 63, false),
                UWMusicSelector.EvenCombatTheme);
            // The owner byte names the attacker for every flight (damage_critter's who = last_hit;
            // a trap's is 0, missile_fire - since 2026-10-19, the trap's counted as the player's).
            fExpectBool("owner: 1 the player's, his own; a creature's index; 0 a trap's, nobody's",
                UWMissileHitRules.AttackerOf(UWMissileHitRules.PlayerOwner) == UWCritterBrain.PlayerIndex
                && UWMissileHitRules.IsPlayersOwn(UWMissileHitRules.PlayerOwner)
                && UWMissileHitRules.AttackerOf(6) == 6 && !UWMissileHitRules.IsPlayersOwn(6)
                && UWMissileHitRules.AttackerOf(0) == UWBlow.Nobody && !UWMissileHitRules.IsPlayersOwn(0), true);

            // Walking into a missile hurts whosever it is - a usable one (do_objhit -> UseObj ->
            // missile_newhit; since 2026-10-19, the player's own alone until then).
            fExpectBool("walk into: a usable missile hurts, an unusable one or a thrown sword does not",
                UWMissileHitRules.HurtsWhoWalksIntoIt(0x12, true) && !UWMissileHitRules.HurtsWhoWalksIntoIt(0x18, false)
                && !UWMissileHitRules.HurtsWhoWalksIntoIt(0x04, true), true);

            // Every missile shakes: missile_thwack's fromwho is its last_hit, never -1 (COMBAT.C:761-780,
            // 382-389; since 2026-10-19 - a trap's did not until then).
            fExpectBool("shake: a creature's missile, the player's own and a trap's shake the view",
                UWDamageRequest.Missile(6, UWDamageTypes.Missile, 1, 6).Shakes
                && UWDamageRequest.Missile(6, UWDamageTypes.Missile, 1, UWMissileHitRules.PlayerOwner).Shakes
                && UWDamageRequest.Missile(6, UWDamageTypes.Missile, 1, 0).Shakes, true);

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
