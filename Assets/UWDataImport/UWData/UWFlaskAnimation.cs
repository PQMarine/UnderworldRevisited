namespace UWDataImport.UWData
{
	/// <summary>
	/// The bubbling of a flask, THE ORIGINAL'S RULE IN BOTH GAMES (2026-11-25, UW2 beta gate 220.7, per user: the second
	/// game's flasks did not bubble, and one path for both games). Underworld Exhumed uw1 and uw2 ui/PANELS.C have the same
	/// code: update_screen's slow update (every 64 ticks of the 1/256 s clock, StepSeconds) draws rand() once and, below
	/// 0x666 of its 0x8000, sets the flask of the number's low bit bubbling (UWFlaskGauge draws it, the one draw for both
	/// flasks); adjust_flasks then shows that flask's bubble pictures, one a slow update, at LEVELS 1 TO 7 of the twelve
	/// ("n &lt; 8 &amp;&amp; n &gt; 0") - eleven of them, bub_frame stepping before it draws, so the set's first picture never
	/// shows - and on the slow update after the last the plain level again. Outside levels 1 to 7 the flask keeps bubbling
	/// and goes on when its level comes back. The level's own step (UWFlaskGauge) comes first in the same call, so a
	/// moving flask bubbles too.
	///
	/// Until then the first game's rule came from the reference (uimanager_flasks.AnimateFlasks, 2026-09-17): an eighth of
	/// a second a step, a 1 in 32 chance for each flask on its own, and no bubbles while a level moved.
	///
	/// The pictures sit in FLASKS.GR right behind the thirteen fill pictures of their colour (UWFlasks,
	/// UWClassicLayoutUw2.Flasks.BubbleFirst): red 13 to 24, mana 38 to 49, poison 63 to 74.
	/// </summary>
	public sealed class UWFlaskAnimation
	{
		/// <summary>Seconds per step: update_screen's slow update, 64 ticks of the 1/256 s clock.</summary>
		public const float StepSeconds = 64f / 256f;

		public const int FrameCount = 12;

		/// <summary>The first picture a bubbling shows - see the class comment.</summary>
		public const int FirstShownFrame = 1;

		/// <summary>Below and above this the flask does not bubble (adjust_flasks).</summary>
		public const int LowestLevel = 1;

		public const int HighestLevel = 7;

		/// <summary>update_screen's draw below this of UWOriginalRandom.Range starts a bubbling, its low bit the flask.</summary>
		public const int StartChance = 0x666;

		private bool mbBubbling;

		/// <summary>bub_frame less bub_first: 0 for the plain level, 1 to FrameCount - 1 a bubble picture.</summary>
		private int miFrame;

		/// <summary>bubbling[]: the flask's bubbles run, or wait for a level of 1 to 7.</summary>
		public bool IsBubbling => mbBubbling;

		/// <summary>The bubble picture shown at piLevel, or -1 for none (not bubbling, between pictures or out of levels 1
		/// to 7).</summary>
		public int FrameAt(int piLevel)
		{
			return mbBubbling && miFrame > 0 && piLevel >= LowestLevel && piLevel <= HighestLevel ? miFrame : -1;
		}

		/// <summary>update_screen's bubbling[r &amp; 1] = 1 (no effect while it already bubbles).</summary>
		public void Start()
		{
			mbBubbling = true;
		}

		/// <summary>adjust_flasks' bubble part at the level after this update's step; true when the picture changed.</summary>
		public bool Step(int piLevel)
		{
			if (!mbBubbling || piLevel < LowestLevel || piLevel > HighestLevel)
				return false;

			if (miFrame == FrameCount - 1)
			{
				miFrame = 0;
				mbBubbling = false;

				return true;
			}

			miFrame++;

			return true;
		}

		public void Stop()
		{
			mbBubbling = false;
			miFrame = 0;
		}
	}
}
