using System.Collections.Generic;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.18 (2026-10-23): the HUD dragons' frame logic (UWDragonAnimator) left
    /// UWHudDragons. Pinned with what that class played, step by step, with the random draws
    /// scripted: the scroll's two passes over frames 2 to 5, the nod's 6,7,8,9,8,9,8,9,8,7,6, the
    /// cover's climb to 13, six held steps and the way back with the head hidden meanwhile, the
    /// right dragon's frames plus 18 and its corner moved by the widened frame, the choice of the
    /// dragon, the ignored duplicate request, the waiting request cutting the scroll short, and the
    /// tail wag that only plays while a dragon's state machine runs (the UW.EXE quirk). The order
    /// of the random draws is pinned by what the scripted generator is asked for.
    /// </summary>
    public static partial class Program
    {
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

        private static void fCheckDragonAnimator()
        {
            Check.Group("HUD dragons (UWDragonAnimator - cleanup step 5.18)");

            // THE SCROLL on the left dragon: the side drawn as 0, no tail wag (every roll 32767).
            RecordingDragonPictures lOPictures = new RecordingDragonPictures();
            ScriptedDragonRandom lORandom = new ScriptedDragonRandom();
            UWDragonAnimator lOAnimator = new UWDragonAnimator(lOPictures, 0, lORandom.Next);

            lORandom.Push(0);
            lOAnimator.Request(UWDragonAnimator.ScrollAnimation);
            Check.Str("request: with both dragons free the side is drawn, bound 2", string.Join(",", lORandom.Bounds), "2");
            fExpectInt("request: nothing plays before the first step", lOAnimator.Current(0), 0);

            lORandom.Bounds.Clear();
            lOAnimator.Step();
            Check.Str("step: the flask roll first, then the tail roll, both of 32768", string.Join(",", lORandom.Bounds), "32768,32768");
            Check.Str("scroll: the first step places the overlay at (40, 155) and shows frame 2", lOPictures.Take(), "P0:40,155 O0:2");
            fExpectInt("scroll: the left dragon plays the scroll", lOAnimator.Current(0), UWDragonAnimator.ScrollAnimation);
            fExpectBool("scroll: the overlay is active", lOAnimator.IsOverlayActive(0), true);

            string lsFrames = string.Empty;

            for (int liStep = 0; liStep < 9; liStep++)
            {
                lOAnimator.Step();
                lsFrames += lOPictures.Take() + "|";
            }

            Check.Str("scroll: frames 2..5 twice, then hidden, then nothing", lsFrames, "O0:3|O0:4|O0:5|O0:2|O0:3|O0:4|O0:5|X0||");
            fExpectInt("scroll: over, the dragon is free", lOAnimator.Current(0), 0);
            fExpectBool("scroll: the overlay is inactive", lOAnimator.IsOverlayActive(0), false);

            // A SECOND REQUEST of the same animation while it runs or waits is ignored.
            lORandom.Push(0);
            lOAnimator.Request(UWDragonAnimator.ScrollAnimation);
            lORandom.Bounds.Clear();
            lOAnimator.Request(UWDragonAnimator.ScrollAnimation);
            Check.Str("request: a waiting scroll ignores a second scroll (no draw)", string.Join(",", lORandom.Bounds), string.Empty);
            lOAnimator.Step();
            lOAnimator.Request(UWDragonAnimator.ScrollAnimation);
            Check.Str("request: a running scroll ignores a second scroll (no draw)", string.Join(",", lORandom.Bounds), "32768,32768");
            lOPictures.Take();

            // THE NOD on the right dragon while the left scrolls: the free one is taken without a
            // draw; its frames are the left one's plus 18, its corner the right table entry.
            lORandom.Bounds.Clear();
            lOAnimator.Request(UWDragonAnimator.NodAnimation);
            Check.Str("request: with the left dragon busy the right one is taken, no draw", string.Join(",", lORandom.Bounds), string.Empty);
            lsFrames = string.Empty;

            for (int liStep = 0; liStep < 12; liStep++)
            {
                lOAnimator.Step();
                lsFrames += lOPictures.Take() + "|";
            }

            Check.Str("nod: the right dragon's 6,7,8,9,8,9,8,9,8,7,6 plus 18 at (204, 145), beside the left scroll's pass",
                lsFrames,
                "O0:3 P1:204,145 O1:24|O0:4 O1:25|O0:5 O1:26|O0:2 O1:27|O0:3 O1:26|O0:4 O1:27|O0:5 O1:26|X0 O1:27|O1:26|O1:25|O1:24|X1|");
            fExpectBool("nod: both free afterwards", lOAnimator.Current(0) == 0 && lOAnimator.Current(1) == 0, true);

            // THE COVER on the right dragon of a widened frame (the right pictures 100 columns to the
            // right): the head is hidden first, the climb 28..31, six held steps, back to 28, the
            // head back.
            RecordingDragonPictures lOWidePictures = new RecordingDragonPictures();
            ScriptedDragonRandom lOWideRandom = new ScriptedDragonRandom();
            UWDragonAnimator lOWide = new UWDragonAnimator(lOWidePictures, 100, lOWideRandom.Next);

            lOWideRandom.Push(1);
            lOWide.Request(UWDragonAnimator.CoverAnimation);
            lsFrames = string.Empty;

            for (int liStep = 0; liStep < 15; liStep++)
            {
                lOWide.Step();

                if (liStep == 0)
                    fExpectBool("cover: the head is hidden while it covers its eyes", lOWide.IsCovering(1), true);

                lsFrames += lOWidePictures.Take() + "|";
            }

            Check.Str("cover: head hidden, corner (200 + 100, 145), 28..31, six held steps, 30 29 28, head 19 back, hidden",
                lsFrames,
                "H1- P1:300,145 O1:28|O1:29|O1:30|O1:31|||||||O1:30|O1:29|O1:28|H1:19 X1||");
            fExpectBool("cover: over, the head shows again", lOWide.IsCovering(1), false);

            // A WAITING REQUEST cuts the scroll short: its pass ends, then the next animation. With
            // the left scrolling and the right nodding (both busy, nothing waiting) the side of the
            // cover is drawn; it goes to the left, whose scroll runs its last pass first.
            RecordingDragonPictures lOCutPictures = new RecordingDragonPictures();
            ScriptedDragonRandom lOCutRandom = new ScriptedDragonRandom();
            UWDragonAnimator lOCut = new UWDragonAnimator(lOCutPictures, 0, lOCutRandom.Next);

            lOCutRandom.Push(0);
            lOCut.Request(UWDragonAnimator.ScrollAnimation);
            lOCut.Step();
            lOCut.Step();
            lOCut.Request(UWDragonAnimator.NodAnimation);
            lOCut.Step();
            Check.Str("cut: the left scrolls, the right begins its nod", lOCutPictures.Take(), "P0:40,155 O0:2|O0:3|O0:4 P1:204,145 O1:24".Replace("|", " "));
            lOCutRandom.Bounds.Clear();
            lOCutRandom.Push(0);
            lOCut.Request(UWDragonAnimator.CoverAnimation);
            Check.Str("request: both busy and nothing waiting draws the side", string.Join(",", lOCutRandom.Bounds), "2");
            lsFrames = string.Empty;

            for (int liStep = 0; liStep < 7; liStep++)
            {
                lOCut.Step();
                lsFrames += lOCutPictures.Take() + "|";
            }

            Check.Str("cut: the scroll ends its pass (5, then 2..5), hides, and the cover starts on the left while the nod goes on",
                lsFrames, "O0:5 O1:25|O0:2 O1:26|O0:3 O1:27|O0:4 O1:26|O0:5 O1:27|X0 O1:26|H0- P0:36,145 O0:10 O1:27|");

            // THE TAIL WAG: a roll below 0x666 with its low bit 0 marks the left tail; it only plays
            // while that dragon's state machine runs (the UW.EXE quirk) - nothing moves on a resting
            // dragon, and during a scroll the tail frames come first.
            RecordingDragonPictures lOTailPictures = new RecordingDragonPictures();
            ScriptedDragonRandom lOTailRandom = new ScriptedDragonRandom();
            UWDragonAnimator lOTail = new UWDragonAnimator(lOTailPictures, 0, lOTailRandom.Next);

            lOTailRandom.Push(0, 2);
            lOTail.Step();
            Check.Str("tail: a resting dragon does not wag, whatever the roll", lOTailPictures.Take(), string.Empty);

            lOTailRandom.Push(0);
            lOTail.Request(UWDragonAnimator.ScrollAnimation);
            lOTailRandom.Push(0, 2);
            lOTail.Step();
            Check.Str("tail: with the scroll running the tail's first frame comes before the overlay", lOTailPictures.Take(), "T0:14 P0:40,155 O0:2");
            lsFrames = string.Empty;

            for (int liStep = 0; liStep < 8; liStep++)
            {
                lOTail.Step();
                lsFrames += lOTailPictures.Take() + "|";
            }

            Check.Str("tail: 15,16,17,16,15,14 over the next steps, then rest", lsFrames, "T0:15 O0:3|T0:16 O0:4|T0:17 O0:5|T0:16 O0:2|T0:15 O0:3|T0:14 O0:4|O0:5|X0|");
        }
    }
}
