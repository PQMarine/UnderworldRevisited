namespace UWDataImport.UWData
{
	/// <summary>
	/// FIXES OF BUGS IN THE ORIGINAL'S CONVERSATION SCRIPTS - a deliberate deviation (Todo.md
	/// section 8), each one decided by the user. The VM runs a patched copy of the code
	/// (UWConversationVM); the loaded conversation, and with it every export and tool, keeps the
	/// original words. A patch only applies where the word it replaces is what is expected, so a
	/// different cnv.ark is left alone.
	/// </summary>
	public static class UWConversationPatches
	{
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
