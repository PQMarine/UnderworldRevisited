namespace UWDataImport.UWData
{
	/// <summary>
	/// What UWXmiSequencer plays its events on: the AdLib driver (UWAdlibMusicDriver), the
	/// General MIDI synthesizer (UWGmMusicDriver) or the MT-32 emulation (UWMt32MusicDriver).
	/// </summary>
	public interface IUWMidiDriver
	{
		/// <summary>One MIDI channel event: status with channel, two data bytes.</summary>
		void Handle(int piStatus, int piData1, int piData2);

		/// <summary>Every sounding note released.</summary>
		void AllNotesOff();

		/// <summary>Notes off, every channel back to its initial state.</summary>
		void Reset();
	}

	/// <summary>A driver that makes its own stereo samples at the output rate (General MIDI,
	/// MT-32), as opposed to the AdLib driver, which writes chip registers.</summary>
	public interface IUWRenderingMidiDriver : IUWMidiDriver
	{
		/// <summary>The next piCount stereo samples into the two buffers, from their start.</summary>
		void Render(float[] pfLeft, float[] pfRight, int piCount);
	}
}
