using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Disassembles the bytecode of a conversation into readable text (uw-formats.txt 7.4).
	///
	/// The purpose is not beauty but traceability: as long as the machine itself
	/// does not run, this is the only way to see what a conversation actually
	/// does - which game variables it reads, which built-in functions it calls and at
	/// which point it outputs text.
	/// </summary>
	public static class UWConversationDisassembler
	{
		public enum Opcode
		{
			NOP = 0x00,
			OPADD = 0x01,
			OPMUL = 0x02,
			OPSUB = 0x03,
			OPDIV = 0x04,
			OPMOD = 0x05,
			OPOR = 0x06,
			OPAND = 0x07,
			OPNOT = 0x08,
			TSTGT = 0x09,
			TSTGE = 0x0A,
			TSTLT = 0x0B,
			TSTLE = 0x0C,
			TSTEQ = 0x0D,
			TSTNE = 0x0E,
			JMP = 0x0F,
			BEQ = 0x10,
			BNE = 0x11,
			BRA = 0x12,
			CALL = 0x13,
			CALLI = 0x14,
			RET = 0x15,
			PUSHI = 0x16,
			PUSHI_EFF = 0x17,
			POP = 0x18,
			SWAP = 0x19,
			PUSHBP = 0x1A,
			POPBP = 0x1B,
			SPTOBP = 0x1C,
			BPTOSP = 0x1D,
			ADDSP = 0x1E,
			FETCHM = 0x1F,
			STO = 0x20,
			OFFSET = 0x21,
			START = 0x22,
			SAVE_REG = 0x23,
			PUSH_REG = 0x24,
			STRCMP = 0x25,
			EXIT_OP = 0x26,
			SAY_OP = 0x27,
			RESPOND_OP = 0x28,
			OPNEG = 0x29
		}

		/// <summary>Instructions with an immediate operand in the next word.</summary>
		public static bool HasImmediateOperand(Opcode peOpcode)
		{
			switch (peOpcode)
			{
				case Opcode.JMP:
				case Opcode.BEQ:
				case Opcode.BNE:
				case Opcode.BRA:
				case Opcode.CALL:
				case Opcode.CALLI:
				case Opcode.PUSHI:
				case Opcode.PUSHI_EFF:
					return true;

				default:
					return false;
			}
		}

		/// <summary>
		/// Disassembles the whole program. pOStrings may be null; if it is set, the matching
		/// conversation text is appended as a comment for PUSHI - that is what makes the
		/// output truly readable, because text output then appears right where it happens.
		/// </summary>
		public static string Disassemble(UWConversations.Conversation pOConversation, UWStrings pOStrings)
		{
			StringBuilder lOOut = new StringBuilder();

			lOOut.AppendLine(string.Format("; Conversation {0}, string block 0x{1:X4}, {2} memory slots, {3} imports, {4} words of code",
				pOConversation.Slot, pOConversation.StringBlock, pOConversation.MemorySlots,
				pOConversation.Imports.Count, pOConversation.Code.Length));

			for (int liIndex = 0; liIndex < pOConversation.Imports.Count; liIndex++)
			{
				UWConversations.Import lOImport = pOConversation.Imports[liIndex];
				lOOut.AppendLine(string.Format(";   import {0,-24} {1,-4} {2}",
					lOImport.Name, lOImport.IdOrAddress, lOImport.IsFunction ? "function" : "variable"));
			}

			lOOut.AppendLine();

			for (int liPosition = 0; liPosition < pOConversation.Code.Length; liPosition++)
			{
				ushort liWord = pOConversation.Code[liPosition];
				Opcode leOpcode = (Opcode)liWord;
				string lsName = liWord <= (int)Opcode.OPNEG ? leOpcode.ToString() : string.Format("?0x{0:X4}", liWord);

				if (liWord <= (int)Opcode.OPNEG && HasImmediateOperand(leOpcode) && liPosition + 1 < pOConversation.Code.Length)
				{
					ushort liOperand = pOConversation.Code[liPosition + 1];
					string lsComment = fDescribeOperand(pOConversation, leOpcode, liOperand, pOStrings);

					lOOut.AppendLine(string.Format("{0,5}: {1,-10} {2,-6}{3}", liPosition, lsName, liOperand, lsComment));
					liPosition++;
				}
				else
				{
					lOOut.AppendLine(string.Format("{0,5}: {1}", liPosition, lsName));
				}
			}

			return lOOut.ToString();
		}

		private static string fDescribeOperand(UWConversations.Conversation pOConversation, Opcode peOpcode,
			ushort piOperand, UWStrings pOStrings)
		{
			if (peOpcode == Opcode.CALLI)
			{
				foreach (UWConversations.Import lOImport in pOConversation.Imports)
				{
					if (lOImport.IsFunction && lOImport.IdOrAddress == piOperand)
						return "   ; " + lOImport.Name;
				}

				return "   ; unknown built-in function";
			}

			if (peOpcode == Opcode.PUSHI && pOStrings != null)
			{
				string lsText = fTryGetString(pOStrings, pOConversation.StringBlock, piOperand);

				if (!string.IsNullOrEmpty(lsText))
				{
					// With a question mark, because PUSHI has both roles: string number AND a quite
					// ordinary number (variable offset, comparison value, loop counter).
					// Statically this cannot be told apart - only whoever uses the value
					// decides it. The annotation is therefore a hint, not a
					// statement.
					return "   ; ?\"" + lsText.Replace("\n", " ").Replace("\r", string.Empty) + "\"";
				}
			}

			return string.Empty;
		}

		private static string fTryGetString(UWStrings pOStrings, int piBlock, int piIndex)
		{
			try
			{
				if (!pOStrings.Blocks.ContainsKey(piBlock))
					return null;

				var lOBlock = pOStrings.Blocks[piBlock];

				if (piIndex < 0 || piIndex >= lOBlock.Strings.Count)
					return null;

				string lsResult = lOBlock.Strings[piIndex];

				return string.IsNullOrWhiteSpace(lsResult) ? null : lsResult.TrimEnd('\r', '\n');
			}
			catch
			{
				return null;
			}
		}
	}
}
