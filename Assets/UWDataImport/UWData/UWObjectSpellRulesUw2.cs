using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TARGET SPELLS ON A THING (UW2 plan G1 part D2, 2026-11-12; Underworld Exhumed uw2 combat/SPELLS.C
	/// obj_spells, SPELLS2.C sp_study_monster and mendable, game/SKILLS.C DetectedTrap and RemoveTrap): class 7 minors 7 to 15,
	/// on the thing the player clicks in the target mode (UWAttackUw2), in the world or in the inventory.
	///
	///   minor  spell              what it does                                                       mana when
	///   7      Study Monster      kind, vitality, poison, spells, resistances (target_spells)        it took
	///   8      Dispel Rune        a rune of flame or stasis goes; "That is not a rune."              a rune
	///   9      Mending            a mendable thing to quality 63; "...no noticeable effect."         it mended
	///   10     Remove Trap        a trap found with 45 is disarmed with 45                           always
	///   11     Name Enchantment   a look with full lore, the thing marked identified                 always
	///   12     Open               checkLock with 45: "The spell unlocks the lock." or not            always
	///   13     Detect Trap        "You have detected a trap." or "You detect no traps."              always
	///   14     Enchantment        sp_enchant: a tier, charges or the blast (D2b)
	///   15     Gate Travel        on a moonstone: to the other one                                   a moonstone
	///
	/// This class holds the rules that need no engine; UWObjectSpellUw2 (the Unity side) runs them on the clicked thing.
	/// </summary>
	public static class UWObjectSpellRulesUw2
	{
		public const int StudyMonster = UWTargetSpellRulesUw2.StudyMonster;

		public const int DispelRune = 8;

		public const int Mending = 9;

		public const int RemoveTrap = 10;

		public const int NameEnchantment = 11;

		public const int Open = 12;

		public const int DetectTrap = 13;

		public const int Enchantment = 14;

		public const int GateTravel = 15;

		/// <summary>D2, and Enchantment since D2b (UWEnchantSpellRulesUw2).</summary>
		public static bool IsBuilt(int piMinor)
		{
			return piMinor >= StudyMonster && piMinor <= GateTravel;
		}

		// ------------------------------------------------- Strings (block 1 by the project's numbering, original + 1)

		public const int NotARuneString = 0x12E + 1;

		/// <summary>"The spell repairs the " - then the thing's name and string 0x60.</summary>
		public const int RepairsString = 0x12F + 1;

		public const int NoNoticeableEffectString = 0x142 + 1;

		public const int UnlocksString = 0x11E + 1;

		public const int NoDiscernableEffectString = 0x11F + 1;

		public const int DetectedTrapString = 0x130 + 1;

		public const int NoTrapsString = 0x131 + 1;

		public const int NotAMoonstoneString = 0x143 + 1;

		public const int MoonstoneNotAvailableString = 0x121 + 1;

		public const int PeriodString = 0x60 + 1;

		/// <summary>" was successfully disarmed." after "The trap on the thing".</summary>
		public const int DisarmedString = 0x168 + 1;

		/// <summary>"Your bumbling attempts have set off the " - then the trap's name and the period.</summary>
		public const int SetOffString = 0x166 + 1;

		public const int UnableToDefuseString = 0x167 + 1;

		/// <summary>'This creature is ', 'This powerful creature is ', 'This undead creature is ', 'This powerful undead
		/// creature is ' (0x135 + flags).</summary>
		public const int StudyFirstString = 0x135 + 1;

		public const int VitalityString = 0x139 + 1;

		public const int PoisonousString = 0x13C + 1;

		public const int ResistantString = 0x13D + 1;

		public const int CanUseString = 0x144 + 1;

		/// <summary>The damage types' names, 0x146 magic ... 0x14B missiles.</summary>
		public const int DamageTypeNameString = 0x146 + 1;

		public const int AndString = 0x74 + 1;

		/// <summary>Block 6's 'Rune of Stasis' (string 0xD25: STR_SPELLS | 0x125).</summary>
		public const int RuneOfStasisSpellName = 0x125 + 1;

		// ------------------------------------------------- Things

		public const int FlameRuneId = 0x19E;

		public const int StasisRuneId = 0x19F;

		public const int MoonstoneId = 0x126;

		/// <summary>Study Monster's two things that are no creature: a skull (0x13) and a wisp.</summary>
		public const int SkullId = 0x13;

		public const int WispId = 0x1CD;

		/// <summary>Mending's quality.</summary>
		public const int MendedQuality = 0x3F;

		/// <summary>The skill the spells detect and disarm a trap with, and open a lock with (obj_spells: 0x2D, -45).</summary>
		public const int SpellSkill = 45;

		/// <summary>DetectedTrap's and RemoveTrap's trigger: a trap-class object of minor class 3 (MINOR_TRIGGER2).</summary>
		public const int SpellTriggerMinor = 3;

		private const int MajorHack = 0;

		private const int MajorMisc = 2;

		private const int MajorRect = 5;

		/// <summary>mendable: weapons and armour, the misc minor 1 (lights and wands; its test against the classes 0x90 and
		/// 0x94, meant for the lights, can never fail) and minor 3 (the food, 0xB0 to 0xBF: Mending freshens a loaf, per user
		/// 2026-11-12 "kann man Brot reparieren?" - yes, the original's rule), the doors.</summary>
		public static bool Mendable(int piObjectId)
		{
			int liMajor = UWObjectIds.Major(piObjectId);
			int liMinor = UWObjectIds.Minor(piObjectId);

			// The misc minor 1's test against the classes 0x90 and 0x94 can never fail (OBJ_CLASS is five bits).
			return liMajor == MajorHack || (liMajor == MajorMisc && (liMinor == 1 || liMinor == 3)) || (liMajor == MajorRect && liMinor == 0);
		}

		/// <summary>The world (0..9) of a dungeon level counted from 1: eight levels each.</summary>
		public static int WorldOf(int piDungeonLevel)
		{
			return System.Math.Max(0, piDungeonLevel - 1) / 8;
		}

		/// <summary>DetectedTrap's target: 10 and twice the world.</summary>
		public static int DetectTarget(int piDungeonLevel)
		{
			return (WorldOf(piDungeonLevel) << 1) + 10;
		}

		/// <summary>RemoveTrap's target: 8 and the world.</summary>
		public static int DisarmTarget(int piDungeonLevel)
		{
			return WorldOf(piDungeonLevel) + 8;
		}

		/// <summary>
		/// The trigger DetectedTrap and RemoveTrap look for: the first trap-class thing of minor class 3 in pOThing's chain
		/// (a thing with a quantity has none), and the trap it leads to - the trigger's own link, its resolved chain's first
		/// where it has one. False when there is none.
		/// </summary>
		public static bool TryFindTrap(UWObject pOThing, IList<UWObject> pORecords, out UWObject pOTrigger, out UWObject pOTrap)
		{
			pOTrigger = null;
			pOTrap = null;

			if (pOThing == null || pORecords == null || pOThing.HasQuantity || pOThing.Quantity == 0)
				return false;

			IEnumerable<UWObject> lOChain = pOThing.Contents != null ? (IEnumerable<UWObject>)pOThing.Contents
				: UWObjectChain.Walk(pOThing, pORecords, pORecords.Count);

			foreach (UWObject lOAt in lOChain)
			{
				if (lOAt == null || UWObjectIds.Major(lOAt.ID) != UWObjectMechanics.TrapMajorClass || UWObjectIds.Minor(lOAt.ID) != SpellTriggerMinor)
					continue;

				pOTrigger = lOAt;

				if (lOAt.Contents != null && lOAt.Contents.Count > 0)
					pOTrap = lOAt.Contents[0];
				else if (!lOAt.HasQuantity && lOAt.Quantity > 0 && lOAt.Quantity < pORecords.Count)
					pOTrap = pORecords[lOAt.Quantity];

				return true;
			}

			return false;
		}

		/// <summary>RemoveTrap's name of a trap: "trap" for inmajor 15, "poison trap" for inmajor 0 with an owner, else its
		/// name.</summary>
		public static string TrapName(DataImport pOData, UWObject pOTrap)
		{
			if (pOTrap == null)
				return "trap";

			int liInMajor = pOTrap.ID & 0x3F;

			if (liInMajor == 15)
				return "trap";

			if (liInMajor == 0 && pOTrap.Owner != 0)
				return "poison trap";

			return UWText.ObjectName(pOData, pOTrap.ID, false, false, "trap");
		}

		// ------------------------------------------------- Gate Travel

		public enum GateEnum
		{
			/// <summary>"The moonstone is not available." - both stones nowhere.</summary>
			NotAvailable,

			/// <summary>The placed ones are on this level: to the other moonstone here, else to the clicked one's square.</summary>
			ThisLevel,

			/// <summary>One was left on another level: there, where do_mstone finds it.</summary>
			OtherLevel
		}

		/// <summary>obj_spells' case 15 on the two moonstone records (the levels each was left on, 0 none): a stone on
		/// another level wins, the first such; none placed at all is not available.</summary>
		public static GateEnum DecideGate(int piFirst, int piSecond, int piPlayerLevel, out int piLevel)
		{
			int[] liStones = { piFirst, piSecond };
			int liNone = 0;

			piLevel = piPlayerLevel;

			for (int liAt = 0; liAt < 2; liAt++)
			{
				if (liStones[liAt] == piPlayerLevel)
					continue;

				if (liStones[liAt] != 0)
				{
					piLevel = liStones[liAt];

					return GateEnum.OtherLevel;
				}

				liNone++;
			}

			return liNone == 2 ? GateEnum.NotAvailable : GateEnum.ThisLevel;
		}

		// ------------------------------------------------- Study Monster

		/// <summary>check_res's damage types Study Monster names, in its order (dtypes): magic, physical, fire, poison, cold,
		/// missiles.</summary>
		public static readonly int[] StudyDamageTypes = { 3, 4, 8, 0x10, 0x20, 0x40 };

		/// <summary>The liches' race, which lists Rune of Stasis among its resistances and not fire.</summary>
		public const int LicheRace = UWTargetSpellRulesUw2.LicheRace;

		/// <summary>The whoami whose vitality Study Monster gives as 30 off the terrain (a special creature).</summary>
		public const int ThirtyVitalityWhoami = 100;

		/// <summary>
		/// sp_study_monster's lines, or null when pOTarget is neither a creature nor a skull or wisp (the spell does not
		/// take). The creature's resistances are rolled as check_res rolls them (UWDamageTypes.Scale of one point), so magic
		/// may show on one cast and not on the next.
		/// </summary>
		public static List<string> StudyLines(DataImport pOData, UWObject pOTarget, int piHitPoints)
		{
			if (pOData == null || pOTarget == null)
				return null;

			bool lbCreature = UWObjectIds.Major(pOTarget.ID) == UWObjectIds.CreatureMajorClass;
			int liFlags = 0;

			if (!lbCreature)
			{
				if (pOTarget.ID == SkullId)
					liFlags |= 2;
				else if (pOTarget.ID != WispId)
					return null;
			}

			int liResistances = 0;

			if (pOData.CommonObjectProperties != null && pOData.CommonObjectProperties.TryGet(pOTarget.ID, out UWCommonObjectProperties.Entry lOEntry))
				liResistances = lOEntry.Resistances;

			UWNpc lONpc = pOTarget as UWNpc;

			if (lbCreature)
			{
				if (lONpc != null && fIsPowerful(lONpc))
					liFlags |= 1;

				if (UWDamageTypes.IsUndead(liResistances))
					liFlags |= 2;
			}

			List<string> lOLines = new List<string>();
			string lsPeriod = UWText.General(pOData, PeriodString, ".");
			string lsName = UWText.ObjectName(pOData, pOTarget.ID, false, true);

			lOLines.Add(UWText.General(pOData, StudyFirstString + liFlags) + lsName + lsPeriod);

			int liVitality = !lbCreature ? 3 | pOTarget.Quality
				: lONpc != null && lONpc.NPCwhoami == ThirtyVitalityWhoami ? 0x1E : piHitPoints;

			lOLines.Add(UWText.General(pOData, VitalityString) + liVitality + lsPeriod);

			if (!lbCreature)
			{
				lOLines.Add(UWText.General(pOData, ResistantString) + UWText.General(pOData, DamageTypeNameString) + ", "
					+ UWText.General(pOData, DamageTypeNameString + 1) + lsPeriod);

				return lOLines;
			}

			UWObjectClassProperties.Critter lOCritter = default;
			bool lbKnown = pOData.ObjectClassProperties != null && pOData.ObjectClassProperties.TryGetCritter(pOTarget.ID, out lOCritter)
				&& lOCritter.Row != null;

			if (lbKnown && lOCritter.RowByte(0x0F) != 0)
				lOLines.Add(UWText.General(pOData, PoisonousString));

			string lsSpells = lbKnown ? fStudySpells(pOData, lOCritter, liResistances) : null;

			if (!string.IsNullOrEmpty(lsSpells))
				lOLines.Add(UWText.General(pOData, CanUseString) + lsSpells);

			int liRace = lbKnown ? lOCritter.Race : -1;
			StringBuilder lOResists = new StringBuilder();

			for (int liAt = 0; liAt < StudyDamageTypes.Length; liAt++)
			{
				if (UWDamageTypes.Scale(liResistances, 1, StudyDamageTypes[liAt]) != 0 || (StudyDamageTypes[liAt] == 8 && liRace == LicheRace))
					continue;

				if (lOResists.Length > 0)
					lOResists.Append(", ");

				lOResists.Append(UWText.General(pOData, DamageTypeNameString + liAt));
			}

			if (liRace == LicheRace)
				lOResists.Append(", ").Append(UWText.Block(pOData, 6, RuneOfStasisSpellName));

			if (lOResists.Length > 0)
				lOLines.Add(UWText.General(pOData, ResistantString) + lOResists + lsPeriod);

			return lOLines;
		}

		/// <summary>The spell table's heals (class 4): Lesser Heal, Heal, Greater Heal (SPELLS.C spells[]).</summary>
		public static readonly int[] HealSpellIndices = { 11, 25, 44 };

		/// <summary>OBJ_POWERFUL: the attitude word's bit 10.</summary>
		private static bool fIsPowerful(UWNpc pONpc)
		{
			return pONpc.RawNpcBytes != null && (UWBinary.Read16(pONpc.RawNpcBytes, UWNpcRecordCodec.AttitudeWord) & 0x400) != 0;
		}

		/// <summary>study_monster_spells: the caster's three spells (a liche's more by its flight, its locks, its fire
		/// resistance and its strength), the same one left out once, by name from block 6, "X, Y and Z."; null for none.</summary>
		private static string fStudySpells(DataImport pOData, UWObjectClassProperties.Critter pOCritter, int piResistances)
		{
			if ((pOCritter.RowByte(0x2D) >> 1) == 0)
				return null;

			int[] liSpells = { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

			for (int liAt = 0; liAt < 3; liAt++)
				liSpells[liAt] = pOCritter.RowByte(0x2A + liAt);

			if (pOCritter.Race == LicheRace)
			{
				int liNext = 3;

				if (pOCritter.IsFlier)
					liSpells[liNext++] = 0x39;

				if (pOCritter.RowByte(0x2E) > 0x2D)
					liSpells[liNext++] = 0x23;

				if (UWDamageTypes.Scale(piResistances, 1, 8) == 0)
					liSpells[liNext++] = 0x1C;

				if ((pOCritter.RowByte(0x2D) >> 1) >= 0x19 && pOCritter.RowByte(0) >= 9)
					liSpells[liNext] = 0x3B;
			}

			if (liSpells[0] == liSpells[1] || liSpells[1] == liSpells[2])
				liSpells[1] = 0xFF;

			if (liSpells[0] == liSpells[2])
				liSpells[2] = 0xFF;

			int liCount = 0;

			foreach (int liSpell in liSpells)
			{
				if (liSpell != 0xFF)
					liCount++;
			}

			if (liCount == 0)
				return null;

			StringBuilder lOText = new StringBuilder();

			// The original's joins count by the slot, not by the names written: kept so.
			for (int liAt = 0; liAt < liSpells.Length; liAt++)
			{
				if (liSpells[liAt] == 0xFF)
					continue;

				int liMajor = (liSpells[liAt] & 0xC0) >> 6;
				int liName = liSpells[liAt] & ~0xC0;

				// A healer's heal reads as its own name for the race 0x0F (major 1, minor 6).
				if (liMajor == 0 && pOCritter.Race == 0x0F && System.Array.IndexOf(HealSpellIndices, liSpells[liAt]) >= 0)
				{
					liMajor = 1;
					liName = 6;
				}

				liName = liMajor > 0 ? liName + ((liMajor + 0xC) << 4) : liName + 0x100;

				lOText.Append(UWText.Block(pOData, 6, liName + 1));

				if (liCount - liAt > 2)
					lOText.Append(", ");

				if (liCount - liAt == 2)
					lOText.Append(UWText.General(pOData, AndString));
			}

			lOText.Append(UWText.General(pOData, PeriodString, "."));

			return lOText.ToString();
		}
	}
}
