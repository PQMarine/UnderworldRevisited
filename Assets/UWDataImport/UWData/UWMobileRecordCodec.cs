namespace UWDataImport.UWData
{
	/// <summary>
	/// A THING IN FLIGHT IN THE LEVEL DATA (2026-10-19): the nineteen extra bytes of a mobile record
	/// that is no creature, read into the motion's UWMobileRecord and written back from it.
	///
	/// The original keeps a flying arrow, a thrown thing or a spell missile as a mobile object in its
	/// tile's list and on the active mobile list (Obj_Alloc appends it, UnderworldExhumed
	/// obj/OBJECTS.C:225-245), and its level block is a plain memory image (map/MAP.C:90-135), so a
	/// save holds it with its whole motion state. move_mobile (critter/AI.C:1539-1567) steps every
	/// entry of the active list, a non-creature through move_me_joe (critter/PATHFIND.C:174-198):
	/// get_phys_data reads the record (motion/OBJPHYS.C:211-274: heading byte 0x09, speed byte 0x13
	/// bits 0-6 times 0x2F, vertical speed (pitch - 16) &lt;&lt; 6 from byte 0x14, gravity byte 0x13
	/// bit 7, the fine position from words 0x0B, 0x0D, 0x0F), do_crit_phys moves it, set_phys_data
	/// writes it back (OBJPHYS.C:276-360) - the same step our UWMobileObjectMotion runs. Nothing in
	/// it depends on how the flight began: a loaded arrow flies on exactly as one just shot. Per user
	/// on the original, 2026-10-19: arrows shot during Freeze Time, saved and loaded in ours stayed in
	/// the air for good and one pointed the wrong way, as ours made a lying object of the record.
	///
	/// The offsets are UWNpcRecordCodec's (record offset 0x08 + n is byte n); for a non-creature the
	/// creature's goal, attitude and destination words hold the fine x, y and z (object.h). The bytes
	/// the motion does not own (0x0A bit 7, 0x11, 0x15 bits 0-6, 0x16 bits 0-3, 0x18 bits 5-7, 0x19,
	/// 0x1A bits 3-7) stay as they were read.
	/// </summary>
	public static class UWMobileRecordCodec
	{
		/// <summary>Byte 0x12, last_hit: who launched it (UWMobileRecord.Owner).</summary>
		public const int OwnerByte = UWNpcRecordCodec.AttackerByte;

		/// <summary>Byte 0x15 bit 7: collided with a mobile already (OBJ_B15_7).</summary>
		public const int CollidedBit = UWNpcRecordCodec.HasPathBit;

		/// <summary>Byte 0x1A bits 0-2: the heading the thing lies in once it comes to rest -
		/// mob_to_static's SET_HEADING(st, obj->whoami), OBJPHYS.C:466.</summary>
		public const int StaticHeadingMask = 0x7;

		/// <summary>
		/// The record of a mobile non-creature lying in the list of tile piTileX, piTileY, from its
		/// nineteen bytes (pyRaw, UWNpc.RawNpcBytes) and its first eight (the object: word 2's
		/// eighth, z and octant). The flight index is the host's to give.
		/// </summary>
		public static UWMobileRecord Read(UWObject pOObject, byte[] pyRaw, int piTileX, int piTileY)
		{
			UWMobileRecord lORecord = new UWMobileRecord
			{
				Object = pOObject,
				TileX = piTileX & UWNpcRecordCodec.HomeMask,
				TileY = piTileY & UWNpcRecordCodec.HomeMask,
				XPos = pOObject.XPos & 7,
				YPos = pOObject.YPos & 7,
				ZPos = pOObject.ZPos & UWObjectRecordCodec.ZPosMask,
				Octant = pOObject.Heading & UWObjectRecordCodec.HeadingMask
			};

			if (pyRaw == null || pyRaw.Length < UWNpcRecordCodec.ByteCount)
				return lORecord;

			lORecord.Hp = pyRaw[UWNpcRecordCodec.HitPointsByte];
			lORecord.HeadingByte = pyRaw[UWNpcRecordCodec.FineHeadingByte];
			lORecord.Phase = pyRaw[UWNpcRecordCodec.StateByte] & UWNpcRecordCodec.DueSlotMask;
			lORecord.ContactIndex = (pyRaw[UWNpcRecordCodec.StateByte] >> UWNpcRecordCodec.TileStateShift) & UWNpcRecordCodec.TileStateMask;
			lORecord.FineX = UWBinary.ReadU16(pyRaw, UWNpcRecordCodec.GoalWord);
			lORecord.FineY = UWBinary.ReadU16(pyRaw, UWNpcRecordCodec.AttitudeWord);
			lORecord.FineZ = UWBinary.ReadU16(pyRaw, UWNpcRecordCodec.DestinationWord);
			lORecord.Owner = pyRaw[OwnerByte];
			lORecord.SpeedByte = pyRaw[UWNpcRecordCodec.SpeedByte] & UWNpcRecordCodec.SpeedMask;
			lORecord.GravityBit = (pyRaw[UWNpcRecordCodec.SpeedByte] & UWNpcRecordCodec.GravityBit) != 0;
			lORecord.Period = pyRaw[UWNpcRecordCodec.IntervalByte] & UWNpcRecordCodec.IntervalMask;
			lORecord.VzField = (pyRaw[UWNpcRecordCodec.IntervalByte] >> UWNpcRecordCodec.PitchShift) & UWNpcRecordCodec.PitchMask;
			lORecord.CollidedWithMobile = (pyRaw[UWNpcRecordCodec.AnimationByte] & CollidedBit) != 0;
			lORecord.FineHeadingBits = pyRaw[UWNpcRecordCodec.FacingByte] & UWNpcRecordCodec.FacingResidualMask;
			lORecord.StaticHeading = pyRaw[UWNpcRecordCodec.WhoAmIByte] & StaticHeadingMask;

			return lORecord;
		}

		/// <summary>
		/// The nineteen bytes of the record: pyBase (the bytes read, or null for a flight that began
		/// in ours - Obj_Alloc does not clear a record, mob_init and missile_fire set what the motion
		/// reads, so the rest is 0 here) with the motion's fields written over it.
		/// </summary>
		public static byte[] Write(UWMobileRecord pORecord, byte[] pyBase)
		{
			byte[] lyRaw = pyBase != null && pyBase.Length >= UWNpcRecordCodec.ByteCount
				? (byte[])pyBase.Clone()
				: new byte[UWNpcRecordCodec.ByteCount];

			lyRaw[UWNpcRecordCodec.HitPointsByte] = (byte)pORecord.Hp;
			lyRaw[UWNpcRecordCodec.FineHeadingByte] = (byte)pORecord.HeadingByte;
			lyRaw[UWNpcRecordCodec.StateByte] = (byte)((lyRaw[UWNpcRecordCodec.StateByte] & UWNpcRecordCodec.AttitudeLockedBit)
				| ((pORecord.ContactIndex & UWNpcRecordCodec.TileStateMask) << UWNpcRecordCodec.TileStateShift)
				| (pORecord.Phase & UWNpcRecordCodec.DueSlotMask));
			UWBinary.Write16(lyRaw, UWNpcRecordCodec.GoalWord, pORecord.FineX & 0xFFFF);
			UWBinary.Write16(lyRaw, UWNpcRecordCodec.AttitudeWord, pORecord.FineY & 0xFFFF);
			UWBinary.Write16(lyRaw, UWNpcRecordCodec.DestinationWord, pORecord.FineZ & 0xFFFF);
			lyRaw[OwnerByte] = (byte)pORecord.Owner;
			lyRaw[UWNpcRecordCodec.SpeedByte] = (byte)((pORecord.SpeedByte & UWNpcRecordCodec.SpeedMask)
				| (pORecord.GravityBit ? UWNpcRecordCodec.GravityBit : 0));
			lyRaw[UWNpcRecordCodec.IntervalByte] = (byte)(((pORecord.VzField & UWNpcRecordCodec.PitchMask) << UWNpcRecordCodec.PitchShift)
				| (pORecord.Period & UWNpcRecordCodec.IntervalMask));
			lyRaw[UWNpcRecordCodec.AnimationByte] = (byte)((lyRaw[UWNpcRecordCodec.AnimationByte] & ~CollidedBit)
				| (pORecord.CollidedWithMobile ? CollidedBit : 0));

			int liHome = UWBinary.Read16(lyRaw, UWNpcRecordCodec.HomeWord) & UWNpcRecordCodec.PathSlotMask;

			liHome |= ((pORecord.TileY & UWNpcRecordCodec.HomeMask) << UWNpcRecordCodec.HomeYShift)
				| ((pORecord.TileX & UWNpcRecordCodec.HomeMask) << UWNpcRecordCodec.HomeXShift);
			UWBinary.Write16(lyRaw, UWNpcRecordCodec.HomeWord, liHome);

			lyRaw[UWNpcRecordCodec.FacingByte] = (byte)((lyRaw[UWNpcRecordCodec.FacingByte] & ~UWNpcRecordCodec.FacingResidualMask)
				| (pORecord.FineHeadingBits & UWNpcRecordCodec.FacingResidualMask));
			lyRaw[UWNpcRecordCodec.WhoAmIByte] = (byte)((lyRaw[UWNpcRecordCodec.WhoAmIByte] & ~StaticHeadingMask)
				| (pORecord.StaticHeading & StaticHeadingMask));

			return lyRaw;
		}

		/// <summary>Word 2 of the record in flight (z, octant, eighths), from the motion's fields:
		/// set_phys_data keeps them current with every step (OBJPHYS.C:302-304), and missile_fire
		/// sets the octant from the launch heading (combat/MISSILE.C:274).</summary>
		public static int PositionWord(UWMobileRecord pORecord)
		{
			return (pORecord.ZPos & UWObjectRecordCodec.ZPosMask)
				| ((pORecord.Octant & UWObjectRecordCodec.HeadingMask) << UWObjectRecordCodec.HeadingShift)
				| ((pORecord.YPos & UWObjectRecordCodec.FinePositionMask) << UWObjectRecordCodec.YPosShift)
				| ((pORecord.XPos & UWObjectRecordCodec.FinePositionMask) << UWObjectRecordCodec.XPosShift);
		}

		/// <summary>
		/// Whether a mobile object of the level data is IN FLIGHT: a non-creature on the active
		/// mobile list (move_mobile steps exactly that list) that is linked in a tile's list. A
		/// non-creature in a mobile slot off the list (our writer gives an item one when the static
		/// list is empty) lies still, as it would in the original.
		/// </summary>
		public static bool IsInFlight(UWObject pOObject, bool pbOnActiveList, bool pbInTileList)
		{
			return pOObject is UWNpc && pOObject.ID != 0 && !UWObjectIds.IsCreature(pOObject.ID)
				&& pbOnActiveList && pbInTileList;
		}
	}
}
