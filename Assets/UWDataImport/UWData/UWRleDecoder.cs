using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Run-length decoder for the compressed images of Ultima Underworld
	/// (uw-formats.txt 3.2.2). The word size depends on the compression type:
	///
	///   Type 08 -> 4 bits per word
	///   Type 06 -> 5 bits per word
	///
	/// Why this exists next to the decoder already in UWTexture: that one works on
	/// nibbles only and can therefore only do type 08. The critter animations use type 06,
	/// i.e. 5-bit words that do not sit on byte boundaries - that needs a real
	/// bitstream reader. This class is kept general and could replace the older decoder
	/// later.
	///
	/// The resulting words are NOT palette indices yet, but indices into an
	/// auxiliary palette (16 or 32 entries) that only then maps to the real 256 colours.
	/// </summary>
	public static class UWRleDecoder
	{
		private class Bitstream
		{
			private readonly byte[] myData;

			private readonly int miWordSize;

			private int miBitPosition;

			public Bitstream(byte[] pyData, int piStart, int piWordSize)
			{
				myData = pyData;
				miBitPosition = piStart * 8;
				miWordSize = piWordSize;
			}

			public bool AtEnd => (miBitPosition + miWordSize) > (myData.Length * 8);

			/// <summary>Reads one word, most significant bit first, across byte boundaries.</summary>
			public int ReadWord()
			{
				int liResult = 0;

				for (int liBit = 0; liBit < miWordSize; liBit++)
				{
					int liByte = miBitPosition >> 3;

					if (liByte >= myData.Length)
						return liResult << (miWordSize - liBit - 1);

					int liShift = 7 - (miBitPosition & 7);
					liResult = (liResult << 1) | ((myData[liByte] >> liShift) & 1);
					miBitPosition++;
				}

				return liResult;
			}

			/// <summary>A number made of one, three or six words: a first word of zero is
			/// followed by two more, and if those are zero too, by three more. Even at word size 5 the words are only shifted by 4 bits - that is
			/// what the format description says, so the words overlap
			/// arithmetically.</summary>
			public int ReadCount()
			{
				int liWord = ReadWord();

				if (liWord != 0)
					return liWord;

				int liCount = (ReadWord() << 4) | ReadWord();

				if (liCount != 0)
					return liCount;

				return (((ReadWord() << 4) | ReadWord()) << 4) | ReadWord();
			}
		}

		/// <summary>
		/// Decodes up to piMaxWords words. Returns the raw words, not yet translated through
		/// the auxiliary palette.
		/// </summary>
		public static byte[] Decode(byte[] pyData, int piStart, int piWordSize, int piMaxWords)
		{
			List<byte> lOResult = new List<byte>(piMaxWords);
			Bitstream lOStream = new Bitstream(pyData, piStart, piWordSize);

			// The decoder alternates between repeat records and raw data records. It starts with
			// a repeat record; a count of 1 there skips it (see below).
			bool lbRepeatStage = true;

			while (lOResult.Count < piMaxWords && !lOStream.AtEnd)
			{
				int liCount = lOStream.ReadCount();

				if (lbRepeatStage)
				{
					if (liCount == 1)
					{
						// Record skipped - the next one is a raw data record again.
						// Happens at the start of a file when the image should begin
						// directly with raw data.
						lbRepeatStage = false;
						continue;
					}

					if (liCount == 2)
					{
						// Multiple repeat: the next number says how many
						// repeat records follow in a row.
						int liRepeats = lOStream.ReadCount();

						for (int liIndex = 0; liIndex < liRepeats && lOResult.Count < piMaxWords && !lOStream.AtEnd; liIndex++)
						{
							int liInnerCount = lOStream.ReadCount();
							byte lyInnerWord = (byte)lOStream.ReadWord();

							fAppend(lOResult, lyInnerWord, liInnerCount, piMaxWords);
						}

						lbRepeatStage = false;
						continue;
					}

					byte lyWord = (byte)lOStream.ReadWord();
					fAppend(lOResult, lyWord, liCount, piMaxWords);
					lbRepeatStage = false;
				}
				else
				{
					for (int liIndex = 0; liIndex < liCount && lOResult.Count < piMaxWords && !lOStream.AtEnd; liIndex++)
						lOResult.Add((byte)lOStream.ReadWord());

					lbRepeatStage = true;
				}
			}

			// Pad images whose data stream ends early with 0 instead of returning
			// fewer words - the caller expects width*height.
			while (lOResult.Count < piMaxWords)
				lOResult.Add(0);

			return lOResult.ToArray();
		}

		private static void fAppend(List<byte> pOTarget, byte pyWord, int piCount, int piMaxWords)
		{
			for (int liIndex = 0; liIndex < piCount && pOTarget.Count < piMaxWords; liIndex++)
				pOTarget.Add(pyWord);
		}
	}
}
