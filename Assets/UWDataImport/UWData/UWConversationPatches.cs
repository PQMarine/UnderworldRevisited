namespace UWDataImport.UWData
{
	/// <summary>
	/// FIXES OF BUGS IN THE ORIGINAL'S CONVERSATION SCRIPTS - a deliberate deviation (Todo.md
	/// section 8), each one decided by the user. The VM runs a patched copy of the code
	/// (UWConversationVM); the loaded conversation, and with it every export and tool, keeps the
	/// original words. A patch only applies where the word it replaces is what is expected, so a
	/// different cnv.ark is left alone. A DEVIATION SWITCH (deviations.conversationPatches,
	/// PatchesApplied): off, the VM runs the loaded words.
	/// </summary>
	public static class UWConversationPatches
	{
		/// <summary>Our fixes are applied by default (the rule set's deviations.conversationPatches).</summary>
		public const bool PatchesApplied = true;

		private struct Patch
		{
			public int Slot;

			/// <summary>The operand word; the instruction's opcode sits in the word before it.</summary>
			public int Address;

			public UWConversationDisassembler.Opcode Opcode;

			public ushort Expected;

			public ushort Replacement;

			/// <summary>Code appended to the program: the instruction at Address - 1 becomes a JMP
			/// to it (Replacement is ignored then). The block must end with a jump of its own.</summary>
			public ushort[] Appended;
		}

		private const ushort Jmp = (ushort)UWConversationDisassembler.Opcode.JMP;

		private const ushort PushiEff = (ushort)UWConversationDisassembler.Opcode.PUSHI_EFF;

		private const ushort Pushi = (ushort)UWConversationDisassembler.Opcode.PUSHI;

		private const ushort Fetchm = (ushort)UWConversationDisassembler.Opcode.FETCHM;

		private const ushort Opadd = (ushort)UWConversationDisassembler.Opcode.OPADD;

		private const ushort Sto = (ushort)UWConversationDisassembler.Opcode.STO;

		private const ushort Offset = (ushort)UWConversationDisassembler.Opcode.OFFSET;

		private const ushort Tsteq = (ushort)UWConversationDisassembler.Opcode.TSTEQ;

		private const ushort Beq = (ushort)UWConversationDisassembler.Opcode.BEQ;

		private static readonly Patch[] maPatches =
		{
			// THE GENERIC TROLLS' NEWS (conversation 288, routine at code 857; per user,
			// 2026-10-01: "do not rebuild the original, fix the script"). The routine collects the
			// untold news into a local array with a counter that starts at ONE and so ends one above
			// their number - two bugs follow: the test for "none left" compares it with zero, so "I
			// have no new news for you." and the reset of the six told flags never came, and
			// random(counter) picks one entry too many, an array slot never written in that call
			// (the original then repeats a stale answer or breaks its own conversation, ours said
			// nothing; per user also right after the reset). The counter now starts at ZERO (869)
			// and the array is written one word further on (895), so the entries land where they
			// are read: the count is right for both the test and the random pick. THIRD, the loop
			// that clears the six flags after "no new news" (938-956) never counts its index on -
			// an endless loop the original never reached (ours aborted it after a million steps
			// with only the first flag cleared, so "no news" and the first news alternated, per
			// user): its BRA back at 955 goes to an appended local8 += 1, then on to 938.
			new Patch { Slot = 288, Address = 870, Opcode = UWConversationDisassembler.Opcode.PUSHI, Expected = 1, Replacement = 0 },
			new Patch { Slot = 288, Address = 896, Opcode = UWConversationDisassembler.Opcode.PUSHI_EFF, Expected = 1, Replacement = 2 },
			new Patch
			{
				Slot = 288, Address = 956, Opcode = UWConversationDisassembler.Opcode.BRA, Expected = 65518,
				Appended = new ushort[] { PushiEff, 8, PushiEff, 8, Fetchm, Pushi, 1, Opadd, Sto, Jmp, 938 }
			},

			// GOLDTHIRST'S FIRST GIFT (conversation 3, routine at code 1356; reported on the RPG Codex, bug plan C,
			// 2026-11-03): the first-gift check sums the values of what lies in the trade area and takes all of it,
			// but never looks for the large gold nugget (175) the way the later-gift check at 3104 does (3156-3166),
			// so a nugget given first went for "This is indeed a goodly gift" and the jeweled axe (3460,
			// take_from_npc(11)) was lost, on every first-gift route. The loop body's first instruction (1408) now
			// jumps to an appended test local1[local13] == 175; on a nugget it goes on in 3104's own nugget code at
			// 3168 (that item alone, "Zounds!", the axe, attitude 3, end: 3460 -> 177 -> 18, EXIT_OP, so the other
			// routine's frame does not matter; its locals 16 and 18 lie inside 1356's 39), else it runs the replaced
			// PUSHI_EFF 12 and goes back to 1410. The BEQ's offset counts from its operand word, as the original's.
			new Patch
			{
				Slot = 3, Address = 1409, Opcode = UWConversationDisassembler.Opcode.PUSHI_EFF, Expected = 12,
				Appended = new ushort[] { PushiEff, 13, Fetchm, PushiEff, 1, Offset, Fetchm, Pushi, 175, Tsteq, Beq, 3, Jmp, 3168, PushiEff, 12, Jmp, 1410 }
			},

			// GOLDTHIRST'S PASSWORD FOR NOTHING (conversation 3; reported on the RPG Codex, bug plan C): "I bring thee
			// this gift." after "It would be difficult to flatter..." calls 1580 (CALL at 1053), his thanks for a gift
			// already checked - the trade area is never looked at, and "Deco Morono" follows for nothing. Every other
			// gift answer calls the check at 1356 (1213, 1313); this one does now.
			new Patch { Slot = 3, Address = 1054, Opcode = UWConversationDisassembler.Opcode.CALL, Expected = 1580, Replacement = 1356 },
		};

		/// <summary>The code the VM runs for this conversation: a patched copy, or the loaded
		/// words themselves when no patch applies.</summary>
		public static ushort[] GetCode(UWConversations.Conversation pOConversation)
		{
			if (pOConversation == null || pOConversation.Code == null)
				return null;

			ushort[] lyCode = pOConversation.Code;

			foreach (Patch lOPatch in maPatches)
			{
				if (lOPatch.Slot != pOConversation.Slot || lOPatch.Address < 1 || lOPatch.Address >= lyCode.Length
					|| lyCode[lOPatch.Address - 1] != (ushort)lOPatch.Opcode || lyCode[lOPatch.Address] != lOPatch.Expected)
					continue;

				if (lyCode == pOConversation.Code)
					lyCode = (ushort[])pOConversation.Code.Clone();

				if (lOPatch.Appended == null)
				{
					lyCode[lOPatch.Address] = lOPatch.Replacement;
					continue;
				}

				int liAt = lyCode.Length;

				System.Array.Resize(ref lyCode, liAt + lOPatch.Appended.Length);
				System.Array.Copy(lOPatch.Appended, 0, lyCode, liAt, lOPatch.Appended.Length);

				lyCode[lOPatch.Address - 1] = Jmp;
				lyCode[lOPatch.Address] = (ushort)liAt;
			}

			return lyCode;
		}
	}
}
