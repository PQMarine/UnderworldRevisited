namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MOTION STATE OF A MOBILE OBJECT - the bytes of the original's 27-byte mobile record
	/// that the motion reads and writes (read 2026-10-05 for the motion rework, stage 1: a thrown
	/// object or missile in flight). The object itself (item id, quality, quantity, contents, the
	/// flags) is the port's UWObject; the record adds what the original keeps only while the
	/// object is mobile: the fine position, the heading byte, the quantised speed and vertical
	/// speed, the gravity bit, the contact state, the schedule and the owner.
	/// </summary>
	public sealed class UWMobileRecord
	{
		/// <summary>The object - word 0 (id and flags), word 4 (quality), word 6 (owner, quantity
		/// or contents).</summary>
		public UWObject Object;

		/// <summary>The object index the collision scans know it by; the host assigns it.</summary>
		public int Index;

		public int ItemId => Object != null ? Object.ID : 0;

		/// <summary>Major class 1: a creature (the player included).</summary>
		public bool IsCreature => ((ItemId >> 6) & 7) == 1;

		/// <summary>Word +0x16: the home tile - for a mobile the tile it is linked in.</summary>
		public int TileX;

		public int TileY;

		/// <summary>Word +2: the eighth inside the tile and the coarse height.</summary>
		public int XPos;

		public int YPos;

		public int ZPos;

		/// <summary>Word +2 bits 7-9: the heading octant (the launch octant of a missile, the
		/// static heading of a knocked-loose object).</summary>
		public int Octant;

		/// <summary>Words +0x0B, +0x0D, +0x0F: the fine position of a non-creature - tile * 256 +
		/// eighth * 32 + 0..31, and coarse z * 8 + 0..7.</summary>
		public int FineX;

		public int FineY;

		public int FineZ;

		/// <summary>Byte +9: the heading in 1/256 of a turn.</summary>
		public int HeadingByte;

		/// <summary>Byte +0x18 bits 0-4: the fine heading of a creature (set at the launch).</summary>
		public int FineHeadingBits;

		/// <summary>Byte +0x13 bits 0-6: the speed / 0x2F; bit 7: gravity -4.</summary>
		public int SpeedByte;

		public bool GravityBit;

		/// <summary>Byte +0x14 bits 3-7: the vertical speed as 16 + vz / 64, 0..31; bits 0-2: the
		/// period in clock units.</summary>
		public int VzField = 16;

		public int Period = 2;

		/// <summary>Byte +0x0A bits 0-3: the clock value the next step is due at; bits 4-6: the
		/// contact state as the 3-bit field (UWMotionTables.ContactStateIndex).</summary>
		public int Phase;

		public int ContactIndex;

		/// <summary>Byte +8: the hit points - a missile's life, a thrown item's quality.</summary>
		public int Hp;

		/// <summary>Byte +0x12: who launched it (1 the player, a creature's index, 0 nobody).</summary>
		public int Owner;

		/// <summary>Byte +0x15 bit 7: collided with a mobile already.</summary>
		public bool CollidedWithMobile;

		/// <summary>Byte +0x1A bits 0-2: the heading the object has as a static object again.</summary>
		public int StaticHeading;

		/// <summary>The record as a body of the collision scans.</summary>
		public UWMotionBody ToBody()
		{
			return new UWMotionBody
			{
				Index = Index,
				ItemId = ItemId,
				XPos = XPos,
				YPos = YPos,
				ZPos = ZPos,
				IsMobile = true,
				IsCreature = IsCreature,
				CollidedWithMobile = CollidedWithMobile,
				Direction = Octant & 7
			};
		}
	}
}
