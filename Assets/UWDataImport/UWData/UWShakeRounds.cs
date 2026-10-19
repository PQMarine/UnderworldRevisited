using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SCREEN SHAKE'S ROUNDS as the original runs them (UnderworldExhumed motion/PLAYMOVE.C,
	/// set_effect and the effects round of the player): set_effect puts a channel's amount into
	/// its BYTE counter (combEfflen for the combat shake 0x20, tremEfflen for the tremor 0x40)
	/// and sets the channel's bit. Every round then, per running channel, tests the counter for 0
	/// BEFORE it decrements it (`if (len-- == 0)` clears the bit) and takes the strength from the
	/// counter AFTER the decrement: the tremor len / 10, at most 8, the combat shake len / 8, at
	/// most 3; a channel that is not running stands at 1, and the round's strength is the sum.
	///
	/// So the round in which a counter runs out sees it wrapped to 255 and shakes at full
	/// strength: EVERY SHAKE ENDS WITH A STRONG JOLT, and a combat shake of level 0
	/// (set_effect(0x20, 0) after a hit of less than 4 past the armour, COMBAT.C do_damage) is that
	/// jolt alone. Per user on the original, 2026-10-19: a trap's arrow doing 3 shook the view, and
	/// one of level 1 visibly; ours showed nothing - it divided both channels by 10 and simply
	/// stopped at 0, without the last round.
	///
	/// The rounds run on our fixed tick (UWScreenShake), not once per frame as in the original
	/// (Docs/DEVIATIONS.md, Rules).
	/// </summary>
	public sealed class UWShakeRounds
	{
		/// <summary>The combat shake, the original's bit 0x20.</summary>
		public const int CombatChannel = 0;

		/// <summary>The tremor (drink, Tremor, the quake), the original's bit 0x40.</summary>
		public const int TremorChannel = 1;

		public const int CombatDivisor = 8;

		public const int CombatMaximum = 3;

		public const int TremorDivisor = 10;

		public const int TremorMaximum = 8;

		/// <summary>What a channel that is not running adds to the round (amp and phase start at 1).</summary>
		public const int IdleStrength = 1;

		private readonly int[] miCounter = new int[2];

		private readonly bool[] mbRunning = new bool[2];

		public bool IsRunning(int piChannel)
		{
			return piChannel >= 0 && piChannel < mbRunning.Length && mbRunning[piChannel];
		}

		public bool AnyRunning
		{
			get { return mbRunning[CombatChannel] || mbRunning[TremorChannel]; }
		}

		/// <summary>set_effect: the amount into the channel's byte counter, the channel running. A
		/// running channel is overwritten, a shorter hit shortens it.</summary>
		public void Start(int piChannel, int piAmount)
		{
			if (piChannel < 0 || piChannel >= mbRunning.Length)
				return;

			miCounter[piChannel] = piAmount & 0xFF;
			mbRunning[piChannel] = true;
		}

		/// <summary>One round: the strength of the shake in it, 0 when no channel ran.</summary>
		public int Round()
		{
			if (!AnyRunning)
				return 0;

			int liTremor = IdleStrength;
			int liCombat = IdleStrength;

			if (mbRunning[TremorChannel])
				liTremor = Math.Min(fStep(TremorChannel) / TremorDivisor, TremorMaximum);

			if (mbRunning[CombatChannel])
				liCombat = Math.Min(fStep(CombatChannel) / CombatDivisor, CombatMaximum);

			return liTremor + liCombat;
		}

		/// <summary>`if (len-- == 0)`: the test on the old value ends the channel, the decrement
		/// wraps the byte, and the round reads the new value.</summary>
		private int fStep(int piChannel)
		{
			if (miCounter[piChannel] == 0)
				mbRunning[piChannel] = false;

			miCounter[piChannel] = (miCounter[piChannel] - 1) & 0xFF;

			return miCounter[piChannel];
		}
	}
}
