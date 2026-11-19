using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S TEACHERS, read from the conversations themselves (2026-11-18, per user: the help's Mantras tab
	/// means nothing in the second game, which has no shrines - its skills are trained by people). A conversation
	/// teaches a skill where it calls x_skills(skill, value) with a value above 10000: the original then spends one of
	/// the player's skill points to advance that skill (uw2 conv/BABLHACK.C x_skills; 10000 only asks, 0 to 30 sets).
	/// Both arguments are constants the script stores into its memory just before the call - PUSHI c, PUSHI_EFF m,
	/// SWAP, STO - and then pushes by address (PUSHI_EFF m) with the argument count; this scan follows exactly that.
	/// Whatever a teacher wants first (a quest done, a mood) lies in each script's own branches and is not read: the
	/// list says who CAN teach what. Shipped: 16 conversations (self-check).
	/// </summary>
	public static class UWTrainersUw2
	{
		/// <summary>x_skills advances the skill for a skill point with a value above this.</summary>
		public const int AdvanceAbove = 10000;

		/// <summary>The import's name in the conversations.</summary>
		public const string SkillsFunction = "x_skills";

		private static readonly Dictionary<UWConversations, Dictionary<int, List<int>>> msCache =
			new Dictionary<UWConversations, Dictionary<int, List<int>>>();

		/// <summary>Every teaching conversation's slot with the skills it advances (in the order its script has them),
		/// read once per conversation archive.</summary>
		public static Dictionary<int, List<int>> All(UWConversations pOConversations)
		{
			if (pOConversations == null)
				return new Dictionary<int, List<int>>();

			lock (msCache)
			{
				if (msCache.TryGetValue(pOConversations, out Dictionary<int, List<int>> lOKnown))
					return lOKnown;

				Dictionary<int, List<int>> lOAll = new Dictionary<int, List<int>>();

				for (int liSlot = 0; liSlot < pOConversations.SlotCount; liSlot++)
				{
					UWConversations.Conversation lOConversation = null;

					try
					{
						lOConversation = pOConversations.GetConversation(liSlot);
					}
					catch (System.Exception)
					{
						lOConversation = null;
					}

					List<int> lOSkills = SkillsTaught(lOConversation);

					if (lOSkills.Count > 0)
						lOAll[liSlot] = lOSkills;
				}

				msCache[pOConversations] = lOAll;

				return lOAll;
			}
		}

		/// <summary>
		/// The name a teacher's conversation shows, as UWConversationSession takes it: the personal name of block 7, or -
		/// as the second game's get_name falls through - the object name of a creature that holds the conversation in the
		/// levels (slot 144's goblin has no personal name; per user 2026-11-19 the help showed "Someone"). Empty when
		/// neither is found.
		/// </summary>
		public static string NameOf(DataImport pOData, int piSlot)
		{
			string lsName = UWText.Block(pOData, UWConversations.PartnerNameStringBlock, piSlot + UWConversations.PartnerNameOffset, string.Empty).Trim();

			if (lsName.Length > 0 || pOData == null || pOData.Levels == null)
				return lsName;

			lock (msObjectOfSlot)
			{
				if (!msObjectOfSlot.TryGetValue(pOData, out Dictionary<int, int> lOOfSlot))
				{
					lOOfSlot = new Dictionary<int, int>();

					foreach (UWLevel lOLevel in pOData.Levels)
					{
						if (lOLevel == null || lOLevel.Masterlist == null)
							continue;

						for (int liAt = 1; liAt < lOLevel.Masterlist.Count && liAt < MobileSlots; liAt++)
							if (lOLevel.Masterlist[liAt] is UWNpc lONpc && lONpc.ID >= FirstCreature && lONpc.ID < CreatureEnd
								&& !lOOfSlot.ContainsKey(lONpc.NPCwhoami))
								lOOfSlot[lONpc.NPCwhoami] = lONpc.ID;
					}

					msObjectOfSlot[pOData] = lOOfSlot;
				}

				return lOOfSlot.TryGetValue(piSlot, out int liObject) ? UWText.Capitalised(UWText.ObjectName(pOData, liObject)) : string.Empty;
			}
		}

		/// <summary>The mobile objects of a level and the creatures' ids (0x40 to 0x7F).</summary>
		private const int MobileSlots = 256;

		private const int FirstCreature = 0x40;

		private const int CreatureEnd = 0x80;

		private static readonly Dictionary<DataImport, Dictionary<int, int>> msObjectOfSlot = new Dictionary<DataImport, Dictionary<int, int>>();

		/// <summary>Whether the conversation in piSlot teaches anything.</summary>
		public static bool Teaches(UWConversations pOConversations, int piSlot)
		{
			return All(pOConversations).ContainsKey(piSlot);
		}

		/// <summary>The skills one conversation advances for a skill point, each once.</summary>
		public static List<int> SkillsTaught(UWConversations.Conversation pOConversation)
		{
			List<int> lOSkills = new List<int>();

			if (pOConversation == null || pOConversation.Code == null)
				return lOSkills;

			int liFunction = -1;

			foreach (UWConversations.Import lOImport in pOConversation.Imports)
				if (lOImport.IsFunction && lOImport.Name == SkillsFunction)
					liFunction = lOImport.IdOrAddress;

			if (liFunction < 0)
				return lOSkills;

			ushort[] lyCode = pOConversation.Code;
			Dictionary<int, int> lOStored = new Dictionary<int, int>();
			List<int> lOOpcodes = new List<int>();
			List<int> lOOperands = new List<int>();

			for (int liAt = 0; liAt < lyCode.Length; liAt++)
			{
				int liOp = lyCode[liAt];
				bool lbOperand = UWConversationDisassembler.HasImmediateOperand((UWConversationDisassembler.Opcode)liOp);
				int liOperand = lbOperand && liAt + 1 < lyCode.Length ? lyCode[liAt + 1] : 0;

				lOOpcodes.Add(liOp);
				lOOperands.Add(liOperand);

				if (lbOperand)
					liAt++;

				int liN = lOOpcodes.Count;

				// PUSHI c, PUSHI_EFF m, SWAP, STO: memory m holds c.
				if (liOp == (int)UWConversationDisassembler.Opcode.STO && liN >= 4
					&& lOOpcodes[liN - 2] == (int)UWConversationDisassembler.Opcode.SWAP
					&& lOOpcodes[liN - 3] == (int)UWConversationDisassembler.Opcode.PUSHI_EFF
					&& lOOpcodes[liN - 4] == (int)UWConversationDisassembler.Opcode.PUSHI)
					lOStored[lOOperands[liN - 3]] = (short)lOOperands[liN - 4];

				if (liOp != (int)UWConversationDisassembler.Opcode.CALLI || liOperand != liFunction)
					continue;

				// Back from the call: the argument count, then the two addresses pushed last (value, then skill) -
				// a PUSHI_EFF that a SWAP follows is a store's, not an argument.
				List<int> lOAddresses = new List<int>();

				for (int liBack = liN - 3; liBack >= 0 && lOAddresses.Count < 2; liBack--)
				{
					if (lOOpcodes[liBack] != (int)UWConversationDisassembler.Opcode.PUSHI_EFF)
						continue;

					if (liBack + 1 < liN && lOOpcodes[liBack + 1] == (int)UWConversationDisassembler.Opcode.SWAP)
						continue;

					lOAddresses.Add(lOOperands[liBack]);
				}

				if (lOAddresses.Count == 2 && lOStored.TryGetValue(lOAddresses[0], out int liValue) && liValue > AdvanceAbove
					&& lOStored.TryGetValue(lOAddresses[1], out int liSkill) && liSkill >= 0 && !lOSkills.Contains(liSkill))
					lOSkills.Add(liSkill);
			}

			return lOSkills;
		}
	}
}
