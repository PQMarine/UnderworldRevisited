using System;

namespace UWDataImport.Audio
{
	/// <summary>
	/// The sound of the game, engine-free: the music devices (UWMusicDeviceSelector), the effects
	/// chip (UWEffectSynth) and the mix into the output buffer. Cleanup step 6.6 (2026-10-24),
	/// out of UWAudioEngine, whose Unity side is now UWAudioMixer - it owns the audio thread and
	/// the lock; this class owns the sound.
	///
	/// NOT THREAD-SAFE BY ITSELF. The host serialises every call: UWAudioMixer holds one lock
	/// around each, the audio thread's Mix included, so whatever comes from the game thread - a
	/// sound effect, a new piece - lands between two buffers. Mix allocates nothing.
	///
	/// The chips run at their 49716 Hz; conversion to the output rate is linear. The sequencer of
	/// the AdLib piece and the sixtieth-of-a-second ticks of the effect engine run along per chip
	/// sample, so that a hitch in the frame does not make the music stutter. General MIDI and the
	/// MT-32 render stereo at the output rate in blocks of MidiBlockFrames, the piece advanced
	/// block by block. The sound effects stay on the AdLib chip whatever the music device.
	/// </summary>
	public sealed class UWAudioMixCore : IDisposable
	{
		/// <summary>How many frames the MIDI synthesizers render at once; the sequencer's events land
		/// on these steps (64 frames, 1.3 ms at 48 kHz).</summary>
		public const int MidiBlockFrames = 64;

		/// <summary>MIDI channel for instruments - free alongside the music. UW.EXE obtains
		/// a free channel from the driver for this (PlayMusicalInstrument_seg014_1195).</summary>
		public const int InstrumentChannel = 14;

		private readonly UWMusicDeviceSelector mOSelector;

		private readonly UWAdlibMusicDevice mOAdlib;

		private readonly UWEffectSynth mOEffects;

		private readonly float[] mfMidiLeft = new float[MidiBlockFrames];

		private readonly float[] mfMidiRight = new float[MidiBlockFrames];

		private int miMidiBlockAt = MidiBlockFrames;

		private readonly double mfRatio;

		private double mfSourcePosition = 1.0;

		private float mfPrevious;

		private float mfCurrent;

		/// <param name="pOBank">The timbres (UW.AD), for the music chip and the effects.</param>
		/// <param name="piOutputRate">The rate of the output buffer.</param>
		/// <param name="psSoundFontPath">The General MIDI soundfont (UWMusicDeviceSelector).</param>
		/// <param name="pFRomFolder">Where the MT-32 ROMs are looked for.</param>
		/// <param name="pFLog">A device started (may be null).</param>
		/// <param name="pFWarn">A device could not start (may be null).</param>
		public UWAudioMixCore(UWAdlibBank pOBank, int piOutputRate, string psSoundFontPath,
			Func<string> pFRomFolder, Action<string> pFLog, Action<string> pFWarn)
		{
			OutputRate = piOutputRate;
			mfRatio = (double)UWOpl2.SampleRate / piOutputRate;
			mOAdlib = new UWAdlibMusicDevice(pOBank);
			mOEffects = new UWEffectSynth(pOBank);
			mOSelector = new UWMusicDeviceSelector(mOAdlib, piOutputRate, psSoundFontPath, pFRomFolder, pFLog, pFWarn);
		}

		public int OutputRate { get; }

		/// <summary>The device the music plays on now.</summary>
		public UWMusicDeviceId MusicDevice => mOSelector.Current;

		/// <summary>Is a piece playing? One that has played to the end no longer counts.</summary>
		public bool IsMusicPlaying
		{
			get
			{
				for (int liSlot = 0; liSlot < mOSelector.SlotCount; liSlot++)
				{
					IUWMusicDevice lIDevice = mOSelector.GetDevice(liSlot);

					if (lIDevice != null && lIDevice.IsPlaying)
						return true;
				}

				return false;
			}
		}

		// ------------------------------------------------------------------
		// Music
		// ------------------------------------------------------------------

		/// <summary>The device a wish comes to, made now if needed (slow: a soundfont or ROMs are
		/// loaded) - call it OUTSIDE the audio lock, then SwitchDevice under it.</summary>
		public UWMusicDeviceId ResolveDevice(UWMusicDeviceId peWanted)
		{
			return mOSelector.Resolve(peWanted);
		}

		/// <summary>Switches the music to a device ResolveDevice gave; whatever was playing stops.
		/// True when the device changed.</summary>
		public bool SwitchDevice(UWMusicDeviceId peDevice)
		{
			if (peDevice == mOSelector.Current)
				return false;

			for (int liSlot = 0; liSlot < mOSelector.SlotCount; liSlot++)
				mOSelector.GetDevice(liSlot)?.OnDeviceSwitch();

			mOSelector.SetCurrent(peDevice);

			return true;
		}

		/// <summary>
		/// The piece of a theme for the device now: the AdLib version "AW" plus the number, or on
		/// General MIDI and the MT-32 the MT-32 version "UW" (the AdLib one again if a game lacks
		/// it; that one then plays on the chip, PlayMusic). Null when there is none. Loads the
		/// file, so OUTSIDE the audio lock. Out of UWMusic.UWMusicOutput.Play (2026-10-24).
		/// </summary>
		public UWXmi.Sequence SelectPiece(UWSound pOSound, string psNumber, out UWXmiVersion peVersion)
		{
			peVersion = mOSelector.CurrentDevice.XmiVersion;

			if (pOSound == null)
				return null;

			UWXmi lOXmi = null;

			if (peVersion != UWXmiVersion.AdLib)
			{
				lOXmi = pOSound.GetMusic(psNumber, false);

				if (lOXmi == null || !lOXmi.IsLoaded || lOXmi.Sequences.Count == 0)
					peVersion = UWXmiVersion.AdLib;
			}

			if (peVersion == UWXmiVersion.AdLib)
				lOXmi = pOSound.GetMusic(psNumber, true);

			if (lOXmi == null || !lOXmi.IsLoaded || lOXmi.Sequences.Count == 0)
				return null;

			return lOXmi.Sequences[0];
		}

		/// <summary>Plays an XMI sequence from the start; whatever was playing stops. The AdLib
		/// version plays on the chip whatever the device; the MT-32 version on the music device.</summary>
		public void PlayMusic(UWXmi.Sequence pOSequence, bool pbLoop, UWXmiVersion peVersion)
		{
			for (int liSlot = 0; liSlot < mOSelector.SlotCount; liSlot++)
				mOSelector.GetDevice(liSlot)?.Reset();

			IUWMusicDevice lIDevice = peVersion == UWXmiVersion.AdLib ? mOAdlib : mOSelector.CurrentDevice;

			lIDevice.Prepare(pOSequence, pbLoop);
		}

		public void StopMusic()
		{
			for (int liSlot = 0; liSlot < mOSelector.SlotCount; liSlot++)
				mOSelector.GetDevice(liSlot)?.Stop();
		}

		// ------------------------------------------------------------------
		// Effects
		// ------------------------------------------------------------------

		/// <summary>See UWEffectSynth.Play.</summary>
		public void PlayEffect(UWSound.Effect pOEffect, int piVelocityOffset)
		{
			mOEffects.Play(pOEffect, piVelocityOffset);
		}

		/// <summary>See UWEffectSynth.StartHeld.</summary>
		public int StartHeldEffect(UWSound.Effect pOEffect, int piVelocityOffset)
		{
			return mOEffects.StartHeld(pOEffect, piVelocityOffset);
		}

		public bool IsHeldEffectPlaying(int piVoice)
		{
			return mOEffects.IsSounding(piVoice);
		}

		public void StopHeldEffect(int piVoice)
		{
			mOEffects.Release(piVoice);
		}

		// ------------------------------------------------------------------
		// Instruments
		// ------------------------------------------------------------------

		/// <summary>
		/// Prepares the instrument channel like UW.EXE: timbre, all controllers reset,
		/// volume and expression full, centre pan. Sounds through the music device and
		/// thus at the music volume - as in the original, which uses the same driver.
		/// </summary>
		public void BeginInstrument(int piProgram)
		{
			fInstrument(0xB0 | InstrumentChannel, 0x72, 0);
			fInstrument(0xC0 | InstrumentChannel, piProgram & 0x7F, 0);
			fInstrument(0xB0 | InstrumentChannel, 0x79, 0);
			fInstrument(0xB0 | InstrumentChannel, 7, 0x7F);
			fInstrument(0xB0 | InstrumentChannel, 11, 0x7F);
			fInstrument(0xB0 | InstrumentChannel, 10, 0x40);
		}

		/// <summary>Strikes a note (velocity 127, like UW.EXE).</summary>
		public void PlayInstrumentNote(int piNote)
		{
			fInstrument(0x90 | InstrumentChannel, piNote & 0x7F, 0x7F);
		}

		public void StopInstrumentNote(int piNote)
		{
			fInstrument(0x80 | InstrumentChannel, piNote & 0x7F, 0);
		}

		/// <summary>The instrument is put down. On the MT-32 the part it borrowed goes back to the
		/// music (UWMt32MusicDriver.ReleaseLockedChannel); the other devices have a channel of
		/// their own for it.</summary>
		public void EndInstrument()
		{
			for (int liSlot = 0; liSlot < mOSelector.SlotCount; liSlot++)
				mOSelector.GetDevice(liSlot)?.ReleaseInstrumentChannel();
		}

		/// <summary>One instrument event to the music device. The bank select 0x72 UW.EXE sends is
		/// one of the Miles driver's own controllers and stops here, as it stopped in every driver
		/// before (UWXmiSequencer.IsXmidiController).</summary>
		private void fInstrument(int piStatus, int piData1, int piData2)
		{
			if ((piStatus & 0xF0) == 0xB0 && UWXmiSequencer.IsXmidiController(piData1))
				return;

			mOSelector.CurrentDevice.Handle(piStatus, piData1, piData2);
		}

		// ------------------------------------------------------------------
		// Audio thread
		// ------------------------------------------------------------------

		/// <summary>Adds the sound to an interleaved output buffer (Unity's OnAudioFilterRead
		/// shape). The gains are the settings' (UWAudioSettings).</summary>
		public void Mix(float[] pfData, int piChannels, float pfMusic, float pfEffects)
		{
			int liFrames = pfData.Length / piChannels;

			for (int liFrame = 0; liFrame < liFrames; liFrame++)
			{
				while (mfSourcePosition >= 1.0)
				{
					mfPrevious = mfCurrent;
					mfCurrent = fNextSourceSample(pfMusic, pfEffects);
					mfSourcePosition -= 1.0;
				}

				float lfValue = mfPrevious + ((mfCurrent - mfPrevious) * (float)mfSourcePosition);
				mfSourcePosition += mfRatio;

				int liBase = liFrame * piChannels;

				for (int liChannel = 0; liChannel < piChannels; liChannel++)
					pfData[liBase + liChannel] += lfValue;
			}

			if (mOSelector.Current != UWMusicDeviceId.AdLib)
			{
				IUWMusicDevice lIDevice = mOSelector.CurrentDevice;

				fMixMidi(lIDevice, pfData, piChannels, liFrames, pfMusic * lIDevice.Gain);
			}
		}

		/// <summary>The General MIDI or MT-32 music, stereo at the output rate: each block of
		/// MidiBlockFrames is rendered by the device, which first advances its piece.</summary>
		private void fMixMidi(IUWMusicDevice pIDevice, float[] pfData, int piChannels, int piFrames, float pfGain)
		{
			for (int liFrame = 0; liFrame < piFrames; liFrame++)
			{
				if (miMidiBlockAt >= MidiBlockFrames)
				{
					pIDevice.Render(mfMidiLeft, mfMidiRight, MidiBlockFrames);
					miMidiBlockAt = 0;
				}

				float lfLeft = mfMidiLeft[miMidiBlockAt] * pfGain;
				float lfRight = mfMidiRight[miMidiBlockAt] * pfGain;
				int liBase = liFrame * piChannels;

				miMidiBlockAt++;

				if (piChannels == 1)
				{
					pfData[liBase] += (lfLeft + lfRight) * 0.5f;
				}
				else
				{
					pfData[liBase] += lfLeft;
					pfData[liBase + 1] += lfRight;
				}
			}
		}

		/// <summary>One sample at 49716 Hz: both chips run one sample (the AdLib piece and the effect
		/// ticks advancing inside), mixed.</summary>
		private float fNextSourceSample(float pfMusic, float pfEffects)
		{
			int liMusic = mOAdlib.NextSample();
			int liEffects = mOEffects.NextSample();

			return ((liMusic * pfMusic) + (liEffects * pfEffects)) / 32768f;
		}

		public void Dispose()
		{
			mOSelector.Dispose();
		}
	}
}
