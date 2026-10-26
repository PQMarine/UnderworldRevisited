namespace UWDataImport.Audio
{
	/// <summary>
	/// What the sound plays at: the music and effect gains (0 with the switch off) and the music
	/// device asked for. Cleanup step 6.6 (2026-10-24).
	///
	/// WHY A SNAPSHOT: the audio thread used to read two gain fields that UWAudioEngine.Update
	/// copied from UWSettings every frame, while the device came straight from UWUserSettings.
	/// Now the game thread captures one immutable snapshot per frame (With) - still every frame,
	/// so the Inspector can change the live UWSettings while playing - and only makes a new one,
	/// with the next Revision, when something changed. The audio thread takes the reference once
	/// per buffer and reads nothing else; nothing is allocated there.
	/// </summary>
	public sealed class UWAudioSettings
	{
		/// <summary>Before the first capture: nothing to hear, the AdLib.</summary>
		public static readonly UWAudioSettings Initial = new UWAudioSettings(0f, 0f, UWMusicDeviceId.AdLib, 0);

		public float MusicGain { get; }

		public float EffectGain { get; }

		public UWMusicDeviceId MusicDevice { get; }

		/// <summary>Counts the changes: a reader that remembers it knows whether anything moved.</summary>
		public int Revision { get; }

		private UWAudioSettings(float pfMusic, float pfEffects, UWMusicDeviceId peDevice, int piRevision)
		{
			MusicGain = pfMusic;
			EffectGain = pfEffects;
			MusicDevice = peDevice;
			Revision = piRevision;
		}

		/// <summary>The snapshot for these values: this one when nothing changed, else a new one
		/// with the next revision. The gains are clamped to 0..1.</summary>
		public UWAudioSettings With(float pfMusic, float pfEffects, UWMusicDeviceId peDevice)
		{
			float lfMusic = Clamp01(pfMusic);
			float lfEffects = Clamp01(pfEffects);

			if (lfMusic == MusicGain && lfEffects == EffectGain && peDevice == MusicDevice)
				return this;

			return new UWAudioSettings(lfMusic, lfEffects, peDevice, Revision + 1);
		}

		/// <summary>The same comparisons as Unity's Mathf.Clamp01, which this replaced.</summary>
		public static float Clamp01(float pfValue)
		{
			if (pfValue < 0f)
				return 0f;

			if (pfValue > 1f)
				return 1f;

			return pfValue;
		}
	}
}
