using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.6 (2026-10-18): the weapon hand moved out of UWCharacter.Update into the
    /// engine-free UWWeaponSwing. Pinned: drawing in half a second, the wind-up of four frames at
    /// 0.125 s, the gem by the charge (and a launcher's red or green only), the blow connecting two
    /// frames before the end, the early release running back, the switch in two motions, and the
    /// events the host answers (music, noise, hit check).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckWeaponSwing(DataImport pOData)
        {
            Check.Group("Weapon hand (UWWeaponSwing - cleanup step 4.6)");

            fExpectInt("swing: an eighth charge rounds to even, 12", UWWeaponSwing.GetChargeForGem(0.125f), 12);
            fExpectInt("swing: 0.135 rounds up to 14", UWWeaponSwing.GetChargeForGem(0.135f), 14);
            fExpectInt("swing: below zero is zero", UWWeaponSwing.GetChargeForGem(-1f), 0);
            fExpectInt("swing: above full is full", UWWeaponSwing.GetChargeForGem(2f), UWCombat.FullCharge);

            if (pOData == null || pOData.WeaponAnimations == null || pOData.PowerGem == null)
                return;

            UWPowerGem lOGem = pOData.PowerGem;
            UWWeaponSwing lOSwing = new UWWeaponSwing();
            FakeWeaponHand lOHand = new FakeWeaponHand();
            List<string> lOTold = new List<string>();

            lOSwing.CombatModeEntered += () => lOTold.Add("drawn");
            lOSwing.CombatModeLeft += () => lOTold.Add("left");
            lOSwing.Struck += () => lOTold.Add("struck");
            lOSwing.AttackConnected += () => lOTold.Add("connected");

            lOSwing.Init(pOData.WeaponAnimations, lOGem, WeaponTypes.RightHandAxe);

            UWWeaponAnimation lOAxe = pOData.WeaponAnimations.GetWeaponAnimation(WeaponTypes.RightHandAxe);

            fExpectBool("swing: starts sheathed, the gem inactive, the ready frame chosen",
                lOSwing.State == UWWeaponSwing.AttackStatusEnum.Seathed && lOSwing.GemPicture == lOGem.Inactive
                && lOSwing.Frame == lOAxe.GetReadyFrame() && !lOSwing.DrawWWeapon && !lOSwing.CanAttack, true);

            lOSwing.SetWeaponType(WeaponTypes.RightHandMace);
            fExpectBool("swing: outside combat mode a switch is silent",
                lOSwing.WeaponType == WeaponTypes.RightHandMace && lOSwing.State == UWWeaponSwing.AttackStatusEnum.Seathed, true);
            lOSwing.SetWeaponType(WeaponTypes.RightHandAxe);

            lOHand.IsCombatModeActive = true;
            lOSwing.Tick(0.1f, lOHand);
            fExpectBool("swing: combat mode draws, the gem unpowered, the music told",
                lOSwing.State == UWWeaponSwing.AttackStatusEnum.Readying && lOSwing.CurrentReadyWeaponTime == 0f
                && lOSwing.GemPicture == lOGem.Unpowered && lOSwing.DrawWWeapon && string.Join(" ", lOTold) == "drawn", true);

            lOSwing.Tick(0.25f, lOHand);
            fExpectBool("swing: a quarter second draws half way", lOSwing.CurrentReadyWeaponTime == 0.5f
                && lOSwing.State == UWWeaponSwing.AttackStatusEnum.Readying, true);
            lOSwing.Tick(0.25f, lOHand);
            fExpectBool("swing: half a second and the weapon is ready", lOSwing.CurrentReadyWeaponTime == 1f
                && lOSwing.CanAttack, true);

            lOHand.IsAttacking = true;
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: the press starts the wind-up at frame 0",
                lOSwing.State == UWWeaponSwing.AttackStatusEnum.PowerUp && lOSwing.IsChargingAttack
                && lOSwing.Frame == lOAxe.GetPowerupFrame(AttackTypes.Slash, 0), true);

            int liTicks = 0;

            while (lOSwing.State == UWWeaponSwing.AttackStatusEnum.PowerUp && liTicks < 100)
            {
                lOSwing.Tick(0.13f, lOHand);
                liTicks++;
            }

            fExpectInt("swing: four wind-up frames at 0.125 s, then complete", liTicks, 5);
            fExpectBool("swing: the wind-up is complete", lOSwing.IsWindupComplete, true);

            lOHand.AttackCharge = 0.5f;
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: half a charge is the gem's frame for 50",
                lOSwing.GemPicture == lOGem.Powering[UWPowerGem.GetFrame(50) - 2], true);

            lOHand.AttackCharge = 1f;
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: a full charge starts the green cycle", lOSwing.GemPicture == lOGem.FullyPowered[0], true);
            lOSwing.Tick(0.2f, lOHand);
            fExpectBool("swing: five green pictures a second", lOSwing.GemPicture == lOGem.FullyPowered[1], true);

            lOTold.Clear();
            lOSwing.ReleaseAttack();
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: the release strikes (the noise)", lOSwing.State == UWWeaponSwing.AttackStatusEnum.Release
                && string.Join(" ", lOTold) == "struck", true);

            int liConnectAt = -1;
            int liFrame = 0;

            lOSwing.AttackConnected += () => liConnectAt = liFrame;

            for (liFrame = 1; liFrame <= 6; liFrame++)
                lOSwing.Tick(0.13f, lOHand);

            int liLength = lOAxe.GetReleaseFrameCount(AttackTypes.Slash);

            fExpectInt("swing: the blow connects two frames before the end of the motion", liConnectAt,
                System.Math.Max(1, liLength - UWWeaponSwing.DefaultFramesFromEnd));
            fExpectInt("swing: and only once", fCount(lOTold, "connected"), 1);

            lOSwing.Tick(0.13f, lOHand);
            fExpectBool("swing: after the motion ready again, the gem unpowered",
                lOSwing.CanAttack && lOSwing.GemPicture == lOGem.Unpowered, true);

            lOTold.Clear();
            lOSwing.Tick(0.01f, lOHand);
            lOSwing.Tick(0.13f, lOHand);
            lOSwing.ReleaseAttack();
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: released during the wind-up it runs back", lOSwing.State == UWWeaponSwing.AttackStatusEnum.PowerDown, true);

            lOHand.IsAttacking = false;

            for (int liStep = 0; liStep < 3; liStep++)
                lOSwing.Tick(0.13f, lOHand);

            fExpectBool("swing: back at ready, nothing struck, nothing connected",
                lOSwing.CanAttack && lOTold.Count == 0, true);

            lOSwing.SetWeaponType(WeaponTypes.RightHandSword);
            fExpectBool("swing: in combat a switch first sheathes the old weapon",
                lOSwing.State == UWWeaponSwing.AttackStatusEnum.Seathing && lOSwing.WeaponType == WeaponTypes.RightHandAxe
                && lOSwing.CurrentReadyWeaponTime == 1f, true);
            lOSwing.Tick(0.5f, lOHand);
            fExpectBool("swing: then draws the new one", lOSwing.State == UWWeaponSwing.AttackStatusEnum.Readying
                && lOSwing.WeaponType == WeaponTypes.RightHandSword, true);
            lOSwing.Tick(0.5f, lOHand);

            lOSwing.HasRangedWeapon = true;
            fExpectBool("swing: a launcher is not held up into view", lOSwing.DrawWWeapon, false);

            lOHand.IsAttacking = true;

            for (int liStep = 0; liStep < 6; liStep++)
                lOSwing.Tick(0.13f, lOHand);

            lOHand.AttackCharge = 0.5f;
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: a launcher's gem stays red before the crosshair", lOSwing.GemPicture == lOGem.Unpowered, true);
            lOHand.IsRangedTargeting = true;
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: and jumps to green with it (frame 9, the first green picture)",
                UWPowerGem.RangedFrame - 2 >= lOGem.Powering.Count && lOSwing.GemPicture == lOGem.FullyPowered[0], true);

            lOTold.Clear();
            lOHand.IsCombatModeActive = false;
            lOSwing.Tick(0.01f, lOHand);
            fExpectBool("swing: leaving combat mode sheathes, the gem inactive, the music told",
                lOSwing.State == UWWeaponSwing.AttackStatusEnum.Seathing && lOSwing.GemPicture == lOGem.Inactive
                && string.Join(" ", lOTold) == "left", true);
        }

        private static int fCount(List<string> pOList, string psItem)
        {
            int liCount = 0;

            foreach (string lsItem in pOList)
                if (lsItem == psItem)
                    liCount++;

            return liCount;
        }
    }
}
