using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Spell class 8 - things conjured into the world out of nothing (the reference:
	/// CastClass8_Summoning). In uw1 there are three:
	///
	///   1  Create Food        places something edible in front of the character
	///   3  Rune of Warding    places a ward rune that reacts to strangers
	///   4  Summon Monster     summons a creature that fights on the caster's side
	///
	/// (The minor classes 2, 5 and 6 - flam rune, daemon, satellite - exist only in uw2; the
	/// reference bails out there for uw1.)
	///
	/// WHERE THINGS ARE PLACED: a short distance in front of the character, in the view
	/// direction, with a random swerve. The reference computes in eighth-tiles - nine of them for
	/// everything except Summon Monster, which takes twelve - and scatters the direction by up to
	/// thirteen 256ths of a full circle, i.e. just under twenty degrees. If the
	/// target tile is solid or lies outside the map, the spell reports "There is no
	/// room to create that." and does nothing else.
	///
	/// Engine-free since 2026-09-18 (P3); the eye position and the view yaw come from the host.
	/// </summary>
	public static class UWSummonSpellRules
	{
		/// <summary>Spell class 8.</summary>
		public const int MajorClass = 8;

		private const int CreateFoodSpell = 1;
		private const int WardingRuneSpell = 3;
		private const int SummonMonsterSpell = 4;

		/// <summary>The seven foods: meat, bread, cheese, apple, corn, another bread,
		/// fish. The reference rolls 0 to 6 on top.</summary>
		private const int FirstFoodId = 176;

		private const int FoodCount = 7;

		/// <summary>First creature object. Summon Monster rolls relative to it.</summary>
		private const int FirstCritterId = UWObjectMechanics.FirstCritterObjectId;

		/// <summary>Two creatures are excluded from summoning - in the reference without
		/// explanation; they are objects 123 and 124, both without a name in the string block.
		/// </summary>
		private const int ExcludedCritterA = 0x7B;

		private const int ExcludedCritterB = 0x7C;

		/// <summary>The lowest rank rolled from; above that the casting skill
		/// decides.</summary>
		private const int MinimumMonsterLevel = 2;

		/// <summary>One eighth-tile in world units (eight).</summary>
		private const float SubTilesToWorld = UWWorldScale.TileSize / 8f;

		/// <summary>Distance in front of the character in eighth-tiles, for everything except
		/// Summon Monster.</summary>
		private const int PlacementDistance = 9;

		/// <summary>Distance in front of the character in eighth-tiles for Summon Monster.</summary>
		private const int SummonDistance = 0xC;

		/// <summary>The random swerve on the direction, in 256ths
		/// of a full circle.</summary>
		private const int HeadingSpread = 13;

		/// <summary>String block 1, our numbering: "There is no room to create that." The
		/// reference says 277.</summary>
		private const int NoRoomMessage = 278;

		/// <summary>The quality of a thing that has only just come into being. The same applies to
		/// the results from CMB.DAT (see UWInventoryModel.TryCombineInto) and to the
		/// fragments of the rock hammer.</summary>
		public const int NewObjectQuality = 0x3F;

		/// <summary>
		/// Casts a class 8 spell. Returns false if that spell is not built yet -
		/// the caller then behaves as before.
		/// </summary>
		public static bool Cast(int piMinorClass, IUWSpellHost pIHost)
		{
			if (pIHost == null || !pIHost.HasPlayer || pIHost.CurrentLevel == null)
				return false;

			switch (piMinorClass)
			{
				case CreateFoodSpell:
					return fCreateFood(pIHost);

				case SummonMonsterSpell:
					return fSummonMonster(pIHost);

				case WardingRuneSpell:
					return fWardingRune(pIHost);

				default:
					return false;
			}
		}

		/// <summary>Create Food: a random one of the seven foods in front of the character.
		/// </summary>
		private static bool fCreateFood(IUWSpellHost pIHost)
		{
			UWTilePos lOTile;

			if (!fFindSpot(PlacementDistance, pIHost, out lOTile))
			{
				pIHost.AddGeneralMessage(NoRoomMessage);

				return true;
			}

			// FRESHLY CONJURED FOOD IS THE BEST FOOD. Without the quality value it got
			// ZERO and thus the worst grade - in the original it is the best, since it has
			// only just come into being (per user, 2026-09-10).
			return pIHost.SpawnObjectById(FirstFoodId + UWRandom.Next(FoodCount), lOTile.X, lOTile.Y, 0, NewObjectQuality);
		}

		/// <summary>
		/// Rune of Warding: places an invisible ward rune in front of the character.
		///
		/// It goes off as soon as a CREATURE walks into it - the player cannot
		/// trigger it. It then reports from which direction it happened and hurts the
		/// creature. How that is built in detail is in UWLevelLoader.SpawnWardRune and
		/// UWTrapRules (the ward trap).
		///
		/// NOTHING IS VISIBLE - trigger and trap are pure logic objects without an image, and that
		/// is the same in the original.
		/// </summary>
		private static bool fWardingRune(IUWSpellHost pIHost)
		{
			UWTilePos lOTile;

			if (!fFindSpot(PlacementDistance, pIHost, out lOTile))
			{
				pIHost.AddGeneralMessage(NoRoomMessage);

				return true;
			}

			return pIHost.SpawnWardRune(lOTile.X, lOTile.Y);
		}

		/// <summary>
		/// Summon Monster: summons a creature whose rank depends on the casting
		/// skill.
		///
		/// THE OBJECT IS ROLLED, NOT THE RANK: the reference draws a number below the
		/// rank and takes it as the index in the creature block. A high-level caster does not
		/// necessarily get something stronger, he just has more to choose from.
		///
		/// EXCLUDED are creatures without vitality in the table (they exist there as
		/// placeholders), swimmers, the peaceful ones and two nameless objects. If the
		/// roll misses sixteen times, the spell gives up.
		///
		/// AN ALLY since 2026-09-23: when the player summons, the original sets byte 0x19 bit 6
		/// on the new creature (Class8Spells_seg038_E45 - until 2026-09-27 this said Class7); a
		/// creature's summoning instead clears the attitude to 0 and sends it at the player.
		///
		/// THE NEW CREATURE'S STATE, read 2026-09-27 (see UWCritterRecord.InitialiseAsNew): goal 8,
		/// attitude 2, hit points rolled from the table's vitality, home post 0x20/0x20 - the
		/// summoning moves only its tile. Ours used the friendliest attitude and set it on the
		/// UWNpc alone; the creature's mind read nineteen zero bytes, attitude 0 and no ally bit,
		/// and it turned on the player once it woke (per user, 2026-09-27).
		/// </summary>
		private static bool fSummonMonster(IUWSpellHost pIHost)
		{
			pIHost.GetEyePosition(out float lfEyeX, out float lfEyeZ);

			if (!fFindSpotFrom(SummonDistance, lfEyeX, lfEyeZ, pIHost.ViewYawDegrees, pIHost, out UWTilePos lOTile,
					out float lfAtX, out float lfAtZ))
			{
				pIHost.AddGeneralMessage(NoRoomMessage);

				return true;
			}

			if (pIHost.Data == null || pIHost.Data.ObjectClassProperties == null)
				return false;

			int liLevel = Math.Max(MinimumMonsterLevel, pIHost.PlayerCastingSkill);

			int liId = RollMonster(liLevel, pIHost.Data.ObjectClassProperties);

			if (liId < 0)
				return true;

			if (!fSummonSpotFits(lOTile, lfAtX, lfAtZ, liId, pIHost))
			{
				pIHost.AddGeneralMessage(NoRoomMessage);

				return true;
			}

			UWNpc lONpc;
			UWCritterRecord lORecord = fCreateNewCreature(liId, lOTile, lfAtX, lfAtZ, pIHost, out lONpc);

			if (lORecord == null)
				return false;

			// The player's summoning adds only the ally bit (byte 0x19 bit 6).
			lORecord.IsAlly = true;

			return pIHost.SpawnObjectAt(lONpc, lfAtX, lfAtZ);
		}

		/// <summary>
		/// A CREATURE SUMMONS (Class8Spells_seg038_E45 with a creature as the caster - mage 106
		/// holds Summon Monster as its third spell; built 2026-09-27): the level is the dungeon
		/// level times 4 (at least 2) instead of the casting skill, the spot twelve eighths along
		/// the caster's facing with the same swerve, and the new creature, set up like any other
		/// (InitialiseAsNew), gets attitude 0, byte 0x19 bit 0 (target confirmed) and the player's
		/// tile as its destination - no ally bit. No message when there is no room: that is the
		/// player's only. Returns false when nothing was summoned.
		/// </summary>
		public static bool SummonByCreature(IUWSpellHost pIHost, float pfFromX, float pfFromZ, float pfYawDegrees,
			int piDungeonLevel, UWTilePos pOPlayerTile)
		{
			if (pIHost == null || pIHost.CurrentLevel == null || pIHost.Data == null
				|| pIHost.Data.ObjectClassProperties == null)
				return false;

			if (!fFindSpotFrom(SummonDistance, pfFromX, pfFromZ, pfYawDegrees, pIHost, out UWTilePos lOTile,
					out float lfAtX, out float lfAtZ))
				return false;

			int liId = RollMonster(Math.Max(MinimumMonsterLevel, piDungeonLevel * CreatureLevelPerDungeonLevel),
				pIHost.Data.ObjectClassProperties);

			if (liId < 0 || !fSummonSpotFits(lOTile, lfAtX, lfAtZ, liId, pIHost))
				return false;

			UWNpc lONpc;
			UWCritterRecord lORecord = fCreateNewCreature(liId, lOTile, lfAtX, lfAtZ, pIHost, out lONpc);

			if (lORecord == null)
				return false;

			lORecord.Attitude = UWNpc.AttitudeHostile;
			lORecord.TargetConfirmed = true;
			lORecord.DestinationX = pOPlayerTile.X;
			lORecord.DestinationY = pOPlayerTile.Y;

			return pIHost.SpawnObjectAt(lONpc, lfAtX, lfAtZ);
		}

		/// <summary>A creature's summoning draws with the dungeon level times this.</summary>
		private const int CreatureLevelPerDungeonLevel = 4;

		/// <summary>The new creature of a summoning: set up as ovr101_5E does
		/// (UWCritterRecord.InitialiseAsNew) on the tile it will stand on - the home post stays
		/// where the set-up put it.</summary>
		private static UWCritterRecord fCreateNewCreature(int piId, UWTilePos pOTile, float pfAtX, float pfAtZ,
			IUWSpellHost pIHost, out UWNpc pONpc)
		{
			pONpc = new UWNpc((ushort)piId);

			UWObjectClassProperties.Critter lOStats;
			int liVitality = pIHost.Data.ObjectClassProperties.TryGetCritter(piId, out lOStats) ? lOStats.Vitality : 0;

			UWCritterRecord lORecord = new UWCritterRecord(pONpc);

			lORecord.InitialiseAsNew(liVitality, UWRandom.Next(UWCritterRecord.NewCreatureHitPointRollRange));
			lORecord.TileX = pOTile.X;
			lORecord.TileY = pOTile.Y;

			pONpc.NPC_HP = (byte)liVitality;
			pONpc.XPos = UWTileQueries.WorldToSubTile(pfAtX, pOTile.X);
			pONpc.YPos = UWTileQueries.WorldToSubTile(pfAtZ, pOTile.Y);
			pONpc.WasSpawned = true;

			try
			{
				pONpc.Texture = pIHost.Data.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, piId);
			}
			catch
			{
				return null;
			}

			return lORecord;
		}


		/// <summary>
		/// Rolls a suitable creature. Returns -1 if none was found.
		///
		/// READ 2026-09-27 (Class8Spells_seg038_E45, labels FA6-100B; per user's request): the
		/// original draws id = 0x40 + level + RNG % level, so from 0x40 + level up to 0x40 +
		/// 2 * level - 1 - the UPPER half, stronger kinds for a stronger caster -, and draws
		/// again while the row's vitality (byte 4) is 0, the kind is passive (byte 0x0A bit 1)
		/// or has byte 0x0A bit 6, or the id is 0x7B or 0x7C; it never gives up. Ours drew
		/// 0x40 + RNG % level (the reference's reading, the lower half) and gave up after 16
		/// tries. Drawing again until one fits is a uniform choice among the fitting kinds of the
		/// range, which is what this does; none fitting (the original would hang) summons nothing.
		/// </summary>
		public static int RollMonster(int piLevel, UWObjectClassProperties pOProperties)
		{
			System.Collections.Generic.List<int> lOFitting = new System.Collections.Generic.List<int>();

			for (int liId = FirstCritterId + piLevel; liId < FirstCritterId + (2 * piLevel) && liId < FirstCritterId + 0x40; liId++)
			{
				if (liId == ExcludedCritterA || liId == ExcludedCritterB)
					continue;

				UWObjectClassProperties.Critter lOStats;

				if (!pOProperties.TryGetCritter(liId, out lOStats))
					continue;

				// No entry in the table (vitality 0), a swimmer or a peaceful creature -
				// the swimmer and passive bits sit in the same byte (see UWObjectClassProperties.Passiveness).
				if (lOStats.Vitality == 0
					|| (lOStats.Passiveness & SwimmerBit) != 0
					|| (lOStats.Passiveness & PassiveBit) != 0)
					continue;

				lOFitting.Add(liId);
			}

			return lOFitting.Count > 0 ? lOFitting[UWRandom.Next(lOFitting.Count)] : -1;
		}

		private const int PassiveBit = 0x02;

		private const int SwimmerBit = 0x40;

		/// <summary>
		/// Finds the tile in front of the character where something can be placed. Returns false if
		/// there is a wall there or the spot lies outside the map.
		/// </summary>
		private static bool fFindSpot(int piDistance, IUWSpellHost pIHost, out UWTilePos pOTile)
		{
			pIHost.GetEyePosition(out float lfEyeX, out float lfEyeZ);

			return fFindSpotFrom(piDistance, lfEyeX, lfEyeZ, pIHost.ViewYawDegrees, pIHost, out pOTile, out _, out _);
		}

		/// <summary>
		/// WHERE A SUMMONED CREATURE MAY STAND (Class8Spells_seg038_E45 with the fit test
		/// UWEasyMovement names, read 2026-09-27 on the user's question): the new
		/// creature is put on the POINT the swerved heading reaches, not in the middle of its
		/// tile, and the summoning fails when anything's collision overlaps the creature there
		/// within its height - so two summoned creatures never stand inside each other; the player
		/// gets "There is no room to create that.", a creature casts in vain. There is ONE try, no
		/// search for another spot. Ours put every summoned creature on sub-position 3/3 of the
		/// tile without a test, so several could stand in one another.
		/// </summary>
		private static bool fSummonSpotFits(UWTilePos pOTile, float pfAtX, float pfAtZ, int piId, IUWSpellHost pIHost)
		{
			UWTile lOTile = pIHost.CurrentLevel.TileData[(pOTile.Y * UWWorldScale.TilesPerAxis) + pOTile.X];

			if (!UWTileQueries.IsSubTileInOpenHalf(lOTile.TileType, UWTileQueries.WorldToSubTile(pfAtX, pOTile.X),
					UWTileQueries.WorldToSubTile(pfAtZ, pOTile.Y)))
				return false;

			return pIHost.CreatureFitsAt(piId, pfAtX, pfAtZ);
		}

		/// <summary>The spot piDistance eighths from a world position along a yaw (0 north,
		/// clockwise), with the random swerve - the player's eye and view, or a creature's body and
		/// facing.</summary>
		private static bool fFindSpotFrom(int piDistance, float pfFromX, float pfFromZ, float pfYawDegrees,
			IUWSpellHost pIHost, out UWTilePos pOTile, out float pfAtX, out float pfAtZ)
		{
			pOTile = new UWTilePos(0, 0);

			float lfYaw = pfYawDegrees + (UWRandom.Next(-HeadingSpread, HeadingSpread + 1) * 360f / 256f);

			// The yaw as Unity turns it: 0 north, clockwise - east is the sine.
			double ldRadians = lfYaw * Math.PI / 180.0;

			float lfDistance = piDistance * SubTilesToWorld;

			pfAtX = pfFromX + ((float)Math.Sin(ldRadians) * lfDistance);
			pfAtZ = pfFromZ + ((float)Math.Cos(ldRadians) * lfDistance);

			// The same tile lookup the level loader uses (it clamps to the map).
			pOTile = UWTileQueries.WorldToTile(pfAtX, pfAtZ);

			if (pOTile.X < 0 || pOTile.X >= UWWorldScale.TilesPerAxis
				|| pOTile.Y < 0 || pOTile.Y >= UWWorldScale.TilesPerAxis)
				return false;

			UWTile lOTile = pIHost.CurrentLevel.TileData[(pOTile.Y * UWWorldScale.TilesPerAxis) + pOTile.X];

			return lOTile != null && lOTile.TileType != UWTile.TileTypeEnum.solid;
		}
	}
}
