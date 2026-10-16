using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Plays MIDI events on the OPL2 - what the AdLib driver of the
	/// Miles library does for the game's music: nine voices, one per chip channel,
	/// the timbres from UW.AD, channel 10 is the percussion from bank 127.
	///
	/// VOLUME as in the Miles driver: the velocity goes through the sixteen-step
	/// curve (UWTvfxVoice.VelocityGraph), plus channel volume and expression, and the
	/// timbre's carrier level is scaled linearly by that and then converted back into the
	/// chip's attenuation. For additive timbres the modulator likewise.
	///
	/// PITCH via the chip's frequency number: fnum = f * 2^(20 - block) / 49716. The
	/// block is chosen so that the number stays below 1024. Pitch bend spans
	/// two semitones, as usual.
	///
	/// Voices are allocated in order; if all are busy, the oldest gives way.
	/// </summary>
	public class UWAdlibMusicDriver : IUWMidiDriver
	{
		public const int VoiceCount = UWOpl2.ChannelCount;

		private const int DrumChannel = 9;

		private const int BendSemitones = 2;

		private static readonly int[] msModulatorOffsets = { 0x00, 0x01, 0x02, 0x08, 0x09, 0x0A, 0x10, 0x11, 0x12 };

		private static readonly int[] msCarrierOffsets = { 0x03, 0x04, 0x05, 0x0B, 0x0C, 0x0D, 0x13, 0x14, 0x15 };

		private class Voice
		{
			public bool Active;

			public int MidiChannel = -1;

			public int Note;

			public double Pitch;

			public int Velocity;

			public bool Held;

			public long Started;

			public UWAdlibBank.MelodicPatch Patch;
		}

		private class MidiChannel
		{
			public int Program;

			public int Volume = 127;

			public int Expression = 127;

			public int Bend;

			public bool Sustain;
		}

		private readonly UWOpl2 mOChip;

		private readonly UWAdlibBank mOBank;

		private readonly Voice[] mOVoices = new Voice[VoiceCount];

		private readonly MidiChannel[] mOChannels = new MidiChannel[16];

		private long miClock;

		/// <summary>Master volume 0 to 127, acts like an additional expression controller.</summary>
		public int MasterVolume { get; set; } = 127;

		public UWAdlibMusicDriver(UWOpl2 pOChip, UWAdlibBank pOBank)
		{
			mOChip = pOChip;
			mOBank = pOBank;

			for (int liAt = 0; liAt < VoiceCount; liAt++)
				mOVoices[liAt] = new Voice();

			for (int liAt = 0; liAt < mOChannels.Length; liAt++)
				mOChannels[liAt] = new MidiChannel();

			Reset();
		}

		/// <summary>All notes off, channels reset, chip to its initial state: waveforms
		/// enabled, deep tremolo and vibrato like the Miles driver.</summary>
		public void Reset()
		{
			mOChip.Reset();
			mOChip.WriteRegister(0x01, 0x20);
			mOChip.WriteRegister(0xBD, 0xC0);

			foreach (Voice lOVoice in mOVoices)
			{
				lOVoice.Active = false;
				lOVoice.Held = false;
				lOVoice.MidiChannel = -1;
			}

			foreach (MidiChannel lOChannel in mOChannels)
			{
				lOChannel.Program = 0;
				lOChannel.Volume = 127;
				lOChannel.Expression = 127;
				lOChannel.Bend = 0;
				lOChannel.Sustain = false;
			}
		}

		public void AllNotesOff()
		{
			for (int liAt = 0; liAt < VoiceCount; liAt++)
			{
				if (mOVoices[liAt].Active)
					fKeyOff(liAt);
			}
		}

		/// <summary>One MIDI event from the sequencer.</summary>
		public void Handle(int piStatus, int piData1, int piData2)
		{
			int liChannel = piStatus & 0x0F;

			switch (piStatus & 0xF0)
			{
				case 0x90:
					if (piData2 == 0)
						NoteOff(liChannel, piData1);
					else
						NoteOn(liChannel, piData1, piData2);
					break;

				case 0x80:
					NoteOff(liChannel, piData1);
					break;

				case 0xB0:
					Controller(liChannel, piData1, piData2);
					break;

				case 0xC0:
					mOChannels[liChannel].Program = piData1 & 0x7F;
					break;

				case 0xE0:
					mOChannels[liChannel].Bend = ((piData2 << 7) | piData1) - 8192;
					fRefreshChannelPitch(liChannel);
					break;
			}
		}

		public void NoteOn(int piChannel, int piNote, int piVelocity)
		{
			MidiChannel lOChannel = mOChannels[piChannel];
			UWAdlibBank.MelodicPatch lOPatch;
			double lfPitch;

			if (piChannel == DrumChannel)
			{
				lOPatch = mOBank.GetDrum(piNote);

				if (lOPatch == null)
					return;

				// For percussion the transpose value says WHICH note is played.
				lfPitch = lOPatch.Transpose;
			}
			else
			{
				lOPatch = mOBank.GetMelodic(lOChannel.Program);

				if (lOPatch == null)
					return;

				lfPitch = piNote + lOPatch.Transpose;
			}

			// The same note on the same channel again: end it first.
			for (int liAt = 0; liAt < VoiceCount; liAt++)
			{
				Voice lOSame = mOVoices[liAt];

				if (lOSame.Active && lOSame.MidiChannel == piChannel && lOSame.Note == piNote)
					fKeyOff(liAt);
			}

			int liVoice = fAllocateVoice();
			Voice lOVoice = mOVoices[liVoice];

			lOVoice.Active = true;
			lOVoice.Held = false;
			lOVoice.MidiChannel = piChannel;
			lOVoice.Note = piNote;
			lOVoice.Pitch = lfPitch;
			lOVoice.Velocity = piVelocity;
			lOVoice.Started = ++miClock;
			lOVoice.Patch = lOPatch;

			fWritePatch(liVoice, lOPatch);
			fWriteVolume(liVoice);
			fWriteFrequency(liVoice, true);
		}

		public void NoteOff(int piChannel, int piNote)
		{
			for (int liAt = 0; liAt < VoiceCount; liAt++)
			{
				Voice lOVoice = mOVoices[liAt];

				if (!lOVoice.Active || lOVoice.MidiChannel != piChannel || lOVoice.Note != piNote || lOVoice.Held)
					continue;

				if (mOChannels[piChannel].Sustain)
					lOVoice.Held = true;
				else
					fKeyOff(liAt);
			}
		}

		public void Controller(int piChannel, int piController, int piValue)
		{
			MidiChannel lOChannel = mOChannels[piChannel];

			switch (piController)
			{
				case 7:
					lOChannel.Volume = piValue;
					fRefreshChannelVolume(piChannel);
					break;

				case 11:
					lOChannel.Expression = piValue;
					fRefreshChannelVolume(piChannel);
					break;

				case 64:
					lOChannel.Sustain = piValue >= 64;

					if (!lOChannel.Sustain)
					{
						for (int liAt = 0; liAt < VoiceCount; liAt++)
						{
							if (mOVoices[liAt].Active && mOVoices[liAt].MidiChannel == piChannel && mOVoices[liAt].Held)
								fKeyOff(liAt);
						}
					}
					break;

				case 121:
					lOChannel.Volume = 127;
					lOChannel.Expression = 127;
					lOChannel.Bend = 0;
					lOChannel.Sustain = false;
					break;

				case 120:
				case 123:
					for (int liAt = 0; liAt < VoiceCount; liAt++)
					{
						if (mOVoices[liAt].Active && mOVoices[liAt].MidiChannel == piChannel)
							fKeyOff(liAt);
					}
					break;
			}
		}

		/// <summary>After a change of the master volume, update all active
		/// voices.</summary>
		public void RefreshVolume()
		{
			for (int liAt = 0; liAt < VoiceCount; liAt++)
			{
				if (mOVoices[liAt].Active)
					fWriteVolume(liAt);
			}
		}

		private int fAllocateVoice()
		{
			int liOldest = 0;
			long liOldestStart = long.MaxValue;

			for (int liAt = 0; liAt < VoiceCount; liAt++)
			{
				if (!mOVoices[liAt].Active)
					return liAt;

				if (mOVoices[liAt].Started < liOldestStart)
				{
					liOldestStart = mOVoices[liAt].Started;
					liOldest = liAt;
				}
			}

			fKeyOff(liOldest);

			return liOldest;
		}

		private void fKeyOff(int piVoice)
		{
			Voice lOVoice = mOVoices[piVoice];

			lOVoice.Active = false;
			lOVoice.Held = false;

			fWriteFrequency(piVoice, false);
		}

		private void fWritePatch(int piVoice, UWAdlibBank.MelodicPatch pOPatch)
		{
			int liModulator = msModulatorOffsets[piVoice];
			int liCarrier = msCarrierOffsets[piVoice];

			mOChip.WriteRegister(0x20 + liModulator, pOPatch.Modulator[0]);
			mOChip.WriteRegister(0x60 + liModulator, pOPatch.Modulator[2]);
			mOChip.WriteRegister(0x80 + liModulator, pOPatch.Modulator[3]);
			mOChip.WriteRegister(0xE0 + liModulator, pOPatch.Modulator[4]);

			mOChip.WriteRegister(0x20 + liCarrier, pOPatch.Carrier[0]);
			mOChip.WriteRegister(0x60 + liCarrier, pOPatch.Carrier[2]);
			mOChip.WriteRegister(0x80 + liCarrier, pOPatch.Carrier[3]);
			mOChip.WriteRegister(0xE0 + liCarrier, pOPatch.Carrier[4]);

			mOChip.WriteRegister(0xC0 + piVoice, pOPatch.FeedbackConnection & 0x0F);
		}

		private void fWriteVolume(int piVoice)
		{
			Voice lOVoice = mOVoices[piVoice];
			MidiChannel lOChannel = mOChannels[lOVoice.MidiChannel];
			UWAdlibBank.MelodicPatch lOPatch = lOVoice.Patch;

			int liComposite = UWTvfxVoice.VelocityGraph[(lOVoice.Velocity & 0x7F) >> 3];
			liComposite = liComposite * lOChannel.Volume / 127;
			liComposite = liComposite * lOChannel.Expression / 127;
			liComposite = liComposite * MasterVolume / 127;

			mOChip.WriteRegister(0x40 + msCarrierOffsets[piVoice], fScaleLevel(lOPatch.Carrier[1], liComposite));

			mOChip.WriteRegister(0x40 + msModulatorOffsets[piVoice], lOPatch.IsAdditive
				? fScaleLevel(lOPatch.Modulator[1], liComposite)
				: lOPatch.Modulator[1]);
		}

		/// <summary>Register value 0x40 (KSL and level) with a linearly scaled level.</summary>
		private static int fScaleLevel(int piRegister, int piComposite)
		{
			int liVolumeIn = (~piRegister) & 0x3F;
			int liScaled = (liVolumeIn * piComposite) / 127;

			return (piRegister & 0xC0) | ((~liScaled) & 0x3F);
		}

		private void fWriteFrequency(int piVoice, bool pbKeyOn)
		{
			Voice lOVoice = mOVoices[piVoice];
			double lfNote = lOVoice.Pitch;

			if (lOVoice.MidiChannel >= 0)
				lfNote += mOChannels[lOVoice.MidiChannel].Bend * BendSemitones / 8192.0;

			double lfFrequency = 440.0 * Math.Pow(2.0, (lfNote - 69.0) / 12.0);
			double lfFNum = lfFrequency * 1048576.0 / UWOpl2.SampleRate;
			int liBlock = 0;

			while (lfFNum >= 1024.0 && liBlock < 7)
			{
				lfFNum /= 2.0;
				liBlock++;
			}

			int liFNum = (int)Math.Round(lfFNum);

			if (liFNum > 1023)
				liFNum = 1023;

			if (liFNum < 0)
				liFNum = 0;

			mOChip.WriteRegister(0xA0 + piVoice, liFNum & 0xFF);
			mOChip.WriteRegister(0xB0 + piVoice, (pbKeyOn ? 0x20 : 0) | (liBlock << 2) | (liFNum >> 8));
		}

		private void fRefreshChannelPitch(int piChannel)
		{
			for (int liAt = 0; liAt < VoiceCount; liAt++)
			{
				if (mOVoices[liAt].Active && mOVoices[liAt].MidiChannel == piChannel)
					fWriteFrequency(liAt, true);
			}
		}

		private void fRefreshChannelVolume(int piChannel)
		{
			for (int liAt = 0; liAt < VoiceCount; liAt++)
			{
				if (mOVoices[liAt].Active && mOVoices[liAt].MidiChannel == piChannel)
					fWriteVolume(liAt);
			}
		}
	}
}
