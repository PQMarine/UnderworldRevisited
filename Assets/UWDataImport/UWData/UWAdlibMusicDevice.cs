namespace UWDataImport.Audio
{
	/// <summary>
	/// The music on the AdLib: its own emulated chip (UWOpl2) with UWAdlibMusicDriver on it, the
	/// timbres from UW.AD. Cleanup step 6.6 (2026-10-24), out of UWAudioEngine.
	///
	/// THE PIECE TICKS PER CHIP SAMPLE: NextSample first advances the sequencer by one sample at
	/// 49716 Hz, then lets the chip make it - exactly what UWAudioEngine.fNextSourceSample did
	/// before, without asking which driver the sequencer fed. The chip always runs, whatever the
	/// device: the AdLib version of a piece plays on it when a game lacks the MT-32 one, and the
	/// instruments sound on it while the AdLib is the device.
	/// </summary>
	public sealed class UWAdlibMusicDevice : UWMusicDevice
	{
		private readonly UWOpl2 mOChip;

		private readonly UWAdlibMusicDriver mODriver;

		public UWAdlibMusicDevice(UWAdlibBank pOBank)
		{
			mOChip = new UWOpl2();
			mODriver = new UWAdlibMusicDriver(mOChip, pOBank);
		}

		protected override IUWMidiDriver Driver => mODriver;

		public override string Name => "AdLib";

		public override UWXmiVersion XmiVersion => UWXmiVersion.AdLib;

		public override int SampleRate => UWOpl2.SampleRate;

		/// <summary>One chip sample (16-bit range): the piece advanced by one sample first.</summary>
		public int NextSample()
		{
			if (mOSequencer != null)
				mOSequencer.Advance(1, mODriver);

			return mOChip.GenerateSample();
		}

		/// <summary>piCount chip samples at 49716 Hz, scaled to -1..1 as the mixer scales them.</summary>
		public override void Render(float[] pfLeft, float[] pfRight, int piCount)
		{
			for (int liAt = 0; liAt < piCount; liAt++)
			{
				float lfValue = NextSample() / 32768f;

				pfLeft[liAt] = lfValue;

				if (pfRight != null)
					pfRight[liAt] = lfValue;
			}
		}
	}
}
