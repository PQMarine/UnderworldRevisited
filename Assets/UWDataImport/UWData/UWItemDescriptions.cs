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

		/// <summary>The last race a look names, 0x1B (LookAt); 28, "a creature", is not.</summary>
		public const int LastOwnerValue = 0x1B;

		/// <summary>The owner field, word 3 bits 0-5: any bit set makes an owner (LookAt).</summary>
		private const int OwnerFieldMask = 0x3F;

		/// <summary>Block 5: 97 hostile, 98 upset, 99 mellow, 100 friendly. The reference computes
		/// 96 plus attitude, our block is shifted by one as with the object names - with 96
		/// an upset animal was called "hostile" and a hostile one nothing at all (per user:
		/// rotworm and bat without word, 2026-09-12).</summary>
		private const int AttitudeWordsIndex = 97;

		/// <summary>From this whoami on a creature's look shows no attitude word, except for
		/// NoAttitudeExceptWhoami (CritterLook: who &lt; 0xF0 or who == 0xFF) - the void creatures,
		/// the Slasher of Veils 248.</summary>
		private const int VoidCreatureWhoami = 240;

		/// <summary>The one whoami from 0xF0 on that keeps its attitude word (CritterLook).</summary>
		private const int NoAttitudeExceptWhoami = 0xFF;

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

			IUWGrammar lIGrammar = UWGrammar.Of(pOData);

			if (!string.IsNullOrEmpty(lsTalisman))
				return lIGrammar.LookSentence(lIGrammar.LiteralPhrase(lsTalisman));

			int liCount = UWStackRules.CountAtLeastOne(pOItem);

			UWEnchantment.Result lOEnchantment = UWEnchantment.Get(pOItem, pOObjectList, pOData.Strings);

			// The name with its article, or the count and the plural; the condition word, "cursed"
			// and "magical" before it in the original's order (IUWGrammar.ItemPhrase - the English
			// rules were here until 2026-10-26, UWGrammarEnglish).
			string lsName = lIGrammar.ItemPhrase(UWText.RawObjectName(pOData, pOItem.ID), liCount, GetItemConditionWord(pOItem, pOData),
				liLore == UWLoreCheck.ResultNamed && lOEnchantment.IsCursed,
				liLore == UWLoreCheck.ResultMagical && lOEnchantment.HasCarrier);

			lsName += EnchantmentSuffix(lOEnchantment, liLore, lIGrammar);

			// WHO OWNS IT is included: "a sword belonging to a knight". The field is
			// the same one theft depends on (see UWWorldPickup.ReportTheft) - whoever takes it
			// upsets those named, and afterwards the addition is no longer shown.
			lsName += DescribeOwner(pOItem, pOData);

			return lIGrammar.LookSentence(lsName);
		}

		/// <summary>
		/// The words after the name (do_of, LOOK.C) at the full detail level: " of &lt;enchantment&gt;"
		/// and the charges. A curse comes BEFORE the name (the caller inserts it), an ordinary
		/// enchantment with "of" after it: "a cursed dagger" versus "a dagger of Very Great
		/// Damage". The original ends with "with N full charges" - without "remaining", and for a
		/// single charge in the singular: "with 1 full charge" (both per user, 2026-09-05). The
		/// charges stay hidden as long as the spell does. Shared by the look and identify_inv
		/// since 2026-10-15; the words are the grammar's since 2026-10-26 (IUWGrammar.EnchantmentPart).
		/// </summary>
		public static string EnchantmentSuffix(UWEnchantment.Result pOEnchantment, int piDetail, IUWGrammar pIGrammar)
		{
			if (piDetail != UWLoreCheck.ResultNamed)
				return string.Empty;

			return (pIGrammar ?? UWGrammar.English).EnchantmentPart(pOEnchantment.IsCursed ? null : pOEnchantment.Name,
				pOEnchantment.Charges);
		}

		/// <summary>
		/// THE NAME identify_inv HANDS A CONVERSATION (conv_inv_name, CONVERSE.C, read 2026-10-15):
		/// no "You see", no condition word, no owner. A pile of more than one starts with its count
		/// and takes the plural; otherwise the article only when the script asks for it
		/// (pbArticle). At detail 2 an enchanted thing is "magical" (and "a magical ..." with the
		/// article), at detail 3 a cursed one "cursed" - before the article the name brings, as the
		/// original builds it ("cursed a crown") - and the enchantment's name and charges follow
		/// (EnchantmentSuffix). The detail is the one the conversation passes, no Lore roll.
		///
		/// Until 2026-10-15 ours handed over the look sentence: article and condition word always
		/// ("this a fine longsword" in Shak's repair offer, where the original says "this
		/// longsword").
		/// </summary>
		public static string IdentifyName(UWObject pOItem, DataImport pOData, List<UWObject> pOObjectList,
			bool pbArticle, int piDetail)
		{
			if (pOItem == null || pOData == null)
				return string.Empty;

			int liCount = UWStackRules.Count(pOItem);
			UWEnchantment.Result lOEnchantment = UWEnchantment.Get(pOItem, pOObjectList, pOData.Strings);
			IUWGrammar lIGrammar = UWGrammar.Of(pOData);

			// The count or the article, "magical" or "cursed", the name (IUWGrammar.IdentifyName - the
			// English rules were here until 2026-10-26, UWGrammarEnglish).
			string lsName = lIGrammar.IdentifyName(UWText.RawObjectName(pOData, pOItem.ID, null), liCount,
				piDetail == UWLoreCheck.ResultMagical && lOEnchantment.HasCarrier,
				piDetail == UWLoreCheck.ResultNamed && lOEnchantment.IsCursed, pbArticle);

			return lsName + EnchantmentSuffix(lOEnchantment, piDetail, lIGrammar);
		}

		/// <summary>The list that holds a carried item's linked objects (the spell of a wand): the
		/// save game's inventory records, else the level's master list - for the look and
		/// identify_inv.</summary>
		public static List<UWObject> ItemObjectList(DataImport pOData, UWLevel pOLevel)
		{
			if (pOData != null && pOData.IsSavegame && pOData.InitialPlayer != null)
				return pOData.InitialPlayer.InventoryRecords;

			return pOLevel != null ? pOLevel.Masterlist : null;
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

			// THE WHOLE FIELD ABOVE 0, ITS RACE AT MOST 0x1B: LookAt (UnderworldExhumed obj/LOOK.C,
			// read 2026-10-15) tests "owner > 0 and (owner & 0x1F) <= 0x1B", so the owner 0x20 (race
			// 0 with bit 5) reads "belonging to a denizen of the area" and race 28 (0x1C, "a
			// creature") is never named - as the theft rule (UWCritterRules.ClearsOwnerAfterTheft).
			// Until 2026-10-15 ours named races 1 to 28 (reference audit round 2, A40).
			int liRace = pOItem.Owner & OwnerRaceMask;

			if ((pOItem.Owner & OwnerFieldMask) == 0 || liRace > LastOwnerValue)
				return string.Empty;

			return UWGrammar.Of(pOData).OwnerPart(UWText.General(pOData, FirstOwnerNameMessage + liRace));
		}

		/// <summary>
		/// THE LOOK AT A THING THAT IS NO ITEM: fixed scenery, a door with its condition word before
		/// the name ("You see a badly damaged door."), the moving door - each with its owner (the
		/// original says "You see a sturdy chest belonging to a green goblin." for barrels and chests,
		/// per user with a screenshot, 2026-09-16; items on the floor get it through DescribeItem). The
		/// condition word may be null, the item too (then no owner). Out of UWLookService (translation
		/// wave 2, 2026-10-26), so the look catalog of the self-check and uwdump sees the sentence the
		/// game shows; the words are the grammar's (IUWGrammar.ScenePhrase).
		/// </summary>
		public static string DescribeScenery(string psRaw, string psCondition, UWObject pOItem, DataImport pOData)
		{
			IUWGrammar lIGrammar = UWGrammar.Of(pOData);

			return lIGrammar.LookSentence(lIGrammar.ScenePhrase(psRaw, psCondition) + DescribeOwner(pOItem, pOData));
		}

		/// <summary>The look at a wall or floor: block 10's texture name in the look sentence (the names
		/// carry their article, "a stone wall").</summary>
		public static string DescribeChunk(string psText, DataImport pOData)
		{
			IUWGrammar lIGrammar = UWGrammar.Of(pOData);

			return lIGrammar.LookSentence(lIGrammar.ChunkPhrase(psText));
		}

		/// <summary>
		/// A creature's look as CritterLook builds it (UnderworldExhumed obj/LOOK.C, read
		/// 2026-10-15): "You see a mellow outcast named Bragit.". The parts: the attitude word
		/// (block 5, 0x60 + attitude, shown only for whoami below 0xF0 or 0xFF, and only with a
		/// kind), the kind (the block 4 name, without its article after an attitude word, with it
		/// otherwise) and the conversation name (block 7, whoami + 0x10). A NAME IN LOWER CASE
		/// REPLACES THE KIND: "You see a hostile head bandit." (the golem, the prisoner, the
		/// bandits, the guards, the imp, "spectre named Warren"). A creature without a kind (the
		/// void creatures, whoami 240 and up) shows its name alone: "You see the Slasher of
		/// Veils.". Until 2026-10-15 ours dropped a lower-case name and showed the kind, and
		/// answered every whoami from 240 with the name alone (reference audit round 2, A39).
		/// Null when there is neither kind nor name - the caller describes it otherwise.
		/// </summary>
		/// <param name="psRaw">The raw object name ("an_outcast", "a_mountainman&mountainmen").</param>
		public static string DescribeCreature(UWNpc pONpc, string psRaw, DataImport pOData)
		{
			if (pONpc == null || pOData == null)
				return null;

			int liWho = pONpc.NPCwhoami;
			string lsKind = fNonEmpty(psRaw);
			string lsAttitude = null;
			string lsName = null;

			if (liWho < VoidCreatureWhoami || liWho == NoAttitudeExceptWhoami)
				lsAttitude = fNonEmpty(UWText.Block(pOData, 5, AttitudeWordsIndex + (pONpc.NPCAttitude & 3), null));

			if (liWho > 0)
				lsName = fNonEmpty(UWText.Block(pOData, UWConversations.PartnerNameStringBlock,
					UWConversations.GetPartnerNameIndex(liWho), null));

			// The sentence is the grammar's (IUWGrammar.CreatureLook; the English rules were here until
			// 2026-10-26, UWGrammarEnglish).
			return UWGrammar.Of(pOData).CreatureLook(lsAttitude, lsKind, lsName);
		}

		/// <summary>The text trimmed, or null when nothing is left (CritterLook's "*s == 0").</summary>
		private static string fNonEmpty(string psText)
		{
			string lsText = psText != null ? psText.Trim() : null;

			return string.IsNullOrEmpty(lsText) ? null : lsText;
		}

		/// <summary>
		/// SpecialLook (LOOK.C) for an item: the line LookAt prints after the look sentence.
		/// KeyLook - a key (0x100-0x10F) says block 5 line 0x64 + its owner field, the lock it
		/// belongs to ("The handle of this key is tarnished from heavy use."), when there is one.
		/// BonesLook - a skull, bone or pile of bones (0xC2-0xC6) whose owner names a creature
		/// says whose: "It looks to be that of " (0x16), for the pile or more than one "They look
		/// to be those of " (0x17), then the creature's name with its article, or "an adventurer"
		/// for owner 0x3F; owner 0, 0x28 and 0x3C-0x3E say nothing. Books are read by the look
		/// itself (Interaction.TryReadBook); the spiked door is Interaction's. Both lines need a
		/// look with detail - the inventory's; in the world a key or bones get none (see
		/// Interaction.fGetSpecialLookLine). Missing until 2026-10-13 (per user's request after
		/// the spiked door). Null when there is no line.
		/// </summary>
		public static string SpecialLookLine(UWObject pOItem, DataImport pOData)
		{
			if (pOItem == null || pOData == null)
				return null;

			int liId = pOItem.ID;

			if (liId >= FirstKeyId && liId <= LastKeyId)
				return UWText.Block(pOData, LookBlock, KeyLookFirst + (pOItem.Owner & 0x3F) + 1, null);

			if (liId < FirstBonesId || liId > LastBonesId)
				return null;

			int liOwner = pOItem.Owner & 0x3F;

			if (liOwner == 0 || liOwner == NoBonesOwner || (liOwner >= FirstSilentBonesOwner && liOwner != AdventurerBonesOwner))
				return null;

			bool lbMany = liId == PileOfBonesId || pOItem.Quantity > 1;
			string lsLead = UWText.General(pOData, (lbMany ? BonesManyMessage : BonesOneMessage) + 1, null);
			// "that of" a creature, in the dative where the language has cases ("von einem Goblin").
			string lsWhose = liOwner == AdventurerBonesOwner
				? UWText.T("grammar.bones.adventurer")
				: UWText.ObjectName(pOData, FirstCreatureId + liOwner, false, true, null, UWGrammarCase.Dative);

			if (lsLead == null || string.IsNullOrEmpty(lsWhose))
				return null;

			return lsLead + lsWhose + ".";
		}

		private const int LookBlock = 5;
		private const int FirstKeyId = 0x100;
		private const int LastKeyId = 0x10F;
		private const int KeyLookFirst = 0x64;
		private const int FirstBonesId = 0xC2;
		private const int LastBonesId = 0xC6;
		private const int PileOfBonesId = 0xC6;
		private const int NoBonesOwner = 0x28;
		private const int FirstSilentBonesOwner = 0x3C;
		private const int AdventurerBonesOwner = 0x3F;
		private const int FirstCreatureId = 0x40;

		/// <summary>game_sprint 0x16 and 0x17 (block 1, the project's numbering adds one).</summary>
		private const int BonesOneMessage = 0x16;
		private const int BonesManyMessage = 0x17;

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
				lOEntry.QualityClass), pOData);
		}

		/// <summary>Inserts the condition word before the item name. The raw descriptions
		/// separate article and name with "_" (see UWObjectDescriptionFormatter), so "a_door"
		/// becomes "a_sturdy door". The English grammar's (UWGrammarEnglish).</summary>
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

	}
}
