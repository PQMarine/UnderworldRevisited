namespace UWDataImport.UWData
{
	/// <summary>
	/// Repairing at the anvil: the estimate and the attempt (reference: repair.RepairLogic and
	/// RepairObjectSkillCheck). Engine-free since 2026-09-18 (P3 of the engine separation),
	/// out of Interaction, which keeps the question, the anvil cutscene and the inventory.
	/// </summary>
	public static class UWRepairRules
	{
		// String block 1, our numbering.
		private const int RepairEstimatePrefix = 217;

		private const int RepairEstimateInfix = 218;

		private const int RepairEstimateSuffix = 219;

		private const int FirstRepairDifficultyWord = 220;

		/// <summary>"You cannot repair that." - what the anvil says to an item without durability,
		/// and to a destroyed item its fate spares. The five outcome lines sit around it in the
		/// original's order (repair_item prints string 0x8E plus the outcome): 141 "You destroy
		/// the ", 142 damaged, 144 no effect, 145 partially, 146 fully repaired.</summary>
		public const int CannotRepairMessage = 143;

		/// <summary>Condition 63 is as good as new.</summary>
		private const int FullQuality = UWObjectMechanics.MaxQuality;

		/// <summary>The critical failure destroys the item when a roll of 0 to 63 (rand and 0x3F)
		/// beats quality plus skill.</summary>
		private const int DestructionRollRange = 64;

		/// <summary>Otherwise it takes 4 plus 0 to 7 (rand and 7) off the quality.</summary>
		private const int FailureLossBase = 4;

		private const int FailureLossRollRange = 8;

		/// <summary>repair_item asks Obj_Elem_Fate(10, obj) before a destroyed item leaves.</summary>
		private const int DestroyedCullingRange = UWLiquidCulling.RangeBase;

		/// <summary>The outcome codes of do_repair (event/WORLDEV.C), in the original's values -
		/// CannotRepairMessage plus the code is the line it prints.</summary>
		public enum OutcomeEnum
		{
			Destroyed = -2,
			Damaged = -1,
			NotRepairable = 0,
			NoEffect = 1,
			Partially = 2,
			Fully = 3
		}

		/// <summary>
		/// A repair takes at least this many minutes of the game clock.
		///
		/// THE WHOLE COST, held against the original again on 2026-09-22 because the Todo still
		/// carried it as open: ovr107_163C (350204-350290) forms durability times three, less
		/// the Repair skill, less half the item's quality, and floors it at fifteen; the caller
		/// ItemRepair_ovr107_1754 multiplies that by 0x3C00 - a game minute - onto PLAYER.DAT
		/// 0xCE (label 1888). So the number is minutes, which is what Attempt returns and
		/// Interaction hands to AdvanceClockMinutes. The anvil cutscene of label 1848 is in
		/// Interaction.fPlayRepairCutscene, and the loudness of label 1871 is RepairNoise.
		/// Nothing of the row was missing.
		/// </summary>
		private const int RepairMinimumMinutes = 15;

		/// <summary>Repairing is loud (reference: repair.cs sets PlayerQuietness 0xF).</summary>
		public const int RepairNoise = 15;

		/// <summary>The outcome of an attempt.</summary>
		public struct Result
		{
			/// <summary>The line for the scroll.</summary>
			public string Message;

			/// <summary>The item broke completely and leaves the inventory.</summary>
			public bool Destroyed;

			/// <summary>What do_repair and repair_item made of it.</summary>
			public OutcomeEnum Outcome;

			/// <summary>How many minutes the game clock advances.</summary>
			public int Minutes;
		}

		/// <summary>
		/// The durability of an item, read as signed - from 0x80 it means "not possible"; -1 for
		/// anything that cannot be repaired at all. Melee weapons and clothing each carry it in
		/// their own table.
		///
		/// SLINGS, BOWS AND CROSSBOWS ARE NOT REPAIRABLE (audit 2026-10-14, A28; get_rep_diff,
		/// event/WORLDEV.C:601-615): the original answers -1 for the whole minor class 0x10-0x1F,
		/// launchers and ammunition alike, so the anvil says "You cannot repair that.". Until
		/// 2026-10-15 ours read the third byte of the missile record as a durability - that byte
		/// is the ammunition the launcher fires (0 sling, 2 bow, 1 crossbow), which made all three
		/// "trivial" to repair.
		/// </summary>
		public static int GetDurability(UWObject pOItem, DataImport pOData)
		{
			if (pOItem == null || pOData == null || pOData.ObjectClassProperties == null)
				return -1;

			UWObjectClassProperties lOProperties = pOData.ObjectClassProperties;

			UWObjectClassProperties.MeleeWeapon lOMelee;

			if (lOProperties.TryGetMeleeWeapon(pOItem.ID, out lOMelee))
				return (sbyte)lOMelee.Durability;

			UWObjectClassProperties.Wearable lOWearable;

			if (lOProperties.TryGetWearable(pOItem.ID, out lOWearable))
				return (sbyte)lOWearable.Durability;

			return -1;
		}

		/// <summary>
		/// The estimate and the question (reference: repair.RepairLogic).
		///
		/// THE ESTIMATE is a pure calculation, not a check: durability minus skill
		/// plus fifteen, and from that one of five words from "trivial" to "very
		/// difficult". So it reveals nothing the player could not work out
		/// themselves - but it does not use anything up either.
		///
		/// WHAT CANNOT BE REPAIRED has a durability from 0x80 - read as a
		/// signed number it is then negative. The reference checks exactly
		/// that; 0xFF in our table stands for "indestructible". Returns false then.
		/// </summary>
		public static bool TryGetQuestion(UWObject pOItem, DataImport pOData, int piRepairSkill, out string psQuestion)
		{
			psQuestion = null;

			int liDurability = GetDurability(pOItem, pOData);

			if (liDurability < 0)
				return false;

			int liScore = liDurability - piRepairSkill + 15;

			int liWord = liScore < 0 ? 0 : (liScore > 30 ? 4 : 1 + (liScore / 10));

			psQuestion = fMessage(pOData, RepairEstimatePrefix)
				+ fMessage(pOData, FirstRepairDifficultyWord + liWord)
				+ fMessage(pOData, RepairEstimateInfix)
				+ UWText.ObjectName(pOData, pOItem.ID)
				+ fMessage(pOData, RepairEstimateSuffix);

			return true;
		}

		/// <summary>
		/// The attempt itself (reference: repair.RepairObjectSkillCheck). Changes the item's
		/// quality; the caller removes a destroyed item, makes the noise and advances the clock.
		///
		/// The Repair skill is checked against the item's DURABILITY - the more
		/// durable, the harder to work on.
		///
		///   critical success    fully repaired at once
		///   success             three plus one fifth of the skill added
		///   failure             nothing
		///   critical failure    damage, now and then total loss
		///
		/// THE CRITICAL FAILURE as do_repair has it (event/WORLDEV.C:623-660, audit 2026-10-14,
		/// A27, built 2026-10-15): a roll of 0 to 63 that BEATS quality plus skill destroys the
		/// item, so a good item in skilled hands is safe (quality plus skill of 63 or more never
		/// breaks); otherwise it loses 4 to 11. Until 2026-10-15 ours followed the reference,
		/// which destroys when the roll does NOT beat it - quality 40 and skill 20 broke on 61 of
		/// 64 critical failures, in the original on 3. A loss that leaves nothing destroys it too.
		///
		/// FULLY OR PARTIALLY: only a gain that goes PAST 63 says "fully" (the quality is held at
		/// 63); one that lands on exactly 63 still says "partially", as in the original.
		///
		/// A DESTROYED ITEM ASKS ITS FATE (repair_item, WORLDEV.C:706-711): it leaves only when the
		/// culling test with range 10 lets it (Obj_Elem_Fate, UWObjectLimitRules.Culls); a
		/// tenacious item stays, the line becomes "You cannot repair that." without the name, and
		/// the quality is what do_repair left (unchanged after the roll, 0 after a loss).
		///
		/// THE LINE is the outcome's string, the item's name and a full stop (string 0x53 after
		/// the name; until 2026-10-15 ours stopped after the name).
		///
		/// A REPAIR TAKES TIME (per user from the original, 2026-09-17: seven uses of the anvil
		/// took the clock from late morning to early evening). UW.EXE ovr107_163C: durability
		/// times three, minus the skill, minus half the quality, at least 15; ItemRepair adds that
		/// times 256 times 60 to the clock at 0xCE, which is that many minutes of our clock. The
		/// reference has Math.Min there, which would cap it at 15 instead. Taken before the roll,
		/// from the quality before the attempt, and whatever the outcome.
		/// </summary>
		public static Result Attempt(UWObject pOItem, DataImport pOData, int piRepairSkill)
		{
			Result lOResult = new Result();

			if (pOItem == null || pOData == null)
				return lOResult;

			string lsName = UWText.ObjectName(pOData, pOItem.ID);
			int liDurability = GetDurability(pOItem, pOData);

			// do_repair refuses before it takes any time.
			if (liDurability == -1)
			{
				lOResult.Outcome = OutcomeEnum.NotRepairable;
				lOResult.Message = fMessage(pOData, CannotRepairMessage);

				return lOResult;
			}

			lOResult.Minutes = System.Math.Max(RepairMinimumMinutes,
				(liDurability * 3) - piRepairSkill - (pOItem.Quality / 2));

			OutcomeEnum leOutcome = Resolve(pOItem, UWSkillCheck.Check(piRepairSkill, liDurability), piRepairSkill);

			if (leOutcome == OutcomeEnum.Destroyed
				&& !UWObjectLimitRules.Culls(pOItem, DestroyedCullingRange, pOData.CommonObjectProperties, null))
				leOutcome = OutcomeEnum.NotRepairable;

			lOResult.Outcome = leOutcome;
			lOResult.Destroyed = leOutcome == OutcomeEnum.Destroyed;
			lOResult.Message = fMessage(pOData, CannotRepairMessage + (int)leOutcome);

			if (leOutcome != OutcomeEnum.NotRepairable)
				lOResult.Message += lsName + ".";

			return lOResult;
		}

		/// <summary>
		/// do_repair's arithmetic after the skill check (event/WORLDEV.C:636-660): the gain of the
		/// result (64 for a critical success, 3 plus a fifth of the skill for a success, nothing
		/// for a plain failure), on a critical failure first the destruction roll (0 to 63 beating
		/// quality plus skill), then the loss of 4 to 11; past 63 is fully at 63, above 0 partially
		/// or damaged, else quality 0 and destroyed. Changes the item's quality. The fate of a
		/// destroyed item is the caller's (Attempt). Public for the self-check.
		/// </summary>
		public static OutcomeEnum Resolve(UWObject pOItem, UWSkillCheck.ResultEnum peCheck, int piRepairSkill)
		{
			if (pOItem == null)
				return OutcomeEnum.NotRepairable;

			int liQuality = pOItem.Quality;
			int liGain;

			switch (peCheck)
			{
				case UWSkillCheck.ResultEnum.CriticalSuccess:
					liGain = FullQuality + 1;
					break;

				case UWSkillCheck.ResultEnum.Success:
					liGain = 3 + (piRepairSkill / 5);
					break;

				case UWSkillCheck.ResultEnum.CriticalFailure:
					if (UWRandom.Next(DestructionRollRange) > liQuality + piRepairSkill)
						return OutcomeEnum.Destroyed;

					liGain = -(FailureLossBase + UWRandom.Next(FailureLossRollRange));
					break;

				default:
					return OutcomeEnum.NoEffect;
			}

			if (liQuality + liGain > FullQuality)
			{
				pOItem.Quality = FullQuality;

				return OutcomeEnum.Fully;
			}

			if (liQuality + liGain > 0)
			{
				pOItem.Quality = (ushort)(liQuality + liGain);

				return liGain > 0 ? OutcomeEnum.Partially : OutcomeEnum.Damaged;
			}

			pOItem.Quality = 0;

			return OutcomeEnum.Destroyed;
		}

		private static string fMessage(DataImport pOData, int piIndex)
		{
			return UWText.General(pOData, piIndex);
		}
	}
}
