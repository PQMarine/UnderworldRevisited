using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Plays an XMI sequence: sends the events to the sound driver
	/// (UWAdlibMusicDriver) at the right time.
	///
	/// TIME: XMI runs at a FIXED 120 ticks per second. The pieces do carry tempo events
	/// (meta 0x51, AW10 even two), but they do not count - the converter of the
	/// Miles library has already folded the tempo into the tick times, the events
	/// are just a leftover of that. MEASURED IN THE ORIGINAL (per user, 2026-09-11): "Armed"
	/// lasts 46 seconds there, which is the 5469 ticks at 120 per second; with the
	/// tempo value of 100 beats it would have been 55. Until then the sequencer honoured
	/// the tempo, and "Dark Abyss" ran more than twice as slow as in the game.
	///
	/// LOOPS: controllers 116 and 117 of the XMIDI extension (FOR/NEXT) - AW01 uses
	/// them - are executed: 116 remembers the position together with a counter (0 means endless),
	/// 117 jumps back as long as the counter has not run out. Whether the whole piece
	/// repeats is set by Loop.
	///
	/// The sequencer is driven by the sound thread per sample (Advance).
	/// </summary>
	public class UWXmiSequencer
	{
		private const int TicksPerQuarter = 60;

		private const int DefaultTempo = 500000;

		private struct LoopFrame
		{
			public int StartIndex;

			public int Remaining;

			public bool Infinite;
		}

		private readonly List<UWXmi.Event> mOEvents;

		private readonly int miSampleRate;

		private double mfTicksPerSample;

		private double mfTickPosition;

		private int miNext;

		private readonly Stack<LoopFrame> mOLoops = new Stack<LoopFrame>();

		public bool Loop { get; set; }

		public bool IsFinished { get; private set; }

		public UWXmiSequencer(UWXmi.Sequence pOSequence, int piSampleRate)
		{
			miSampleRate = piSampleRate;
			mOEvents = pOSequence != null ? pOSequence.Events : new List<UWXmi.Event>();
			fSetTempo(DefaultTempo);
			IsFinished = mOEvents.Count == 0;
		}

		private void fSetTempo(int piTempo)
		{
			if (piTempo <= 0)
				piTempo = DefaultTempo;

			double lfTicksPerSecond = TicksPerQuarter * 1000000.0 / piTempo;
			mfTicksPerSample = lfTicksPerSecond / miSampleRate;
		}

		/// <summary>Advance by this many samples; events that are due go to the driver.</summary>
		public void Advance(int piSamples, UWAdlibMusicDriver pODriver)
		{
			if (IsFinished)
				return;

			mfTickPosition += piSamples * mfTicksPerSample;

			int liGuard = 0;

			while (miNext < mOEvents.Count && mOEvents[miNext].Tick <= mfTickPosition && liGuard++ < 10000)
			{
				UWXmi.Event lOEvent = mOEvents[miNext];
				miNext++;

				if (lOEvent.Status == 0xFF)
				{
					// Tempo (0x51) deliberately ignored - see class comment.
					if (lOEvent.Data1 == 0x2F)
						break;

					continue;
				}

				if ((lOEvent.Status & 0xF0) == 0xB0 && lOEvent.Data1 == 116)
				{
					mOLoops.Push(new LoopFrame
					{
						StartIndex = miNext,
						Remaining = lOEvent.Data2,
						Infinite = lOEvent.Data2 == 0
					});

					continue;
				}

				if ((lOEvent.Status & 0xF0) == 0xB0 && lOEvent.Data1 == 117)
				{
					if (mOLoops.Count > 0)
					{
						LoopFrame lOLoop = mOLoops.Pop();

						if (lOLoop.Infinite || --lOLoop.Remaining > 0)
						{
							mOLoops.Push(lOLoop);
							miNext = lOLoop.StartIndex;
							mfTickPosition = mOEvents[miNext < mOEvents.Count ? miNext : mOEvents.Count - 1].Tick;
						}
					}

					continue;
				}

				pODriver.Handle(lOEvent.Status, lOEvent.Data1, lOEvent.Data2);
			}

			if (miNext >= mOEvents.Count)
			{
				if (Loop && mOEvents.Count > 0)
				{
					pODriver.AllNotesOff();
					miNext = 0;
					mfTickPosition = 0;
					mOLoops.Clear();
					fSetTempo(DefaultTempo);
				}
				else
				{
					IsFinished = true;
					pODriver.AllNotesOff();
				}
			}
		}
	}
}
