using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SPELL A CARRIED THING CASTS WHEN USED (G4, 2026-11-16): the second game's checkSpell, useNSpellCharges and
	/// UseWand (uw2 obj/OBJUSE.C:229, :353, :492) for a thing used from the pack - a wand, a potion, food with a spell.
	///
	/// checkSpell: the thing's spell (UWEnchantSpellRulesUw2.TryDecode) is cast only when it is the kind cast on use
	/// (ID_FLAG11). The player may cast one every 0x2FD game clock ticks, about three game seconds ("You are not yet ready
	/// to cast another spell." with sound 0x15); UseFood clears that wait before and after, so a potion always takes. The
	/// cast goes to inanimate_spell with the player as caster and target: by class and minor for a major of 0 or more,
	/// by its number in the spell list for -1. Then one charge is spent.
	///
	/// useNSpellCharges: the spell object's quality is its charges. With none left the quality stays 0 (and decode misses
	/// it four times in ten); spending past 0 deletes the spell object four times in ten.
	///
	/// UseWand: a wand (0x98-0x9B) used from the pack sets off its traps, casts, and when its spell object is gone it
	/// becomes the broken wand (id + 4) with "With a loud &lt;SNAP!&gt;, the wand cracks.". A broken wand (0x9C-0x9F)
	/// does nothing, not even its traps.
	/// </summary>
	public static class UWCarriedSpellRulesUw2
	{
		public enum CastEnum
		{
			/// <summary>No spell cast on use (or decode missed it).</summary>
			None,

			/// <summary>Within the wait since the last cast: refused with its line and sound.</summary>
			NotReady,

			Cast
		}

		public const int FirstWand = 0x98;

		public const int LastWand = 0x9B;

		public const int FirstBrokenWand = 0x9C;

		public const int LastBrokenWand = 0x9F;

		/// <summary>A wand's broken form: item + 4 (SET_INCLASS + 4).</summary>
		public const int BrokenWandStep = 4;

		/// <summary>The potions UseReag sends to UseFood (0xE1-0xE7).</summary>
		public const int FirstPotion = 0xE1;

		public const int LastPotion = 0xE7;

		/// <summary>nextSpellTime's step: 0x2FD game clock ticks.</summary>
		public const int CastWait = 0x2FD;

		/// <summary>"You are not yet ready to cast another spell." (0xB; our block counts one higher).</summary>
		public const int NotReadyString = 0xB + 1;

		/// <summary>play_effect_here(0x15) with the refusal.</summary>
		public const int NotReadySound = 0x15;

		/// <summary>"With a loud &lt;SNAP!&gt;, the wand cracks." (0x8B).</summary>
		public const int WandCracksString = 0x8B + 1;

		/// <summary>The game clock before which the player casts nothing more from a thing (nextSpellTime). Not saved,
		/// as the original keeps it in memory only.</summary>
		private static int msNextSpellTime;

		public static bool IsWand(int piId) => piId >= FirstWand && piId <= LastWand;

		public static bool IsBrokenWand(int piId) => piId >= FirstBrokenWand && piId <= LastBrokenWand;

		public static bool IsPotion(int piId) => piId >= FirstPotion && piId <= LastPotion;

		/// <summary>UseFood's nextSpellTime = 0: the next cast from a thing waits for nothing.</summary>
		public static void ClearWait()
		{
			msNextSpellTime = 0;
		}

		/// <summary>
		/// checkSpell in the world (how 0, uw2 obj/OBJUSE.C:353): the thing itself is the source and there is no wait; the
		/// player touching an unbroken wand casts nothing. Decode, the cast on the one who used it, one charge. True when
		/// cast.
		/// </summary>
		public static bool CheckSpellInWorld(UWObject pOThing, IList<UWObject> pORecords, Func<int, int, bool> pfCast)
		{
			if (pOThing == null || IsWand(pOThing.ID))
				return false;

			if (!UWEnchantSpellRulesUw2.TryDecode(pOThing, pORecords, out UWObject _, out int liMajor, out int liEffect, out bool lbCastOnUse)
				|| !lbCastOnUse)
				return false;

			if (pfCast != null)
				pfCast(liMajor, liEffect);

			UseCharges(pOThing, pORecords, 1);

			return true;
		}

		/// <summary>
		/// checkSpell for the player (how nonzero): decode, the wait, the cast, one charge. pfCast(major, effect) casts -
		/// a major of 0 or more by class and minor, -1 by the spell's number (effect).
		/// </summary>
		public static CastEnum CheckSpell(UWObject pOThing, IList<UWObject> pORecords, int piGameClock, Func<int, int, bool> pfCast)
		{
			if (!UWEnchantSpellRulesUw2.TryDecode(pOThing, pORecords, out UWObject _, out int liMajor, out int liEffect, out bool lbCastOnUse)
				|| !lbCastOnUse)
				return CastEnum.None;

			if ((uint)piGameClock < (uint)msNextSpellTime)
				return CastEnum.NotReady;

			msNextSpellTime = piGameClock + CastWait;

			if (pfCast != null)
				pfCast(liMajor, liEffect);

			UseCharges(pOThing, pORecords, 1);

			return CastEnum.Cast;
		}

		/// <summary>useNSpellCharges: spends piCount charges of the spell cast on use in the thing's contents. Returns
		/// the charges left.</summary>
		public static int UseCharges(UWObject pOThing, IList<UWObject> pORecords, int piCount)
		{
			if (pOThing == null || pOThing.HasQuantity || pOThing.Quantity == 0)
				return 0;

			UWObject lOSpell = UWEnchantSpellRulesUw2.FindSpellObject(pOThing, pORecords);

			if (lOSpell == null || (lOSpell.Flags & CastOnUseFlag) == 0)
				return 0;

			int liCharges = lOSpell.Quality - piCount;

			if (liCharges >= 0)
				lOSpell.Quality = (ushort)Math.Min(liCharges, MaxCharges);
			else if (UWRandom.Next(10) < 4)
				fRemoveSpellObject(pOThing, lOSpell, pORecords);

			return Math.Max(liCharges, 0);
		}

		/// <summary>
		/// UseWand from the pack, after its traps (the caller's checkTrap with mode 4): the cast, then the crack when the
		/// spell object is gone - the wand's id goes up by four. True when it cracked (pfSay has the line). A broken wand
		/// does nothing.
		/// </summary>
		public static CastEnum UseWand(UWObject pOWand, IList<UWObject> pORecords, int piGameClock, Func<int, int, bool> pfCast,
			out bool pbCracked)
		{
			pbCracked = false;

			if (pOWand == null || !IsWand(pOWand.ID))
				return CastEnum.None;

			CastEnum leCast = CheckSpell(pOWand, pORecords, piGameClock, pfCast);

			if (leCast != CastEnum.NotReady && !pOWand.HasQuantity
				&& UWEnchantSpellRulesUw2.FindSpellObject(pOWand, pORecords) == null)
			{
				pOWand.ID = (ushort)(pOWand.ID + BrokenWandStep);
				pbCracked = true;
			}

			return leCast;
		}

		/// <summary>Bit 11 of the id word (ID_FLAG11), bit 2 of UWObject.Flags.</summary>
		private const int CastOnUseFlag = 0x4;

		/// <summary>Quality is six bits.</summary>
		private const int MaxCharges = 0x3F;

		/// <summary>Obj_Rem and Obj_Free of the spell object: out of the thing's contents, and out of the link chain when
		/// the thing is linked through the records.</summary>
		private static void fRemoveSpellObject(UWObject pOThing, UWObject pOSpell, IList<UWObject> pORecords)
		{
			if (pOThing.Contents != null)
			{
				pOThing.Contents.Remove(pOSpell);

				if (pOThing.Contents.Count == 0)
					pOThing.Quantity = 0;

				return;
			}

			int liIndex = pORecords != null ? pORecords.IndexOf(pOSpell) : -1;

			if (liIndex <= 0)
				return;

			if (pOThing.Quantity == liIndex)
			{
				pOThing.Quantity = pOSpell.Link;

				return;
			}

			foreach (UWObject lOAt in UWObjectChain.Walk(pOThing, pORecords, pORecords.Count))
				if (lOAt.Link == liIndex)
				{
					lOAt.Link = pOSpell.Link;

					return;
				}
		}
	}
}
