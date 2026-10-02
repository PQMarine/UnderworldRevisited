namespace UWDataImport.UWData
{
	/// <summary>
	/// A regular tick driven by elapsed time (P0 of the engine separation, 2026-09-17).
	///
	/// THE CLOCK IS Advance(elapsedSeconds), not "seconds since start" (decided 2026-09-16): the
	/// host tells how much time has passed, the tick counts how many whole intervals that
	/// completes and keeps the rest. In the game Unity hands over the real frame time, a test
	/// driver always the same slice, which makes a run repeatable without the rules knowing.
	///
	/// Replaces the hand-written "timer += Time.deltaTime; if (timer &gt;= interval)" patterns,
	/// which also dropped the remainder: a 20-second tick reached after 20.3 seconds started the
	/// next one at zero, so over a long session the ticks drifted later.
	/// </summary>
	public sealed class UWTickClock
	{
		private double mdAccumulated;

		/// <summary>Seconds per tick. Zero or less never ticks.</summary>
		public float IntervalSeconds;

		public UWTickClock(float pfIntervalSeconds)
		{
			IntervalSeconds = pfIntervalSeconds;
		}

		/// <summary>Time collected towards the next tick.</summary>
		public double AccumulatedSeconds
		{
			get { return mdAccumulated; }
		}

		/// <summary>Adds elapsed time and returns how many whole ticks it completed (usually 0 or
		/// 1; more after a long pause of the host).</summary>
		public int Advance(float pfElapsedSeconds)
		{
			if (IntervalSeconds <= 0f || pfElapsedSeconds <= 0f)
				return 0;

			mdAccumulated += pfElapsedSeconds;

			int liTicks = (int)(mdAccumulated / IntervalSeconds);

			mdAccumulated -= liTicks * (double)IntervalSeconds;

			return liTicks;
		}

		/// <summary>Starts with this much time already collected (at most one interval, so the
		/// first Advance gives at most one tick at once).</summary>
		public void Preload(double pdSeconds)
		{
			mdAccumulated = System.Math.Max(0d, System.Math.Min(pdSeconds, IntervalSeconds));
		}

		/// <summary>Starts counting from zero again.</summary>
		public void Reset()
		{
			mdAccumulated = 0d;
		}
	}
}
