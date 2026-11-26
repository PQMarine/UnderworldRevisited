using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE RUNE SHELF IN ONE PLACE (cleanup 5.10, 2026-10-20): the up to three runes lying in the
	/// casting hollow (the original's player->shelf, kept with the save game at PLAYER.DAT 0x47),
	/// how a click adds, puts, clears and loads them. Which rune or strip of the rune bag's page (PANELS.GR 1)
	/// lies under a point was this class's too, until it went per game into UWRuneTabletGeometry (UW2 plan,
	/// part U4c step 1, 2026-11-23). Until then the selection lived in the classic HUD's
	/// UWHudRunes, which the modern rune panel, hollow, action bar and help window reached into; the
	/// append-then-trim rule was written twice there, the grid and the clear strip stood in
	/// UWHudPanel/UWHudRunes and UWModernRunePanel both, and the spell helpers (RunesOf, SequenceOf,
	/// HasRunes) sat on the modern panel. The Unity views draw it and turn screen points into the
	/// page's pixels: UWHudRunes and UWHudPanel for the classic frame, UWModernRunePanel and
	/// UWModernHud for the modern scheme. Whether a cast may start is UWSpellCasting.CanCast.
	///
	/// ONE HIT RULE FOR BOTH SCHEMES (decision 27 of the cleanup plan, decided by the user
	/// 2026-10-20): RuneCellAt, the original's gapless cells. Until then the two schemes differed -
	/// the classic shelf hit the 16 x 16 rune pictures (two clear columns more, a row shared with the
	/// rune below, the upper one winning), the modern panel the 14 x 14 compartments, both with gaps
	/// between them where a click did nothing. The original has neither: mous_in_rune
	/// (UnderworldExhumed RUNES.C:117) divides the whole panel into tiles of 18 columns by 15 rows
	/// with no gap between them - x from 3 in the panel's mouse area, rows from its bottom from 0x12
	/// on, below that the clear strip.
	/// </summary>
	public sealed class UWRuneShelfModel
	{
		/// <summary>No more than three: the fourth pushes out the left one (per user
		/// on the original, 2026-09-03). Matches the three slots in the save game (0x47-0x49).
		/// </summary>
		public const int Capacity = 3;

		/// <summary>
		/// A CAST STARTS A NEW SHELF (mous_in_rune and try_cast, UnderworldExhumed RUNES.C, read
		/// 2026-10-12): every cast attempt from the hollow sets a flag - also "Not a spell" and one
		/// refused as too soon - and the next rune clicked empties the hollow first. After In Lor a
		/// click on Ort gives "Ort", not "In Lor Ort". Clicking the hollow again recasts as before.
		/// Until 2026-10-12 ours always appended; without a cast in between the fourth rune still
		/// pushes out the first (per user, 2026-09-03).
		///
		/// ONE FLAG FOR THE WHOLE SESSION: the original's is a static byte that nothing resets, so a
		/// cast before restoring a game still empties the restored hollow at the first rune (per user
		/// on the original, 2026-10-13). Ours lived on the HUD component, which a restore builds anew;
		/// since cleanup 5.10 it is this class's, still static for the same reason.
		/// </summary>
		private static bool msbCastSinceLastRune;

		private readonly List<int> mOSelected = new List<int>(Capacity);

		/// <summary>The selected runes, from left to right. Read by UWSavegameWriter for the
		/// save game.</summary>
		public IReadOnlyList<int> Selected
		{
			get { return mOSelected; }
		}

		/// <summary>The selection as a rune sequence (UWRunicMagic.GetSequence), empty slots as
		/// UWRunicMagic.EmptyRune.</summary>
		public int Sequence
		{
			get { return SequenceOf(mOSelected); }
		}

		/// <summary>The rune at the given slot, or the empty value.</summary>
		public int RuneInSlot(int piSlot)
		{
			return piSlot >= 0 && piSlot < mOSelected.Count
				? mOSelected[piSlot]
				: UWRunicMagic.EmptyRune;
		}

		/// <summary>A rune into the hollow, appended on the right - from the fourth on the left one
		/// drops out, and the first rune after a cast starts a new shelf (msbCastSinceLastRune).</summary>
		public void Add(int piRune)
		{
			if (msbCastSinceLastRune)
				mOSelected.Clear();

			msbCastSinceLastRune = false;

			mOSelected.Add(piRune);

			while (mOSelected.Count > Capacity)
				mOSelected.RemoveAt(0);
		}

		/// <summary>Puts the given runes into the casting hollow, replacing what lay there - for the
		/// help window's spell list and the modern panel's (a click equips a spell, per user
		/// 2026-09-27). The caller makes sure the runes are in the bag.</summary>
		public void Put(IReadOnlyList<int> pORunes)
		{
			mOSelected.Clear();

			// A LAID SPELL COUNTS AS A WHOLE SPELL (per user 2026-11-26: "if the help laid a spell, one hardly wants to add
			// a single rune to it"): the next rune clicked by hand starts a new shelf, as after a cast. Until then the laid
			// runes took the next rune as their fourth.
			msbCastSinceLastRune = true;

			for (int liAt = 0; pORunes != null && liAt < pORunes.Count && mOSelected.Count < Capacity; liAt++)
				mOSelected.Add(pORunes[liAt]);
		}

		/// <summary>Empties the hollow - the clear strip, and Armageddon (UWMiscSpell). The flag of
		/// the last cast stays, as clear_shelf leaves it (RUNES.C).</summary>
		public void Clear()
		{
			mOSelected.Clear();
		}

		/// <summary>A cast from the hollow was tried (try_cast sets the flag before anything else):
		/// the next rune starts a new shelf.</summary>
		public void NoteCast()
		{
			msbCastSinceLastRune = true;
		}

		/// <summary>
		/// Takes over the three spell slots from the save game.
		///
		/// They are at 0x47 in PLAYER.DAT, one byte per slot, with 0x24 for empty. Whoever leaves
		/// the game with selected runes finds them in the hollow again on loading -
		/// until 2026-09-05 every save game in our version started with empty slots.
		///
		/// The order matters: the list is filled from left to right, and a
		/// fourth rune pushes out the first. The flag of the last cast is the session's and stays.
		/// </summary>
		public void Load(UWPlayerData pOPlayer)
		{
			mOSelected.Clear();

			if (pOPlayer == null)
				return;

			for (int liSlot = 0; liSlot < Capacity; liSlot++)
			{
				int liRune = pOPlayer.GetSelectedRune(liSlot);

				if (liRune < 0)
					continue;

				mOSelected.Add(liRune);
			}
		}

		// ------------------------------------------------- Rune sequences

		/// <summary>The runes of a sequence, first to last, empty slots left out.</summary>
		public static List<int> RunesOf(int piSequence)
		{
			List<int> lORunes = new List<int>(Capacity);

			for (int liShift = 10; liShift >= 0; liShift -= 5)
			{
				int liRune = (piSequence >> liShift) & 0x1F;

				if (liRune < UWRunicMagic.EmptyRune)
					lORunes.Add(liRune);
			}

			return lORunes;
		}

		/// <summary>The sequence of up to three runes, missing ones as UWRunicMagic.EmptyRune.</summary>
		public static int SequenceOf(IReadOnlyList<int> pORunes)
		{
			int liFirst = pORunes != null && pORunes.Count > 0 ? pORunes[0] : UWRunicMagic.EmptyRune;
			int liSecond = pORunes != null && pORunes.Count > 1 ? pORunes[1] : UWRunicMagic.EmptyRune;
			int liThird = pORunes != null && pORunes.Count > 2 ? pORunes[2] : UWRunicMagic.EmptyRune;

			return UWRunicMagic.GetSequence(liFirst, liSecond, liThird);
		}

		/// <summary>Whether every rune of the sequence is in the bag; false without a bag.</summary>
		public static bool HasRunes(int piSequence, UWPlayerData pOBag)
		{
			if (pOBag == null)
				return false;

			foreach (int liRune in RunesOf(piSequence))
			{
				if (!pOBag.HasRune((UWPlayerData.Rune)liRune))
					return false;
			}

			return true;
		}
	}
}
