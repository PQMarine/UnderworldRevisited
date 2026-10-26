using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Hosts of single rule classes that only record what they are asked: the cutscene runner's,
    /// the endgame sequence's, the creature time pass's, the dragon animator's pictures and
    /// generator, the weapon hand.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>Records what a runner asks of its host: the cues as short tokens with their
        /// time in milliseconds, the frames and brightness values on their own, and everything
        /// in one log for the digest.</summary>
        private sealed class CutsceneRecorder : UWCutsceneRunner.IHost
        {
            public readonly List<string> Cues = new List<string>();

            public readonly List<int> Frames = new List<int>();

            public readonly List<float> Brightness = new List<float>();

            public readonly StringBuilder Log = new StringBuilder();

            public readonly List<string> Subtitles = new List<string>();

            /// <summary>Where the animation files lie; null plays without pictures.</summary>
            public string AnimationsFolder;

            /// <summary>The runner's tuning; null for the defaults.</summary>
            public UWCutsceneRunner.Timing Timing;

            public UWCutsceneRunner Runner;

            /// <summary>The recording lengths: even voices speak for 2.5 seconds, odd ones have
            /// no recording - both paths of a line.</summary>
            public bool WithSpeech = true;

            public int Milliseconds
            {
                get { return Runner == null ? 0 : (int)Math.Round(Runner.Now * 1000.0); }
            }

            private void fLog(string psEvent)
            {
                Log.Append(psEvent).Append('@').Append(Milliseconds).Append('\n');
            }

            private void fCue(string psCue)
            {
                Cues.Add(psCue + "@" + Milliseconds);
                fLog(psCue);
            }

            public UWCutscene OpenAnimation(string psFileName)
            {
                string lsPath = AnimationsFolder == null ? null : Path.Combine(AnimationsFolder, psFileName);
                if (lsPath == null || !File.Exists(lsPath))
                {
                    fCue("miss " + psFileName);

                    return null;
                }

                UWCutscene lOAnimation = new UWCutscene(lsPath);

                fCue("open " + psFileName);
                ShowFrame(0);

                return lOAnimation;
            }

            public void ShowFrame(int piFrame)
            {
                Frames.Add(piFrame);
                fLog("frame " + piFrame);
            }

            public void SetBrightness(float pfBrightness)
            {
                Brightness.Add(pfBrightness);
                fLog("bright " + pfBrightness.ToString("R", CultureInfo.InvariantCulture));
            }

            public float StartSpeech(int piVoice)
            {
                float lfLength = WithSpeech && piVoice % 2 == 0 ? 2.5f : -1f;

                fCue("voice " + piVoice + (lfLength < 0f ? " none" : string.Empty));

                return lfLength;
            }

            public void ShowLine(int piStringIndex, string psSubtitle, int piColourIndex, float pfSeconds)
            {
                Subtitles.Add(psSubtitle);
                fCue("line " + piStringIndex + " c" + piColourIndex + " "
                    + pfSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s" + (psSubtitle != null ? " text" : string.Empty));
            }

            public void PlayChime()
            {
                fCue("chime");
            }

            /// <summary>The cues in one line, for the pins.</summary>
            public string CueLine
            {
                get { return string.Join(" | ", Cues); }
            }

            /// <summary>The digest of the whole log, every frame and brightness value included.</summary>
            public string Digest
            {
                get { return fCutsceneDigest(Log.ToString()); }
            }
        }

        /// <summary>A launch host without a scene: logs what the sequence asks for. Since cleanup
        /// step 3.6 (2026-10-17) the messages, the gate and the journey go to the world, a FakeWorld
        /// writing into the same log, and this host keeps the camera.</summary>
        private sealed class LoggingEndgameHost : UWEndgameSequence.IHost
        {
            public readonly List<string> Log = new List<string>();

            public readonly FakeWorld World;

            public LoggingEndgameHost()
            {
                World = new FakeWorld(Log);
                World.Travelled = () => TravelledAt = Clock != null ? Clock.Now : -1;
            }

            public readonly List<int> Poses = new List<int>();

            public readonly List<UWMoongateFlight.Pose> PoseValues = new List<UWMoongateFlight.Pose>();

            public bool HasCamera = true;

            public long TravelledAt = -1;

            public UWWorldScheduler Clock;


            public bool TryGetCamera(out UWVector3 pOPosition, out float pfYaw)
            {
                pOPosition = new UWVector3(32 * 64f + 256f, 40f, 32 * 64f);
                pfYaw = 270f;

                return HasCamera;
            }

            public void BeginCamera(UWMoongateFlight.Pose pOPose) { Log.Add("camera"); }

            public void SetCameraPose(UWMoongateFlight.Pose pOPose)
            {
                Poses.Add(Poses.Count);
                PoseValues.Add(pOPose);
            }

            public void EndCamera() { Log.Add("end camera"); }
        }

        /// <summary>A host that records what the pass asked of the scene.</summary>
        private sealed class RecordingTimeHost : IUWCreatureTimeHost
        {
            public readonly UWWorldEdit Edit;

            public readonly List<UWNpc> Removed = new List<UWNpc>();

            public readonly List<UWNpc> Changed = new List<UWNpc>();

            public RecordingTimeHost(UWWorldEdit pOEdit)
            {
                Edit = pOEdit;
            }

            public void RemoveCreature(UWNpc pONpc)
            {
                Removed.Add(pONpc);
                UWLevelExit.RemoveFromLevel(Edit, pONpc);
            }

            public void CreatureChanged(UWNpc pONpc)
            {
                Changed.Add(pONpc);
            }
        }

        /// <summary>Records what the animator tells the pictures, one token per call.</summary>
        private sealed class RecordingDragonPictures : UWDragonAnimator.IPictures
        {
            public readonly StringBuilder Log = new StringBuilder();

            private void fAdd(string psToken)
            {
                if (Log.Length > 0)
                    Log.Append(' ');

                Log.Append(psToken);
            }

            public string Take()
            {
                string lsLog = Log.ToString();

                Log.Clear();

                return lsLog;
            }

            public void ShowTail(int piSide, int piImage) { fAdd("T" + piSide + ":" + piImage); }

            public void ShowOverlay(int piSide, int piImage) { fAdd("O" + piSide + ":" + piImage); }

            public void HideOverlay(int piSide) { fAdd("X" + piSide); }

            public void PlaceOverlay(int piSide, int piX, int piY) { fAdd("P" + piSide + ":" + piX + "," + piY); }

            public void ShowHead(int piSide, int piImage) { fAdd("H" + piSide + ":" + piImage); }

            public void HideHead(int piSide) { fAdd("H" + piSide + "-"); }
        }

        /// <summary>A scripted generator: the values in order, each with the bound it was asked for.</summary>
        private sealed class ScriptedDragonRandom
        {
            private readonly Queue<int> mOValues = new Queue<int>();

            public readonly List<int> Bounds = new List<int>();

            public int Fallback = 32767;

            public void Push(params int[] piValues)
            {
                foreach (int liValue in piValues)
                    mOValues.Enqueue(liValue);
            }

            public int Next(int piBound)
            {
                Bounds.Add(piBound);

                return mOValues.Count > 0 ? mOValues.Dequeue() : Fallback % piBound;
            }
        }

        private sealed class FakeWeaponHand : IUWWeaponHand
        {
            public bool IsCombatModeActive { get; set; }

            public bool IsAttacking { get; set; }

            public float AttackCharge { get; set; }

            public bool IsRangedTargeting { get; set; }
        }
    }
}
