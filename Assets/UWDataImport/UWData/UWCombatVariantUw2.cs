using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S COMBAT STEPS (UW2 plan phase 4), from Underworld Exhumed's uw2 combat/COMBAT.C and DAMAGE.C.
	/// The special weapons' powers fire after a hit (SpecialWeapon, PowerFires; the deeds in UWAttackUw2.WeaponPowerOnHit).
	/// </summary>
	public sealed class UWCombatVariantUw2 : IUWCombatVariant
	{
		private const int WeaponEnchantmentClass = 0xC;

		private const int LowEnchantments = 4;

		private const int PlainEnchantments = 8;

		/// <summary>specweap 8's damage: spec_damage[8] reads the byte after the eight zeros, COMBAT.C:59.</summary>
		private const int LastSpecialEffect = 15;

		private const int LastSpecialDamage = 5;

		/// <summary>check_res' fire and cold bits, in the resist byte and in a damage type.</summary>
		private const int Fire = 8;

		private const int Cold = 0x20;

		private const int MostDamage = 0xFF;

		private const int LastDoubled = 0x7F;

		/// <summary>debris_type's pile of debris, ITEM_PILE_OF_DEBRIS_D6.</summary>
		private const int DebrisPile = 0xD6;

		private const int WoodChips = 0xDC;

		private const int BrokenDagger = 0xC7;

		/// <summary>A broken weapon is its skill's: Weapons[].skill + 0xC5, a sword 0xC8.</summary>
		private const int FirstBrokenWeapon = 0xC5;

		private const int Dagger = 3;

		private const int FurnitureClass = 0x15;

		/// <summary>Pure fire leaves no broken weapon (DAMAGE.C:114).</summary>
		private const int PureFire = 8;

		/// <summary>player_weapon: bare hands, a weapon that is not sharp, a sharp one.</summary>
		public const int FistWeapon = 0;

		public const int BluntWeapon = 1;

		public const int SharpWeapon = 2;

		/// <summary>Items 0x10 and up are no weapons to swing; the cudgel and the two after it are blunt.</summary>
		private const int WeaponClassEnd = 0x10;

		private const int FirstBluntWeapon = 7;

		private const int BluntWeaponEnd = 10;

		private const int SharpHitSound = 0x1B;

		/// <summary>The armour on a part of the player (UWVitalsUw2): the poisonous blow's roll reads it.</summary>
		public static Func<int, int> PlayerArmour;

		/// <summary>An effect below 4 adds 2e + 1 to the damage, from 4 to 7 2e - 7 to the attack score; 8 and up
		/// are the special weapons, their tables with the player's blows (COMBAT.C:641).</summary>
		public void WeaponEnchantment(int piMajor, int piMinor, out int piAccuracy, out int piDamage)
		{
			piAccuracy = 0;
			piDamage = 0;

			if (piMajor != WeaponEnchantmentClass)
				return;

			if (piMinor >= PlainEnchantments)
			{
				if (piMinor == LastSpecialEffect)
					piDamage = LastSpecialDamage;

				return;
			}

			if (piMinor < LowEnchantments)
				piDamage = (piMinor * 2) + 1;
			else
				piAccuracy = (piMinor * 2) - 7;
		}

		/// <summary>DoPlayerWeapon's specweap: effect - 7 for a weapon enchantment of effect 8 and up, 0 otherwise.</summary>
		public int SpecialWeapon(int piMajor, int piMinor)
		{
			return piMajor == WeaponEnchantmentClass && piMinor >= PlainEnchantments ? piMinor - (PlainEnchantments - 1) : 0;
		}

		// ------------------------------------------------- The special weapons' powers (player_attack, COMBAT.C:790)

		/// <summary>Heals the player by the damage done (get_hp_back).</summary>
		public const int HealPower = 1;

		/// <summary>sp_ward_undead on the target.</summary>
		public const int WardUndeadPower = 2;

		/// <summary>A fireball on the target's square: the explosion, its flames and damage_square kind 1.</summary>
		public const int FireballPower = 3;

		/// <summary>sp_hold on the target.</summary>
		public const int HoldPower = 4;

		/// <summary>On a door: the Open spell's lock pick (obj_spells 0xC), then OpenDoor.</summary>
		public const int OpenDoorPower = 5;

		/// <summary>As OpenDoorPower, and it fires on every hit, not only a critical.</summary>
		public const int EveryHitPower = 6;

		/// <summary>
		/// WHETHER THE POWER FIRES after a hit (player_attack: do_attack() and specweap and (criti or specweap == 6)). criti
		/// is UWAttackResolution.LastCritical, the original's flag: on a door it keeps the last creature roll's value, so
		/// power 5 opens a door only when that happens to be set (as the original; no shipped weapon has power 5).
		/// </summary>
		public static bool PowerFires(int piPower, bool pbCritical)
		{
			return piPower > 0 && (pbCritical || piPower == EveryHitPower);
		}

		/// <summary>rand() % (poison + 6) must beat twice the armour at the hit (COMBAT.C:911).</summary>
		public bool PoisonGetsThrough(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome)
		{
			int liArmour = PlayerArmour != null ? PlayerArmour(pOOutcome.Part) : 0;

			return UWRandom.Next(pOAttacker.PoisonByte + 6) > liArmour * 2;
		}

		/// <summary>Fire on a creature resisting cold, or cold on one resisting fire but not cold, doubled up to
		/// 255 (DAMAGE.C:225).</summary>
		public int Unresisted(int piResistances, int piDamage, int piDamageType)
		{
			bool lbFireOnCold = (piDamageType & Fire) != 0 && (piResistances & Cold) != 0;
			bool lbColdOnFire = (piDamageType & Cold) != 0 && (piResistances & Fire) != 0 && (piResistances & (Fire | Cold)) != (Fire | Cold);

			if (!lbFireOnCold && !lbColdOnFire)
				return piDamage;

			return piDamage < LastDoubled ? piDamage << 1 : MostDamage;
		}

		public bool ShrugsOff(bool pbProtected, int piQualityClass)
		{
			return pbProtected || piQualityClass >= UWObjectDamageRules.ImmuneQualityClass;
		}

		/// <summary>The bit spares everything but a door (DAMAGE.C:265).</summary>
		public bool DoorsTakeDamage => true;

		private const int ThrownSound = 0x1C;

		private const int SpaceSound = 0x2D;

		private const int Sling = 0x18;

		private const int Bow = 0x19;

		private const int Crossbow = 0x1A;

		public int ThrowSound => ThrownSound;

		/// <summary>player_fire: using_bow for weapons 8, 9 and 10.</summary>
		public int ShotSound(int piLauncherId)
		{
			return piLauncherId == Sling || piLauncherId == Bow || piLauncherId == Crossbow ? UWSoundEffectId.Miss : ThrownSound;
		}

		public bool Twangs(int piLauncherId, bool pbFired)
		{
			return pbFired && (piLauncherId == Bow || piLauncherId == Crossbow);
		}

		public int NoRoomSound => SpaceSound;

		public int NoRoomToFireString => 0x10E + 1;

		public bool MissileDamageSounds => false;

		/// <summary>mts_doanim plays no sound (per user 2026-11-12 on the original: a lightning bolt's impact sounds as a
		/// magic arrow's, ours played the sword's clank on a wall).</summary>
		public int OwnMissileImpactSound => UWAttackResolution.NoSound;

		/// <summary>spell_fire: 0x28 on the missile, 0xA for the acid (0x16) and the snowball (0x1C); a creature's spell
		/// played the miss's 0xA until 2026-11-12 (per user: the imps' Magic Arrow sounded like a missed blow).</summary>
		public int SpellMissileLaunchSound(int piProjectileId) => piProjectileId == 0x16 || piProjectileId == 0x1C ? UWSoundEffectId.Miss : 0x28;

		/// <summary>See IUWCombatVariant.MissileLaunchVolume.</summary>
		public int MissileLaunchVolume(int piLaunchSound, int piProjectileId)
		{
			if (piLaunchSound == ThrowLaunchSound)
				return 0;

			if (piLaunchSound == UWSoundEffectId.Miss && (piProjectileId == 0x16 || piProjectileId == 0x1C))
				return 0;

			return LaunchVolume;
		}

		/// <summary>missile_fire's own sound for a throw (neither bow nor spell): 0x1C at its plain volume.</summary>
		private const int ThrowLaunchSound = 0x1C;

		private const int LaunchVolume = 0x14;

		/// <summary>fill_FB(0x23) on a critical against the player (COMBAT.C:332).</summary>
		public const int CriticalFlash = 0x23;

		public int CriticalFlashColour => CriticalFlash;

		/// <summary>The pile, for what names no item.</summary>
		public int RollDebris()
		{
			return DebrisPile;
		}

		/// <summary>debris_type (DAMAGE.C:190).</summary>
		public int DebrisOf(int piItem, int piDamageType, DataImport pOData)
		{
			if ((piItem >> 4) == 0 && piDamageType != PureFire)
			{
				if (piItem == Dagger)
					return BrokenDagger;

				if (pOData != null && pOData.ObjectClassProperties != null
					&& pOData.ObjectClassProperties.TryGetMeleeWeapon(piItem, out UWObjectClassProperties.MeleeWeapon lOMelee))
					return lOMelee.SkillType + FirstBrokenWeapon;
			}

			return (piItem >> 4) == FurnitureClass ? WoodChips : DebrisPile;
		}

		/// <summary>remove_object sets hp and quality of the new debris to 0x28 (DAMAGE.C:178-180).</summary>
		public int DebrisQuality => 0x28;

		/// <summary>GetPlayerWeapon and is_sharp (COMBAT.C:275): the missile class is no melee weapon.</summary>
		public int PlayerWeaponKind(int piWeaponId)
		{
			if (piWeaponId < 0 || piWeaponId >= WeaponClassEnd - 1)
				return FistWeapon;

			return piWeaponId >= FirstBluntWeapon && piWeaponId < BluntWeaponEnd ? BluntWeapon : SharpWeapon;
		}

		public int MeleeHitSound(int piWeaponKind)
		{
			return piWeaponKind == SharpWeapon ? SharpHitSound : UWSoundEffectId.HitCritter;
		}

		public int OtherHitSound(int piPlayersLastWeaponKind)
		{
			return MeleeHitSound(piPlayersLastWeaponKind);
		}

		/// <summary>GetPlayerWeapon's other kinds: a bow or sling with its ammunition (3), without (4).</summary>
		public const int LauncherWeapon = 3;

		public const int LauncherWithoutAmmunition = 4;

		public int WallSound(int piWeaponKind)
		{
			return piWeaponKind == BluntWeapon || piWeaponKind == SharpWeapon ? UWCritterCombat.BlockedMetalSound : UWCritterCombat.BlockedSound;
		}

		public bool MissSoundsAtWall => false;

		public int BlockedSound(int piWeaponKind, bool pbArmourMetal)
		{
			return piWeaponKind == BluntWeapon || (piWeaponKind == SharpWeapon && pbArmourMetal)
				? UWCritterCombat.BlockedMetalSound : UWCritterCombat.BlockedSound;
		}
	}
}
