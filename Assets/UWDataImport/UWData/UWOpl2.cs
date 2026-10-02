// This file is a C# port of the YM3812 (OPL2) parts of ymfm by Aaron Giles
// (https://github.com/aaronsgiles/ymfm: ymfm.h, ymfm_fm.h, ymfm_fm.ipp,
// ymfm_opl.h, ymfm_opl.cpp). It is distributed under the original license:
//
// BSD 3-Clause License
//
// Copyright (c) 2021, Aaron Giles
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
//
// 3. Neither the name of the copyright holder nor the names of its
//    contributors may be used to endorse or promote products derived from
//    this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

namespace UWDataImport.UWData
{
	/// <summary>
	/// Emulation of the YM3812 (OPL2), the AdLib sound chip - the sound the original
	/// produces on a Sound Blaster: music from the AW*.XMI pieces with the instruments
	/// from UW.AD, and the sound effects it animates as TVFX programs on the same kind of
	/// chip (see UWTvfxVoice; UWAudioEngine runs one instance for music and one for
	/// effects). Ultima Underworld 1 has no sampled effects.
	///
	/// PORT OF YMFM (2026-09-15, see the license header): only the OPL2 path of ymfm's
	/// generic engine (fm_engine_base / fm_channel / fm_operator with
	/// opl_registers_base&lt;2&gt; and ym3812), specialised to nine two-operator channels
	/// and one output. The structure and the order of operations follow ymfm; names are
	/// given in the comments where they help to compare. Not ported: timers, status and
	/// IRQ (nobody reads them), therefore also CSM mode (it needs timer A), save/restore
	/// and the debug logging.
	///
	/// DEVIATION FROM YMFM: a key-off write starts the release immediately instead of at
	/// the next sample. The driver writes key up and key down for a repeated note right
	/// after each other; on the real chip the delays of the register accesses lie in
	/// between, here nothing would, and the note would not be retriggered (noticed by the
	/// user in the intro: four notes in the original, two in ours, 2026-09-11).
	///
	/// The sample rate is the chip's, 49716 Hz (3.579545 MHz divided by prescale 4 and
	/// 18 operators). Anyone who needs a different one resamples - see UWAudioEngine.
	/// </summary>
	public class UWOpl2
	{
		public const int SampleRate = 49716;

		public const int ChannelCount = 9;

		private const int OperatorCount = 18;

		private const int RegisterCount = 0x100;

		private const int WaveformLength = 0x400;

		private const int WaveformCount = 4;

		private const int RegMode = 0x04;

		private const uint AllChannels = (1u << ChannelCount) - 1;

		// envelope states (ymfm envelope_state; DEPRESS is OPLL only but keeps the indices)
		private const int EgDepress = 0;
		private const int EgAttack = 1;
		private const int EgDecay = 2;
		private const int EgSustain = 3;
		private const int EgRelease = 4;
		private const int EgStates = 5;

		// key-on sources (ymfm keyon_type)
		private const int KeyonNormal = 0;
		private const int KeyonRhythm = 1;

		/// <summary>"Quiet" envelope value, used to skip work (ymfm EG_QUIET).</summary>
		private const uint EgQuiet = 0x380;

		/// <summary>Set as phase step to recalculate it each sample when vibrato is on.</summary>
		private const uint PhaseStepDynamic = 1;

		/// <summary>
		/// Absolute value of sin over a quarter period, as 4.8 logarithmic attenuation
		/// (ymfm abs_sin_attenuation: "matches the internal format of the OPN chip,
		/// extracted from the die").
		/// </summary>
		private static readonly ushort[] msSinTable =
		{
			0x859,0x6c3,0x607,0x58b,0x52e,0x4e4,0x4a6,0x471,0x443,0x41a,0x3f5,0x3d3,0x3b5,0x398,0x37e,0x365,
			0x34e,0x339,0x324,0x311,0x2ff,0x2ed,0x2dc,0x2cd,0x2bd,0x2af,0x2a0,0x293,0x286,0x279,0x26d,0x261,
			0x256,0x24b,0x240,0x236,0x22c,0x222,0x218,0x20f,0x206,0x1fd,0x1f5,0x1ec,0x1e4,0x1dc,0x1d4,0x1cd,
			0x1c5,0x1be,0x1b7,0x1b0,0x1a9,0x1a2,0x19b,0x195,0x18f,0x188,0x182,0x17c,0x177,0x171,0x16b,0x166,
			0x160,0x15b,0x155,0x150,0x14b,0x146,0x141,0x13c,0x137,0x133,0x12e,0x129,0x125,0x121,0x11c,0x118,
			0x114,0x10f,0x10b,0x107,0x103,0x0ff,0x0fb,0x0f8,0x0f4,0x0f0,0x0ec,0x0e9,0x0e5,0x0e2,0x0de,0x0db,
			0x0d7,0x0d4,0x0d1,0x0cd,0x0ca,0x0c7,0x0c4,0x0c1,0x0be,0x0bb,0x0b8,0x0b5,0x0b2,0x0af,0x0ac,0x0a9,
			0x0a7,0x0a4,0x0a1,0x09f,0x09c,0x099,0x097,0x094,0x092,0x08f,0x08d,0x08a,0x088,0x086,0x083,0x081,
			0x07f,0x07d,0x07a,0x078,0x076,0x074,0x072,0x070,0x06e,0x06c,0x06a,0x068,0x066,0x064,0x062,0x060,
			0x05e,0x05c,0x05b,0x059,0x057,0x055,0x053,0x052,0x050,0x04e,0x04d,0x04b,0x04a,0x048,0x046,0x045,
			0x043,0x042,0x040,0x03f,0x03e,0x03c,0x03b,0x039,0x038,0x037,0x035,0x034,0x033,0x031,0x030,0x02f,
			0x02e,0x02d,0x02b,0x02a,0x029,0x028,0x027,0x026,0x025,0x024,0x023,0x022,0x021,0x020,0x01f,0x01e,
			0x01d,0x01c,0x01b,0x01a,0x019,0x018,0x017,0x017,0x016,0x015,0x014,0x014,0x013,0x012,0x011,0x011,
			0x010,0x00f,0x00f,0x00e,0x00d,0x00d,0x00c,0x00c,0x00b,0x00a,0x00a,0x009,0x009,0x008,0x008,0x007,
			0x007,0x007,0x006,0x006,0x005,0x005,0x005,0x004,0x004,0x004,0x003,0x003,0x003,0x002,0x002,0x002,
			0x002,0x001,0x001,0x001,0x001,0x001,0x001,0x001,0x000,0x000,0x000,0x000,0x000,0x000,0x000,0x000
		};

		/// <summary>
		/// 10-bit mantissas of the attenuation-to-volume conversion (ymfm
		/// attenuation_to_volume), stored reversed and with the implied 0x400 bit, shifted
		/// left by 2 - built in the static constructor from these raw values.
		/// </summary>
		private static readonly ushort[] msPowerMantissas =
		{
			0x3fa,0x3f5,0x3ef,0x3ea,0x3e4,0x3df,0x3da,0x3d4,0x3cf,0x3c9,0x3c4,0x3bf,0x3b9,0x3b4,0x3ae,0x3a9,
			0x3a4,0x39f,0x399,0x394,0x38f,0x38a,0x384,0x37f,0x37a,0x375,0x370,0x36a,0x365,0x360,0x35b,0x356,
			0x351,0x34c,0x347,0x342,0x33d,0x338,0x333,0x32e,0x329,0x324,0x31f,0x31a,0x315,0x310,0x30b,0x306,
			0x302,0x2fd,0x2f8,0x2f3,0x2ee,0x2e9,0x2e5,0x2e0,0x2db,0x2d6,0x2d2,0x2cd,0x2c8,0x2c4,0x2bf,0x2ba,
			0x2b5,0x2b1,0x2ac,0x2a8,0x2a3,0x29e,0x29a,0x295,0x291,0x28c,0x288,0x283,0x27f,0x27a,0x276,0x271,
			0x26d,0x268,0x264,0x25f,0x25b,0x257,0x252,0x24e,0x249,0x245,0x241,0x23c,0x238,0x234,0x230,0x22b,
			0x227,0x223,0x21e,0x21a,0x216,0x212,0x20e,0x209,0x205,0x201,0x1fd,0x1f9,0x1f5,0x1f0,0x1ec,0x1e8,
			0x1e4,0x1e0,0x1dc,0x1d8,0x1d4,0x1d0,0x1cc,0x1c8,0x1c4,0x1c0,0x1bc,0x1b8,0x1b4,0x1b0,0x1ac,0x1a8,
			0x1a4,0x1a0,0x19c,0x199,0x195,0x191,0x18d,0x189,0x185,0x181,0x17e,0x17a,0x176,0x172,0x16f,0x16b,
			0x167,0x163,0x160,0x15c,0x158,0x154,0x151,0x14d,0x149,0x146,0x142,0x13e,0x13b,0x137,0x134,0x130,
			0x12c,0x129,0x125,0x122,0x11e,0x11b,0x117,0x114,0x110,0x10c,0x109,0x106,0x102,0x0ff,0x0fb,0x0f8,
			0x0f4,0x0f1,0x0ed,0x0ea,0x0e7,0x0e3,0x0e0,0x0dc,0x0d9,0x0d6,0x0d2,0x0cf,0x0cc,0x0c8,0x0c5,0x0c2,
			0x0be,0x0bb,0x0b8,0x0b5,0x0b1,0x0ae,0x0ab,0x0a8,0x0a4,0x0a1,0x09e,0x09b,0x098,0x094,0x091,0x08e,
			0x08b,0x088,0x085,0x082,0x07e,0x07b,0x078,0x075,0x072,0x06f,0x06c,0x069,0x066,0x063,0x060,0x05d,
			0x05a,0x057,0x054,0x051,0x04e,0x04b,0x048,0x045,0x042,0x03f,0x03c,0x039,0x036,0x033,0x030,0x02d,
			0x02a,0x028,0x025,0x022,0x01f,0x01c,0x019,0x016,0x014,0x011,0x00e,0x00b,0x008,0x006,0x003,0x000
		};

		private static readonly ushort[] msPowerTable = new ushort[256];

		/// <summary>Attenuation increments per 6-bit rate, 4 bits per step index
		/// (ymfm attenuation_increment).</summary>
		private static readonly uint[] msIncrementTable =
		{
			0x00000000, 0x00000000, 0x10101010, 0x10101010,  // 0-3    (0x00-0x03)
			0x10101010, 0x10101010, 0x11101110, 0x11101110,  // 4-7    (0x04-0x07)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 8-11   (0x08-0x0B)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 12-15  (0x0C-0x0F)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 16-19  (0x10-0x13)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 20-23  (0x14-0x17)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 24-27  (0x18-0x1B)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 28-31  (0x1C-0x1F)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 32-35  (0x20-0x23)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 36-39  (0x24-0x27)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 40-43  (0x28-0x2B)
			0x10101010, 0x10111010, 0x11101110, 0x11111110,  // 44-47  (0x2C-0x2F)
			0x11111111, 0x21112111, 0x21212121, 0x22212221,  // 48-51  (0x30-0x33)
			0x22222222, 0x42224222, 0x42424242, 0x44424442,  // 52-55  (0x34-0x37)
			0x44444444, 0x84448444, 0x84848484, 0x88848884,  // 56-59  (0x38-0x3B)
			0x88888888, 0x88888888, 0x88888888, 0x88888888   // 60-63  (0x3C-0x3F)
		};

		/// <summary>Key scale attenuation for the top 4 FNUM bits at block 7, in 0.75 dB
		/// units (ymfm opl_key_scale_atten).</summary>
		private static readonly byte[] msFnumToAtten = { 0, 24, 32, 37, 40, 43, 45, 47, 48, 50, 51, 52, 53, 54, 55, 56 };

		/// <summary>Vibrato scale per PM LFO chunk, as a 1.3 value (ymfm pm_scale).</summary>
		private static readonly int[] msPmScale = { 8, 4, 0, -4, -8, -4, 0, 4 };

		/// <summary>Fixed operator pairs per channel (ymfm operator_map for OPL/OPL2).</summary>
		private static readonly int[,] msChannelOperators =
		{
			{ 0, 3 }, { 1, 4 }, { 2, 5 }, { 6, 9 }, { 7, 10 }, { 8, 11 }, { 12, 15 }, { 13, 16 }, { 14, 17 }
		};

		/// <summary>The four OPL2 waveforms as 4.8 attenuation with the sign in bit 15.</summary>
		private static readonly ushort[,] msWaveforms = new ushort[WaveformCount, WaveformLength];

		static UWOpl2()
		{
			for (int liAt = 0; liAt < 256; liAt++)
				msPowerTable[liAt] = (ushort)((msPowerMantissas[liAt] | 0x400) << 2);

			for (uint liAt = 0; liAt < WaveformLength; liAt++)
				msWaveforms[0, liAt] = (ushort)(fAbsSinAttenuation(liAt) | (fBitfield(liAt, 9) << 15));

			ushort lyZero = msWaveforms[0, 0];

			for (uint liAt = 0; liAt < WaveformLength; liAt++)
			{
				ushort lySine = msWaveforms[0, liAt];
				msWaveforms[1, liAt] = fBitfield(liAt, 9) != 0 ? lyZero : lySine;
				msWaveforms[2, liAt] = (ushort)(lySine & 0x7fff);
				msWaveforms[3, liAt] = fBitfield(liAt, 8) != 0 ? lyZero : (ushort)(lySine & 0x7fff);
			}
		}

		/// <summary>Values computed once per register change (ymfm opdata_cache).</summary>
		private sealed class OperatorCache
		{
			public int Waveform;
			public uint PhaseStep;
			public uint TotalLevel;
			public uint BlockFreq;
			public uint Multiple;
			public uint EgSustain;
			public readonly byte[] EgRate = new byte[EgStates];
		}

		/// <summary>One operator ("slot"): phase, envelope and sine stage (ymfm fm_operator).</summary>
		private sealed class Operator
		{
			public readonly uint OpOffset;
			public uint ChOffset;
			public uint PhaseValue;
			public ushort EnvAttenuation = 0x3ff;
			public int EnvState = EgRelease;
			public byte KeyState;
			public byte KeyonLive;
			public readonly OperatorCache Cache = new OperatorCache();

			private readonly UWOpl2 mOChip;

			public Operator(UWOpl2 pOChip, uint piOpOffset)
			{
				mOChip = pOChip;
				OpOffset = piOpOffset;
			}

			public void Reset()
			{
				PhaseValue = 0;
				EnvAttenuation = 0x3ff;
				EnvState = EgRelease;
				KeyState = 0;
				KeyonLive = 0;
			}

			/// <summary>ymfm fm_operator::prepare - refresh the cache, clock the key state,
			/// return whether the operator still sounds.</summary>
			public bool Prepare()
			{
				mOChip.fCacheOperatorData(ChOffset, OpOffset, Cache);
				fClockKeystate(KeyonLive != 0 ? 1u : 0u);
				return EnvState != EgRelease || EnvAttenuation < EgQuiet;
			}

			public void Clock(uint piEnvCounter, int piLfoRawPm)
			{
				if (fBitfield(piEnvCounter, 0, 2) == 0)
					fClockEnvelope(piEnvCounter >> 2);

				uint liPhaseStep = Cache.PhaseStep;

				if (liPhaseStep == PhaseStepDynamic)
					liPhaseStep = mOChip.fComputePhaseStep(OpOffset, Cache, piLfoRawPm);

				PhaseValue += liPhaseStep;
			}

			public uint Phase
			{
				get { return PhaseValue >> 10; }
			}

			/// <summary>14-bit signed output for a phase and an AM offset
			/// (ymfm compute_volume).</summary>
			public int ComputeVolume(uint piPhase, uint piAmOffset)
			{
				if (EnvAttenuation > EgQuiet)
					return 0;

				uint liSinAttenuation = msWaveforms[Cache.Waveform, piPhase & (WaveformLength - 1)];
				uint liEnvAttenuation = fEnvelopeAttenuation(piAmOffset) << 2;
				int liResult = (int)fAttenuationToVolume((liSinAttenuation & 0x7fff) + liEnvAttenuation);

				return fBitfield(liSinAttenuation, 15) != 0 ? -liResult : liResult;
			}

			public void KeyOnOff(uint piOn, int piType)
			{
				KeyonLive = (byte)((KeyonLive & ~(1 << piType)) | ((int)fBitfield(piOn, 0) << piType));

				// Deviation from ymfm (see the class summary): a key-off takes effect at once,
				// so that key up and key down in the same sample still retrigger the note.
				if (KeyonLive == 0 && KeyState != 0)
					fClockKeystate(0);
			}

			private void fStartAttack()
			{
				if (EnvState == EgAttack)
					return;

				EnvState = EgAttack;
				PhaseValue = 0;

				if (Cache.EgRate[EgAttack] >= 62)
					EnvAttenuation = 0;
			}

			private void fStartRelease()
			{
				if (EnvState >= EgRelease)
					return;

				EnvState = EgRelease;
			}

			private void fClockKeystate(uint piKeystate)
			{
				if ((piKeystate ^ KeyState) != 0)
				{
					KeyState = (byte)piKeystate;

					if (piKeystate != 0)
						fStartAttack();
					else
						fStartRelease();
				}
			}

			private void fClockEnvelope(uint piEnvCounter)
			{
				if (EnvState == EgAttack && EnvAttenuation == 0)
					EnvState = EgDecay;

				// immediately after attack->decay, in case the sustain level is 0
				if (EnvState == EgDecay && EnvAttenuation >= Cache.EgSustain)
					EnvState = EgSustain;

				uint liRate = Cache.EgRate[EnvState];

				// shift the counter so that it becomes a 5.11 fixed point number
				int liRateShift = (int)(liRate >> 2);
				piEnvCounter <<= liRateShift;

				if (fBitfield(piEnvCounter, 0, 11) != 0)
					return;

				uint liRelevantBits = fBitfield(piEnvCounter, liRateShift <= 11 ? 11 : liRateShift, 3);
				uint liIncrement = fBitfield(msIncrementTable[liRate], (int)(4 * liRelevantBits), 4);

				if (EnvState == EgAttack)
				{
					// attack rates 62/63 do not increment when changed after the key-on
					if (liRate < 62)
						EnvAttenuation = (ushort)(EnvAttenuation + (unchecked((uint)~(int)EnvAttenuation * liIncrement) >> 4));
				}
				else
				{
					int liNew = EnvAttenuation + (int)liIncrement;
					EnvAttenuation = (ushort)(liNew >= 0x400 ? 0x3ff : liNew);
				}
			}

			private uint fEnvelopeAttenuation(uint piAmOffset)
			{
				uint liResult = EnvAttenuation;

				if (mOChip.fOpLfoAmEnable(OpOffset) != 0)
					liResult += piAmOffset;

				liResult += Cache.TotalLevel;

				return liResult < 0x3ff ? liResult : 0x3ff;
			}
		}

		/// <summary>A two-operator channel (ymfm fm_channel).</summary>
		private sealed class Channel
		{
			public readonly uint ChOffset;
			public readonly Operator[] Op = new Operator[2];

			private readonly short[] mOFeedback = new short[2];
			private short miFeedbackIn;
			private readonly UWOpl2 mOChip;

			public Channel(UWOpl2 pOChip, uint piChOffset)
			{
				mOChip = pOChip;
				ChOffset = piChOffset;
			}

			public void Reset()
			{
				mOFeedback[0] = mOFeedback[1] = 0;
				miFeedbackIn = 0;
			}

			public void KeyOnOff(uint piStates, int piType)
			{
				for (int liOp = 0; liOp < 2; liOp++)
					Op[liOp].KeyOnOff(fBitfield(piStates, liOp), piType);
			}

			public bool Prepare()
			{
				bool lbActive = false;

				for (int liOp = 0; liOp < 2; liOp++)
					if (Op[liOp].Prepare())
						lbActive = true;

				return lbActive;
			}

			public void Clock(uint piEnvCounter, int piLfoRawPm)
			{
				mOFeedback[0] = mOFeedback[1];
				mOFeedback[1] = miFeedbackIn;

				for (int liOp = 0; liOp < 2; liOp++)
					Op[liOp].Clock(piEnvCounter, piLfoRawPm);
			}

			private int fOperator1WithFeedback(uint piAmOffset)
			{
				int liOpMod = 0;
				uint liFeedback = mOChip.fChFeedback(ChOffset);

				if (liFeedback != 0)
					liOpMod = (mOFeedback[0] + mOFeedback[1]) >> (int)(10 - liFeedback);

				miFeedbackIn = (short)Op[0].ComputeVolume(unchecked(Op[0].Phase + (uint)liOpMod), piAmOffset);
				return miFeedbackIn;
			}

			/// <summary>ymfm output_2op with rshift and clipmax as for YM3812. OPL2 delays
			/// the modulator by one sample (MODULATOR_DELAY), so operator 2 is modulated by
			/// the previous operator 1 value.</summary>
			public int Output2Op(int piRShift, int piClipMax)
			{
				uint liAmOffset = mOChip.miLfoAm;
				fOperator1WithFeedback(liAmOffset);

				int liResult;

				if (fBitfield(mOChip.fChAlgorithm(ChOffset), 0) == 0)
				{
					int liOpMod = mOFeedback[1] >> 1;
					liResult = Op[1].ComputeVolume(unchecked(Op[1].Phase + (uint)liOpMod), liAmOffset) >> piRShift;
				}
				else
				{
					liResult = mOFeedback[1] >> piRShift;
					liResult += Op[1].ComputeVolume(Op[1].Phase, liAmOffset) >> piRShift;
					liResult = fClamp(liResult, -piClipMax - 1, piClipMax);
				}

				return liResult;
			}

			/// <summary>Rhythm mode channel 6: bass drum (ymfm output_rhythm_ch6).</summary>
			public int OutputRhythmCh6(int piRShift)
			{
				uint liAmOffset = mOChip.miLfoAm;
				int liOpOut1 = fOperator1WithFeedback(liAmOffset);
				int liOpMod = fBitfield(mOChip.fChAlgorithm(ChOffset), 0) != 0 ? 0 : (liOpOut1 >> 1);
				int liResult = Op[1].ComputeVolume(unchecked(Op[1].Phase + (uint)liOpMod), liAmOffset) >> piRShift;
				return liResult * 2;
			}

			/// <summary>Rhythm mode channel 7: high hat and snare drum (ymfm output_rhythm_ch7).</summary>
			public int OutputRhythmCh7(uint piPhaseSelect, int piRShift, int piClipMax)
			{
				uint liAmOffset = mOChip.miLfoAm;
				uint liNoiseState = fBitfield(mOChip.miNoiseLfsr >> 23, 0);

				uint liPhase = (piPhaseSelect << 9) | (0xd0u >> (int)(2 * (liNoiseState ^ piPhaseSelect)));
				int liResult = Op[0].ComputeVolume(liPhase, liAmOffset) >> piRShift;

				uint liOp13Phase = Op[0].Phase;
				liPhase = (0x100u << (int)fBitfield(liOp13Phase, 8)) ^ (liNoiseState << 8);
				liResult += Op[1].ComputeVolume(liPhase, liAmOffset) >> piRShift;
				liResult = fClamp(liResult, -piClipMax - 1, piClipMax);

				return liResult * 2;
			}

			/// <summary>Rhythm mode channel 8: tom tom and top cymbal (ymfm output_rhythm_ch8).</summary>
			public int OutputRhythmCh8(uint piPhaseSelect, int piRShift, int piClipMax)
			{
				uint liAmOffset = mOChip.miLfoAm;
				int liResult = Op[0].ComputeVolume(Op[0].Phase, liAmOffset) >> piRShift;

				uint liPhase = 0x100u | (piPhaseSelect << 9);
				liResult += Op[1].ComputeVolume(liPhase, liAmOffset) >> piRShift;
				liResult = fClamp(liResult, -piClipMax - 1, piClipMax);

				return liResult * 2;
			}
		}

		private readonly byte[] myRegisters = new byte[RegisterCount];
		private readonly Channel[] mOChannels = new Channel[ChannelCount];
		private readonly Operator[] mOOperators = new Operator[OperatorCount];

		private uint miEnvCounter;
		private uint miActiveChannels = AllChannels;
		private uint miModifiedChannels = AllChannels;
		private uint miPrepareCount;

		private ushort miLfoAmCounter;
		private ushort miLfoPmCounter;
		private uint miNoiseLfsr = 1;
		private byte miLfoAm;

		public UWOpl2()
		{
			for (int liOp = 0; liOp < OperatorCount; liOp++)
				mOOperators[liOp] = new Operator(this, (uint)(liOp + 2 * (liOp / 6)));

			for (int liCh = 0; liCh < ChannelCount; liCh++)
			{
				Channel lOChannel = new Channel(this, (uint)liCh);

				for (int liIndex = 0; liIndex < 2; liIndex++)
				{
					Operator lOOp = mOOperators[msChannelOperators[liCh, liIndex]];
					lOOp.ChOffset = (uint)liCh;
					lOChannel.Op[liIndex] = lOOp;
				}

				mOChannels[liCh] = lOChannel;
			}

			// ymfm leaves the register array uninitialised until reset(); start clean.
			Reset();
		}

		/// <summary>Back to the start: all registers zero, all notes off
		/// (ymfm fm_engine_base::reset; the LFO, noise and envelope counters keep running
		/// as in ymfm).</summary>
		public void Reset()
		{
			System.Array.Clear(myRegisters, 0, RegisterCount);
			miModifiedChannels = AllChannels;

			foreach (Channel lOChannel in mOChannels)
				lOChannel.Reset();

			foreach (Operator lOOp in mOOperators)
				lOOp.Reset();
		}

		/// <summary>A register write, as the original does it via port 0x388/0x389
		/// (ymfm ym3812::write_data -> fm_engine_base::write -> opl_registers_base::write).</summary>
		public void WriteRegister(int piAddress, int piValue)
		{
			int liIndex = piAddress & 0xFF;
			byte lyData = (byte)(piValue & 0xFF);

			miModifiedChannels = AllChannels;

			// writes to the mode register with the high bit set ignore the low bits
			if (liIndex == RegMode && fBitfield(lyData, 7) != 0)
				myRegisters[liIndex] |= 0x80;
			else
				myRegisters[liIndex] = lyData;

			if (liIndex == 0xbd)
			{
				uint liOpMask = fBitfield(lyData, 5) != 0 ? fBitfield(lyData, 0, 5) : 0;
				mOChannels[6].KeyOnOff(fBitfield(liOpMask, 4) != 0 ? 3u : 0u, KeyonRhythm);
				mOChannels[7].KeyOnOff(fBitfield(liOpMask, 0) | (fBitfield(liOpMask, 3) << 1), KeyonRhythm);
				mOChannels[8].KeyOnOff(fBitfield(liOpMask, 2) | (fBitfield(liOpMask, 1) << 1), KeyonRhythm);
				return;
			}

			if ((liIndex & 0xf0) == 0xb0)
			{
				int liChannel = liIndex & 0x0f;

				if (liChannel < ChannelCount)
					mOChannels[liChannel].KeyOnOff(fBitfield(lyData, 5) != 0 ? 15u : 0u, KeyonNormal);
			}
		}

		/// <summary>Fills the buffer with mono samples at 49716 Hz.</summary>
		public void Generate(short[] pyBuffer, int piCount)
		{
			for (int liAt = 0; liAt < piCount; liAt++)
				pyBuffer[liAt] = GenerateSample();
		}

		/// <summary>One sample, all nine channels mixed (ymfm ym3812::generate).</summary>
		public short GenerateSample()
		{
			fClock();
			int liOutput = fOutput(1, 32767);

			// the YM3014 DAC receives a mantissa/exponent value; simulate the truncation
			return fRoundtripFp(liOutput);
		}

		/// <summary>ymfm fm_engine_base::clock.</summary>
		private void fClock()
		{
			// prepare when something was modified, and every 4096 samples to catch ending notes
			if (miModifiedChannels != 0 || miPrepareCount++ >= 4096)
			{
				miActiveChannels = 0;

				for (int liCh = 0; liCh < ChannelCount; liCh++)
					if (mOChannels[liCh].Prepare())
						miActiveChannels |= 1u << liCh;

				miModifiedChannels = 0;
				miPrepareCount = 0;
			}

			// the OPL envelope clock divider is 1
			miEnvCounter += 4;

			int liLfoRawPm = fClockNoiseAndLfo();

			for (int liCh = 0; liCh < ChannelCount; liCh++)
				mOChannels[liCh].Clock(miEnvCounter, liLfoRawPm);
		}

		/// <summary>ymfm fm_engine_base::output for one output.</summary>
		private int fOutput(int piRShift, int piClipMax)
		{
			int liSum = 0;
			uint liChanMask = AllChannels & miActiveChannels;

			if (fRhythmEnable() != 0)
			{
				uint liOp13Phase = mOOperators[13].Phase;
				uint liOp17Phase = mOOperators[17].Phase;
				uint liPhaseSelect = (fBitfield(liOp13Phase, 2) ^ fBitfield(liOp13Phase, 7)) | fBitfield(liOp13Phase, 3)
					| (fBitfield(liOp17Phase, 5) ^ fBitfield(liOp17Phase, 3));

				for (int liCh = 0; liCh < ChannelCount; liCh++)
				{
					if (fBitfield(liChanMask, liCh) == 0)
						continue;

					if (liCh == 6)
						liSum += mOChannels[liCh].OutputRhythmCh6(piRShift);
					else if (liCh == 7)
						liSum += mOChannels[liCh].OutputRhythmCh7(liPhaseSelect, piRShift, piClipMax);
					else if (liCh == 8)
						liSum += mOChannels[liCh].OutputRhythmCh8(liPhaseSelect, piRShift, piClipMax);
					else
						liSum += mOChannels[liCh].Output2Op(piRShift, piClipMax);
				}
			}
			else
			{
				for (int liCh = 0; liCh < ChannelCount; liCh++)
					if (fBitfield(liChanMask, liCh) != 0)
						liSum += mOChannels[liCh].Output2Op(piRShift, piClipMax);
			}

			return liSum;
		}

		/// <summary>Noise LFSR and the two fixed-frequency LFOs; returns the raw PM value
		/// (ymfm opl_clock_noise_and_lfo).</summary>
		private int fClockNoiseAndLfo()
		{
			// 23-bit noise generator for the rhythm section
			miNoiseLfsr <<= 1;
			miNoiseLfsr |= fBitfield(miNoiseLfsr, 23) ^ fBitfield(miNoiseLfsr, 9) ^ fBitfield(miNoiseLfsr, 8) ^ fBitfield(miNoiseLfsr, 1);

			// AM LFO: 210*64 steps, a triangle; depth 0 divides by 2, depth 1 multiplies by 2
			uint liAmCounter = miLfoAmCounter++;

			if (liAmCounter >= 210 * 64 - 1)
				miLfoAmCounter = 0;

			int liShift = 9 - 2 * (int)fLfoAmDepth();
			miLfoAm = (byte)(((liAmCounter < 105 * 64) ? liAmCounter : (210 * 64 + 63 - liAmCounter)) >> liShift);

			// PM LFO: 8192 steps in 8 chunks of 1024
			uint liPmCounter = miLfoPmCounter++;
			return msPmScale[fBitfield(liPmCounter, 10, 3)] >> (int)(fLfoPmDepth() ^ 1);
		}

		/// <summary>ymfm opl_registers_base::cache_operator_data.</summary>
		private void fCacheOperatorData(uint piChOffset, uint piOpOffset, OperatorCache pOCache)
		{
			pOCache.Waveform = (int)(fOpWaveform(piOpOffset) % WaveformCount);

			uint liBlockFreq = pOCache.BlockFreq = fChBlockFreq(piChOffset);

			// 4-bit keycode: the block plus one of the two top FNUM bits (by note select,
			// reversed from what the manual says)
			uint liKeycode = fBitfield(liBlockFreq, 10, 3) << 1;
			liKeycode |= fBitfield(liBlockFreq, 9 - (int)fNoteSelect(), 1);

			// multiple as an x.1 value; 0 means 0.5, table 0,1,2,3,4,5,6,7,8,9,10,10,12,12,15,15
			uint liMultiple = fOpMultiple(piOpOffset);
			pOCache.Multiple = ((liMultiple & 0xe) | fBitfield(0xc2aa, (int)liMultiple)) * 2;

			if (pOCache.Multiple == 0)
				pOCache.Multiple = 1;

			if (fOpLfoPmEnable(piOpOffset) == 0)
				pOCache.PhaseStep = fComputePhaseStep(piOpOffset, pOCache, 0);
			else
				pOCache.PhaseStep = PhaseStepDynamic;

			pOCache.TotalLevel = fOpTotalLevel(piOpOffset) << 3;

			uint liKsl = fOpKsl(piOpOffset);

			if (liKsl != 0)
				pOCache.TotalLevel += fKeyScaleAtten(fBitfield(liBlockFreq, 10, 3), fBitfield(liBlockFreq, 6, 4)) << (int)liKsl;

			// 4-bit sustain level, but 15 means 31
			pOCache.EgSustain = fOpSustainLevel(piOpOffset);
			pOCache.EgSustain |= (pOCache.EgSustain + 1) & 0x10;
			pOCache.EgSustain <<= 5;

			uint liKsrVal = liKeycode >> (int)(2 * (fOpKsr(piOpOffset) ^ 1));
			pOCache.EgRate[EgAttack] = fEffectiveRate(fOpAttackRate(piOpOffset) * 4, liKsrVal);
			pOCache.EgRate[EgDecay] = fEffectiveRate(fOpDecayRate(piOpOffset) * 4, liKsrVal);
			pOCache.EgRate[EgSustain] = fOpEgSustain(piOpOffset) != 0 ? (byte)0 : fEffectiveRate(fOpReleaseRate(piOpOffset) * 4, liKsrVal);
			pOCache.EgRate[EgRelease] = fEffectiveRate(fOpReleaseRate(piOpOffset) * 4, liKsrVal);
			pOCache.EgRate[EgDepress] = 0x3f;
		}

		/// <summary>ymfm opl_compute_phase_step.</summary>
		private uint fComputePhaseStep(uint piOpOffset, OperatorCache pOCache, int piLfoRawPm)
		{
			int liLfoRawPm = fOpLfoPmEnable(piOpOffset) != 0 ? piLfoRawPm : 0;
			uint liBlockFreq = pOCache.BlockFreq;

			// frequency number as a 12-bit fraction, with the PM adjustment from its top bits
			uint liFnum = fBitfield(liBlockFreq, 0, 10) << 2;
			liFnum = unchecked(liFnum + (((uint)liLfoRawPm * fBitfield(liBlockFreq, 7, 3)) >> 1));
			liFnum &= 0xfff;

			int liBlock = (int)fBitfield(liBlockFreq, 10, 3);
			uint liPhaseStep = (liFnum << liBlock) >> 2;

			return (liPhaseStep * pOCache.Multiple) >> 1;
		}

		private static byte fEffectiveRate(uint piRawRate, uint piKsr)
		{
			return piRawRate == 0 ? (byte)0 : (byte)(piRawRate + piKsr < 63 ? piRawRate + piKsr : 63);
		}

		private static uint fKeyScaleAtten(uint piBlock, uint piFnum4Msb)
		{
			int liResult = msFnumToAtten[piFnum4Msb] - 8 * (int)(piBlock ^ 7);
			return liResult > 0 ? (uint)liResult : 0;
		}

		private static uint fAbsSinAttenuation(uint piInput)
		{
			// the second half of the curve is a mirror image
			if (fBitfield(piInput, 8) != 0)
				piInput = ~piInput;

			return msSinTable[piInput & 0xff];
		}

		/// <summary>5.8 logarithmic attenuation to 13-bit linear volume.</summary>
		private static uint fAttenuationToVolume(uint piInput)
		{
			int liShift = (int)(piInput >> 8);
			return liShift < 32 ? (uint)msPowerTable[piInput & 0xff] >> liShift : 0;
		}

		/// <summary>ymfm roundtrip_fp: the value after the YM3014 floating-point
		/// encode/decode.</summary>
		private static short fRoundtripFp(int piValue)
		{
			if (piValue < -32768)
				return -32768;

			if (piValue > 32767)
				return 32767;

			int liScanValue = piValue ^ (piValue >> 31);
			int liExponent = 7 - fCountLeadingZeros(unchecked((uint)(liScanValue << 17)));

			if (liExponent < 1)
				liExponent = 1;

			liExponent -= 1;
			int liMask = (1 << liExponent) - 1;
			return (short)(piValue & ~liMask);
		}

		private static int fCountLeadingZeros(uint piValue)
		{
			if (piValue == 0)
				return 32;

			int liCount = 0;

			while ((piValue & 0x80000000u) == 0)
			{
				piValue <<= 1;
				liCount++;
			}

			return liCount;
		}

		private static uint fBitfield(uint piValue, int piStart, int piLength = 1)
		{
			return (piValue >> piStart) & ((1u << piLength) - 1);
		}

		private static int fClamp(int piValue, int piMin, int piMax)
		{
			return piValue < piMin ? piMin : (piValue > piMax ? piMax : piValue);
		}

		// register accessors (ymfm opl_registers_base, OPL2 variant)
		private uint fByte(uint piOffset, int piStart, int piCount, uint piExtraOffset = 0)
		{
			return fBitfield(myRegisters[piOffset + piExtraOffset], piStart, piCount);
		}

		private uint fWaveformEnable() { return fByte(0x01, 5, 1); }
		private uint fNoteSelect() { return fByte(0x08, 6, 1); }
		private uint fLfoAmDepth() { return fByte(0xbd, 7, 1); }
		private uint fLfoPmDepth() { return fByte(0xbd, 6, 1); }
		private uint fRhythmEnable() { return fByte(0xbd, 5, 1); }
		private uint fChBlockFreq(uint piChOffset) { return (fByte(0xb0, 0, 5, piChOffset) << 8) | fByte(0xa0, 0, 8, piChOffset); }
		private uint fChFeedback(uint piChOffset) { return fByte(0xc0, 1, 3, piChOffset); }
		private uint fChAlgorithm(uint piChOffset) { return fByte(0xc0, 0, 1, piChOffset); }
		private uint fOpLfoAmEnable(uint piOpOffset) { return fByte(0x20, 7, 1, piOpOffset); }
		private uint fOpLfoPmEnable(uint piOpOffset) { return fByte(0x20, 6, 1, piOpOffset); }
		private uint fOpEgSustain(uint piOpOffset) { return fByte(0x20, 5, 1, piOpOffset); }
		private uint fOpKsr(uint piOpOffset) { return fByte(0x20, 4, 1, piOpOffset); }
		private uint fOpMultiple(uint piOpOffset) { return fByte(0x20, 0, 4, piOpOffset); }
		private uint fOpKsl(uint piOpOffset) { uint liTemp = fByte(0x40, 6, 2, piOpOffset); return fBitfield(liTemp, 1) | (fBitfield(liTemp, 0) << 1); }
		private uint fOpTotalLevel(uint piOpOffset) { return fByte(0x40, 0, 6, piOpOffset); }
		private uint fOpAttackRate(uint piOpOffset) { return fByte(0x60, 4, 4, piOpOffset); }
		private uint fOpDecayRate(uint piOpOffset) { return fByte(0x60, 0, 4, piOpOffset); }
		private uint fOpSustainLevel(uint piOpOffset) { return fByte(0x80, 4, 4, piOpOffset); }
		private uint fOpReleaseRate(uint piOpOffset) { return fByte(0x80, 0, 4, piOpOffset); }
		private uint fOpWaveform(uint piOpOffset) { return fWaveformEnable() != 0 ? fByte(0xe0, 0, 2, piOpOffset) : 0; }
	}
}
