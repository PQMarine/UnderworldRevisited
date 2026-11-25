using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S 3D MODELS (Docs/UW2-PLAN.md phase 1 step 5, 2026-10-29): UW3DModelImport reads
    /// UW2.EXE at the second game's layout (UW3DModelLayout.Uw2). Every model the table names runs
    /// through the interpreter without an unknown opcode and without being cut off (the second game's
    /// one new opcode, 0xD2 do_compact_map, on model 0x0A); the models both games have keep the first
    /// game's shape but for four (the arrow 0x08, 0x0D, the table 0x18, the chair 0x1C; counted index by
    /// index);
    /// the object-to-model table after the colour records names only models with geometry; the
    /// first game's import is unchanged (the Models group pins it).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2Models(CheckContext pOContext)
        {
            Check.Group("The second game's 3D models from UW2.EXE (UW2 plan, phase 1 step 5)");

            string lsUw2Exe = pOContext.Uw2DataPath == null ? null : DataPath.FindExe(pOContext.Uw2DataPath, UWGameProfiles.Uw2);
            string lsUw1Exe = pOContext.DataPath == null ? null : DataPath.FindExe(pOContext.DataPath);

            if (lsUw2Exe == null)
            {
                Check.Pass("uw2 models", "no second game here: skipped");

                return;
            }

            UW3DModelImport lOUw2 = new UW3DModelImport(lsUw2Exe, UWGameProfiles.Uw2.Models);
            UW3DModelImport lOUw1 = lsUw1Exe != null ? new UW3DModelImport(lsUw1Exe, UWGameProfiles.Uw1.Models) : null;
            int liModels = 0;
            int liFaces = 0;
            List<string> lsTrouble = new List<string>();
            List<string> lsChanged = new List<string>();
            HashSet<int> lOWithGeometry = new HashSet<int>();

            for (int liAt = 0; liAt < UW3DModelImport.Count; liAt++)
            {
                UW3DModel lOModel = lOUw2.GetModel(liAt);

                // Every model the table names, with faces or without (model 0x0A had none while 0xD2 was unknown).
                if (lOModel != null && lOModel.UnknownNodeIds.Count > 0)
                    lsTrouble.Add("0x" + liAt.ToString("X2") + " unknown " + string.Join(",", lOModel.UnknownNodeIds));

                if (lOModel == null || lOModel.Faces == null || lOModel.Faces.Count == 0)
                    continue;

                liModels++;
                liFaces += lOModel.Faces.Count;
                lOWithGeometry.Add(liAt);

                UW3DModel lOFirst = lOUw1 != null ? lOUw1.GetModel(liAt) : null;

                if (lOFirst != null && lOFirst.Faces != null && lOFirst.Faces.Count > 0
                    && (lOFirst.Faces.Count != lOModel.Faces.Count || lOFirst.Vertices.Count != lOModel.Vertices.Count))
                    lsChanged.Add("0x" + liAt.ToString("X2"));
            }

            Check.Str("uw2 models: models with geometry and their faces (as read)", liModels + " " + liFaces, "30 583");
            Check.Str("uw2 models: ... every one without an unknown opcode", string.Join("; ", lsTrouble), string.Empty);
            Check.Str("uw2 models: the models both games have that differ in vertices or faces (as read)", string.Join(" ", lsChanged), "0x08 0x0D 0x18 0x1C");
            Check.Bool("uw2 models: the door frame's first colour from the second game's record", lOUw2.GetPrimaryPaletteIndex(UW3DModelImport.ModelIndex.DoorFrame) != 0, true);
            // The door leaf's narrow sides (UWObjectSpawner.fSpawnDoor; UnderworldExhumed DRAWOBJ.C rect_cols[0xE]).
            Check.Str("uw2 models: the door leaf's side colour, first and second game", (lOUw1 != null ? lOUw1.GetPrimaryPaletteIndex(UW3DModelImport.ModelIndex.DoorLeaf).ToString("X2") : "--") + " " + lOUw2.GetPrimaryPaletteIndex(UW3DModelImport.ModelIndex.DoorLeaf).ToString("X2"), "EC 8F");

            // The model of each 3D object type, items 0x150 to 0x16F: a model with geometry or none.
            byte[] lyExe = File.ReadAllBytes(lsUw2Exe);
            List<string> lsNoGeometry = new List<string>();
            int liTyped = 0;

            for (int liType = 0; liType < 32; liType++)
            {
                int liModel = (short)UWBinary.ReadU16(lyExe, UW3DModelLayout.Uw2ObjectModelsStart + (2 * liType));

                if (liModel < 0)
                    continue;

                liTyped++;

                if (!lOWithGeometry.Contains(liModel))
                    lsNoGeometry.Add("0x" + (0x150 + liType).ToString("X3") + ":" + liModel);
            }

            Check.Str("uw2 models: the 3D object types with a model (as read), each model with geometry", liTyped + " " + string.Join(" ", lsNoGeometry), "26 ");

            // The importer's lookup of that table (UW3DModelImport.GetObjectModel, 2026-11-16): the bed, the large blackrock
            // gem and the shelf of the second game; the first game's table names the shrine and the bench as its mapping does.
            Check.Str("uw2 models: GetObjectModel - bed, large blackrock gem, shelf; none past the table or below it",
                lOUw2.GetObjectModel(0x167) + " " + lOUw2.GetObjectModel(0x168) + " " + lOUw2.GetObjectModel(0x169) + " "
                + lOUw2.GetObjectModel(0x16F) + " " + lOUw2.GetObjectModel(0x170) + " " + lOUw2.GetObjectModel(0x14F),
                "29 30 31 -1 -1 -1");

            // THE LARGE BLACKROCK GEM (UWBlackrockGemLook, do_rect model 0x1E): the registers its faces are painted from,
            // and the colours: facets 0 and 3 used, facet 5 shown, the rest dark; the body pulsing; all used blinking.
            SortedSet<int> lOGemRegisters = new SortedSet<int>();

            foreach (UW3DModelFace lOFace in lOUw2.GetModel(UWBlackrockGemLook.Model).Faces)
                if (lOFace.Uvs == null)
                    lOGemRegisters.Add(lOFace.ColorRegister);

            List<string> lsFacets = new List<string>();

            foreach (int liRegister in new[] { 0, 1, 2, 0xC, 0xD, 0xE, 0xF, 0x10 })
                lsFacets.Add(UWBlackrockGemLook.Colour(liRegister, 0x09, 5, 0).ToString("X2"));

            Check.Str("uw2 models: the gem's face registers (as read); facets 0 and 3 used, 5 shown; the body over 512 ticks; all used",
                string.Join(",", lOGemRegisters) + " | " + string.Join(" ", lsFacets) + " | "
                + UWBlackrockGemLook.Colour(3, 0x09, 5, 0).ToString("X2") + UWBlackrockGemLook.Colour(3, 0x09, 5, 0xC0).ToString("X2")
                + UWBlackrockGemLook.Colour(3, 0x09, 5, 0x100).ToString("X2") + UWBlackrockGemLook.Colour(3, 0x09, 5, 0x1C0).ToString("X2")
                + " | " + UWBlackrockGemLook.Colour(3, 0xFF, 5, 0).ToString("X2") + UWBlackrockGemLook.Colour(0, 0xFF, 5, 0).ToString("X2")
                + UWBlackrockGemLook.Colour(0, 0xFF, 5, 0x80).ToString("X2") + UWBlackrockGemLook.Colour(4, 0x09, 5, 0),
                "0,1,2,3,12,13,14,15,16 | 4D 52 52 4D 52 4F 52 52 | 53565653 | 504C4F-1");

            // THE MOONGATE (UWMoongateLook, do_rect's flag 0x80, per user 2026-11-22): its faces (record colour, Gouraud, the
            // corner shades as read) in both games, and the colour of the Void's yellow gate (link 0x237) over 512 ticks of the
            // clock, a face with record colour 2, and the volcano gate (link 0x2C0).
            foreach (UW3DModelImport lOGame in new[] { lOUw2, lOUw1 })
            {
                if (lOGame == null)
                    continue;

                UW3DModel lOGate = lOGame.GetModel(UWMoongateLook.Model);
                List<string> lsGateFaces = new List<string>();

                foreach (UW3DModelFace lOFace in lOGate.Faces)
                    lsGateFaces.Add(lOFace.ColorIndex + (lOFace.IsGouraud ? "g" : "f"));

                List<string> lsShades = new List<string>();

                foreach (KeyValuePair<int, byte> lOShade in lOGate.VertexDarkValues)
                    lsShades.Add(lOShade.Value.ToString());

                Check.Str("models: the moongate's faces and corner shades as read (" + (lOGame == lOUw2 ? "uw2" : "uw1") + ")",
                    string.Join(",", lsGateFaces) + " | " + string.Join("", lsShades), "0g,0g,0g,0g | 555522220");
            }

            // THE FACES SEEN FROM ONE SIDE (UW3DModelLayering.FaceFronts, per user 2026-11-22: the inner sides of the Void's
            // bridges are see-through in the original): the bridge's deck from above, its underside from below, its two side
            // walls from outside, in both games; and how many of the chest's faces are one-sided.
            foreach (UW3DModelImport lOGame in new[] { lOUw2, lOUw1 })
            {
                if (lOGame == null)
                    continue;

                List<string> lsFronts = new List<string>();

                foreach (UWVector3? lOFront in UW3DModelLayering.FaceFronts(lOGame.GetModel(UW3DModelImport.ModelIndex.Bridge)))
                    lsFronts.Add(lOFront.HasValue
                        ? string.Format("{0:0},{1:0},{2:0}", lOFront.Value.X, lOFront.Value.Y, lOFront.Value.Z) : "both");

                Check.Str("models: the bridge's faces seen from one side, X,Y,height (" + (lOGame == lOUw2 ? "uw2" : "uw1") + ")",
                    string.Join(" ", lsFronts), "0,0,1 0,0,-1 1,0,0 -1,0,0");
            }

            List<string> lsGateColours = new List<string>();

            for (int liTick = 0; liTick < 0x200; liTick += 0x40)
                lsGateColours.Add(UWMoongateLook.Colour(0x237, 0, liTick).ToString("X2"));

            Check.Str("models: the moongate's colour over the clock (link 0x237), record colour 2, the volcano gate",
                string.Join(" ", lsGateColours) + " | " + UWMoongateLook.Colour(0x237, 2, 0x40).ToString("X2") + " | "
                + UWMoongateLook.Colour(0x2C0, 0, 0).ToString("X2"),
                "37 38 39 3A 3A 39 38 37 | 3A | C0");

            if (lOUw1 != null)
                Check.Str("uw2 models: the first game's table through GetObjectModel - shrine, bench",
                    lOUw1.GetObjectModel(0x157) + " " + lOUw1.GetObjectModel(0x150),
                    UW3DModelImport.ModelIndex.Shrine + " " + UW3DModelImport.ModelIndex.Bench);

            // The original paints a back-face test's faces in program order (UW3DModelLayering, per user 2026-11-18, the
            // chest's bands): only a face covering an earlier one moves (per user 2026-11-22: cracks in the boulders and the
            // shrine) - how many move and how far at most (in model units): the chest in either game, the portcullis, the three
            // boulders, the shrine, the table.
            if (lOUw1 != null)
                Check.Str("models: faces moved to paint over (count, farthest in units): chest uw1, uw2 | portcullis | boulders | shrine | table",
                    fLayered(lOUw1.GetModel(UW3DModelImport.ModelIndex.Chest)) + " | " + fLayered(lOUw2.GetModel(UW3DModelImport.ModelIndex.Chest))
                    + " | " + fLayered(lOUw1.GetModel(UW3DModelImport.ModelIndex.Portcullis)) + " | "
                    + fLayered(lOUw1.GetModel(UW3DModelImport.ModelIndex.SmallBoulder)) + ", " + fLayered(lOUw1.GetModel(UW3DModelImport.ModelIndex.MediumBoulder))
                    + ", " + fLayered(lOUw1.GetModel(UW3DModelImport.ModelIndex.LargeBoulder)) + " | " + fLayered(lOUw1.GetModel(UW3DModelImport.ModelIndex.Shrine))
                    + " | " + fLayered(lOUw1.GetModel(UW3DModelImport.ModelIndex.Table)),
                    "19 1.14 | 19 1.14 | 0 0.00 | 0 0.00, 0 0.00, 0 0.00 | 0 0.00 | 0 0.00");

            // THE T-JUNCTIONS (UW3DModelLayering.CloseTJunctions, run by the import): the shrine's loop and crossbar, the
            // large boulder - the faces' corner counts after it, and a second run finds nothing more.
            if (lOUw1 != null)
            {
                UW3DModel lOShrine = lOUw1.GetModel(UW3DModelImport.ModelIndex.Shrine);
                UW3DModel lOLarge = lOUw1.GetModel(UW3DModelImport.ModelIndex.LargeBoulder);
                int liShrineCorners = 0, liLargeCorners = 0;

                foreach (UW3DModelFace lOFace in lOShrine.Faces)
                    liShrineCorners += lOFace.VertexIndices.Count;

                foreach (UW3DModelFace lOFace in lOLarge.Faces)
                    liLargeCorners += lOFace.VertexIndices.Count;

                Check.Str("models: corners after closing the T-junctions (shrine, large boulder), and a second run",
                    liShrineCorners + " " + liLargeCorners + " | " + UW3DModelLayering.CloseTJunctions(lOShrine) + " " + UW3DModelLayering.CloseTJunctions(lOLarge),
                    "261 181 | 0 0");
            }
        }

        private static string fLayered(UW3DModel pOModel)
        {
            List<UWVector3[]> lOCorners = UW3DModelLayering.FaceCorners(pOModel);
            int liMoved = 0;
            double ldFarthest = 0;

            for (int liFace = 0; liFace < pOModel.Faces.Count; liFace++)
            {
                double ldFace = 0;

                for (int liAt = 0; liAt < lOCorners[liFace].Length; liAt++)
                {
                    UWVector3 lOFrom = pOModel.Vertices[pOModel.Faces[liFace].VertexIndices[liAt]];
                    UWVector3 lOTo = lOCorners[liFace][liAt];
                    double ldX = lOFrom.X - lOTo.X, ldY = lOFrom.Y - lOTo.Y, ldZ = lOFrom.Z - lOTo.Z;

                    ldFace = System.Math.Max(ldFace, System.Math.Sqrt(ldX * ldX + ldY * ldY + ldZ * ldZ) * 256);
                }

                if (ldFace > 0.001)
                    liMoved++;

                ldFarthest = System.Math.Max(ldFarthest, ldFace);
            }

            return liMoved + " " + ldFarthest.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
