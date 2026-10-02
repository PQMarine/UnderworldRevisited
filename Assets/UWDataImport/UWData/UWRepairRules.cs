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

		/// <summary>"You cannot repair the ..." - what the anvil says to an item without durability.</summary>
		public const int CannotRepairMessage = 143;

		private const int DestroyedItemMessage = 141;

		private const int DamagedItemMessage = 142;

		private const int NoEffectMessage = 144;

		private const int PartiallyRepairedMessage = 145;

		private const int FullyRepairedMessage = 146;

		/// <summary>Condition 63 is as good as new.</summary>
		private const int FullQuality = UWObjectMechanics.MaxQuality;

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

			/// <summary>How many minutes the game clock advances.</summary>
			public int Minutes;
		}

		/// <summary>
		/// The durability of an item, read as signed - from 0x80 it means
		/// "not possible". Depending on the kind it is in a different table: melee weapons,
		/// ranged weapons and clothing each carry it themselves.
		/// </summary>
		public static int GetDurability(UWObject pOItem, DataImport pOData)
		{
			if (pOItem == null || pOData == null || pOData.ObjectClassProperties == null)
				return -1;

			UWObjectClassProperties lOProperties = pOData.ObjectClassProperties;

			UWObjectClassProperties.MeleeWeapon lOMelee;

			if (lOProperties.TryGetMeleeWeapon(pOItem.ID, out lOMelee))
				return (sbyte)lOMelee.Durability;

			UWObjectClassProperties.RangedWeapon lORanged;

			if (lOProperties.TryGetRangedWeapon(pOItem.ID, out lORanged))
				return (sbyte)lORanged.Durability;

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
				+ UWItemDescriptions.GetBareName(pOItem.ID, pOData)
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
		///   critical failure    damage, often total loss
		///
		/// THE TOTAL LOSS DEPENDS ON AN ODD ROLL: zero to 63 against condition plus
		/// skill. The BETTER you are, the more likely the item breaks completely - that is
		/// how it is in the reference, and it looks like a swapped comparison in the
		/// original. Adopted as found.
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

			int liQuality = pOItem.Quality;
			int liDurability = GetDurability(pOItem, pOData);

			lOResult.Minutes = System.Math.Max(RepairMinimumMinutes,
				(liDurability * 3) - piRepairSkill - (liQuality / 2));

			string lsName = UWItemDescriptions.GetBareName(pOItem.ID, pOData);

			switch (UWSkillCheck.Check(piRepairSkill, liDurability))
			{
				case UWSkillCheck.ResultEnum.CriticalSuccess:
					pOItem.Quality = FullQuality;
					lOResult.Message = fMessage(pOData, FullyRepairedMessage) + lsName;
					break;

				case UWSkillCheck.ResultEnum.Success:
					pOItem.Quality = (ushort)System.Math.Min(FullQuality, liQuality + 3 + (piRepairSkill / 5));

					lOResult.Message = fMessage(pOData, pOItem.Quality == FullQuality
						? FullyRepairedMessage : PartiallyRepairedMessage) + lsName;
					break;

				case UWSkillCheck.ResultEnum.CriticalFailure:
					fDamageOnRepair(pOItem, liQuality, piRepairSkill, lsName, pOData, ref lOResult);
					break;

				default:
					lOResult.Message = fMessage(pOData, NoEffectMessage) + lsName;
					break;
			}

			return lOResult;
		}

		/// <summary>A destroyed item leaves the inventory as a whole - the caller does that via
		/// RemoveItem, which also finds it inside closed containers (TryConsumeOne counted
		/// enchanted armour down instead of removing it, per user 2026-09-17).</summary>
		private static void fDamageOnRepair(UWObject pOItem, int piQuality, int piSkill, string psName,
			DataImport pOData, ref Result pOResult)
		{
			if (UWRandom.Next(64) <= piQuality + piSkill)
			{
				pOItem.Quality = 0;

				pOResult.Message = fMessage(pOData, DestroyedItemMessage) + psName;
				pOResult.Destroyed = true;

				return;
			}

			int liNew = piQuality - (4 + UWRandom.Next(7));

			pOItem.Quality = (ushort)System.Math.Max(0, liNew);

			if (pOItem.Quality == 0)
			{
				pOResult.Message = fMessage(pOData, DestroyedItemMessage) + psName;
				pOResult.Destroyed = true;

				return;
			}

			pOResult.Message = fMessage(pOData, DamagedItemMessage) + psName;
		}

		private static string fMessage(DataImport pOData, int piIndex)
		{
			try
			{
				string lsMessage = pOData.GetGeneralMessage(piIndex);

				return string.IsNullOrEmpty(lsMessage) ? string.Empty : lsMessage.TrimEnd('\r', '\n');
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
