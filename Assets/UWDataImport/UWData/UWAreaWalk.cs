using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHO AN AREA SPELL OR A BLAST STRIKES, chosen as the original chooses it: by the squares of
	/// the area and the object list of each square, no physics (cleanup step 4.0, decision 11 of
	/// Docs/CLEANUP-PLAN.md, decided by the user 2026-10-18).
	///
	/// WHY: until 2026-10-18 the Unity side gathered the targets with a physics sphere and put a
	/// creature on the square its body stood on (the transform, which trails the record by up to a
	/// step), so a creature at a tile edge or high above the floor could be struck or spared where
	/// the original decides otherwise, and two creatures came in the order the physics reported
	/// them. The original walks the map: process_area and damage_square (UnderworldExhumed
	/// SPELLS.C). Our tile lists follow the record tile since the motion rework (UWCritter relinks
	/// on every tile change through UWWorldEdit.Move), so the walk runs on the level data alone.
	///
	/// Used by the area spells and the class 7 spells (UWAreaSpellRules), Tremor
	/// (UWMiscSpellRules) and every blast on a square (UWTileBlast: the explosion of a fireball or
	/// a lightning bolt, the strokes of Sheet Lightning and Flame Wind).
	/// </summary>
	public static class UWAreaWalk
	{
		/// <summary>AREA_CRITTERS (combat.h), target type 0x00 - the upper two bits of a spell's minor
		/// class (UWRunicMagic.GetAreaTargetType): every creature on the walked squares but the
		/// caster. Confusion and all five class 7 spells.</summary>
		public const int Critters = 0x00;

		/// <summary>AREA_RANDOM, type 0x40: open squares at random, the handler gets no object.
		/// Sheet Lightning, Flame Wind and Tremor.</summary>
		public const int RandomSquares = 0x40;

		/// <summary>AREA_OBJECTS, type 0x80: every object of every square, without the random roll -
		/// Reveal, meant to reveal reliably.</summary>
		public const int Objects = 0x80;

		/// <summary>AREA_ALL, type 0xC0: every object as well (the original tests it apart, with the
		/// same effect).</summary>
		public const int All = 0xC0;

		/// <summary>The squares per side of the map (process_area's MAP_SIZE).</summary>
		private const int MapSize = UWWorldScale.TilesPerAxis;

		/// <summary>AREA_RANDOM strikes a square when rand() % (w * h + 3) lies below the count left
		/// (the area walk in UW.EXE, label B21).</summary>
		private const int RandomRollExtra = 3;

		/// <summary>AREA_RANDOM walks the area again while the count lasts: the first pass and four
		/// more (process_area's tries++ &lt; 4; the area walk in UW.EXE, labels C59-C70).</summary>
		private const int RandomMorePasses = 4;

		/// <summary>
		/// One call of the area's action (the original's SpellFn): the square and, unless the walk
		/// is AREA_RANDOM, the object of that square's list. True when it took effect - then it
		/// counts against the walk's count.
		/// </summary>
		public delegate bool Handler(UWTile pOTile, UWObject pOTarget);

		/// <summary>
		/// gronk_area's call of process_area around a centre (SPELLS.C): the square from centre -
		/// radius with width and height 2 * radius + 1 - which process_area walks INCLUSIVELY, so
		/// to centre + radius + 1 on both axes. The centre itself is the caller's (move_along from
		/// the caster, UWAreaSpellRules.GetCentreTile, or from a creature's record).
		/// </summary>
		/// <param name="piSource">The caster's mobile index (the player's is
		/// UWCritterBrain.PlayerIndex); AREA_CRITTERS leaves the creature with that index out.</param>
		public static void Square(UWLevel pOLevel, UWTilePos pOCentre, int piRadius, int piCount, int piSource,
			int piType, Handler pOHandler)
		{
			int liSide = (piRadius * 2) + 1;

			Process(pOLevel, piCount, piSource, piType, pOCentre.X - piRadius, pOCentre.Y - piRadius, liSide, liSide,
				pOHandler);
		}

		/// <summary>
		/// PROCESS_AREA (UnderworldExhumed SPELLS.C, read 2026-10-18), step for step:
		/// - the rectangle is clipped to the map first (a w or h reaching past it is shortened, a
		///   negative x0 or y0 moved to 0 with the size shortened by as much); the clipped w and h
		///   are what AREA_RANDOM's roll uses;
		/// - x runs from x0 to x0 + w, and within each x, y from y0 to y0 + h, both inclusive;
		/// - AREA_RANDOM: a square that is not solid is struck when rand() % (w * h + 3) lies below
		///   the count left; a stroke that takes effect uses one, and the walk ends when none is
		///   left; with some left after a pass, up to four more passes;
		/// - otherwise the square's object list in its order: AREA_OBJECTS and AREA_ALL hand over
		///   every object, AREA_CRITTERS every creature (major class 1) whose mobile index is not
		///   the caster's; every call that takes effect uses one, and the walk ends at once when
		///   the count reaches zero - in the middle of a square.
		/// THE LIST AS IT WAS when the walk reached the square: the original follows the links as
		/// it goes, so an object the handler takes out is followed by the one after it, and what
		/// the handler links at the head (Obj_Add) is never reached. Ours walks a copy
		/// and passes over what has left the square meanwhile - the same, as long as nothing is
		/// added behind the current object (Reveal's traps may create objects; ours appends them,
		/// and they stay out of this walk, as they did when Reveal walked a copy of its own).
		/// </summary>
		public static void Process(UWLevel pOLevel, int piCount, int piSource, int piType, int piX0, int piY0, int piW,
			int piH, Handler pOHandler)
		{
			if (pOLevel == null || pOLevel.TileData == null || pOHandler == null)
				return;

			int liX0 = piX0;
			int liY0 = piY0;
			int liW = piW;
			int liH = piH;

			if (liX0 >= MapSize || liX0 + liW < 0 || liY0 >= MapSize || liY0 + liH < 0)
				return;

			if (liX0 < 0)
			{
				liW -= -liX0;
				liX0 = 0;
			}
			else if (liX0 + liW >= MapSize)
				liW -= liX0 + liW - MapSize;

			if (liY0 < 0)
			{
				liH -= -liY0;
				liY0 = 0;
			}
			else if (liY0 + liH > MapSize)
				liH -= liY0 + liH - MapSize;

			if (liW <= 0 || liH <= 0)
				return;

			int liCount = piCount;
			int liTries = 0;

			do
			{
				for (int liX = liX0; liX <= liX0 + liW; liX++)
				{
					for (int liY = liY0; liY <= liY0 + liH; liY++)
					{
						UWTile lOTile = pOLevel.GetTile(liX, liY);

						if (lOTile == null)
							continue;

						if (piType == RandomSquares)
						{
							if (lOTile.TileType != UWTile.TileTypeEnum.solid
								&& UWRandom.Next((liW * liH) + RandomRollExtra) < liCount
								&& pOHandler(lOTile, null) && --liCount == 0)
								return;

							continue;
						}

						if (fWalkList(pOLevel, lOTile, piType, piSource, pOHandler, ref liCount))
							return;
					}
				}
			}
			while (piType == RandomSquares && liCount > 0 && liTries++ < RandomMorePasses);
		}

		/// <summary>The square's list for process_area; true when the count ran out. Walked over the
		/// list as it was when the walk reached the square, each object only while it still lies
		/// there (see Process).</summary>
		private static bool fWalkList(UWLevel pOLevel, UWTile pOTile, int piType, int piSource, Handler pOHandler,
			ref int piCount)
		{
			List<UWObject> lOList = pOTile.ObjectsInTile;

			if (lOList == null || lOList.Count == 0)
				return false;

			foreach (UWObject lOObject in lOList.ToArray())
			{
				if (lOObject == null || !lOList.Contains(lOObject))
					continue;

				if (fWanted(pOLevel, lOObject, piType, piSource) && pOHandler(pOTile, lOObject) && --piCount <= 0)
					return true;
			}

			return false;
		}

		/// <summary>process_area's test of an object against the walk's type.</summary>
		private static bool fWanted(UWLevel pOLevel, UWObject pOObject, int piType, int piSource)
		{
			if (piType == Objects || piType == All)
				return true;

			if (piType != Critters || UWObjectIds.Major(pOObject.ID) != UWObjectIds.CreatureMajorClass)
				return false;

			return MobileIndexOf(pOLevel, pOObject) != piSource;
		}

		/// <summary>A creature's index in the level's mobile list (its Masterlist slot, as
		/// UWCritter.Index takes it); -1 when the level has no list or the object is not in it.</summary>
		public static int MobileIndexOf(UWLevel pOLevel, UWObject pOObject)
		{
			return pOLevel != null && pOLevel.Masterlist != null ? pOLevel.Masterlist.IndexOf(pOObject) : -1;
		}

		/// <summary>
		/// DAMAGE_SQUARE (UnderworldExhumed SPELLS.C, read 2026-10-18): a blast on a square strikes
		/// every object of the square's list in its order, each once, with no test of height, kind
		/// or place within the square - the creatures among them, and the player, whom the original
		/// keeps in his square's list (PHYSICS.C links him at its head with Obj_Add when he enters
		/// it; ours keeps him in no list, so the host strikes him first, pOPlayer, when he stands
		/// there).
		///
		/// What the blast itself puts on the square is not struck: damage_square takes each
		/// object's successor before it damages it, so a creature's remains or a bag's spilled
		/// contents are never reached - ours walks the list as it was before the first blow (a
		/// creature this blast kills drops its remains at once in ours, while the original lets it
		/// die over its death animation first). An object of that snapshot that has left the
		/// square meanwhile (destroyed, replaced by debris) is passed over.
		/// </summary>
		public static void DamageSquare(UWTile pOTile, Action pOPlayer, Action<UWObject> pOStrike)
		{
			if (pOTile == null)
				return;

			UWObject[] lOLying = pOTile.ObjectsInTile != null ? pOTile.ObjectsInTile.ToArray() : Array.Empty<UWObject>();

			pOPlayer?.Invoke();

			if (pOStrike == null)
				return;

			foreach (UWObject lOObject in lOLying)
			{
				if (lOObject == null || !pOTile.ObjectsInTile.Contains(lOObject))
					continue;

				pOStrike(lOObject);
			}
		}
	}
}
