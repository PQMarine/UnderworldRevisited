namespace UWDataImport.UWData
{
	/// <summary>
	/// The sound effects: a second emulated AdLib chip (UWOpl2) with nine UWTvfxVoice, one per
	/// chip channel, serviced sixty times a second. Cleanup step 6.6 (2026-10-24), out of
	/// UWAudioEngine, where PlayEffect and StartHeldEffect each had their own copy of the voice
	/// search; now one (fStart).
	///
	/// If all nine voices are busy, a new sound is dropped - the original does it the same way.
	/// </summary>
	public sealed class UWEffectSynth
	{
		/// <summary>The tick rate of the voice service.</summary>
		public const double TicksPerSecond = 60.0;

		private readonly UWOpl2 mOChip;

		private readonly UWAdlibBank mOBank;

		private readonly UWTvfxVoice[] mOVoices = new UWTvfxVoice[UWOpl2.ChannelCount];

		private double mfTickAccumulator;

		public UWEffectSynth(UWAdlibBank pOBank)
		{
			mOBank = pOBank;
			mOChip = new UWOpl2();
			mOChip.WriteRegister(0x01, 0x20);

			for (int liAt = 0; liAt < mOVoices.Length; liAt++)
				mOVoices[liAt] = new UWTvfxVoice(liAt);
		}

		public int VoiceCount => mOVoices.Length;

		/// <summary>
		/// Starts a sound effect from SOUNDS.DAT. piVelocityOffset is the offset to the entry's
		/// velocity, as supplied by the distance calculation (UWSoundAttenuation). Returns the
		/// voice, or -1 when none was free or the patch is missing.
		/// </summary>
		public int Play(UWSound.Effect pOEffect, int piVelocityOffset)
		{
			return fStart(pOEffect, pOEffect.LifetimeTicks, piVelocityOffset);
		}

		/// <summary>
		/// Starts an effect and HOLDS it: the voice is not released until Release, and it gets no
		/// lifetime, so it only ends through its own program. That is how the original keeps the
		/// water sound going while one is in the water.
		///
		/// Returns the voice, or -1 when none was free. The caller keeps the number and asks
		/// IsSounding whether the program has ended in the meantime - the original starts it
		/// afresh as well.
		/// </summary>
		public int StartHeld(UWSound.Effect pOEffect, int piVelocityOffset)
		{
			return fStart(pOEffect, -1, piVelocityOffset);
		}

		/// <summary>Whether a voice is still sounding - see StartHeld.</summary>
		public bool IsSounding(int piVoice)
		{
			return piVoice >= 0 && piVoice < mOVoices.Length && mOVoices[piVoice].Phase != UWTvfxVoice.PhaseEnum.Idle;
		}

		/// <summary>Ends a held effect - see StartHeld.</summary>
		public void Release(int piVoice)
		{
			if (piVoice >= 0 && piVoice < mOVoices.Length)
				mOVoices[piVoice].Release();
		}

		/// <summary>The first idle voice gets the effect (lifetime in sixtieths, -1 for held).</summary>
		private int fStart(UWSound.Effect pOEffect, int piLifetimeTicks, int piVelocityOffset)
		{
			UWAdlibBank.TvfxPatch lOPatch = mOBank.GetEffect(pOEffect.Patch);

			if (lOPatch == null)
				return -1;

			byte lyScale = UWTvfxVoice.ComputeVolumeScale(pOEffect.Velocity, piVelocityOffset);

			for (int liAt = 0; liAt < mOVoices.Length; liAt++)
			{
				if (mOVoices[liAt].Phase != UWTvfxVoice.PhaseEnum.Idle)
					continue;

				mOVoices[liAt].StartKeyOn(lOPatch, piLifetimeTicks, lyScale);

				return liAt;
			}

			return -1;
		}

		/// <summary>One chip sample at 49716 Hz (16-bit range): the sixtieth-of-a-second ticks of
		/// the voices run along per sample, so a hitch in the frame does not make them stutter.</summary>
		public int NextSample()
		{
			mfTickAccumulator += TicksPerSecond / UWOpl2.SampleRate;

			if (mfTickAccumulator >= 1.0)
			{
				mfTickAccumulator -= 1.0;

				foreach (UWTvfxVoice lOVoice in mOVoices)
				{
					if (lOVoice.Phase == UWTvfxVoice.PhaseEnum.Idle)
						continue;

					lOVoice.ServiceTick();
					lOVoice.EmitRegisters(mOChip);
				}
			}

			return mOChip.GenerateSample();
		}

		/// <summary>piCount chip samples at 49716 Hz, scaled to -1..1 as the mixer scales them.</summary>
		public void Render(float[] pfOut, int piCount)
		{
			for (int liAt = 0; liAt < piCount; liAt++)
				pfOut[liAt] = NextSample() / 32768f;
		}
	}
}
