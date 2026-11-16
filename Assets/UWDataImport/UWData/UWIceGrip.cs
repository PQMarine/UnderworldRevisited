using System;

namespace UWDataImport.UWData
{
	/// <summary>The setting IceGrip: how the second game's ice holds the feet.</summary>
	public enum UWIceGripEnum
	{
		/// <summary>As the original: the ice slides, and at speed the steering does nothing.</summary>
		Off = 0,

		/// <summary>The ice still slides, but the steering always takes at least a sixteenth of the way an
		/// original frame: a drift of about two seconds.</summary>
		Half = 1,

		/// <summary>Flat ice grips like ground: no slide, no bounce off walls. Ice slopes stay the original's under every
		/// mode (UWPlayerMotion.fIceAndCurrents, per user 2026-11-15: puzzles may need their push).</summary>
		Full = 2
	}

	/// <summary>
	/// THE ICE GRIP (per user 2026-11-14, a gameplay aid: "the sliding is extremely annoying"; for every motion
	/// mode, Original, Smooth and Modern, and both schemes, off by default). The original's rule stays the path
	/// (UWPlayerMotion.fIceAndCurrents, PHYSICS.C:405-428): HALF only raises the strength munge_vectors blends
	/// with to at least HalfStrength, so the slide, the bounce and the slope push stay; FULL makes the ice tile
	/// count as ice without effects (the floor bits 7 of the original) and slides along walls as on ground.
	/// Read live at every frame. Only the second game has ice.
	/// </summary>
	public static class UWIceGrip
	{
		/// <summary>Where the setting is read (the Unity layer's settings; the self-check its own); null reads Off.</summary>
		public static Func<UWIceGripEnum> ModeSource { get; set; }

		public static UWIceGripEnum Mode => ModeSource == null ? UWIceGripEnum.Off : ModeSource();

		/// <summary>Half's least strength per original frame: a sixteenth of munge_vectors' whole way (0x40), so
		/// steering and braking take about two thirds after a second and nearly all after three. 0x10 (a quarter)
		/// until 2026-11-15, then 0x08; per user twice the same day Half should slide more, still steerable.</summary>
		public const int HalfStrength = 0x04;

		/// <summary>The ice grips like ground (Full).</summary>
		public static bool AsGround => Mode == UWIceGripEnum.Full;

		/// <summary>
		/// HALF'S BLEND (per user 2026-11-15): as munge_vectors' ice kind - the first motion moved towards the second
		/// by strength / 0x40 - but in exact numbers. The original's loses about a sixteenth of the speed every call
		/// (the table sine over 0x800 at most 15, the sum over 16), harmless where the original blends only at low
		/// speed, a brake on the run where Half blends at every speed. Headings in the full circle of 16 bits (0
		/// north, clockwise); standing keeps the first heading.
		/// </summary>
		public static void Blend(int piHeading, int piSpeed, int piHeading2, int piSpeed2, int piStrength,
			out int piOutHeading, out int piOutSpeed)
		{
			if (piStrength <= 0)
			{
				piOutHeading = piHeading;
				piOutSpeed = piSpeed;

				return;
			}

			double ldK = Math.Min(piStrength, UWMotionVariantUw2.MungeFull) / (double)UWMotionVariantUw2.MungeFull;
			double ldA1 = piHeading * Math.PI * 2.0 / 65536.0;
			double ldA2 = piHeading2 * Math.PI * 2.0 / 65536.0;
			double ldX1 = Math.Sin(ldA1) * piSpeed;
			double ldY1 = Math.Cos(ldA1) * piSpeed;
			double ldX = ldX1 + (((Math.Sin(ldA2) * piSpeed2) - ldX1) * ldK);
			double ldY = ldY1 + (((Math.Cos(ldA2) * piSpeed2) - ldY1) * ldK);

			piOutSpeed = (int)Math.Round(Math.Sqrt((ldX * ldX) + (ldY * ldY)));
			piOutHeading = piOutSpeed > 0 ? (int)Math.Round(Math.Atan2(ldX, ldY) * 65536.0 / (Math.PI * 2.0)) & 0xFFFF : piHeading;
		}

		/// <summary>The original's strength per original frame under the mode: Off as it is, Half at least
		/// HalfStrength, Full the whole way.</summary>
		public static int Strength(int piOriginal, UWIceGripEnum peMode)
		{
			switch (peMode)
			{
				case UWIceGripEnum.Half:
					return Math.Max(piOriginal, HalfStrength);
				case UWIceGripEnum.Full:
					return UWMotionVariantUw2.MungeFull;
				default:
					return piOriginal;
			}
		}
	}
}
