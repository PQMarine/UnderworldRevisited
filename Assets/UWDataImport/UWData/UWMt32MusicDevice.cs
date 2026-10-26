namespace UWDataImport.Audio
{
	/// <summary>
	/// The music on the MT-32: the MT-32 version of the pieces through UWMt32MusicDriver (Munt's
	/// libmt32emu with the player's own ROMs), stereo at the output rate. Cleanup step 6.6
	/// (2026-10-24), out of UWAudioEngine, which special-cased this driver at five places.
	///
	/// Its reset differs from the others': the driver's Reset already releases the notes (and
	/// gives the instrument's part back), and a switch of the device resets it rather than only
	/// releasing the notes - both as UWAudioEngine did.
	/// </summary>
	public sealed class UWMt32MusicDevice : UWSynthMusicDevice
	{
		/// <summary>The MT-32's level beside the AdLib's, set by ear.</summary>
		public const float Mt32Gain = 1f;

		private readonly UWMt32MusicDriver mODriver;

		private UWMt32MusicDevice(UWMt32MusicDriver pODriver)
		{
			mODriver = pODriver;
		}

		/// <summary>The emulation for the ROMs in a folder, or null with the reason - no ROMs, or
		/// no library on this system (UWMt32MusicDriver.TryCreate).</summary>
		public static UWMt32MusicDevice TryCreate(string psRomFolder, int piSampleRate, out string psError)
		{
			UWMt32MusicDriver lODriver = UWMt32MusicDriver.TryCreate(psRomFolder, piSampleRate, out psError);

			return lODriver != null ? new UWMt32MusicDevice(lODriver) : null;
		}

		/// <summary>The ROMs the emulation runs on, for the log.</summary>
		public string Description => mODriver.Description;

		protected override IUWRenderingMidiDriver Synth => mODriver;

		public override string Name => "MT-32";

		public override int SampleRate => mODriver.SampleRate;

		public override float Gain => Mt32Gain;

		public override void Reset()
		{
			mOSequencer = null;
			mODriver.Reset();
		}

		public override void OnDeviceSwitch()
		{
			mOSequencer = null;
			mODriver.Reset();
		}

		public override void ReleaseInstrumentChannel()
		{
			mODriver.ReleaseLockedChannel();
		}

		public override void Dispose()
		{
			mODriver.Dispose();
		}
	}
}
