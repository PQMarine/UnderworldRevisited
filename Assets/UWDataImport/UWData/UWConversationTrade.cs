using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Trading in conversation: the two barter areas between the portraits and the
	/// conversation functions that work on them.
	///
	/// HOW IT WORKS IN THE ORIGINAL: the player puts goods from his inventory into his four slots
	/// and marks what he offers; the conversation partner puts his goods into his four
	/// slots (setup_to_barter), the player marks what he wants of them. The conversation file
	/// then calls do_offer (the partner evaluates the offer), do_demand (the player demands
	/// the goods) or do_judgement (the player appraises the deal). If the partner accepts,
	/// the marked goods change sides.
	///
	/// THE NUMBERS come from the reference (conversationtrade.cs and the functions next to it),
	/// which has them from the disassembly: per creature type three nibbles of the creature table -
	/// threshold, patience, appraisal skill -, each shifted by a random share. The
	/// value of an item is its money value from COMOBJ.DAT times condition; coins are always worth
	/// full value. What the partner likes counts one and a half times, what he dislikes not at all.
	///
	/// THE PARTNER'S POSSESSIONS are his object chain (Special Link). What he does not already carry,
	/// UWCritterLoot rolls on the first trade - just like on death -, and the creature
	/// remembers that (NPCLootSpawned) so it does not happen twice.
	///
	/// HANDLES: the conversation functions pass items around as numbers - find_inv returns
	/// one, count_inv or give_to_npc get it back. They are the OBJECT NUMBERS of the level, as
	/// in UW.EXE (take_id_from_npc_ovr095_1C42 walks the partner's chain comparing
	/// ReverseObjectLookup with it): UWCarriedSlots.NumberOf, which carried objects have since
	/// 2026-09-29. A conversation may KEEP one - Shak stores the item he repairs in his global 2
	/// and hands it back by it in a later conversation. Until 2026-10-01 ours numbered the items
	/// from one per conversation, so the number came back as whatever was named first then: the
	/// gold just paid (per user, the sword repaired by Shak). Only an item without a number (one
	/// created here) gets one from the old directory, from DirectoryHandleBase on.
	///
	/// PLUS THE INVENTORY FUNCTIONS that work on the same directory: show_inv, find_inv,
	/// count_inv, do_inv_create, do_inv_delete, take_from_npc_inv, give_ptr_npc,
	/// take_id_from_npc, identify_inv, check_inv_quality, set_inv_quality, x_obj_stuff,
	/// place_object - the attitudes set_attitude, set_race_attitude - and remove_talker.
	///
	/// ENGINE-FREE since 2026-09-18 (P3 of the engine separation): the world, the player's
	/// numbers and the inventory come through IUWConversationHost, the inventory as the
	/// UWInventoryModel of P2. UWItemDrag and UWConversationScreen work on the slot arrays
	/// as before.
	/// </summary>
	public class UWConversationTrade
	{
		public const int SlotCount = 4;

		/// <summary>Goods with number 1000 and above mean a whole class: (number - 1000)
		/// is the object number divided by sixteen.</summary>
		private const int ClassBase = 1000;

		private const int FullQuality = UWObjectMechanics.MaxQuality;

		/// <summary>Trade lines in string block 7: " that I am getting ", then the five
		/// degrees of certainty and the nine verdicts.</summary>
		private const int JudgementBlock = 7;

		private const int JudgementJoinString = 3;

		private const int JudgementCertaintyFirst = 4;

		private const int JudgementVerdictFirst = 9;

		public readonly UWObject[] PlayerItems = new UWObject[SlotCount];
		public readonly bool[] PlayerSelected = new bool[SlotCount];
		public readonly UWObject[] NpcItems = new UWObject[SlotCount];
		public readonly bool[] NpcSelected = new bool[SlotCount];

		/// <summary>
		/// BOUGHT GOODS STAY ON THE PARTNER'S SIDE (per user, 2026-09-29: in the original the goods
		/// of an accepted deal remain in the trade area, and the player drags them away). An
		/// accepted offer or demand calls do_decline(1): only the unmarked goods go back, the marked
		/// ones stay in their slots, out of the partner's possession, and flag_4872 lets the player
		/// take them (ovr095_683). Ours put them straight into the backpack.
		/// </summary>
		public readonly bool[] NpcBought = new bool[SlotCount];

		/// <summary>flag_4872: the goods on the partner's side may be taken. Every do_offer clears
		/// it first; an accepted offer or demand sets it.</summary>
		public bool NpcGoodsBought { get; private set; }

		/// <summary>Counts up on every change - the display then redraws.</summary>
		public int Version { get; private set; }

		/// <summary>An accepted offer, not yet carried out (see Swap).</summary>
		public bool PendingSwap { get; private set; }

		/// <summary>The pending swap comes from a demand: only the partner's goods move (see
		/// fDoDemand).</summary>
		private bool mbPlayerKeepsHisGoods;

		private readonly DataImport mOData;
		private readonly UWNpc mONpc;
		private readonly IUWConversationHost mIHost;
		private readonly Action<int> mOSay;
		private readonly Action<string> mOPrint;
		private readonly Func<string, int> mORegisterString;
		private readonly Func<UWObject, int, string> mODescribe;
		private readonly UWOriginalRandom mORandom;

		/// <summary>The player's inventory, or null without one.</summary>
		private UWInventoryModel fInventory => mIHost != null ? mIHost.Inventory : null;

		/// <summary>The level the conversation takes place on, or null without a world.</summary>
		private UWLevel fLevel => mIHost != null ? mIHost.CurrentLevel : null;

		/// <summary>
		/// HOW THE CONVERSATIONS COUNT THEIR ARRAYS - read off the guard of Urgo on level 3
		/// (conversation 15) with a log of every call (per user, 2026-09-17):
		///
		///   WHAT WE FILL (show_inv, find_barter_total) is read FROM ZERO. The guard walks the array
		///     from the count downwards and stops before zero, so with entries written from one he
		///     read a leftover local instead of the last item: a single stack of five fish was not
		///     counted at all, and four slots lost exactly one of them.
		///   WHAT A SCRIPT FILLS ITSELF and then hands to give_to_npc starts at ONE - the guard
		///     counts its index up BEFORE writing. Reading that array from zero was the original
		///     fault: nothing was handed over, and the food fell to the floor at the end of the
		///     conversation.
		///
		/// So give_to_npc takes the handles it finds from zero up to the count, skipping empty
		/// entries, and works either way round.
		/// </summary>
		private const int GiveArrayFirstIndex = 0;

		/// <summary>Writes what the trade functions hand to a conversation into the log - for
		/// tracking down why a partner does not take what he asked for. It is how the array
		/// counting and the pointer fault of 2026-09-17 were found; left in, switched off.</summary>
		private static readonly bool LogTrade = false;

		/// <summary>The directory of handles, see class comment. Handle = index in this list + 1.</summary>
		private readonly List<UWObject> mOHandles = new List<UWObject>();

		private UWObjectClassProperties.Critter mOStats;
		private bool mbHasStats;

		private int miThreshold;
		private int miPatience;
		private int miAccuracy;
		private int miPreviousEvaluation;
		private int miLikesPointer;
		private int miDislikesPointer;
		private UWConversationVM mOVm;

		public UWConversationTrade(DataImport pOData, UWNpc pONpc, IUWConversationHost pIHost,
			Action<int> pOSay, Action<string> pOPrint,
			Func<string, int> pORegisterString, Func<UWObject, int, string> pODescribe)
		{
			mOData = pOData;
			mONpc = pONpc;
			mIHost = pIHost;
			mOSay = pOSay;
			mOPrint = pOPrint;
			mORegisterString = pORegisterString;
			mODescribe = pODescribe;

			// READ 2026-09-25 (the barter setup of SetupToBarter_ovr095_0): the generator is seeded
			// with the partner's OBJECT NUMBER, so each creature - not each kind - always trades
			// the same way; then threshold, patience and appraisal are rolled from its row and the
			// player's Charm moves the first two. Until then ours seeded with the kind, left the
			// threshold at a sixth of its value, ignored Charm and took 16 for the appraisal's 15.
			// SINCE 2026-09-29 WITH THE ORIGINAL'S GENERATOR (UWOriginalRandom): System.Random gave
			// other numbers from the same seed, so the partner's threshold differed from the
			// original's (per user: the Gray Goblin took 8 coins in ours and not below 9 in the
			// original, at Charm 0).
			mORandom = new UWOriginalRandom(pONpc != null ? fObjectNumber(pONpc) : 0);

			if (pONpc != null && pOData != null && pOData.ObjectClassProperties != null
				&& pOData.ObjectClassProperties.TryGetCritter(pONpc.ID, out mOStats))
				mbHasStats = true;

			int liCharm = mIHost != null && mIHost.HasPlayer ? mIHost.GetPlayerSkill(UWPlayerData.Skill.Charm) : 0;

			miThreshold = fRandomOffset((mbHasStats ? mOStats.TradeThreshold : 0) * 6, -25, 25) - 2 * liCharm;
			miPatience = fRandomOffset(mbHasStats ? mOStats.TradePatience : 0, -20, 100) + liCharm / 2;
			miAccuracy = fRandomOffset((15 - (mbHasStats ? mOStats.TradeAppraisal : 0)) * 6, -25, 50);
		}

		/// <summary>Reference Rng.RandomOffset: the base value shifted by a percentage from the
		/// range.</summary>
		private int fRandomOffset(int piBase, int piLower, int piUpper)
		{
			return mORandom.Noise(piBase, piLower, piUpper);
		}

		// ------------------------------------------------------------------
		// Conversation functions
		// ------------------------------------------------------------------

		/// <summary>Whether this class knows a function of this name.</summary>
		public static bool Handles(string psName)
		{
			switch (psName)
			{
				case "setup_to_barter":
				case "find_barter":
				case "find_barter_total":
				case "do_offer":
				case "do_demand":
				case "do_judgement":
				case "do_decline":
				case "set_likes_dislikes":
				case "give_to_npc":
				case "take_from_npc":
				case "give_all_stuff":
				case "show_inv":
				case "find_inv":
				case "count_inv":
				case "do_inv_create":
				case "do_inv_delete":
				case "take_from_npc_inv":
				case "give_ptr_npc":
				case "take_id_from_npc":
				case "identify_inv":
				case "set_attitude":
				case "set_race_attitude":
				case "check_inv_quality":
				case "set_inv_quality":
				case "x_obj_stuff":
				case "place_object":
				case "remove_talker":
					return true;
			}

			return false;
		}

		/// <summary>Handles of items without an object number start here - above the level's
		/// 1024 objects, so they never meet a real number.</summary>
		private const int DirectoryHandleBase = 1024;

		/// <summary>The handle of an item: its object number, else a directory entry assigned the
		/// first time it is named.</summary>
		public int HandleOf(UWObject pOItem)
		{
			if (pOItem == null)
				return 0;

			int liNumber = UWCarriedSlots.NumberOf(pOItem);

			if (liNumber > 0 && liNumber < DirectoryHandleBase)
				return liNumber;

			int liIndex = mOHandles.IndexOf(pOItem);

			if (liIndex < 0)
			{
				mOHandles.Add(pOItem);
				liIndex = mOHandles.Count - 1;
			}

			return DirectoryHandleBase + liIndex + 1;
		}

		public UWObject Resolve(int piHandle)
		{
			if (piHandle > DirectoryHandleBase)
				return piHandle - DirectoryHandleBase <= mOHandles.Count ? mOHandles[piHandle - DirectoryHandleBase - 1] : null;

			if (piHandle <= 0)
				return null;

			foreach (UWObject lOItem in NpcItems)
				if (lOItem != null && UWCarriedSlots.NumberOf(lOItem) == piHandle)
					return lOItem;

			foreach (UWObject lOItem in PlayerItems)
				if (lOItem != null && UWCarriedSlots.NumberOf(lOItem) == piHandle)
					return lOItem;

			UWObject lOFound = fFindNumbered(fGetNpcInventory(), piHandle, 0);

			if (lOFound != null)
				return lOFound;

			UWInventoryModel lOInventory = fInventory;

			if (lOInventory != null)
			{
				if (lOInventory.CursorItem != null && UWCarriedSlots.NumberOf(lOInventory.CursorItem) == piHandle)
					return lOInventory.CursorItem;

				return lOInventory.FindCarried(pOItem => UWCarriedSlots.NumberOf(pOItem) == piHandle);
			}

			return null;
		}

		/// <summary>The item with this object number in a chain, its contents included.</summary>
		private static UWObject fFindNumbered(List<UWObject> pOChain, int piNumber, int piDepth)
		{
			if (pOChain == null || piDepth > 8)
				return null;

			foreach (UWObject lOItem in pOChain)
			{
				if (lOItem == null)
					continue;

				if (UWCarriedSlots.NumberOf(lOItem) == piNumber)
					return lOItem;

				if (!lOItem.HasQuantity && lOItem.Contents != null)
				{
					UWObject lOInner = fFindNumbered(lOItem.Contents, piNumber, piDepth + 1);

					if (lOInner != null)
						return lOInner;
				}
			}

			return null;
		}

		public int Call(string psName, IReadOnlyList<int> pOArguments, IReadOnlyList<int> pOAddresses, UWConversationVM pOVm)
		{
			mOVm = pOVm;

			switch (psName)
			{
				case "setup_to_barter":
					fSetupToBarter();
					return 0;

				case "find_barter":
					return fFindBarter(fArgument(pOArguments, 0));

				case "find_barter_total":
					return fFindBarterTotal(pOArguments, pOAddresses);

				case "do_offer":
					return fDoOffer(fLastArguments(pOArguments, OfferArgumentCount));

				case "do_demand":
					return fDoDemand(fLastArguments(pOArguments, DemandArgumentCount));

				case "do_judgement":
					fDoJudgement();
					return 0;

				case "do_decline":
					fClearNpcSlots();
					return 0;

				// The ADDRESSES of the two lists, not the words behind them: set_likes_dislikes_ovr095_1F7B
				// hands the pushed numbers to ovr093_226B, which makes them pointers into the
				// conversation's memory (Shak and Zak push 37 and 58, where their lists begin).
				// Until 2026-09-29 ours took the first entry of each list as its address and so
				// read neither list.
				case "set_likes_dislikes":
					miLikesPointer = fAddress(pOAddresses, 0);
					miDislikesPointer = fAddress(pOAddresses, 1);
					return 0;

				case "give_to_npc":
					// The array comes as an ADDRESS, not as a value - taking the value handed us the
					// contents of the local instead of the list (per user, 2026-09-17: a stale 242).
					return fGiveToNpc(fArgument(pOArguments, 0), fAddress(pOAddresses, 1));

				case "take_from_npc":
					return fTakeFromNpc(fArgument(pOArguments, 0));

				case "give_all_stuff":
					return fGiveAllStuff();

				case "show_inv":
					return fShowInv(pOAddresses);

				// find_inv(item, where): FIRST the item is pushed, THEN the search location (0 partner,
				// otherwise player) - like that in all conversations, and the reference reads the search
				// location from the top of the stack. Swapped until 2026-09-14: we searched for object 1
				// on the player, Judy (level 4) never found the picture of Tom (272), the reply to it was
				// missing (per user). The other functions have the first argument at the bottom, as before.
				case "find_inv":
					return fFindInv(fArgument(pOArguments, 1), fArgument(pOArguments, 0));

				case "count_inv":
				{
					UWObject lOItem = Resolve(fArgument(pOArguments, 0));

					if (LogTrade)
						UWLog.Info(string.Format("[Trade] count_inv handle {0} -> {1}",
							fArgument(pOArguments, 0), lOItem != null ? fGetCount(lOItem) : 0));

					return lOItem != null ? fGetCount(lOItem) : 0;
				}

				case "do_inv_create":
					return fDoInvCreate(fArgument(pOArguments, 0));

				case "do_inv_delete":
					return fDoInvDelete(fArgument(pOArguments, 0));

				case "take_from_npc_inv":
					return fTakeFromNpcInv(fArgument(pOArguments, 0));

				case "give_ptr_npc":
					return fGivePtrNpc(fArgument(pOArguments, 0), fArgument(pOArguments, 1));

				case "take_id_from_npc":
					return fTakeIdFromNpc(fArgument(pOArguments, 0));

				case "identify_inv":
					return fIdentifyInv(pOArguments, pOAddresses);

				case "set_attitude":
					fSetAttitudeOf(fArgument(pOArguments, 0), fArgument(pOArguments, 1));
					return 0;

				case "set_race_attitude":
					fSetRaceAttitude(fArgument(pOArguments, 0), fArgument(pOArguments, 1), fArgument(pOArguments, 2));
					return 0;

				case "check_inv_quality":
				{
					UWObject lOItem = Resolve(fArgument(pOArguments, 0));

					return lOItem != null ? lOItem.Quality : 0;
				}

				case "set_inv_quality":
				{
					UWObject lOItem = Resolve(fArgument(pOArguments, 0));

					if (lOItem != null)
						lOItem.Quality = (ushort)(fArgument(pOArguments, 1) & 0x3F);

					return 0;
				}

				case "x_obj_stuff":
					return fXObjStuff(pOArguments, pOAddresses);

				case "place_object":
					return fPlaceObject(fArgument(pOArguments, 0), fArgument(pOArguments, 1), fArgument(pOArguments, 2));

				case "remove_talker":
					fRemoveTalker();
					return 0;
			}

			return 0;
		}

		/// <summary>
		/// x_obj_stuff(handle, mode, identified, owner, flags, link, flag1, flag0, quality):
		/// reads (mode 0) or sets the fields of an item - each field only if it is
		/// not -1. "identified" is the object's heading, in which the original
		/// keeps the degree of identification; "flags" the three flag bits plus enchantment; "link"
		/// the quantity field.
		/// </summary>
		private int fXObjStuff(IReadOnlyList<int> pOArguments, IReadOnlyList<int> pOAddresses)
		{
			UWObject lOItem = Resolve(fArgument(pOArguments, 0));

			if (lOItem == null || mOVm == null || pOAddresses == null || pOAddresses.Count < 9)
				return 0;

			bool lbSet = fArgument(pOArguments, 1) != 0;

			for (int liField = 2; liField < 9; liField++)
			{
				int liValue = fArgument(pOArguments, liField);

				if (liValue == -1)
					continue;

				// "identified" (the heading) is neither read nor written for doors and
				// switches (major class 5) or for objects that really use their heading (render type
				// 2 in COMOBJ.DAT byte 9) - UW.EXE x_obj_stuff_ovr094_74A checks both.
				if (liField == 2 && !fHasIdentificationField(lOItem))
					continue;

				if (lbSet)
					fSetObjectField(lOItem, liField, liValue);
				else
					mOVm.WriteMemory(pOAddresses[liField], fGetObjectField(lOItem, liField));
			}

			return 0;
		}

		private bool fHasIdentificationField(UWObject pOItem)
		{
			if ((pOItem.ID >> 6) == DoorMajorClass)
				return false;

			UWCommonObjectProperties.Entry lOEntry;

			return mOData == null || mOData.CommonObjectProperties == null
				|| !mOData.CommonObjectProperties.TryGet(pOItem.ID, out lOEntry)
				|| (lOEntry.Byte9 & 0x3) != DirectionalRenderType;
		}

		/// <summary>Doors and switches - see fHasIdentificationField.</summary>
		private const int DoorMajorClass = UWObjectMechanics.DoorMajorClass;

		/// <summary>COMOBJ.DAT byte 9 render type of objects whose heading is a real direction.</summary>
		private const int DirectionalRenderType = UWObjectMechanics.DirectionalRenderType;

		/// <summary>
		/// Field reads as UW.EXE x_obj_stuff_ovr094_74A does them. "link" is the 10-bit field
		/// above the owner; its top bit 0x200 marks a quantity or special link and is masked off
		/// when reading (0x1FF). The two single flags come back as their raw bits (0x400 and
		/// 0x200 of the first word), not as 0 or 1.
		/// </summary>
		private static int fGetObjectField(UWObject pOItem, int piField)
		{
			switch (piField)
			{
				case 2: return pOItem.Heading;
				case 3: return pOItem.Owner;
				case 4: return pOItem.Flags | (pOItem.IsEnchanted ? 0x8 : 0);
				case 5: return pOItem.Quantity & 0x1FF;
				case 6: return ((pOItem.Flags >> 1) & 0x1) << 10;
				case 7: return (pOItem.Flags & 0x1) << 9;
				case 8: return pOItem.Quality;
			}

			return 0;
		}

		/// <summary>Field writes as UW.EXE x_obj_stuff_ovr094_74A does them. Writing "link" always
		/// sets bit 0x200: (value | 0x200) &amp; 0x3FF - so a script cannot store an object list link
		/// through this function. Until 2026-09-14 we wrote value &amp; 0x3FF without that bit.</summary>
		private static void fSetObjectField(UWObject pOItem, int piField, int piValue)
		{
			switch (piField)
			{
				case 2: pOItem.Heading = (ushort)(piValue & 0x7); break;
				case 3: pOItem.Owner = (ushort)(piValue & 0x3F); break;
				case 4:
					pOItem.Flags = (ushort)(piValue & 0xF);
					pOItem.IsEnchanted = (piValue & 0x8) != 0;
					break;
				case 5: pOItem.Quantity = (ushort)((piValue | 0x200) & 0x3FF); break;
				case 6: pOItem.Flags = (ushort)((pOItem.Flags & ~0x2) | ((piValue & 0x1) << 1)); break;
				case 7: pOItem.Flags = (ushort)((pOItem.Flags & ~0x1) | (piValue & 0x1)); break;
				case 8: pOItem.Quality = (ushort)(piValue & 0x3F); break;
			}
		}

		/// <summary>place_object(handle, x, y): an item from the partner's possessions goes
		/// onto a tile of the level.</summary>
		private int fPlaceObject(int piHandle, int piTileX, int piTileY)
		{
			UWObject lOItem = Resolve(piHandle);
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOItem == null || lOInventory == null || mIHost == null || !lOInventory.Remove(lOItem))
				return 0;

			fForgetInNpcSlots(lOItem);
			lOItem.Link = 0;
			lOItem.XPos = 3;
			lOItem.YPos = 3;

			return mIHost.SpawnObjectInTile(lOItem, piTileX, piTileY) ? 1 : 0;
		}

		/// <summary>remove_talker: the partner disappears from the world - display included. The
		/// conversation runs to its end as usual.</summary>
		private void fRemoveTalker()
		{
			if (mONpc == null || mIHost == null)
				return;

			mIHost.RemoveObjectFromWorld(mONpc);
		}

		private static int fArgument(IReadOnlyList<int> pOArguments, int piIndex)
		{
			return pOArguments != null && piIndex < pOArguments.Count ? pOArguments[piIndex] : 0;
		}

		/// <summary>The strings do_offer reads, and do_demand.</summary>
		private const int OfferArgumentCount = 5;

		private const int DemandArgumentCount = 2;

		/// <summary>
		/// THE LAST piCount ARGUMENTS: do_offer_ovr095_110F reads its five strings at bx-0Ah to
		/// bx-2, do_demand_ovr095_12BA its two at bx-4 and bx-2 - counted down from the argument
		/// count, so the words pushed last (the imported call hands over the stack at the count,
		/// see UWConversationVM.fSetCallResult). Most conversations push exactly that many; the
		/// generic Gray Goblin's pushes two arrays before the five strings, and ours read the
		/// arrays as the first strings, so an accepted offer said a stale word (per user,
		/// 2026-09-29: "Maybe I will." where the goblin should say "Yeah, we trade this.").
		/// </summary>
		private static IReadOnlyList<int> fLastArguments(IReadOnlyList<int> pOArguments, int piCount)
		{
			if (pOArguments == null || pOArguments.Count <= piCount)
				return pOArguments;

			List<int> lOLast = new List<int>(piCount);

			for (int liAt = pOArguments.Count - piCount; liAt < pOArguments.Count; liAt++)
				lOLast.Add(pOArguments[liAt]);

			return lOLast;
		}

		/// <summary>The pointer behind an argument - for the arrays a conversation hands over.</summary>
		private static int fAddress(IReadOnlyList<int> pOAddresses, int piIndex)
		{
			return pOAddresses != null && piIndex < pOAddresses.Count ? pOAddresses[piIndex] : 0;
		}

		/// <summary>
		/// setup_to_barter: the partner lays out his goods. He keeps the first weapon (he
		/// wields it), does not offer anything worthless, and if he has more than four things,
		/// chance replaces earlier slots (reference).
		/// </summary>
		private void fSetupToBarter()
		{
			fClearNpcSlots();

			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOInventory == null)
				return;

			bool lbSkippedWeapon = false;
			bool lbOverflow = false;
			int liSlot = 0;
			int liCount = 0;

			foreach (UWObject lOItem in lOInventory)
			{
				if (lOItem == null || ++liCount >= 40)
					continue;

				bool lbSkip = false;

				if (!lbSkippedWeapon && (lOItem.ID >> 4) == 0)
				{
					lbSkippedWeapon = true;
					lbSkip = true;
				}

				if (fGetBaseValue(lOItem.ID) == 0)
					lbSkip = true;

				// After the rolls the original reseeds with the time, so this one is free.
				if (lbOverflow && UWRandom.Next(0, 7) < 5)
					lbSkip = true;

				if (lbSkip)
					continue;

				NpcItems[liSlot] = lOItem;
				NpcSelected[liSlot] = false;
				liSlot++;

				if (liSlot >= SlotCount)
				{
					lbOverflow = true;
					liSlot = 0;
				}
			}

			Version++;
		}

		/// <summary>find_barter: the player's first marked slot with this item (or
		/// this class from 1000). Returns the slot's handle, otherwise 0.</summary>
		private int fFindBarter(int piWanted)
		{
			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				UWObject lOItem = PlayerItems[liSlot];

				if (lOItem == null || !PlayerSelected[liSlot])
					continue;

				if (fMatches(lOItem, piWanted))
					return fHandle(liSlot);
			}

			return 0;
		}

		/// <summary>Whether an item matches a search: below 1000 the object number itself,
		/// above it the class from major and minor class (reference find_barter).</summary>
		private static bool fMatches(UWObject pOItem, int piWanted)
		{
			if (piWanted < ClassBase)
				return pOItem.ID == piWanted;

			int liMajor = (piWanted - ClassBase) >> 2;
			int liMinor = (piWanted - ClassBase) & 0x3;

			return (pOItem.ID >> 6) == liMajor && ((pOItem.ID >> 4) & 0x3) == liMinor;
		}

		private int fHandle(int piSlot)
		{
			return HandleOf(PlayerItems[piSlot]);
		}

		/// <summary>
		/// find_barter_total(item, &amp;count, array, &amp;quantity): all of the player's marked slots
		/// with this item, their handles into the array, the quantity summed up. Returns 1
		/// if there is anything at all.
		/// </summary>
		private int fFindBarterTotal(IReadOnlyList<int> pOArguments, IReadOnlyList<int> pOAddresses)
		{
			if (mOVm == null || pOAddresses == null || pOAddresses.Count < 4)
				return 0;

			int liWanted = fArgument(pOArguments, 0);
			int liCountAddress = pOAddresses[1];
			int liArrayAddress = fAddress(pOAddresses, 2);
			int liQuantityAddress = pOAddresses[3];

			int liMatches = 0;
			int liQuantity = 0;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				UWObject lOItem = PlayerItems[liSlot];

				if (lOItem == null || !PlayerSelected[liSlot] || liWanted >= ClassBase || lOItem.ID != liWanted)
					continue;

				mOVm.WriteMemory(liArrayAddress + liMatches, fHandle(liSlot));
				liQuantity += fGetCount(lOItem);
				liMatches++;
			}

			mOVm.WriteMemory(liCountAddress, liMatches);
			mOVm.WriteMemory(liQuantityAddress, liQuantity);

			return liQuantity == 0 ? 0 : 1;
		}

		private static int fGetCount(UWObject pOItem)
		{
			return pOItem.HasQuantity && pOItem.Quantity > 0 && pOItem.Quantity < 512 ? pOItem.Quantity : 1;
		}

		/// <summary>
		/// do_offer(gladly, tooLittle, takenForAFool, outOfPatience, noSense): the partner
		/// weighs what the player has marked against what he has marked himself.
		/// If the evaluation is above his threshold, he accepts. Otherwise a bad
		/// offer costs patience, an insulting one twice as much; once it is used up, he stops talking.
		/// </summary>
		private int fDoOffer(IReadOnlyList<int> pOArguments)
		{
			NpcGoodsBought = false;

			int liYes = fArgument(pOArguments, 0);
			int liTooLittle = fArgument(pOArguments, 1);
			int liInsulting = fArgument(pOArguments, 2);
			int liTired = fArgument(pOArguments, 3);
			int liNoSense = fArgument(pOArguments, 4);

			if (miPatience < 0)
			{
				mOSay(liTired);
				return 0;
			}

			if (!fAnySelected(PlayerItems, PlayerSelected) || !fAnySelected(NpcItems, NpcSelected))
			{
				mOSay(liNoSense);
				return 0;
			}

			// The likes count only for what the partner receives (read 2026-09-25, do_offer_ovr095_110F).
			int liPlayerValue = fGetSideValue(PlayerItems, PlayerSelected, true, miAccuracy);
			int liNpcValue = fGetSideValue(NpcItems, NpcSelected, false, miAccuracy);

			int liEvaluation = liNpcValue <= 0 ? 100 : ((liPlayerValue - liNpcValue) * 100) / liNpcValue;

			if (liEvaluation >= miThreshold)
			{
				mOSay(liYes);
				PendingSwap = true;
				return 1;
			}

			// 0 nothing said, 1 too little, 2 insulting - as do_offer_ovr095_110F decides: the FIRST
			// offer at half the threshold or more gets no answer and costs no patience (ours said
			// "too little" there until 2026-09-25); a worse offer than the last insults; a better
			// one is too little while it closes less than two thirds of the remaining gap.
			int liVerdict;

			if (miPreviousEvaluation == 0)
				liVerdict = liEvaluation * 2 >= miThreshold ? 0 : 1;
			else if (liEvaluation < miPreviousEvaluation)
				liVerdict = 2;
			else
				liVerdict = ((miThreshold - miPreviousEvaluation) * 3) / 2 > miThreshold - liEvaluation ? 1 : 0;

			switch (liVerdict)
			{
				case 2:
					mOSay(liInsulting);
					miPatience -= 2;
					break;

				case 1:
					mOSay(liTooLittle);
					miPatience--;
					break;
			}

			miPreviousEvaluation = liEvaluation;

			return 0;
		}

		/// <summary>
		/// do_demand(ifYouInsist, no): the player demands the marked goods.
		///
		/// READ 2026-09-25 (do_demand_ovr095_12BA, whole). The player's score is his health
		/// (2 at full, 0 near death: 2 - (max - current) * 2 / max), his level, Charm / 6 and one
		/// more with the weapon drawn (PLAYER.DAT 0x5F bit 1, which DrawWeapon sets and
		/// PutAwayWeapon clears). The partner's is its health the same way, the value of the
		/// marked goods / 10, the conversation level of its creature row (row byte 0x0D, low
		/// nibble - the same value a conversation reads as npc_level) and a mood: -1 for an ally,
		/// otherwise +1 below attitude 2. The player wins when his score is HIGHER - and an ally
		/// gives in whatever the scores say.
		///
		/// GIVING IN: the line, the partner one step less friendly (down to 0), and only the
		/// partner's marked goods change sides - the player's own marked goods stay his, since
		/// the original moves them only in do_offer (ovr095_F07). REFUSING: the line, and the
		/// partner attacks (goal 5 at the player).
		///
		/// Until 2026-09-25 ours read row byte 0 (the body armour) as the partner's level, took
		/// the ally bit for hunger and so never let an ally give in, left out the drawn weapon,
		/// stopped the attitude at 1 instead of 0, and handed the player's marked goods over too.
		/// </summary>
		private int fDoDemand(IReadOnlyList<int> pOArguments)
		{
			int liGiveIn = fArgument(pOArguments, 0);
			int liRefuse = fArgument(pOArguments, 1);

			int liAttitude = mOVm != null ? mOVm.GetImportedGlobal("npc_attitude") : (mONpc != null ? mONpc.NPCAttitude : 0);

			int liPlayerScore = 1;

			if (mIHost != null && mIHost.HasPlayer)
			{
				int liMax = mIHost.PlayerMaxHitPoints;

				if (liMax > 0)
					liPlayerScore = 2 - (((liMax - mIHost.PlayerHitPoints) << 1) / liMax);

				liPlayerScore += mIHost.PlayerLevel + (mIHost.GetPlayerSkill(UWPlayerData.Skill.Charm) / 6)
					+ (mIHost.PlayerWeaponDrawn ? 1 : 0);
			}

			int liNpcScore = 1;

			if (mbHasStats && mOStats.Vitality > 0 && mONpc != null)
				liNpcScore = 2 - (((mOStats.Vitality - mONpc.HitPoints) << 1) / mOStats.Vitality);

			bool lbAlly = mONpc != null && mONpc.NPCIsAlly;
			int liMood = lbAlly ? -1 : (liAttitude < 2 ? 1 : 0);

			liNpcScore += (fGetSideValue(NpcItems, NpcSelected, false, miAccuracy) / 10)
				+ (mbHasStats ? mOStats.ConversationLevel : 0) + liMood;

			if (liPlayerScore > liNpcScore || lbAlly)
			{
				mOSay(liGiveIn);

				if (liAttitude > 0)
					fSetAttitude(liAttitude - 1);

				PendingSwap = true;
				mbPlayerKeepsHisGoods = true;
				return 1;
			}

			mOSay(liRefuse);
			fMakeHostile();

			return 0;
		}

		/// <summary>
		/// do_judgement: the player appraises the deal according to his appraisal skill - "...I
		/// think that I am getting a good deal..." from string block 7.
		/// </summary>
		private void fDoJudgement()
		{
			int liAppraise = mIHost != null && mIHost.HasPlayer ? mIHost.GetPlayerSkill(UWPlayerData.Skill.Appraise) : 0;
			int liAccuracy = 50 - ((liAppraise * 45) / 30);

			int liPlayerValue = fGetSideValue(PlayerItems, PlayerSelected, false, liAccuracy);
			int liNpcValue = fGetSideValue(NpcItems, NpcSelected, false, liAccuracy);

			int liEvaluation = liNpcValue == 0 ? 100 : ((liPlayerValue - liNpcValue) * 100) / liNpcValue;

			int liCertainty = liAppraise < 6 ? 0 : liAppraise < 12 ? 1 : liAppraise < 18 ? 2 : liAppraise < 24 ? 3 : 4;

			int liVerdict;

			if (liEvaluation > 50)
				liVerdict = 0;
			else if (liEvaluation > 35)
				liVerdict = 1;
			else if (liEvaluation > 25)
				liVerdict = 2;
			else if (liEvaluation > 10)
				liVerdict = 3;
			else if (liEvaluation > -10)
				liVerdict = 4;
			else if (liEvaluation > -25)
				liVerdict = 5;
			else if (liEvaluation > -35)
				liVerdict = 6;
			else if (liEvaluation > -50)
				liVerdict = 7;
			else
				liVerdict = 8;

			mOPrint(fGetString(JudgementBlock, JudgementCertaintyFirst + liCertainty)
				+ fGetString(JudgementBlock, JudgementJoinString)
				+ fGetString(JudgementBlock, JudgementVerdictFirst + liVerdict));
		}

		/// <summary>give_to_npc(count, array): the goods with the handles in the array go to the
		/// partner - with nothing in return, this is how you give something away.</summary>
		private int fGiveToNpc(int piCount, int piArrayAddress)
		{
			if (mOVm == null)
				return 0;

			int liSelected = 0;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (PlayerItems[liSlot] != null && PlayerSelected[liSlot])
					liSelected++;
			}

			if (liSelected < piCount)
				return 0;

			// The handles from the array, whichever end the script started at (see
			// GiveArrayFirstIndex). Entries that name no marked item are skipped - a script's own
			// array leaves the unused places as they were, and a leftover local otherwise counted
			// as a handle (per user, 2026-09-17: the guard took nothing although the trade went
			// through, and the array held a stale 242 at its first place).
			List<int> lOSlots = new List<int>();

			for (int liAt = GiveArrayFirstIndex; liAt <= piCount && lOSlots.Count < piCount; liAt++)
			{
				int liWanted = mOVm.ReadMemory(piArrayAddress + liAt);

				if (liWanted == 0)
					continue;

				for (int liSlot = 0; liSlot < SlotCount; liSlot++)
				{
					if (PlayerItems[liSlot] != null && PlayerSelected[liSlot]
						&& fHandle(liSlot) == liWanted && !lOSlots.Contains(liSlot))
					{
						lOSlots.Add(liSlot);
						break;
					}
				}
			}

			if (LogTrade)
				UWLog.Info(string.Format("[Trade] give_to_npc wants {0} of {1} marked, found {2}",
					piCount, liSelected, lOSlots.Count));

			// All or nothing, like the original (give_to_npc in UW.EXE): if one of the
			// requested handles is missing among the marked goods, nothing is handed over.
			if (lOSlots.Count < piCount)
				return 0;

			foreach (int liSlot in lOSlots)
			{
				fGiveItemToNpc(PlayerItems[liSlot]);
				PlayerItems[liSlot] = null;
				PlayerSelected[liSlot] = false;
			}

			Version++;

			return 1;
		}

		/// <summary>take_from_npc(item): the partner hands over an item from his possessions (object
		/// number, or class from 1000) - into the hand, otherwise into a free slot of the
		/// barter area, otherwise onto the floor.</summary>
		private int fTakeFromNpc(int piWanted)
		{
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOInventory == null)
				return 0;

			foreach (UWObject lOItem in lOInventory)
			{
				if (lOItem == null)
					continue;

				bool lbMatch = piWanted >= ClassBase
					? (lOItem.ID >> 4) == piWanted - ClassBase
					: lOItem.ID == piWanted;

				if (!lbMatch)
					continue;

				lOInventory.Remove(lOItem);
				fForgetInNpcSlots(lOItem);
				lOItem.Link = 0;

				// Into the hand, as in the original (take_from_npc_ovr095_1AB1: ObjectInHand) - the
				// player then puts it down where he wants. Only if the hand is full, into the
				// barter area.
				if (fGiveToHand(lOItem))
					return 1;

				for (int liSlot = 0; liSlot < SlotCount; liSlot++)
				{
					if (PlayerItems[liSlot] != null)
						continue;

					PlayerItems[liSlot] = lOItem;
					PlayerSelected[liSlot] = false;
					Version++;
					return 1;
				}

				fDropAtPlayer(lOItem);
				Version++;
				return 1;
			}

			return 0;
		}

		/// <summary>give_all_stuff: everything the partner has laid out goes to the player. It is
		/// only marked here; the handover itself happens in Swap.
		/// </summary>
		private int fGiveAllStuff()
		{
			bool lbAny = false;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				NpcSelected[liSlot] = NpcItems[liSlot] != null;
				PlayerSelected[liSlot] = false;

				if (NpcItems[liSlot] != null)
					lbAny = true;
			}

			if (lbAny)
				PendingSwap = true;

			Version++;

			return lbAny ? 1 : 0;
		}

		// ------------------------------------------------------------------
		// Inventory functions
		// ------------------------------------------------------------------

		/// <summary>
		/// show_inv(arrayIds, arrayHandles): the player's marked goods - four
		/// entries per array, object number and handle, rest zero. Returns the count.
		///
		/// The conversations compare the first array almost everywhere with object numbers (coins
		/// 160, bread 176 and so on). Carasso on level 8 compares with 1011, a class -
		/// that is how it is in the data, and that is why his food question fails in the reference too.
		/// What the original does there is unresolved.
		/// </summary>
		private int fShowInv(IReadOnlyList<int> pOAddresses)
		{
			if (mOVm == null || pOAddresses == null || pOAddresses.Count < 2)
				return 0;

			int liIds = pOAddresses[0];
			int liHandles = pOAddresses[1];
			int liCount = 0;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				UWObject lOItem = PlayerItems[liSlot];

				if (lOItem == null || !PlayerSelected[liSlot])
					continue;

				mOVm.WriteMemory(liIds + liCount, lOItem.ID);
				mOVm.WriteMemory(liHandles + liCount, fHandle(liSlot));
				liCount++;

				if (LogTrade)
					UWLog.Info(string.Format("[Trade] show_inv {0}: id {1}, handle {2}, count {3}, quantity bit {4}, quantity {5}",
						liCount, lOItem.ID, fHandle(liSlot), fGetCount(lOItem), lOItem.HasQuantity, lOItem.Quantity));
			}

			for (int liAt = liCount; liAt < SlotCount; liAt++)
			{
				mOVm.WriteMemory(liIds + liAt, 0);
				mOVm.WriteMemory(liHandles + liAt, 0);
			}

			if (LogTrade)
			{
				System.Text.StringBuilder lOText = new System.Text.StringBuilder();

				lOText.AppendFormat("[Trade] show_inv returns {0}, ids at {1}, handles at {2}:", liCount, liIds, liHandles);

				for (int liAt = 0; liAt <= SlotCount + 1; liAt++)
					lOText.AppendFormat(" [{0}] {1}/{2}", liAt, mOVm.ReadMemory(liIds + liAt), mOVm.ReadMemory(liHandles + liAt));

				UWLog.Info(lOText.ToString());
			}

			return liCount;
		}

		/// <summary>find_inv(item, who): 0 searches the partner's possessions, otherwise the
		/// player's inventory (argument order see Call). Below 1000 the object number, above it the class. Returns the handle.
		/// </summary>
		private int fFindInv(int piWho, int piWanted)
		{
			IEnumerable<UWObject> lOItems = piWho == 0
				? (IEnumerable<UWObject>)fGetNpcInventory()
				: (fInventory != null ? fInventory.EnumerateAll() : null);

			if (lOItems == null)
				return 0;

			foreach (UWObject lOItem in lOItems)
			{
				if (lOItem != null && fMatches(lOItem, piWanted))
					return HandleOf(lOItem);
			}

			return 0;
		}

		/// <summary>do_inv_create(item): a new item in best condition in the partner's
		/// possessions. Returns its handle.</summary>
		private int fDoInvCreate(int piId)
		{
			List<UWObject> lOInventory = fGetNpcInventory();
			UWObject lOItem = fCreateItem(piId, 0, FullQuality);

			if (lOInventory == null || lOItem == null)
				return 0;

			lOItem.Link = 0;
			lOInventory.Insert(0, lOItem);

			return HandleOf(lOItem);
		}

		/// <summary>do_inv_delete(item): the first item with this number disappears from
		/// the partner's possessions.</summary>
		private int fDoInvDelete(int piId)
		{
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOInventory == null)
				return 0;

			foreach (UWObject lOItem in lOInventory)
			{
				if (lOItem == null || lOItem.ID != piId)
					continue;

				lOInventory.Remove(lOItem);
				fForgetInNpcSlots(lOItem);
				Version++;

				return 1;
			}

			return 0;
		}

		/// <summary>take_from_npc_inv(n): the handle of the n-th item in the partner's
		/// possessions, counted from zero.</summary>
		private int fTakeFromNpcInv(int piIndex)
		{
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOInventory == null || piIndex < 0 || piIndex >= lOInventory.Count)
				return 0;

			return HandleOf(lOInventory[piIndex]);
		}

		/// <summary>
		/// give_ptr_npc(handle, quantity): an item of the player goes to the partner - from the
		/// barter area or directly from the inventory. A partial quantity is split off if one
		/// is requested and the stack is larger.
		/// </summary>
		private int fGivePtrNpc(int piHandle, int piQuantity)
		{
			UWObject lOItem = Resolve(piHandle);

			if (lOItem == null)
				return 0;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (PlayerItems[liSlot] != lOItem)
					continue;

				PlayerItems[liSlot] = null;
				PlayerSelected[liSlot] = false;
				fGiveItemToNpc(lOItem);
				Version++;

				return 1;
			}

			UWInventoryModel lOInventory = fInventory;

			if (lOInventory == null)
				return 0;

			if (piQuantity > 0 && lOItem.HasQuantity && lOItem.Quantity > piQuantity)
			{
				UWObject lOPart = lOItem.CloneWithQuantity(piQuantity);

				if (lOPart == null)
					return 0;

				lOItem.Quantity = (ushort)(lOItem.Quantity - piQuantity);
				lOInventory.NotifyChanged();
				fGiveItemToNpc(lOPart);

				return 1;
			}

			if (!lOInventory.RemoveItem(lOItem))
				return 0;

			fGiveItemToNpc(lOItem);

			return 1;
		}

		/// <summary>take_id_from_npc(handle): the partner hands over this item - into the
		/// hand, otherwise into the barter area, otherwise onto the floor (then 2).</summary>
		private int fTakeIdFromNpc(int piHandle)
		{
			UWObject lOItem = Resolve(piHandle);
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOItem == null)
				return 0;

			if (lOInventory != null)
				lOInventory.Remove(lOItem);

			fForgetInNpcSlots(lOItem);
			lOItem.Link = 0;

			// Into the hand, as in the original (take_id_from_npc_ovr095_1C42); otherwise barter area,
			// otherwise onto the floor (then 2).
			if (fGiveToHand(lOItem))
				return 1;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (PlayerItems[liSlot] != null)
					continue;

				PlayerItems[liSlot] = lOItem;
				PlayerSelected[liSlot] = false;
				Version++;
				return 1;
			}

			fDropAtPlayer(lOItem);
			Version++;

			return 2;
		}

		/// <summary>Attaches an item to the cursor if nothing is attached there. UWItemDrag turns
		/// it into a sticky drag.</summary>
		private bool fGiveToHand(UWObject pOItem)
		{
			UWInventoryModel lOInventory = fInventory;

			if (lOInventory == null || lOInventory.CursorItem != null)
				return false;

			// Only if the player can carry it - otherwise the barter area. Thus UW.EXE
			// take_id_from_npc_ovr095_1C42 (check before putting it in the hand); per user on the
			// original: with 0 carrying capacity the picture of Tom landed in the barter area, with us
			// in the hand (2026-09-13).
			if (!fCanCarry(pOItem))
				return false;

			lOInventory.BeginDragFromExternal(pOItem);
			Version++;

			return true;
		}

		/// <summary>Does the item still fit within the carrying capacity? Maximum load from the player
		/// data against what backpack and equipment already weigh, via UWInventoryModel.CanCarry (the
		/// same calculation as the display, UWGameUI.fRefreshWeight).</summary>
		private bool fCanCarry(UWObject pOItem)
		{
			UWInventoryModel lOInventory = fInventory;

			return lOInventory == null || lOInventory.CanCarry(pOItem);
		}

		/// <summary>
		/// identify_inv(handle, ?, &amp;text, knowledge): describes an item - the text goes as a new
		/// string number into the third argument - and returns its value in the eyes of the partner.
		///
		/// READ 2026-10-01 (identify_inv_ovr100_16B6, per user: Shak "identified" a ring for a gold
		/// piece and it stayed unidentified): the description is the article, the name, and through
		/// LookAtMagicEquipment_ovr122_34A / see MagicItemDescription_ovr122_3AC: "magical" at knowledge 2
		/// and the enchantment's name at 3 - with the KNOWLEDGE THE CONVERSATION PASSES, no Lore
		/// roll, and nothing is written into the item, so it is not identified afterwards. Shak
		/// passes 0: he names the plain item and its worth. Until then ours described it with the
		/// player's own Lore roll and kept that roll in the item.
		/// </summary>
		private int fIdentifyInv(IReadOnlyList<int> pOArguments, IReadOnlyList<int> pOAddresses)
		{
			UWObject lOItem = Resolve(fArgument(pOArguments, 0));

			if (lOItem == null || mOVm == null)
				return 0;

			if (pOAddresses != null && pOAddresses.Count >= 3 && mORegisterString != null)
			{
				string lsText = mODescribe != null ? mODescribe(lOItem, System.Math.Max(0, fArgument(pOArguments, 3))) : string.Empty;

				mOVm.WriteMemory(pOAddresses[2], mORegisterString(lsText));
			}

			// The original's identify_inv is not read; no appraisal noise here until it is.
			return fGetItemValue(lOItem, true, 0);
		}

		/// <summary>set_attitude(whoami, attitude): all characters of this conversation slot on
		/// the level get the attitude.</summary>
		private void fSetAttitudeOf(int piWhoAmI, int piAttitude)
		{
			if (fLevel == null)
				return;

			foreach (UWNpc lONpc in fEnumerateNpcs())
			{
				if (lONpc.NPCwhoami != piWhoAmI)
					continue;

				fApplyAttitude(lONpc, piAttitude);
			}
		}

		/// <summary>
		/// set_race_attitude(race, attitude, range): the partner's kin within range
		/// get the attitude. DEVIATION: the reference checks the faction from the
		/// creature table for this, which we do not read - here only the same object number counts.
		/// </summary>
		private void fSetRaceAttitude(int piRace, int piAttitude, int piRange)
		{
			if (mONpc == null || fLevel == null)
				return;

			int liRange = piRange <= 0 ? 1 : piRange;

			foreach (UWNpc lONpc in fEnumerateNpcs())
			{
				if (lONpc.ID != mONpc.ID)
					continue;

				if (Math.Abs(lONpc.TileX - mONpc.TileX) > liRange || Math.Abs(lONpc.TileY - mONpc.TileY) > liRange)
					continue;

				fApplyAttitude(lONpc, piAttitude);
			}
		}

		private IEnumerable<UWNpc> fEnumerateNpcs()
		{
			UWLevel lOLevel = fLevel;

			if (lOLevel == null || lOLevel.TileData == null)
				yield break;

			foreach (UWTile lOTile in lOLevel.TileData)
			{
				if (lOTile == null || lOTile.ObjectsInTile == null)
					continue;

				foreach (UWObject lOObject in lOTile.ObjectsInTile)
				{
					UWNpc lONpc = lOObject as UWNpc;

					if (lONpc != null && lONpc.ID >= 64 && lONpc.ID <= 127)
						yield return lONpc;
				}
			}
		}

		private void fApplyAttitude(UWNpc pONpc, int piAttitude)
		{
			pONpc.NPCAttitude = (byte)(piAttitude & 0x3);

			if (pONpc == mONpc && mOVm != null)
				mOVm.SetImportedGlobal("npc_attitude", piAttitude);

			if (piAttitude != 0 || mIHost == null)
				return;

			mIHost.AngerNpc(pONpc);
		}

		// ------------------------------------------------------------------
		// Values
		// ------------------------------------------------------------------

		private static bool fAnySelected(UWObject[] pOItems, bool[] pbSelected)
		{
			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (pOItems[liSlot] != null && pbSelected[liSlot])
					return true;
			}

			return false;
		}

		private int fGetSideValue(UWObject[] pOItems, bool[] pbSelected, bool pbLikes, int piAccuracy)
		{
			int liTotal = 0;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (pOItems[liSlot] != null && pbSelected[liSlot])
					liTotal += fGetItemValue(pOItems[liSlot], pbLikes, piAccuracy);
			}

			return liTotal;
		}

		/// <summary>
		/// The value of a traded item as its partner sees it. READ 2026-09-25 (ovr095_16B1 with
		/// ovr095_1FAD and ovr095_17B9, called through ovr095_1647 by do_offer, do_demand and
		/// do_judgement): the money value of COMOBJ.DAT; on the side the partner RECEIVES (the
		/// player's goods in an offer) a liked item counts 3/2 and a disliked one nothing; times
		/// the count of a stack; times quality / 64, at least 1 - and quality 0 is worth nothing;
		/// then the appraisal: plus or minus up to piAccuracy percent, from a random generator
		/// seeded with the item's object number, so one item always comes out the same.
		///
		/// Until 2026-09-25 ours counted a stack as one item, gave coins full quality, applied the
		/// likes to both sides, checked the dislikes first and left the appraisal out.
		/// </summary>
		private int fGetItemValue(UWObject pOItem, bool pbLikes, int piAccuracy)
		{
			int liValue = fGetBaseValue(pOItem.ID);

			if (pbLikes)
			{
				int liLike = fLikes(pOItem.ID);

				if (liValue == 0 || liLike < 0)
					return 0;

				if (liLike > 0)
					liValue = (short)(liValue * 3) >> 1;
			}

			// The products in 16 bits, as ovr095_16B1 multiplies (IMUL into AX alone).
			if (pOItem.HasQuantity && pOItem.Quantity < UWObjectMechanics.SpecialPropertyThreshold)
				liValue = (short)(liValue * pOItem.Quantity);

			int liQuality = pOItem.Quality & UWObjectMechanics.MaxQuality;

			if (liValue > 0)
				liValue = liQuality > 0 ? Math.Max(1, (short)(liValue * liQuality) >> 6) : 0;

			return new UWOriginalRandom(fObjectNumber(pOItem)).Noise(liValue, -piAccuracy, piAccuracy);
		}

		/// <summary>
		/// The partner's opinion of an object (ovr095_1FAD): 1 liked, -1 disliked, 0 neither. An
		/// EXACT like decides at once; a class like (1000 + id / 16) holds unless a dislike
		/// follows - an exact dislike or a class dislike overrides it.
		/// </summary>
		private int fLikes(int piId)
		{
			int liResult = 0;

			int liLike = fFindInList(miLikesPointer, piId);

			if (liLike == ExactMatch)
				return 1;

			if (liLike == ClassMatch)
				liResult = 1;

			int liDislike = fFindInList(miDislikesPointer, piId);

			if (liDislike == ExactMatch)
				return -1;

			if (liDislike == ClassMatch)
				liResult = -1;

			return liResult;
		}

		private const int NoMatch = 0;

		private const int ExactMatch = 1;

		private const int ClassMatch = 2;

		/// <summary>A list terminated by -1 in conversation memory: object numbers or
		/// classes from 1000 (set_likes_dislikes). An exact entry ends the search; a class
		/// entry is remembered and the search goes on, as the original walks it.</summary>
		private int fFindInList(int piPointer, int piId)
		{
			if (piPointer == 0 || mOVm == null)
				return NoMatch;

			int liResult = NoMatch;

			for (int liAt = 0; liAt < 64; liAt++)
			{
				int liEntry = mOVm.ReadMemory(piPointer + liAt);

				if (liEntry == -1)
					break;

				if (liEntry < ClassBase && liEntry == piId)
					return ExactMatch;

				if (liEntry >= ClassBase && (piId >> 4) == liEntry - ClassBase)
					liResult = ClassMatch;
			}

			return liResult;
		}

		/// <summary>An object's number for the appraisal seed: its place in the level's object
		/// list or in the inventory records, as the original seeds with the object's index;
		/// otherwise a number fixed for this object.</summary>
		/// <summary>The object's number in the level's object list - for a carried item the static
		/// slot it holds there (UWCarriedSlots; until 2026-09-29 ours had none for it and seeded
		/// with a hash, so the player's side of an offer came out random).</summary>
		private int fObjectNumber(UWObject pOObject)
		{
			return UWCarriedSlots.NumberOf(pOObject);
		}

		private int fGetBaseValue(int piId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			return mOData != null && mOData.CommonObjectProperties != null
				&& mOData.CommonObjectProperties.TryGet(piId, out lOEntry)
				? lOEntry.Value
				: 0;
		}

		private string fGetString(int piBlock, int piIndex)
		{
			try
			{
				return UWTextLayout.CleanText(mOData.Strings.Blocks[piBlock].Strings[piIndex]);
			}
			catch
			{
				return string.Empty;
			}
		}

		// ------------------------------------------------------------------
		// Partner's possessions
		// ------------------------------------------------------------------

		/// <summary>The creature's object chain, extended by the rolled loot the first
		/// time.</summary>
		private List<UWObject> fGetNpcInventory()
		{
			UWLevel lOLevel = fLevel;

			if (mONpc == null || lOLevel == null)
				return null;

			mONpc.EnsureContentsLoaded(lOLevel.Masterlist);

			if (mONpc.Contents == null)
				return null;

			if (!mONpc.NPCLootSpawned)
			{
				mONpc.NPCLootSpawned = true;

				if (mbHasStats && mOData != null)
				{
					List<UWCritterLoot.Drop> lODrops = UWCritterLoot.Generate(mOStats,
						mOData.CommonObjectProperties, lOLevel.LevelNumber, mOData.ObjectProperties);

					foreach (UWCritterLoot.Drop lODrop in lODrops)
					{
						UWObject lOItem = fCreateItem(lODrop.ObjectId, lODrop.Quantity, lODrop.Quality);

						if (lOItem == null)
							continue;

						// Each piece takes a static slot (DropNPCLoot_ovr150_516 through
						// GetFreeObject), so what the player splits off afterwards gets the
						// next one - see UWCarriedSlots.
						if (UWCarriedSlots.Level != null)
							lOItem.SlotIndex = UWCarriedSlots.Level.TakeStaticSlot();

						mONpc.Contents.Add(lOItem);
					}
				}
			}

			return mONpc.Contents;
		}

		/// <summary>A new item, as UWLevelLoader.SpawnObjectById builds it - only
		/// without a place in the world.</summary>
		private UWObject fCreateItem(int piId, int piQuantity, int piQuality)
		{
			UWObject lOItem = new UWObject((ushort)piId);

			if (piQuantity > 0)
			{
				lOItem.HasQuantity = true;
				lOItem.Quantity = (ushort)piQuantity;
			}

			if (piQuality >= 0)
				lOItem.Quality = (ushort)piQuality;

			try
			{
				lOItem.Texture = mOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piId);
			}
			catch
			{
				return null;
			}

			lOItem.WasSpawned = true;

			return lOItem;
		}

		private void fGiveItemToNpc(UWObject pOItem)
		{
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOInventory == null)
			{
				fDropAtPlayer(pOItem);
				return;
			}

			// At the front, like the reference (at the head of the chain).
			pOItem.Link = 0;
			lOInventory.Insert(0, pOItem);
		}

		private void fForgetInNpcSlots(UWObject pOItem)
		{
			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (NpcItems[liSlot] == pOItem)
				{
					NpcItems[liSlot] = null;
					NpcSelected[liSlot] = false;
				}
			}
		}

		/// <summary>do_decline(0): every good on the partner's side goes back to him - a bought
		/// one the player left there too, it returns into his possession.</summary>
		private void fClearNpcSlots()
		{
			List<UWObject> lOInventory = null;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (NpcBought[liSlot] && NpcItems[liSlot] != null)
				{
					if (lOInventory == null)
						lOInventory = fGetNpcInventory();

					if (lOInventory != null && !lOInventory.Contains(NpcItems[liSlot]))
						lOInventory.Add(NpcItems[liSlot]);
				}

				NpcItems[liSlot] = null;
				NpcSelected[liSlot] = false;
				NpcBought[liSlot] = false;
			}

			Version++;
		}

		/// <summary>Carries out an accepted deal: the player's marked goods go to the partner, the
		/// partner's marked goods stay on his side as the player's (NpcBought). After a demand
		/// the player's own marked goods stay on his side.</summary>
		public void Swap()
		{
			bool lbPlayerKeeps = mbPlayerKeepsHisGoods;

			PendingSwap = false;
			mbPlayerKeepsHisGoods = false;

			for (int liSlot = 0; liSlot < SlotCount && !lbPlayerKeeps; liSlot++)
			{
				if (PlayerItems[liSlot] == null || !PlayerSelected[liSlot])
					continue;

				fGiveItemToNpc(PlayerItems[liSlot]);
				PlayerItems[liSlot] = null;
				PlayerSelected[liSlot] = false;
			}

			// do_decline(1): the unmarked goods go back, the marked ones stay where they are - now
			// the player's, to be dragged away (NpcBought).
			List<UWObject> lOInventory = fGetNpcInventory();

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				UWObject lOItem = NpcItems[liSlot];

				if (lOItem == null)
					continue;

				if (!NpcSelected[liSlot] && !NpcBought[liSlot])
				{
					NpcItems[liSlot] = null;
					continue;
				}

				if (lOInventory != null)
					lOInventory.Remove(lOItem);

				lOItem.Link = 0;
				NpcBought[liSlot] = true;
			}

			NpcGoodsBought = true;
			Version++;
		}

		/// <summary>At the end of the conversation: what the player still has laid out falls onto his
		/// tile, at a random spot - NOT back into the backpack (reference
		/// conversationvm.cs at conversation end, confirmed by user on the original, 2026-09-13).
		/// The partner's display is only a view of his possessions.</summary>
		public void EndConversation()
		{
			if (PendingSwap)
				Swap();

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				UWObject lOItem = PlayerItems[liSlot];

				PlayerItems[liSlot] = null;
				PlayerSelected[liSlot] = false;

				if (lOItem == null)
					continue;

				// end_barter_ovr095_3C2 puts the player's side down with the same routine as the
				// partner's, around the player object with radius 5 (per user, 2026-09-29: in
				// both games the item lies on the floor).
				if (mIHost != null)
					mIHost.DropAroundPlayer(lOItem);
			}

			// end_barter_ovr095_3C2: what still lies on the partner's side goes down around him -
			// bought goods the player did not take. Unbought goods have been his all along.
			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (!NpcBought[liSlot] || NpcItems[liSlot] == null)
					continue;

				UWObject lOItem = NpcItems[liSlot];

				NpcItems[liSlot] = null;
				NpcSelected[liSlot] = false;
				NpcBought[liSlot] = false;

				if (mIHost != null)
					mIHost.DropAtNpc(lOItem, mONpc);
			}

			fClearNpcSlots();
		}

		private void fDropAtPlayer(UWObject pOItem)
		{
			if (mIHost == null || pOItem == null)
				return;

			mIHost.DropAtPlayer(pOItem);
		}

		private void fSetAttitude(int piAttitude)
		{
			if (mOVm != null)
				mOVm.SetImportedGlobal("npc_attitude", piAttitude);

			if (mONpc != null)
				mONpc.NPCAttitude = (byte)piAttitude;
		}

		/// <summary>The partner turns hostile (reference: npc.SetGoalAndGtarg(talker, 5, 1)).
		/// With us: attitude to zero, and the creature attacks.</summary>
		private void fMakeHostile()
		{
			fSetAttitude(0);

			if (mIHost != null && mONpc != null)
				mIHost.AngerNpc(mONpc);
		}

		// ------------------------------------------------------------------
		// Player's barter area - for UWItemDrag
		// ------------------------------------------------------------------

		/// <summary>Takes the item out of one of the player's slots (onto the cursor).</summary>
		public UWObject TakeFromPlayerSlot(int piSlot)
		{
			if (piSlot < 0 || piSlot >= SlotCount)
				return null;

			UWObject lOItem = PlayerItems[piSlot];

			PlayerItems[piSlot] = null;
			PlayerSelected[piSlot] = false;
			Version++;

			return lOItem;
		}

		/// <summary>Puts an item into one of the player's slots, marked. If one is already
		/// there, that one is returned - for swapping on the cursor.</summary>
		public UWObject PutInPlayerSlot(int piSlot, UWObject pOItem)
		{
			if (piSlot < 0 || piSlot >= SlotCount || pOItem == null)
				return pOItem;

			UWObject lOPrevious = PlayerItems[piSlot];

			PlayerItems[piSlot] = pOItem;
			PlayerSelected[piSlot] = true;
			Version++;

			return lOPrevious;
		}

		/// <summary>A slot's item has changed in place - a stack dropped onto it grew (see
		/// UWItemDrag); redraws the area.</summary>
		public void PlayerSlotChanged(int piSlot)
		{
			if (piSlot >= 0 && piSlot < SlotCount)
				Version++;
		}

		/// <summary>A good on the partner's side changed in place - part of a bought stack was
		/// taken; redraws the area.</summary>
		public void NpcSlotChanged(int piSlot)
		{
			if (piSlot >= 0 && piSlot < SlotCount)
				Version++;
		}

		public void TogglePlayerSelected(int piSlot)
		{
			if (piSlot < 0 || piSlot >= SlotCount)
				return;

			PlayerSelected[piSlot] = PlayerItems[piSlot] != null && !PlayerSelected[piSlot];
			Version++;
		}

		/// <summary>Takes a bought good off the partner's side (onto the cursor), when flag_4872
		/// allows it - see NpcBought.</summary>
		public UWObject TakeFromNpcSlot(int piSlot)
		{
			if (!NpcGoodsBought || piSlot < 0 || piSlot >= SlotCount || !NpcBought[piSlot])
				return null;

			UWObject lOItem = NpcItems[piSlot];

			NpcItems[piSlot] = null;
			NpcSelected[piSlot] = false;
			NpcBought[piSlot] = false;
			Version++;

			return lOItem;
		}

		/// <summary>Puts a bought good back onto its place on the partner's side - a take the
		/// weight did not allow.</summary>
		public void ReturnToNpcSlot(int piSlot, UWObject pOItem)
		{
			if (piSlot < 0 || piSlot >= SlotCount || pOItem == null || NpcItems[piSlot] != null)
				return;

			NpcItems[piSlot] = pOItem;
			NpcSelected[piSlot] = true;
			NpcBought[piSlot] = true;
			Version++;
		}

		public void ToggleNpcSelected(int piSlot)
		{
			if (piSlot < 0 || piSlot >= SlotCount)
				return;

			NpcSelected[piSlot] = NpcItems[piSlot] != null && !NpcSelected[piSlot];
			Version++;
		}
	}
}
