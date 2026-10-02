using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The conversations from "cnv.ark" (uw-formats.txt chapter 7). Each conversation is
	/// not a dialogue tree but a PROGRAM: bytecode for a 16-bit machine with
	/// base pointer, stack pointer and a result register. Branches, loops,
	/// subroutines and calls into the game world are perfectly ordinary instructions in it.
	///
	/// This class loads and disassembles the programs. It does NOT execute them - that would
	/// need the machine plus the roughly 40 built-in functions (see
	/// uw-formats.txt 7.6), which is a separate step. The benefit even without execution:
	/// one sees which conversation touches which game variables and functions - and
	/// exactly through those run things like the portcullis at the goblins, which in the original
	/// opens during a conversation.
	///
	/// In uw1 the file is uncompressed; only the .ark files of uw2 are packed.
	/// </summary>
	public class UWConversations
	{
		public class Import
		{
			public string Name;

			/// <summary>For functions the call id, for variables the memory address.</summary>
			public int IdOrAddress;

			/// <summary>0x010F variable, 0x0111 imported function.</summary>
			public int ImportType;

			/// <summary>0x0000 no return value, 0x0129 number, 0x012B string.</summary>
			public int ReturnType;

			public bool IsFunction => ImportType == FunctionImportType;

			public override string ToString()
			{
				return string.Format("{0} {1} = {2}", IsFunction ? "func" : "var", Name, IdOrAddress);
			}
		}

		public class Conversation
		{
			/// <summary>Slot in cnv.ark, zero-based.</summary>
			public int Slot;

			/// <summary>String block the spoken texts come from.</summary>
			public int StringBlock;

			/// <summary>Memory slots for variables, including imported and
			/// private globals.</summary>
			public int MemorySlots;

			public List<Import> Imports = new List<Import>();

			/// <summary>The bytecode, one word per instruction or operand.</summary>
			public ushort[] Code;
		}

		public const int VariableImportType = 0x010F;

		public const int FunctionImportType = 0x0111;

		/// <summary>The names of the conversation partners are in string block 7.</summary>
		public const int PartnerNameStringBlock = 7;

		/// <summary>The docs give 16 as the offset. Here it is 17, because of the same
		/// offset that applies throughout the project: our parsed string blocks are one
		/// entry above the game's numbering (see GetObjectDescription with ObjectId + 1).
		/// Cross-check against the data: block 7 holds trade phrases up to index 17 ("an excellent
		/// deal..."), names start at 18 - Corby, Shak, Goldthirst. With 16, slot 1 lands on
		/// the phrase, with 17 on Corby.</summary>
		public const int PartnerNameOffset = 17;

		private readonly byte[] myData;

		private readonly int[] miOffsets;

		private readonly Dictionary<int, Conversation> mOCache = new Dictionary<int, Conversation>();

		public int SlotCount => miOffsets == null ? 0 : miOffsets.Length;

		public bool IsLoaded { get; private set; }

		/// <summary>Size of the private variable area per conversation slot, from
		/// "babglobs.dat". These variables outlive the conversation - in them an
		/// NPC remembers what it has already said or what the player has promised it. On
		/// saving they end up in "bglobals.dat".</summary>
		private readonly Dictionary<int, int> mOPrivateGlobalSizes = new Dictionary<int, int>();

		public int GetPrivateGlobalSize(int piSlot)
		{
			return mOPrivateGlobalSizes.TryGetValue(piSlot, out int liSize) ? liSize : 0;
		}

		public int PrivateGlobalSlotCount => mOPrivateGlobalSizes.Count;

		private void fLoadPrivateGlobalSizes(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "BABGLOBS.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyData = File.ReadAllBytes(lsFile);

			// Pairs of slot number and size. The values themselves are only in the
			// save game file, explicitly not here.
			for (int liOffset = 0; liOffset + 3 < lyData.Length; liOffset += 4)
			{
				int liSlot = lyData[liOffset] | (lyData[liOffset + 1] << 8);
				int liSize = lyData[liOffset + 2] | (lyData[liOffset + 3] << 8);

				mOPrivateGlobalSizes[liSlot] = liSize;
			}
		}

		public UWConversations(string psDataPath)
		{
			fLoadPrivateGlobalSizes(psDataPath);

			string lsFile = Path.Combine(psDataPath, "CNV.ARK");

			if (!File.Exists(lsFile))
				return;

			myData = File.ReadAllBytes(lsFile);

			if (myData.Length < 2)
				return;

			int liCount = fRead16(0);
			miOffsets = new int[liCount];

			for (int liIndex = 0; liIndex < liCount; liIndex++)
				miOffsets[liIndex] = (int)fRead32(2 + (liIndex * 4));

			IsLoaded = true;
		}

		/// <summary>Whether this slot is occupied at all - an offset of 0 means "no
		/// conversation".</summary>
		public bool HasConversation(int piSlot)
		{
			return IsLoaded && piSlot >= 0 && piSlot < miOffsets.Length && miOffsets[piSlot] != 0;
		}

		/// <summary>String index of the partner name in string block 7.</summary>
		public static int GetPartnerNameIndex(int piSlot)
		{
			return piSlot + PartnerNameOffset;
		}

		public Conversation GetConversation(int piSlot)
		{
			if (!HasConversation(piSlot))
				return null;

			if (mOCache.TryGetValue(piSlot, out Conversation lOCached))
				return lOCached;

			Conversation lOResult = fLoad(piSlot);
			mOCache[piSlot] = lOResult;

			return lOResult;
		}

		private Conversation fLoad(int piSlot)
		{
			int liCursor = miOffsets[piSlot];

			Conversation lOResult = new Conversation
			{
				Slot = piSlot
			};

			// 0x0000 and 0x0002 are unknown and constant according to the docs (0x0828 / 0x0000).
			int liCodeSize = fRead16(liCursor + 4);
			lOResult.StringBlock = fRead16(liCursor + 10);
			lOResult.MemorySlots = fRead16(liCursor + 12);
			int liImportCount = fRead16(liCursor + 14);

			liCursor += 16;

			for (int liIndex = 0; liIndex < liImportCount; liIndex++)
			{
				int liNameLength = fRead16(liCursor);
				liCursor += 2;

				StringBuilder lOName = new StringBuilder();

				for (int liChar = 0; liChar < liNameLength; liChar++)
				{
					byte lyChar = myData[liCursor + liChar];

					if (lyChar != 0)
						lOName.Append((char)lyChar);
				}

				liCursor += liNameLength;

				lOResult.Imports.Add(new Import
				{
					Name = lOName.ToString(),
					IdOrAddress = fRead16(liCursor),
					// liCursor + 2 is always 1 according to the docs.
					ImportType = fRead16(liCursor + 4),
					ReturnType = fRead16(liCursor + 6)
				});

				liCursor += 8;
			}

			lOResult.Code = new ushort[liCodeSize];

			for (int liIndex = 0; liIndex < liCodeSize; liIndex++)
				lOResult.Code[liIndex] = (ushort)fRead16(liCursor + (liIndex * 2));

			return lOResult;
		}

		private int fRead16(int piOffset)
		{
			if (piOffset + 1 >= myData.Length)
				return 0;

			return myData[piOffset] | (myData[piOffset + 1] << 8);
		}

		private uint fRead32(int piOffset)
		{
			if (piOffset + 3 >= myData.Length)
				return 0;

			return (uint)(myData[piOffset] | (myData[piOffset + 1] << 8)
				| (myData[piOffset + 2] << 16) | (myData[piOffset + 3] << 24));
		}
	}
}
