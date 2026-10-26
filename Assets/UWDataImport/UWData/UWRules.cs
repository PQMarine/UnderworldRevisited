using System;
using System.Collections.Generic;
using System.Reflection;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE RULE SET (cleanup phase 10, 2026-10-25; Docs/CLEANUP-PLAN.md section 6, the mod plan's
	/// section 3.3): the original's gameplay numbers that a player or a mod may change - skill-check
	/// bands, damage coefficients, mana per circle, the level table, the hours of a night - one C#
	/// class per domain (UWRulesCombat, UWRulesMagic, UWRulesExperience, UWRulesVitals,
	/// UWRulesSleep; wave 2: UWRulesCritter, UWRulesRespawn, UWRulesMotion, UWRulesTrade,
	/// UWRulesInventory, UWRulesLights, UWRulesQuirks, UWRulesDeviations, UWRulesStory,
	/// UWRulesLevels; the rest follow domain by domain).
	///
	/// THE DEFAULTS STAY IN CODE: every field's initialiser is the const the rule class always had,
	/// so the comment with the original's routine stays on that const, and the field's UWRule
	/// attribute names it again for the self-check, which asserts that the two agree. Only
	/// coefficients are rules, never a formula's integer shape - (v * 3) &gt;&gt; 1 keeps its shift.
	///
	/// ONE SET PER GAME: the set hangs on the session (UWGameSession.Rules); a new session starts
	/// with CreateOriginal(), and the host applies the player's rules.json to it at the session's
	/// start (UWGameSession.ApplyRulesFile, UWRulesJson). Current is a transitional facade for the
	/// rule classes that do not take the session yet, as UWRandom is.
	///
	/// THE KEYS are flat and dotted and mirror the classes - the field Combat.SkillCheck.RollRange
	/// is "combat.skillCheck.rollRange" - so the same file could later generate a C++ header and two
	/// ports read identical defaults. Entries lists them in ordinal order.
	/// </summary>
	public sealed class UWRules
	{
		public readonly UWRulesCombat Combat = new UWRulesCombat();

		public readonly UWRulesMagic Magic = new UWRulesMagic();

		public readonly UWRulesExperience Experience = new UWRulesExperience();

		public readonly UWRulesVitals Vitals = new UWRulesVitals();

		public readonly UWRulesSleep Sleep = new UWRulesSleep();

		public readonly UWRulesCritter Critter = new UWRulesCritter();

		public readonly UWRulesRespawn Respawn = new UWRulesRespawn();

		public readonly UWRulesMotion Motion = new UWRulesMotion();

		public readonly UWRulesTrade Trade = new UWRulesTrade();

		public readonly UWRulesInventory Inventory = new UWRulesInventory();

		public readonly UWRulesLights Lights = new UWRulesLights();

		public readonly UWRulesQuirks Quirks = new UWRulesQuirks();

		public readonly UWRulesDeviations Deviations = new UWRulesDeviations();

		public readonly UWRulesStory Story = new UWRulesStory();

		public readonly UWRulesLevels Levels = new UWRulesLevels();

		/// <summary>The original's rules - every field at its anchor const.</summary>
		public static UWRules CreateOriginal()
		{
			return new UWRules();
		}

		/// <summary>TRANSITIONAL: the current session's rules (UWGameSession.Current.Rules), for the
		/// rule classes that do not take the session as a parameter yet. Goes with
		/// UWGameSession.Current.</summary>
		public static UWRules Current => UWGameSession.Current.Rules;

		// ------------------------------------------------- The entries

		private static List<UWRuleEntry> msEntries;

		/// <summary>Every rule of the set by its key, in ordinal key order - built once by
		/// reflection over the domain classes: a public instance field of a value the JSON can
		/// hold (int, bool, int[]) is a rule, any other public instance field of a class is a group
		/// whose name becomes a part of the key.</summary>
		public static IReadOnlyList<UWRuleEntry> Entries
		{
			get
			{
				if (msEntries == null)
				{
					List<UWRuleEntry> lOEntries = new List<UWRuleEntry>();

					fCollect(typeof(UWRules), string.Empty, new List<FieldInfo>(), lOEntries);
					lOEntries.Sort((pOA, pOB) => string.CompareOrdinal(pOA.Key, pOB.Key));
					msEntries = lOEntries;
				}

				return msEntries;
			}
		}

		/// <summary>The entry of a key, or null.</summary>
		public static UWRuleEntry Find(string psKey)
		{
			foreach (UWRuleEntry lOEntry in Entries)
			{
				if (string.Equals(lOEntry.Key, psKey, StringComparison.Ordinal))
					return lOEntry;
			}

			return null;
		}

		private static void fCollect(Type pOType, string psPrefix, List<FieldInfo> pOPath, List<UWRuleEntry> pOInto)
		{
			foreach (FieldInfo lOField in pOType.GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				string lsKey = psPrefix + char.ToLowerInvariant(lOField.Name[0]) + lOField.Name.Substring(1);

				if (UWRuleEntry.IsRuleType(lOField.FieldType))
				{
					pOInto.Add(new UWRuleEntry(lsKey, pOPath.ToArray(), lOField));

					continue;
				}

				if (!lOField.FieldType.IsClass)
					continue;

				pOPath.Add(lOField);
				fCollect(lOField.FieldType, lsKey + ".", pOPath, pOInto);
				pOPath.RemoveAt(pOPath.Count - 1);
			}
		}
	}

	/// <summary>
	/// What a rule says about itself: the const it defaults to (the anchor - the self-check asserts
	/// that the field's initialiser equals it), a one-line meaning for the template, and the
	/// smallest value that keeps the formula working (a divisor or a die's range).
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class UWRuleAttribute : Attribute
	{
		public UWRuleAttribute(Type pOAnchorType, string psAnchorName, string psMeaning)
		{
			AnchorType = pOAnchorType;
			AnchorName = psAnchorName;
			Meaning = psMeaning;
		}

		public Type AnchorType { get; }

		public string AnchorName { get; }

		public string Meaning { get; }

		/// <summary>The smallest value a rules.json may set (every element of a table); smaller
		/// ones are reported and the default stays.</summary>
		public int Minimum { get; set; } = int.MinValue;

		/// <summary>The largest value a rules.json may set (every element of a table) - where the
		/// original's 16-bit arithmetic would overflow with more (2026-10-26: a forward speed of 1880
		/// turned the run's (Walk >> 2) * speed negative, and W stood still); larger ones are
		/// reported and the default stays.</summary>
		public int Maximum { get; set; } = int.MaxValue;
	}

	/// <summary>One rule: its key, where it sits in a UWRules, what it defaults to.</summary>
	public sealed class UWRuleEntry
	{
		private readonly FieldInfo[] mOPath;

		public UWRuleEntry(string psKey, FieldInfo[] pOPath, FieldInfo pOField)
		{
			Key = psKey;
			mOPath = pOPath;
			Field = pOField;
			Rule = pOField.GetCustomAttribute<UWRuleAttribute>();
		}

		/// <summary>The flat dotted key, as rules.json names it.</summary>
		public string Key { get; }

		public FieldInfo Field { get; }

		/// <summary>The rule's own description; null when the field has none (the self-check fails
		/// on that).</summary>
		public UWRuleAttribute Rule { get; }

		public Type ValueType => Field.FieldType;

		public string Meaning => Rule != null ? Rule.Meaning : string.Empty;

		public int Minimum => Rule != null ? Rule.Minimum : int.MinValue;

		public int Maximum => Rule != null ? Rule.Maximum : int.MaxValue;

		/// <summary>The kinds of values a rule holds: a whole number, a switch, a table of whole
		/// numbers.</summary>
		public static bool IsRuleType(Type pOType)
		{
			return pOType == typeof(int) || pOType == typeof(bool) || pOType == typeof(int[]);
		}

		public object GetValue(UWRules pORules)
		{
			return Field.GetValue(fOwner(pORules));
		}

		/// <summary>Sets the value; a table is copied, so the caller's array stays its own.</summary>
		public void SetValue(UWRules pORules, object pOValue)
		{
			if (pOValue is int[] liTable)
				pOValue = liTable.Clone();

			Field.SetValue(fOwner(pORules), pOValue);
		}

		/// <summary>The anchor's value - the const (or the static readonly table) the attribute
		/// names; null when it cannot be found.</summary>
		public object GetAnchorValue()
		{
			if (Rule == null || Rule.AnchorType == null)
				return null;

			FieldInfo lOAnchor = Rule.AnchorType.GetField(Rule.AnchorName,
				BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

			if (lOAnchor == null)
				return null;

			return lOAnchor.IsLiteral ? lOAnchor.GetRawConstantValue() : lOAnchor.GetValue(null);
		}

		/// <summary>"UWSkillCheck.RollRange" - the anchor by name, for reports.</summary>
		public string AnchorText => Rule != null && Rule.AnchorType != null
			? Rule.AnchorType.Name + "." + Rule.AnchorName
			: "(none)";

		private object fOwner(UWRules pORules)
		{
			object lOOwner = pORules;

			foreach (FieldInfo lOGroup in mOPath)
				lOOwner = lOGroup.GetValue(lOOwner);

			return lOOwner;
		}
	}
}
