using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// HOW A FRACTION BECOMES A WHOLE NUMBER, named (cleanup phase 10, wave 3, 2026-10-26): every
	/// place of the engine-free layer where a rule turns a float into an int says which of these
	/// it means, so that a mirror in another language does the same - C++'s std::round rounds
	/// halves away from zero, C#'s Math.Round to even, and a cast truncates in both.
	///
	/// The original computes in integers throughout and rounds only the way its instructions do:
	/// idiv truncates towards zero (C#'s integer division, the same), an arithmetic shift right
	/// floors (C#'s >> on an int, the same). Those stay written as divisions and shifts. The
	/// floats are ours - the world in Unity units, the Smooth mode's fractions of a frame, the
	/// time in seconds - and where one of them decides a rule, it rounds through here:
	///
	/// - HalfToEven: to the nearest, halves to the even neighbour - what Unity's Mathf.RoundToInt
	///   did and the world conversions (UWUnits.RoundToInt) were tuned on (a 45-degree yaw that
	///   the transform hands back as 44.99998 must still be the eighth it was).
	/// - Floor: down, also below zero - where a count of whole steps is taken from a running
	///   total (ticks from seconds, a tile from a coordinate).
	/// - Truncate: towards zero - where the original divides (momentum * 10 / speed) or shifts a
	///   value that is never negative, and ours has the quotient as a float.
	///
	/// Each takes a double; a float argument widens exactly, so a float computation keeps its
	/// result to the bit.
	/// </summary>
	public static class UWRounding
	{
		/// <summary>To the nearest whole number, halves to even (2.5 -> 2, 3.5 -> 4, -2.5 -> -2).</summary>
		public static int HalfToEven(double pdValue)
		{
			return (int)Math.Round(pdValue, MidpointRounding.ToEven);
		}

		/// <summary>The largest whole number not above the value (2.7 -> 2, -2.3 -> -3).</summary>
		public static int Floor(double pdValue)
		{
			return (int)Math.Floor(pdValue);
		}

		/// <summary>The whole part, towards zero (2.7 -> 2, -2.7 -> -2).</summary>
		public static int Truncate(double pdValue)
		{
			return (int)pdValue;
		}
	}
}
