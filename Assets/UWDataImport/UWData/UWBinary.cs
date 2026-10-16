using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Little-endian words over a byte array, explicit about the byte order (cleanup phase 1,
	/// 2026-10-11). Until then five classes carried their own fRead16/fWrite16 and two more read
	/// through BitConverter, which takes the HOST'S byte order - identical on every machine we
	/// run on, but a C++ port on a big-endian target would have read the files wrong. Every
	/// original file (LEV.ARK, PLAYER.DAT, BGLOBALS.DAT, UW.AD, the fonts) is little-endian.
	///
	/// The plain readers index the array without a check, as the copies they replace did. The
	/// OrZero readers answer 0 for an offset past the end - PLAYER.DAT and CNV.ARK parsing used
	/// that guard, and keeping it here keeps the behaviour on a truncated file.
	/// </summary>
	public static class UWBinary
	{
		/// <summary>The unsigned 16-bit word at piAt as an int, 0 to 0xFFFF.</summary>
		public static int Read16(byte[] pyData, int piAt)
		{
			return pyData[piAt] | (pyData[piAt + 1] << 8);
		}

		/// <summary>The 16-bit word at piAt as a ushort.</summary>
		public static ushort ReadU16(byte[] pyData, int piAt)
		{
			return (ushort)(pyData[piAt] | (pyData[piAt + 1] << 8));
		}

		/// <summary>The unsigned 32-bit word at piAt.</summary>
		public static uint Read32(byte[] pyData, int piAt)
		{
			return (uint)(pyData[piAt] | (pyData[piAt + 1] << 8)
				| (pyData[piAt + 2] << 16) | (pyData[piAt + 3] << 24));
		}

		/// <summary>Read16 that answers 0 when the word does not fit into the array.</summary>
		public static int Read16OrZero(byte[] pyData, int piAt)
		{
			return piAt + 1 < pyData.Length ? Read16(pyData, piAt) : 0;
		}

		/// <summary>Read32 that answers 0 when the word does not fit into the array.</summary>
		public static uint Read32OrZero(byte[] pyData, int piAt)
		{
			return piAt + 3 < pyData.Length ? Read32(pyData, piAt) : 0;
		}

		/// <summary>Writes the low 16 bits of piValue at piAt.</summary>
		public static void Write16(byte[] pyData, int piAt, int piValue)
		{
			pyData[piAt] = (byte)(piValue & 0xFF);
			pyData[piAt + 1] = (byte)((piValue >> 8) & 0xFF);
		}

		/// <summary>Writes all 32 bits of piValue at piAt.</summary>
		public static void Write32(byte[] pyData, int piAt, int piValue)
		{
			Write16(pyData, piAt, piValue & 0xFFFF);
			Write16(pyData, piAt + 2, (piValue >> 16) & 0xFFFF);
		}

		/// <summary>Appends the low 16 bits of piValue - for a serializer that grows a list
		/// (BGLOBALS.DAT).</summary>
		public static void Write16(List<byte> pyOut, int piValue)
		{
			pyOut.Add((byte)(piValue & 0xFF));
			pyOut.Add((byte)((piValue >> 8) & 0xFF));
		}
	}
}
