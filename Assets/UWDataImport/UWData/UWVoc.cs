using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A sound effect in Creative Voice format (*.VOC) from the "sound" folder. The format
	/// is not an Underworld invention but the standard format of the Sound Blaster era; the
	/// game documentation explicitly refers to it.
	///
	/// Structure: a 26-byte header with the signature "Creative Voice File", followed by
	/// a chain of blocks. Each block starts with a type byte and a 24-bit length.
	/// For Underworld five types are enough - everything else is skipped instead of guessed.
	///
	/// The sample rate is stored as a "time constant" in the data and results in
	/// 1000000 / (256 - time constant). The game's files are thus at around 12 kHz,
	/// 8 bit, mono, uncompressed.
	/// </summary>
	public class UWVoc
	{
		private const int HeaderSize = 0x1A;

		private const int BlockSoundData = 1;

		private const int BlockSoundContinue = 2;

		private const int BlockSilence = 3;

		private const int BlockExtended = 8;

		private const int BlockNewSoundData = 9;

		private const int BlockTerminator = 0;

		/// <summary>Sample rate in hertz.</summary>
		public int SampleRate { get; private set; }

		/// <summary>The samples, normalised to -1 to 1 - that is how a Unity
		/// AudioClip expects them.</summary>
		public float[] Samples { get; private set; }

		public int Channels { get; private set; }

		public bool IsLoaded { get; private set; }

		public UWVoc(string psFileName)
		{
			Channels = 1;
			Samples = new float[0];

			if (!File.Exists(psFileName))
				return;

			byte[] lyData = File.ReadAllBytes(psFileName);

			if (lyData.Length < HeaderSize || !fHasSignature(lyData))
				return;

			// The header size is stored in the file itself - do not use the constant,
			// in case a file has a different header.
			int liCursor = lyData[0x14] | (lyData[0x15] << 8);
			List<float> lOSamples = new List<float>();

			while (liCursor < lyData.Length)
			{
				int liType = lyData[liCursor];

				if (liType == BlockTerminator)
					break;

				if (liCursor + 3 >= lyData.Length)
					break;

				int liLength = lyData[liCursor + 1]
					| (lyData[liCursor + 2] << 8)
					| (lyData[liCursor + 3] << 16);

				int liPayload = liCursor + 4;

				switch (liType)
				{
					case BlockSoundData:
						if (liPayload + 1 < lyData.Length)
						{
							fSetSampleRateFromTimeConstant(lyData[liPayload]);
							fAppendUnsigned8(lOSamples, lyData, liPayload + 2, liLength - 2);
						}

						break;

					case BlockSoundContinue:
						fAppendUnsigned8(lOSamples, lyData, liPayload, liLength);
						break;

					case BlockSilence:
						// The length in samples is stored in the block, not in the block length.
						if (liPayload + 2 < lyData.Length)
						{
							int liSilence = lyData[liPayload] | (lyData[liPayload + 1] << 8);
							fSetSampleRateFromTimeConstant(lyData[liPayload + 2]);

							for (int liIndex = 0; liIndex <= liSilence; liIndex++)
								lOSamples.Add(0f);
						}

						break;

					case BlockExtended:
						// Stereo and higher rates - does not occur in uw1, only read
						// along so that the channel count is correct.
						if (liPayload + 3 < lyData.Length)
							Channels = lyData[liPayload + 3] + 1;

						break;

					case BlockNewSoundData:
						if (liPayload + 11 < lyData.Length)
						{
							SampleRate = lyData[liPayload]
								| (lyData[liPayload + 1] << 8)
								| (lyData[liPayload + 2] << 16)
								| (lyData[liPayload + 3] << 24);
							Channels = lyData[liPayload + 5];
							fAppendUnsigned8(lOSamples, lyData, liPayload + 12, liLength - 12);
						}

						break;
				}

				liCursor = liPayload + liLength;
			}

			Samples = lOSamples.ToArray();
			IsLoaded = Samples.Length > 0 && SampleRate > 0;
		}

		private void fSetSampleRateFromTimeConstant(byte pyTimeConstant)
		{
			// 256 minus time constant must not become 0.
			int liDivisor = 256 - pyTimeConstant;

			if (liDivisor > 0)
				SampleRate = 1000000 / liDivisor;
		}

		private static void fAppendUnsigned8(List<float> pOTarget, byte[] pyData, int piStart, int piCount)
		{
			for (int liIndex = 0; liIndex < piCount; liIndex++)
			{
				int liPosition = piStart + liIndex;

				if (liPosition >= pyData.Length)
					break;

				// 8-bit VOC data is unsigned, 0x80 is the rest position.
				pOTarget.Add((pyData[liPosition] - 128) / 128f);
			}
		}

		private static bool fHasSignature(byte[] pyData)
		{
			const string lsSignature = "Creative Voice File";

			for (int liIndex = 0; liIndex < lsSignature.Length; liIndex++)
			{
				if (pyData[liIndex] != lsSignature[liIndex])
					return false;
			}

			return true;
		}
	}
}
