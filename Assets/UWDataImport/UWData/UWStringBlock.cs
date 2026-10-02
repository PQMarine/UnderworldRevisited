using System;
using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	public class UWStringBlock : IComparable<UWStringBlock>
	{
		public short BlockNumber;

		public int BlockOffset;

		private List<string> mOUWStrings;

		public List<string> Strings => mOUWStrings;

		/// <summary>
		/// A block from STRINGS.PAK: a count word, then one start offset per string,
		/// then the Huffman-encoded strings.
		///
		/// THE OFFSET POINTER WAS ONE WORD TOO EARLY and thus sat on the count word
		/// itself. Result: string zero was read from a nonsensical position - a
		/// truncated piece of the first real string -, all real strings slipped back by
		/// one, and the LAST one fell off the end, because the loop only has as many
		/// iterations as there are strings.
		///
		/// Noticed in dream 1 (block 0x0C18): its script fetches three lines, what was delivered
		/// was two and a fragment (per user, 2026-09-07). In the intro the same kind of
		/// fragment sits at slot zero, there the end of string two.
		///
		/// THE PLACEHOLDER STAYS ON PURPOSE. The whole project looks up strings with an
		/// offset of one - "the project's usual +1", in three dozen places, from
		/// the object names through the state words to the message numbers. If the
		/// list now started at zero, every one of these places would be off by one. Instead
		/// slot zero holds an empty string, and the previously missing
		/// last string is appended at the end. The numbers stay where they are.
		///
		/// Anyone who wants to get rid of the offset one day removes the placeholder here and
		/// at the same time EVERY +1 in the project - after that our numbers match those of the
		/// reference.
		/// </summary>
		public UWStringBlock(byte[] pyBlockData, ref byte[] pyStringFileData, List<UWStringNode> pONodes)
		{
			BlockNumber = BitConverter.ToInt16(pyBlockData, 0);
			BlockOffset = BitConverter.ToInt32(pyBlockData, 2);
			mOUWStrings = new List<string>();
			short num = BitConverter.ToInt16(pyStringFileData, BlockOffset);
			ushort[] array = new ushort[num];
			int num2 = BlockOffset + 2;
			for (int i = 0; i < num; i++)
			{
				array[i] = BitConverter.ToUInt16(pyStringFileData, num2);
				num2 += 2;
			}

			// Slot zero stays empty - see above.
			mOUWStrings.Add(string.Empty);
			int num3 = BlockOffset + (num + 1) * 2;
			int num4 = pONodes.Count - 1;
			int num5 = num3;
			for (int j = 0; j < array.Length; j++)
			{
				StringBuilder stringBuilder = new StringBuilder();
				char c = '|';
				num5 = num3 + array[j];
				int num6 = num4;
				byte b = 0;
				byte b2 = 0;
				do
				{
					num6 = num4;
					while (pONodes[num6].LeftChild != 255 && pONodes[num6].RightChild != 255)
					{
						if (b == 0)
						{
							b = 8;
							b2 = pyStringFileData[num5];
							num5++;
						}
						num6 = (((b2 & 0x80) >> 7 == 1) ? pONodes[num6].RightChild : pONodes[num6].LeftChild);
						b2 <<= 1;
						b--;
					}
					c = (char)pONodes[num6].CharSymbol;
					stringBuilder.Append(c);
				}
				while (c != '|');
				mOUWStrings.Add(stringBuilder.ToString(0, stringBuilder.Length - 1));
			}
		}

		public int CompareTo(UWStringBlock other)
		{
			return other.BlockNumber.CompareTo(BlockNumber);
		}
	}
}
