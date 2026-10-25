namespace UWDataImport.UWData
{
	/// <summary>
	/// WHO CASTS A GROUND BLOB, AND HOW BIG (cleanup step 6.4, 2026-10-24): one rule for the
	/// Remastered blobs (UWRemasterGroundShadows) and the palette path's ground shadows
	/// (UWPaletteEffects), which until this step each carried the same filter and the same
	/// numbers.
	///
	/// WHICH OBJECTS: everything standing in the world as a sprite (the billboard shaders, asked on
	/// the Unity side by UWGroundShadowCasters), except the light sources (UWLightSourceTable - a
	/// campfire casting a dark blob would be wrong) and what flies (per user, 2026-10-04: the blob
	/// flew along under spell missiles, thrown things, arrows; a thrown thing that lands is a new
	/// object and gets its blob). Wall decorations, doors and 3D models get none.
	///
	/// HOW BIG: as wide as the collider in plan view. For items that is the image width, for
	/// creatures the radius from COMOBJ.DAT (UWObjectSpawner.fApplyCritterFootprint) - the image
	/// width of a goblin would be more than a tile. Never smaller than MinDiameter.
	///
	/// HOW DARK is each path's own: the Remastered blob's strength (RemasterGroundShadow, on its
	/// material) and the palette path's levels (PaletteGroundShadows, up to MaxPaletteLevels).
	/// </summary>
	public static class UWGroundShadowRule
	{
		/// <summary>The smallest blob's diameter - a coin or a key would otherwise get a barely
		/// visible dot.</summary>
		public const float MinDiameter = 10f;

		/// <summary>The width of a sprite without a collider.</summary>
		public const float FallbackWidth = 16f;

		/// <summary>The most levels a palette ground shadow may darken by.</summary>
		public const float MaxPaletteLevels = 6f;

		/// <summary>Whether a world object casts a blob, as far as the object itself decides (the
		/// caller adds "stands as a sprite").</summary>
		public static bool Casts(int piObjectId, bool pbInFlight)
		{
			return !pbInFlight && !UWLightSourceTable.IsLightSource(piObjectId);
		}

		/// <summary>The caster's width in plan view: the larger side of its collider, or
		/// FallbackWidth.</summary>
		public static float Width(bool pbHasCollider, float pfSizeX, float pfSizeZ)
		{
			return pbHasCollider ? fMax(pfSizeX, pfSizeZ) : FallbackWidth;
		}

		/// <summary>The Remastered blob's diameter: the width times the user's size setting
		/// (RemasterGroundShadowSize), at least MinDiameter.</summary>
		public static float RemasterDiameter(float pfWidth, float pfSize)
		{
			return fMax(MinDiameter, pfWidth * pfSize);
		}

		/// <summary>The palette ground shadow's radius: half the width, at least half of
		/// MinDiameter (the size setting is the Remastered blob's only).</summary>
		public static float PaletteRadius(float pfWidth)
		{
			return fMax(MinDiameter, pfWidth) * 0.5f;
		}

		/// <summary>As Unity's Mathf.Max(float, float), which the paths used before the step.
		/// </summary>
		private static float fMax(float pfA, float pfB)
		{
			return pfA > pfB ? pfA : pfB;
		}
	}
}
