using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S COMPRESSION (UW2 plan phase 0 step 5, 2026-10-28; the first game has none):
	/// Haruhiko Okumura's LZSS of 1989, as Underworld Exhumed's docs/FORMATS.md ("Compression")
	/// describes the original's - a 4096-byte ring filled with spaces up to 4078, writing from 4078;
	/// groups of a flag byte, low bit first, then eight items: a 1 bit a literal byte, a 0 bit two
	/// bytes giving a ring position (b0 | (b1 &amp; 0xF0) &lt;&lt; 4) and a length ((b1 &amp; 0x0F) + 3).
	/// Like the original it stops when the compressed bytes run out, not at the expected length.
	///
	/// ENCODING (phase 1 step 1, decision 4: the second game's archives are written compressed like
	/// DOS): Okumura's encoder with his binary search trees - the one 256 trees by the first byte, the
	/// longest match up to 18 and the latest of equal ones, a match of 2 or less written as a literal.
	/// It gives the shipped streams of BYT.ARK, CNV.ARK and LEV.ARK back byte for byte (the self-check
	/// holds it), so it is the original's choice of matches too.
	/// </summary>
	public static class UWLzss
	{
		private const int RingSize = 4096;

		private const int MaxMatch = 18;

		private const int Threshold = 2;

		/// <summary>Decodes piCount bytes of pySource from piOffset; piExpectedLength is the size the
		/// block says it decodes to (a hint for the buffer; the stream decides).</summary>
		public static byte[] Decode(byte[] pySource, int piOffset, int piCount, int piExpectedLength)
		{
			byte[] lyRing = new byte[RingSize];

			for (int liAt = 0; liAt < RingSize - MaxMatch; liAt++)
				lyRing[liAt] = 0x20;

			MemoryStream lOOut = new MemoryStream(Math.Max(piExpectedLength, 0));
			int liRing = RingSize - MaxMatch;
			int liEnd = Math.Min(pySource.Length, piOffset + piCount);
			int liIn = piOffset;
			int liFlags = 0;

			while (true)
			{
				liFlags >>= 1;

				if ((liFlags & 0x100) == 0)
				{
					if (liIn >= liEnd)
						break;

					// The high byte counts the eight items of the group.
					liFlags = pySource[liIn++] | 0xFF00;
				}

				if ((liFlags & 1) != 0)
				{
					if (liIn >= liEnd)
						break;

					byte lyByte = pySource[liIn++];

					lOOut.WriteByte(lyByte);
					lyRing[liRing] = lyByte;
					liRing = (liRing + 1) & (RingSize - 1);
				}
				else
				{
					if (liIn + 1 >= liEnd)
						break;

					int liFirst = pySource[liIn++];
					int liSecond = pySource[liIn++];
					int liPosition = liFirst | ((liSecond & 0xF0) << 4);
					int liLength = (liSecond & 0x0F) + Threshold + 1;

					for (int liStep = 0; liStep < liLength; liStep++)
					{
						byte lyByte = lyRing[(liPosition + liStep) & (RingSize - 1)];

						lOOut.WriteByte(lyByte);
						lyRing[liRing] = lyByte;
						liRing = (liRing + 1) & (RingSize - 1);
					}
				}
			}

			return lOOut.ToArray();
		}

		/// <summary>The LZSS stream of pyData (without the archive's length prefix); empty for no data.</summary>
		public static byte[] Encode(byte[] pyData)
		{
			return new Encoder(pyData).Run();
		}

		/// <summary>One encoding: the ring with its tail copy, and the trees over it - a node per ring
		/// position (NIL = RingSize) and 256 roots after them, one per first byte.</summary>
		private sealed class Encoder
		{
			private const int Nil = RingSize;

			private readonly byte[] myData;

			private readonly byte[] myRing = new byte[RingSize + MaxMatch - 1];

			private readonly int[] miLeft = new int[RingSize + 1];

			private readonly int[] miRight = new int[RingSize + 257];

			private readonly int[] miParent = new int[RingSize + 1];

			private int miMatchPosition;

			private int miMatchLength;

			public Encoder(byte[] pyData)
			{
				myData = pyData ?? new byte[0];
			}

			public byte[] Run()
			{
				MemoryStream lOOut = new MemoryStream();

				for (int liAt = RingSize + 1; liAt <= RingSize + 256; liAt++)
					miRight[liAt] = Nil;

				for (int liAt = 0; liAt < RingSize; liAt++)
					miParent[liAt] = Nil;

				byte[] lyGroup = new byte[17];
				int liGroupLength = 1;
				int liMask = 1;
				int liStart = 0;
				int liAhead = RingSize - MaxMatch;
				int liIn = 0;
				int liLength = 0;

				for (int liAt = liStart; liAt < liAhead; liAt++)
					myRing[liAt] = 0x20;

				while (liLength < MaxMatch && liIn < myData.Length)
					myRing[liAhead + liLength++] = myData[liIn++];

				if (liLength == 0)
					return lOOut.ToArray();

				for (int liAt = 1; liAt <= MaxMatch; liAt++)
					fInsert(liAhead - liAt);

				fInsert(liAhead);

				do
				{
					if (miMatchLength > liLength)
						miMatchLength = liLength;

					if (miMatchLength <= Threshold)
					{
						miMatchLength = 1;
						lyGroup[0] |= (byte)liMask;
						lyGroup[liGroupLength++] = myRing[liAhead];
					}
					else
					{
						lyGroup[liGroupLength++] = (byte)miMatchPosition;
						lyGroup[liGroupLength++] = (byte)(((miMatchPosition >> 4) & 0xF0) | (miMatchLength - (Threshold + 1)));
					}

					liMask = (liMask << 1) & 0xFF;

					if (liMask == 0)
					{
						lOOut.Write(lyGroup, 0, liGroupLength);
						lyGroup[0] = 0;
						liGroupLength = 1;
						liMask = 1;
					}

					int liLast = miMatchLength;
					int liStep = 0;

					for (; liStep < liLast && liIn < myData.Length; liStep++)
					{
						byte lyByte = myData[liIn++];

						fDelete(liStart);
						myRing[liStart] = lyByte;

						// The copy of the ring's start after its end, so a match may run past it.
						if (liStart < MaxMatch - 1)
							myRing[liStart + RingSize] = lyByte;

						liStart = (liStart + 1) & (RingSize - 1);
						liAhead = (liAhead + 1) & (RingSize - 1);
						fInsert(liAhead);
					}

					// The input has ended: the rest of the look-ahead is used up.
					while (liStep++ < liLast)
					{
						fDelete(liStart);
						liStart = (liStart + 1) & (RingSize - 1);
						liAhead = (liAhead + 1) & (RingSize - 1);

						if (--liLength != 0)
							fInsert(liAhead);
					}
				}
				while (liLength > 0);

				if (liGroupLength > 1)
					lOOut.Write(lyGroup, 0, liGroupLength);

				return lOOut.ToArray();
			}

			/// <summary>Puts the string at piAt into its tree and finds the longest match for it; a match of
			/// the full length replaces the older node.</summary>
			private void fInsert(int piAt)
			{
				int liCompare = 1;
				int liNode = RingSize + 1 + myRing[piAt];

				miRight[piAt] = Nil;
				miLeft[piAt] = Nil;
				miMatchLength = 0;

				while (true)
				{
					if (liCompare >= 0)
					{
						if (miRight[liNode] != Nil)
							liNode = miRight[liNode];
						else
						{
							miRight[liNode] = piAt;
							miParent[piAt] = liNode;

							return;
						}
					}
					else
					{
						if (miLeft[liNode] != Nil)
							liNode = miLeft[liNode];
						else
						{
							miLeft[liNode] = piAt;
							miParent[piAt] = liNode;

							return;
						}
					}

					int liAt = 1;

					for (; liAt < MaxMatch; liAt++)
					{
						liCompare = myRing[piAt + liAt] - myRing[liNode + liAt];

						if (liCompare != 0)
							break;
					}

					if (liAt > miMatchLength)
					{
						miMatchPosition = liNode;
						miMatchLength = liAt;

						if (liAt >= MaxMatch)
							break;
					}
				}

				miParent[piAt] = miParent[liNode];
				miLeft[piAt] = miLeft[liNode];
				miRight[piAt] = miRight[liNode];
				miParent[miLeft[liNode]] = piAt;
				miParent[miRight[liNode]] = piAt;

				if (miRight[miParent[liNode]] == liNode)
					miRight[miParent[liNode]] = piAt;
				else
					miLeft[miParent[liNode]] = piAt;

				miParent[liNode] = Nil;
			}

			/// <summary>Takes the node at piAt out of its tree.</summary>
			private void fDelete(int piAt)
			{
				if (miParent[piAt] == Nil)
					return;

				int liNode;

				if (miRight[piAt] == Nil)
					liNode = miLeft[piAt];
				else if (miLeft[piAt] == Nil)
					liNode = miRight[piAt];
				else
				{
					liNode = miLeft[piAt];

					if (miRight[liNode] != Nil)
					{
						do
						{
							liNode = miRight[liNode];
						}
						while (miRight[liNode] != Nil);

						miRight[miParent[liNode]] = miLeft[liNode];
						miParent[miLeft[liNode]] = miParent[liNode];
						miLeft[liNode] = miLeft[piAt];
						miParent[miLeft[piAt]] = liNode;
					}

					miRight[liNode] = miRight[piAt];
					miParent[miRight[piAt]] = liNode;
				}

				miParent[liNode] = miParent[piAt];

				if (miRight[miParent[piAt]] == piAt)
					miRight[miParent[piAt]] = liNode;
				else
					miLeft[miParent[piAt]] = liNode;

				miParent[piAt] = Nil;
			}
		}
	}
}
