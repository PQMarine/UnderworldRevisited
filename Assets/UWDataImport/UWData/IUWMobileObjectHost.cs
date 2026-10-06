namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE OBJECT MOTION ASKS OF THE GAME beyond the motion core's world (UWMobileObjectMotion,
	/// 2026-10-05): the relinking between tiles, the object's own damage, the sounds and pictures
	/// of a landing, the talismans in the lava, the detonation, and the end - the object removed,
	/// or placed as a lying object. Everything else (where it lands, whether the liquid takes it,
	/// whether it hops off or slides on) is decided engine-free.
	/// </summary>
	public interface IUWMobileObjectHost
	{
		int DungeonLevel { get; }

		/// <summary>The world clock's new value (dseg_248D) - a re-promoted object is due one unit
		/// after it.</summary>
		int ClockPhase { get; }

		/// <summary>The object moved into another tile: unlink and relink (InsertObjectToList at
		/// the head).</summary>
		void MoveToTile(UWMobileRecord pORecord, int piOldTileX, int piOldTileY, int piNewTileX, int piNewTileY);

		/// <summary>DamageObject_seg023_35A on the object itself (type 0 the impact of a landing,
		/// 8 the fire of lava). The host changes Hp; true when the object was destroyed and is gone.</summary>
		bool DamageSelf(UWMobileRecord pORecord, int piDamage, int piType);

		/// <summary>PlaySoundEffectAtObject(id, obj, volume).</summary>
		void PlaySoundAtObject(int piSound, UWMobileRecord pORecord, int piVolume);

		/// <summary>SpawnClass7Object(obj, 6, 3): the splash of a landing in water.</summary>
		void Splash(UWMobileRecord pORecord);

		/// <summary>A talisman come to rest on lava: the volcano's rule (UWEndgameRules.DropTalismanInLava
		/// - level 8 within 5 tiles of 32/32 with the player alive; before the burial the dream bit,
		/// after it one talisman less with its explosions). True when it burns and is gone.</summary>
		bool TalismanBurns(UWMobileRecord pORecord, int piTileX, int piTileY);

		/// <summary>The object has been demoted to a static object for the settling: a lit light
		/// source goes out (ids 0x94-0x96 become 0x90-0x92), a class-7 overlay moves its link.</summary>
		void OnDemoted(UWMobileRecord pORecord);

		/// <summary>DetonateProjectile_seg044_95A: a fireball or lightning bolt at rest. Returns its
		/// result; 0 means the missile is then removed (through the culling test).</summary>
		int Detonate(UWMobileRecord pORecord, int piTileX, int piTileY, int piOwner);

		/// <summary>The object leaves the world for good (culled, burnt, broken, detonated).</summary>
		void Remove(UWMobileRecord pORecord);

		/// <summary>The object comes to rest as a lying object at its record's tile, eighth and
		/// coarse z, with this heading octant and quality.</summary>
		void PlaceAtRest(UWMobileRecord pORecord, int piOctant, int piQuality);
	}
}
