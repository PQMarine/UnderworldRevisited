namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF COMBAT WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0 step 2,
	/// 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "Combat"). The rules keep their flow and ask here; what only the second game has (Valor's
	/// bonus, Poison Weapon, the special weapons, the combat sounds by the weapon's kind) comes with
	/// its variant in phase 4. The numbers that differ are rule set entries (UWRulesCombat).
	/// </summary>
	public interface IUWCombatVariant
	{
		/// <summary>What a weapon's enchantment of this class adds to the attack score and to the
		/// damage (UW1: class 0xC, (minor &amp; 7) + 1 to one of them by bit 3, COMBAT.C:594; UW2: by
		/// the effect's range, and special weapons from 8, COMBAT.C:641).</summary>
		void WeaponEnchantment(int piMajor, int piMinor, out int piAccuracy, out int piDamage);

		/// <summary>Whether a creature's poisonous hit gets past the player's armour (UW1: always, the
		/// resistance alone decides, COMBAT.C:808; UW2: a roll against twice the armour first,
		/// COMBAT.C:911).</summary>
		bool PoisonGetsThrough(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome);

		/// <summary>The damage of a type the target resists not at all (check_res; UW1: as it is,
		/// DAMAGE.C:166; UW2: fire on a creature resisting cold, or cold on one resisting fire but not
		/// cold, doubled up to 255, DAMAGE.C:225).</summary>
		int Unresisted(int piResistances, int piDamage, int piDamageType);

		/// <summary>damage_object's first test: whether an object takes nothing at all: the door direction
		/// bit or quality class 3 (DAMAGE.C:213).</summary>
		bool ShrugsOff(bool pbProtected, int piQualityClass);

		/// <summary>Whether the door direction bit spares a door (UW1: yes; UW2: only others, DAMAGE.C:265).</summary>
		bool DoorsTakeDamage { get; }

		/// <summary>The debris a destroyed object becomes (UW1: one of two at random, DAMAGE.C:140;
		/// UW2: debris_type and the broken weapons, DAMAGE.C:117 and :174).</summary>
		int RollDebris();

		/// <summary>What a destroyed thing leaves (UW1: RollDebris, DAMAGE.C:140; UW2: debris_type, the broken
		/// weapon of its skill, wood chips or the pile, DAMAGE.C:190).</summary>
		int DebrisOf(int piItem, int piDamageType, DataImport pOData);

		/// <summary>The quality (and, in flight, the hit points) of the debris a thing becomes (UW1: the 0 the
		/// wear left, UWObjectDamageRules.DebrisQuality, remove_object sets none; UW2: 0x28, DAMAGE.C:178-180).</summary>
		int DebrisQuality { get; }

		/// <summary>The kind of what the player swings for the sounds (UW2 player_weapon, 0 fist, 1 blunt, 2 sharp;
		/// UW1: -1, its own row's kinds answer).</summary>
		int PlayerWeaponKind(int piWeaponId);

		/// <summary>The sound of a blow that landed (UW1: 4; UW2: by the kind, do_damage).</summary>
		int MeleeHitSound(int piWeaponKind);

		/// <summary>The sound of a blow that met a wall (UW1: 7; UW2: 7 with a weapon, else 8, find_wall_coll).</summary>
		int WallSound(int piWeaponKind);

		/// <summary>Whether the swing's sound follows a blow that met a wall (UW2's do_miss skips it).</summary>
		bool MissSoundsAtWall { get; }

		/// <summary>The blocked blow's sound (do_miss; UW2 sounds a blunt weapon as metal on anything).</summary>
		int BlockedSound(int piWeaponKind, bool pbArmourMetal);

		/// <summary>The sound of a thing thrown (UW1: 0xA, as every missile; UW2: 0x1C, missile_fire).</summary>
		int ThrowSound { get; }

		/// <summary>The sound of a shot (UW1: 0xA; UW2: 0xA for sling, bow and crossbow, player_fire; the
		/// jeweled bow is none of them to using_bow and sounds as a throw).</summary>
		int ShotSound(int piLauncherId);

		/// <summary>Whether this launcher twangs (UW1: bow and crossbow, also when there was no room; UW2: only
		/// when the shot flew, MISSILE.C:103).</summary>
		bool Twangs(int piLauncherId, bool pbFired);

		/// <summary>The sound of a shot or a drop that found no room, UWAttackResolution.NoSound for none at the
		/// shot (UW1; UW2: 0x2D, MISSILE.C:119).</summary>
		int NoRoomSound { get; }

		/// <summary>"You need more space to fire that weapon." in our block 1.</summary>
		int NoRoomToFireString { get; }

		/// <summary>Whether do_damage's louder sound follows a missile's on a creature or a thing (UW1: yes; UW2:
		/// missile_hit keeps it off, COMBAT.C:395).</summary>
		bool MissileDamageSounds { get; }

		/// <summary>The sound of the player's own missile where its impact picture appears - a fireball's explosion,
		/// a lightning bolt's flash (UW1: effect 7, SpawnImpactAnimo; UW2: none, mts_doanim plays nothing and effect 7
		/// is the sword's clank on a wall there, uw2 EFFECT.C). UWAttackResolution.NoSound for none.</summary>
		int OwnMissileImpactSound { get; }

		/// <summary>The sound a spell's missile leaves with, by its object (UW1: missile_fire's 0xA for every missile;
		/// UW2: spell_fire's 0x28, the acid and the snowball 0xA - for the player and the creatures alike, MISSILE.C).</summary>
		int SpellMissileLaunchSound(int piProjectileId);
	}
}
