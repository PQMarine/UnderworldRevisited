namespace UWDataImport.Audio
{
	/// <summary>
	/// The music on General MIDI: the MT-32 version of the pieces through UWGmMusicDriver
	/// (MeltySynth with the soundfont that ships with the game), stereo at the output rate.
	/// Cleanup step 6.6 (2026-10-24), out of UWAudioEngine.
	/// </summary>
	public sealed class UWGmMusicDevice : UWSynthMusicDevice
	{
		/// <summary>The General MIDI music's level beside the AdLib's, set by ear.</summary>
		public const float GeneralMidiGain = 1f;

		private readonly UWGmMusicDriver mODriver;

		/// <summary>Loads the soundfont; throws if it cannot be read.</summary>
		public UWGmMusicDevice(string psSoundFontPath, int piSampleRate)
		{
			mODriver = new UWGmMusicDriver(psSoundFontPath, piSampleRate);
		}

		protected override IUWRenderingMidiDriver Synth => mODriver;

		public override string Name => "General MIDI";

		public override int SampleRate => mODriver.SampleRate;

		public override float Gain => GeneralMidiGain;
	}
}
