namespace UWDataImport.UWData
{
	/// <summary>
	/// HOW TALL THE ORIGINAL DRAWS A SPRITE'S PIXEL (2026-10-29, per user with screenshots beside the
	/// original: our items stood too wide). in_scalebm sizes a sprite by its picture times the words at
	/// +0x0A (across) and +0x0C (up) of its scale record (Underworld Exhumed uw1 port/3d/tmapops.c from
	/// 6863, do_scalebm). UW.EXE's four records hold 4 and 5 (file 0x554E8 on), UW2.EXE's object records
	/// 0x20 and 0x28 (file 0x5EF2C on) - the same 4:5 in eighths. A pixel is one unit across, so it stands
	/// five quarters of a unit high: objects in both games, and the first game's creatures (the second
	/// game's are square, UWCritterArtUw2.CreaturePixelScale). Until then our objects had square pixels,
	/// and the first game's creatures 1.3, judged by eye beside the original on 2026-09-17.
	/// </summary>
	public static class UWSpriteScale
	{
		public const int RecordAcross = 4;

		public const int RecordUp = 5;

		/// <summary>A sprite pixel's height in units of its width.</summary>
		public const float PixelHeight = (float)RecordUp / RecordAcross;

		/// <summary>
		/// WHERE A SPRITE HANGS ON ITS POINT (2026-11-11, per user with screenshots of Mass Confusion beside the
		/// original: our sparkles stood too high). The object record's hot spot is fixed, the same for every
		/// picture whatever its size (UW.EXE file 0x554F6 on, UW2.EXE 0x5EF32: x 8, y 16 at +6 and +8, beside the
		/// scale words above); in_scalebm moves the point left by x and up by y pixels and draws the picture
		/// from there to the right and down (Underworld Exhumed TMAPOPS.ASM in_scalebm). So a 16 by 16 picture -
		/// nearly every item - stands with its bottom on the point, centred; a taller one, as most animations,
		/// hangs below the point, and a wider one reaches further right than left.
		/// </summary>
		public const int AnchorX = 8;

		public const int AnchorY = 16;
	}
}
