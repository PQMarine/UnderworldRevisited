using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// An object as the motion core's collision scan sees it (ScanForCollisions_seg026_BF6): what
	/// the original reads from the object record and the level's tile lists. The host fills these
	/// for a tile; the COMOBJ properties the core reads itself by the item id.
	/// </summary>
	public struct UWMotionBody
	{
		/// <summary>The object index; a mover never collides with itself. 1 is the player.</summary>
		public int Index;

		/// <summary>Word 0 bits 0-8.</summary>
		public int ItemId;

		/// <summary>The eighth inside the tile, 0..7, and the coarse height 0..127.</summary>
		public int XPos;

		public int YPos;

		public int ZPos;

		/// <summary>A mobile record (index below 0x100 in the original): creatures, the player, a
		/// flying object.</summary>
		public bool IsMobile;

		/// <summary>Major class 1 (the player included).</summary>
		public bool IsCreature;

		/// <summary>Byte +0x15 bit 7 of a mobile non-creature: it has collided with a mobile already
		/// and is skipped by the scans of other mobiles.</summary>
		public bool CollidedWithMobile;

		/// <summary>Word 2 bits 7-9: the heading octant (a lying object's, a creature's facing); the
		/// tile routes read a closed door's orientation from it (UWTileTraverse).</summary>
		public int Direction;
	}

	/// <summary>
	/// WHAT THE MOTION CORE ASKS OF THE WORLD (UWMotionCore, stage 1 of the motion rework,
	/// 2026-10-05): the tiles, the objects of a tile, the random numbers, and the side effects of
	/// a collision that belong to the game - object use, triggers, the missile's hit, the push of
	/// what was struck, sounds. Everything in the original's units: tiles 0..63, eighths, coarse
	/// z, 16-bit headings.
	///
	/// The host is the Unity side; the self-check builds a fake with a small tile map and a
	/// scripted RNG.
	/// </summary>
	public interface IUWMotionWorld
	{
		/// <summary>The tile's type (0 solid, 1 open, 2-5 diagonals, 6-9 slopes), floor height
		/// nibble 0..15 and terrain 0..3 (TERRAIN.DAT floor value &gt;&gt; 4: 1 water, 2 lava). False
		/// off the map; the core then treats the tile as solid.</summary>
		bool TryGetTile(int piTileX, int piTileY, out int piType, out int piFloorNibble, out int piTerrain);

		/// <summary>The objects linked in this tile's list, in the list's order, appended to
		/// pOInto. The mover itself may be among them; the core skips its own index.</summary>
		void GetBodiesInTile(int piTileX, int piTileY, List<UWMotionBody> pOInto);

		/// <summary>RNG_seg005_DE7: 0..0x7FFF.</summary>
		int Random();

		// ------------------------------------------------- CollideObjects_seg029_29EE_173

		/// <summary>The struck object USES ITSELF on the mover (COMOBJ byte 6 bit 1 of the struck
		/// one): the glowing rock joining the carried one.</summary>
		void UseOnMover(int piOtherIndex, int piMoverIndex, int piHitTileX, int piHitTileY);

		/// <summary>The struck object is a trigger (class 6): the move trigger fires. Returns the
		/// CollideObjects result it stands for (2 nothing, 4 obstacle, 8 end the step, 0x10 stop).</summary>
		int TriggerMove(int piMoverIndex, int piOtherIndex, int piHitTileX, int piHitTileY);

		/// <summary>The mover is a missile (COMOBJ byte 6 bit 1 of the mover) and strikes the
		/// object: MissileAttackHit_seg022_14BF. True when the struck object is gone afterwards
		/// (Tybal's orb) - the mover then stops dead.</summary>
		bool MissileHits(int piMoverIndex, int piOtherIndex, int piMoverTileX, int piMoverTileY);

		/// <summary>The momentum transfer seg029_29EE_3: the struck object gets the mover's heading,
		/// speed 0xEB and this vertical speed - a creature stores them in its record, a lying object
		/// is knocked loose (static to mobile) and moves on its own from the next update.</summary>
		void PushObject(int piOtherIndex, int piHeading, int piSpeed, int piVz, int piHitTileX, int piHitTileY);

		/// <summary>LoadSoundAtCoordinate(id, x, y, volume argument): the position in eighths.</summary>
		void PlaySoundAt(int piSound, int piX8, int piY8, int piVolume);
	}
}
