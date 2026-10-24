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
	/// UWCritterLoot rolls when the conversation starts (GenerateInventory, since 2026-10-19; on the
	/// first trade until then) - just like on death -, and the creature remembers that
	/// (NPCLootSpawned) so it does not happen twice.
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
	/// UWInventoryModel of P2. UWItemDrag and UWConversationView work on the slot arrays
	/// as before.
	/// </summary>
	public class UWConversationTrade
	{
		public const int SlotCount = 4;

		/// <summary>setup_to_barter's roll once the slots are full: rand() &amp; 7, one of eight.</summary>
		public const int BarterOverflowRoll = 8;

		/// <summary>... and an item is passed over below five - five in eight.</summary>
		public const int BarterOverflowSkipBelow = 5;

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
		private readonly Action<string> mOPlayerSay;
		private readonly Func<string, int> mORegisterString;
		private readonly UWOriginalRandom mORandom;

		/// <summary>The world the conversation takes place in, or null without one (the host's world
		/// block until cleanup step 3.6, 2026-10-17).</summary>
		private IUWWorld fWorld => mIHost != null ? mIHost.World : null;

		/// <summary>The player's inventory, or null without one.</summary>
		private UWInventoryModel fInventory => fWorld != null ? fWorld.Player.Inventory : null;

		/// <summary>The level the conversation takes place on, or null without a world.</summary>
		private UWLevel fLevel => fWorld != null ? fWorld.Level : null;

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
			Action<int> pOSay, Action<string> pOPlayerSay, Func<string, int> pORegisterString)
		{
			mOData = pOData;
			mONpc = pONpc;
			mIHost = pIHost;
			mOSay = pOSay;
			mOPlayerSay = pOPlayerSay;
			mORegisterString = pORegisterString;

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

		/// <summary>
		/// The trade's conversation functions (UWConversationFunctionTable, since 2026-10-14). Until
		/// then a static Handles list and the switch of a Call method, which had to name the same
		/// functions by hand. Each function first takes the machine of the call, as Call did.
		/// </summary>
		internal static void RegisterFunctions(UWConversationFunctionTable pOTable)
		{
			fRegister(pOTable, "setup_to_barter", (pOTrade, pOCall) =>
			{
				pOTrade.fSetupToBarter();
				return 0;
			});

			fRegister(pOTable, "find_barter", (pOTrade, pOCall) => pOTrade.fFindBarter(pOCall.Argument(0)));

			fRegister(pOTable, "find_barter_total", (pOTrade, pOCall) => pOTrade.fFindBarterTotal(pOCall.Arguments, pOCall.Addresses));

			fRegister(pOTable, "do_offer", (pOTrade, pOCall) => pOTrade.fDoOffer(fLastArguments(pOCall.Arguments, OfferArgumentCount)));

			fRegister(pOTable, "do_demand", (pOTrade, pOCall) => pOTrade.fDoDemand(fLastArguments(pOCall.Arguments, DemandArgumentCount)));

			fRegister(pOTable, "do_judgement", (pOTrade, pOCall) =>
			{
				pOTrade.fDoJudgement();
				return 0;
			});

			fRegister(pOTable, "do_decline", (pOTrade, pOCall) =>
			{
				pOTrade.fReturnNpcGoods(false);
				return 0;
			});

			// The ADDRESSES of the two lists, not the words behind them: set_likes_dislikes_ovr095_1F7B
			// hands the pushed numbers to ovr093_226B, which makes them pointers into the
			// conversation's memory (Shak and Zak push 37 and 58, where their lists begin).
			// Until 2026-09-29 ours took the first entry of each list as its address and so
			// read neither list.
			fRegister(pOTable, "set_likes_dislikes", (pOTrade, pOCall) =>
			{
				pOTrade.miLikesPointer = fAddress(pOCall.Addresses, 0);
				pOTrade.miDislikesPointer = fAddress(pOCall.Addresses, 1);
				return 0;
			});

			// The array comes as an ADDRESS, not as a value - taking the value handed us the
			// contents of the local instead of the list (per user, 2026-09-17: a stale 242).
			fRegister(pOTable, "give_to_npc", (pOTrade, pOCall) => pOTrade.fGiveToNpc(pOCall.Argument(0), fAddress(pOCall.Addresses, 1)));

			fRegister(pOTable, "take_from_npc", (pOTrade, pOCall) => pOTrade.fTakeFromNpc(pOCall.Argument(0)));

			fRegister(pOTable, "show_inv", (pOTrade, pOCall) => pOTrade.fShowInv(pOCall.Addresses));

			// find_inv(item, where): FIRST the item is pushed, THEN the search location (0 partner,
			// otherwise player) - like that in all conversations, and the reference reads the search
			// location from the top of the stack. Swapped until 2026-09-14: we searched for object 1
			// on the player, Judy (level 4) never found the picture of Tom (272), the reply to it was
			// missing (per user). The other functions have the first argument at the bottom, as before.
			fRegister(pOTable, "find_inv", (pOTrade, pOCall) => pOTrade.fFindInv(pOCall.Argument(1), pOCall.Argument(0)));

			fRegister(pOTable, "count_inv", (pOTrade, pOCall) =>
			{
				UWObject lOItem = pOTrade.Resolve(pOCall.Argument(0));

				if (LogTrade)
					UWLog.Info(string.Format("[Trade] count_inv handle {0} -> {1}",
						pOCall.Argument(0), lOItem != null ? fGetCount(lOItem) : 0));

				return lOItem != null ? fGetCount(lOItem) : 0;
			});

			fRegister(pOTable, "do_inv_create", (pOTrade, pOCall) => pOTrade.fDoInvCreate(pOCall.Argument(0)));

			fRegister(pOTable, "do_inv_delete", (pOTrade, pOCall) => pOTrade.fDoInvDelete(pOCall.Argument(0)));

			fRegister(pOTable, "take_from_npc_inv", (pOTrade, pOCall) => pOTrade.fTakeFromNpcInv(pOCall.Argument(0)));

			fRegister(pOTable, "give_ptr_npc", (pOTrade, pOCall) => pOTrade.fGivePtrNpc(pOCall.Argument(0), pOCall.Argument(1)));

			fRegister(pOTable, "take_id_from_npc", (pOTrade, pOCall) => pOTrade.fTakeIdFromNpc(pOCall.Argument(0)));

			fRegister(pOTable, "identify_inv", (pOTrade, pOCall) => pOTrade.fIdentifyInv(pOCall.Arguments, pOCall.Addresses));

			fRegister(pOTable, "set_attitude", (pOTrade, pOCall) =>
			{
				pOTrade.fSetAttitudeOf(pOCall.Argument(0), pOCall.Argument(1));
				return 0;
			});

			fRegister(pOTable, "set_race_attitude", (pOTrade, pOCall) =>
			{
				pOTrade.fSetRaceAttitude(pOCall.Argument(0), pOCall.Argument(1), pOCall.Argument(2));
				return 0;
			});

			fRegister(pOTable, "check_inv_quality", (pOTrade, pOCall) =>
			{
				UWObject lOItem = pOTrade.Resolve(pOCall.Argument(0));

				return lOItem != null ? lOItem.Quality : 0;
			});

			fRegister(pOTable, "set_inv_quality", (pOTrade, pOCall) =>
			{
				UWObject lOItem = pOTrade.Resolve(pOCall.Argument(0));

				if (lOItem != null)
					lOItem.Quality = (ushort)(pOCall.Argument(1) & 0x3F);

				return 0;
			});

			fRegister(pOTable, "x_obj_stuff", (pOTrade, pOCall) => pOTrade.fXObjStuff(pOCall.Arguments, pOCall.Addresses));

			fRegister(pOTable, "place_object", (pOTrade, pOCall) => pOTrade.fPlaceObject(pOCall.Argument(0), pOCall.Argument(1), pOCall.Argument(2)));

			fRegister(pOTable, "remove_talker", (pOTrade, pOCall) =>
			{
				pOTrade.fRemoveTalker();
				return 0;
			});
		}

		/// <summary>Registers one trade function: the session's trade runs it with the call's machine.</summary>
		private static void fRegister(UWConversationFunctionTable pOTable, string psName,
			Func<UWConversationTrade, UWConversationFunctionTable.Call, int> pOFunction)
		{
			pOTable.Register(psName, UWConversationFunctionTable.Provider.Trade, (pOSession, pOCall) =>
			{
				UWConversationTrade lOTrade = pOSession.Trade;

				lOTrade.mOVm = pOCall.Vm;

				return pOFunction(lOTrade, pOCall);
			});
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
			UWObject lOItem = Resolve(UWConversationVM.Argument(pOArguments, 0));

			if (lOItem == null || mOVm == null || pOAddresses == null || pOAddresses.Count < 9)
				return 0;

			bool lbSet = UWConversationVM.Argument(pOArguments, 1) != 0;

			for (int liField = FirstObjectField; liField < FirstObjectField + mOObjectFields.Length; liField++)
			{
				int liValue = UWConversationVM.Argument(pOArguments, liField);

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
			if (UWObjectIds.Major(pOItem.ID) == DoorMajorClass)
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

		/// <summary>One field of x_obj_stuff: how it is read and how it is written.</summary>
		private readonly struct ObjectField
		{
			public readonly Func<UWObject, int> Get;

			public readonly Action<UWObject, int> Set;

			public ObjectField(Func<UWObject, int> pOGet, Action<UWObject, int> pOSet)
			{
				Get = pOGet;
				Set = pOSet;
			}
		}

		/// <summary>The first field of x_obj_stuff after handle and mode.</summary>
		private const int FirstObjectField = 2;

		/// <summary>
		/// The fields of x_obj_stuff, "identified" to "quality", read and written as UW.EXE
		/// x_obj_stuff_ovr094_74A does it - one row per field since 2026-10-14 (before that two
		/// parallel switches, fGetObjectField and fSetObjectField).
		///
		/// READING: "link" is the 10-bit field above the owner; its top bit 0x200 marks a quantity
		/// or special link and is masked off (0x1FF). The two single flags come back as their raw
		/// bits (0x400 and 0x200 of the first word), not as 0 or 1. "flags" is OBJ_FLAGS, the four
		/// bits with the enchanted bit on top - since 2026-10-12 UWObject.Flags holds exactly those
		/// (B13).
		///
		/// WRITING "link" always sets bit 0x200: (value | 0x200) &amp; 0x3FF - so a script cannot
		/// store an object list link through this function. Until 2026-09-14 we wrote value &amp;
		/// 0x3FF without that bit. SET_FLAGS writes all four bits, the enchanted bit among them.
		/// </summary>
		private static readonly ObjectField[] mOObjectFields =
		{
			// 2 identified (the heading)
			new ObjectField(pOItem => pOItem.Heading,
				(pOItem, piValue) => pOItem.Heading = (ushort)(piValue & 0x7)),
			// 3 owner
			new ObjectField(pOItem => pOItem.Owner,
				(pOItem, piValue) => pOItem.Owner = (ushort)(piValue & 0x3F)),
			// 4 flags
			new ObjectField(pOItem => pOItem.Flags & UWObjectRecordCodec.FlagsMask,
				(pOItem, piValue) => pOItem.Flags = (ushort)(piValue & UWObjectRecordCodec.FlagsMask)),
			// 5 link
			new ObjectField(pOItem => pOItem.Quantity & 0x1FF,
				(pOItem, piValue) => pOItem.Quantity = (ushort)((piValue | 0x200) & 0x3FF)),
			// 6 flag1
			new ObjectField(pOItem => ((pOItem.Flags >> 1) & 0x1) << 10,
				(pOItem, piValue) => pOItem.Flags = (ushort)((pOItem.Flags & ~0x2) | ((piValue & 0x1) << 1))),
			// 7 flag0
			new ObjectField(pOItem => (pOItem.Flags & 0x1) << 9,
				(pOItem, piValue) => pOItem.Flags = (ushort)((pOItem.Flags & ~0x1) | (piValue & 0x1))),
			// 8 quality
			new ObjectField(pOItem => pOItem.Quality,
				(pOItem, piValue) => pOItem.Quality = (ushort)(piValue & 0x3F)),
		};

		private static int fGetObjectField(UWObject pOItem, int piField)
		{
			int liRow = piField - FirstObjectField;

			return liRow >= 0 && liRow < mOObjectFields.Length ? mOObjectFields[liRow].Get(pOItem) : 0;
		}

		private static void fSetObjectField(UWObject pOItem, int piField, int piValue)
		{
			int liRow = piField - FirstObjectField;

			if (liRow >= 0 && liRow < mOObjectFields.Length)
				mOObjectFields[liRow].Set(pOItem, piValue);
		}

		/// <summary>place_object(handle, x, y): an item from the partner's possessions goes
		/// onto a tile of the level - or, with a negative x, down at the player's feet (BABLHACK.C,
		/// read 2026-10-12; ours answered 0 there until then).</summary>
		private int fPlaceObject(int piHandle, int piTileX, int piTileY)
		{
			UWObject lOItem = Resolve(piHandle);
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOItem == null || lOInventory == null || mIHost == null || !lOInventory.Remove(lOItem))
				return 0;

			lOItem.Link = 0;

			if (piTileX < 0)
			{
				if (fWorld != null)
					fWorld.DropAtPlayer(lOItem);

				return 1;
			}
			lOItem.XPos = 3;
			lOItem.YPos = 3;

			// SET_Z to the tile's floor (place_object, BABLHACK.C): an inventory item's own height
			// must not lift it, the host places at this spot and never below the floor.
			lOItem.ZPos = 0;

			// At the item's own fine spot (place_object sets only z, to the floor; 3/3 above), not
			// the tile centre. Per user in the original 2026-10-17 (audit round 2 B4): Judy's Key of
			// Love lay on 33/8 at 3/3; ours saved it at 4/4 when this went through the loader's
			// SpawnObjectInTile, which takes the centre.
			return fWorld != null && fWorld.Spawn(lOItem, new UWTilePos(piTileX, piTileY)) ? 1 : 0;
		}

		/// <summary>remove_talker: the partner disappears from the world - display included. The
		/// conversation runs to its end as usual.</summary>
		private void fRemoveTalker()
		{
			if (mONpc == null || fWorld == null)
				return;

			fWorld.Remove(mONpc);
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

		/// <summary>setup_to_barter looks at this many items of the partner's chain at most (BARTER.C,
		/// count++ &lt; 0x28).</summary>
		private const int BarterLookLimit = 0x28;

		/// <summary>
		/// setup_to_barter (UnderworldExhumed BARTER.C, read again 2026-10-15): the partner lays out
		/// his goods, and what lies in his slots LEAVES HIS POSSESSIONS (Obj_Rem) - fReturnNpcGoods is
		/// the way back, end_barter drops what is left at his feet (EndConversation).
		///
		/// THE WALK follows his chain from its head and looks at 0x28 items at most. Passed over are:
		/// the first item of minor class 0 (bits 4-5 of the id clear - the weapons 0x00-0x0F, but also
		/// the bags 0x80-0x8F and the 0xC0 group); anything COMOBJ.DAT values at 0; and, once all four
		/// slots have been filled, five in eight of the rest - rand() &amp; 7 below 5, rolled only for
		/// an item the first two tests would lay out (the || stops before the roll). An item of minor
		/// class 0 that is passed over for any of the three reasons counts as the held-back one. A
		/// slot filled again gives its old item back to the head of the chain (Obj_Add); the walk
		/// stops when it meets the first item it gave back - which it cannot, since the head lies
		/// behind it, so in effect it ends at the chain's end or after 0x28 items.
		///
		/// The slots are not emptied first and a slot's mark is not cleared: a second call finds
		/// them as the last one left them (only reachable in a second trade round, which no shipped
		/// conversation has - after a trade the conversation ends, per user 2026-10-15).
		///
		/// Until 2026-10-15 ours held back only ids 0-15, rolled for every item once the slots were
		/// full, looked at 39 items, emptied the slots first and left the laid-out goods in the
		/// partner's possessions (the slots were a view of them).
		/// </summary>
		private void fSetupToBarter()
		{
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOInventory == null)
				return;

			bool lbSkippedMinorZero = false;
			bool lbAllFilled = false;
			int liSlot = 0;
			int liCount = 0;
			UWObject lOFirstGivenBack = null;
			UWObject lOItem = lOInventory.Count > 0 ? lOInventory[0] : null;

			while (lOItem != null && lOItem != lOFirstGivenBack && liCount++ < BarterLookLimit)
			{
				int liAt = lOInventory.IndexOf(lOItem);
				UWObject lONext = liAt >= 0 && liAt + 1 < lOInventory.Count ? lOInventory[liAt + 1] : null;
				bool lbMinorZero = UWObjectIds.Minor(lOItem.ID) == 0;

				// The session's generator: barter_init has reseeded the original's rand from the
				// clock before this.
				if ((lbMinorZero && !lbSkippedMinorZero) || fGetBaseValue(lOItem.ID) == 0
					|| (lbAllFilled && UWGameSession.Current.Random.Next(BarterOverflowRoll) < BarterOverflowSkipBelow))
				{
					if (lbMinorZero)
						lbSkippedMinorZero = true;

					lOItem = lONext;
					continue;
				}

				lOInventory.Remove(lOItem);
				lOItem.Link = 0;

				if (NpcItems[liSlot] != null)
				{
					if (lOFirstGivenBack == null)
						lOFirstGivenBack = NpcItems[liSlot];

					lOInventory.Insert(0, NpcItems[liSlot]);
				}

				NpcItems[liSlot] = lOItem;
				NpcBought[liSlot] = false;
				liSlot++;

				if (liSlot >= SlotCount)
				{
					liSlot = 0;
					lbAllFilled = true;
				}

				lOItem = lONext;
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

			return UWObjectIds.Major(pOItem.ID) == liMajor && UWObjectIds.Minor(pOItem.ID) == liMinor;
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

			int liWanted = UWConversationVM.Argument(pOArguments, 0);
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
			return UWStackRules.CountAtLeastOne(pOItem);
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

			int liYes = UWConversationVM.Argument(pOArguments, 0);
			int liTooLittle = UWConversationVM.Argument(pOArguments, 1);
			int liInsulting = UWConversationVM.Argument(pOArguments, 2);
			int liTired = UWConversationVM.Argument(pOArguments, 3);
			int liNoSense = UWConversationVM.Argument(pOArguments, 4);

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
		/// GIVING IN: the line, the partner one step less friendly in npc_attitude (down to 0),
		/// and only the partner's marked goods change sides - the player's own marked goods stay
		/// his, since the original moves them only in do_offer (ovr095_F07). REFUSING: the line,
		/// all his goods back to him, and his object's goal set to attack the player (see the
		/// refusal below).
		///
		/// Until 2026-09-25 ours read row byte 0 (the body armour) as the partner's level, took
		/// the ally bit for hunger and so never let an ally give in, left out the drawn weapon,
		/// stopped the attitude at 1 instead of 0, and handed the player's marked goods over too.
		/// </summary>
		private int fDoDemand(IReadOnlyList<int> pOArguments)
		{
			int liGiveIn = UWConversationVM.Argument(pOArguments, 0);
			int liRefuse = UWConversationVM.Argument(pOArguments, 1);

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

			// Refused, every good on his side goes back to him (ReturnTradeObjectsToNPC(0)), and the
			// OBJECT is set to attack the player (change_critter_goal(talking_to, 5, 1), the guard
			// goal saved as critter_set_goal does) - neither npc_attitude nor npc_goal changes, so
			// at the end the write-back puts the script's values over it. The shipped refusals
			// call a helper that sets npc_goal 5, npc_gtarg 1, npc_attitude 1 themselves; Zak's
			// (conversation 28) goes into a dialogue instead. Until 2026-10-15 ours set
			// npc_attitude to 0 and angered the critter at once (GH D18, N3).
			fReturnNpcGoods(false);

			if (mONpc != null)
				new UWCritterRecord(mONpc).SetNewGoal(DemandRefusedGoal, DemandRefusedTarget);

			return 0;
		}

		/// <summary>The verdict thresholds of do_judgement, best first: verdict n when the evaluation
		/// lies above entry n, the ninth below them all. Until 2026-10-14 an if-chain in fDoJudgement.
		/// </summary>
		private static readonly int[] miJudgementVerdictAbove = { 50, 35, 25, 10, -10, -25, -35, -50 };

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

			// The first threshold the evaluation lies above names the verdict; below them all it is
			// the last one.
			int liVerdict = miJudgementVerdictAbove.Length;

			for (int liAt = 0; liAt < miJudgementVerdictAbove.Length; liAt++)
			{
				if (liEvaluation > miJudgementVerdictAbove[liAt])
				{
					liVerdict = liAt;
					break;
				}
			}

			// The player's own words (play_say, BARTER.C do_judgement), in the player's colour - and
			// they count as his last line for the closing pause. Until 2026-10-15 ours printed them
			// as narration.
			mOPlayerSay(fGetString(JudgementBlock, JudgementCertaintyFirst + liCertainty)
				+ fGetString(JudgementBlock, JudgementJoinString)
				+ fGetString(JudgementBlock, JudgementVerdictFirst + liVerdict));
		}

		/// <summary>
		/// give_to_npc(count, array): the goods with the handles in the array go to the partner -
		/// with nothing in return, this is how you give something away.
		///
		/// conv_give_inv (CONVERSE.C, read 2026-10-15): EXACTLY the first count entries of the array,
		/// from its first word (a script's array counts from 1 through OFFSET, base + index - 1, so
		/// its first element lies on that word - the guard of conversation 15 writes its food
		/// handles there), each one a different marked good, or nothing is handed over. Until then
		/// ours read count + 1 entries and skipped those that named nothing: the workaround of
		/// 2026-09-17 (the guard took nothing, the array held a stale 242 at its first place), from
		/// before the base pointer was made relative to the stack (2026-09-29), which put the
		/// guard's locals where they belong.
		/// </summary>
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

			List<int> lOSlots = new List<int>();

			for (int liAt = 0; liAt < piCount; liAt++)
			{
				int liWanted = mOVm.ReadMemory(piArrayAddress + liAt);
				int liFound = -1;

				for (int liSlot = 0; liSlot < SlotCount && liFound < 0; liSlot++)
				{
					if (PlayerItems[liSlot] != null && PlayerSelected[liSlot]
						&& fHandle(liSlot) == liWanted && !lOSlots.Contains(liSlot))
						liFound = liSlot;
				}

				if (LogTrade)
					UWLog.Info(string.Format("[Trade] give_to_npc entry {0}: handle {1} -> slot {2}", liAt, liWanted, liFound));

				// All or nothing: one entry that names no marked good stops the whole gift.
				if (liFound < 0)
					return 0;

				lOSlots.Add(liFound);
			}

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
		/// number, or class from 1000) - npc_barter_give (BARTER.C, read 2026-10-12): nothing while
		/// the player holds something on the pointer (0); into the hand if he can carry it, else a
		/// free slot of the barter area (1); else down at the partner's feet, radius 5 (2); 3 when
		/// the culling test takes it there. Until 2026-10-12 a full hand fell through to the slots,
		/// and a drop went onto a random spot of the player's tile and answered 1.</summary>
		private int fTakeFromNpc(int piWanted)
		{
			if (fInventory != null && fInventory.CursorItem != null)
				return 0;

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

				Version++;

				return fDropAtNpc(lOItem) ? 2 : 3;
			}

			return 0;
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
		/// player's inventory (argument order see Call). Below 1000 the object number, above it the
		/// class. Returns the handle. Both sides look into containers (npc_barter_find, BARTER.C:
		/// Obj_InList with recurse - each item, then what it holds, then the next); until
		/// 2026-10-15 ours searched only the top of the partner's chain.
		/// </summary>
		private int fFindInv(int piWho, int piWanted)
		{
			IEnumerable<UWObject> lOItems = piWho == 0
				? fEnumerateWithContents(fGetNpcInventory())
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

		/// <summary>A chain depth-first, each item before what it holds (Obj_InList's recursion: an
		/// object without the quantity bit that links to others).</summary>
		private static IEnumerable<UWObject> fEnumerateWithContents(List<UWObject> pOChain, int piDepth = 0)
		{
			if (pOChain == null || piDepth > 8)
				yield break;

			foreach (UWObject lOItem in pOChain)
			{
				if (lOItem == null)
					continue;

				yield return lOItem;

				if (!lOItem.HasQuantity && lOItem.Contents != null)
				{
					foreach (UWObject lOInner in fEnumerateWithContents(lOItem.Contents, piDepth + 1))
						yield return lOInner;
				}
			}
		}

		/// <summary>
		/// do_inv_create(item): a new item in best condition in the partner's possessions. Returns
		/// its handle - or 0 when it joined a pile of the same item he already has: npc_inv_create
		/// (BARTER.C, read 2026-10-15) adds a counted item to the first like pile of his chain
		/// (UWStackRules.CanJoinBarterStack), frees it and hands back its index, which is 0 by then.
		/// The count is CreateObj's own (its second argument there is the mobile flag, not a count):
		/// a kind of stack class 0 or 2 starts as a pile of one. Until then ours never joined,
		/// always answered a handle and created every item without a count.
		/// </summary>
		private int fDoInvCreate(int piId)
		{
			List<UWObject> lOInventory = fGetNpcInventory();
			UWObject lOItem = fCreateItem(piId, UWObjectFactory.Unset, FullQuality);

			if (lOInventory == null || lOItem == null)
				return 0;

			if (fTryJoinNpcStack(lOInventory, lOItem))
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
		///
		/// give_ptr_npc (CONVERSE.C, read 2026-10-15): from the barter area only out of a MARKED slot
		/// (player_barter_items lists the marked ones); anything else goes the inventory's way
		/// (invRemoveObject), which answers 0 for a thing the player does not carry - an unmarked
		/// slot's thing among them - before any pile is split. Until then ours took any slot, and
		/// split a pile it then could not remove.
		/// </summary>
		private int fGivePtrNpc(int piHandle, int piQuantity)
		{
			UWObject lOItem = Resolve(piHandle);

			if (lOItem == null)
				return 0;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (PlayerItems[liSlot] != lOItem || !PlayerSelected[liSlot])
					continue;

				PlayerItems[liSlot] = null;
				PlayerSelected[liSlot] = false;
				fGiveItemToNpc(lOItem);
				Version++;

				return 1;
			}

			UWInventoryModel lOInventory = fInventory;

			if (lOInventory == null || lOInventory.FindCarried(pOCarried => pOCarried == lOItem) == null)
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

		/// <summary>take_id_from_npc(handle): the partner hands over this item -
		/// npc_barter_give_id, the same answers as take_from_npc (fTakeFromNpc).</summary>
		private int fTakeIdFromNpc(int piHandle)
		{
			if (fInventory != null && fInventory.CursorItem != null)
				return 0;

			UWObject lOItem = Resolve(piHandle);
			List<UWObject> lOInventory = fGetNpcInventory();

			// Only an item of his chain (npc_barter_give_id walks it): a good laid out in his slots is
			// out of it since 2026-10-15 and answers 0, as in the original.
			if (lOItem == null || lOInventory == null || !lOInventory.Remove(lOItem))
				return 0;

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

			Version++;

			return fDropAtNpc(lOItem) ? 2 : 3;
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
		/// identify_inv(handle, article, &amp;text, knowledge): describes an item - the text goes as a
		/// new string number into the third argument - and returns its value in the eyes of the
		/// partner.
		///
		/// READ 2026-10-01 (identify_inv_ovr100_16B6, per user: Shak "identified" a ring for a gold
		/// piece and it stayed unidentified): "magical" at knowledge 2 and the enchantment's name at
		/// 3 - with the KNOWLEDGE THE CONVERSATION PASSES, no Lore roll, and nothing is written into
		/// the item, so it is not identified afterwards. Shak passes 0.
		///
		/// READ AGAIN 2026-10-15 (conv_inv_name, CONVERSE.C): the text is UWItemDescriptions.
		/// IdentifyName - the article only when the second argument asks for it, no condition word;
		/// and the value is the partner's appraisal, his likes and his blur included (assess_value
		/// with npc_assess). Until then ours took the look sentence and the value without the blur,
		/// so Shak's repair price and the identifier's quotes differed by up to his blur.
		/// </summary>
		private int fIdentifyInv(IReadOnlyList<int> pOArguments, IReadOnlyList<int> pOAddresses)
		{
			UWObject lOItem = Resolve(UWConversationVM.Argument(pOArguments, 0));

			if (lOItem == null || mOVm == null)
				return 0;

			int liValue = fGetItemValue(lOItem, true, miAccuracy);

			if (pOAddresses != null && pOAddresses.Count >= 3 && mORegisterString != null)
			{
				string lsText = UWItemDescriptions.IdentifyName(lOItem, mOData,
					UWItemDescriptions.ItemObjectList(mOData, fLevel),
					UWConversationVM.Argument(pOArguments, 1) != 0, UWConversationVM.Argument(pOArguments, 3));

				mOVm.WriteMemory(pOAddresses[2], mORegisterString(lsText));
			}

			return liValue;
		}

		/// <summary>
		/// set_attitude(whoami, attitude): the FIRST critter of the level's active mobile list with
		/// this whoami takes the attitude (set_attitude through gronk_whoami(who, 0, ...), BABLHACK.C
		/// and SPELLS.C, read 2026-10-15: the walk stops after the first match). The list holds
		/// every mobile object in use, in the original's order (UWLevel.ActiveMobiles). Until then
		/// ours set every critter of the level with that whoami.
		/// </summary>
		private void fSetAttitudeOf(int piWhoAmI, int piAttitude)
		{
			UWNpc lONpc = UWTrapRules.FindActiveByWhoAmI(fLevel, piWhoAmI);

			if (lONpc != null)
				fApplyAttitude(lONpc, piAttitude);
		}

		/// <summary>
		/// set_race_attitude(race, attitude, range) (BABLHACK.C, read 2026-10-15): every critter of
		/// the partner's item type whose creature row names this race (CRIT.DAT byte 9,
		/// GeneralType) and that is no loner (byte 0x0A bit 7, UWCritterRecord.AttitudeLocked) takes
		/// the attitude - on the tiles from range before to range past the partner's tile (word
		/// 0x16), clipped to 1..63; range 0 is the tile alone. Until then ours ignored the race and
		/// the loner bit, measured from the loaded tile and made a range of 0 one.
		/// </summary>
		private void fSetRaceAttitude(int piRace, int piAttitude, int piRange)
		{
			UWLevel lOLevel = fLevel;

			if (mONpc == null || lOLevel == null || lOLevel.ActiveMobiles == null || lOLevel.Masterlist == null)
				return;

			UWCritterRecord lOTalker = new UWCritterRecord(mONpc);
			int liFirstX = Math.Max(1, lOTalker.TileX - piRange);
			int liFirstY = Math.Max(1, lOTalker.TileY - piRange);
			int liLastX = lOTalker.TileX + piRange >= UWWorldScale.TilesPerAxis ? UWWorldScale.TilesPerAxis - 1 : lOTalker.TileX + piRange;
			int liLastY = lOTalker.TileY + piRange >= UWWorldScale.TilesPerAxis ? UWWorldScale.TilesPerAxis - 1 : lOTalker.TileY + piRange;

			foreach (int liIndex in lOLevel.ActiveMobiles)
			{
				UWNpc lONpc = liIndex > 0 && liIndex < lOLevel.Masterlist.Count ? lOLevel.Masterlist[liIndex] as UWNpc : null;

				if (lONpc == null || lONpc.ID != mONpc.ID || fRaceOf(lONpc) != piRace)
					continue;

				UWCritterRecord lORecord = new UWCritterRecord(lONpc);

				if (lORecord.AttitudeLocked || lORecord.TileX < liFirstX || lORecord.TileX > liLastX
					|| lORecord.TileY < liFirstY || lORecord.TileY > liLastY)
					continue;

				lORecord.Attitude = piAttitude & UWNpcRecordCodec.AttitudeMask;
			}
		}

		/// <summary>The race of a critter's kind: Creature[].race, CRIT.DAT byte 9 (GeneralType); -1
		/// without a row.</summary>
		private int fRaceOf(UWNpc pONpc)
		{
			UWObjectClassProperties.Critter lOStats;

			return mOData != null && mOData.ObjectClassProperties != null
				&& mOData.ObjectClassProperties.TryGetCritter(pONpc.ID, out lOStats) ? lOStats.GeneralType : -1;
		}

		/// <summary>
		/// SET_ATTITUDE on the object alone (set_mob_att, BABLHACK.C): through the record, so the
		/// living critter sees it at once. The partner's script variable npc_attitude is not
		/// touched - at the end update_converse_data writes the variable over the object, as in the
		/// original. Until 2026-10-15 ours wrote the variable as well when the partner was meant
		/// and made a critter set to 0 attack (AngerNpc) - inert where the script then sets
		/// npc_attitude itself, as the shipped ones do (N4).
		/// </summary>
		private static void fApplyAttitude(UWNpc pONpc, int piAttitude)
		{
			new UWCritterRecord(pONpc).Attitude = piAttitude & UWNpcRecordCodec.AttitudeMask;
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
				int liLike = fDoesNpcLike(pOItem);

				if (liLike < 0)
					return 0;

				if (liLike > 0)
					liValue = (short)(liValue * 3) >> 1;
			}

			// The products in 16 bits, as ovr095_16B1 multiplies - with the raw count, so a field of
			// 0 makes the value 0 (UWStackRules.Count). UNSIGNED, as the original plays: per user on
			// the original 2026-10-17, Shak (who likes coins: 4 each) takes 250 coins for his plate
			// gauntlets every time; 1000 * quality 40 = 40000 wraps to a negative signed word, and
			// ours, taking it signed, valued the whole pile at 1 and refused. 40000 / 64 = 625.
			if (UWStackRules.IsStackable(pOItem))
				liValue = (ushort)(liValue * pOItem.Quantity);

			int liQuality = pOItem.Quality & UWObjectMechanics.MaxQuality;

			if (liValue > 0)
				liValue = liQuality > 0 ? Math.Max(1, (ushort)(liValue * liQuality) >> 6) : 0;

			return new UWOriginalRandom(fObjectNumber(pOItem)).Noise(liValue, -piAccuracy, piAccuracy);
		}

		/// <summary>does_npc_like (BARTER.C): -1 for an item COMOBJ.DAT values at 0, else the
		/// partner's opinion (fLikes). The one test for what he refuses - in his valuation
		/// (fGetItemValue) and in what an accepted offer hands him (Swap).</summary>
		private int fDoesNpcLike(UWObject pOItem)
		{
			return fGetBaseValue(pOItem.ID) == 0 ? -1 : fLikes(pOItem.ID);
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
			return UWTextLayout.CleanText(UWText.Block(mOData, piBlock, piIndex));
		}

		// ------------------------------------------------------------------
		// Partner's possessions
		// ------------------------------------------------------------------

		/// <summary>The creature's object chain, extended by the rolled loot the first time
		/// (GenerateInventory - the conversation's start rolls it; this stays the guard the
		/// original's barter routines keep too, BARTER.C:127-128, 920-921, 948-949).</summary>
		private List<UWObject> fGetNpcInventory()
		{
			UWLevel lOLevel = fLevel;

			if (mONpc == null || lOLevel == null)
				return null;

			GenerateInventory();

			return mONpc.Contents;
		}

		/// <summary>
		/// generate_inventory (UnderworldExhumed obj/TREASURE.C:202-216): the partner's loot, rolled
		/// once - NPCLootSpawned is the original's OBJ_HAS_INV bit - and laid at the head of his chain.
		/// THE CONVERSATION'S START ROLLS IT (2026-10-19, the user's decision "as the original"):
		/// Converse calls it before the script runs, for a partner not rolled yet (conv/CONVERSE.C:
		/// 329-330), so UWConversationSession calls this right after the trade is set up. Until then
		/// ours rolled it on the first trade lookup of the script (fGetNpcInventory), between the
		/// script's own draws, and not at all in a conversation without a trade function. The roll,
		/// the creation and the slots are the same.
		/// </summary>
		public void GenerateInventory()
		{
			UWLevel lOLevel = fLevel;

			if (mONpc == null || lOLevel == null)
				return;

			mONpc.EnsureContentsLoaded(lOLevel.Masterlist);

			if (mONpc.Contents == null)
				return;

			if (!mONpc.NPCLootSpawned)
			{
				mONpc.NPCLootSpawned = true;

				if (mbHasStats && mOData != null)
				{
					List<UWCritterLoot.Drop> lODrops = UWCritterLoot.Generate(mOStats,
						mOData.CommonObjectProperties, lOLevel.LevelNumber, mOData.ObjectProperties);
					List<UWObject> lOLoot = new List<UWObject>();

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

						lOLoot.Add(lOItem);
					}

					// AT THE HEAD OF THE CHAIN, piece by piece (cleanup step 4.12c, 2026-10-19, the
					// user's decision "as the original"): generate_inventory Obj_Adds each rolled piece
					// (UnderworldExhumed obj/TREASURE.C:202-216, OBJECTS.C:267), so the last piece
					// rolled leads and what the partner carried follows - in the trade window
					// (setup_to_barter walks the chain from its head) and in a later death's spill. The
					// same insertion as the death's roll (UWCritterLoot.AddToChain). Until then the
					// trade appended the loot at the tail in the rolled order. The roll, the creation
					// and the slots are unchanged.
					UWCritterLoot.AddToChain(mONpc.Contents, lOLoot);
				}
			}
		}

		/// <summary>A new item for the NPC's inventory, without a place in the world: CreateObj
		/// (UWObjectFactory) with the caller's count and quality - npc_inv_create gives 0x3F
		/// (BARTER.C), the loot its own roll.</summary>
		private UWObject fCreateItem(int piId, int piQuantity, int piQuality)
		{
			UWObject lOItem = UWObjectFactory.Create(piId, mOData, piQuantity, piQuality);

			if (lOItem == null)
				return null;

			lOItem.WasSpawned = true;

			return lOItem;
		}

		/// <summary>The original's gold coin (ITEM_GOLD_COIN, 0xA1) - the one kind npc_inv_add puts
		/// onto a pile the partner already has.</summary>
		private const int GoldCoinId = 0xA1;

		/// <summary>
		/// npc_inv_add (BARTER.C): an item goes to the partner, at the head of his chain - gold coins
		/// onto the first coin pile of his chain they join (UWStackRules.CanJoinBarterStack), the
		/// paid stack freed. The same for the goods of an accepted offer (probablyTradeObjects),
		/// give_to_npc and give_ptr_npc. Until 2026-10-15 every item went to the head, so the next
		/// trade showed the coins just paid as a pile of their own.
		/// </summary>
		private void fGiveItemToNpc(UWObject pOItem)
		{
			List<UWObject> lOInventory = fGetNpcInventory();

			if (lOInventory == null)
			{
				fDropAtPlayer(pOItem);
				return;
			}

			if (pOItem.ID == GoldCoinId && fTryJoinNpcStack(lOInventory, pOItem))
				return;

			pOItem.Link = 0;
			lOInventory.Insert(0, pOItem);
		}

		/// <summary>The first pile of the partner's chain (its top level) that pOItem may join: its
		/// count goes there and it is freed. False when there is none.</summary>
		private static bool fTryJoinNpcStack(List<UWObject> pOInventory, UWObject pOItem)
		{
			foreach (UWObject lOOther in pOInventory)
			{
				if (lOOther == null || !UWStackRules.CanJoinBarterStack(lOOther, pOItem))
					continue;

				lOOther.Quantity = (ushort)(lOOther.Quantity + pOItem.Quantity);
				UWCarriedSlots.OnMerged(pOItem);

				return true;
			}

			return false;
		}

		/// <summary>
		/// ReturnTradeObjectsToNPC (BARTER.C): the goods on the partner's side go back to the head of
		/// his chain, slot by slot - all of them (do_decline, a refused demand), or with
		/// pbOnlyUnmarked those he did not sell (an accepted offer or demand). A bought good the
		/// player left there goes back too. Since 2026-10-15 the goods really leave his possessions
		/// while they lie in the slots (setup_to_barter); until then this only emptied the view.
		/// </summary>
		private void fReturnNpcGoods(bool pbOnlyUnmarked)
		{
			List<UWObject> lOInventory = fGetNpcInventory();

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				UWObject lOItem = NpcItems[liSlot];

				if (lOItem == null || (pbOnlyUnmarked && NpcSelected[liSlot]))
					continue;

				if (lOInventory != null && !lOInventory.Contains(lOItem))
					lOInventory.Insert(0, lOItem);

				NpcItems[liSlot] = null;
				NpcSelected[liSlot] = false;
				NpcBought[liSlot] = false;
			}

			Version++;
		}

		/// <summary>Carries out an accepted deal (do_offer: ReturnTradeObjectsToNPC(1), then
		/// probablyTradeObjects): the partner's unmarked goods go back to him first, his marked ones
		/// stay on his side as the player's (NpcBought); then the player's marked goods go to him.
		/// After a demand the player's own marked goods stay on his side.</summary>
		public void Swap()
		{
			bool lbPlayerKeeps = mbPlayerKeepsHisGoods;

			PendingSwap = false;
			mbPlayerKeepsHisGoods = false;

			fReturnNpcGoods(true);

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				if (NpcItems[liSlot] != null)
					NpcBought[liSlot] = true;
			}

			// probablyTradeObjects (BARTER.C) hands over only what he does not refuse: a good he
			// dislikes or COMOBJ.DAT values at 0 stays marked in the player's slot - and drops at the
			// player's feet when the conversation ends. Until 2026-10-15 ours handed over every
			// marked good.
			for (int liSlot = 0; liSlot < SlotCount && !lbPlayerKeeps; liSlot++)
			{
				if (PlayerItems[liSlot] == null || !PlayerSelected[liSlot] || fDoesNpcLike(PlayerItems[liSlot]) < 0)
					continue;

				fGiveItemToNpc(PlayerItems[liSlot]);
				PlayerItems[liSlot] = null;
				PlayerSelected[liSlot] = false;
			}

			NpcGoodsBought = true;
			Version++;
		}

		/// <summary>
		/// end_barter (BARTER.C), when the conversation ends: slot by slot, what the player still has
		/// laid out goes down around him and what lies on the partner's side around the partner,
		/// both radius 5 - NOT back into the backpack (per user on the original, 2026-09-13 and
		/// 2026-09-29: the item lies on the floor). The partner's side is everything still there,
		/// bought or not: since 2026-10-15 those goods are out of his possessions (setup_to_barter),
		/// and the shipped conversations decline before they end, so only a good he sold and the
		/// player left behind is usually left. Until then ours gave unsold goods back to him.
		/// </summary>
		public void EndConversation()
		{
			if (PendingSwap)
				Swap();

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				UWObject lOItem = PlayerItems[liSlot];

				PlayerItems[liSlot] = null;
				PlayerSelected[liSlot] = false;

				if (lOItem != null && fWorld != null)
					fWorld.DropAroundPlayer(lOItem);

				lOItem = NpcItems[liSlot];

				NpcItems[liSlot] = null;
				NpcSelected[liSlot] = false;
				NpcBought[liSlot] = false;

				if (lOItem != null && fWorld != null)
					fWorld.DropAtCreature(lOItem, mONpc);
			}

			Version++;
		}

		private void fDropAtPlayer(UWObject pOItem)
		{
			if (fWorld == null || pOItem == null)
				return;

			fWorld.DropAtPlayer(pOItem);
		}

		/// <summary>near_mob_put_at(talking_to, item, 5, cull); false when the culling test took it.</summary>
		private bool fDropAtNpc(UWObject pOItem)
		{
			return fWorld != null && pOItem != null && fWorld.DropAtCreature(pOItem, mONpc);
		}

		/// <summary>change_critter_goal(talking_to, GOAL_ATTACK, 1) of a refused demand: attack the
		/// player.</summary>
		private const int DemandRefusedGoal = UWNpc.GoalAttack;

		private const int DemandRefusedTarget = 1;

		/// <summary>A demand given in lowers the partner's attitude in the script's variable alone
		/// (bab_var("npc_attitude"), do_demand); the write-back carries it onto the object at the
		/// end. Until 2026-10-15 ours set the object too.</summary>
		private void fSetAttitude(int piAttitude)
		{
			if (mOVm != null)
				mOVm.SetImportedGlobal("npc_attitude", piAttitude);
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

		/// <summary>Major class 2 minor 0 - the containers, which a trade slot neither fills nor swaps
		/// (CombineToSlot_ovr097_D30: MAJOR_MISC, MINOR_CONTAINER).</summary>
		private const int ContainerMajorClass = 2;

		private const int ContainerMinorClass = 0;

		/// <summary>
		/// A DROP ON ONE OF THE PLAYER'S SLOTS (UseTradeSlot_ovr097_6E8 with ovr097_CDB and
		/// CombineToSlot_ovr097_D30, BARTER.C, read 2026-10-15): an empty slot takes the held thing;
		/// a slot holding a container takes nothing and gives nothing back; a pile of the same item
		/// takes the held pile's count and keeps its own quality (UWStackRules.CanJoinBarterStack),
		/// the held pile freed; anything else is swapped. The slot is marked afterwards in every
		/// case, the refused one too. Returns what is on the pointer afterwards: the held thing
		/// itself when a container refused it, the slot's old thing after a swap, else null.
		///
		/// Until 2026-10-15 the classic and the modern drop each stacked through the inventory's
		/// AddTogether (the qualities averaged, a single piece turned into a counted stack) and
		/// swapped a container like anything else.
		/// </summary>
		public UWObject DropOnPlayerSlot(int piSlot, UWObject pOHeld)
		{
			if (piSlot < 0 || piSlot >= SlotCount || pOHeld == null)
				return pOHeld;

			UWObject lOThere = PlayerItems[piSlot];
			UWObject lOBack = null;

			if (lOThere == null)
				PlayerItems[piSlot] = pOHeld;
			else if (UWObjectIds.Major(lOThere.ID) == ContainerMajorClass && UWObjectIds.Minor(lOThere.ID) == ContainerMinorClass)
				lOBack = pOHeld;
			else if (UWStackRules.CanJoinBarterStack(lOThere, pOHeld))
			{
				lOThere.Quantity = (ushort)(lOThere.Quantity + pOHeld.Quantity);
				UWCarriedSlots.OnMerged(pOHeld);
			}
			else
			{
				PlayerItems[piSlot] = pOHeld;
				lOBack = lOThere;
			}

			PlayerSelected[piSlot] = true;
			Version++;

			return lOBack;
		}

		/// <summary>Part of the pile in one of the player's slots was taken: the rest stays there,
		/// unmarked, as UseTradeSlot_ovr097_6E8 clears the mark after any pick-up.</summary>
		public void PlayerSlotPartTaken(int piSlot)
		{
			if (piSlot < 0 || piSlot >= SlotCount)
				return;

			PlayerSelected[piSlot] = false;
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
