using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.3 (2026-10-17): IUWWorld.Damage with UWBlow. Pinned: what each site's blow
    /// carries (type, attacker, how a creature learns of it) and where it lands in a fake world - a
    /// missile's attacker announced before the damage and left for the damage event, a creature's
    /// blow and damage nobody dealt charged for that blow only, the player's blow and the target
    /// spells untold, a blast that spares its own caster's announcement, nothing on a destroyed
    /// creature, the curse and the backfire on the player only, a damage trap's strike on a thing
    /// in flight.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckBlow()
        {
            Check.Group("Blow (UWBlow, UWBlowRouting, IUWWorld.Damage - cleanup step 3.3)");

            fCheckBlowFactories();
            fCheckBlowRouting();
        }

        private static void fExpectBlow(string psName, UWBlow pOBlow, int piType, int piAttacker, UWBlowAttribution peAttribution)
        {
            fExpectInt(psName + ": damage type", pOBlow.DamageType, piType);
            fExpectInt(psName + ": attacker", pOBlow.Attacker, piAttacker);
            fExpectInt(psName + ": attribution", (int)pOBlow.Attribution, (int)peAttribution);
        }

        /// <summary>Each site's blow as it was before the step.</summary>
        private static void fCheckBlowFactories()
        {
            fExpectBlow("player's blow (bash, untold)", UWBlow.PlayerBlow(5), UWDamageTypes.Physical, UWCritterBrain.PlayerIndex,
                UWBlowAttribution.Untold);
            fExpectBlow("creature's blow (bash, for this blow only)", UWBlow.CreatureBlow(4, 9), UWDamageTypes.Physical, 9,
                UWBlowAttribution.ThisBlowOnly);
            fExpectBlow("missile (its type, announced)", UWBlow.Missile(3, UWDamageTypes.Missile, 12), UWDamageTypes.Missile, 12,
                UWBlowAttribution.Announced);

            fExpectInt("blast: the player's blast is announced to a creature",
                (int)UWBlow.Blast(6, UWDamageTypes.Fire, UWCritterBrain.PlayerIndex, 7).Attribution, (int)UWBlowAttribution.Announced);
            fExpectInt("blast: a mage's own blast is not announced to the mage",
                (int)UWBlow.Blast(6, UWDamageTypes.Fire, 7, 7).Attribution, (int)UWBlowAttribution.Untold);
            fExpectInt("blast: a blast without an attacker is not announced",
                (int)UWBlow.Blast(6, UWDamageTypes.Fire, UWBlow.NoBlastAttacker, 7).Attribution, (int)UWBlowAttribution.Untold);
            fExpectInt("blast: keeps the blast's type", UWBlow.Blast(6, UWDamageTypes.Magic, 1, UWBlow.NoCreature).DamageType,
                UWDamageTypes.Magic);

            fExpectBlow("target spell (untold)", UWBlow.TargetSpell(0xFF, UWDamageTypes.Magic), UWDamageTypes.Magic,
                UWCritterBrain.PlayerIndex, UWBlowAttribution.Untold);
            fExpectBlow("damage trap on a creature (nobody's)", UWBlow.Trap(8, UWDamageTypes.Physical), UWDamageTypes.Physical,
                UWBlow.Nobody, UWBlowAttribution.ThisBlowOnly);
            fExpectInt("damage trap on the player: untyped as before (B5)", UWBlow.Trap(8, UWDamageTypes.None).DamageType,
                UWDamageTypes.None);
            fExpectBlow("ward (bash, nobody's)", UWBlow.Ward(3), UWDamageTypes.Physical, UWBlow.Nobody, UWBlowAttribution.ThisBlowOnly);
            fExpectBlow("lava under a creature (plain fire, nobody's)", UWBlow.Lava(1), UWDamageTypes.PlainFire, UWBlow.Nobody,
                UWBlowAttribution.ThisBlowOnly);
            fExpectInt("sleep: untyped as before (B5)", UWBlow.Sleep(2).DamageType, UWDamageTypes.None);

            fExpectBool("curse: the amount is dice", UWBlow.Curse(3).RollsDice, true);
            fExpectBool("backfire: the amount is dice", UWBlow.Backfire(2).RollsDice, true);
            fExpectBool("a blow's amount is hit points", UWBlow.PlayerBlow(2).RollsDice, false);
            fExpectInt("nobody is attacker 0 (damage_critter's who = 0)", UWBlow.Nobody, 0);
        }

        /// <summary>The routing in a fake world: what lands where, in which order.</summary>
        private static void fCheckBlowRouting()
        {
            UWDamageTarget lOCreature = UWDamageTarget.Creature(7);

            List<string> lOLog = fLand(lOCreature, UWBlow.Missile(4, UWDamageTypes.Missile, 12));
            fExpectInt("missile on a creature: the launcher announced before the damage",
                fIndexOfSequence(lOLog, "attacker of 7 = 12", "Creature 7 takes 4 type 64"), 0);
            fExpectInt("missile on a creature: the announcement is left for the damage event", lOLog.Count, 2);

            lOLog = fLand(lOCreature, UWBlow.CreatureBlow(5, 9));
            fExpectInt("creature's blow: charged to the attacker for this blow, then the player again",
                fIndexOfSequence(lOLog, "attacker of 7 = 9", "Creature 7 takes 5 type 4", "attacker of 7 = 1"), 0);

            lOLog = fLand(lOCreature, UWBlow.CreatureBlow(0, 9));
            fExpectInt("creature's blow that missed: 0 still lands, so the defender records the attacker",
                fIndexOfSequence(lOLog, "attacker of 7 = 9", "Creature 7 takes 0 type 4", "attacker of 7 = 1"), 0);

            lOLog = fLand(lOCreature, UWBlow.Ward(3));
            fExpectInt("ward: nobody's for this blow, then the player again",
                fIndexOfSequence(lOLog, "attacker of 7 = 0", "Creature 7 takes 3 type 4", "attacker of 7 = 1"), 0);

            lOLog = fLand(lOCreature, UWBlow.Lava(1));
            fExpectInt("lava: plain fire, nobody's",
                fIndexOfSequence(lOLog, "attacker of 7 = 0", "Creature 7 takes 1 type 8", "attacker of 7 = 1"), 0);

            lOLog = fLand(lOCreature, UWBlow.PlayerBlow(6));
            fExpectInt("player's blow: only the damage", lOLog.Count, 1);
            fExpectBool("player's blow: ... bash", lOLog.Count == 1 && lOLog[0] == "Creature 7 takes 6 type 4", true);

            lOLog = fLand(lOCreature, UWBlow.Blast(6, UWDamageTypes.Fire, 7, 7));
            fExpectInt("blast on its own caster: only the damage", lOLog.Count, 1);

            FakeWorld lOWorld = new FakeWorld();

            lOWorld.DestroyedCreatures.Add(7);
            lOWorld.Damage(lOCreature, UWBlow.CreatureBlow(5, 9));
            lOWorld.Damage(lOCreature, UWBlow.Missile(5, UWDamageTypes.Missile, 9));
            fExpectInt("a destroyed creature: no attacker, no damage", lOWorld.Blows.Count, 0);

            lOLog = fLand(UWDamageTarget.ThePlayer, UWBlow.Trap(5, UWDamageTypes.None));
            fExpectBool("trap on the player: his hit points, untyped", lOLog.Count == 1 && lOLog[0] == "player takes 5 type 0", true);

            lOLog = fLand(UWDamageTarget.ThePlayer, UWBlow.Curse(3));
            fExpectBool("curse: the player's curse with its dice", lOLog.Count == 1 && lOLog[0] == "player cursed 3", true);

            lOLog = fLand(UWDamageTarget.ThePlayer, UWBlow.Backfire(2));
            fExpectBool("backfire: the player's backfire with its dice", lOLog.Count == 1 && lOLog[0] == "player backfire 2", true);

            lOLog = fLand(lOCreature, UWBlow.Curse(3));
            fExpectInt("curse on a creature: nothing", lOLog.Count, 0);

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
