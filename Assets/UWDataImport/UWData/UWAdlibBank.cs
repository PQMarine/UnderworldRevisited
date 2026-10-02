using System.Collections.Generic;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The AdLib timbre library UW.AD in the format of the Miles Sound library (AIL 2.0).
	///
	/// LAYOUT: at the front a directory of six-byte entries - patch number, bank,
	/// file offset -, terminated by FF FF. Each entry points to a record that starts with
	/// its length. The length tells the kind (counted in the file on
	/// 2026-09-11):
	///
	///   Bank 0, 128 records of 14 bytes    melodic instruments 0 to 127 (General MIDI numbers)
	///   Bank 127, 24 records of 14 bytes   drums, number = note of MIDI channel 10
	///   Bank 1, 23 records of 194 to 354   TVFX effect programs (see UWTvfxVoice)
	///
	/// ONE 14-BYTE RECORD (length, transpose, then eleven register values): modulator 0x20,
	/// 0x40, 0x60, 0x80, 0xE0, then feedback/connection 0xC0, then the carrier in
	/// the same order. The order is checked against the data: that way attack rates
	/// (0xF1) and waveforms (0 to 3) sit where they belong. For drums the
	/// transpose is the absolute note at which the sound is played.
	/// </summary>
	public class UWAdlibBank
	{
		public class MelodicPatch
		{
			public sbyte Transpose;

			/// <summary>0x20, 0x40, 0x60, 0x80, 0xE0 of the modulator.</summary>
			public byte[] Modulator = new byte[5];

			/// <summary>Register 0xC0: feedback in bits 1-3, bit 0 additive.</summary>
			public byte FeedbackConnection;

			/// <summary>0x20, 0x40, 0x60, 0x80, 0xE0 of the carrier.</summary>
			public byte[] Carrier = new byte[5];

			public bool IsAdditive => (FeedbackConnection & 1) != 0;
		}

		/// <summary>One of the eight animated quantities of an effect program.</summary>
		public struct TvfxParameter
		{
			public ushort InitialValue;

			public ushort KeyOnOffset;

			public ushort ReleaseOffset;
		}

		public enum TvfxType : byte
		{
			BankInstrument = 0,
			TvInstrument = 1,
			TvEffect = 2,
			Opl3Instrument = 3
		}

		/// <summary>
		/// A TVFX program (bank 1): 54-byte header with eight parameter triples (initial value,
		/// offset of the key-on stream, offset of the release stream), then optionally eight bytes
		/// of envelopes (present if the first key-on offset is not 0x34), then the
		/// streams themselves - bytecode for the state machine in UWTvfxVoice.
		///
		/// Layout after the reference (UnderworldGodot, TvfxPatch.cs, which cites the Miles
		/// AIL driver source for it; the driver code itself ships with the game as
		/// SOUND\ADLIB.ADV).
		/// </summary>
		public class TvfxPatch
		{
			public byte[] Raw;

			public int Size;

			public sbyte Transpose;

			public TvfxType Type;

			public int Duration;

			public TvfxParameter[] Parameters = new TvfxParameter[8];

			public bool HasEnvelopeBlock;

			public byte KeyOnAdCarrier, KeyOnSrCarrier, KeyOnAdModulator, KeyOnSrModulator;

			public byte ReleaseAdCarrier, ReleaseSrCarrier, ReleaseAdModulator, ReleaseSrModulator;

			public TvfxPatch(byte[] pyRaw)
			{
				Raw = pyRaw;
				Size = fRead16(pyRaw, 0);
				Transpose = (sbyte)pyRaw[2];
				Type = (TvfxType)pyRaw[3];
				Duration = fRead16(pyRaw, 4);

				for (int liAt = 0; liAt < 8; liAt++)
				{
					int liOffset = 6 + (liAt * 6);

					Parameters[liAt] = new TvfxParameter
					{
						InitialValue = (ushort)fRead16(pyRaw, liOffset),
						KeyOnOffset = (ushort)fRead16(pyRaw, liOffset + 2),
						ReleaseOffset = (ushort)fRead16(pyRaw, liOffset + 4)
					};
				}

				HasEnvelopeBlock = Parameters[0].KeyOnOffset != 0x34;

				if (HasEnvelopeBlock && pyRaw.Length >= 0x3E)
				{
					// In each pair the SR register comes first, the AD register after it.
					KeyOnSrCarrier = pyRaw[0x36];
					KeyOnAdCarrier = pyRaw[0x37];
					KeyOnSrModulator = pyRaw[0x38];
					KeyOnAdModulator = pyRaw[0x39];
					ReleaseSrCarrier = pyRaw[0x3A];
					ReleaseAdCarrier = pyRaw[0x3B];
					ReleaseSrModulator = pyRaw[0x3C];
					ReleaseAdModulator = pyRaw[0x3D];
				}
				else
				{
					KeyOnAdCarrier = KeyOnAdModulator = ReleaseAdCarrier = ReleaseAdModulator = 0xFF;
					KeyOnSrCarrier = KeyOnSrModulator = ReleaseSrCarrier = ReleaseSrModulator = 0x0F;
				}
			}
		}

		private const int MelodicPatchSize = 14;

		private const int Mt32PatchSize = 248;

		private const int DrumBank = 127;

		private const int EffectBank = 1;

		private readonly Dictionary<int, MelodicPatch> mOMelodic = new Dictionary<int, MelodicPatch>();

		private readonly Dictionary<int, MelodicPatch> mODrums = new Dictionary<int, MelodicPatch>();

		private readonly Dictionary<int, TvfxPatch> mOEffects = new Dictionary<int, TvfxPatch>();

		public bool IsLoaded { get; private set; }

		public int MelodicCount => mOMelodic.Count;

		public int DrumCount => mODrums.Count;

		public int EffectCount => mOEffects.Count;

		public UWAdlibBank(string psFileName)
		{
			if (!File.Exists(psFileName))
				return;

			byte[] lyData = File.ReadAllBytes(psFileName);
			int liCursor = 0;

			while (liCursor + 6 <= lyData.Length)
			{
				int liPatch = lyData[liCursor];
				int liBank = lyData[liCursor + 1];

				if (liPatch == 0xFF && liBank == 0xFF)
					break;

				int liOffset = lyData[liCursor + 2] | (lyData[liCursor + 3] << 8)
					| (lyData[liCursor + 4] << 16) | (lyData[liCursor + 5] << 24);

				liCursor += 6;

				if (liOffset < 0 || liOffset + 2 > lyData.Length)
					continue;

				int liSize = fRead16(lyData, liOffset);

				if (liSize < 2 || liOffset + liSize > lyData.Length)
					continue;

				if (liSize == MelodicPatchSize)
				{
					MelodicPatch lOPatch = new MelodicPatch
					{
						Transpose = (sbyte)lyData[liOffset + 2]
					};

					for (int liAt = 0; liAt < 5; liAt++)
					{
						lOPatch.Modulator[liAt] = lyData[liOffset + 3 + liAt];
						lOPatch.Carrier[liAt] = lyData[liOffset + 9 + liAt];
					}

					lOPatch.FeedbackConnection = lyData[liOffset + 8];

					if (liBank == DrumBank)
						mODrums[liPatch] = lOPatch;
					else if (liBank == 0)
						mOMelodic[liPatch] = lOPatch;

					continue;
				}

				if (liSize == Mt32PatchSize)
					continue;

				if (liBank == EffectBank)
				{
					byte[] lyRaw = new byte[liSize];
					System.Array.Copy(lyData, liOffset, lyRaw, 0, liSize);
					mOEffects[liPatch] = new TvfxPatch(lyRaw);
				}
			}

			IsLoaded = mOMelodic.Count > 0 || mOEffects.Count > 0;
		}

		public MelodicPatch GetMelodic(int piPatch)
		{
			MelodicPatch lOPatch;

			return mOMelodic.TryGetValue(piPatch, out lOPatch) ? lOPatch : null;
		}

		/// <summary>The drum for a note on channel 10.</summary>
		public MelodicPatch GetDrum(int piNote)
		{
			MelodicPatch lOPatch;

			return mODrums.TryGetValue(piNote, out lOPatch) ? lOPatch : null;
		}

		public TvfxPatch GetEffect(int piPatch)
		{
			TvfxPatch lOPatch;

			return mOEffects.TryGetValue(piPatch, out lOPatch) ? lOPatch : null;
		}

		private static int fRead16(byte[] pyData, int piOffset)
		{
			return pyData[piOffset] | (pyData[piOffset + 1] << 8);
		}
	}
}
