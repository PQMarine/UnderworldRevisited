using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The virtual machine that executes a conversation (uw-formats.txt 7.3 to 7.5).
	///
	/// A conversation is a small program: 16-bit words as instructions, a memory of
	/// 64k words, three registers (BP base pointer, SP stack pointer, RV return value). The
	/// texts are not in the program but in a separate string block; the program
	/// only names numbers.
	///
	/// Everything that acts from the conversation on the world - showing text, presenting
	/// a choice, reading quest flags, trading items - goes through IHost. This class
	/// knows neither Unity nor the savegame.
	///
	/// PAUSING AND RESUMING: a choice needs the player. Instead of blocking,
	/// Run() then reports AwaitingChoice and remembers the position; as soon as the answer
	/// is there, it continues with SupplyChoice(). That is why the whole state is a field of this
	/// class and not a local variable.
	/// </summary>
	public class UWConversationVM
	{
		public enum RunState
		{
			/// <summary>Still running - call Run() again.</summary>
			Running,

			/// <summary>Needs an answer from the player, see PendingChoices.</summary>
			AwaitingChoice,

			/// <summary>Needs typed text (babl_ask), see SupplyTypedInput.</summary>
			AwaitingInput,

			/// <summary>The conversation is over.</summary>
			Finished,

			/// <summary>Aborted, see Error.</summary>
			Failed
		}

		/// <summary>
		/// What the virtual machine needs from outside. The numbers are the
		/// function numbers from the import table (uw-formats.txt 7.6).
		/// </summary>
		public interface IHost
		{
			/// <summary>The NPC says something (SAY_OP and "say").</summary>
			void Say(string psText);

			/// <summary>Text without a speaker ("print"), such as scene descriptions.</summary>
			void Print(string psText);

			/// <summary>Read a quest flag.</summary>
			int GetQuest(int piFlag);

			/// <summary>Set a quest flag.</summary>
			void SetQuest(int piFlag, int piValue);

			/// <summary>Random number from 1 up to and including piMaximum.</summary>
			int Random(int piMaximum);

			/// <summary>
			/// A function this machine does not know itself. The return value is the value
			/// for the result register. pOArguments[0] is the FIRST argument in the
			/// order the caller pushed them - the values have already been fetched
			/// from behind the pointers. pOAddresses are the pointers themselves, for
			/// functions that WRITE SOMETHING BACK (find_barter_total fills an array),
			/// and pOVm the machine, to read and write there (trade: the lists
			/// of preferences lie in the conversation's memory).
			/// </summary>
			int CallUnknown(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
				IReadOnlyList<int> pOAddresses, UWConversationVM pOVm);
		}

		/// <summary>
		/// Fills the start of memory from the conversation's persistent memory (see
		/// UWConversationGlobals) - before the imported globals, which are set on top of it,
		/// as in the reference.
		/// </summary>
		public void LoadMemory(int[] piWords)
		{
			if (piWords == null)
				return;

			for (int liIndex = 0; liIndex < piWords.Length && liIndex < StackStartAddress; liIndex++)
				myMemory[liIndex] = (ushort)piWords[liIndex];
		}

		/// <summary>The counterpart at the end: the same addresses back into persistent memory.</summary>
		public void SaveMemory(int[] piWords)
		{
			if (piWords == null)
				return;

			for (int liIndex = 0; liIndex < piWords.Length && liIndex < StackStartAddress; liIndex++)
				piWords[liIndex] = myMemory[liIndex];
		}

		/// <summary>A word of the conversation memory, signed.</summary>
		public int ReadMemory(int piAddress)
		{
			return fRead(piAddress);
		}

		public void WriteMemory(int piAddress, int piValue)
		{
			fWrite(piAddress, piValue);
		}

		/// <summary>Reads an imported game global, if the conversation imports it
		/// at all. Without this distinction, writing back wrote a zero for everything
		/// a conversation does not know - Bragit turned hostile after the conversation because
		/// his attitude ended up at zero that way (per user, 2026-09-11).</summary>
		public bool TryGetImportedGlobal(string psName, out int piValue)
		{
			foreach (UWConversations.Import lOImport in mOConversation.Imports)
			{
				if (!lOImport.IsFunction && lOImport.Name == psName)
				{
					piValue = fRead(lOImport.IdOrAddress);
					return true;
				}
			}

			piValue = 0;
			return false;
		}

		/// <summary>An imported game global, or 0 if the conversation does not
		/// know it.</summary>
		public int GetImportedGlobal(string psName)
		{
			foreach (UWConversations.Import lOImport in mOConversation.Imports)
			{
				if (!lOImport.IsFunction && lOImport.Name == psName)
					return fRead(lOImport.IdOrAddress);
			}

			return 0;
		}

		private const int MemorySize = 0x10000;

		private const int StackStartAddress = 0x1000;

		private const int BablMenuFunction = 0x0000;

		private const int BablFMenuFunction = 0x0001;

		private const int PrintFunction = 0x0002;

		/// <summary>babl_ask: the player types an answer; the result is the
		/// string number of what was typed (the reference appends the text to the string block).
		/// </summary>
		private const int BablAskFunction = 0x0003;

		private const int RandomFunction = 0x0005;

		private const int SayFunction = 0x000D;

		private const int GetQuestFunction = 0x000F;

		private const int SetQuestFunction = 0x0010;

		/// <summary>Guard against infinite loops in foreign code. A conversation does not need
		/// a million steps; if the number is reached, something is wrong.</summary>
		private const int MaxSteps = 1000000;

		private readonly UWConversations.Conversation mOConversation;

		/// <summary>The code this machine runs - the conversation's own words with the script fixes
		/// of UWConversationPatches.</summary>
		private readonly ushort[] myCode;

		private readonly IHost mOHost;

		private readonly Func<int, string> mOGetString;

		private readonly ushort[] myMemory = new ushort[MemorySize];

		private int miInstructionPointer;
		private int miBasePointer;
		private int miStackPointer;
		private int miResultRegister;
		private bool mbFinished;
		private bool mbStarted;

		/// <summary>After RunState.AwaitingChoice: the texts the player
		/// chooses from. The answer is the ONE-based index into this list.</summary>
		public List<string> PendingChoices { get; } = new List<string>();

		/// <summary>What went wrong, if RunState.Failed came.</summary>
		public string Error { get; private set; }

		public UWConversationVM(UWConversations.Conversation pOConversation, IHost pOHost, Func<int, string> pOGetString)
		{
			mOConversation = pOConversation;
			myCode = UWConversationPatches.GetCode(pOConversation);
			mOHost = pOHost;
			mOGetString = pOGetString;

			// The stack starts far above the variables: 0 to 31 are the imported
			// game globals (the import table names their addresses, up to 31), from 32
			// on are the private globals. 0x1000 leaves plenty of room in between, and the
			// memory holds 64k words.
			miStackPointer = StackStartAddress;
			miBasePointer = miStackPointer;
		}

		/// <summary>Sets an imported game global at its address from the
		/// import table.</summary>
		public void SetImportedGlobal(string psName, int piValue)
		{
			foreach (UWConversations.Import lOImport in mOConversation.Imports)
			{
				if (lOImport.IsFunction || lOImport.Name != psName)
					continue;

				if (lOImport.IdOrAddress >= 0 && lOImport.IdOrAddress < myMemory.Length)
					myMemory[lOImport.IdOrAddress] = (ushort)piValue;

				return;
			}
		}

		/// <summary>The answer to a choice, one-based as in the original.</summary>
		public void SupplyChoice(int piChoice)
		{
			// babl_fmenu returns the string number of the chosen entry, babl_menu the position.
			if (mbPendingChoiceIsString && piChoice >= 1 && piChoice <= mOPendingChoiceStrings.Count)
				fSetCallResult(mOPendingChoiceStrings[piChoice - 1]);
			else
				fSetCallResult(piChoice);

			PendingChoices.Clear();
			mOPendingChoiceStrings.Clear();
			mbPendingChoiceIsString = false;
		}

		private readonly List<int> mOPendingChoiceStrings = new List<int>();

		private bool mbPendingChoiceIsString;

		/// <summary>The typed answer to babl_ask, as a string number.</summary>
		public void SupplyTypedInput(int piStringNumber)
		{
			fSetCallResult(piStringNumber);
		}

		/// <summary>
		/// The result of an imported function: into the result register AND over the word on
		/// top of the stack, the argument count, as CallImportedFunction_ovr093_1FBD does. The
		/// caller pops that word, but a broken frame (see PUSHBP) can read it again later.
		/// </summary>
		private void fSetCallResult(int piValue)
		{
			miResultRegister = piValue;

			if (miStackPointer >= 0 && miStackPointer < myMemory.Length)
				myMemory[miStackPointer] = (ushort)piValue;
		}

		/// <summary>
		/// Runs until the conversation needs an answer or is over.
		/// </summary>
		public RunState Run()
		{
			if (mbFinished)
				return RunState.Finished;

			if (mOConversation == null || myCode == null || myCode.Length == 0)
			{
				Error = "No program present.";
				return RunState.Failed;
			}

			for (int liStep = 0; liStep < MaxSteps; liStep++)
			{
				if (miInstructionPointer < 0 || miInstructionPointer >= myCode.Length)
				{
					Error = string.Format("Instruction pointer {0} lies outside the program.", miInstructionPointer);
					return RunState.Failed;
				}

				RunState leState = fExecuteOne();

				if (leState != RunState.Running)
					return leState;
			}

			Error = "Too many steps - probably an infinite loop.";

			return RunState.Failed;
		}

		private RunState fExecuteOne()
		{
			UWConversationDisassembler.Opcode leOpcode =
				(UWConversationDisassembler.Opcode)myCode[miInstructionPointer];

			int liOperand = 0;

			if (UWConversationDisassembler.HasImmediateOperand(leOpcode))
			{
				if (miInstructionPointer + 1 >= myCode.Length)
				{
					Error = "Operand missing at end of program.";
					return RunState.Failed;
				}

				liOperand = myCode[miInstructionPointer + 1];
			}

			int liNext = miInstructionPointer + (UWConversationDisassembler.HasImmediateOperand(leOpcode) ? 2 : 1);

			switch (leOpcode)
			{
				case UWConversationDisassembler.Opcode.NOP:
					break;

				case UWConversationDisassembler.Opcode.OPADD:
					fBinary((a, b) => a + b);
					break;

				case UWConversationDisassembler.Opcode.OPMUL:
					fBinary((a, b) => a * b);
					break;

				// For the non-commutative operations s[1] is the left value: the one pushed
				// first is on the left. Hence b - a and not a - b.
				case UWConversationDisassembler.Opcode.OPSUB:
					fBinary((a, b) => b - a);
					break;

				case UWConversationDisassembler.Opcode.OPDIV:
					fBinary((a, b) => a == 0 ? 0 : b / a);
					break;

				case UWConversationDisassembler.Opcode.OPMOD:
					fBinary((a, b) => a == 0 ? 0 : b % a);
					break;

				case UWConversationDisassembler.Opcode.OPOR:
					fBinary((a, b) => (a != 0 || b != 0) ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.OPAND:
					fBinary((a, b) => (a != 0 && b != 0) ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.OPNOT:
					fPush(fPop() == 0 ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.TSTGT:
					fBinary((a, b) => b > a ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.TSTGE:
					fBinary((a, b) => b >= a ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.TSTLT:
					fBinary((a, b) => b < a ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.TSTLE:
					fBinary((a, b) => b <= a ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.TSTEQ:
					fBinary((a, b) => a == b ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.TSTNE:
					fBinary((a, b) => a != b ? 1 : 0);
					break;

				case UWConversationDisassembler.Opcode.JMP:
					miInstructionPointer = liOperand;
					return RunState.Running;

				case UWConversationDisassembler.Opcode.BEQ:
					if (fPop() == 0)
					{
						miInstructionPointer = liNext + fToSigned(liOperand) - 1;
						return RunState.Running;
					}

					break;

				case UWConversationDisassembler.Opcode.BNE:
					if (fPop() != 0)
					{
						miInstructionPointer = liNext + fToSigned(liOperand) - 1;
						return RunState.Running;
					}

					break;

				case UWConversationDisassembler.Opcode.BRA:
					miInstructionPointer = liNext + fToSigned(liOperand) - 1;
					return RunState.Running;

				case UWConversationDisassembler.Opcode.CALL:
					fPush(liNext);
					miInstructionPointer = liOperand;
					return RunState.Running;

				case UWConversationDisassembler.Opcode.CALLI:
				{
					miInstructionPointer = liNext;

					RunState leState = fCallImported(liOperand);

					return leState;
				}

				// AN EMPTY STACK ENDS THE CONVERSATION (RET_ovr093_1EFB: StackPtr <= 0 returns 0,
				// which stops Conversation_VM_ovr093_176E). A main function that returns ends
				// here, and so does a conversation whose frames a callee has broken: the generic
				// Gray Goblin's trade leaves its caller one word below the stack (see PUSHBP), and
				// its next return ends the talk - in the original after "Farewell"; ours popped on
				// and kept talking (per user, 2026-09-29).
				case UWConversationDisassembler.Opcode.RET:
					if (miStackPointer - StackStartAddress <= 0)
					{
						mbFinished = true;
						return RunState.Finished;
					}

					miInstructionPointer = fPop();
					return RunState.Running;

				case UWConversationDisassembler.Opcode.PUSHI:
					fPush(liOperand);
					break;

				case UWConversationDisassembler.Opcode.PUSHI_EFF:
					fPush(miBasePointer + fToSigned(liOperand));
					break;

				// Discard, NOT into the result register: after a call the caller
				// clears its arguments with POP and then fetches the result with
				// PUSH_REG. If POP overwrote the register, it would be destroyed
				// exactly when it is needed.
				case UWConversationDisassembler.Opcode.POP:
					fPop();
					break;

				case UWConversationDisassembler.Opcode.SWAP:
				{
					int liTop = fPop();
					int liBelow = fPop();

					fPush(liTop);
					fPush(liBelow);

					break;
				}

				// THE SAVED BASE POINTER IS RELATIVE TO THE STACK, as in UW.EXE (PUSHBP_ovr093_19B2,
				// POPBP_ovr093_19CE): there the stack begins right after the conversation's memory
				// slots and BasePtr counts from that start (PUSHI_EFF_ovr093_1A54 adds
				// NoOfMemorySlots). Some compiled code writes element 0 of a local array, which
				// OFFSET (base + index - 1, same in UW.EXE) puts on the saved base pointer - the
				// generic Gray Goblin's do_offer function stores -1 there. In the original the
				// caller's frame then starts one word below the stack, its locals still inside
				// the stack. Ours kept the absolute address, so -1 made the caller's locals
				// addresses 0 to 31 - the imported globals: after a trade the goblin conversation
				// wrote 0 into play_hp and the player died (per user, 2026-09-29).
				case UWConversationDisassembler.Opcode.PUSHBP:
					fPush(miBasePointer - StackStartAddress);
					break;

				case UWConversationDisassembler.Opcode.POPBP:
					miBasePointer = StackStartAddress + fPop();
					break;

				case UWConversationDisassembler.Opcode.SPTOBP:
					miBasePointer = miStackPointer;
					break;

				case UWConversationDisassembler.Opcode.BPTOSP:
					miStackPointer = miBasePointer;
					break;

				case UWConversationDisassembler.Opcode.ADDSP:
					miStackPointer += fPop();
					break;

				case UWConversationDisassembler.Opcode.FETCHM:
					fPush(fRead(fPop()));
					break;

				case UWConversationDisassembler.Opcode.STO:
				{
					int liValue = fPop();
					int liAddress = fPop();

					TraceStore?.Invoke(miInstructionPointer, liAddress, liValue);

					fWrite(liAddress, liValue);

					break;
				}

				case UWConversationDisassembler.Opcode.OFFSET:
				{
					int liIndex = fPop();
					int liAddress = fPop();

					fPush(liAddress + liIndex - 1);

					break;
				}

				case UWConversationDisassembler.Opcode.START:
					mbStarted = true;
					break;

				// Reads the top without taking it (SAVE_REG_ovr093_1A8B) - the compiled code
				// follows it with a POP of its own.
				case UWConversationDisassembler.Opcode.SAVE_REG:
					miResultRegister = miStackPointer >= 0 && miStackPointer < myMemory.Length
						? fToSigned(myMemory[miStackPointer])
						: 0;
					break;

				case UWConversationDisassembler.Opcode.PUSH_REG:
					fPush(miResultRegister);
					break;

				case UWConversationDisassembler.Opcode.EXIT_OP:
					mbFinished = true;
					return RunState.Finished;

				case UWConversationDisassembler.Opcode.SAY_OP:
					mOHost.Say(fGetSubstitutedString(fPop()));
					break;

				case UWConversationDisassembler.Opcode.OPNEG:
					fPush(-fPop());
					break;

				// According to uw-formats.txt, STRCMP and RESPOND_OP do not occur in uw1 ("I
				// haven't yet encountered these in the wild"). They are deliberately
				// skipped instead of guessed - if one of them does turn up, it will show
				// up at the latest when comparing with the original.
				case UWConversationDisassembler.Opcode.STRCMP:
				case UWConversationDisassembler.Opcode.RESPOND_OP:
					break;

				default:
					Error = string.Format("Unknown instruction 0x{0:X4} at position {1}.",
						myCode[miInstructionPointer], miInstructionPointer);

					return RunState.Failed;
			}

			miInstructionPointer = liNext;

			return RunState.Running;
		}

		/// <summary>
		/// Calls an imported function. The known ones are served here, everything else
		/// goes to the host.
		/// </summary>
		private RunState fCallImported(int piFunctionId)
		{
			string lsName = fGetImportName(piFunctionId);

			switch (piFunctionId)
			{
				case BablMenuFunction:
				case BablFMenuFunction:
				{
					// babl_menu gets a pointer to a 0-terminated list of
					// string numbers as its only argument, babl_fmenu two lists:
					// first argument the flag list, second the string list.
					// Only entries with a non-zero flag appear, and the result is
					// the STRING NUMBER, not the list position - the conversation code then compares
					// against string numbers (the reference babl_fmenu.cs; per user 2026-09-13
					// confirmed with Garamon: "Might Tyball's Orb..." does not appear in the original).
					bool lbFiltered = piFunctionId == BablFMenuFunction;
					int liPointer = fGetArgumentAddress(lbFiltered ? 2 : 1);
					// The flag list lies directly below the string list. NOT
					// fGetArgumentAddress(1): with a count of 0 that counts only one argument and
					// would hit the string list again (per user 2026-09-13, Orb stayed visible).
					int liFlagPointer = lbFiltered ? fRead(miStackPointer - 2) : 0;

					PendingChoices.Clear();
					mOPendingChoiceStrings.Clear();
					mbPendingChoiceIsString = lbFiltered;

					for (int liIndex = 0; liIndex < 64; liIndex++)
					{
						int liString = fRead(liPointer + liIndex);

						if (liString == 0)
							break;

						if (lbFiltered && fRead(liFlagPointer + liIndex) == 0)
							continue;

						PendingChoices.Add(fGetSubstitutedString(liString));
						mOPendingChoiceStrings.Add(liString);
					}

					if (PendingChoices.Count == 0)
					{
						fSetCallResult(0);
						return RunState.Running;
					}

					return RunState.AwaitingChoice;
				}

				case PrintFunction:
					mOHost.Print(fGetSubstitutedString(fGetArgumentValue(1)));
					break;

				// Like the menus: the machine pauses until the host supplies the answer
				// (SupplyTypedInput sets the result register), and then continues after the
				// call.
				case BablAskFunction:
					return RunState.AwaitingInput;

				case SayFunction:
					mOHost.Say(fGetSubstitutedString(fGetArgumentValue(1)));
					break;

				case RandomFunction:
					fSetCallResult(mOHost.Random(fGetArgumentValue(1)));
					break;

				case GetQuestFunction:
					fSetCallResult(mOHost.GetQuest(fGetArgumentValue(1)));
					break;

				case SetQuestFunction:
					// First argument the flag number, second the value. Confirmed with Dorna
					// Ironfist (conversation 12): there flag 32 is set to 0, and according to the
					// flag list 32 is exactly the state of the Crux Ansata quest. The docs, however,
					// call arg1 "new flag value", which does not fit the data.
					mOHost.SetQuest(fGetArgumentValue(1), fGetArgumentValue(2));
					break;

				default:
				{
					int liCount = fRead(miStackPointer);

					List<int> lOArguments = new List<int>();
					List<int> lOAddresses = new List<int>();

					for (int liIndex = 1; liIndex <= Math.Max(1, liCount); liIndex++)
					{
						lOArguments.Add(fGetArgumentValue(liIndex));
						lOAddresses.Add(fGetArgumentAddress(liIndex));
					}

					fSetCallResult(mOHost.CallUnknown(lsName, piFunctionId, lOArguments, lOAddresses, this));

					break;
				}
			}

			return RunState.Running;
		}


		/// <summary>
		/// The address of a call argument, one-based in the order in which the
		/// caller pushed them.
		///
		/// The calling convention is not in the docs; it was read off the generated code
		/// (2026-08-30): the caller first pushes the arguments, then their count, then
		/// comes CALLI, and afterwards it cleans up with just as many POPs. For get_quest and
		/// random there is a 1, for sex and set_quest a 2 - and just as many pointers
		/// lie before it. The two menu functions push a 0 instead, although they
		/// have arguments; their lists end on a zero entry anyway, so no count
		/// is needed there. That is why the count used is at least piArgument: with a real
		/// count, argument N lies at SP - count + N - 1; with the menus' 0 every argument
		/// resolves to SP - 1, the topmost pointer. That fits babl_menu's single list and
		/// babl_fmenu's string list, but NOT babl_fmenu's flag list, which fCallImported
		/// therefore reads directly at SP - 2.
		///
		/// The arguments are pointers: what is pushed is PUSHI_EFF, i.e. an address.
		/// </summary>
		private int fGetArgumentAddress(int piArgument)
		{
			int liCount = fRead(miStackPointer);
			int liTotal = Math.Max(piArgument, liCount);

			// Argument 1 lies lowest: below the count the arguments lie in
			// reverse push order.
			return fRead(miStackPointer - liTotal + (piArgument - 1));
		}

		/// <summary>The value behind an argument pointer.</summary>
		private int fGetArgumentValue(int piArgument)
		{
			return fRead(fGetArgumentAddress(piArgument));
		}
		private string fGetImportName(int piFunctionId)
		{
			foreach (UWConversations.Import lOImport in mOConversation.Imports)
			{
				if (lOImport.IsFunction && lOImport.IdOrAddress == piFunctionId)
					return lOImport.Name;
			}

			return "?" + piFunctionId;
		}

		/// <summary>
		/// Fetches a conversation text and fills in the placeholders (uw-formats.txt 7.5):
		/// @XY&lt;number&gt;, where X is the source (G global, S stack, P pointer) and Y the type
		/// (S string number, I integer).
		/// </summary>
		private string fGetSubstitutedString(int piStringNumber)
		{
			string lsText = mOGetString != null ? mOGetString(piStringNumber) : null;

			if (string.IsNullOrEmpty(lsText) || lsText.IndexOf('@') < 0)
				return lsText ?? string.Empty;

			System.Text.StringBuilder lOOut = new System.Text.StringBuilder(lsText.Length);

			for (int liIndex = 0; liIndex < lsText.Length; liIndex++)
			{
				if (lsText[liIndex] != '@' || liIndex + 3 >= lsText.Length)
				{
					lOOut.Append(lsText[liIndex]);
					continue;
				}

				char lcSource = lsText[liIndex + 1];
				char lcType = lsText[liIndex + 2];

				int liCursor = liIndex + 3;
				bool lbNegative = false;

				if (liCursor < lsText.Length && lsText[liCursor] == '-')
				{
					lbNegative = true;
					liCursor++;
				}

				int liNumber = 0;
				int liDigits = 0;

				while (liCursor < lsText.Length && lsText[liCursor] >= '0' && lsText[liCursor] <= '9')
				{
					liNumber = (liNumber * 10) + (lsText[liCursor] - '0');
					liCursor++;
					liDigits++;
				}

				if (liDigits == 0)
				{
					lOOut.Append(lsText[liIndex]);
					continue;
				}

				if (lbNegative)
					liNumber = -liNumber;

				// TWO OPTIONAL PARTS FOLLOW (uw-formats 7.5, the reference's pattern
				// [@][GSP][SI](-*[0-9]*)([S][I])?([0-9]*)?([C][0-9]*)?):
				//
				//   SI<n>  a shift read from the stack: the value at BP + n, minus one.
				//   C<n>   an index into an array that starts at the named place, counted from one.
				//
				// Without them Shak's line "@GI3C2 minutes" printed the wrong variable and left a
				// stray "C2" in the text (per user with a screenshot, 2026-09-17: "0C2 minutes").
				int liStackShift = 0;
				int liArrayIndex = 0;

				if (liCursor + 1 < lsText.Length && lsText[liCursor] == 'S' && lsText[liCursor + 1] == 'I')
				{
					liCursor += 2;

					int liShiftDigits = 0;
					int liShift = 0;

					while (liCursor < lsText.Length && lsText[liCursor] >= '0' && lsText[liCursor] <= '9')
					{
						liShift = (liShift * 10) + (lsText[liCursor] - '0');
						liCursor++;
						liShiftDigits++;
					}

					if (liShiftDigits > 0)
						liStackShift = fRead(miBasePointer + liShift) - 1;
				}

				if (liCursor < lsText.Length && lsText[liCursor] == 'C')
				{
					int liIndexAt = liCursor + 1;
					int liIndexDigits = 0;
					int liIndexValue = 0;

					while (liIndexAt < lsText.Length && lsText[liIndexAt] >= '0' && lsText[liIndexAt] <= '9')
					{
						liIndexValue = (liIndexValue * 10) + (lsText[liIndexAt] - '0');
						liIndexAt++;
						liIndexDigits++;
					}

					if (liIndexDigits > 0)
					{
						liArrayIndex = liIndexValue - 1;
						liCursor = liIndexAt;
					}
				}

				int liValue;

				switch (lcSource)
				{
					case 'G':
						liValue = fRead(liNumber + (lcType == 'S' ? liStackShift : liArrayIndex));
						break;

					case 'S':
						liValue = lcType == 'S' && liStackShift != 0
							? fRead(miBasePointer + fRead(miBasePointer + liStackShift))
							: fRead(miBasePointer + liNumber + liArrayIndex);
						break;

					case 'P':
						liValue = fRead(fRead(miBasePointer + liNumber));
						break;

					default:
						lOOut.Append(lsText[liIndex]);
						continue;
				}

				// The substitution can itself contain placeholders again; resolving once
				// is enough in practice and protects against mutual references.
				lOOut.Append(lcType == 'S'
					? (mOGetString != null ? mOGetString(liValue) : liValue.ToString())
					: liValue.ToString());

				liIndex = liCursor - 1;
			}

			return lOOut.ToString();
		}

		private static int fToSigned(int piWord)
		{
			return piWord >= 0x8000 ? piWord - 0x10000 : piWord;
		}

		private void fBinary(Func<int, int, int> pOOperation)
		{
			int liTop = fPop();
			int liBelow = fPop();

			fPush(pOOperation(liTop, liBelow));
		}

		// The stack pointer points at the TOPMOST word, not at the next free one:
		// PUSH increments first and then writes, POP reads and then decrements (like the
		// reference). Only this way, after CALL, PUSHBP, SPTOBP, is the first argument at BP-2
		// and the return address at BP-1. With the opposite convention a
		// call with a pointer argument read the return address as a pointer - Bragit thus set
		// his attitude to 0 and attacked after the conversation (per user, 2026-09-11).
		private void fPush(int piValue)
		{
			if (miStackPointer < -1 || miStackPointer + 1 >= myMemory.Length)
				return;

			miStackPointer++;
			myMemory[miStackPointer] = (ushort)piValue;
		}

		private int fPop()
		{
			int liValue = miStackPointer >= 0 && miStackPointer < myMemory.Length
				? fToSigned(myMemory[miStackPointer])
				: 0;

			miStackPointer--;

			return liValue;
		}

		private int fRead(int piAddress)
		{
			return piAddress >= 0 && piAddress < myMemory.Length ? fToSigned(myMemory[piAddress]) : 0;
		}

		private void fWrite(int piAddress, int piValue)
		{
			if (piAddress >= 0 && piAddress < myMemory.Length)
				myMemory[piAddress] = (ushort)piValue;
		}

		/// <summary>Whether START was executed. For debugging only.</summary>
		public bool HasStarted => mbStarted;

		/// <summary>For debugging only: called on every STO with (instruction pointer, address,
		/// value), if set.</summary>
		public System.Action<int, int, int> TraceStore;
	}
}
