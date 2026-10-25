namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PORT'S GLOWING THINGS in one table (cleanup step 6.4, 2026-10-24): which world objects
	/// light their surroundings in the Remastered mode (UWRemasterLights) and in the palette
	/// effects (UWPaletteEffects: light sources, glow), which of them flicker as open fire, and who
	/// therefore casts no ground blob (UWGroundShadowRule). Until this step the same nine ids lived
	/// in two parallel tables (UWRemasterLights' recipes and UWPaletteEffects' light levels) plus a
	/// membership test the palette path borrowed from the Remastered MonoBehaviour.
	///
	/// WHY A TABLE AND NOT THE DATA: in the nine levels of uw1 not a single light source lights
	/// anything - all light comes from the player's torch, at his eye (see UWLighting). The
	/// brightness field of OBJECTS.DAT (UWObjectProperties.GetLightBrightness) belongs to the
	/// carried lights 144-159 only and is zero for every placed object, so the list comes from the
	/// NAMES and was counted at the locations found (see the removed tool UWLightObjectDump):
	/// torch 145 (9 pieces), candle 146 (4), lantern 144 (6), campfire 298 (22),
	/// fire elemental 120 (23), glowing stone 297 (83, all on level 5), mushroom 184 (18),
	/// orb 279 (4) and orb stone 274 (10).
	///
	/// NOT IN THIS TABLE, because the original already has one home for them: the carried light's
	/// level (OBJECTS.DAT, UWObjectProperties.GetLightBrightness, the larger one carried by
	/// UWInventoryModel), the light spells' level (their minor class, UWArmourProtection's status
	/// pass), and the reach of a level (SHADES.DAT, UWShades.Entry.ViewingDistance). "Any light
	/// item, lit or not" stays UWObjectMechanics.IsAnyLight (the original's 0x1F8 mask).
	///
	/// The light colour is not in the table either: it is taken from the object's own image
	/// (UWObjectLightColour), with FallbackColour when the image has none.
	/// </summary>
	public static class UWLightSourceTable
	{
		/// <summary>What makes up a glowing object.</summary>
		public struct Row
		{
			public int Id;

			/// <summary>Remastered: range in tiles.</summary>
			public float Tiles;

			/// <summary>Remastered: brightness at one tile distance.</summary>
			public float Brightness;

			/// <summary>How strongly it flickers (0 = steady). For open fire a factor on the setting
			/// RemasterFireFlicker, otherwise the value itself (the lantern barely flickers behind
			/// glass). Independently of this, the sprite's glow flickers only weakly
			/// (RemasterSpriteFlicker).</summary>
			public float Flicker;

			/// <summary>Remastered: may cast shadows (the nearest few do, UWRemasterLights).</summary>
			public bool Shadows;

			/// <summary>Open fire: flickers more strongly, and the light jitters a little so the
			/// shadows dance; in the palette light sources its light level swings
			/// (UWFlicker.PaletteFireLevel).</summary>
			public bool OpenFire;

			/// <summary>Remastered: how strongly the bright spots of the sprite itself glow.</summary>
			public float Glow;

			/// <summary>Remastered: size of the fire for sparks and smoke (UWRemasterParticles),
			/// 0 = none.</summary>
			public float Fire;

			/// <summary>Palette light sources: the light level a source shines with, as a carried
			/// light of that level would (0-7, UWShades): a candle or a mushroom like a candle, a
			/// torch or an orb like a torch, a lantern or a fire brighter. Chosen by what they are.
			/// </summary>
			public float PaletteLightLevel;
		}

		/// <summary>Moved verbatim from UWRemasterLights.mORecipes and UWPaletteEffects.msLightLevels
		/// (2026-10-24); the self-check group LightSources pins every value.</summary>
		private static readonly Row[] msRows =
		{
			new Row { Id = UWObjectIds.Torch,   Tiles = 3f,   Brightness = 0.8f,  Flicker = 1f,    Shadows = true,  OpenFire = true,  Glow = 1.5f, Fire = 1f,    PaletteLightLevel = 2f }, // torch
			new Row { Id = UWObjectIds.Candle,  Tiles = 1.5f, Brightness = 0.5f,  Flicker = 0.9f,  Shadows = false, OpenFire = true,  Glow = 1.2f, Fire = 0.35f, PaletteLightLevel = 1f }, // candle
			new Row { Id = UWObjectIds.Lantern, Tiles = 3.5f, Brightness = 0.9f,  Flicker = 0.08f, Shadows = true,  OpenFire = false, Glow = 1f,                 PaletteLightLevel = 3f }, // lantern
			new Row { Id = 298, Tiles = 4f,   Brightness = 1.1f,  Flicker = 1.2f,  Shadows = true,  OpenFire = true,  Glow = 1.8f, Fire = 1.8f,  PaletteLightLevel = 3f }, // campfire
			new Row { Id = 120, Tiles = 4f,   Brightness = 1.0f,  Flicker = 1.2f,  Shadows = false, OpenFire = true,  Glow = 1.5f, Fire = 1.4f,  PaletteLightLevel = 3f }, // fire elemental
			new Row { Id = UWObjectIds.GlowingRock, Tiles = 2f, Brightness = 0.35f, Flicker = 0f, Shadows = false, OpenFire = false, Glow = 1f, PaletteLightLevel = 1f }, // glowing stone
			new Row { Id = 184, Tiles = 1.5f, Brightness = 0.25f, Flicker = 0f,    Shadows = false, OpenFire = false, Glow = 0.8f,               PaletteLightLevel = 1f }, // glowing mushroom
			new Row { Id = 279, Tiles = 2.5f, Brightness = 0.6f,  Flicker = 0f,    Shadows = false, OpenFire = false, Glow = 1f,                 PaletteLightLevel = 2f }, // orb
			new Row { Id = 274, Tiles = 2.5f, Brightness = 0.6f,  Flicker = 0f,    Shadows = false, OpenFire = false, Glow = 1f,                 PaletteLightLevel = 2f }, // orb stone
		};

		public static int Count
		{
			get { return msRows.Length; }
		}

		/// <summary>The rows in table order (for the self-check and a dump).</summary>
		public static Row GetRow(int piIndex)
		{
			return msRows[piIndex];
		}

		public static bool TryGet(int piId, out Row pORow)
		{
			for (int liAt = 0; liAt < msRows.Length; liAt++)
			{
				if (msRows[liAt].Id == piId)
				{
					pORow = msRows[liAt];
					return true;
				}
			}

			pORow = default(Row);

			return false;
		}

		/// <summary>Whether a world object is one of the glowing things: it lights in Remastered,
		/// is a palette light source and glows, and casts no ground blob (a campfire casting a dark
		/// blob would be wrong).</summary>
		public static bool IsLightSource(int piId)
		{
			Row lORow;

			return TryGet(piId, out lORow);
		}

		/// <summary>Whether the object is open fire (torch, candle, campfire, fire elemental). The
		/// carried light asks it with the unlit id (UWLighting): the torch or candle in the hand
		/// flickers and jitters like the placed ones, the lantern does not.</summary>
		public static bool IsOpenFire(int piId)
		{
			Row lORow;

			return TryGet(piId, out lORow) && lORow.OpenFire;
		}

		/// <summary>The light colour when the object's image gives none (UWObjectLightColour):
		/// a warm fire colour, for the placed lights and the carried item alike.</summary>
		public const float FallbackRed = 1f;

		public const float FallbackGreen = 0.78f;

		public const float FallbackBlue = 0.5f;

		/// <summary>
		/// LAVA lights its surroundings too - not an object but every lava tile.
		///
		/// Remastered: a light above every few lava tiles (UWRemasterLights.LavaLightSpacing - one
		/// per lava tile would be hundreds on a lava level), LavaTiles reach, LavaBrightness, a
		/// slight flicker, a fixed orange.
		///
		/// Palette: light level 1 (by SHADES.DAT 4, 8, 14 at 0, 1, 2 tiles), dimmed by LavaPaletteDim
		/// levels: 7, 11, dark - a dull glow with a reach of about two tiles. It was 2 without a
		/// dimming (2, 6, 13) until the user found the brightening too strong, then 1 (4, 8, 14),
		/// still too bright: "In meiner Vorstellung macht Lava nur ein dumpfes Licht" (2026-10-07).
		/// </summary>
		public const float LavaTiles = 3f;

		public const float LavaBrightness = 0.7f;

		public const float LavaFlicker = 0.12f;

		public const float LavaRed = 1f;

		public const float LavaGreen = 0.45f;

		public const float LavaBlue = 0.12f;

		public const float LavaPaletteLightLevel = 1f;

		public const float LavaPaletteDim = 3f;
	}
}
