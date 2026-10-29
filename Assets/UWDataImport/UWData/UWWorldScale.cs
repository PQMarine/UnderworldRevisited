namespace UWDataImport.UWData
{
	/// <summary>
	/// The measures of our world, in one engine-free place (P0 of the engine separation,
	/// 2026-09-17). Until then they lived on UWLevelMeshBuilder and UWObjectSpawner, so every
	/// rule that needed a tile size had to reach into the Unity build code.
	///
	/// OUR WORLD UNIT is a quarter of the original's: a tile is 64 of ours and 256 of the
	/// original, a sub-tile step 8 against 32, a zpos step 2 against 8. See UWUnits for the
	/// conversions.
	/// </summary>
	public static class UWWorldScale
	{
		/// <summary>Tiles per map axis.</summary>
		public const int TilesPerAxis = 64;

		/// <summary>Edge length of a tile, and distance between two tile centres.</summary>
		public const float TileSize = 64f;

		/// <summary>Half the edge length of a tile. Tile n has its centre at n * TileSize, so its
		/// lower corner lies half a tile before that.</summary>
		public const float TileHalfSize = 32f;

		/// <summary>Height of the ceiling above height zero.</summary>
		public const float CeilingHeight = 256f;

		/// <summary>One sub-tile step (xpos/ypos 0 to 7): an eighth of a tile. In the original a
		/// tile is 256 units and a step 32, so exactly one eighth; the collision code uses the
		/// same unit (obj.xpos + (tileX &lt;&lt; 3)).
		///
		/// THE HIGHEST VALUE THEREFORE DOES NOT REACH THE TILE EDGE: 7 times 8 is 56 out of 64,
		/// leaving eight units of clearance. That is exactly the point - an item at the edge of
		/// its tile would otherwise have its image stuck in the neighbouring wall.
		///
		/// Until 2026-09-06 this was 9, so that values 0 to 7 spanned the whole tile width. The
		/// user found the bug on an axe on level 3, tile 48/55: it has xpos 7 and ypos 7, and the
		/// tile is solid both to the east and to the north. With 9 it ended up 0.9 units before
		/// the edge and was stuck in the wall; in the original it is not. Things that hang ON A
		/// WALL still use the 9 (UWObjectSpawner.DecalSubTileScale): a plaque has to reach the
		/// wall face. Until the cleanup of 2026-10-11 this lived on UWObjectSpawner.SubTileScale.</summary>
		public const float SubTileStep = 8f;

		/// <summary>Where an object stands within its eighth: 0xF of the 32 original units of a
		/// step, 3.75 of our 8. The original computes a position as
		/// (tile &lt;&lt; 8) + (xpos &lt;&lt; 5) + 0xF (UnderworldGodot, taken from UW.EXE: object
		/// creation, projectiles, spells), the same for x and y. We left it out until 2026-09-17:
		/// in a comparison in front of Drog (SAVE4 level 1) the wall plate had to move 4 to 5
		/// units along the wall and Drog stood too far away (per user with screenshots). An
		/// object at xpos 7 now lies at 59.75 of 64, still clear of the neighbouring wall (see
		/// the axe under SubTileStep).</summary>
		public const float SubTileOffset = 3.75f;

		/// <summary>One zpos step (0 to 127). A floor height level is 8 zpos steps, 16 units.</summary>
		public const float ZPosStep = 2f;

		/// <summary>One unit of the original in our units.</summary>
		public const float UnitsPerOriginalUnit = 0.25f;

		/// <summary>A full circle in the original's angle measure.</summary>
		public const float FullCircleUnits = 65536f;

		/// <summary>The eye above the feet: 0xA4 of the original's units, 41 of ours (Underworld
		/// Exhumed uw1 motion/PLAYMOVE.C get_eye, cPlayer->z = PN.z + 0xA4; the same in uw2). Until
		/// 2026-10-29 the camera stood where the scene put it, 40.05 above the feet (per user beside the
		/// original in front of Drog: our view too low).</summary>
		public const float EyeHeight = 0xA4 * UnitsPerOriginalUnit;

		/// <summary>
		/// THE VIEW'S OWN OFFSET FROM THE EYE (2026-10-29, per user beside the original: our view stood
		/// too low and too far right, the same to the screen's side whichever way the player looked).
		/// The player's camera is head_view (Underworld Exhumed uw1 3d/CAMERA.ASM; the only camera the
		/// game uses, docs/FINDINGS.md "One camera"): it turns its view record's offset vector by the eye's
		/// matrix (mm3x9t) and adds it to the eye. That record is UW.EXE's seg051:0006 (the view
		/// description at seg051:0000, which 104h selects): -8 across and 11 up, 0 along the view, in
		/// the original's units; its distance word at +1Eh is 0, so the vector stays as stored. So the
		/// picture is seen from 2 of our units left of the eye and 2.75 above it, turned with the
		/// view's pitch.
		/// </summary>
		public const float ViewOffsetAcross = -8 * UnitsPerOriginalUnit;

		public const float ViewOffsetUp = 11 * UnitsPerOriginalUnit;
	}
}
