namespace UWDataImport.UWData
{
	/// <summary>
	/// THE TEXTURES' LOOK IN REMASTERED (per user 2026-11-25, before the release after 0.23.0: "Alle vier", then "Getrennt für
	/// Wand und Rest entfernen. Entweder alles oder garnichts."): one choice for everything the Remastered world draws from
	/// the game's pictures - walls, floors and ceilings, the pictures on the walls, doors and the models' textures, things,
	/// creatures and the weapon in the hand. It took the place of the two xBR switches of 0.23.0 (UWUserData.XbrTextures for
	/// the walls, XbrSprites for the rest); a file with either of them on reads xBR.
	///
	/// Pixels: the original's pixels, as before. Smooth: bilinear on the original's texels, as the project's 2012 version drew
	/// its world (per user the same day with a screenshot of that version, "Bilinear wie 2012": LoadUWLevelcc made its walls,
	/// floors, objects and doors as Texture2D with mipmaps and left Unity's default filter, bilinear) - the walls' colour array
	/// bilinear, the sprites and the weapon blended by the shaders; the outline of a cut-out picture stays hard (per user:
	/// only the inside is smoothed). xBR: the
	/// pictures scaled to four times their size by the pixel-art scaler, hard (UWXbrScaler, UWSpriteXbr). The palette modes
	/// keep the original's pixels whatever is chosen.
	///
	/// TRIED AND TAKEN OUT THE SAME DAY (per user, against Underworld Adventures' look, which scales by hq4x and filters
	/// linearly - read in its renderer, nothing taken): trilinear on the original's texels, sharp bilinear with a blend one
	/// screen pixel wide ("sieht man kaum einen Unterschied"), xBR then trilinear ("keine davon"), and each texel quadrupled
	/// then bilinear ("pixelig in unscharf"). Plain bilinear came close to the 2012 picture at the full resolution (per user;
	/// with a low world resolution, UWWorldResolution, it looks pixelated and blurry again, so the two stay apart).
	/// </summary>
	public enum UWTextureLookEnum
	{
		Pixels = 0,
		Smooth = 1,
		Xbr = 2
	}

	public static class UWTextureLook
	{
		/// <summary>The last of the choices (UWUserSettingsTable.TextureLook).</summary>
		public const int Last = (int)UWTextureLookEnum.Xbr;

		/// <summary>The filter after any scaling (the shaders' _UWSpriteFilter and the walls' array).</summary>
		public enum FilterEnum
		{
			None = 0,
			Smooth = 1
		}

		/// <summary>A stored number as a look; anything else reads Pixels.</summary>
		public static UWTextureLookEnum Of(int piValue)
		{
			return piValue < 0 || piValue > Last ? UWTextureLookEnum.Pixels : (UWTextureLookEnum)piValue;
		}

		/// <summary>Whether the pictures are scaled by xBR first.</summary>
		public static bool ScalesByXbr(UWTextureLookEnum peLook)
		{
			return peLook == UWTextureLookEnum.Xbr;
		}

		/// <summary>The filter the look draws with.</summary>
		public static FilterEnum Filter(UWTextureLookEnum peLook)
		{
			return peLook == UWTextureLookEnum.Smooth ? FilterEnum.Smooth : FilterEnum.None;
		}

		/// <summary>What a settings file without the choice reads (0.23.0's two switches): xBR where either was on.</summary>
		public static int FromXbrSwitches(bool pbTextures, bool pbSprites)
		{
			return (int)(pbTextures || pbSprites ? UWTextureLookEnum.Xbr : UWTextureLookEnum.Pixels);
		}
	}
}
