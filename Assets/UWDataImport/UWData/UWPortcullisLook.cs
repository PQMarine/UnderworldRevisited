namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PORTCULLIS'S STRIPES IN EITHER GAME (2026-11-18, per user: the second game's portcullises looked different from
	/// the original). Our grille (UWObjectSpawner.fSpawnPortcullis) paints each face of a bar in one colour, measured on a
	/// first-game screenshot as palette slots 106 to 110. Those are the shades of the portcullis model's own colour (model
	/// 0x0C, 104 in UW.EXE's colour record) at light levels 3, 5, 6, 8 and 9 of LIGHT.DAT. The second game's model is the
	/// same shape in grey 202 (UW2.EXE), and its slots 106 to 110 are a skin tone - so the stripes are taken as those light
	/// levels of the model's colour, through the game's own light table. The first game's indices stay 106 to 110.
	/// </summary>
	public static class UWPortcullisLook
	{
		/// <summary>The portcullis model's colour in UW.EXE, when no model data is at hand.</summary>
		public const int FirstGameBase = 104;

		/// <summary>The light levels of the measured stripes 106, 107, 108, 109 and 110 over the first game's 104.</summary>
		public static readonly int[] StripeLevels = { 3, 5, 6, 8, 9 };

		/// <summary>The five stripe indices, lightest first, for the model colour piBase in the game's light table.</summary>
		public static int[] StripeIndices(int piBase, UWLightLevels pOLevels)
		{
			int[] liIndices = new int[StripeLevels.Length];

			for (int liAt = 0; liAt < StripeLevels.Length; liAt++)
				liIndices[liAt] = pOLevels != null ? pOLevels.Remap(StripeLevels[liAt], piBase) : piBase;

			return liIndices;
		}
	}
}
