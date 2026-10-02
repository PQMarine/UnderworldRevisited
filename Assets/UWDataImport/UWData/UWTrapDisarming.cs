using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Detecting and disarming the trap on an object (reference: trapdisarming). Engine-free
	/// since 2026-09-18 (P3 of the engine separation), out of Interaction, which keeps the
	/// question and fires the trap when an attempt sets it off.
	/// </summary>
	public static class UWTrapDisarming
	{
		/// <summary>String block 1: "You found a trap!  Do you wish to try to disarm it?"</summary>
		public const int TrapFoundMessage = 99;

		/// <summary>
		/// The three disarm messages are NOT in STRINGS.PAK, but in
		/// UW.EXE - just like the missing ammunition. The reference
		/// names numbers in string block 1 for them, where our entries are empty.
		///
		/// Found in the game binary (2026-09-10), including the original's typo:
		/// "dearmed" instead of "disarmed".
		/// </summary>
		private const string TrapDisarmedSuffix = " was successfully dearmed.";

		private const string TrapSetOffMessage = "Your bumbling attempts have set off the ";

		private const string TrapDisarmFailedMessage = "Unable to defuse trap.";

		/// <summary>Target value of both checks - the reference uses a fixed eight in uw1.</summary>
		private const int TrapCheckDifficulty = 8;

		/// <summary>The spell Remove Trap searches and disarms with this skill.</summary>
		public const int SpellTrapSkill = 45;

		/// <summary>The outcome of an attempt.</summary>
		public struct Result
		{
			/// <summary>The line for the scroll.</summary>
			public string Message;

			/// <summary>A critical failure: the trap goes off on the object's tile.</summary>
			public UWObject TrapToFire;

			/// <summary>The trigger between item and trap, or null when the trap hangs directly -
			/// see UWTrapRules.SetOffDisarmableTrap.</summary>
			public UWObject TriggerToFire;
		}

		/// <summary>
		/// Is a trap noticed when looking?
		///
		/// THE CHECK IS SEARCH against eight (reference: trapdisarming.DetectTrapTrigger and
		/// DoTrapSkillCheck). If you spot it, the original asks whether you want to disarm it
		/// - with a pre-typed "Yes".
		///
		/// Detection happens ANEW EACH TIME. Whoever notices nothing can look again; there is no
		/// memory list.
		/// </summary>
		public static bool Detect(UWObject pOTarget, List<UWObject> pOMasterlist, int piSearchSkill)
		{
			UWObject lOTrap;
			UWObject lOTrigger;

			if (pOTarget == null || pOMasterlist == null
				|| !UWObjectMechanics.TryFindDisarmableTrap(pOTarget, pOMasterlist, out lOTrap, out lOTrigger))
				return false;

			return UWSkillCheck.IsSuccess(UWSkillCheck.Check(piSearchSkill, TrapCheckDifficulty));
		}

		/// <summary>
		/// The spell Remove Trap (reference: trapdisarming.TrapDisarmSpell).
		///
		/// IT DOES NOT ASK AND ROLLS WITH FIXED VALUES: 45 for detecting, 45 for
		/// disarming - both well above anything a character ever reaches. The
		/// spell thus almost always succeeds, but can fail just like an attempt by
		/// hand, and then the trap goes off.
		///
		/// Returns false if no trap hangs on the target at all, or the spell does not find it -
		/// then the caller reports "The spell has no discernable effect.".
		/// </summary>
		public static bool SpellFinds(UWObject pOTarget, List<UWObject> pOMasterlist)
		{
			return Detect(pOTarget, pOMasterlist, SpellTrapSkill);
		}

		/// <summary>
		/// The disarm attempt: Traps skill against eight.
		///
		///   success          the trap is gone
		///   failure          nothing happens
		///   critical failure the trap goes off (Result.TrapToFire)
		///
		/// THE CHAIN IS DISARMED, not the trap itself: DefuseTrap_sub_8E27D clears the whole chain
		/// under the item's special link (see ClearObjectChain_seg027_772 on word +6), trigger and
		/// trap alike - so the item's special link goes to 0 either way. Until 2026-09-30 a
		/// trigger in between had its next link cut instead, which left the item armed.
		/// </summary>
		public static Result Disarm(UWObject pOTarget, UWLevel pOLevel, int piTrapsSkill, DataImport pOData)
		{
			Result lOResult = new Result();
			List<UWObject> lOMasterlist = pOLevel != null ? pOLevel.Masterlist : null;

			UWObject lOTrap;
			UWObject lOTrigger;

			if (pOTarget == null || lOMasterlist == null
				|| !UWObjectMechanics.TryFindDisarmableTrap(pOTarget, lOMasterlist, out lOTrap, out lOTrigger))
				return lOResult;

			UWSkillCheck.ResultEnum leResult = UWSkillCheck.Check(piTrapsSkill, TrapCheckDifficulty);

			string lsTrapName = GetTrapName(lOTrap, pOData);

			if (UWSkillCheck.IsSuccess(leResult))
			{
				fClearChain(pOTarget, pOLevel);

				lOResult.Message = "The " + lsTrapName + " on the "
					+ UWItemDescriptions.GetBareName(pOTarget.ID, pOData) + TrapDisarmedSuffix;

				return lOResult;
			}

			if (leResult == UWSkillCheck.ResultEnum.CriticalFailure)
			{
				lOResult.Message = TrapSetOffMessage + lsTrapName;
				lOResult.TrapToFire = lOTrap;
				lOResult.TriggerToFire = lOTrigger;

				return lOResult;
			}

			lOResult.Message = TrapDisarmFailedMessage;

			return lOResult;
		}

		/// <summary>
		/// See ClearObjectChain_seg027_772 on the item's special link (UWTrapChainRemoval): the trap and
		/// what hangs behind it leave the level, their static slots go back on the free list.
		/// Checked on a save of the original taken right after a successful disarm (per user,
		/// 2026-09-30, SAVE3): the potion's special link 0, the trap's slot free. Since 2026-10-01
		/// the original's routine itself, shared with the one-shot trigger cleanup; a trigger in
		/// between now goes as ovr153_135C has it.
		/// </summary>
		private static void fClearChain(UWObject pOItem, UWLevel pOLevel)
		{
			UWTrapChainRemoval.ClearSpecialChain(pOItem, pOLevel);
		}

		/// <summary>
		/// What the trap is called in the message.
		///
		/// A damage trap with the owner field set is a POISON trap - the same field
		/// the effect depends on (see UWTrapRules). Otherwise
		/// the reference takes the object name.
		/// </summary>
		public static string GetTrapName(UWObject pOTrap, DataImport pOData)
		{
			if (pOTrap.ID == UWObjectMechanics.DamageTrapId && pOTrap.Owner != 0)
				return "poison trap";

			return UWItemDescriptions.GetBareName(pOTrap.ID, pOData);
		}
	}
}
