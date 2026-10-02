namespace UWDataImport.UWData
{
	/// <summary>
	/// Damage types and the resistances against them (2026-09-06).
	///
	/// Every object carries a resistance byte in comobj.dat, byte 8. Until now we had
	/// not read it. The meaning of the bits comes from the reference and is confirmed by the data
	/// itself - here the creatures for which anything is set at all:
	///
	///   Skeleton, ghoul, dark ghoul       0x80  undead
	///   Ghost, dread spirit               0x90  undead and poison-proof
	///   Fire elemental (and object 123)   0x08  fireproof
	///   Object 124                        0x7F  immune to everything, magic level 3
	///
	/// All other 60 creatures carry zero. Exactly the undead have the undead bit, and the
	/// ghost is the only one that is poison-proof - that fits every expectation and is thus an
	/// independent confirmation of the bit layout, not merely a copy.
	///
	/// THE UNDEAD BIT is not a resistance type in the proper sense: the original queries it with
	/// damage type 0x80 to find out WHETHER something is undead (Smite Undead, Repel
	/// Undead). A "resistance against it" here simply means "yes, undead".
	///
	/// The lower two bits are not a yes/no marker, but a LEVEL from zero to three:
	/// against magic damage a roll is made instead of a flat rejection. In uw1 only
	/// object 124 has a value there, so the rolling practically never happens.
	///
	/// THE PLAYER CARRIES NOTHING IN THIS TABLE (object 127, the adventurer, has the byte
	/// at zero). His resistances come exclusively from the spells and
	/// enchantments of class 3 - see UWArmourProtection and UWCharacter.DamageTypeProof.
	/// Five of them are documented by name in string block 6 and together give exactly
	/// this bit layout:
	///
	///   Minor class 5  Missile Protection          0x40
	///   Minor class 6  Flameproof                  0x08
	///   Minor class 7  Poison Resistance           0x10
	///   Minor class 8  Magic Protection            0x01   (magic level 1)
	///   Minor class 9  Greater Magic Protection    0x02   (magic level 2)
	///
	/// The two magic protection spells are thus the same level mechanism as above: one
	/// catches every third spell, the greater one two out of three, both together everything.
	/// </summary>
	public static class UWDamageTypes
	{
		/// <summary>No type given - no resistance is checked. Fallback value
		/// for all places where the type of damage is not yet determined.</summary>
		public const int None = 0x00;

		/// <summary>Pure magic. Sheet Lightning and the effect of the lightning bolt.</summary>
		public const int Magic = 0x03;

		/// <summary>
		/// The blow - weapons, claws, teeth. The type with which every melee attack
		/// arrives (reference: combat.AttackerAppliesFinalDamage calls with 4).
		/// </summary>
		public const int Physical = 0x04;

		/// <summary>Fire. Carries the magic bits too, so it is magical at the same time - that is how
		/// the reference has it (0x0B, not 0x08).</summary>
		public const int Fire = 0x0B;

		/// <summary>
		/// Fire WITHOUT the magic bits - lava and fire traps. The reference passes a plain
		/// eight at these places (motion_player, runetrap), only the fire spell uses 0x0B.
		///
		/// The difference is not hair-splitting: with 0x0B a magic protection would be allowed
		/// to roll against LAVA, and in the original it does not.
		/// </summary>
		public const int PlainFire = 0x08;

		public const int Poison = 0x10;

		public const int Cold = 0x23;

		/// <summary>
		/// Missiles - arrows, bolts, sling stones.
		///
		/// INFERRED, NOT COPIED: the reference computes a missile's type
		/// from the negated ammunition type and arrives at values like 0xFF - that is
		/// evidently misread. That 0x40 is the missile type, on the other hand, is certain, because the
		/// spell Missile Protection sets exactly this bit.
		/// </summary>
		public const int Missile = 0x40;

		/// <summary>No damage, but the QUESTION of whether the target is undead.</summary>
		public const int UndeadTest = 0x80;

		/// <summary>The magic bits - their presence triggers the level roll.</summary>
		private const int MagicBits = 0x03;

		/// <summary>
		/// Offsets the damage against the target's resistances. Zero means: it bounces off.
		///
		/// The sequence comes from the reference (ScaleDamageUW1) and is closely rebuilt:
		///
		/// 1. If the damage type touches none of the set resistance bits, it goes through
		///    in full. That is the normal case - the vast majority of beings carry none at all.
		/// 2. If the damage type carries magic bits, a roll is made against the magic level: a roll
		///    from zero to two. If it is below the level, everything bounces off. Otherwise the
		///    magic bits are dropped and it continues with the rest - so fire stays fire, even
		///    if the magic was caught.
		/// 3. If a resistance bit is still affected after that, the blow bounces off.
		///
		/// UW1 has NO doubling for vulnerability; that only exists in uw2.
		/// </summary>
		/// <param name="piResistances">Byte 8 from comobj.dat, see
		/// UWCommonObjectProperties.Entry.Resistances.</param>
		public static int Scale(int piResistances, int piBaseDamage, int piDamageType)
		{
			if (piDamageType == None)
				return piBaseDamage;

			if ((piResistances & piDamageType) == 0)
				return piBaseDamage;

			if ((piDamageType & MagicBits) != 0)
			{
				if (UWRandom.Next(0, 3) < (piResistances & MagicBits))
					return 0;

				piDamageType &= ~MagicBits;
			}

			return (piResistances & piDamageType) != 0 ? 0 : piBaseDamage;
		}

		/// <summary>Whether the target is undead. The original asks this question the same
		/// way as a resistance - see UndeadTest.</summary>
		public static bool IsUndead(int piResistances)
		{
			return (piResistances & UndeadTest) != 0;
		}
	}
}
