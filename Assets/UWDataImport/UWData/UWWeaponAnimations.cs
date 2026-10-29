using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UWDataImport.UWData
{
	public class UWWeaponAnimations
	{
		private readonly List<UWTexture> mOWeaponImages;

		private List<UWWeaponAnimation> mWeapons;

		private List<WeaponCoordinate> mFrameCoordinates;

		public UWTexture Black => mOWeaponImages[3];

		/// <summary>
		/// One record of the second game's WEAP.DAT (0x61 bytes): a picture count, the frame totals of
		/// the three swings, then 31 steps, 31 x and 31 y (Underworld Exhumed uw2 ui/PANELS.C:564,
		/// do_weapload; docs/FORMATS.md "Smaller files"). Its pictures are WEAP.GR's from record x 0x1F
		/// on. What the step bytes mean (a picture in the low five bits, the top three set too) is read
		/// when the swing is built (phase 4); here they are held as stored.
		/// </summary>
		public sealed class Uw2Record
		{
			public int PictureCount;

			public byte[] FrameTotals = new byte[Uw2SwingCount];

			public byte[] Steps = new byte[Uw2StepCount];

			public byte[] X = new byte[Uw2StepCount];

			public byte[] Y = new byte[Uw2StepCount];

			public List<UWTexture> Pictures = new List<UWTexture>();
		}

		public const int Uw2RecordSize = 0x61;

		public const int Uw2StepCount = 0x1F;

		public const int Uw2SwingCount = 3;

		/// <summary>Records by (1 - lefty) x 4 + weapon kind (0 to 3), as the first game's.</summary>
		public const int RecordCount = 8;

		public const string Uw2DataFile = "WEAP.DAT";

		/// <summary>The second game's eight records (null for the first game's data).</summary>
		public IReadOnlyList<Uw2Record> Uw2Records { get; }

		/// <summary>The weapon pictures of either game (UW2 plan phase 2 step 1, 2026-10-29): the first
		/// game's WEAPONS.DAT and its 28 pictures per record as until then, the second game's WEAP.DAT in
		/// Uw2Records - its records do not fit the first game's swing layout, so GetWeaponAnimation has
		/// none for it (null).</summary>
		public UWWeaponAnimations(string psDataPath, List<UWTexture> pOWeaponImages, UWGame peGame)
		{
			mOWeaponImages = pOWeaponImages;

			if (peGame != UWGame.Uw2)
			{
				fLoadUw1(psDataPath);

				return;
			}

			mWeapons = new List<UWWeaponAnimation>();

			byte[] lyData = File.ReadAllBytes(Path.Combine(psDataPath, Uw2DataFile));
			List<Uw2Record> lORecords = new List<Uw2Record>();

			for (int liRecord = 0; liRecord < RecordCount; liRecord++)
			{
				int liBase = liRecord * Uw2RecordSize;
				Uw2Record lORecord = new Uw2Record { PictureCount = lyData[liBase] };

				System.Array.Copy(lyData, liBase + 1, lORecord.FrameTotals, 0, Uw2SwingCount);
				System.Array.Copy(lyData, liBase + 1 + Uw2SwingCount, lORecord.Steps, 0, Uw2StepCount);
				System.Array.Copy(lyData, liBase + 1 + Uw2SwingCount + Uw2StepCount, lORecord.X, 0, Uw2StepCount);
				System.Array.Copy(lyData, liBase + 1 + Uw2SwingCount + (2 * Uw2StepCount), lORecord.Y, 0, Uw2StepCount);

				int liStart = liRecord * Uw2StepCount;

				for (int liPicture = 0; liPicture < lORecord.PictureCount && liStart + liPicture < mOWeaponImages.Count; liPicture++)
					lORecord.Pictures.Add(mOWeaponImages[liStart + liPicture]);

				lORecords.Add(lORecord);
			}

			Uw2Records = lORecords;
		}

		public UWWeaponAnimations(string psDataPath, List<UWTexture> pOWeaponImages)
		{
			mOWeaponImages = pOWeaponImages;
			fLoadUw1(psDataPath);
		}

		private void fLoadUw1(string psDataPath)
		{
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
			return Uw2Records != null ? null : mWeapons.First((UWWeaponAnimation w) => w.WeaponType == peWeapon);
		}
	}
}
