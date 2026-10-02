using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The memory of conversations: per conversation slot the words its
	/// memory starts with - the imported game globals AND the private variables in which
	/// an NPC remembers what it has already said or what it was promised.
	///
	/// DATA/BABGLOBS.DAT only gives the size per slot (slot, size, one word each), the
	/// savegame keeps the same sequence including values in BGLOBALS.DAT (slot, size, then
	/// size words). A new game starts with zeros.
	///
	/// The reference (conversationvariables.ImportVariables) copies the words at the start
	/// of a conversation to addresses 0 through size minus one and then places the
	/// imported globals on top; at the end it writes the same addresses back.
	/// UWConversationSession does the same. Until 2026-09-11 every conversation started with zeros,
	/// and every NPC greeted you as if for the first time (per user).
	/// </summary>
	public class UWConversationGlobals
	{
		private readonly List<int> mOOrder = new List<int>();

		private readonly Dictionary<int, int[]> mOWords = new Dictionary<int, int[]>();

		public bool IsLoaded { get; private set; }

		/// <summary>psSavegamePath null: a new game, everything zero.</summary>
		public UWConversationGlobals(string psDataPath, string psSavegamePath)
		{
			string lsSave = string.IsNullOrEmpty(psSavegamePath) ? null : Path.Combine(psSavegamePath, "BGLOBALS.DAT");

			if (lsSave != null && File.Exists(lsSave))
			{
				fLoad(File.ReadAllBytes(lsSave), true);
				IsLoaded = mOOrder.Count > 0;
			}

			if (mOOrder.Count == 0)
			{
				string lsSizes = Path.Combine(psDataPath, "BABGLOBS.DAT");

				if (File.Exists(lsSizes))
				{
					fLoad(File.ReadAllBytes(lsSizes), false);
					IsLoaded = mOOrder.Count > 0;
				}
			}
		}

		private void fLoad(byte[] pyData, bool pbWithValues)
		{
			int liAt = 0;

			while (liAt + 3 < pyData.Length)
			{
				int liSlot = pyData[liAt] | (pyData[liAt + 1] << 8);
				int liSize = pyData[liAt + 2] | (pyData[liAt + 3] << 8);

				liAt += 4;

				int[] liWords = new int[liSize];

				if (pbWithValues)
				{
					for (int liIndex = 0; liIndex < liSize && liAt + 1 < pyData.Length; liIndex++, liAt += 2)
						liWords[liIndex] = pyData[liAt] | (pyData[liAt + 1] << 8);
				}

				if (!mOWords.ContainsKey(liSlot))
					mOOrder.Add(liSlot);

				mOWords[liSlot] = liWords;
			}
		}

		/// <summary>The words of a slot - the array itself, changes persist. Null
		/// if the slot has no memory.</summary>
		public int[] GetWords(int piSlot)
		{
			int[] liWords;

			return mOWords.TryGetValue(piSlot, out liWords) ? liWords : null;
		}

		/// <summary>BGLOBALS.DAT as the original writes it: slot, size, words.</summary>
		public byte[] Serialize()
		{
			List<byte> lyOut = new List<byte>();

			foreach (int liSlot in mOOrder)
			{
				int[] liWords = mOWords[liSlot];

				fWrite16(lyOut, liSlot);
				fWrite16(lyOut, liWords.Length);

				foreach (int liWord in liWords)
					fWrite16(lyOut, liWord);
			}

			return lyOut.ToArray();
		}

		private static void fWrite16(List<byte> pyOut, int piValue)
		{
			pyOut.Add((byte)(piValue & 0xFF));
			pyOut.Add((byte)((piValue >> 8) & 0xFF));
		}
	}
}
