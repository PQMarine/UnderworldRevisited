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
	/// Engine-free since 2026-09-18 (P3): what stands in the area is collected by the world
	/// (IUWWorld.CollectAreaTargets, physics on the Unity side) and held here as a list for the
	/// cast (UWAreaTarget; numbered entries on the spell host until cleanup step 3.6, 2026-10-17);
	/// the effect pictures and the damage go back through the world, the damage charged to the
	/// cast's caster (UWCastContext).
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
			if (pIHost == null || !pIHost.HasPlayer || UWSpellCasting.LevelOf(pIHost) == null
				|| UWSpellCasting.LevelOf(pIHost).TileData == null)
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
			if (pIHost == null || UWSpellCasting.LevelOf(pIHost) == null
				|| UWSpellCasting.LevelOf(pIHost).TileData == null)
				return false;

			if (piMinorClass != RevealMinor && piMinorClass != SheetLightningMinor
				&& piMinorClass != ConfusionMinor && piMinorClass != FlameWindMinor)
				return false;

			List<UWAreaTarget> lOTargets = new List<UWAreaTarget>();

			pIHost.World.CollectAreaTargets(pOCentre, TileRadius + 1, lOTargets);

			int liRemaining = UWRandom.RollDice(HitDiceCount, HitDiceRange);

			if (piTargetType == TargetTiles)
			{
				StrikeTiles(pOCentre, TileRadius, liRemaining, pIHost,
					pOTile => fApplyToTile(piMinorClass, pOTile, lOTargets, 1, pIHost));

				return true;
			}

			int liSide = (TileRadius * 2) + 1;
			int liX0 = pOCentre.X - TileRadius;
			int liY0 = pOCentre.Y - TileRadius;

			for (int liX = liX0; liX <= liX0 + liSide; liX++)
			{
				for (int liZ = liY0; liZ <= liY0 + liSide; liZ++)
				{
					UWTile lOTile = UWSpellCasting.LevelOf(pIHost).GetTile(liX, liZ);

					if (lOTile == null)
						continue;

					liRemaining -= fApplyToTile(piMinorClass, lOTile, lOTargets, liRemaining, pIHost);

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
			if (pIHost == null || UWSpellCasting.LevelOf(pIHost) == null || pOStrike == null)
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
						UWTile lOTile = UWSpellCasting.LevelOf(pIHost).GetTile(liX, liZ);

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
		/// The centre of the area: four tiles along the player's heading, as gronk_area moves
		/// there with move_along (MoveAlong) from the player's tile and his 256-step heading
		/// (UWHeadings.DegreesToHeadingByte, the yaw truncated as the original's facing is).
		/// </summary>
		public static UWTilePos GetCentreTile(IUWSpellHost pIHost)
		{
			return GetCentreTile(pIHost, CentreDistanceTiles);
		}

		/// <summary>The same at another distance - Tremor's five tiles.</summary>
		public static UWTilePos GetCentreTile(IUWSpellHost pIHost, int piDistanceTiles)
		{
			UWTilePos lOFrom = UWSpellCasting.CasterTile(pIHost);

			return MoveAlong(lOFrom, UWHeadings.DegreesToHeadingByte(pIHost.World.Player.ViewYawDegrees),
				piDistanceTiles);
		}

		/// <summary>
		/// MOVE_ALONG (UnderworldExhumed UTIL.C, read 2026-10-12): piDistance tiles from pOFrom along
		/// a 256-step heading (0 north, clockwise). The table sine and cosine (cFstSinCos, the
		/// unblended entry - UWMotionTables.SinCosCoarse) of 0x140 - heading are cut to a byte
		/// (/ 0x80), times the distance, / 0x100, each step TRUNCATED and then, when not zero, one
		/// added away from zero. Off the axes the centre lies further out than a rounded one: a
		/// sideways share of 0.26 to 0.37 of four tiles gives two, not one. Until 2026-10-12 ours
		/// rounded the view direction times the distance. Also the creatures' area spells.
		/// </summary>
		public static UWTilePos MoveAlong(UWTilePos pOFrom, int piHeading, int piDistance)
		{
			UWMotionTables.SinCosCoarse(((MoveAlongQuarterTurn - piHeading) & 0xFF) << 8, out int liSin, out int liCos);

			return new UWTilePos(pOFrom.X + fMoveAlongStep(liCos, piDistance), pOFrom.Y + fMoveAlongStep(liSin, piDistance));
		}

		/// <summary>move_along's 0x140: the compass heading turned into the sine table's angle.</summary>
		private const int MoveAlongQuarterTurn = 0x140;

		private static int fMoveAlongStep(int piTable, int piDistance)
		{
			int liStep = (short)((piTable / 0x80) * piDistance) / 0x100;

			if (liStep > 0)
				liStep++;
			else if (liStep < 0)
				liStep--;

			return liStep;
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
		/// WHICH ONE: THE FIRST IN process_area's ORDER (UnderworldExhumed SPELLS.C, nail_1area ->
		/// gronk_area): the square from centre - 2 to centre + 3 on both axes (x0 = centre - 2,
		/// width 5, the loops run to x0 + 5 inclusive), x ascending and within it y ascending, each
		/// tile's creatures in list order; the first the spell counts on takes it. Confirmed by the
		/// user on the original 2026-10-14 (audit B1): two goblins two and five tiles ahead in an
		/// east-west row - facing east the NEAR one, facing west the FAR one, i.e. always the
		/// western one. Until then ours sorted by distance and took the farthest, from an
		/// observation of 2026-09-09 that fits this order when facing west or north.
		///
		/// NO RANDOM FILTER: that belongs to the tile pass of the area spells, which do
		/// something per tile. This is about creatures, and they are rare enough anyway.
		/// </summary>
		public static bool CastTargetSpellInArea(int piMinorClass, IUWSpellHost pIHost)
		{
			if (pIHost == null || !pIHost.HasPlayer || UWSpellCasting.LevelOf(pIHost) == null
				|| UWSpellCasting.LevelOf(pIHost).TileData == null)
				return false;

			UWTilePos lOCentre = GetCentreTile(pIHost);

			List<UWAreaTarget> lOTargets = new List<UWAreaTarget>();

			pIHost.World.CollectAreaTargets(lOCentre, TileRadius, lOTargets);

			// process_area's square and order, see the header comment.
			int liX0 = lOCentre.X - TileRadius;
			int liY0 = lOCentre.Y - TileRadius;
			int liSide = TileRadius * 2 + 1;
			List<int> lOSorted = new List<int>();

			for (int liAt = 0; liAt < lOTargets.Count; liAt++)
			{
				if (!lOTargets[liAt].IsCreature)
					continue;

				UWTilePos lOTile = lOTargets[liAt].Tile;

				if (lOTile.X < liX0 || lOTile.X > liX0 + liSide || lOTile.Y < liY0 || lOTile.Y > liY0 + liSide)
					continue;

				lOSorted.Add(liAt);
			}

			// x ascending, then y ascending; within a tile the order they were collected in.
			lOSorted.Sort((piLeft, piRight) =>
			{
				UWTilePos lOLeft = lOTargets[piLeft].Tile;
				UWTilePos lORight = lOTargets[piRight].Tile;

				if (lOLeft.X != lORight.X)
					return lOLeft.X.CompareTo(lORight.X);

				return lOLeft.Y != lORight.Y ? lOLeft.Y.CompareTo(lORight.Y) : piLeft.CompareTo(piRight);
			});

			int liRemaining = TargetSpellMaxEffects;

			foreach (int liTarget in lOSorted)
			{
				UWTargetSpellRules.Result leResult = pIHost.World.CastTargetSpellOnAreaTarget(lOTargets[liTarget],
					piMinorClass);

				if (leResult == UWTargetSpellRules.Result.Missed)
					continue;

				// The picture only where the spell took (UWTargetSpellRules.Result).
				if (leResult == UWTargetSpellRules.Result.Shown)
					pIHost.World.ShowEffectAtAreaTarget(lOTargets[liTarget], SpellEffectObjectId, EffectSeconds);

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

		/// <summary>The effect on ONE tile, at most piLimit single effects. Returns how many
		/// resulted - zero means the tile had nothing to offer and does not count against the
		/// hit count.
		///
		/// THE COUNT STOPS AT THE OBJECT THAT USES IT UP (process_area, UnderworldExhumed
		/// SPELLS.C, read 2026-10-12: count is decremented per object and the walk returns at
		/// once). Until 2026-10-12 ours finished the tile first, so Confusion on a crowded tile
		/// could take one or two more.</summary>
		private static int fApplyToTile(int piMinorClass, UWTile pOTile, List<UWAreaTarget> pOTargets, int piLimit,
			IUWSpellHost pIHost)
		{
			switch (piMinorClass)
			{
				case SheetLightningMinor:
					// DiceRoll(6, 5), so 6 to 30, of type 3, pure magic (DamageObjectsInTile with
					// kind 2: tables 9D3/9D5/9D7). The flash at a random height (sp_sheet_light).
					fSpawnStrikePicture(pOTile, LightningEffectObjectId, pIHost);
					fStrikeTile(pOTile.X, pOTile.Z, LightningDiceCount, LightningDiceRange, UWDamageTypes.Magic, pIHost);

					return 1;

				case FlameWindMinor:
					return fFlameWind(pOTile, pIHost);

				case ConfusionMinor:
					return fConfuseTile(pOTile, pOTargets, piLimit, pIHost);

				case RevealMinor:
					return fRevealTile(pOTile, piLimit, pIHost);

				default:
					return 0;
			}
		}

		/// <summary>
		/// Flame Wind: fire not only on the tile hit, but in a CROSS around it -
		/// centre plus the four neighbours. That is why the spell reaches further than the area
		/// suggests.
		///
		/// THE PICTURE IS ONE EXPLOSION on the struck tile, at a random height, with three to five
		/// flames scattered around it; the cross only takes the damage, solid tiles included
		/// (sp_meteor and fireball_effect, UnderworldExhumed SPELLS.C and EFFECT.C, read
		/// 2026-10-12). Until 2026-10-12 ours put an explosion on the floor of each of the five
		/// tiles and skipped solid neighbours.
		/// </summary>
		private static int fFlameWind(UWTile pOTile, IUWSpellHost pIHost)
		{
			int liX = pOTile.X;
			int liZ = pOTile.Z;

			int liZPos = fSpawnStrikePicture(pOTile, FlameEffectObjectId, pIHost);

			fScatterFlames(liX, liZ, liZPos, pIHost);

			fBurnAt(liX, liZ, pIHost);
			fBurnAt(liX + 1, liZ, pIHost);
			fBurnAt(liX - 1, liZ, pIHost);
			fBurnAt(liX, liZ + 1, pIHost);
			fBurnAt(liX, liZ - 1, pIHost);

			return 1;
		}

		/// <summary>Deals the fire to everything on a tile of the cross. DamageObjectsInTile_seg038_3307_182A
		/// rolls the dice anew FOR EVERY OBJECT of the tile's list (ours rolled once per tile until
		/// 2026-09-27) and charges the caster - and that list holds the loose objects and, while
		/// playing, the player himself (see UWObjectDamageRules; ours left both out until
		/// 2026-09-27).</summary>
		private static void fBurnAt(int piX, int piZ, IUWSpellHost pIHost)
		{
			if (piX < 0 || piZ < 0 || piX >= MapSize || piZ >= MapSize)
				return;

			// DiceRoll(10, 6), so 10 to 60, of type 0x0B, fire - it bounces off a fire elemental
			// (DamageObjectsInTile with kind 1).
			fStrikeTile(piX, piZ, FlameDiceCount, FlameDiceRange, UWDamageTypes.Fire, pIHost);
		}

		/// <summary>
		/// UWTileBlast on the tile (IUWWorld.StrikeTile). The player's own spell strikes him too
		/// when his tile is in the area (per user, 2026-09-27, and PlacePlayerInTile keeping him in
		/// the tile list), and the loose objects there are worn down. The hits are charged to the
		/// caster (UWCastContext.AttackerIndex): the player's as the projectile's blast charges
		/// them, a creature's to that creature. The spell host's DamageObjectsInTile with its
		/// AreaCaster until cleanup step 3.6 (2026-10-17).
		/// </summary>
		private static void fStrikeTile(int piX, int piZ, int piDiceCount, int piDiceRange, int piDamageType,
			IUWSpellHost pIHost)
		{
			pIHost.World.StrikeTile(new UWTilePos(piX, piZ), piDiceCount, piDiceRange, piDamageType,
				pIHost.Cast.AttackerIndex);
		}

		/// <summary>This long an effect of an area spell stays in the world when the rule gives no
		/// time of its own - as with a projectile impact. The spell host's EffectSeconds until
		/// cleanup step 3.6 (2026-10-17).</summary>
		private const float EffectSeconds = 0.7f;

		/// <summary>An effect picture in a tile for a moment, at a fine spot (eighths, 0 to 7) and a
		/// height in zpos steps - for pfSeconds when given (EffectSeconds otherwise), its animation
		/// starting on piStartFrame (the spell host's SpawnEffectInTile until cleanup step 3.6).
		/// The presentation keeps the guards: no level, or a tile off the map, shows nothing.</summary>
		private static void fShowEffect(int piTileX, int piTileY, int piFineX, int piFineY, int piZPos,
			int piEffectObjectId, IUWSpellHost pIHost, float pfSeconds = -1f, int piStartFrame = 0)
		{
			pIHost.World.Presentation.ShowEffect(piEffectObjectId,
				new UWSpot(new UWTilePos(piTileX, piTileY), piFineX, piFineY, piZPos),
				pfSeconds > 0f ? pfSeconds : EffectSeconds, piStartFrame);
		}

		/// <summary>Tiles per side of a level map.</summary>
		private const int MapSize = 64;

		/// <summary>CreateObj's fine position for a new object: 3, 3 (MAPADDR.C).</summary>
		private const int NewObjectFine = 3;

		/// <summary>The ceiling in zpos steps; build_new_obj rolls below it.</summary>
		private const int CeilingZPos = 0x80;

		/// <summary>build_new_obj (SPELLS.C): the strike's picture at CreateObj's fine spot 3, 3 and
		/// a random height between the floor and the ceiling (RollStrikeZPos). Returns that
		/// height, which the flames of Flame Wind scatter around.</summary>
		private static int fSpawnStrikePicture(UWTile pOTile, int piEffectObjectId, IUWSpellHost pIHost)
		{
			int liZPos = RollStrikeZPos(pOTile.FloorHeight >> 1);

			fShowEffect(pOTile.X, pOTile.Z, NewObjectFine, NewObjectFine, liZPos, piEffectObjectId, pIHost);

			return liZPos;
		}

		/// <summary>build_new_obj's height: the floor's zpos (tile height * 8) plus rand % (0x80 -
		/// z) while it is below the ceiling.</summary>
		public static int RollStrikeZPos(int piFloorZPos)
		{
			return piFloorZPos < CeilingZPos ? piFloorZPos + UWRandom.Next(CeilingZPos - piFloorZPos) : piFloorZPos;
		}

		/// <summary>fireball_effect (EFFECT.C) around the explosion in the middle of the tile - the
		/// rule is UWExplosionRules.ScatterFlames, shared with the fireball's impact.</summary>
		private static void fScatterFlames(int piTileX, int piTileY, int piZPos, IUWSpellHost pIHost)
		{
			foreach (UWExplosionRules.Flame lOFlame in UWExplosionRules.ScatterFlames(FlameEffectObjectId,
				NewObjectFine, NewObjectFine, piZPos))
				fShowEffect(piTileX, piTileY, lOFlame.FineX, lOFlame.FineY, lOFlame.ZPos, lOFlame.ObjectId, pIHost,
					UWExplosionRules.LengthToSeconds(lOFlame.LengthTicks), lOFlame.StartFrame);
		}

		private const int FlameDiceCount = 10;

		private const int FlameDiceRange = 6;

		private const int LightningDiceCount = 6;

		private const int LightningDiceRange = 5;

		/// <summary>Confuses every creature on the tile. Counts per creature, not per tile -
		/// the reference does the same.</summary>
		private static int fConfuseTile(UWTile pOTile, List<UWAreaTarget> pOTargets, int piLimit, IUWSpellHost pIHost)
		{
			UWTilePos lOTile = new UWTilePos(pOTile.X, pOTile.Z);
			int liCount = 0;

			for (int liAt = 0; liAt < pOTargets.Count && liCount < piLimit; liAt++)
			{
				if (pOTargets[liAt].Tile != lOTile || !pOTargets[liAt].IsCreature)
					continue;

				UWTargetSpellRules.Result leResult = pIHost.World.ConfuseAreaTarget(pOTargets[liAt]);

				if (leResult == UWTargetSpellRules.Result.Missed)
					continue;

				// A resisted Confusion still counts (sp_confusion returns 1), without the picture.
				if (leResult == UWTargetSpellRules.Result.Shown)
					pIHost.World.ShowEffectAtAreaTarget(pOTargets[liAt], SpellEffectObjectId, EffectSeconds);

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
		private static int fRevealTile(UWTile pOTile, int piLimit, IUWSpellHost pIHost)
		{
			if (pOTile.ObjectsInTile == null || pOTile.ObjectsInTile.Count == 0)
				return 0;

			UWObject[] lOObjects = pOTile.ObjectsInTile.ToArray();
			int liCount = 0;

			foreach (UWObject lOObject in lOObjects)
			{
				if (lOObject == null || UWObjectIds.Major(lOObject.ID) == TrapMajorClass)
					continue;

				if (pIHost.FireLookTrigger(lOObject, UWTrapRules.RevealSearchSkill) && ++liCount >= piLimit)
					break;
			}

			return liCount;
		}

	}
}
