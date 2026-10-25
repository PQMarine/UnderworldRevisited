using System;

namespace UWDataImport.UWData
{
	/// <summary>Which version of a piece a device plays: the AdLib one (AW*.XMI) or the MT-32 one
	/// (UW*.XMI), which General MIDI plays as well.</summary>
	public enum UWXmiVersion
	{
		AdLib,
		Mt32
	}

	/// <summary>
	/// A device the music plays on: the AdLib chip (UWAdlibMusicDevice), General MIDI
	/// (UWGmMusicDevice) or the MT-32 (UWMt32MusicDevice). Cleanup step 6.6 (2026-10-24).
	///
	/// WHY: UWAudioEngine kept one sequencer and the three drivers side by side, asked by
	/// reference which driver the sequencer fed (three places), special-cased the MT-32 at five
	/// and repeated "notes off, reset" for every driver in three places. Now each device owns the
	/// sequencer of the piece it plays and ticks it inside its own Render, at its own rate (the
	/// chip per chip sample, the synthesizers per block), and the lifecycle calls go to every
	/// device in one loop (UWAudioMixCore).
	///
	/// The events of the piece and of the instruments reach the device's driver through Handle;
	/// the device itself is the driver as the game's instrument path sees it.
	/// </summary>
	public interface IUWMusicDevice : IUWMidiDriver, IDisposable
	{
		/// <summary>For the log and the menu.</summary>
		string Name { get; }

		/// <summary>Which file of a piece this device is meant for.</summary>
		UWXmiVersion XmiVersion { get; }

		/// <summary>The rate the device renders at, and so the rate its sequencer counts in.</summary>
		int SampleRate { get; }

		/// <summary>The device's level beside the AdLib's, set by ear.</summary>
		float Gain { get; }

		/// <summary>Is a piece playing on this device? One that has played to the end no longer
		/// counts.</summary>
		bool IsPlaying { get; }

		/// <summary>The piece this device plays from now on, from its start (null: none). The
		/// caller has reset the device first (Reset).</summary>
		void Prepare(UWXmi.Sequence pOSequence, bool pbLoop);

		/// <summary>The music stops: the piece is dropped, sounding notes are released.</summary>
		void Stop();

		/// <summary>The music moves to another device (or to this one): the piece is dropped, and
		/// the device silences itself in its own way (the MT-32 also gives the instrument's part
		/// back).</summary>
		void OnDeviceSwitch();

		/// <summary>The next piCount samples at SampleRate, advancing the piece as it goes. The
		/// synthesizers write stereo into both buffers; the chip writes mono into pfLeft (and
		/// pfRight when it is not null).</summary>
		void Render(float[] pfLeft, float[] pfRight, int piCount);

		/// <summary>The instrument is put down; a device that lent it a part of the music's gets
		/// that part back (the MT-32).</summary>
		void ReleaseInstrumentChannel();
	}

	/// <summary>
	/// What every device shares: the sequencer of its piece and the lifecycle calls on its
	/// driver. Reset (IUWMidiDriver) on a device also drops its piece - the piece always starts
	/// anew after a reset (UWAudioMixCore.PlayMusic).
	/// </summary>
	public abstract class UWMusicDevice : IUWMusicDevice
	{
		/// <summary>The piece's sequencer, null without one. Advanced only inside Render.</summary>
		protected UWXmiSequencer mOSequencer;

		/// <summary>The driver the piece's events and the instrument's go to.</summary>
		protected abstract IUWMidiDriver Driver { get; }

		public abstract string Name { get; }

		public abstract UWXmiVersion XmiVersion { get; }

		public abstract int SampleRate { get; }

		public virtual float Gain => 1f;

		public bool IsPlaying => mOSequencer != null && !mOSequencer.IsFinished;

		public void Prepare(UWXmi.Sequence pOSequence, bool pbLoop)
		{
			mOSequencer = pOSequence != null ? new UWXmiSequencer(pOSequence, SampleRate) { Loop = pbLoop } : null;
		}

		public void Handle(int piStatus, int piData1, int piData2)
		{
			Driver.Handle(piStatus, piData1, piData2);
		}

		public void AllNotesOff()
		{
			Driver.AllNotesOff();
		}

		/// <summary>Piece dropped, notes off, every channel back to its initial state.</summary>
		public virtual void Reset()
		{
			mOSequencer = null;
			Driver.AllNotesOff();
			Driver.Reset();
		}

		public void Stop()
		{
			mOSequencer = null;
			Driver.AllNotesOff();
		}

		public virtual void OnDeviceSwitch()
		{
			mOSequencer = null;
			Driver.AllNotesOff();
		}

		public abstract void Render(float[] pfLeft, float[] pfRight, int piCount);

		public virtual void ReleaseInstrumentChannel()
		{
		}

		public virtual void Dispose()
		{
		}
	}

	/// <summary>
	/// A device whose driver makes its own stereo samples at the output rate (General MIDI,
	/// MT-32): Render first advances the piece by the block, then renders it - the events land on
	/// the block's start, as they always did (UWAudioMixCore.MidiBlockFrames).
	/// </summary>
	public abstract class UWSynthMusicDevice : UWMusicDevice
	{
		protected abstract IUWRenderingMidiDriver Synth { get; }

		protected override IUWMidiDriver Driver => Synth;

		public override UWXmiVersion XmiVersion => UWXmiVersion.Mt32;

		public override void Render(float[] pfLeft, float[] pfRight, int piCount)
		{
			if (mOSequencer != null)
				mOSequencer.Advance(piCount, Synth);

			Synth.Render(pfLeft, pfRight, piCount);
		}
	}
}
