using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Properties that apply to whole object ranges. Stored in OBJECTS.DAT, which
	/// contains several tables in a row.
	///
	/// So far only the light source table and the table of ranged weapons and projectiles
	/// are evaluated; the remaining tables are
	/// noted with their offsets so they can be added later.
	/// </summary>
	public class UWObjectProperties
	{
		/// <summary>First object id that has an entry in the light table.</summary>
		public const int LightSourceFirstId = 0x0090;

		/// <summary>Number of entries in the light table.</summary>
		public const int LightSourceCount = 16;

		/// <summary>Highest brightness value according to the format description.</summary>
		public const int MaxBrightness = 4;

		private const int MeleeWeaponsOffset = 0x0002;
		private const int RangedWeaponsOffset = 0x0082;
		private const int ArmourOffset = 0x00B2;
		private const int CrittersOffset = 0x0132;
		private const int ContainersOffset = 0x0D32;
		private const int LightSourcesOffset = 0x0D62;
		public const int AnimationOffset = 0x0DA2;

		private readonly byte[] myBrightness = new byte[LightSourceCount];
		private readonly byte[] myDuration = new byte[LightSourceCount];

		public UWObjectProperties(string psDataPath)
		{
			string lsFile = Path.Combine(psDataPath, "OBJECTS.DAT");

			if (!File.Exists(lsFile))
				return;

			byte[] lyData = File.ReadAllBytes(lsFile);

			fLoadLightSources(lyData);
			fLoadRangedObjects(lyData);
		}

		/// <summary>Number of entries in the table of ranged weapons and projectiles: the
		/// lower four bits of the object number select the entry.</summary>
		public const int RangedObjectCount = 16;

		private readonly byte[] myRangedDamage = new byte[RangedObjectCount];

		private readonly byte[] myRangedSpeed = new byte[RangedObjectCount];

		private readonly byte[] myRangedType = new byte[RangedObjectCount];

		/// <summary>
		/// Ranged weapons and projectiles: three bytes each, damage, speed, weapon type.
		///
		/// The entry depends only on the lower four bits of the object number - arrow, bolt,
		/// fireball and lightning all lie in the same block of sixteen starting at 0x10.
		/// </summary>
		private void fLoadRangedObjects(byte[] pyData)
		{
			for (int liEntry = 0; liEntry < RangedObjectCount; liEntry++)
			{
				int liOffset = RangedWeaponsOffset + (liEntry * 3);

				if (liOffset + 2 >= pyData.Length)
					break;

				myRangedDamage[liEntry] = pyData[liOffset];
				myRangedSpeed[liEntry] = pyData[liOffset + 1];
				myRangedType[liEntry] = pyData[liOffset + 2];
			}
		}

		/// <summary>Base damage of a projectile. For a spell projectile this is the whole
		/// damage; only for physical projectiles does the original factor in the Missile
		/// skill (weapon type 0xC0).</summary>
		public int GetRangedDamage(int piObjectId)
		{
			return myRangedDamage[piObjectId & 0xF];
		}

		/// <summary>
		/// How fast a projectile flies.
		///
		/// The reference reads this byte as AMMUNITION TYPE - which ammunition a ranged weapon
		/// fires. That does not add up: sling, bow and crossbow all carry the same 15
		/// there, and 15 plus 16 would be the jeweled bow.
		///
		/// Read as SPEED everything falls into place: crossbow bolt 26, arrow 22, sling stone
		/// 15, thrown rock 10 - exactly the order one expects. And for the
		/// spell projectiles fireball 24, lightning 20, Magic Missile 15; the user has
		/// confirmed in the original that the lightning is faster than the Magic Missile and
		/// cannot be caught up with, whereas the Missile is slower than walking
		/// (2026-09-05). The reference indeed uses the value where the trajectory
		/// is concerned (GetPitchToGTarg).
		/// </summary>
		public int GetRangedSpeed(int piObjectId)
		{
			return myRangedSpeed[piObjectId & 0xF];
		}

		/// <summary>Weapon type. 0xC0 are the physical projectiles.</summary>
		public int GetRangedType(int piObjectId)
		{
			return myRangedType[piObjectId & 0xF];
		}

		private void fLoadLightSources(byte[] pyData)
		{
			for (int i = 0; i < LightSourceCount; i++)
			{
				int liOffset = LightSourcesOffset + (i * 2);

				if (liOffset + 1 >= pyData.Length)
					break;

				// The format description lists brightness first, then duration. The data
				// says the opposite: read that way a candle (12) would be brighter than a torch
				// (3), and the taper would have duration 3 instead of the documented 0 for
				// "never goes out" - of all things the example from the description itself.
				// The other way round gives a consistent picture: lantern 4 (the maximum),
				// torch 2, candle 1, taper 3 and burning indefinitely.
				myDuration[i] = pyData[liOffset];
				myBrightness[i] = pyData[liOffset + 1];
			}
		}

		/// <summary>
		/// Brightness of a light source, 0 to 4. 0 means the object emits no light.
		/// For objects outside the light table 0 is returned.
		/// </summary>
		public int GetLightBrightness(int piObjectId)
		{
			int liIndex = piObjectId - LightSourceFirstId;

			if (liIndex < 0 || liIndex >= LightSourceCount)
				return 0;

			return myBrightness[liIndex];
		}

		/// <summary>
		/// Burn duration of a light source. 0 means it never goes out.
		/// </summary>
		public int GetLightDuration(int piObjectId)
		{
			int liIndex = piObjectId - LightSourceFirstId;

			if (liIndex < 0 || liIndex >= LightSourceCount)
				return 0;

			return myDuration[liIndex];
		}

		public bool IsLightSource(int piObjectId)
		{
			return GetLightBrightness(piObjectId) > 0;
		}
	}
}
