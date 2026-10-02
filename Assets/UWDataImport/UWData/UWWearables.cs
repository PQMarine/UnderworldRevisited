using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWWearables
	{
		public enum Sex
		{
			Male,
			Female
		}

		public enum ArmorConditions
		{
			Damaged,
			Worn,
			Servicable,
			Excellent
		}

		public enum ArmorMaterials
		{
			Leather,
			Chain,
			Plate
		}

		public enum ArmorTypes
		{
			None = -1,
			Chest,
			Legs,
			Gloves,
			Boots,
			Helmet,
			SpecialBoots,
			SpecialBand1,
			Crown,
			SpecialBand2
		}

		private Dictionary<Sex, List<UWTexture>> mOArmors;

		private UWTexture mNone;

		public UWWearables(UWTextures pOTextures, UWTexture pNone)
		{
			mNone = pNone;
			mOArmors = new Dictionary<Sex, List<UWTexture>>();
			mOArmors.Add(Sex.Male, pOTextures.GetTexturesByType(UWTexture.TextureTypes.ARMOR_M));
			mOArmors.Add(Sex.Female, pOTextures.GetTexturesByType(UWTexture.TextureTypes.ARMOR_F));
		}

		public UWTexture GetArmor(int piIndex, Sex peSex)
		{
			return mOArmors[peSex][piIndex];
		}

		public UWTexture GetArmor(Sex peSex, ArmorMaterials peArmorMaterial, ArmorConditions peCondition, ArmorTypes peArmorType)
		{
			if (peArmorType == ArmorTypes.None)
			{
				return mNone;
			}
			// Only the special pieces (boots of 60, bands, crown) have a single image. Helmets and
			// boots have four conditions like the rest; with "> Gloves" they always drew the first
			// row, so a repaired leather cap kept its old look (per user, 2026-09-17).
			if (peArmorType > ArmorTypes.Helmet)
			{
				return mOArmors[peSex][fGetArmorTypeIndex(peArmorMaterial, peArmorType)];
			}
			return mOArmors[peSex][fGetConditionStartIndex(peCondition) + fGetArmorTypeIndex(peArmorMaterial, peArmorType)];
		}

		private int fGetConditionStartIndex(ArmorConditions peCondition)
		{
			return peCondition switch
			{
				ArmorConditions.Damaged => 0, 
				ArmorConditions.Worn => 15, 
				ArmorConditions.Servicable => 30, 
				ArmorConditions.Excellent => 45, 
				_ => 0, 
			};
		}

		private int fGetArmorTypeIndex(ArmorMaterials peArmorMaterial, ArmorTypes peArmorType)
		{
			int num = 0;
			switch (peArmorType)
			{
			case ArmorTypes.SpecialBoots:
				return 60;
			case ArmorTypes.SpecialBand1:
				return 61;
			case ArmorTypes.Crown:
				return 62;
			case ArmorTypes.SpecialBand2:
				return 63;
			default:
				num = (int)peArmorType * 3;
				return peArmorMaterial switch
				{
					ArmorMaterials.Leather => num + 0, 
					ArmorMaterials.Chain => num + 1, 
					ArmorMaterials.Plate => num + 2, 
					_ => num + 0, 
				};
			}
		}
	}
}
