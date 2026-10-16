using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The heading conversions of the original, without any engine type (cleanup phase 1,
	/// 2026-10-11). The original measures a direction three ways: the object data's heading in
	/// EIGHTHS of a turn (0 to 7, 0 north, clockwise - a door frame, an arrow trap), a mobile's
	/// heading in 256ths (the eighth times 32 plus a fine part) and the player's in 65536ths
	/// (PLAYER.DAT 0x5A, UWWorldScale.FullCircleUnits). Our yaw is in degrees clockwise from
	/// north, so every conversion here is a scale by 45, 360/256 or 360/65536 - and, towards the
	/// original, a ROUNDING, halves to even as everywhere (UWUnits.RoundToInt) - except the
	/// 256-step heading of the player, which the original takes by a shift and
	/// DegreesToHeadingByte therefore TRUNCATES (decision 23 of Docs/CLEANUP-PLAN.md,
	/// 2026-10-12). Each function replaced copies that were numerically identical; the
	/// truncating UWUnits.DegreesToAngle stays for its own callers.
	///
	/// UWViewpoint builds Unity vectors from HeadingByteToDirection; the other functions are
	/// plain floats and ints and need no wrapper.
	/// </summary>
	public static class UWHeadings
	{
		/// <summary>Eighths per turn.</summary>
		public const int EighthsPerTurn = 8;

		/// <summary>Degrees per eighth of a turn.</summary>
		public const float DegreesPerEighth = 45f;

		/// <summary>Steps of the 256-unit heading of a mobile (UWCritterRecord.FullFacing,
		/// UWProjectileRecord.HeadingByte).</summary>
		public const int HeadingBytesPerTurn = 256;

		/// <summary>A heading in eighths (0 north, clockwise) in degrees - what the object build
		/// rotates a model by, what the arrow trap and the camera trap aim with.</summary>
		public static float EighthToDegrees(int piEighth)
		{
			return piEighth * DegreesPerEighth;
		}

		/// <summary>A heading in 256ths of a turn (0 north, clockwise) in degrees. Only the low
		/// byte counts; a heading written as eighth times 32 plus a fine part fits that.</summary>
		public static float HeadingByteToDegrees(int piHeading)
		{
			return (piHeading & 0xFF) * 360f / HeadingBytesPerTurn;
		}

		/// <summary>A heading in 256ths of a turn as a horizontal unit direction: X to the east, Z
		/// to the north (0 gives (0, 1), 64 gives (1, 0)). The one home of the formula that
		/// UWCritter and UWProjectileFlight each carried; UWViewpoint.HeadingByteToDirection makes
		/// the Vector3.</summary>
		public static (float X, float Z) HeadingByteToDirection(int piHeading)
		{
			float lfRadians = (piHeading & 0xFF) * (2f * (float)Math.PI / HeadingBytesPerTurn);

			return ((float)Math.Sin(lfRadians), (float)Math.Cos(lfRadians));
		}

		/// <summary>A yaw in degrees (clockwise from north) as the nearest eighth of a turn, 0 to
		/// 7: the heading of the object data for a direction the world gave - a thrown item's
		/// flight, the compass text of the debug overlay, the creature's facing for its picture.
		/// Rounded to the nearest eighth, halves to even; a yaw outside 0 to 360 is wrapped
		/// first.</summary>
		public static int DegreesToEighth(float pfDegrees)
		{
			return UWUnits.RoundToInt(UWUnits.Repeat(pfDegrees, 360f) / DegreesPerEighth) & (EighthsPerTurn - 1);
		}

		/// <summary>A yaw in degrees as one of the 256 headings, 0 to 255, by TRUNCATION: the
		/// player's 256-step heading in the original is the 65536-unit facing shifted down,
		/// PlayerFacing &gt;&gt; 8 (SET_FINEHEAD in PHYSICS.C; OBJPHYS.C reads a mobile's the same
		/// way, pp-&gt;heading &gt;&gt; 8), never rounded. The throw's heading and the compass disc
		/// read the camera this way - one conversion since 2026-10-12, decision 23 of
		/// Docs/CLEANUP-PLAN.md; until then the compass rounded (halves to even) and could sit
		/// one step ahead of the throw. UWProjectileWorld.AimFromDirection still ROUNDS: the
		/// original takes those headings from its arctangent table, not from the player's yaw.</summary>
		public static int DegreesToHeadingByte(float pfDegrees)
		{
			return (int)(UWUnits.Repeat(pfDegrees, 360f) * HeadingBytesPerTurn / 360f) & 0xFF;
		}

		/// <summary>The compass needle's step, 0 to 15, from a 256-step heading: the original's
		/// (v + 8 &amp; 0xFF) &gt;&gt; 4 in display_scr (INTERACT.C) - eight added to round to the
		/// nearest sixteenth, the byte wrap taking 248 and above back to north.</summary>
		public static int HeadingByteToCompassStep(int piHeading)
		{
			return ((piHeading + 8) & 0xFF) >> 4;
		}

		/// <summary>A yaw in degrees as the original's 16-bit angle, 0 to 65535, ROUNDED: the
		/// transform gives back an angle written as 45 degrees as 44.99998, and a truncation made
		/// that 8191 instead of 8192 - the easy turn then landed on the same eighth again and the
		/// compass disc's right arrow did nothing (per user, 2026-10-10; see
		/// UWPlayerMovement.fTransformYawToAngle). The save game writes the heading this way too,
		/// and since 2026-10-12 the creature animator's camera yaw as well (it floored before;
		/// decision 24 of Docs/CLEANUP-PLAN.md). UWUnits.DegreesToAngle is the TRUNCATING variant
		/// and stays for its own callers.</summary>
		public static int DegreesToAngleUnits(float pfDegrees)
		{
			return UWUnits.RoundToInt(UWUnits.Repeat(pfDegrees, 360f) * UWWorldScale.FullCircleUnits / 360f) & 0xFFFF;
		}
	}
}
