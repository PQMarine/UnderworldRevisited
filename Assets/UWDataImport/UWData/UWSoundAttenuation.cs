namespace UWDataImport.UWData
{
	/// <summary>
	/// The volume of a sound by its distance, as in the original (seg014_8AE): distance in
	/// eighths of a tile, full volume up to 8, nothing from 48, linear in between. Stereo
	/// panning is dropped - the AdLib is mono, and it was in 1992 too. Engine-free since
	/// 2026-09-18 (P3 of the engine separation), out of UWSoundEffects.
	/// </summary>
	public static class UWSoundAttenuation
	{
		/// <summary>From this distance on a sound is not played at all.</summary>
		public const int CullDistance = 48;

		/// <summary>Up to this distance a sound plays at full volume.</summary>
		public const int FullVolumeRadius = 8;

		/// <summary>The loudest value the sound engine takes.</summary>
		public const int MaxVolume = 0x7F;

		/// <summary>
		/// The volume for a distance in eighths of a tile, or -1 when the sound is too far away
		/// to play at all. piBaseVelocity is the effect's own loudness, piVolumeDelta an offset a
		/// hit adds by its damage.
		/// </summary>
		public static int GetVolume(int piDistanceEighthTiles, int piBaseVelocity, int piVolumeDelta)
		{
			if (piDistanceEighthTiles > CullDistance)
				return -1;

			int liRaw = piBaseVelocity + piVolumeDelta;
			int liVolume = piDistanceEighthTiles < FullVolumeRadius
				? liRaw
				: liRaw * (CullDistance - piDistanceEighthTiles) / (CullDistance - FullVolumeRadius);

			return liVolume < 0 ? 0 : (liVolume > MaxVolume ? MaxVolume : liVolume);
		}
	}
}
