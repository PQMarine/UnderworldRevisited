using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE LIGHT SPHERES' RECHARGE ON TALORUS (per user 2026-11-22, SAVE2 before the switch: no flash came). Level 33,
        /// the use trigger aiming at 58/21 holds hack 35 (recharge_lightbulbs, uw2 WORLDEV.C:1588): every light sphere on
        /// that square gets quality 0x3F and each thing there that is or holds one gets the lightning 0x1CC
        /// (put_effect(obj, 0xC, 4, 0, 0, tx, ty)). A spent sphere is put down there for the check.
        /// </summary>
        private static void fCheckUw2LightSpheres(CheckContext pOContext)
        {
            Check.Group("The light spheres' recharge on Talorus (UW2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("light spheres", "no second game here: skipped");

                return;
            }

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            UWGameSession lOBefore = UWGameSession.Current;

            UWGameSession.MakeCurrent(lOImport.Session);

            try
            {
                UWLevel lOLevel = lOImport.Levels[32];
                UWObject lOTrigger = lOLevel.Masterlist.Find(o => o != null && o.ID == 0x1A2 && o.Quality == 58 && o.Owner == 21);
                UWObject lOSphere = lOLevel.GetTile(57, 22).ObjectsInTile.Find(o => o != null && o.ID == UWTrapRulesUw2.LightSphereId);

                if (lOTrigger == null || lOSphere == null)
                {
                    Check.Fail("light spheres", "no trigger aiming at 58/21 or no sphere on 57/22 of level 33");

                    return;
                }

                // The sphere spent and carried over to the recharge square.
                lOLevel.GetTile(57, 22).ObjectsInTile.Remove(lOSphere);
                lOLevel.GetTile(58, 21).ObjectsInTile.Add(lOSphere);
                lOSphere.TileX = 58;
                lOSphere.TileY = 21;
                lOSphere.Quality = 3;

                FakeTrapHost lOHost = new FakeTrapHost(lOImport, 32);

                UWTrapRulesUw2.UseTrigger(null, lOTrigger, UWTrapRulesUw2.UseMode, lOHost);

                string lsLog = string.Join(", ", lOHost.Fake.Log);

                Check.Bool("light spheres: the switch's hack 35 recharges the sphere on 58/21 and flashes the lightning over it ("
                    + lsLog + "; quality " + lOSphere.Quality + ")",
                    lOSphere.Quality == 0x3F && lsLog.Contains("effect " + UWTrapRulesUw2.RechargeEffectId + " at "), true);
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }
    }
}
