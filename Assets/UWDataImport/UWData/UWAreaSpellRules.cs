using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Spell class 6: effect on an AREA, not on a single target (2026-09-06).
	///
	/// Four of these can be cast via runes in uw1; the minor class says which:
	///
	///   1  Reveal          reveals what a look trigger hides
	///   2  Sheet Lightning lightning strikes the tiles of the area
	///   3  Confusion       creatures in the area leave the player alone
	///   4  Flame Wind      fire in a cross around every tile hit
	///
	/// A fifth entry (minor class 5, "Mass Paralyze") is in the spell table,
	/// but has no rune sequence there - it only exists in enchanted items. The
	/// reference also knows only the four above for uw1, so it is left out here.
	///
	/// WHERE THE AREA LIES: not around the player, but IN FRONT of him - the reference takes
	/// his view direction and goes four tiles out, that is the centre. That is also why there is
	/// no aiming: unlike a projectile spell (class 5) the spell takes effect immediately
	/// on casting, the view direction at the moment of casting is the entire targeting.
	///
	/// TARGET TYPE: the upper two bits of the minor class. For the lasting spells
	/// the same bits carry the duration - here they say what the effect applies to (see
	/// UWRunicMagic.GetAreaTargetType). That fits the four spells exactly: Sheet Lightning
	/// and Flame Wind hit tiles, Confusion only creatures, Reveal both.
	///
	/// READ IN UW.EXE 2026-09-27 (see CastAt): radius 2 and distance 4, the hit count DiceRoll(3, 4),
	/// the per-tile roll and up to five passes in tile mode, the damage rolled per object. The
	/// earlier uncertainties about radius and hit count (the reference's uw1 table) are settled.
	///
	/// THE PLAYER IS STRUCK when the fire reaches his tile (read 2026-09-27; the guess before
	/// said no): DamageObjectsInTile_seg038_3307_182A damages the tile's object list, the game
	/// keeps the player in the list of his tile while playing, and DamageNPC knows him (the
	/// player object's byte 8 is his vitality). The loose objects there are worn down as well -
	/// see UWObjectDamageRules.
	///
	/// RESISTANCES apply since 2026-09-06: Flame Wind deals fire damage (damage type
	/// 0x0B), Sheet Lightning pure magic (0x03) - both values from the reference. So Flame Wind
	/// bounces off a fire elemental. See UWDamageTypes.
	///
	/// Engine-free since 2026-09-18 (P3): what stands in the area is collected by the host
	/// (physics) and addressed here by index; the effect pictures and the damage go back
	/// through the host as well.
	/// </summary>
	public static class UWAreaSpellRules
	{
		/// <summary>Reveals what is behind a look trigger.</summary>
		private const int RevealMinor = 1;

		/// <summary>Lightning in the tiles of the area.</summary>
		private const int SheetLightningMinor = 2;

		/// <summary>Creatures leave the player alone.</summary>
		private const int ConfusionMinor = 3;

		/// <summary>Fire in a cross around every tile hit.</summary>
		private const int FlameWindMinor = 4;

		/// <summary>Target type 0x00: creatures only.</summary>
		private const int TargetCritters = 0x00;

		/// <summary>Target type 0x40: tiles only.</summary>
		private const int TargetTiles = 0x40;

		/// <summary>Target type 0x80: tiles AND everything on them. This target type also bypasses
		/// the random filter - Reveal is meant to reveal reliably.</summary>
		private const int TargetEverything = 0x80;

		/// <summary>This many tiles in front of the player lies the centre of the area. Value from the
		/// reference table, the same for all four spells in uw1.</summary>
		private const int CentreDistanceTiles = 4;

		/// <summary>Radius of the area in tiles - two gives five by five. See the
		/// uncertainties in the class header.</summary>
		public const int TileRadius = 2;

		/// <summary>The hit count: DiceRoll(3, 4) = 3 to 12 (Class6Spells_seg038_3307_DBA). The
		/// reference had a flat 2.</summary>
		private const int HitDiceCount = 3;

		private const int HitDiceRange = 4;

		/// <summary>Tile mode runs over the area up to this many times while hits are left: the
		/// first pass and four more (the area walk, labels C59-C70).</summary>
		private const int MaxTilePasses = 5;

		/// <summary>The per-tile roll adds this to width times height: RNG % (w * h + 3) below the
		/// hits left (label B21).</summary>
		private const int TileRollExtra = 3;

		/// <summary>First animated object (ANIMO.GR). The effects below count from here,
		/// exactly as in the reference.</summary>
		private const int AnimationFirstId = UWObjectClassProperties.AnimationFirstId;

		/// <summary>Fire: the same object an impacting fireball leaves behind
		/// (450, "an explosion").</summary>
		private const int FlameEffectObjectId = AnimationFirstId + 2;

		/// <summary>Lightning: the same object as on the impact of an Electrical Bolt (453). The
		/// name in the string block is "a splash" and does not quite fit - but the number comes from
		/// the reference and is used the same way in both places there, see
		/// UWRunicMagic.GetProjectileImpactId.</summary>
		private const int LightningEffectObjectId = AnimationFirstId + 5;

		/// <summary>The sparkle above a confused creature (455, "a spell effect").</summary>
		private const int SpellEffectObjectId = AnimationFirstId + 7;

		/// <summary>Objects of major class 6 are traps and triggers. Reveal does not touch
		/// them, otherwise it would reveal itself.</summary>
		private const int TrapMajorClass = UWObjectMechanics.TrapMajorClass;

		/// <summary>
		/// Casts an area spell. Returns false if there is none for this minor class
		/// - then the caller has hit a spell we do not know yet.
		/// </summary>
		/// <param name="piMinorClass">Minor class without the upper bits (GetEffectMinor).</param>
		/// <param name="piTargetType">The upper bits (GetAreaTargetType).</param>
		public static bool Cast(int piMinorClass, int piTargetType, IUWSpellHost pIHost)
		{
			if (pIHost == null || !pIHost.HasPlayer || pIHost.CurrentLevel == null
				|| pIHost.CurrentLevel.TileData == null)
				return false;

			return CastAt(piMinorClass, piTargetType, GetCentreTile(pIHost), pIHost);
		}

		/// <summary>
		/// THE AREA AS UW.EXE WALKS IT (read 2026-09-27 for the creatures' area spells, which go
		/// through the same code with the creature as the caster): Class6Spells_seg038_3307_DBA
		/// rolls the hit count DiceRoll(3, 4) and calls the routine that runs code on targets around an object
		/// with distance 4 and radius 2, which moves from the caster's tile four tiles along its
		/// heading and hands the area walk (the same one UWCritterRules cites for the area of a theft or a trespass) the square from centre - 2 with
		/// width and height 5. That routine walks x and y from the corner TO corner + 5, both
		/// inclusive - six tiles each way, centre - 2 to centre + 3, as the original's loop does.
		///
		/// TILE MODE (target type 0x40, Sheet Lightning and Flame Wind): a tile that is not solid
		/// is struck when RNG % (5 * 5 + 3) lies below the hits left; every stroke uses one hit
		/// (both spells' routines return 1); with hits left after a pass, up to four more passes.
		/// The other target types go through the objects of every tile without a roll and in one
		/// pass, each effect using a hit.
		///
		/// Until 2026-09-27 ours followed the reference: a flat 2 hits, one pass, and a filter
		/// that skipped a tile when the roll was AT OR BELOW the hits left - the opposite test.
		/// </summary>
		public static bool CastAt(int piMinorClass, int piTargetType, UWTilePos pOCentre, IUWSpellHost pIHost)
		{
			if (pIHost == null || pIHost.CurrentLevel == null || pIHost.CurrentLevel.TileData == null)
				return false;

			if (piMinorClass != RevealMinor && piMinorClass != SheetLightningMinor
				&& piMinorClass != ConfusionMinor && piMinorClass != FlameWindMinor)
				return false;

			int liTargets = pIHost.CollectAreaTargets(pOCentre, TileRadius + 1);

			int liRemaining = UWRandom.RollDice(HitDiceCount, HitDiceRange);

			if (piTargetType == TargetTiles)
			{
				StrikeTiles(pOCentre, TileRadius, liRemaining, pIHost,
					pOTile => fApplyToTile(piMinorClass, pOTile, liTargets, pIHost));

				return true;
			}

			int liSide = (TileRadius * 2) + 1;
			int liX0 = pOCentre.X - TileRadius;
			int liY0 = pOCentre.Y - TileRadius;

			for (int liX = liX0; liX <= liX0 + liSide; liX++)
			{
				for (int liZ = liY0; liZ <= liY0 + liSide; liZ++)
				{
					UWTile lOTile = pIHost.CurrentLevel.GetTile(liX, liZ);

					if (lOTile == null)
						continue;

					liRemaining -= fApplyToTile(piMinorClass, lOTile, liTargets, pIHost);

					if (liRemaining <= 0)
						return true;
				}
			}

			return true;
		}

		/// <summary>
		/// The walk in TILE MODE (target type 0x40) on its own, for any code the original hands it
		/// - the class 6 effects above, and Tremor's boulders (UWMiscSpellRules). The square from
		/// centre - radius to centre + radius + 1 both ways (the loop is inclusive), a tile that is
		/// not solid struck when RNG % (side * side + 3) lies below the hits left, pOStrike returning
		/// how many hits it used, up to five passes while hits are left.
		/// </summary>
		public static void StrikeTiles(UWTilePos pOCentre, int piRadius, int piHits, IUWSpellHost pIHost,
			Func<UWTile, int> pOStrike)
		{
			if (pIHost == null || pIHost.CurrentLevel == null || pOStrike == null)
				return;

			int liSide = (piRadius * 2) + 1;
			int liX0 = pOCentre.X - piRadius;
			int liY0 = pOCentre.Y - piRadius;
			int liRemaining = piHits;

			for (int liPass = 0; liPass < MaxTilePasses; liPass++)
			{
				for (int liX = liX0; liX <= liX0 + liSide; liX++)
				{
					for (int liZ = liY0; liZ <= liY0 + liSide; liZ++)
					{
						UWTile lOTile = pIHost.CurrentLevel.GetTile(liX, liZ);

						if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid
							|| UWRandom.Next((liSide * liSide) + TileRollExtra) >= liRemaining)
							continue;

						liRemaining -= pOStrike(lOTile);

						if (liRemaining <= 0)
							return;
					}
				}
			}
		}

		/// <summary>
		/// The centre of the area: four tiles in the view direction.
		///
		/// The reference computes this with the character's heading in 256ths of a
		/// full circle; here the camera's view direction is the same thing, continuous, and the
		/// rounding to whole tiles evens out the difference.
		/// </summary>
		public static UWTilePos GetCentreTile(IUWSpellHost pIHost)
		{
			return GetCentreTile(pIHost, CentreDistanceTiles);
		}

		/// <summary>The same at another distance - Tremor's five tiles.</summary>
		public static UWTilePos GetCentreTile(IUWSpellHost pIHost, int piDistanceTiles)
		{
			UWTilePos lOFrom = pIHost.PlayerTile;

			pIHost.GetViewDirection(out float lfX, out float lfZ);

			return new UWTilePos(
				lOFrom.X + UWUnits.RoundToInt(lfX * piDistanceTiles),
				lOFrom.Y + UWUnits.RoundToInt(lfZ * piDistanceTiles));
		}

		/// <summary>
		/// Casts a TARGET SPELL of class 7 on the creatures in the area in front of the player,
		/// instead of on a clicked target.
		///
		/// WHAT FOR: a class 7 spell has no clicked target in the original - neither from an
		/// item, where the potion is drunk at once, nor from runes (per user, 2026-09-23, Ally
		/// on an acid slug; until then rune casting waited for a click). It takes effect on the
		/// creatures in front of the player.
		///
		/// THE AREA IS THE SAME AS FOR THE AREA SPELLS: centre four tiles ahead,
		/// radius two. It thus covers tiles TWO to SIX - the own tile and the one directly
		/// in front do NOT belong to it.
		///
		/// This is how we got there, because otherwise it gets repeated on the next reading: first it
		/// looked like tiles two to five, then an enemy right in front of the player's nose was
		/// hit after all, and finally three of them stood there and none was hit - it
		/// depends on the tile edge, it was said. That is exactly the explanation: an enemy in
		/// melee range lies on the boundary at two tiles and, depending on position, rounds
		/// sometimes above, sometimes below. The user cross-checked that it is NOT reliably
		/// hit (2026-09-09) - so no special rule, but the edge of the area.
		///
		/// With that, all observations fit the numbers the reference gives for its
		/// area spells anyway. It knows no effect for this case, but it would not have
		/// invented the geometry twice.
		///
		/// EXACTLY ONE CREATURE IS HIT, even if several stand in the area (measured by
		/// user) - see TargetSpellMaxEffects. So the order below alone
		/// decides WHICH.
		///
		/// THE MOST DISTANT CREATURE IS HIT. This is measured too: the user
		/// cross-checked it against the view direction, and it holds (2026-09-09).
		///
		/// THIS IS A REAL SORT, not a side effect of a scan order. At first it
		/// seemed likely that it fell out of the order in which the reference processes its only
		/// area routine (callbacks.RunCodeOnTargetsInArea walks the square X
		/// ascending and within that Y ascending, i.e. from a fixed corner). Then the
		/// selection would have had to reverse when turning - it does not. So here it is sorted by
		/// distance.
		///
		/// The reference says nothing about this: it knows no effect at all for a target spell
		/// from an item.
		///
		/// NO RANDOM FILTER: that belongs to the tile pass of the area spells, which do
		/// something per tile. This is about creatures, and they are rare enough anyway.
		/// </summary>
		public static bool CastTargetSpellInArea(int piMinorClass, IUWSpellHost pIHost)
		{
			if (pIHost == null || !pIHost.HasPlayer || pIHost.CurrentLevel == null
				|| pIHost.CurrentLevel.TileData == null)
				return false;

			UWTilePos lOCentre = GetCentreTile(pIHost);

			int liTargets = pIHost.CollectAreaTargets(lOCentre, TileRadius);

			// THE MOST DISTANT FIRST. Measured, see header comment - so it is sorted
			// instead of walked tile by tile.
			List<int> lOSorted = new List<int>();

			for (int liAt = 0; liAt < liTargets; liAt++)
			{
				if (!pIHost.AreaTargetIsCreature(liAt))
					continue;

				UWTilePos lOTile = pIHost.AreaTargetTile(liAt);

				if (System.Math.Abs(lOTile.X - lOCentre.X) > TileRadius
					|| System.Math.Abs(lOTile.Y - lOCentre.Y) > TileRadius)
					continue;

				lOSorted.Add(liAt);
			}

			lOSorted.Sort((piLeft, piRight) =>
				pIHost.AreaTargetDistanceSquared(piRight).CompareTo(pIHost.AreaTargetDistanceSquared(piLeft)));

			int liRemaining = TargetSpellMaxEffects;

			foreach (int liTarget in lOSorted)
			{
				if (!pIHost.CastTargetSpellOnAreaTarget(liTarget, piMinorClass))
					continue;

				pIHost.SpawnEffectAtAreaTarget(liTarget, SpellEffectObjectId);

				if (--liRemaining <= 0)
					break;
			}

			return true;
		}

		/// <summary>
		/// This many creatures a target spell from an item hits: EXACTLY ONE.
		///
		/// Measured, not estimated: the user had several enemies in view at once with the
		/// poison potion, and the effect always appeared on only one (2026-09-09). The
		/// class 6 area spells, by contrast, may cause two effects - see
		/// MaxEffects.
		/// </summary>
		private const int TargetSpellMaxEffects = 1;

		/// <summary>The effect on ONE tile. Returns how many single effects
		/// resulted - zero means the tile had nothing to offer and does not count
		/// against the hit count.</summary>
		private static int fApplyToTile(int piMinorClass, UWTile pOTile, int piTargets, IUWSpellHost pIHost)
		{
			switch (piMinorClass)
			{
				case SheetLightningMinor:
					// DiceRoll(6, 5), so 6 to 30, of type 3, pure magic (DamageObjectsInTile with
					// kind 2: tables 9D3/9D5/9D7).
					fBurnTile(pOTile, LightningEffectObjectId, LightningDiceCount, LightningDiceRange,
						UWDamageTypes.Magic, pIHost);

					return 1;

				case FlameWindMinor:
					return fFlameWind(pOTile, pIHost);

				case ConfusionMinor:
					return fConfuseTile(pOTile, piTargets, pIHost);

				case RevealMinor:
					return fRevealTile(pOTile, pIHost);

				default:
					return 0;
			}
		}

		/// <summary>
		/// Flame Wind: fire not only on the tile hit, but in a CROSS around it -
		/// centre plus the four neighbours. That is why the spell reaches further than the area
		/// suggests.
		/// </summary>
		private static int fFlameWind(UWTile pOTile, IUWSpellHost pIHost)
		{
			int liX = pOTile.X;
			int liZ = pOTile.Z;

			fBurnAt(liX, liZ, pIHost);
			fBurnAt(liX + 1, liZ, pIHost);
			fBurnAt(liX - 1, liZ, pIHost);
			fBurnAt(liX, liZ + 1, pIHost);
			fBurnAt(liX, liZ - 1, pIHost);

			return 1;
		}

		private static void fBurnAt(int piX, int piZ, IUWSpellHost pIHost)
		{
			UWTile lOTile = pIHost.CurrentLevel?.GetTile(piX, piZ);

			if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
				return;

			// DiceRoll(10, 6), so 10 to 60, of type 0x0B, fire - it bounces off a fire elemental
			// (DamageObjectsInTile with kind 1).
			fBurnTile(lOTile, FlameEffectObjectId, FlameDiceCount, FlameDiceRange, UWDamageTypes.Fire, pIHost);
		}

		private const int FlameDiceCount = 10;

		private const int FlameDiceRange = 6;

		private const int LightningDiceCount = 6;

		private const int LightningDiceRange = 5;

		/// <summary>Places the effect on the tile and deals damage to everything that stands on
		/// it. DamageObjectsInTile_seg038_3307_182A rolls the dice anew FOR EVERY OBJECT of the
		/// tile's list (ours rolled once per tile until 2026-09-27) and charges the caster - and
		/// that list holds the loose objects and, while playing, the player himself (see
		/// UWObjectDamageRules; ours left both out until 2026-09-27).</summary>
		private static void fBurnTile(UWTile pOTile, int piEffectObjectId, int piDiceCount, int piDiceRange,
			int piDamageType, IUWSpellHost pIHost)
		{
			pIHost.SpawnEffectOnTileFloor(pOTile.X, pOTile.Z, piEffectObjectId);

			pIHost.DamageObjectsInTile(pOTile.X, pOTile.Z, piDiceCount, piDiceRange, piDamageType);
		}

		/// <summary>Confuses every creature on the tile. Counts per creature, not per tile -
		/// the reference does the same.</summary>
		private static int fConfuseTile(UWTile pOTile, int piTargets, IUWSpellHost pIHost)
		{
			UWTilePos lOTile = new UWTilePos(pOTile.X, pOTile.Z);
			int liCount = 0;

			for (int liAt = 0; liAt < piTargets; liAt++)
			{
				if (pIHost.AreaTargetTile(liAt) != lOTile || !pIHost.AreaTargetIsCreature(liAt))
					continue;

				if (!pIHost.ConfuseAreaTarget(liAt))
					continue;

				pIHost.SpawnEffectAtAreaTarget(liAt, SpellEffectObjectId);

				liCount++;
			}

			return liCount;
		}

		/// <summary>
		/// Reveal: fires the look trigger on everything on the tile, without having to
		/// look at it.
		///
		/// That is the entire effect of the spell, and it is narrower than the name
		/// suggests: it briefly raises the SEARCH skill to 0x2D and fires the
		/// look triggers in range. Where none is attached, Reveal does nothing - on level 1
		/// there is exactly one, and it has no search check at all. In the whole game the
		/// spell is worthwhile at nine spots (see the removed tool UWLookTriggerDump).
		///
		/// A message only appears if a text trap is behind the trigger. Most
		/// are terrain or create-object traps: there the world changes, the
		/// message window shows nothing.
		///
		/// Traps and triggers themselves are left out, otherwise the spell would reveal itself.
		/// Iterates over a copy of the list, because a fired trap can put objects into the
		/// tile.
		/// </summary>
		private static int fRevealTile(UWTile pOTile, IUWSpellHost pIHost)
		{
			if (pOTile.ObjectsInTile == null || pOTile.ObjectsInTile.Count == 0)
				return 0;

			UWObject[] lOObjects = pOTile.ObjectsInTile.ToArray();
			int liCount = 0;

			foreach (UWObject lOObject in lOObjects)
			{
				if (lOObject == null || (lOObject.ID >> 6) == TrapMajorClass)
					continue;

				if (pIHost.FireLookTrigger(lOObject, UWTrapRules.RevealSearchSkill))
					liCount++;
			}

			return liCount;
		}

	}
}
