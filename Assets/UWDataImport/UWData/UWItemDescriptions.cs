using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The look messages: an item with condition word, quantity, enchantment and owner, a
	/// creature with its attitude and name, the condition of a door. Engine-free since
	/// 2026-09-18 (P3 of the engine separation), out of Interaction, which keeps only what
	/// needs the clicked entity.
	/// </summary>
	public static class UWItemDescriptions
	{
		/// <summary>String block 1, our numbering: the owner names follow from here ("a green
		/// goblin" at 371 + 6). The names follow the CRIT.DAT general type (byte 9).</summary>
		public const int FirstOwnerNameMessage = 371;

		/// <summary>The owner field is masked with 0x1F as in the reference (bit 5 only steers
		/// whether passive members get angry).</summary>
		public const int OwnerRaceMask = 0x1F;

		/// <summary>The last named value, 28 "a creature".</summary>
		public const int LastOwnerValue = 28;

		/// <summary>Block 5: 97 hostile, 98 upset, 99 mellow, 100 friendly. The reference computes
		/// 96 plus attitude, our block is shifted by one as with the object names - with 96
		/// an upset animal was called "hostile" and a hostile one nothing at all (per user:
		/// rotworm and bat without word, 2026-09-12).</summary>
		private const int AttitudeWordsIndex = 97;

		/// <summary>From this whoami on the game describes a creature only by its name from
		/// block 7 (reference npc.cs: void creatures, Slasher of Veils 248).</summary>
		private const int VoidCreatureWhoami = 240;

		/// <summary>
		/// The look message for an item in the inventory or on the floor, with condition word,
		/// quantity and enchantment.
		///
		/// An item in the backpack has no health the condition could hang on
		/// - it is stored in its own quality field from 0 to 63. Which
		/// group of six from string block 5 applies is given by the quality type from COMOBJ.DAT.
		///
		/// Items without condition, such as a sack or a potion, have a type with empty
		/// words - then the message stays as before.
		/// </summary>
		/// <param name="pOObjectList">The list that holds the item's linked objects (the spell
		/// of a wand): the save game's records or the level's master list.</param>
		/// <summary>
		/// THE DETAIL LEVEL OF A LOOK DEPENDS ON WHERE THE ITEM IS (read 2026-09-29, per user: Zak's
		/// taper got its name in the original and not in ours). The original hands LookAt_ovr122_0
		/// a level from 1 to 3 and decides it by place:
		///   in the world (Look_seg024_24DC_D20): always 1, no roll - an item on the floor never
		///     shows "magical", an enchantment's name or a talisman's name;
		///   in the inventory (seg024_24DC_109B, see UWLoreCheck): Lore against 10, plus one, kept in the item
		///     (UWLoreCheck.Evaluate) - this value;
		///   in the trade area (ovr095_683, see UWLoreCheck): the player's own side 1 + Lore against 15, the
		///     partner's side 2 on a success against 20, else 1 - both rolled anew, kept nowhere.
		/// Until then ours rolled against 8 and kept the result for all three.
		/// </summary>
		public const int DetailFromLoreCheck = -1;

		public static string DescribeItem(UWObject pOItem, DataImport pOData, int piLoreSkill, List<UWObject> pOObjectList,
			int piDetail = DetailFromLoreCheck)
		{
			if (pOItem == null || pOData == null)
				return null;

			// HOW MUCH YOU SEE is decided by the Lore check (see UWLoreCheck): nothing at all,
			// only "magical", or the full name. The roll happens once per item.
			// A fixed detail level comes from the caller where the original rolls nothing or
			// keeps nothing (see DetailFromLoreCheck).
			int liLore = piDetail >= 0
				? System.Math.Max(UWLoreCheck.ResultUnknown, piDetail)
				: UWLoreCheck.Evaluate(pOItem, piLoreSkill, pOData.CommonObjectProperties);

			// Talismans carry their own name and neither article nor condition word - BUT ONLY
			// WITH THE FULL LORE RESULT (per user on the original, 2026-09-29: Krawallus, with
			// little Lore, sees the Ring of Humility as an iron ring; ours named it). LookAt_ovr122_0
			// calls LookAtTalisman_ovr122_CC3 only for detail level 3; that one then prints "You
			// see" and block 1 0x105 + n for the objects of class 0x0A (COMOBJ byte 7 bits 1-4).
			// Otherwise the talisman is described like any other object.
			string lsTalisman = liLore == UWLoreCheck.ResultNamed ? UWTalismans.GetName(pOItem.ID, pOData.Strings) : null;

			if (!string.IsNullOrEmpty(lsTalisman))
				return "You see " + lsTalisman + ".";

			int liCount = GetStackCount(pOItem);

			string lsRaw = pOData.GetObjectDescription(pOItem.ID + 1);

			string lsCondition = GetItemConditionWord(pOItem, pOData);

			UWEnchantment.Result lOEnchantment = UWEnchantment.Get(pOItem, pOObjectList, pOData.Strings);

			// In the original the words before the name come in this order: condition,
			// cursed, magical. They must be inserted BEFORE the article so that the
			// "a" can become "an" - hence here and not on the finished name.
			if (liLore == UWLoreCheck.ResultMagical && lOEnchantment.HasCarrier)
				lsRaw = InsertCondition(lsRaw, "magical");

			if (liLore == UWLoreCheck.ResultNamed && lOEnchantment.IsCursed)
				lsRaw = InsertCondition(lsRaw, "cursed");

			if (!string.IsNullOrEmpty(lsCondition))
				lsRaw = InsertCondition(lsRaw, lsCondition);

			string lsName = UWObjectDescriptionFormatter.FormatItemName(lsRaw, liCount > 1);

			// For a stack the number replaces the article: "a loaf of
			// bread" becomes "11 loaves of bread". The plural form after the "&" carries
			// no article, so nothing may be cut off.
			if (liCount > 1)
			{
				// If the plural form is missing entirely, the article is still on the left - it must go then,
				// otherwise "2 a dagger" would come out.
				string lsPlural = lsRaw != null && lsRaw.Contains("&")
					? UWObjectDescriptionFormatter.FormatItemName(lsRaw, true)
					: UWObjectDescriptionFormatter.FormatBareItemName(lsRaw, false);

				lsName = liCount + " " + lsPlural;
			}

			// A curse comes BEFORE the name (inserted further up), an ordinary
			// enchantment with "of" after it: "a cursed dagger" versus "a dagger of Very Great
			// Damage". Both only if the Lore check yields the full name.
			if (liLore == UWLoreCheck.ResultNamed && !lOEnchantment.IsCursed
				&& !string.IsNullOrEmpty(lOEnchantment.Name))
				lsName += " of " + lOEnchantment.Name;

			// The original ends with "with N full charges" - without "remaining", and for a
			// single charge in the singular: "with 1 full charge" (both per user,
			// 2026-09-05). The charges stay hidden as long as the spell does.
			if (liLore == UWLoreCheck.ResultNamed && lOEnchantment.Charges >= 0)
				lsName += lOEnchantment.Charges == 0
					? " with no full charges"
					: lOEnchantment.Charges == 1
						? " with 1 full charge"
						: " with " + lOEnchantment.Charges + " full charges";

			// WHO OWNS IT is included: "a sword belonging to a knight". The field is
			// the same one theft depends on (see Interaction.ReportTheft) - whoever takes it
			// upsets those named, and afterwards the addition is no longer shown.
			lsName += DescribeOwner(pOItem, pOData);

			return "You see " + lsName + ".";
		}

		/// <summary>" belonging to a green goblin", or empty for an item nobody owns.</summary>
		public static string DescribeOwner(UWObject pOItem, DataImport pOData)
		{
			if (pOItem == null || pOData == null || pOData.CommonObjectProperties == null)
				return string.Empty;

			UWCommonObjectProperties.Entry lOEntry;

			if (!pOData.CommonObjectProperties.TryGet(pOItem.ID, out lOEntry)
				|| !lOEntry.CanHaveOwner)
				return string.Empty;

			int liRace = pOItem.Owner & OwnerRaceMask;

			if (liRace <= 0 || liRace > LastOwnerValue)
				return string.Empty;

			return " belonging to" + fGetGeneralMessage(pOData, FirstOwnerNameMessage + liRace);
		}

		/// <summary>
		/// The look message of fixed scenery, with the owner: the original says "You see a
		/// sturdy chest belonging to a green goblin." for barrels and chests (per user with a
		/// screenshot, 2026-09-16). Items on the floor get it through DescribeItem.
		/// </summary>
		public static string FormatLookMessageWithOwner(string psRaw, UWObject pOItem, DataImport pOData)
		{
			string lsLook = UWObjectDescriptionFormatter.FormatLookMessage(psRaw);

			if (pOItem != null && lsLook.EndsWith("."))
			{
				string lsOwner = DescribeOwner(pOItem, pOData);

				if (!string.IsNullOrEmpty(lsOwner))
					lsLook = lsLook.Substring(0, lsLook.Length - 1) + lsOwner + ".";
			}

			return lsLook;
		}

		/// <summary>
		/// A creature with attitude: "You see a mellow outcast named Bragit." (per user in
		/// the original, 2026-09-12). The word comes from string block 5 from 96 (hostile,
		/// upset, mellow, friendly), the article follows it, the name is in block 7 and appears
		/// immediately, without having to have talked - as in the reference
		/// (npc.RegularNPCDescription). Null when this is not a creature description.
		/// </summary>
		/// <param name="psRaw">The raw object name ("an_outcast", "a_mountainman&mountainmen").</param>
		public static string DescribeCreature(UWNpc pONpc, string psRaw, DataImport pOData)
		{
			// THE VOID CREATURES (whoami from 240) have no name in block 4 - the line there
			// is empty, and "You see ." came out (per user: Slasher of Veils without name,
			// 2026-09-14). The reference (npc.cs) then takes only the name from block 7, without
			// attitude and kind: "You see the Slasher of Veils."
			if (pONpc != null && pOData != null && pONpc.NPCwhoami >= VoidCreatureWhoami)
			{
				try
				{
					string lsVoidName = pOData.Strings.Blocks[UWConversations.PartnerNameStringBlock]
						.Strings[UWConversations.GetPartnerNameIndex(pONpc.NPCwhoami)].Trim().Replace('_', ' ');

					if (!string.IsNullOrEmpty(lsVoidName))
						return "You see " + lsVoidName + ".";
				}
				catch
				{
					// falls back to the usual description
				}
			}

			if (pONpc == null || pOData == null || string.IsNullOrEmpty(psRaw))
				return null;

			string lsMood;

			try
			{
				lsMood = pOData.Strings.Blocks[5].Strings[AttitudeWordsIndex + (pONpc.NPCAttitude & 3)]
					.TrimEnd('\r', '\n');
			}
			catch
			{
				return null;
			}

			// "an_outcast": the article now belongs to the attitude word.
			int liSeparator = psRaw.IndexOf('_');
			string lsRace = liSeparator >= 0 ? psRaw.Substring(liSeparator + 1) : psRaw;

			// "mountainman&mountainmen": after the & comes the plural (per user, Smonden,
			// 2026-09-13).
			int liPlural = lsRace.IndexOf('&');

			if (liPlural >= 0)
				lsRace = lsRace.Substring(0, liPlural);

			string lsArticle = UWObjectDescriptionFormatter.StartsWithVowel(lsMood) ? "an" : "a";

			string lsName = null;

			if (pONpc.NPCwhoami != 0)
			{
				try
				{
					lsName = pOData.Strings.Blocks[UWConversations.PartnerNameStringBlock]
						.Strings[UWConversations.GetPartnerNameIndex(pONpc.NPCwhoami)].Trim();
				}
				catch
				{
					lsName = null;
				}
			}

			// A lowercase "name" is not a name, only the kind (reference).
			if (string.IsNullOrEmpty(lsName) || char.IsLower(lsName[0]))
				return "You see " + lsArticle + " " + lsMood + " " + lsRace + ".";

			return "You see " + lsArticle + " " + lsMood + " " + lsRace + " named " + lsName + ".";
		}

		/// <summary>Condition word from string block 5 - broken, badly damaged, damaged, sturdy,
		/// massive - for a condition string index as UWHealth computes it (see
		/// UWCombat.GetConditionStringIndex).</summary>
		public static string GetConditionWord(int piConditionStringIndex, DataImport pOData)
		{
			if (pOData == null)
				return null;

			try
			{
				// The project's usual +1: the parsed string blocks are one entry above the
				// game's numbering.
				string lsWord = pOData.GetObjectLookAndQualityDescription(piConditionStringIndex + 1);

				return string.IsNullOrWhiteSpace(lsWord) ? null : lsWord.TrimEnd('\r', '\n');
			}
			catch
			{
				return null;
			}
		}

		/// <summary>The condition word of an item from its own quality field.</summary>
		public static string GetItemConditionWord(UWObject pOItem, DataImport pOData)
		{
			UWCommonObjectProperties.Entry lOEntry;

			if (pOData == null || pOData.CommonObjectProperties == null
				|| !pOData.CommonObjectProperties.TryGet(pOItem.ID, out lOEntry))
				return null;

			return GetConditionWord(UWCombat.GetConditionStringIndex(lOEntry.QualityType, pOItem.Quality,
				lOEntry.QualityClass, UWObjectMechanics.IsAnyLight(pOItem.ID)), pOData);
		}

		/// <summary>Quantity of a stack. From 512 the field is no longer a number but a
		/// special property (uw-formats.txt 4.3) - then the item counts as a single piece.</summary>
		public static int GetStackCount(UWObject pOItem)
		{
			if (!pOItem.HasQuantity || pOItem.Quantity >= 512)
				return 1;

			return pOItem.Quantity < 1 ? 1 : pOItem.Quantity;
		}

		/// <summary>Inserts the condition word before the item name. The raw descriptions
		/// separate article and name with "_" (see UWObjectDescriptionFormatter), so "a_door"
		/// becomes "a_sturdy door".</summary>
		public static string InsertCondition(string psRawDescription, string psCondition)
		{
			if (string.IsNullOrEmpty(psRawDescription))
				return psRawDescription;

			int liSeparator = psRawDescription.IndexOf('_');

			return liSeparator < 0
				? psCondition + " " + psRawDescription
				: psRawDescription.Substring(0, liSeparator + 1) + psCondition + " "
					+ psRawDescription.Substring(liSeparator + 1);
		}

		/// <summary>The bare name of an object ("dagger"), for messages that append it.</summary>
		public static string GetBareName(int piObjectId, DataImport pOData)
		{
			try
			{
				return UWObjectDescriptionFormatter.FormatBareItemName(pOData.GetObjectDescription(piObjectId + 1));
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>A line of string block 1, our numbering, or empty.</summary>
		private static string fGetGeneralMessage(DataImport pOData, int piIndex)
		{
			try
			{
				string lsMessage = pOData.GetGeneralMessage(piIndex);

				return string.IsNullOrEmpty(lsMessage) ? string.Empty : lsMessage.TrimEnd('\r', '\n');
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
