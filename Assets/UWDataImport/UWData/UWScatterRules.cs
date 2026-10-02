using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// How the original puts an object down AROUND a point - the loot a creature spills and the
	/// remains it leaves. READ 2026-09-25 (InsertObjectToList_seg026_133F, with its callers
	/// MoveObjectToCoordinates_seg026_122B, PlaceObjectAtNPC_seg026_12E5 and SpillInventory_ovr150_0):
	///
	/// the point is given in EIGHTHS of a tile (tile * 8 + the sub-tile spot) together with a
	/// radius; up to 24 times a spot is rolled, each axis on its own, centre - radius plus
	/// 0 to 2 * radius, and the first one the object FITS at is taken - so the spot can lie in a
	/// neighbouring tile. If none of the 24 fits, the culling test decides (ObjectCulling with
	/// the range 0x0A, the one the liquids use): what it culls is gone, what it spares lies on
	/// the point itself.
	///
	/// The radius is 6 eighths for what a creature carries (SpillInventory) and 4 for the
	/// corpse it may leave; its blood goes straight onto its own spot (DropNPCRemains).
	///
	/// WHAT "FITS" MEANS in the original (its item-fit test in segment 26, see UWEasyMovement)
	/// is its full motion test: no wall and no higher floor under the object's footprint, and no object in the way
	/// that cannot be stood on. The host checks the tile part - open, the open half of a
	/// diagonal, a floor no higher than the point - over every eighth of the object's radius (since
	/// 2026-09-28, before only at the point, so loot could lie against a wall); the object part
	/// belongs to the motion code that stays on Unity's physics (Todo.md 11.6, P6).
	///
	/// Until 2026-09-25 ours scattered within the death tile only, and the remains lay exactly
	/// on the body.
	/// </summary>
	public static class UWScatterRules
	{
		public const int Tries = 24;

		/// <summary>SpillInventory hands MoveObjectToCoordinates 6.</summary>
		public const int LootRadius = 6;

		/// <summary>The remains go down with 4.</summary>
		public const int RemainsRadius = 4;

		/// <summary>Rolls up to Tries spots around the centre and returns the first that
		/// pbFits accepts; false if none did.</summary>
		public static bool TryPickSpot(int piCentreX, int piCentreY, int piRadius,
			Func<int, int, bool> pbFits, out int piX, out int piY)
		{
			int liSpan = (piRadius * 2) + 1;

			for (int liTry = 0; liTry < Tries; liTry++)
			{
				piX = piCentreX - piRadius + UWRandom.Next(liSpan);
				piY = piCentreY - piRadius + UWRandom.Next(liSpan);

				if (pbFits == null || pbFits(piX, piY))
					return true;
			}

			piX = piCentreX;
			piY = piCentreY;

			return false;
		}
	}
}
