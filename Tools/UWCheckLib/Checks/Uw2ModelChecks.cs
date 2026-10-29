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
        }
    }
}
