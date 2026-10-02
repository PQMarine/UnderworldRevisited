using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Whether you can tell by looking at an item what is attached to it - the original's
	/// Lore check (the reference: interaction/look.LoreCheck).
	///
	/// THIS IS HOW IT LOOKS IN THE GAME (per user, 2026-09-09): you see THAT a piece of armour
	/// is enchanted, and with enough Lore also WHAT the enchantment is called. Numbers are
	/// shown nowhere - neither the armour value nor the strength of the enchantment.
	///
	/// Three levels:
	///
	///     1   nothing - the item looks ordinary
	///     2   "magical" before the name, but without saying what it can do
	///     3   the full name ("a sword of Very Great Damage") including charges
	///
	/// THE CHECK IS ROLLED ONLY ONCE. The item itself remembers the result,
	/// namely in its HEADING FIELD: bit 2 means "already attempted", bits 0 and 1
	/// hold the level. That is why looking at a piece again and again does not help. If
	/// the skill rises later, a new roll may only improve the old result, never
	/// worsen it - the reference explicitly notes that a previously achieved
	/// result must not be lost.
	///
	/// THAT THE FIELD IS USED TWICE is no oversight: only things that need no heading
	/// can be identified. For this the reference excludes major classes 5
	/// (doors, switches) and 6 (traps, triggers) as well as everything with render type 2.
	/// Recalculated against COMOBJ.DAT: render type 2 is carried by exactly the forty-nine
	/// objects that have a direction in the game - all fifteen 3D models, all doors and
	/// the projectiles. So no item that can be picked up loses its direction.
	/// </summary>
	public static class UWLoreCheck
	{
		/// <summary>Nothing to see.</summary>
		public const int ResultUnknown = 1;

		/// <summary>You only see that something is attached to it.</summary>
		public const int ResultMagical = 2;

		/// <summary>Full name of the enchantment.</summary>
		public const int ResultNamed = 3;

		/// <summary>Target value of the check: 0x0A in seg024_24DC_109B, the inventory look (read
		/// 2026-09-29; the reference had 8). The trade area uses 15 and 20 of its own, see
		/// UWItemDescriptions.DetailFromLoreCheck.</summary>
		public const int LoreTarget = 10;

		/// <summary>The trade area's two checks (ovr095_683, see UWConversationTrade): own side and the
		/// partner's side.</summary>
		public const int OwnTradeTarget = 15;

		public const int PartnerTradeTarget = 20;

		/// <summary>Bit 2 of the heading: a roll has already been made.</summary>
		private const int AttemptedBit = 0x4;

		/// <summary>Bits 0 and 1 hold the level achieved.</summary>
		private const int ResultMask = 0x3;

		/// <summary>Doors and switches.</summary>
		private const int DoorMajorClass = UWObjectMechanics.DoorMajorClass;

		/// <summary>Traps and triggers.</summary>
		private const int TrapMajorClass = UWObjectMechanics.TrapMajorClass;

		/// <summary>Render type from COMOBJ.DAT, byte 9, lower two bits. Whatever carries this type
		/// really uses its heading field.</summary>
		private const int DirectionalRenderType = UWObjectMechanics.DirectionalRenderType;

		/// <summary>Mask of that render type. Same value as ResultMask, but a different meaning -
		/// the two used to share one constant (2026-09-16).</summary>
		private const int RenderTypeMask = 0x3;

		public static bool CanBeIdentified(UWObject pOItem, UWCommonObjectProperties pOCommon)
		{
			if (pOItem == null)
				return false;

			int liMajorClass = pOItem.ID >> 6;

			if (liMajorClass == DoorMajorClass || liMajorClass == TrapMajorClass)
				return false;

			if (pOCommon == null)
				return true;

			UWCommonObjectProperties.Entry lOEntry;

			if (!pOCommon.TryGet(pOItem.ID, out lOEntry))
				return true;

			return (lOEntry.Byte9 & RenderTypeMask) != DirectionalRenderType;
		}

		/// <summary>
		/// The level for this item. Rolls the first time and writes the
		/// result into the item; after that it returns the same again.
		/// </summary>
		public static int Evaluate(UWObject pOItem, int piLoreSkill, UWCommonObjectProperties pOCommon)
		{
			if (!CanBeIdentified(pOItem, pOCommon))
				return ResultUnknown;

			if ((pOItem.Heading & AttemptedBit) != 0)
				return pOItem.Heading & ResultMask;

			// The check returns -1 to 2, the result should be 1 to 3. The critical
			// failure thereby slides onto the same level as the ordinary one.
			int liResult = (int)UWSkillCheck.Check(piLoreSkill, LoreTarget) + 1;

			if (liResult < ResultUnknown)
				liResult = ResultUnknown;

			int liPrevious = pOItem.Heading & ResultMask;

			if (liResult < liPrevious)
				liResult = liPrevious;

			pOItem.Heading = (ushort)(AttemptedBit | liResult);

			return liResult;
		}

		/// <summary>
		/// Makes all world items forget that a roll has already been made - after
		/// an increase in Lore.
		///
		/// WHY THIS IS NEEDED: the check is rolled only once per item and the
		/// result noted in the heading field. Without this reset a better Lore
		/// would bring nothing at all - you would keep seeing the same names. The
		/// reference therefore clears bit 2 of all world objects (uwObject.ResetIdentification)
		/// and leaves the achieved level in bits 0 and 1; so it can only get
		/// better, because Evaluate takes the old result as the lower bound.
		///
		/// ONLY IN THE WORLD, NOT IN THE BACKPACK. The reference notes this oddity
		/// itself ("oddly enough not on objects the player has in inventory") - whatever you
		/// carry keeps its state.
		///
		/// AND ONLY ON THE LEVEL ONE IS STANDING ON. ResetObjectIdentifcation_ovr107_19BC walks
		/// the CURRENT tile map and nothing else, so what lies on the other eight levels keeps
		/// the result it was given. The Todo carried "the lore state per level that the save
		/// game keeps" as something still to build until 2026-09-22; it is a NON-ITEM, and the
		/// reason is worth writing down because the byte really is there. Right after the
		/// reset, SkillGain_ovr143_271 (label 39E to 3B5, 393089-393108) stores the Lore skill
		/// into PLAYER.DAT at 0xC2 plus the dungeon level, for levels up to eight - a
		/// per-level record of "identified with this much Lore". NOTHING EVER READS IT. The
		/// whole image holds exactly one access to those bytes, and it is that write. So the
		/// original reserved the record, filled it and never used it, and the other levels stay
		/// as they were - which is what we do.
		/// </summary>
		public static void ResetAttempts(IEnumerable<UWObject> pOObjects,
			UWCommonObjectProperties pOCommon)
		{
			if (pOObjects == null)
				return;

			foreach (UWObject lOObject in pOObjects)
			{
				if (lOObject == null || !CanBeIdentified(lOObject, pOCommon))
					continue;

				lOObject.Heading = (ushort)(lOObject.Heading & ResultMask);
			}
		}

		/// <summary>
		/// Sets an item permanently to the highest level - the Name Enchantment
		/// spell. For this the reference simply writes seven into the heading,
		/// i.e. "already attempted" plus level three, and checks the same exclusions as the
		/// check. Returns false if the item cannot be identified.
		/// </summary>
		public static bool Identify(UWObject pOItem, UWCommonObjectProperties pOCommon)
		{
			if (!CanBeIdentified(pOItem, pOCommon))
				return false;

			pOItem.Heading = (ushort)(AttemptedBit | ResultNamed);

			return true;
		}
	}
}
