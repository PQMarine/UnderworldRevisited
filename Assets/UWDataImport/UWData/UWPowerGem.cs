using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWPowerGem
	{
		/// <summary>
		/// WHICH FRAME THE GEM SHOWS WHILE A BLOW IS BEING CHARGED: one plus the charge
		/// divided by twelve, over the charge's full range of 0 to 100
		/// (seg022_230E_133B to 1367, 83027-83063; the charge itself grows there by the
		/// weapon table's byte 4 per 0x10 PIT ticks and is capped at 100). Frame 1 is the
		/// unpowered red, 2 to 8 are the seven yellow levels, 9 is the first green one.
		/// </summary>
		public const int ChargeStep = 12;

		/// <summary>
		/// A RANGED WEAPON NEVER CHARGES THE GEM. The user saw the sling jump from red
		/// straight to green, without yellow, exactly when the targeting cursor appears
		/// (2026-09-10), and the reason sat unread until 2026-09-22.
		///
		/// The branch is at seg022_230E_1276 (82909): when the drag reaches the attack zone
		/// and the weapon is a ranged one, the whole charge path is skipped and label 1294
		/// sets the gem to frame 9 in one go, switches the cursor to the crosshair 0x1075 and
		/// clears the swing type. The seven yellow frames are simply never used with a bow, a
		/// crossbow or a sling.
		///
		/// THE CHARGE IS NOT ONLY MISSING FROM THE PICTURE - it does not exist for a shot at
		/// all, which is why the damage of a missile counts with the neutral 0x80 however long
		/// the button was held (see UWPlayerAttack.GetMissileDamage).
		///
		/// The out-of-bounds reading that looked like the explanation at first is real but has
		/// nothing to do with it: the ranged table has rows of three bytes, so byte 4 of a row
		/// is the second byte of the NEXT row, and for the last row it falls into the melee
		/// table right behind it. Nobody ever reads it, because the loop is not entered.
		/// </summary>
		public const int RangedFrame = 9;

		/// <summary>The gem while a melee blow is charged, 1 to 9.</summary>
		public static int GetFrame(int piCharge)
		{
			if (piCharge < 0)
				piCharge = 0;

			return 1 + (piCharge / ChargeStep);
		}

		/// <summary>The gem with a ranged weapon: red until the crosshair is up, green from
		/// then on - see RangedFrame.</summary>
		public static int GetRangedFrame(bool pbTargeting)
		{
			return pbTargeting ? RangedFrame : 1;
		}

		public UWTexture Inactive { get; set; }

		public UWTexture Unpowered { get; set; }

		public List<UWTexture> Powering { get; set; }

		public List<UWTexture> FullyPowered { get; set; }

		public UWPowerGem(UWTextures pTextures)
		{
			List<UWTexture> texturesByType = pTextures.GetTexturesByType(UWTexture.TextureTypes.POWER);
			Inactive = texturesByType[0];
			Unpowered = texturesByType[1];
			Powering = texturesByType.GetRange(2, 7);
			FullyPowered = texturesByType.GetRange(9, 5);
		}
	}
}
