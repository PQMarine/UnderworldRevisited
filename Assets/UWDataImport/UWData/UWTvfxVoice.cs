namespace UWDataImport.UWData
{
	/// <summary>
	/// One voice of the TVFX effect engine ("time-varying effects") of the Miles AdLib driver.
	///
	/// This is how the game's sound effects sound: not a sample, but a small program per
	/// effect (UW.AD, bank 1) that updates eight parameters of an OPL2 channel sixty times per
	/// second - pitch, two levels, priority, feedback, two multipliers,
	/// waveform. Each parameter has its own bytecode stream of steps (counter,
	/// increment per tick), set commands (FFFF value, FFFE base register) and jumps (0000
	/// offset). On key-on the key-on stream runs, after the program's duration the
	/// release stream, until both levels have fallen below 0x400.
	///
	/// Rebuilt after the reference (UnderworldGodot, TvfxVoice.cs), including its corrections:
	/// the stream offsets lie two bytes behind the header value, jumps are signed,
	/// the change flags are ORed together, the release end counts unsigned.
	///
	/// Driver constants are checked against the AdLib driver shipped with the game,
	/// SOUND\ADLIB.ADV (AIL 2.0 real-mode driver, "Copyright (C) 1991 John Miles", code
	/// offsets = file offsets; SBFM/SBPFM/PASFM.ADV contain the same code), 2026-09-15:
	/// velocity table at 0x0DD5 (looked up with velocity >> 3 at 0x261A), release clamp at
	/// 0x0739 (level 1) and 0x0794 (level 2), release end below 0x400 at 0x086C, volume
	/// composite at 0x207E. (The AIL 2.0 source was also released by John Miles on
	/// 2000-05-26 as "open-source freeware, usable by anyone for any purpose".)
	/// </summary>
	public class UWTvfxVoice
	{
		public enum PhaseEnum
		{
			Idle,
			KeyOn,
			Release
		}

		/// <summary>
		/// The velocity curve of the Miles driver (ADLIB.ADV file offset 0x0DD5, sixteen values,
		/// identical in the other FM drivers): the upper four bits of a velocity become a volume
		/// factor between 82 and 127.
		/// Completely quiet does not exist - silence comes from distance alone.
		/// </summary>
		public static readonly byte[] VelocityGraph =
		{
			82, 85, 88, 91, 94, 97, 100, 103, 106, 109, 112, 115, 118, 121, 124, 127
		};

		/// <summary>The factor for a velocity plus offset, both 0 to 127.</summary>
		public static byte ComputeVolumeScale(int piBaseVelocity, int piVelocityOffset)
		{
			int liEffective = piBaseVelocity + piVelocityOffset;

			if (liEffective < 0)
				liEffective = 0;

			if (liEffective > 0x7F)
				liEffective = 0x7F;

			return VelocityGraph[liEffective >> 3];
		}

		private static readonly int[] msModulatorOffsets = { 0x00, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x10, 0x11, 0x12 };

		private static readonly int[] msCarrierOffsets = { 0x03, 0x04, 0x05, 0x0B, 0x0C, 0x0D, 0x13, 0x14, 0x15 };

		public int Channel { get; private set; }

		public PhaseEnum Phase { get; private set; } = PhaseEnum.Idle;

		public UWAdlibBank.TvfxPatch Patch { get; private set; }

		private readonly ushort[] miAccumulator = new ushort[8];

		private readonly ushort[] miCounter = new ushort[8];

		private readonly short[] miIncrement = new short[8];

		private readonly int[] miCursorWord = new int[8];

		private byte myBlockBase;

		private byte myKslModulator, myKslCarrier;

		private byte myFeedbackBase;

		private byte myAvekmModulatorBase, myAvekmCarrierBase;

		private byte myAdModulator, mySrModulator, myAdCarrier, mySrCarrier;

		private int miUpdateMask;

		private bool mbEnvelopeDirty;

		private int miPhaseTicks;

		private int miLifetimeTicks;

		private byte myVolumeScale = 127;

		public UWTvfxVoice(int piChannel)
		{
			Channel = piChannel;
		}

		/// <summary>
		/// Starts the program on this voice. piLifetimeTicks in sixtieths of a second, -1 for
		/// unlimited (the sound then ends through its own program).
		/// </summary>
		public void StartKeyOn(UWAdlibBank.TvfxPatch pOPatch, int piLifetimeTicks, byte pyVolumeScale)
		{
			Patch = pOPatch;
			Phase = PhaseEnum.KeyOn;
			miPhaseTicks = 0;
			miLifetimeTicks = piLifetimeTicks;
			myVolumeScale = pyVolumeScale;

			for (int liAt = 0; liAt < 8; liAt++)
			{
				miAccumulator[liAt] = pOPatch.Parameters[liAt].InitialValue;
				miCounter[liAt] = 1;
				miIncrement[liAt] = 0;
				miCursorWord[liAt] = (pOPatch.Parameters[liAt].KeyOnOffset / 2) + 1;
			}

			fResetBases();

			myAdModulator = pOPatch.KeyOnAdModulator;
			mySrModulator = pOPatch.KeyOnSrModulator;
			myAdCarrier = pOPatch.KeyOnAdCarrier;
			mySrCarrier = pOPatch.KeyOnSrCarrier;
			mbEnvelopeDirty = true;
			miUpdateMask = 0xFF;
		}

		private void fResetBases()
		{
			myBlockBase = Patch.Type == UWAdlibBank.TvfxType.TvInstrument ? (byte)0x20 : (byte)0x28;
			myKslModulator = myKslCarrier = 0;
			myFeedbackBase = 0;
			myAvekmModulatorBase = myAvekmCarrierBase = 0x20;
		}

		/// <summary>One sixtieth further: advance all eight streams, switch phases.</summary>
		public void ServiceTick()
		{
			if (Phase == PhaseEnum.Idle || Patch == null)
				return;

			for (int liAt = 0; liAt < 8; liAt++)
			{
				bool lbChanged = false;

				if (miIncrement[liAt] != 0)
				{
					ushort lyPrevious = miAccumulator[liAt];
					ushort lyNext = (ushort)(lyPrevious + (ushort)miIncrement[liAt]);

					// During release a level must not wrap around below zero: if the
					// top bit flips and the new value lies on the same side as the increment,
					// it stays at zero (ADLIB.ADV 0x0739 / 0x0794).
					if (Phase == PhaseEnum.Release && (liAt == 1 || liAt == 2))
					{
						int liFlipped = (lyPrevious ^ lyNext) & 0x8000;
						int liSameSide = (lyNext ^ (ushort)miIncrement[liAt]) & 0x8000;

						if (liFlipped != 0 && liSameSide == 0)
							lyNext = 0;
					}

					miAccumulator[liAt] = lyNext;
					lbChanged = true;
				}

				if (miCounter[liAt] > 0)
					miCounter[liAt]--;

				if (miCounter[liAt] == 0)
				{
					fAdvanceSegment(liAt);
					lbChanged = true;
				}

				if (lbChanged)
					miUpdateMask |= 1 << liAt;
			}

			miPhaseTicks++;

			// After duration plus one ticks the key goes up; a TV instrument holds until
			// someone cuts it off.
			int liTrigger = Patch.Type == UWAdlibBank.TvfxType.TvInstrument ? 0x10000 : Patch.Duration + 1;

			if (Phase == PhaseEnum.KeyOn && miPhaseTicks >= liTrigger)
				fEnterRelease();

			if (Phase == PhaseEnum.Release && miPhaseTicks > 0
				&& miAccumulator[1] < 0x400 && miAccumulator[2] < 0x400)
				Phase = PhaseEnum.Idle;

			if (miLifetimeTicks > 0)
			{
				miLifetimeTicks--;

				if (miLifetimeTicks == 0)
					Phase = PhaseEnum.Idle;
			}
		}

		/// <summary>
		/// Cuts a HELD sound off: the original keeps the water sound going while one is in it
		/// and ends it by hand when one leaves (SurfaceFootsteps_seg034_2F89_713 keeps the
		/// handle in dseg_5c99_77E and gives it back through seg014_1DC5_BFD). Without this a
		/// voice can only end by its own program or by its lifetime.
		/// </summary>
		public void Release()
		{
			if (Phase == PhaseEnum.KeyOn)
				fEnterRelease();
		}

		private void fEnterRelease()
		{
			Phase = PhaseEnum.Release;
			miPhaseTicks = 0;

			for (int liAt = 0; liAt < 8; liAt++)
			{
				miCursorWord[liAt] = (Patch.Parameters[liAt].ReleaseOffset / 2) + 1;
				miCounter[liAt] = 1;
				miIncrement[liAt] = 0;
			}

			fResetBases();

			myAdModulator = Patch.ReleaseAdModulator;
			mySrModulator = Patch.ReleaseSrModulator;
			myAdCarrier = Patch.ReleaseAdCarrier;
			mySrCarrier = Patch.ReleaseSrCarrier;
			mbEnvelopeDirty = true;
			miUpdateMask = 0xFF;
		}

		/// <summary>Reads the stream of one parameter up to the next step (at most ten
		/// entries).</summary>
		private void fAdvanceSegment(int piIndex)
		{
			byte[] lyRaw = Patch.Raw;

			for (int liIteration = 0; liIteration < 10; liIteration++)
			{
				int liBytePos = miCursorWord[piIndex] * 2;

				if (liBytePos < 0 || liBytePos + 4 > lyRaw.Length)
				{
					miCounter[piIndex] = 0xFFFF;
					miIncrement[piIndex] = 0;
					return;
				}

				int liWord0 = lyRaw[liBytePos] | (lyRaw[liBytePos + 1] << 8);
				int liWord1 = lyRaw[liBytePos + 2] | (lyRaw[liBytePos + 3] << 8);

				if (liWord0 == 0x0000)
				{
					// Jump by a signed byte offset.
					miCursorWord[piIndex] += ((short)liWord1) / 2;
					continue;
				}

				miCursorWord[piIndex] += 2;

				if (liWord0 == 0xFFFF)
				{
					miAccumulator[piIndex] = (ushort)liWord1;
					continue;
				}

				if (liWord0 == 0xFFFE)
				{
					fApplySetBase(piIndex, liWord1);
					continue;
				}

				miCounter[piIndex] = (ushort)liWord0;
				miIncrement[piIndex] = (short)liWord1;
				return;
			}

			miCounter[piIndex] = 0xFFFF;
			miIncrement[piIndex] = 0;
		}

		private void fApplySetBase(int piIndex, int piData)
		{
			switch (piIndex)
			{
				case 0:
				{
					byte lyBlock = (byte)((piData >> 8) & 0xFF);

					if (Patch.Type == UWAdlibBank.TvfxType.TvInstrument)
						lyBlock &= 0xE0;

					myBlockBase = lyBlock;
					break;
				}

				case 1: myKslModulator = (byte)(piData & 0xFF); break;
				case 2: myKslCarrier = (byte)(piData & 0xFF); break;
				case 4: myFeedbackBase = (byte)((piData >> 8) & 0xFF); break;
				case 5: myAvekmModulatorBase = (byte)(piData & 0xFF); break;
				case 6: myAvekmCarrierBase = (byte)(piData & 0xFF); break;
			}
		}

		/// <summary>
		/// Writes whatever has changed since last time to the chip. If the voice has
		/// just ended, the key goes up - otherwise the note would sound forever.
		/// </summary>
		public void EmitRegisters(UWOpl2 pOChip)
		{
			int liModulator = msModulatorOffsets[Channel];
			int liCarrier = msCarrierOffsets[Channel];

			if (mbEnvelopeDirty)
			{
				pOChip.WriteRegister(0x60 + liModulator, myAdModulator);
				pOChip.WriteRegister(0x60 + liCarrier, myAdCarrier);
				pOChip.WriteRegister(0x80 + liModulator, mySrModulator);
				pOChip.WriteRegister(0x80 + liCarrier, mySrCarrier);
				mbEnvelopeDirty = false;
			}

			if ((miUpdateMask & (1 << 5)) != 0)
				pOChip.WriteRegister(0x20 + liModulator, (miAccumulator[5] >> 12) | myAvekmModulatorBase);

			if ((miUpdateMask & (1 << 6)) != 0)
				pOChip.WriteRegister(0x20 + liCarrier, (miAccumulator[6] >> 12) | myAvekmCarrierBase);

			if ((miUpdateMask & (1 << 1)) != 0)
			{
				// AN ADDITIVE VOICE IS HEARD THROUGH BOTH OPERATORS, so the modulator has to be
				// scaled by the volume as well - otherwise it stays at full level and the effect
				// hardly gets quieter. The driver does exactly this (ADLIB.ADV 0x207E), and our
				// music path has done it since it was written (UWAdlibMusicDriver.fApplyVolume
				// with IsAdditive); only the effects path scaled the carrier alone (2026-09-16).
				// With FM connection the modulator only bends the carrier and its level is a
				// timbre, not a volume - it stays untouched.
				int liLevelIn = (miAccumulator[1] >> 10) & 0x3F;

				if ((myFeedbackBase & 1) != 0)
					liLevelIn = (liLevelIn * myVolumeScale) / 127;

				pOChip.WriteRegister(0x40 + liModulator, ((~liLevelIn) & 0x3F) | myKslModulator);
			}

			if ((miUpdateMask & (1 << 2)) != 0)
			{
				// The carrier level is scaled linearly by the volume factor and then converted
				// back into the chip.s attenuation (ADLIB.ADV 0x207E). Channel volume and
				// expression do not come in here: an effect is started directly and has no
				// controllers - the music path composes them (UWAdlibMusicDriver.fApplyVolume).
				int liVolumeIn = (miAccumulator[2] >> 10) & 0x3F;
				int liScaled = (liVolumeIn * myVolumeScale) / 127;
				int liTl = (~liScaled) & 0x3F;

				pOChip.WriteRegister(0x40 + liCarrier, liTl | myKslCarrier);
			}

			if ((miUpdateMask & (1 << 4)) != 0)
				pOChip.WriteRegister(0xC0 + Channel, ((miAccumulator[4] >> 12) & 0x0E) | (myFeedbackBase & 1));

			if ((miUpdateMask & (1 << 7)) != 0)
			{
				pOChip.WriteRegister(0xE0 + liModulator, (miAccumulator[7] >> 8) & 0x07);
				pOChip.WriteRegister(0xE0 + liCarrier, miAccumulator[7] & 0x07);
			}

			bool lbFrequency = (miUpdateMask & 1) != 0 || Phase == PhaseEnum.Idle;

			if (lbFrequency)
			{
				int liFrequency = miAccumulator[0] >> 6;

				pOChip.WriteRegister(0xA0 + Channel, liFrequency & 0xFF);

				int liB0 = (liFrequency >> 8) | myBlockBase;

				if (Phase == PhaseEnum.Idle)
					liB0 &= ~0x20;

				pOChip.WriteRegister(0xB0 + Channel, liB0);
			}

			miUpdateMask = 0;
		}
	}
}
