using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The rune spells: which rune sequence yields which spell.
	///
	/// ORIGIN: the table comes from UnderworldGodot, where it was disassembled from the
	/// original. Only pure table values are adopted, no derived logic -
	/// that is the part of this source that is reliable.
	///
	/// Two independent checks from the game data itself: spell 0 carries runes 8 and 11,
	/// i.e. In Lor - the light. Spell 5 carries 20 and 15, i.e. Uus Por, and string block 3
	/// says "Uus Por for large jumps".
	///
	/// The rune sequence is stored as one number with five bits per slot:
	/// (first &lt;&lt; 10) + (second &lt;&lt; 5) + third. An empty slot is 24 - directly
	/// after Ylem, the last rune. The five entries with sequence 25368 are thus
	/// empty three times: these are spells that CANNOT be cast via runes (they live in
	/// enchanted items) and are skipped during lookup.
	/// </summary>
	public static class UWRunicMagic
	{
		/// <summary>The value of an empty slot in the rune sequence.</summary>
		public const int EmptyRune = 24;

		/// <summary>The sequence of three empty slots. Entries with this value are not
		/// rune spells.</summary>
		public const int EmptySequence = (EmptyRune << 10) + (EmptyRune << 5) + EmptyRune;

		/// <summary>The mana a spell costs per circle: In Lor, circle 1, costs 3 (per user, compared
		/// with the original 2026-09-03).</summary>
		public const int ManaPerCircle = 3;

		public struct Spell
		{
			public readonly int Index;

			public readonly int MajorClass;

			public readonly int MinorClass;

			public readonly int RuneSequence;

			public Spell(int piIndex, int piMajorClass, int piMinorClass, int piRuneSequence)
			{
				Index = piIndex;
				MajorClass = piMajorClass;
				MinorClass = piMinorClass;
				RuneSequence = piRuneSequence;
			}

			/// <summary>The spell circle, one to eight. Every six spells share one.</summary>
			public int Level
			{
				get
				{
					int liLevel = 1 + (Index / 6);

					return liLevel > 8 ? 1 : liLevel;
				}
			}

			/// <summary>The circle times the rule set's magic.manaPerCircle (ManaPerCircle by
			/// default; cleanup phase 10).</summary>
			public int ManaCost
			{
				get { return Level * UWRules.Current.Magic.ManaPerCircle; }
			}
		}

		private static readonly Spell[] mOSpells =
		{
			new Spell(0, 0, 131, 8568),
			new Spell(1, 2, 2, 1298),
			new Spell(2, 5, 1, 14648),
			new Spell(3, 8, 1, 8599),
			new Spell(4, 3, 2, 18680),
			new Spell(5, 1, 1, 20984),
			new Spell(6, 3, 1, 600),
			new Spell(7, 1, 2, 17519),
			new Spell(8, 4, 2, 8236),
			new Spell(9, 11, 1, 22936),
			new Spell(10, 7, 1, 16472),
			new Spell(11, 8, 3, 8504),
			new Spell(12, 11, 0, 18031),
			new Spell(13, 3, 3, 1611),
			new Spell(14, 0, 133, 16760),
			new Spell(15, 5, 2, 14552),
			new Spell(16, 11, 2, 18744),
			new Spell(17, 2, 67, 8792),
			new Spell(18, 1, 68, 24056),
			new Spell(19, 4, 4, 8600),
			new Spell(20, 1, 3, 7672),
			new Spell(21, 7, 4, 13720),
			new Spell(22, 3, 70, 18616),
			new Spell(23, 11, 3, 312),
			new Spell(24, 5, 3, 15544),
			new Spell(25, 7, 2, 76),
			new Spell(26, 11, 4, 15063),
			new Spell(27, 3, 5, 6735),
			new Spell(28, 11, 5, 4856),
			new Spell(29, 11, 6, 440),
			new Spell(30, 4, 15, 21772),
			new Spell(31, 6, 66, 21958),
			new Spell(32, 11, 10, 22063),
			new Spell(33, 7, 5, 143),
			new Spell(34, 0, 134, 21771),
			new Spell(35, 11, 8, 14839),
			new Spell(36, 1, 5, 21743),
			new Spell(37, 7, 3, 8593),
			new Spell(38, 8, 4, 10648),
			new Spell(39, 3, 68, 22091),
			new Spell(40, 6, 3, 21526),
			new Spell(41, 6, 129, 14352),
			new Spell(42, 2, 69, 8882),
			new Spell(43, 11, 9, 22007),
			new Spell(44, 11, 7, 14838),
			new Spell(45, 6, 68, 5368),
			new Spell(46, 11, 11, 632),
			new Spell(47, 11, 12, 21826),
			new Spell(48, 6, 5, 25368),
			new Spell(49, 5, 4, 25368),
			new Spell(50, 11, 13, 25368),
			new Spell(51, 10, 3, 25368),
			new Spell(52, 10, 9, 25368),
		};

		/// <summary>
		/// Icon number in SPELLS.GR per major class, adopted from the reference. 0x80 means:
		/// no icon. The result is offset plus minor class.
		///
		/// Check: Light is major class 0, minor class 3, so 0x0E + 3 = 17 - and image 17 in
		/// SPELLS.GR is the torch.
		///
		/// The MINUS ONE for class 1 is a real offset, not a missing value: the
		/// movement spells carry minor classes one to five and thus land on
		/// images zero to four. Until 2026-09-03 we read it as "no icon" -
		/// that is why no movement spell showed an icon.
		/// </summary>
		private static readonly int[] miIconOffsets =
		{
			0x0E, -1, 0x0D, 0x04, 0x80, 0x80, 0x80, 0x80,
			0x80, 0x80, 0x80, 0x0B, 0x80, 0x80, 0x80, 0x80
		};

		/// <summary>
		/// Which object a spell projectile of major class 5 is, by its minor class.
		///
		/// The reference lists the values 7, 5, 4, 6 and adds sixteen. Each of the four
		/// numbers has been cross-checked against our own data: 23 is "a magic missile",
		/// 21 "a lightning bolt", 20 "a fireball", 22 "acid" (string block 4).
		/// </summary>
		private static readonly int[] miProjectileIds = { 23, 21, 20, 22 };

		/// <summary>Object number of the projectile, or -1 if there is none for this
		/// minor class.</summary>
		public static int GetProjectileId(int piMinorClass)
		{
			int liIndex = piMinorClass - 1;

			return liIndex >= 0 && liIndex < miProjectileIds.Length ? miProjectileIds[liIndex] : -1;
		}

		/// <summary>
		/// What a projectile is replaced with on impact, or -1 for nothing.
		///
		/// The reference keeps two tables for this: only fireball (20) and
		/// lightning bolt (21) are replaced. The MAGIC MISSILE is NOT listed there - it hits without
		/// an effect, and that is exactly how it is in the original (per user, 2026-09-04).
		///
		/// 450 is "an explosion" in string block 4 and fits. The 453 for the lightning bolt
		/// also comes from the reference, but for us it is "a splash" - unclear whether the
		/// number or our offset is wrong there. Stays in until someone looks at it.
		/// </summary>
		public static int GetProjectileImpactId(int piProjectileId)
		{
			switch (piProjectileId)
			{
				case 20:
					return 450;

				case 21:
					return 453;

				default:
					return -1;
			}
		}

		/// <summary>
		/// Which damage type a projectile's hit deals - used to decide whether the target resists
		/// it (see UWDamageTypes).
		///
		/// FROM THE TABLE (missile_newhit, UnderworldExhumed OBJPHYS.C, read 2026-10-12): the hit
		/// passes the negated third byte of the projectile's OBJECTS.DAT ranged entry, read as a
		/// signed byte, to missile_thwack as an unsigned char. The four spell projectiles carry
		/// (UW1 data, read 2026-10-12): fireball 0xF5 -> 0x0B (magic and fire), lightning bolt
		/// 0xDD -> 0x23 (magic and the cold bit), acid 0xF0 -> 0x10 (poison), magic missile 0xBD
		/// -> 0x43 (magic and missile); the physical missiles 0xC0 -> 0x40. Until 2026-10-12 ours
		/// assumed fire and plain magic for the first two from the area spells' table and nothing
		/// for Magic Arrow and Acid. The impact square's damage is separate (UWObjectDamageRules).
		/// </summary>
		public static int GetProjectileDamageType(int piProjectileId, UWObjectProperties pOProperties)
		{
			if (pOProperties == null)
				return UWDamageTypes.None;

			int liAmmo = unchecked((sbyte)pOProperties.GetRangedType(piProjectileId));

			return unchecked((byte)-liAmmo);
		}

		/// <summary>From this value on there is no icon.</summary>
		public const int NoIcon = 0x80;

		public static int GetIconIndex(int piMajorClass, int piMinorClass)
		{
			if (piMajorClass < 0 || piMajorClass >= miIconOffsets.Length)
				return NoIcon;

			int liOffset = miIconOffsets[piMajorClass];

			if (liOffset >= NoIcon)
				return NoIcon;

			int liIcon = liOffset + piMinorClass;

			return liIcon < 0 || liIcon >= NoIcon ? NoIcon : liIcon;
		}

		/// <summary>The minor class carries in its upper bits how long the spell lasts.
		/// The lower six bits remain as the actual minor class.</summary>
		public static int GetEffectMinor(int piMinorClass)
		{
			return piMinorClass & 0x3F;
		}

		public static int GetStabilityClass(int piMinorClass)
		{
			return piMinorClass & 0xC0;
		}

		/// <summary>
		/// For the AREA SPELLS (major class 6) the same upper two bits do not carry the
		/// duration but the TARGET TYPE - what the effect applies to at all:
		///
		///   0x00  creatures only                          (Confusion)
		///   0x40  tiles only                              (Sheet Lightning, Flame Wind)
		///   0x80  tiles and everything lying on them       (Reveal)
		///
		/// Same bits, two meanings - the major class decides which applies. The
		/// separate method exists so that the call site shows which of the
		/// two is meant. See UWAreaSpell.
		/// </summary>
		public static int GetAreaTargetType(int piMinorClass)
		{
			return piMinorClass & 0xC0;
		}

		/// <summary>The name of an active spell is in string block 6 from entry 385,
		/// counted by the ICON number - not by the spell number. Check: Light has
		/// icon 17, and [6:402] is "Light".</summary>
		public static string GetEffectName(int piIconIndex, UWStrings pOStrings)
		{
			try
			{
				return pOStrings.Blocks[6].Strings[385 + piIconIndex].Trim();
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>All entries of the table, in their order.</summary>
		public static IReadOnlyList<Spell> AllSpells
		{
			get { return mOSpells; }
		}

		/// <summary>The 24 runes in rune number order (the alphabet of the rune bag).</summary>
		public static readonly string[] RuneNames =
		{
			"An", "Bet", "Corp", "Des", "Ex", "Flam", "Grav", "Hur",
			"In", "Jux", "Kal", "Lor", "Mani", "Nox", "Ort", "Por",
			"Quas", "Rel", "Sanct", "Tym", "Uus", "Vas", "Wis", "Ylem"
		};

		/// <summary>The runes of a sequence as names, "In Lor"; empty for a spell without
		/// runes.</summary>
		public static string DescribeRunes(int piSequence)
		{
			string lsResult = string.Empty;

			for (int liShift = 10; liShift >= 0; liShift -= 5)
			{
				int liRune = (piSequence >> liShift) & 0x1F;

				if (liRune >= RuneNames.Length)
					continue;

				lsResult += (lsResult.Length > 0 ? " " : string.Empty) + RuneNames[liRune];
			}

			return lsResult;
		}

		/// <summary>Encodes three rune slots into a sequence. Empty slots as EmptyRune.</summary>
		public static int GetSequence(int piFirst, int piSecond, int piThird)
		{
			return (piFirst << 10) + (piSecond << 5) + piThird;
		}

		/// <summary>Looks up the spell for a rune sequence. Returns false if there is none -
		/// the original then reports "Not a spell".</summary>
		public static bool TryGetSpell(int piSequence, out Spell pOSpell)
		{
			pOSpell = default(Spell);

			if (piSequence == EmptySequence)
				return false;

			foreach (Spell lOSpell in mOSpells)
			{
				if (lOSpell.RuneSequence != piSequence || lOSpell.RuneSequence == EmptySequence)
					continue;

				pOSpell = lOSpell;

				return true;
			}

			return false;
		}

		/// <summary>The name of a spell is in string block 6 from entry 256 in the game's numbering,
		/// i.e. 257 plus the spell number in our parsed blocks, which lie one entry higher.</summary>
		public static string GetName(Spell pOSpell, UWStrings pOStrings)
		{
			try
			{
				return pOStrings.Blocks[6].Strings[257 + pOSpell.Index].Trim();
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
