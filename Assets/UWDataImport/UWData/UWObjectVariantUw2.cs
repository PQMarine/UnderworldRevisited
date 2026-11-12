using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S ITEM STEPS: the valuable a critter carries, from Underworld Exhumed's uw2
	/// obj/TREASURE.C (a conversation rolls the partner's inventory before its script runs, per user
	/// 2026-10-31: the refused roll left the trade without goods). The light and the lock come with the
	/// game's rules in phase 4 of Docs/UW2-PLAN.md.
	/// </summary>
	public sealed class UWObjectVariantUw2 : IUWObjectVariant
	{
		private const int LevelsPerWorld = 8;

		private const int WorldStep = 3;

		private const int FirstWorldSpan = 37;

		private const int FirstWorldShift = 30;

		/// <summary>A roll of this type becomes the first: the coin, never the gold coin.</summary>
		private const int RolledAway = 1;

		/// <summary>Lit, a light sounds (UseLight, USEITEMS.C:664).</summary>
		public const int LightSound = 0x20;

		public bool LightUsedUp(int piQuality)
		{
			return piQuality <= 1;
		}

		private const int FirstLight = 0x90;

		private const int FirstBlackrockGem = 0x118;

		private const int ClassMask = 0x1F8;

		/// <summary>Our number of game_sprint 0x164, "cool "; the next is "warm ".</summary>
		private const int CoolWord = 0x164 + 1;

		public int LookQuality(int piItemId, int piQuality)
		{
			return piQuality == 1 && (piItemId & ClassMask) == FirstLight ? 0 : piQuality;
		}

		public int LastOwnerRace => 0x1E;

		/// <summary>do_mods: warm or cool by the owner field.</summary>
		public int NameWordString(UWObject pOItem)
		{
			if ((pOItem.ID & ClassMask) != FirstBlackrockGem)
				return -1;

			return CoolWord + (pOItem.Owner > 0 ? 1 : 0);
		}

		public int NothingToSeeString => 0xA6 + 2 + 1;

		public int DoorSpikedString => 0x91 + 1;

		public int BonesLeadString => 0x1A + 1;

		public int BurntOutQuality => 1;

		public int LightUsedUpString => 0x8A + 1;

		public int HandsFullString => 0x105 + 1;

		/// <summary>rand() % (37 - world * 3) - (30 - world * 3), at least 0 and never 1; the world is the
		/// level's (TREASURE.C:103-107).</summary>
		public int RollValuableType(int piDungeonLevel)
		{
			int liWorld = ((piDungeonLevel - 1) / LevelsPerWorld) * WorldStep;
			int liType = UWRandom.Next(0, FirstWorldSpan - liWorld) - (FirstWorldShift - liWorld);

			return liType < 0 || liType == RolledAway ? 0 : liType;
		}

		/// <summary>checkLock's roll (uw2 obj/OBJUSE.C): skill_check(skill, z * 3), as the first game's. The lockpick's
		/// "+ 1" is its caller's (USEITEMS.C passes -(Picklock + 1)); Open passes 45 (part D2, 2026-11-12: until then this
		/// threw and the spell vanished without a word, per user on test list 30.5).</summary>
		public UWSkillCheck.ResultEnum PickRoll(int piPicklock, int piLockHeight)
		{
			return UWSkillCheck.Check(piPicklock, piLockHeight * 3);
		}
	}
}
