using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWWeaponAnimation
	{
		private Dictionary<AttackTypes, List<UWTexture>> mPowerUpFrames;

		private Dictionary<AttackTypes, List<UWTexture>> mReleaseFrames;

		private UWTexture mReadyFrame;

		private Dictionary<AttackTypes, List<WeaponCoordinate>> mPowerUpCoords;

		private Dictionary<AttackTypes, List<WeaponCoordinate>> mReleaseCoords;

		private WeaponCoordinate mReadyCoord;

		public WeaponTypes WeaponType { get; set; }

		public UWWeaponAnimation(WeaponTypes pWeaponType, List<UWTexture> pWeaponImages, List<WeaponCoordinate> pCoordinates)
		{
			WeaponType = pWeaponType;
			mPowerUpFrames = new Dictionary<AttackTypes, List<UWTexture>>();
			mReleaseFrames = new Dictionary<AttackTypes, List<UWTexture>>();
			mPowerUpCoords = new Dictionary<AttackTypes, List<WeaponCoordinate>>();
			mReleaseCoords = new Dictionary<AttackTypes, List<WeaponCoordinate>>();
			for (int i = 0; i < 3; i++)
			{
				int index = i * 9;
				mPowerUpFrames.Add((AttackTypes)i, pWeaponImages.GetRange(index, 4));
				mPowerUpCoords.Add((AttackTypes)i, pCoordinates.GetRange(index, 4));
				index = i * 9 + 4;
				mReleaseFrames.Add((AttackTypes)i, pWeaponImages.GetRange(index, 5));
				mReleaseCoords.Add((AttackTypes)i, pCoordinates.GetRange(index, 5));
			}
			mReadyFrame = pWeaponImages[27];
			mReadyCoord = pCoordinates[27];
		}

		public UWTexture GetReadyFrame()
		{
			return mReadyFrame;
		}

		public UWTexture GetPowerupFrame(AttackTypes pAttackType, int pFrame)
		{
			if (pFrame > 3)
			{
				pFrame = 3;
			}
			return mPowerUpFrames[pAttackType][pFrame];
		}

		public UWTexture GetReleaseFrame(AttackTypes pAttackType, int pFrame)
		{
			if (pFrame > 4)
			{
				pFrame = 4;
			}
			return mReleaseFrames[pAttackType][pFrame];
		}

		public WeaponCoordinate GetReadyCoord()
		{
			return mReadyCoord;
		}

		public WeaponCoordinate GetPowerupCoord(AttackTypes pAttackType, int pFrame)
		{
			if (pFrame > 3)
			{
				pFrame = 3;
			}
			return mPowerUpCoords[pAttackType][pFrame];
		}

		/// <summary>
		/// How many frames the strike animation of this attack type really has. The table
		/// lists five slots for every type, but not all are filled: for the axe chop
		/// only zero coordinates remain from the fifth slot on, for the thrust on the other hand
		/// there are real values up to the end. A zero pair therefore marks the end.
		///
		/// This is needed for the moment a strike lands (see
		/// UWCharacter.AttackConnected) - that counts from the END of the animation, which lies
		/// somewhere else depending on the attack type.
		/// </summary>
		public int GetReleaseFrameCount(AttackTypes pAttackType)
		{
			if (!mReleaseCoords.ContainsKey(pAttackType))
				return 1;

			List<WeaponCoordinate> lOCoords = mReleaseCoords[pAttackType];

			for (int liFrame = 0; liFrame < lOCoords.Count; liFrame++)
			{
				if (lOCoords[liFrame].X == 0 && lOCoords[liFrame].Y == 0)
					return liFrame < 1 ? 1 : liFrame;
			}

			return lOCoords.Count;
		}

		public WeaponCoordinate GetReleaseCoord(AttackTypes pAttackType, int pFrame)
		{
			if (pFrame > 4)
			{
				pFrame = 4;
			}
			return mReleaseCoords[pAttackType][pFrame];
		}

		public override string ToString()
		{
			return WeaponType.ToString();
		}
	}
}
