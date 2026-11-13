namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MAGIC SATELLITE (UW2 plan G1 part E3, 2026-11-13; Underworld Exhumed uw2 combat/SPELLS2.C creat_spell's case
	/// 6, critter/PATHFIND.C check_sat, motion/OBJPHYS.C mob_init and missile_newhit). The spell puts the satellite (item
	/// 0x1E, a missile) nine eighths ahead of the caster, 0x12 above the floor, heading a quarter turn to one side of the
	/// caster's facing, at a speed of 15 to 29 with 63 hit points and no gravity; its source (last_hit) is the caster.
	/// After every step that keeps it in flight it steers round the source: the tangent to the circle round it mixed with
	/// a pull back to about four eighths away, its pitch after the source's height, its speed kept at 15 at the least. It
	/// loses a hit point about one step in 32 and ends at once after striking a creature (the loner bit); it never strikes
	/// its source (missile_newhit). Only the second game has it; the first game's object 0x1E is none.
	/// </summary>
	public static class UWSatelliteRulesUw2
	{
		/// <summary>ITEM_SATELLITE.</summary>
		public const int ItemId = 0x1E;

		/// <summary>creat_spell's SET_SPEED(obj, rand() % 0xF + 0xF).</summary>
		public const int SpeedBase = 0xF;

		public const int SpeedRange = 0xF;

		/// <summary>creat_spell's z += 0x12 above the floor.</summary>
		public const int Rise = 0x12;

		/// <summary>check_sat's distance it pulls back to, in eighths.</summary>
		private const int OrbitDistance = 4;

		/// <summary>
		/// check_sat on the record of a satellite still in flight; pIHost finds the source by its index (Owner). A source
		/// that is gone ends the satellite (the original reads whatever record the index holds).
		/// </summary>
		public static void Steer(UWMobileRecord pORecord, IUWMotionWorld pIWorld, IUWMobileObjectHost pIHost)
		{
			if (pORecord == null || pIWorld == null || pIHost == null)
				return;

			if (!pIHost.TryGetSourceSpot(pORecord.Owner, out int liSourceX8, out int liSourceY8, out int liSourceZ))
			{
				pORecord.Hp = 0;

				return;
			}

			int liX8 = (pORecord.TileX << 3) + (pORecord.XPos & 7);
			int liY8 = (pORecord.TileY << 3) + (pORecord.YPos & 7);
			int liVector = UWHomingDartRules.GetTheta(liX8, liY8, liSourceX8, liSourceY8) & 0xFFFF;
			int liTheta = (liVector + 0x4000) & 0xFFFF;
			int liDiff = System.Math.Abs((liTheta >> 8) - (pORecord.HeadingByte & 0xFF));

			if (liDiff > 0x80)
				liDiff = 0xFF - liDiff;

			// The tangent the satellite already runs along, not the other one.
			if (liDiff > 0x40)
				liTheta = (liTheta + 0x8000) & 0xFFFF;

			int liDistance = System.Math.Abs(liX8 - liSourceX8) + System.Math.Abs(liY8 - liSourceY8);
			int liDivisor = 2;

			if (liDistance < OrbitDistance)
				liVector = (liVector + 0x8000) & 0xFFFF;
			else
				liDivisor = 8;

			int liTurn = (System.Math.Abs(liDistance - OrbitDistance) << 3) / liDivisor;

			if (liTurn > 0x40)
				liTurn = 0x40;

			int liHeading = UWHomingDartRules.MergeAngles(pORecord.HeadingByte & 0xFF, liTheta >> 8, 0x20);

			pORecord.HeadingByte = UWHomingDartRules.MergeAngles(liHeading, liVector >> 8, liTurn);

			int liPitch = pORecord.VzField & 0x1F;
			int liZPitch = ((liSourceZ - pORecord.ZPos + 0xF) / 8) + 0x10;
			int liNew = (liPitch + liZPitch) / 2;

			if ((pIWorld.Random() & 3) == 0)
			{
				if (liNew < 0xF)
					liNew++;
				else if (liNew > 0x11)
					liNew--;
				else
					liNew = liNew + (pIWorld.Random() & 3) - 1;
			}

			pORecord.VzField = liNew & 0x1F;

			if (pORecord.SpeedByte < SpeedBase)
				pORecord.SpeedByte = SpeedBase;

			if (pORecord.Loner)
				pORecord.Hp = 0;
			else if ((pIWorld.Random() & 0x1F) == 1 && pORecord.Hp > 0)
				pORecord.Hp--;
		}

		/// <summary>creat_spell's heading for the satellite: the swerved heading plus a quarter turn, to either side by
		/// rand() &amp; 1 (heading + (rand() &amp; 1) * 0x7F + 0x40).</summary>
		public static int StartHeading(int piSwervedHeading, int piRandom)
		{
			return (piSwervedHeading + ((piRandom & 1) * 0x7F) + 0x40) & 0xFF;
		}

		/// <summary>creat_spell's height: the floor + 0x12, one more above 0x78 (as it stands), at most 0x7F.</summary>
		public static int StartZ(int piFloorZ)
		{
			int liZ = piFloorZ + Rise;

			if (liZ > 0x78)
				liZ++;

			return System.Math.Min(liZ, 0x7F);
		}
	}
}
