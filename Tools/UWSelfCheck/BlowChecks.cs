using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.3 (2026-10-17): IUWWorld.Damage with UWBlow. Pinned: what each site's blow
    /// carries (type, attacker) and where it lands in a fake world - since cleanup step 4.8
    /// (2026-10-18) a creature takes the attacker with the damage: a missile's launcher, a
    /// creature's blow its index, damage nobody dealt nobody, the player's blow (a miss with 0) and
    /// the target spells the player, a blast its caster unless it strikes the caster himself;
    /// nothing on a destroyed creature, the curse and the backfire on the player only, a damage
    /// trap's strike on a thing in flight.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckBlow()
        {
            Check.Group("Blow (UWBlow, UWBlowRouting, IUWWorld.Damage - cleanup step 3.3)");

            fCheckBlowFactories();
            fCheckBlowRouting();
        }

        private static void fExpectBlow(string psName, UWBlow pOBlow, int piType, int piAttacker)
        {
            fExpectInt(psName + ": damage type", pOBlow.DamageType, piType);
            fExpectInt(psName + ": attacker", pOBlow.Attacker, piAttacker);
        }

        /// <summary>Each site's blow as it was before the step; the attacker is the one the struck
        /// creature's damage event took (the pending one until cleanup step 4.8).</summary>
        private static void fCheckBlowFactories()
        {
            fExpectBlow("player's blow (bash, the player's)", UWBlow.PlayerBlow(5), UWDamageTypes.Physical, UWCritterBrain.PlayerIndex);
            fExpectBlow("player's miss (0, the player's)", UWBlow.PlayerBlow(0), UWDamageTypes.Physical, UWCritterBrain.PlayerIndex);
            fExpectBlow("creature's blow (bash, its index)", UWBlow.CreatureBlow(4, 9), UWDamageTypes.Physical, 9);
            fExpectBlow("missile (its type, its launcher)", UWBlow.Missile(3, UWDamageTypes.Missile, 12), UWDamageTypes.Missile, 12);

            fExpectInt("blast: a creature's blast is charged to the caster",
                UWBlow.Blast(6, UWDamageTypes.Fire, 9).Attacker, 9);
            fExpectInt("blast: the player's blast is charged to the player",
                UWBlow.Blast(6, UWDamageTypes.Fire, UWCritterBrain.PlayerIndex).Attacker, UWCritterBrain.PlayerIndex);
            fExpectInt("blast: a blast without an attacker is nobody's (damage_square from src 0, cleanup 4.10)",
                UWBlow.Blast(6, UWDamageTypes.Fire, UWBlow.NoBlastAttacker).Attacker, UWBlow.Nobody);
            fExpectInt("blast: a blast from 0 is nobody's", UWBlow.Blast(6, UWDamageTypes.Fire, UWBlow.Nobody).Attacker,
                UWBlow.Nobody);
            fExpectInt("blast: keeps the blast's type", UWBlow.Blast(6, UWDamageTypes.Magic, 1).DamageType,
                UWDamageTypes.Magic);

            fExpectBlow("target spell (the player's)", UWBlow.TargetSpell(0xFF, UWDamageTypes.Magic), UWDamageTypes.Magic,
                UWCritterBrain.PlayerIndex);
            fExpectBlow("damage trap on a creature (nobody's)", UWBlow.Trap(8, UWDamageTypes.Physical), UWDamageTypes.Physical,
                UWBlow.Nobody);
            fExpectBlow("ward (bash, nobody's)", UWBlow.Ward(3), UWDamageTypes.Physical, UWBlow.Nobody);
            fExpectBlow("lava under a creature (plain fire, nobody's)", UWBlow.Lava(1), UWDamageTypes.PlainFire, UWBlow.Nobody);
            fExpectBool("a blow to the player strikes the player only",
                UWBlow.ToPlayer(UWDamageRequest.Curse(3)).StrikesPlayerOnly && !UWBlow.PlayerBlow(2).StrikesPlayerOnly, true);
            fExpectInt("nobody is attacker 0 (damage_critter's who = 0)", UWBlow.Nobody, 0);
        }

        /// <summary>The routing in a fake world: what lands where, in which order.</summary>
        private static void fCheckBlowRouting()
        {
            UWDamageTarget lOCreature = UWDamageTarget.Creature(7);

            List<string> lOLog = fLand(lOCreature, UWBlow.Missile(4, UWDamageTypes.Missile, 12));
            fExpectBool("missile on a creature: the damage with the launcher",
                lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 4 type 64 by 12", true);

            lOLog = fLand(lOCreature, UWBlow.CreatureBlow(5, 9));
            fExpectBool("creature's blow: the damage with the attacker",
                lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 5 type 4 by 9", true);

            lOLog = fLand(lOCreature, UWBlow.CreatureBlow(0, 9));
            fExpectBool("creature's blow that missed: 0 still lands, so the defender records the attacker",
                lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 0 type 4 by 9", true);

            lOLog = fLand(lOCreature, UWBlow.Ward(3));
            fExpectBool("ward: nobody's", lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 3 type 4 by 0", true);

            lOLog = fLand(lOCreature, UWBlow.Lava(1));
            fExpectBool("lava: plain fire, nobody's", lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 1 type 8 by 0", true);

            lOLog = fLand(lOCreature, UWBlow.PlayerBlow(6));
            fExpectBool("player's blow: bash, the player's", lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 6 type 4 by 1", true);

            lOLog = fLand(lOCreature, UWBlow.PlayerBlow(0));
            fExpectBool("player's miss: 0 lands, the creature records the player",
                lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 0 type 4 by 1", true);

            // A MAGE CAUGHT IN ITS OWN FIREBALL is hit by the mage (damage_critter stores the
            // launcher's own index; cleanup 4.10, 2026-10-18 - the player's until then).
            lOLog = fLand(lOCreature, UWBlow.Blast(6, UWDamageTypes.Fire, 7));
            fExpectBool("blast on its own caster: the caster's, not the player's",
                lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 6 type 11 by 7", true);

            lOLog = fLand(lOCreature, UWBlow.Blast(6, UWDamageTypes.Fire, UWBlow.NoBlastAttacker));
            fExpectBool("blast nobody launched: nobody's, not the player's",
                lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 6 type 11 by 0", true);

            FakeWorld lOWorld = new FakeWorld();

            lOWorld.DestroyedCreatures.Add(7);
            lOWorld.Damage(lOCreature, UWBlow.CreatureBlow(5, 9));
            lOWorld.Damage(lOCreature, UWBlow.Missile(5, UWDamageTypes.Missile, 9));
            fExpectInt("a destroyed creature: no attacker, no damage", lOWorld.Blows.Count, 0);

            lOLog = fLand(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Trap(5)));
            fExpectBool("trap on the player: his hit points, type 4 (whack_thing, B5)",
                lOLog.Count == 1 && lOLog[0] == "player takes 5 type 4", true);

            lOLog = fLand(UWDamageTarget.ThePlayer, UWBlow.Trap(5, UWDamageTypes.Physical));
            fExpectBool("a blow without a request on the player: plain damage of its type",
                lOLog.Count == 1 && lOLog[0] == "player takes 5 type 4", true);

            lOLog = fLand(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Curse(3)));
            fExpectBool("curse: the player's curse with its dice", lOLog.Count == 1 && lOLog[0] == "player cursed 3", true);

            lOLog = fLand(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Backfire(2)));
            fExpectBool("backfire: the player's backfire with its dice", lOLog.Count == 1 && lOLog[0] == "player backfire 2", true);

            lOLog = fLand(lOCreature, UWBlow.ToPlayer(UWDamageRequest.Curse(3)));
            fExpectInt("curse on a creature: nothing", lOLog.Count, 0);

            // The player's end takes a request whole (cleanup step 4.2, 2026-10-17).
            FakeWorld lORequestWorld = new FakeWorld();
            UWDamageRequest lOBlowRequest = UWDamageRequest.CreatureBlow(9, 2, true);

            lORequestWorld.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(lOBlowRequest));
            fExpectBool("request: reaches the player's end whole (part, armour, halving, flash)",
                lORequestWorld.PlayerRequests.Count == 1 && lORequestWorld.PlayerRequests[0].BodyPart == 2
                && lORequestWorld.PlayerRequests[0].SubtractArmour && lORequestWorld.PlayerRequests[0].HalveOnEasy
                && lORequestWorld.PlayerRequests[0].FlashColour == UWDamageRequest.CriticalFlashColour, true);

            lOLog = fLand(lOCreature, UWBlow.ToPlayer(UWDamageRequest.Lava(1)));
            fExpectInt("request on a creature: nothing, it strikes the player only", lOLog.Count, 0);

            lOLog = fLand(UWDamageTarget.ObjectOf(new UWObject(0x140)), UWBlow.ToPlayer(UWDamageRequest.Lava(1)));
            fExpectInt("request on an object: nothing", lOLog.Count, 0);

            lOLog = fLand(UWDamageTarget.ObjectOf(new UWObject(0x140)), UWBlow.CreatureBlow(4, 9));
            fExpectBool("creature's blow on a door: only the damage, an object keeps no attacker",
                lOLog.Count == 1 && lOLog[0] == "object 320 takes 4 type 4", true);

            lOLog = fLand(UWDamageTarget.InFlight(1030), UWBlow.Trap(6, UWDamageTypes.Physical));
            fExpectBool("damage trap on a thing in flight: its strike, bash",
                lOLog.Count == 1 && lOLog[0] == "flight 1030 takes 6 type 4", true);

            UWBlowRouting.Land(null, lOCreature, UWBlow.PlayerBlow(1));
            fExpectBool("routing without bodies does nothing", true, true);
        }

        private static List<string> fLand(UWDamageTarget pOTarget, UWBlow pOBlow)
        {
            FakeWorld lOWorld = new FakeWorld();

            lOWorld.Damage(pOTarget, pOBlow);

            return lOWorld.Blows;
        }
    }
}
