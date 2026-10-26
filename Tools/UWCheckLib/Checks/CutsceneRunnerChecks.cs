using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8.7 (2026-10-24): the cutscene interpreter without an engine,
    /// UWCutsceneRunner, out of UWIntroPlayer. The data-free group plays small scripts written
    /// here and states what the segment model does with them (lengths, 999 entries, repeats, the
    /// click rules, the holds, the fades, the file increment); the data group plays the game's own
    /// scripts at a steady 60 frames a second and pins what comes when. The pinned values were
    /// recorded from the runner after a scratch run had played the same scripts through a literal
    /// transcription of the old coroutine (UWIntroPlayer at cd2a99c1) under an emulation of Unity's
    /// coroutine scheduler: 1,000 runs over 25 cutscenes, five frame-time patterns, four click
    /// patterns, with and without speech, without a single difference.
    ///
    /// 2026-10-24 (per user): the click rules became the original's (skip targets, hidden frames,
    /// 0x0B, the debounce; a click while a recording plays counts). The click cases were re-pinned
    /// and clicked runs of the intro, the first dream and the end sequence added; of the runs
    /// without a click only the intro changed, by its 0x0B jump.
    /// </summary>
    public static partial class SelfChecks
    {
        private const float CutsceneTickSeconds = 1f / 60f;

        private const int CutsceneCueLineLimit = 400;

        /// <summary>FNV-1a, 64 bits, over the characters.</summary>
        private static string fCutsceneDigest(string psText)
        {
            ulong luHash = 14695981039346656037UL;

            for (int liAt = 0; liAt < psText.Length; liAt++)
            {
                luHash ^= psText[liAt];
                luHash *= 1099511628211UL;
            }

            return luHash.ToString("x16");
        }

        /// <summary>A control script from words: frame, command, arguments, ... - written to a
        /// scratch file because UWCutsceneScript reads files only.</summary>
        private static UWCutsceneScript fCutsceneScript(params int[] piWords)
        {
            string lsFile = Path.GetTempFileName();

            try
            {
                byte[] lyBytes = new byte[piWords.Length * 2];

                for (int liAt = 0; liAt < piWords.Length; liAt++)
                {
                    lyBytes[liAt * 2] = (byte)(piWords[liAt] & 0xFF);
                    lyBytes[(liAt * 2) + 1] = (byte)((piWords[liAt] >> 8) & 0xFF);
                }

                File.WriteAllBytes(lsFile, lyBytes);

                return new UWCutsceneScript(lsFile);
            }
            finally
            {
                File.Delete(lsFile);
            }
        }

        /// <summary>Plays a script as UWIntroPlayer does: the first animation file is opened
        /// before the start (fPrepare), then one Tick per 60th of a second; a click arrives
        /// before the Tick of its frame, as Update collects it before the coroutine runs.
        /// Returns the frame the script ended in, or -1 if it ran past piMaxTicks.</summary>
        private static int fPlayCutscene(CutsceneRecorder pORecorder, int piCutscene, UWCutsceneScript pOScript,
            UWStrings pOStrings, int piMaxTicks, params int[] piClickTicks)
        {
            UWCutscene lOFirst = pORecorder.OpenAnimation(UWCutsceneRunner.GetFirstAnimationFile(piCutscene));
            UWCutsceneRunner lORunner = new UWCutsceneRunner(piCutscene, pOScript, lOFirst, pOStrings, pORecorder,
                pORecorder.Timing ?? new UWCutsceneRunner.Timing());

            pORecorder.Runner = lORunner;

            bool lbRunning = lORunner.Start();
            int liTick = 0;

            while (lbRunning && liTick < piMaxTicks)
            {
                liTick++;

                if (Array.IndexOf(piClickTicks, liTick) >= 0)
                    lORunner.RequestAdvance();

                lbRunning = lORunner.Tick(CutsceneTickSeconds);
            }

            return lbRunning ? -1 : liTick;
        }

        // Commands as the script stores them (UWCutsceneScript.Command).
        private const int CsShowText = 0x00, CsPause = 0x03, CsSkip = 0x04, CsSetFrame = 0x05, CsEnd = 0x06, CsRepeat = 0x07,
            CsOpen = 0x08, CsFadeOut = 0x09, CsFadeIn = 0x0A, CsJump = 0x0B, CsTextAndPlay = 0x0D, CsHold = 0x0E, CsChime = 0x0F;

        private static void fCheckCutsceneRunner()
        {
            Check.Group("Cutscene runner (UWCutsceneRunner - cleanup step 8.7)");

            // Where and with what a cutscene plays.
            Check.Int("cutscene: the intro brings no music of its own", UWCutsceneRunner.GetMusicTheme(0), -1);
            Check.Int("cutscene: the end sequence starts theme 4", UWCutsceneRunner.GetMusicTheme(1), 4);
            Check.Int("cutscene: ... and so does 3", UWCutsceneRunner.GetMusicTheme(3), 4);
            Check.Int("cutscene: the title does not", UWCutsceneRunner.GetMusicTheme(9), -1);
            Check.Bool("cutscene: 0xFF full screen", UWCutsceneRunner.PlaysInViewport(0xFF), false);
            Check.Bool("cutscene: 0x100 in the viewport", UWCutsceneRunner.PlaysInViewport(0x100), true);
            Check.Str("cutscene: the first dream's script is CS030.N00 (octal)", UWCutsceneRunner.GetScriptFile(24), "CS030.N00");
            Check.Str("cutscene: the final death starts with CS403.N01", UWCutsceneRunner.GetFirstAnimationFile(0x103), "CS403.N01");
            Check.Int("cutscene: the subtitles of the end sequence are block 0xC01", UWCutsceneRunner.GetSpeechBlock(1), 0xC01);

            // Without animation files every segment runs at the fallback rate, six a second.
            CutsceneRecorder lORecorder = new CutsceneRecorder();
            int liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsShowText, 240, 5, 3, CsSetFrame, 0, 0, CsEnd), null, 1000);

            Check.Int("segment: the frame-set's number is its length, 3 frames at 6 a second end at tick 30", liEnd, 30);
            Check.Seq("segment: frames 0 to 2 shown, no frame 3", lORecorder.Frames.ToArray(), new[] { 0, 1, 2 });
            Check.Str("segment: the line at once, the next file after the segment",
                lORecorder.CueLine, "miss CS040.N01@0 | line 5 c240 1.5s@0 | miss CS040.N02@500");
            Check.Bool("segment: no fade-in in the script, so it starts bright",
                lORecorder.Brightness.Count == 1 && lORecorder.Brightness[0] == 1f, true);

            // THE CLICK RULES, the original's since 2026-10-24 (per user): a frame wait is never cut
            // short. Without a skip target ahead, a click in a segment WITHOUT a line ends it there
            // (the title, the anvil); in one WITH a line it does nothing outside a hold - the race to
            // the segment's end is gone. Clicks count 0x40 PIT ticks (15 ticks here) after the start.
            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsShowText, 240, 5, 3, CsSetFrame, 0, 3, CsSetFrame, 0, 0, CsEnd), null, 1000, 17);
            Check.Int("click: a line segment without a target plays on, the next one too", liEnd, 60);
            Check.Seq("click: ... frames 0, 1, 2 twice", lORecorder.Frames.ToArray(), new[] { 0, 1, 2, 0, 1, 2 });

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsChime, 3, CsSetFrame, 0, 3, CsSetFrame, 0, 0, CsEnd), null, 1000, 17);
            Check.Int("click: a silent segment ends at the next frame, the next one plays in full", liEnd, 50);
            Check.Seq("click: ... frames 0, 1, 2 then 0, 1, 2", lORecorder.Frames.ToArray(), new[] { 0, 1, 2, 0, 1, 2 });

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsChime, 3, CsSetFrame, 0, 3, CsSetFrame, 0, 0, CsEnd), null, 1000, 5);
            Check.Int("click: debounce, a click 5 ticks after the start does not count", liEnd, 60);

            // Opcode 4 sets the skip target: a click after a frame's wait runs the frames up to it
            // hidden - no frame, no wait, but their commands (the chime, the line) - and the picture
            // goes on from the target. The fade-in (rate 0) opens the gate.
            int[] liSkipScript = { 0, CsFadeIn, 0, 1, CsShowText, 240, 5, 1, CsSkip, 6, 3, 3, CsChime, 5, CsShowText, 240, 6,
                8, CsSetFrame, 0, 0, CsEnd };

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(liSkipScript), null, 1000);
            Check.Int("skip: no click, no change (speech output, so no hold)", liEnd, 80);
            Check.Seq("skip: ... frames 0 to 7", lORecorder.Frames.ToArray(), new[] { 0, 1, 2, 3, 4, 5, 6, 7 });

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(liSkipScript), null, 1000, 25);
            Check.Int("skip: a click in frame 2's wait jumps to frame 6 after it", liEnd, 50);
            Check.Seq("skip: ... frames 0, 1, 2 then 6, 7", lORecorder.Frames.ToArray(), new[] { 0, 1, 2, 6, 7 });
            Check.Str("skip: ... the hidden frames' chime and line still run, at the jump", lORecorder.CueLine,
                "miss CS040.N01@0 | line 5 c240 1.5s@167 | chime@500 | line 6 c240 1.5s@500 | miss CS040.N02@833");

            int[] liSkipUnfaded = new int[liSkipScript.Length - 3];

            Array.Copy(liSkipScript, 3, liSkipUnfaded, 0, liSkipUnfaded.Length);
            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(liSkipUnfaded), null, 1000, 25);
            Check.Int("skip: never faded in, the click is dropped (show_anm: fade47 == -2)", liEnd, 80);

            lORecorder = new CutsceneRecorder();
            lORecorder.Timing = new UWCutsceneRunner.Timing { SpeechOutput = false };
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsFadeIn, 0, 1, CsSkip, 6, 2, 8, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Int("skip: without speech output frame 5 also holds 2 seconds (80 + 120 ticks)", liEnd, 200);

            // A click during a fade-in counts for the target set after it in the same frame: the
            // original fades after the frame's opcodes and polls the input after the fade.
            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(1, CsFadeIn, 1, 1, CsSkip, 5, 0, 7, CsSetFrame, 0, 0, CsEnd), null, 1000, 20);
            Check.Seq("skip: a click during the fade-in jumps after frame 1", lORecorder.Frames.ToArray(), new[] { 0, 1, 5, 6 });

            // A click while a recording plays counts (the deliberate deviation): the jump as without
            // speech.
            lORecorder = new CutsceneRecorder();
            fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsFadeIn, 0, 1, CsTextAndPlay, 241, 1, 4, 1, CsSkip, 5, 0, 7, CsSetFrame, 0, 0, CsEnd), null, 1000, 25);
            Check.Seq("speech: a click while the recording plays jumps all the same", lORecorder.Frames.ToArray(), new[] { 0, 1, 2, 5, 6 });

            // A repeat makes the frame after it the target: a click ends the repeats and runs on.
            lORecorder = new CutsceneRecorder();
            fPlayCutscene(lORecorder, 0x20, fCutsceneScript(0, CsFadeIn, 0, 2, CsRepeat, 3, 4, CsSetFrame, 0, 0, CsEnd), null, 1000, 25);
            Check.Seq("repeat: a click in the first repeat skips the rest to frame 3", lORecorder.Frames.ToArray(), new[] { 0, 1, 2, 3 });

            // 0x0B jumps by itself: to the frame before its argument, hidden, so the picture goes on
            // with the argument's frame - unless that is the next frame anyway.
            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(1, CsJump, 5, 3, CsChime, 7, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Int("jump: from frame 1 to 5, three frames fewer", liEnd, 40);
            Check.Seq("jump: ... frames 0, then 4 to 6", lORecorder.Frames.ToArray(), new[] { 0, 4, 5, 6 });
            Check.Str("jump: ... the hidden frame's chime runs", lORecorder.CueLine, "miss CS040.N01@0 | chime@167 | miss CS040.N02@667");

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(1, CsJump, 2, 7, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Int("jump: to the next frame does nothing", liEnd, 70);

            // Pause holds the picture for its argument in seconds; a click cuts it short and is used
            // up by it, so even a silent segment plays on.
            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(1, CsPause, 2, 3, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Int("pause: 2 seconds at frame 1, before it is shown (10 + 120 + 20 ticks)", liEnd, 150);

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(1, CsPause, 2, 3, CsSetFrame, 0, 0, CsEnd), null, 1000, 40);
            Check.Int("pause: a click ends the hold and is used up, the silent segment plays on", liEnd, 60);

            // The debounce counts from the last click that counted: 10 ticks after it nothing, 20
            // ticks after it the next hold breaks.
            int[] liTwoPauses = { 0, CsShowText, 240, 5, 1, CsPause, 2, 2, CsPause, 2, 3, CsSetFrame, 0, 0, CsEnd };

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(liTwoPauses), null, 1000, 20, 30);
            Check.Int("debounce: the second click 10 ticks later does not count", liEnd, 160);

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(liTwoPauses), null, 1000, 20, 40);
            Check.Int("debounce: ... 20 ticks later it does", liEnd, 50);

            // 0x0E: with a recording the hold lasts until it has ended and then argument 2 seconds;
            // without one, argument 1 seconds.
            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsTextAndPlay, 241, 1, 4, 1, CsHold, 3, 1, 2, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Int("hold 0x0E: the recording's 2.5 s, then 1 s, then frame 1 (210 + 10 ticks)", liEnd, 220);
            Check.Str("hold 0x0E: no subtitle while the recording speaks", lORecorder.CueLine,
                "miss CS040.N01@0 | voice 4@0 | line 1 c241 2.5s@0 | miss CS040.N02@3667");

            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20,
                fCutsceneScript(0, CsTextAndPlay, 241, 1, 5, 1, CsHold, 3, 1, 2, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Int("hold 0x0E: without a recording, 3 s (10 + 180 + 10 ticks)", liEnd, 200);
            Check.Bool("hold 0x0E: ... the missing recording leaves the reading time", lORecorder.CueLine.Contains("voice 5 none@0 | line 1 c241 1.5s"), true);

            // Commands numbered 999 run after the segment's frames.
            lORecorder = new CutsceneRecorder();
            fPlayCutscene(lORecorder, 0x20, fCutsceneScript(2, CsSetFrame, 0, 999, CsChime, 0, CsEnd), null, 1000);
            Check.Str("999: the chime after both frames", lORecorder.CueLine, "miss CS040.N01@0 | chime@333 | miss CS040.N02@333");

            // Repeat-segment: from the segment's start up to its frame, argument times in total.
            lORecorder = new CutsceneRecorder();
            fPlayCutscene(lORecorder, 0x20, fCutsceneScript(2, CsRepeat, 3, 4, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Seq("repeat: three times 0 to 2, then on", lORecorder.Frames.ToArray(), new[] { 0, 1, 2, 0, 1, 2, 0, 1, 2, 3 });

            // A fade-in: rate * 8 steps of at least 8 PIT ticks (1/32 s), so two ticks each here.
            lORecorder = new CutsceneRecorder();
            liEnd = fPlayCutscene(lORecorder, 0x20, fCutsceneScript(0, CsFadeIn, 1, 1, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Int("fade: starts dark, 8 steps up to bright", lORecorder.Brightness.Count, 9);
            Check.Bool("fade: ... 0 first, 1 last", lORecorder.Brightness[0] == 0f && lORecorder.Brightness[8] == 1f, true);
            Check.Int("fade: 16 ticks of steps, then the frame (10 ticks)", liEnd, 26);

            lORecorder = new CutsceneRecorder();
            fPlayCutscene(lORecorder, 0x20, fCutsceneScript(0, CsFadeOut, 0, 1, CsSetFrame, 0, 0, CsEnd), null, 1000);
            Check.Bool("fade: rate 0 switches at once", lORecorder.Brightness.Count == 2 && lORecorder.Brightness[1] == 0f, true);

            // Open-file names its file (octal); after a segment that opened one, no increment.
            lORecorder = new CutsceneRecorder();
            fPlayCutscene(lORecorder, 0x20, fCutsceneScript(1, CsOpen, 0x103, 2, 1, CsSetFrame, 0, 1, CsSetFrame, 0, 0, CsEnd),
                null, 1000);
            Check.Str("open-file: CS403.N02 by the script, then this one's N02", lORecorder.CueLine,
                "miss CS040.N01@0 | miss CS403.N02@167 | miss CS040.N02@333");

            // The automatic increment counts in octal: after .N07 come .N10, .N11 and .N12.
            List<int> lOWords = new List<int>();

            for (int liSegment = 0; liSegment < 9; liSegment++)
                lOWords.AddRange(new[] { 1, CsSetFrame, 0 });

            lOWords.AddRange(new[] { 0, CsEnd });
            lORecorder = new CutsceneRecorder();
            fPlayCutscene(lORecorder, 0x20, fCutsceneScript(lOWords.ToArray()), null, 1000);
            Check.Bool("open-file: ... N07, N10, N11, N12", lORecorder.CueLine.EndsWith(
                "miss CS040.N07@1000 | miss CS040.N10@1167 | miss CS040.N11@1333 | miss CS040.N12@1500"), true);
        }

        /// <summary>The CUTS folder with the control scripts: next to DATA, or in the save folder's
        /// installation (the GOG image holds the animations, UNDEROM1\CUTS the .N00 files).</summary>
        private static string fFindCutsceneScripts(CheckContext pOContext, string psAnimations)
        {
            if (psAnimations != null && File.Exists(Path.Combine(psAnimations, UWCutsceneRunner.GetScriptFile(0))))
                return psAnimations;

            string lsSaves = string.IsNullOrEmpty(pOContext.SavesPath) ? null : Path.Combine(pOContext.SavesPath, "CUTS");

            return lsSaves != null && File.Exists(Path.Combine(lsSaves, UWCutsceneRunner.GetScriptFile(0))) ? lsSaves : null;
        }

        private static void fCheckCutsceneRunnerData(CheckContext pOContext)
        {
            Check.Group("Cutscene runner on the game's scripts (UWCutsceneRunner - cleanup step 8.7)");

            string lsAnimations = DataPath.FindSiblingFolder(pOContext.DataPath, "CUTS");
            string lsScripts = fFindCutsceneScripts(pOContext, lsAnimations);

            if (lsAnimations == null || lsScripts == null)
            {
                Check.Pass("cutscene scripts", "no CUTS folder with CS000.N00 next to DATA or under --saves");

                return;
            }

            UWStrings lOStrings = pOContext.Data.Strings;

            Check.Bool("intro: the script fades in, so it starts dark",
                UWCutsceneRunner.ScriptFadesIn(new UWCutsceneScript(Path.Combine(lsScripts, "CS000.N00"))), true);
            Check.Bool("death: CS402 has no fade-in (it starts bright)",
                UWCutsceneRunner.ScriptFadesIn(new UWCutsceneScript(Path.Combine(lsScripts, "CS402.N00"))), false);

            // The intro without a click changed once, on 2026-10-24, and only by its 0x0B jump
            // (CS000.N13 runs from frame 1 to 4 hidden; until then 13992, e9549cbb4742bcc4,
            // 7c90d2a2154f3ccf - with 0x0B switched off the runner still gave exactly those).
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "intro", 0, null, 13947,
                "120 cues 291f1d3db51b8b25", "de4458aaf1c001fa");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "end sequence", 1, null, 6307,
                "39 cues e923cc03d080163e", "f330aa83dcd28175");

            // Clicked through with the original's rules (2026-10-24): to the skip targets, the
            // frames between hidden. The intro's clicks fall in line 11 (to frame 67 of CS000.N10)
            // and in the silent CS000.N20 (to its end, then CS000.N22); the dream's in line 0 (to the
            // segment's end) and line 2 (to frame 13); the end sequence's in line 0 and line 12 (the
            // click ends its repeat, so line 12 starts once instead of twice: 38 cues).
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "intro clicked", 0, new[] { 2970, 11936 }, 13307,
                "120 cues 455795c2d7003c74", "47287fd978a9164a");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "first dream clicked", 24, new[] { 396, 760 }, 904,
                "miss CS030.N01@0 | open CS000.N02@0 | open CS000.N05@2733 | line 0 c32 5.4s text@2900 | open CS000.N05@6733"
                + " | line 1 c32 4.8s text@6900 | open CS000.N04@11567 | line 2 c32 3.18s text@11733 | open CS000.N06@14067",
                "bce47eb23fb0bb39");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "end sequence clicked", 1, new[] { 145, 4178 }, 5975,
                "38 cues 5ce5763794a13f48", "a374cf2fae3b30b3");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "title", 9, null, 1092,
                "open CS011.N01@0", "94d8dc60988eedad");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "title clicked away", 9, new[] { 30 }, 60,
                "open CS011.N01@0", "3c224f09ae162504");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "first dream", 24, null, 1014,
                "miss CS030.N01@0 | open CS000.N02@0 | open CS000.N05@2733 | line 0 c32 5.4s text@2900 | open CS000.N05@7567"
                + " | line 1 c32 4.8s text@7733 | open CS000.N04@12400 | line 2 c32 3.18s text@12567 | open CS000.N06@15900",
                "da86da135cc44495");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "death", 0x102, null, 576,
                "open CS402.N01@0", "27692539083ea79c");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "final death", 0x103, null, 780,
                "open CS403.N01@0 | open CS403.N02@3600", "972b1cec47a20ed1");
            // A click during the skulls' repeat ends the final death (per user, 2026-10-24: a
            // deviation - the original drops it, the viewport cutscenes are never faded in).
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "final death clicked in the skulls", 0x103, new[] { 300 }, 300,
                "open CS403.N01@0 | open CS403.N02@3600", "090f55449267a743");
            fCheckCutscenePin(lsAnimations, lsScripts, lOStrings, "anvil", 0x104, null, 216,
                "open CS404.N01@0 | chime@400 | chime@1300 | chime@2200 | chime@3100", "ccc883c6bc03d20c");

            // The painted block of the viewport cutscenes (UWCutscene.MeasurePaintedBlock, moved out
            // of UWIntroPlayer the same day): what UWCutsceneView cuts out of the 320x200 canvas and
            // places in the view window or the leather frame. CS402.N01 also fixes the canvas offset
            // of all of them (UWCutsceneView.fGetViewportCanvasOffset).
            fCheckPaintedBlock(lsAnimations, "CS402.N01", "148 88 172 112");
            fCheckPaintedBlock(lsAnimations, "CS403.N01", "148 76 172 124");
            fCheckPaintedBlock(lsAnimations, "CS403.N02", "146 76 174 124");
            fCheckPaintedBlock(lsAnimations, "CS404.N01", "148 88 172 112");
        }

        private static void fCheckPaintedBlock(string psAnimations, string psFile, string psExpected)
        {
            string lsPath = Path.Combine(psAnimations, psFile);

            if (!File.Exists(lsPath))
            {
                Check.Fail("painted block of " + psFile, "file missing");

                return;
            }

            UWCutscene.MeasurePaintedBlock(lsPath, out int liX, out int liY, out int liWidth, out int liHeight);
            Check.Str("painted block of " + psFile + " (x y width height)", liX + " " + liY + " " + liWidth + " " + liHeight, psExpected);
        }

        /// <summary>Plays one of the game's cutscenes and compares the tick it ends in, its cues
        /// and the digest of the whole log against the pinned values.</summary>
        private static void fCheckCutscenePin(string psAnimations, string psScripts, UWStrings pOStrings, string psName,
            int piCutscene, int[] piClicks, int piEndTick, string psCues, string psDigest)
        {
            UWCutsceneScript lOScript = new UWCutsceneScript(Path.Combine(psScripts, UWCutsceneRunner.GetScriptFile(piCutscene)));

            if (!lOScript.IsLoaded)
            {
                Check.Fail("cutscene " + psName, UWCutsceneRunner.GetScriptFile(piCutscene) + " not found");

                return;
            }

            CutsceneRecorder lORecorder = new CutsceneRecorder();

            lORecorder.AnimationsFolder = psAnimations;

            int liEnd = fPlayCutscene(lORecorder, piCutscene, lOScript, pOStrings, 200000, piClicks ?? new int[0]);

            // A long cue line (the intro, the end sequence) is pinned by its count and digest.
            string lsCues = lORecorder.CueLine;

            if (lsCues.Length > CutsceneCueLineLimit)
                lsCues = lORecorder.Cues.Count + " cues " + fCutsceneDigest(lsCues);

            Check.Int("cutscene " + psName + ": ends at tick", liEnd, piEndTick);
            Check.Str("cutscene " + psName + ": cues", lsCues, psCues);
            Check.Str("cutscene " + psName + ": every frame and fade step (digest)", lORecorder.Digest, psDigest);
        }
    }
}
