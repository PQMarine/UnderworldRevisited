using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Reads and writes the parts of a LEV.ARK level block that are not records: the file
	/// header, the two free lists and the list of the active mobile objects - the layout of
	/// UWLevArkLayout over the little-endian words of UWBinary (cleanup phase 2, step
	/// "Codecs", 2026-10-12). Until then UWLevel read the lists with its own loops and
	/// UWLevelWriter wrote them with another pair, and the swap-remove of the active list
	/// stood in both.
	///
	/// Every helper takes the array and the block's start in it, so the same code serves the
	/// whole file in memory (UWLevel, start = the block's offset) and a copied block
	/// (UWLevelWriter, start = 0).
	/// </summary>
	public static class UWLevArkCodec
	{
		// ------------------------------------------------- The header

		/// <summary>The block starts the header lists: a 16-bit count, then 32 bits each. A
		/// start of 0 marks a block the file does not have.</summary>
		public static List<uint> ReadBlockOffsets(byte[] pyArk)
		{
			List<uint> lOOffsets = new List<uint>();
			int liCount = (short)UWBinary.ReadU16(pyArk, 0);

			for (int liBlock = 0; liBlock < liCount; liBlock++)
				lOOffsets.Add(UWBinary.Read32(pyArk, 2 + (liBlock * 4)));

			return lOOffsets;
		}

		// ------------------------------------------------- The free lists

		/// <summary>
		/// Entries 0 up to the top pointer, in storage order - the topmost last, which is the
		/// one Obj_Alloc hands out next. The pointer is a signed 16-bit index, -1 for an empty
		/// list. Empty when the pointer does not fit into the array (a truncated file).
		/// </summary>
		public static List<int> ReadFreeList(byte[] pyData, int piBlockStart, int piListOffset, int piTopOffset, int piCapacity)
		{
			List<int> lOList = new List<int>();
			int liTopAt = piBlockStart + piTopOffset;

			if (liTopAt + 2 > pyData.Length)
				return lOList;

			int liTop = (short)UWBinary.Read16(pyData, liTopAt);

			for (int liAt = 0; liAt <= liTop && liAt < piCapacity; liAt++)
				lOList.Add(UWBinary.Read16(pyData, piBlockStart + piListOffset + (liAt * 2)));

			return lOList;
		}

		public static List<int> ReadMobileFreeList(byte[] pyData, int piBlockStart)
		{
			return ReadFreeList(pyData, piBlockStart, UWLevArkLayout.MobileFreeListOffset,
				UWLevArkLayout.MobileFreeTopOffset, UWLevArkLayout.MobileFreeListCapacity);
		}

		public static List<int> ReadStaticFreeList(byte[] pyData, int piBlockStart)
		{
			return ReadFreeList(pyData, piBlockStart, UWLevArkLayout.StaticFreeListOffset,
				UWLevArkLayout.StaticFreeTopOffset, UWLevArkLayout.StaticFreeListCapacity);
		}

		/// <summary>
		/// Writes a free list back. Entries above the pointer stay as they were - the original
		/// also leaves old values lying there. Only when the list gets LONGER are they
		/// overwritten. A list longer than the capacity is cut.
		/// </summary>
		public static void WriteFreeList(byte[] pyData, int piBlockStart, int piListOffset, int piTopOffset, List<int> pOList, int piCapacity)
		{
			int liCount = pOList.Count < piCapacity ? pOList.Count : piCapacity;

			for (int liAt = 0; liAt < liCount; liAt++)
				UWBinary.Write16(pyData, piBlockStart + piListOffset + (liAt * 2), pOList[liAt]);

			UWBinary.Write16(pyData, piBlockStart + piTopOffset, (liCount - 1) & 0xFFFF);
		}

		public static void WriteMobileFreeList(byte[] pyData, int piBlockStart, List<int> pOList)
		{
			WriteFreeList(pyData, piBlockStart, UWLevArkLayout.MobileFreeListOffset,
				UWLevArkLayout.MobileFreeTopOffset, pOList, UWLevArkLayout.MobileFreeListCapacity);
		}

		public static void WriteStaticFreeList(byte[] pyData, int piBlockStart, List<int> pOList)
		{
			WriteFreeList(pyData, piBlockStart, UWLevArkLayout.StaticFreeListOffset,
				UWLevArkLayout.StaticFreeTopOffset, pOList, UWLevArkLayout.StaticFreeListCapacity);
		}

		// ------------------------------------------------- The active mobile list

		/// <summary>The allocated mobile slots in the original's order, one byte each; empty
		/// when the count word does not fit into the array.</summary>
		public static List<int> ReadActiveMobiles(byte[] pyData, int piBlockStart)
		{
			List<int> lOList = new List<int>();
			int liCountAt = piBlockStart + UWLevArkLayout.ActiveMobileCountOffset;

			if (liCountAt + 2 > pyData.Length)
				return lOList;

			int liCount = UWBinary.Read16(pyData, liCountAt);

			for (int liAt = 0; liAt < liCount && liAt < UWLevArkLayout.ActiveMobileListCapacity; liAt++)
				lOList.Add(pyData[piBlockStart + UWLevArkLayout.ActiveMobileListOffset + liAt]);

			return lOList;
		}

		/// <summary>The list and its count; a list longer than the capacity is cut.</summary>
		public static void WriteActiveMobiles(byte[] pyData, int piBlockStart, List<int> pOList)
		{
			int liCount = pOList.Count < UWLevArkLayout.ActiveMobileListCapacity
				? pOList.Count : UWLevArkLayout.ActiveMobileListCapacity;

			for (int liAt = 0; liAt < liCount; liAt++)
				pyData[piBlockStart + UWLevArkLayout.ActiveMobileListOffset + liAt] = (byte)pOList[liAt];

			UWBinary.Write16(pyData, piBlockStart + UWLevArkLayout.ActiveMobileCountOffset, liCount);
		}

		/// <summary>active_critter (OBJECTS.C): a newly allocated mobile is appended at the end.
		/// The port adds the guard against a second entry for the same slot.</summary>
		public static void AddActiveMobile(List<int> pOList, int piIndex)
		{
			if (!pOList.Contains(piIndex))
				pOList.Add(piIndex);
		}

		/// <summary>free_critter (OBJECTS.C): the freed slot leaves the list and the LAST entry
		/// moves into its place - the order of the rest is kept, which is what the whoami
		/// searches walk (per user, 2026-09-29: Tyball's death took the guards in exactly this
		/// order). False when the slot was not in the list.</summary>
		public static bool RemoveActiveMobile(List<int> pOList, int piIndex)
		{
			int liAt = pOList.IndexOf(piIndex);

			if (liAt < 0)
				return false;

			pOList[liAt] = pOList[pOList.Count - 1];
			pOList.RemoveAt(pOList.Count - 1);

			return true;
		}
	}
}
