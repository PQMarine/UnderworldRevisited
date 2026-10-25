using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Does the installation come through: the one load, the nine levels, the strings, every
    /// texture, the 3D models of UW.EXE, the cutscenes and the sound.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The one read of the installation; every group that needs data takes what
        /// this loaded.</summary>
        private static void fCheckLoad(CheckContext pOContext)
        {
            if (pOContext.DataPath == null)
                fPass("load", "no data folder given, the data groups are skipped");
            else if (pOContext.TryLoad(out long llMilliseconds))
                fPass("load", string.Format("{0} ms", llMilliseconds));
            else
                fFail("load", pOContext.LoadError);
        }

        /// <summary>All nine levels, every tile in place, and everything lying in them known
        /// to COMOBJ.DAT.</summary>
        private static void fCheckLevels(DataImport pOData)
        {
            try
            {
                const int liTilesPerLevel = 64 * 64;

                int liLevels = pOData.Levels == null ? 0 : pOData.Levels.Count;
                int liObjects = 0;
                int liUnknown = 0;
                List<string> lOProblems = new List<string>();

                for (int liAt = 0; pOData.Levels != null && liAt < pOData.Levels.Count; liAt++)
                {
                    UWLevel lOLevel = pOData.Levels[liAt];

                    if (lOLevel == null || lOLevel.TileData == null || lOLevel.TileData.Length != liTilesPerLevel)
                    {
                        lOProblems.Add("level " + (liAt + 1) + " has no complete tile map");

                        continue;
                    }

                    foreach (UWTile lOTile in lOLevel.TileData)
                    {
                        if (lOTile == null)
                        {
                            lOProblems.Add("level " + lOLevel.LevelNumber + " has an empty tile");

                            break;
                        }

                        if (lOTile.ObjectsInTile == null)
                            continue;

                        foreach (UWObject lOObject in lOTile.ObjectsInTile)
                        {
                            if (lOObject == null)
                                continue;

                            liObjects++;

                            if (pOData.CommonObjectProperties != null
                                && !pOData.CommonObjectProperties.TryGet(lOObject.ID, out UWCommonObjectProperties.Entry lOIgnored))
                                liUnknown++;
                        }
                    }
                }

                if (liLevels != 9)
                    lOProblems.Add(liLevels + " levels instead of 9");

                if (liUnknown != 0)
                    lOProblems.Add(liUnknown + " objects without an entry in COMOBJ.DAT");

                if (lOProblems.Count == 0)
                    fPass("levels", string.Format("{0} levels, {1} objects in tiles", liLevels, liObjects));
                else
                    fFail("levels", string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("levels", lOException.Message);
            }
        }

        /// <summary>The string blocks the game addresses by number: messages, object and
        /// critter names, conditions, the character screen.</summary>
        private static void fCheckStrings(DataImport pOData)
        {
            try
            {
                if (pOData.Strings == null || pOData.Strings.Blocks == null)
                {
                    fFail("strings", "no string blocks");

                    return;
                }

                int[] liNeeded = { 1, 2, 3, 4, 5, 6, 7 };
                List<string> lOMissing = new List<string>();
                int liStrings = 0;

                foreach (KeyValuePair<int, UWStringBlock> lOPair in pOData.Strings.Blocks)
                    liStrings += lOPair.Value == null || lOPair.Value.Strings == null ? 0 : lOPair.Value.Strings.Count;

                foreach (int liBlock in liNeeded)
                {
                    if (!pOData.Strings.Blocks.TryGetValue(liBlock, out UWStringBlock lOBlock)
                        || lOBlock.Strings == null || lOBlock.Strings.Count == 0)
                        lOMissing.Add(liBlock.ToString());
                }

                // Block 4 holds the object names, one per object id.
                if (pOData.Strings.Blocks.TryGetValue(4, out UWStringBlock lOObjectNames)
                    && lOObjectNames.Strings != null && lOObjectNames.Strings.Count < 465)
                    lOMissing.Add("4 is too short (" + lOObjectNames.Strings.Count + ")");

                if (lOMissing.Count == 0)
                    fPass("strings", string.Format("{0} blocks, {1} strings",
                        pOData.Strings.Blocks.Count, liStrings));
                else
                    fFail("strings", "missing or too short: block " + string.Join(", ", lOMissing));
            }
            catch (Exception lOException)
            {
                fFail("strings", lOException.Message);
            }
        }

        /// <summary>Every picture of the kinds the game draws from, decoded once.</summary>
        private static void fCheckTextures(DataImport pOData)
        {
            UWTexture.TextureTypes[] leTypes =
            {
                UWTexture.TextureTypes.WALL,
                UWTexture.TextureTypes.FLOOR,
                UWTexture.TextureTypes.OBJECTS,
                UWTexture.TextureTypes.DOORS,
                UWTexture.TextureTypes.CURSORS,
                UWTexture.TextureTypes.ANIMO
            };

            try
            {
                int liTextures = 0;
                List<string> lOProblems = new List<string>();

                foreach (UWTexture.TextureTypes leType in leTypes)
                {
                    List<UWTexture> lOTextures = pOData.Textures.GetTexturesByType(leType);

                    if (lOTextures == null || lOTextures.Count == 0)
                    {
                        lOProblems.Add(leType + " is empty");

                        continue;
                    }

                    foreach (UWTexture lOTexture in lOTextures)
                    {
                        // EMPTY ENTRIES ARE NORMAL: the files end in unused slots the game
                        // never draws - three at the end of OBJECTS, one in ANIMO. Only a
                        // picture that claims a size and does not deliver the pixels is broken.
                        if (lOTexture == null || lOTexture.Width <= 0 || lOTexture.Height <= 0)
                            continue;

                        if (lOTexture.PaletteIndices == null
                            || lOTexture.PaletteIndices.Length < lOTexture.Width * lOTexture.Height)
                        {
                            lOProblems.Add(leType + " has a picture without pixels");

                            break;
                        }

                        liTextures++;
                    }
                }

                if (lOProblems.Count == 0)
                    fPass("textures", liTextures + " pictures in " + leTypes.Length + " kinds");
                else
                    fFail("textures", string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("textures", lOException.Message);
            }
        }

        /// <summary>
        /// The 3D models are not in a data file but in the game's executable, which is why
        /// only the GOG build is supported: the offsets are build-specific (see
        /// UWGogInstall.SupportedExeSha1).
        /// </summary>
        private static void fCheckModels(string psDataPath)
        {
            try
            {
                string lsExe = DataPath.FindExe(psDataPath);

                if (lsExe == null)
                {
                    fFail("models", "no UW.EXE next to the data folder");

                    return;
                }

                bool lbSupported = UWGogInstall.IsSupportedExe(lsExe);

                UW3DModelImport lOModels = new UW3DModelImport(lsExe);

                int liModels = 0;
                int liFaces = 0;

                for (int liAt = 0; liAt < UW3DModelImport.Count; liAt++)
                {
                    UW3DModel lOModel = lOModels.GetModel(liAt);

                    if (lOModel == null || lOModel.Faces == null || lOModel.Faces.Count == 0)
                        continue;

                    liModels++;
                    liFaces += lOModel.Faces.Count;
                }

                // Counting alone says little - only about half of the 64 slots carry geometry
                // in uw1. What has to be there is what the game actually places.
                string lsMissing = fGetMissingModels(lOModels);

                if (lsMissing != null)
                    fFail("models", "no geometry for: " + lsMissing);
                else if (!lbSupported)
                    fFail("models", string.Format("{0} models with {1} faces, but this UW.EXE is NOT the supported GOG build - colours and models can be wrong",
                        liModels, liFaces));
                else
                    fPass("models", string.Format("{0} models with {1} faces, UW.EXE is the supported build", liModels, liFaces));
            }
            catch (Exception lOException)
            {
                fFail("models", lOException.Message);
            }
        }

        /// <summary>The models the game puts into the world by name. Null if every one of them
        /// has geometry.</summary>
        private static string fGetMissingModels(UW3DModelImport pOModels)
        {
            KeyValuePair<string, int>[] lONeeded =
            {
                new KeyValuePair<string, int>("door frame", UW3DModelImport.ModelIndex.DoorFrame),
                new KeyValuePair<string, int>("bridge", UW3DModelImport.ModelIndex.Bridge),
                new KeyValuePair<string, int>("bench", UW3DModelImport.ModelIndex.Bench),
                new KeyValuePair<string, int>("small boulder", UW3DModelImport.ModelIndex.SmallBoulder),
                new KeyValuePair<string, int>("medium boulder", UW3DModelImport.ModelIndex.MediumBoulder),
                new KeyValuePair<string, int>("large boulder", UW3DModelImport.ModelIndex.LargeBoulder),
                new KeyValuePair<string, int>("arrow", UW3DModelImport.ModelIndex.Arrow),
                new KeyValuePair<string, int>("pillar", UW3DModelImport.ModelIndex.Pillar),
                new KeyValuePair<string, int>("shrine", UW3DModelImport.ModelIndex.Shrine),
                new KeyValuePair<string, int>("gravestone", UW3DModelImport.ModelIndex.Gravestone),
                new KeyValuePair<string, int>("moongate", UW3DModelImport.ModelIndex.Moongate),
                new KeyValuePair<string, int>("chest", UW3DModelImport.ModelIndex.Chest),
                new KeyValuePair<string, int>("nightstand", UW3DModelImport.ModelIndex.Nightstand),
                new KeyValuePair<string, int>("barrel", UW3DModelImport.ModelIndex.Barrel),
                new KeyValuePair<string, int>("chair", UW3DModelImport.ModelIndex.Chair)
            };

            List<string> lOMissing = new List<string>();

            foreach (KeyValuePair<string, int> lOEntry in lONeeded)
            {
                UW3DModel lOModel = pOModels.GetModel(lOEntry.Value);

                if (lOModel == null || lOModel.Faces == null || lOModel.Faces.Count == 0)
                    lOMissing.Add(lOEntry.Key);
            }

            return lOMissing.Count == 0 ? null : string.Join(", ", lOMissing);
        }

        /// <summary>Intro, dreams, end sequence: every animation file of the CUTS folder is
        /// decoded and asked for its frames.</summary>
        private static void fCheckCutscenes(string psDataPath)
        {
            try
            {
                string lsFolder = DataPath.FindSiblingFolder(psDataPath, "CUTS");

                if (lsFolder == null)
                {
                    fFail("cutscenes", "no CUTS folder next to the data folder");

                    return;
                }

                int liFiles = 0;
                int liFrames = 0;
                List<string> lOProblems = new List<string>();

                foreach (string lsFile in Directory.GetFiles(lsFolder, "CS*.N*"))
                {
                    // The .N00 of a sequence is its control script, not an animation.
                    if (lsFile.EndsWith("00", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        UWCutscene lOCutscene = new UWCutscene(lsFile);

                        if (lOCutscene.FrameCount <= 0 || lOCutscene.Width <= 0 || lOCutscene.Height <= 0)
                        {
                            lOProblems.Add(Path.GetFileName(lsFile) + " has no frames");

                            continue;
                        }

                        // Decoding only happens when a frame is asked for.
                        byte[] lyFrame = lOCutscene.GetFrame(lOCutscene.FrameCount - 1);

                        if (lyFrame == null || lyFrame.Length < lOCutscene.Width * lOCutscene.Height)
                        {
                            lOProblems.Add(Path.GetFileName(lsFile) + " has a short frame");

                            continue;
                        }

                        liFiles++;
                        liFrames += lOCutscene.FrameCount;
                    }
                    catch (Exception lOException)
                    {
                        lOProblems.Add(Path.GetFileName(lsFile) + ": " + lOException.Message);
                    }
                }

                if (lOProblems.Count == 0 && liFiles > 0)
                    fPass("cutscenes", string.Format("{0} files, {1} frames", liFiles, liFrames));
                else
                    fFail("cutscenes", lOProblems.Count == 0
                        ? "no animation files found"
                        : string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("cutscenes", lOException.Message);
            }
        }

        /// <summary>The 24 sound effects from SOUNDS.DAT, the AdLib instrument bank and every
        /// music piece the game asks for by number.</summary>
        private static void fCheckSound(DataImport pOData)
        {
            try
            {
                if (pOData.Sound == null || !pOData.Sound.IsAvailable)
                {
                    fFail("sound", "no SOUND folder");

                    return;
                }

                List<string> lOProblems = new List<string>();

                int liEffects = pOData.Sound.Effects == null ? 0 : pOData.Sound.Effects.Count;

                if (liEffects != 24)
                    lOProblems.Add(liEffects + " effects instead of 24");

                if (pOData.Sound.AdlibBank == null)
                    lOProblems.Add("no AdLib instrument bank");

                int liTracks = 0;
                int liNotes = 0;

                foreach (KeyValuePair<string, string> lOTrack in UWSound.MusicTrackNames)
                {
                    UWXmi lOMusic = pOData.Sound.GetMusic(lOTrack.Key, true);

                    if (lOMusic == null || lOMusic.Sequences == null || lOMusic.Sequences.Count == 0)
                    {
                        lOProblems.Add("AW" + lOTrack.Key + " (" + lOTrack.Value + ") has no sequence");

                        continue;
                    }

                    liTracks++;

                    foreach (UWXmi.Sequence lOSequence in lOMusic.Sequences)
                        liNotes += lOSequence.Notes == null ? 0 : lOSequence.Notes.Count;
                }

                if (lOProblems.Count == 0)
                    fPass("sound", string.Format("{0} effects, {1} music pieces with {2} notes",
                        liEffects, liTracks, liNotes));
                else
                    fFail("sound", string.Join("; ", lOProblems));
            }
            catch (Exception lOException)
            {
                fFail("sound", lOException.Message);
            }
        }
    }
}
