using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UWDataImport.UWData
{
	public class UWWeaponAnimations
	{
		private readonly List<UWTexture> mOWeaponImages;

		private readonly List<UWWeaponAnimation> mWeapons;

		private List<WeaponCoordinate> mFrameCoordinates;

		public UWTexture Black => mOWeaponImages[3];

		public UWWeaponAnimations(string psDataPath, List<UWTexture> pOWeaponImages)
		{
			mOWeaponImages = pOWeaponImages;
			fLoadWeaponCoordinates(psDataPath);
			mWeapons = new List<UWWeaponAnimation>();
			int i;
			for (i = 0; i < 8; i++)
			{
				mWeapons.Add(new UWWeaponAnimation((WeaponTypes)i, mOWeaponImages.Where((UWTexture w) => w.Index >= i * 28 && w.Index < i * 28 + 28).ToList(), mFrameCoordinates.Where((WeaponCoordinate w) => w.Index >= i * 28 && w.Index < i * 28 + 28).ToList()));
			}
		}

		private void fLoadWeaponCoordinates(string psDataPath)
		{
			mFrameCoordinates = new List<WeaponCoordinate>();
			byte[] array = File.ReadAllBytes(Path.Combine(psDataPath, "WEAPONS.DAT"));
			int num = 0;
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 28; j++)
				{
					if (i != 7 || j == 27)
					{
					}
					mFrameCoordinates.Add(new WeaponCoordinate
					{
						Index = num,
						X = array[i * 56 + j],
						Y = array[i * 56 + j + 28]
					});
					num++;
				}
			}
		}

		public UWWeaponAnimation GetWeaponAnimation(WeaponTypes peWeapon)
		{
			return mWeapons.First((UWWeaponAnimation w) => w.WeaponType == peWeapon);
		}
	}
}
