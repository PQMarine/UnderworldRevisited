using System;
using System.Collections.Generic;
using UWDataImport.Audio;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE CUTSCENE INTERPRETER without an engine (cleanup step 8.7, 2026-10-24: out of
	/// UWIntroPlayer.fRun, fExecute, fFade, fWaitOrClick and fStartLine, moved as they were): it
	/// walks the control script CSxxx.N00 (UWCutsceneScript) and decides which frame, which line,
	/// which fade step, which chime and which animation file comes when. What it decides it hands
	/// to an IHost - the Unity player draws and plays it, the self-check records it.
	///
	/// The script is a command list in which each entry carries a FRAME NUMBER - i.e. the
	/// moment at which it is triggered. So the timing comes from the picture track (6 or 10
	/// frames per second, noted in the header of each animation file), not from seconds. On
	/// every open-file the count starts over, because a new file is then running.
	///
	/// SEGMENT MODEL (see fRun): the script is cut into segments at each frame-set, and the
	/// frame number of the frame-set is the LENGTH of the segment. The segment's frame counter
	/// drives both picture and speech; a line does not wait for the previous recording to end
	/// (the older speech-driven model was removed on 2026-09-07).
	///
	/// If the picture track is missing - say because the CUTS folder is incomplete -, sound and text
	/// still run, then timed by the same frame counter at the fallback frame rate.
	///
	/// THE CLOCK is the time the host passes to Tick, in seconds; every wait is measured from the
	/// moment it is reached, as UW.EXE CutsceneDataProcessing_ovr105_869 measures each frame from
	/// the moment the last one ended (frame_start = now). The rates are fixed (the frame rate of the
	/// file, 1/32 second per fade step, seconds for the holds); the host's frames only decide how
	/// finely they are polled, as the original's PIT polling did. Driven once per Unity frame with
	/// the unscaled delta, it reproduces the coroutine it replaced step for step.
	///
	/// THE CLICK RULES ARE THE ORIGINAL'S since 2026-10-24 (decided per user; UW.EXE show_anm with
	/// cutsop_skip, cutsop_jump, cutsop_loop, cutsop_pause and cutsop_wait in UnderworldExhumed's
	/// CUTS.C): opcode 4 sets the frame a click skips to (frame3B), a click after a frame's wait
	/// with that target still ahead runs the frames up to it HIDDEN - their commands still execute,
	/// as show_anm runs the opcodes of every frame - and the picture goes on from the target; a
	/// click breaks a hold; opcode 0x0B jumps by itself; mouse buttons count only 0x40 PIT ticks
	/// after the last accepted one. ONE DELIBERATE DEVIATION: the original takes no input while a
	/// recording plays (speech43 != -1); ours does, and the click acts as one without speech would -
	/// the host's StartSpeech stops the running recording when the next line starts, so two voices
	/// never overlap. Until then a click in a segment with a line raced its frames through without
	/// waits, and 0x04 and 0x0B were not interpreted.
	/// </summary>
	public sealed class UWCutsceneRunner
	{
		/// <summary>What the interpreter needs from outside: the picture, the sound, the text.
		/// </summary>
		public interface IHost
		{
			/// <summary>Opens animation file psFileName from the CUTS folder, shows its frame 0
			/// and returns it - or null if it is missing or unreadable (then the picture track
			/// pauses and the last frame stays up).</summary>
			UWCutscene OpenAnimation(string psFileName);

			/// <summary>Shows frame piFrame of the open file; numbers outside it leave the last
			/// frame up.</summary>
			void ShowFrame(int piFrame);

			/// <summary>Brightness of the picture track, 0 dark to 1 bright - for the fades.
			/// </summary>
			void SetBrightness(float pfBrightness);

			/// <summary>Starts speech recording piVoice; returns its length in seconds, or a
			/// negative value if there is no such recording.</summary>
			float StartSpeech(int piVoice);

			/// <summary>A line starts: piStringIndex is its entry in the speech block (game
			/// numbering, UWCutsceneScript.NoText for none), psSubtitle its text, or null when the
			/// recording speaks it (or it has none); piColourIndex is the script's palette index;
			/// pfSeconds how long the line lasts (the recording, or the reading time).</summary>
			void ShowLine(int piStringIndex, string psSubtitle, int piColourIndex, float pfSeconds);

			/// <summary>The "Chime" sound of the intro.</summary>
			void PlayChime();
		}

		/// <summary>The tuning of lines and pictures - the player's Inspector values.</summary>
		public sealed class Timing
		{
			/// <summary>Frame rate if an animation file does not specify its own.</summary>
			public int FallbackFrameRate = 6;

			/// <summary>Seconds per character for lines without a speech recording.</summary>
			public float SecondsPerCharacter = 0.06f;

			/// <summary>Minimum duration of a line without a speech recording, in seconds.</summary>
			public float MinimumLineDuration = 1.5f;

			/// <summary>Is there speech output? The original's flag b5 (speech_available): without
			/// it a line shows its text instead of starting its recording, and opcode 4 also holds
			/// the frame before its target (cutsop_skip). Our player always has it; false only in
			/// the self-check (2026-10-24).</summary>
			public bool SpeechOutput = true;
		}

		/// <summary>Cutscenes from this number on play inside the 3D viewport, the game UI stays
		/// visible (reference cutsplayer: CutsceneNo >= 256 is not full screen, CutsSmall) - the
		/// death cutscenes 0x102 and 0x103 among them (per user, 2026-09-14).</summary>
		public const int FirstViewportCutscene = 0x100;

		/// <summary>Cutscenes with their own music, and the theme they load - see
		/// GetMusicTheme.</summary>
		public const int FirstMusicCutscene = 1;

		public const int LastMusicCutscene = 3;

		public const int CutsceneMusicTheme = 4;

		/// <summary>The frame number with which a command belongs AFTER the segment.</summary>
		public const int AfterSegmentFrame = 999;

		/// <summary>The picture track starts with this extension: .N01.</summary>
		public const int FirstAnimationExtension = 1;

		/// <summary>Palette steps per unit of fade rate (the rate is shifted left by three).</summary>
		public const int FadeStepsPerRate = 8;

		/// <summary>The PIT ticks each fade step waits at least.</summary>
		public const float FadeStepPitTicks = 8f;

		/// <summary>A mouse button counts only this many PIT ticks (256 a second) after the last
		/// accepted input or the start of the file: show_anm's GAME_TIME() - input_time > 0x40.
		/// Keys count at once there; our cutscenes take mouse buttons only (Escape ends them).
		/// </summary>
		public const int ClickDebouncePitTicks = 0x40;

		/// <summary>No skip target, no hold frame.</summary>
		private const int NoFrame = -1;

		/// <summary>Does cutscene piCutscene play inside the viewport? See FirstViewportCutscene.
		/// </summary>
		public static bool PlaysInViewport(int piCutscene)
		{
			return piCutscene >= FirstViewportCutscene;
		}

		/// <summary>
		/// The music theme a cutscene starts, or -1 for none. Cutscenes 1 to 3 (the end sequence
		/// is 1) bring their own music: UW.EXE PlayCutscene_ovr105_1402 calls LoadXMIFILE(4, 1)
		/// for them, theme 4 (AW04.XMI). LoadXMIFILE keeps a piece that is already playing, and so
		/// does UWAudioService.
		/// </summary>
		public static int GetMusicTheme(int piCutscene)
		{
			return piCutscene >= FirstMusicCutscene && piCutscene <= LastMusicCutscene ? CutsceneMusicTheme : -1;
		}

		/// <summary>The control script of a cutscene: CSxxx.N00 - the file names are octal, see
		/// UWCutscene.FileName.</summary>
		public static string GetScriptFile(int piCutscene)
		{
			return UWCutscene.FileName(piCutscene, 0);
		}

		/// <summary>What a sequence starts with before the first open-file comes: according to the
		/// file list in uw-formats.txt the .N01 is the black screen.</summary>
		public static string GetFirstAnimationFile(int piCutscene)
		{
			return UWCutscene.FileName(piCutscene, FirstAnimationExtension);
		}

		/// <summary>The string block of the subtitles is at 0x0C00 plus the sequence number.
		/// </summary>
		public static int GetSpeechBlock(int piCutscene)
		{
			return UWSound.SpeechStringBlock + piCutscene;
		}

		/// <summary>Does the script contain a fade-in? See fRun.</summary>
		public static bool ScriptFadesIn(UWCutsceneScript pOScript)
		{
			if (pOScript == null)
				return false;

			for (int liAt = 0; liAt < pOScript.Entries.Count; liAt++)
			{
				if (pOScript.Entries[liAt].Command == UWCutsceneScript.Command.FadeIn)
					return true;
			}

			return false;
		}

		/// <summary>The kinds of wait fRun can stand in.</summary>
		private enum WaitKind
		{
			/// <summary>A frame of the picture track; no click shortens it.</summary>
			Frame,

			/// <summary>A hold of the script, cut short by a click (fWaitOrClick until
			/// 2026-10-24); the click is used up by it, as show_anm's hold loop clears b1.</summary>
			HoldOrClick,

			/// <summary>Command 0x0E while a recording plays: until it has ended, or a click.
			/// </summary>
			SpeechOrClick,

			/// <summary>One step of a palette fade: until the host's time since it began reaches
			/// the step - summed from the deltas, as the fade coroutine summed them.</summary>
			FadeStep
		}

		private readonly int miCutscene;

		private readonly UWCutsceneScript mOScript;

		private readonly UWStrings mOStrings;

		private readonly IHost mOHost;

		private readonly Timing mOTiming;

		private readonly IEnumerator<bool> mOSteps;

		/// <summary>The open animation file, null while the picture track pauses.</summary>
		private UWCutscene mOAnimation;

		/// <summary>The host's time since Start, in seconds.</summary>
		private double mdNow;

		private bool mbWaiting;

		private WaitKind meWait;

		/// <summary>Until when a Frame or HoldOrClick wait lasts.</summary>
		private double mdWaitUntil;

		/// <summary>FadeStep: how long the step must last, and how long it has.</summary>
		private float mfWaitSeconds;

		private float mfWaited;

		/// <summary>Brightness of the picture track, 0 dark to 1 bright - for the fades.</summary>
		private float mfBrightness = 1f;

		/// <summary>Did the running line start a speech recording? Then its subtitle is not
		/// drawn, and command 0x0E waits for the recording to end (see fExecute).</summary>
		private bool mbSpeechPlaying;

		/// <summary>Time at which the running recording ends - reference point for 0x0E.</summary>
		private double mdSpeechEndsAt;

		/// <summary>
		/// Set as soon as a mouse click happened; cleared as soon as the next line starts.
		///
		/// Both buttons count. It matters at the points where the sequence is WAITING
		/// for something - for the end of a spoken line or for a pause from the script. The
		/// rest of the sequence runs on as before; whoever wants to skip everything uses Escape.
		///
		/// The original's flag b1 since 2026-10-24: after a frame's wait it starts the jump to
		/// miSkipTarget (fStartJump), a hold uses it up, and the commands that set a target or a
		/// hold clear it (cutsop_skip, cutsop_pause, cutsop_wait, cutsop_loop), as does the
		/// start of a file.
		/// </summary>
		private bool mbAdvanceRequested;

		/// <summary>A click during a fade step. The original fades after the opcodes of the frame
		/// and polls the input only after the fade, so such a click meets the target or hold those
		/// opcodes set; ours fades while it runs the opcodes, so the click waits here until they
		/// are done (fPromoteFadeClick).</summary>
		private bool mbClickInFade;

		/// <summary>When the last click was accepted, or the running file started - the time
		/// the debounce counts from (show_anm's input_time).</summary>
		private double mdLastInput;

		/// <summary>The frame a click skips to (frame3B), NoFrame for none: set by opcode 4,
		/// by 0x0B and by a repeat (the frame after it).</summary>
		private int miSkipTarget = NoFrame;

		/// <summary>Opcode 4's hold without speech output: the frame it holds (frame3D) and for
		/// how long (frame3F, seconds).</summary>
		private int miHoldFrame = NoFrame;

		private float mfHoldSeconds;

		/// <summary>Are the frames running hidden towards miSkipTarget? The original's b0 cleared:
		/// no frame is shown or waited for, the holds and the skip commands are ignored, and a
		/// fade-in waits until the picture shows again (miPendingFadeIn); every other command runs.
		/// </summary>
		private bool mbHidden;

		/// <summary>The rate of a fade-in met while hidden, -1 for none.</summary>
		private int miPendingFadeIn = -1;

		/// <summary>Has the picture been faded in (and not out since)? Only then does a click
		/// jump: show_anm drops it while fade47 is -2 - before the first fade-in, after a fade-out,
		/// and always in the viewport cutscenes, whose fades the original ignores.</summary>
		private bool mbFadedIn;

		/// <summary>pOAnimation is the file the player opened before the start (the .N01, see
		/// GetFirstAnimationFile), or null if it is missing.</summary>
		public UWCutsceneRunner(int piCutscene, UWCutsceneScript pOScript, UWCutscene pOAnimation, UWStrings pOStrings,
			IHost pOHost, Timing pOTiming)
		{
			miCutscene = piCutscene;
			mOScript = pOScript;
			mOAnimation = pOAnimation;
			mOStrings = pOStrings;
			mOHost = pOHost;
			mOTiming = pOTiming ?? new Timing();
			mOSteps = fRun();
		}

		/// <summary>Has the script run to its end?</summary>
		public bool IsFinished { get; private set; }

		/// <summary>The host's time since Start, in seconds.</summary>
		public double Now => mdNow;

		/// <summary>A click: see mbAdvanceRequested. It counts only ClickDebouncePitTicks after
		/// the last one that counted or the start of the running file.</summary>
		public void RequestAdvance()
		{
			if (mdNow - mdLastInput <= ClickDebouncePitTicks / (double)UWPlayerTick.PitTicksPerSecond)
				return;

			mdLastInput = mdNow;

			if (mbWaiting && meWait == WaitKind.FadeStep)
				mbClickInFade = true;
			else
				mbAdvanceRequested = true;
		}

		/// <summary>Runs the script up to its first wait, without time passing - what starting
		/// the coroutine did. False if it ended there already.</summary>
		public bool Start()
		{
			return fAdvance();
		}

		/// <summary>Lets pfDeltaSeconds pass and runs everything that falls due. False once the
		/// script has ended.</summary>
		public bool Tick(float pfDeltaSeconds)
		{
			if (IsFinished)
				return false;

			mdNow += pfDeltaSeconds;

			if (mbWaiting && meWait == WaitKind.FadeStep)
				mfWaited += pfDeltaSeconds;

			return fAdvance();
		}

		private bool fAdvance()
		{
			while (true)
			{
				if (mbWaiting && fBlocks())
					return true;

				mbWaiting = false;

				if (!mOSteps.MoveNext())
				{
					IsFinished = true;

					return false;
				}

				mbWaiting = true;
			}
		}

		/// <summary>Does the current wait still hold? Asked the moment it is reached and after
		/// every Tick, as the coroutine asked its loop conditions each frame.</summary>
		private bool fBlocks()
		{
			switch (meWait)
			{
				case WaitKind.Frame:
					return mdNow < mdWaitUntil;

				case WaitKind.HoldOrClick:
					return mdNow < mdWaitUntil && !mbAdvanceRequested;

				case WaitKind.SpeechOrClick:
					return mdNow < mdSpeechEndsAt && !mbAdvanceRequested;

				default:
					return mfWaited < mfWaitSeconds;
			}
		}

		private void fWait(WaitKind peKind, float pfSeconds)
		{
			meWait = peKind;
			mdWaitUntil = mdNow + pfSeconds;
			mfWaitSeconds = pfSeconds;
			mfWaited = 0f;
		}

		private IEnumerator<bool> fRun()
		{
			// Start dark: the script's first command is a fade-in, which would otherwise
			// have no effect.
			// ONLY IF THE SCRIPT FADES IN AT ALL. The death cutscenes (CS402, CS403) have no fade-in
			// command - starting dark left them completely black (per user, 2026-09-14).
			mfBrightness = ScriptFadesIn(mOScript) ? 0f : 1f;
			mOHost.SetBrightness(mfBrightness);
			mbSpeechPlaying = false;

			List<UWCutsceneScript.Entry> lOSegment = new List<UWCutsceneScript.Entry>();
			List<UWCutsceneScript.Entry> lOAfter = new List<UWCutsceneScript.Entry>();

			int liAt = 0;
			int liExtension = FirstAnimationExtension;
			bool lbFirstSegment = true;
			bool lbFileChanged = false;

			while (liAt < mOScript.Entries.Count)
			{
				// AFTER EACH SEGMENT the next animation file - unless the segment has
				// opened one itself. That is how the intro gets from CS000.N01 via N02 to N03,
				// without the script having to say so every time.
				if (!lbFirstSegment && !lbFileChanged)
				{
					liExtension++;
					fOpenAnimation(UWCutscene.FileName(miCutscene, liExtension));
				}

				lbFirstSegment = false;
				lbFileChanged = false;

				// EVERY FILE STARTS WITHOUT A TARGET, A HOLD OR A CLICK, and the debounce counts from
				// its start (show_anm per LPF file: input_time = now, b1 and frame3D cleared). The
				// original keeps frame3B and b0 across files; ours ends a jump with its segment - the
				// debounce keeps a click from reaching the old target in the first frame anyway.
				if (mbHidden)
				{
					foreach (bool lbStep in fShowAgain())
						yield return lbStep;
				}

				mbAdvanceRequested = false;
				mbClickInFade = false;
				mdLastInput = mdNow;
				miSkipTarget = NoFrame;
				miHoldFrame = NoFrame;

				lOSegment.Clear();
				lOAfter.Clear();

				int liLength = 0;
				bool lbHasLength = false;
				bool lbEnd = false;

				// The segment extends to the next frame-set. ITS FRAME NUMBER IS THE
				// LENGTH, not a point in time - see class comment.
				while (liAt < mOScript.Entries.Count)
				{
					UWCutsceneScript.Entry lOEntry = mOScript.Entries[liAt++];

					if (lOEntry.Command == UWCutsceneScript.Command.SetStaticFrame)
					{
						liLength = lOEntry.Frame;
						lbHasLength = true;

						break;
					}

					if (lOEntry.Command == UWCutsceneScript.Command.EndCutscene)
					{
						// An end command without a preceding frame-set sets the length
						// itself - that is how the title screen keeps its palette animation going.
						if (!lbHasLength && lOEntry.Frame > 0)
						{
							liLength = lOEntry.Frame;
							lbHasLength = true;
						}

						lbEnd = true;

						break;
					}

					lOSegment.Add(lOEntry);
				}

				// Commands with frame number 999 belong AFTER the segment.
				while (liAt < mOScript.Entries.Count
					&& mOScript.Entries[liAt].Frame == AfterSegmentFrame)
					lOAfter.Add(mOScript.Entries[liAt++]);

				if (lbHasLength && liLength > 0)
				{
					float lfDelay = 1f / Math.Max(1, fGetFrameRate());

					// Does this segment speak or write? That decides what a click does - see
					// the frame loop below.
					bool lbHasLine = false;

					for (int liEntry = 0; liEntry < lOSegment.Count; liEntry++)
					{
						if (lOSegment[liEntry].Command == UWCutsceneScript.Command.ShowText
							|| lOSegment[liEntry].Command == UWCutsceneScript.Command.TextAndPlay)
							lbHasLine = true;
					}

					// Repeats left per repeat-segment entry of this segment (index -> count).
					Dictionary<int, int> lORepeats = new Dictionary<int, int>();

					// A jump ends the repeats for good (show_anm: repeat41 = 0).
					bool lbNoMoreRepeats = false;

					// UP TO AND INCLUDING the length: on this number sits the open-file
					// that fetches the file for the next segment. In the script it is placed
					// directly before the frame-set and carries the same frame number.
					for (int liFrame = 0; liFrame <= liLength; liFrame++)
					{
						// THE END OF A JUMP: at its target the picture shows again, before the
						// target's commands (show_anm: cur_frame == frame3B while not drawing).
						if (mbHidden && liFrame == miSkipTarget)
						{
							foreach (bool lbStep in fShowAgain())
								yield return lbStep;
						}

						bool lbRepeat = false;

						for (int liEntry = 0; liEntry < lOSegment.Count; liEntry++)
						{
							if (lOSegment[liEntry].Frame != liFrame)
								continue;

							// REPEAT-SEGMENT (0x07): plays the segment from its start up to this
							// frame again, argument times in total. The final death cutscene
							// (CS403) repeats its end - the skulls' eyes flash several times in the
							// original, once with us (per user, 2026-09-14). The reference leaves
							// the command unimplemented; the exact semantics are ASSUMED.
							// (UW.EXE's loop, cutsop_loop in UnderworldExhumed's show_anm, replays the
							// FILE from the frame after it; ours replays the segment from its start.)
							if (lOSegment[liEntry].Command == UWCutsceneScript.Command.RepeatSegment)
							{
								// cutsop_loop also makes the frame after it the skip target, ends a
								// hold and clears the click - once, when it is first met. Hidden, the
								// original replays the file unseen; ours does not replay.
								if (!lORepeats.ContainsKey(liEntry))
								{
									lORepeats[liEntry] = Math.Max(0, lOSegment[liEntry].Arguments[0] - 1);
									miSkipTarget = liFrame + 1;
									miHoldFrame = NoFrame;
									mbAdvanceRequested = false;
								}

								if (lORepeats[liEntry] > 0 && !mbHidden && !lbNoMoreRepeats)
								{
									lORepeats[liEntry]--;
									lbRepeat = true;
									break;
								}

								continue;
							}

							if (lOSegment[liEntry].Command == UWCutsceneScript.Command.OpenFile)
								lbFileChanged = true;

							foreach (bool lbStep in fExecute(lOSegment[liEntry]))
								yield return lbStep;
						}

						fPromoteFadeClick();

						if (lbRepeat)
						{
							mOHost.ShowFrame(liFrame);

							liFrame = -1;

							fWait(WaitKind.Frame, lfDelay);
							yield return true;

							if (fStartJump(0, lbHasLine))
								lbNoMoreRepeats = true;

							// A segment without a line that is not faded in - the final death's
							// skulls in the viewport: the click ends it (fStartJump left it).
							if (mbAdvanceRequested && !lbHasLine && !mbFadedIn)
							{
								mbAdvanceRequested = false;

								break;
							}

							continue;
						}

						// Opcode 4's hold without speech output, after the frame's commands as
						// show_anm's hold loop (cur_frame == frame3D).
						if (!mbHidden && liFrame == miHoldFrame)
						{
							fWait(WaitKind.HoldOrClick, mfHoldSeconds);
							yield return true;

							mbAdvanceRequested = false;
						}

						if (liFrame >= liLength)
							break;

						if (mbHidden)
							continue;

						mOHost.ShowFrame(liFrame);

						// A CLICK. In a segment WITH a line it skips the rest of it, and the
						// next line clears the flag (fStartLine). In a segment WITHOUT one -
						// the title, the anvil - nothing would ever clear it, so the frames
						// would race past with no wait at all and the picture would be gone
						// before it was seen (per user, 2026-09-22, about the title). There a
						// click means what the viewer means by it: away with it.
						//
						// SINCE 2026-10-24 (per user) the original's jump decides first (fStartJump
						// after the wait below); this rule stays for a segment without a line where
						// the original has no skip target - the anvil, the deaths, the title before
						// its repeat sets one. The race in segments with a line is gone.
						if (mbAdvanceRequested && !lbHasLine && (liFrame + 1 >= miSkipTarget || !mbFadedIn))
						{
							mbAdvanceRequested = false;

							break;
						}

						fWait(WaitKind.Frame, lfDelay);
						yield return true;

						if (fStartJump(liFrame + 1, lbHasLine))
							lbNoMoreRepeats = true;
					}
				}
				else
				{
					for (int liEntry = 0; liEntry < lOSegment.Count; liEntry++)
					{
						if (lOSegment[liEntry].Command == UWCutsceneScript.Command.OpenFile)
							lbFileChanged = true;

						foreach (bool lbStep in fExecute(lOSegment[liEntry]))
							yield return lbStep;
					}
				}

				for (int liEntry = 0; liEntry < lOAfter.Count; liEntry++)
				{
					foreach (bool lbStep in fExecute(lOAfter[liEntry]))
						yield return lbStep;
				}

				if (lbEnd)
					break;
			}
		}

		/// <summary>
		/// A single script command.
		///
		/// NOT INCLUDED: frame-set, which is the segment separator and is consumed in fRun, and
		/// repeat-segment, which fRun also handles itself. To-frame (0x04) used to set the playing
		/// time here; in the segment model the segment length does that, and the reference has no
		/// case for it at all - since 2026-10-24 it is the original's skip again (cutsop_skip).
		/// </summary>
		private IEnumerable<bool> fExecute(UWCutsceneScript.Entry pOEntry)
		{
			switch (pOEntry.Command)
			{
				case UWCutsceneScript.Command.OpenFile:
					fOpenAnimation(UWCutscene.FileName(pOEntry.Arguments[0], pOEntry.Arguments[1]));
					break;

				// While hidden the fade-in waits for the picture (show_anm applies fade45 only
				// while drawing).
				case UWCutsceneScript.Command.FadeIn:
					if (mbHidden)
					{
						miPendingFadeIn = pOEntry.Arguments[0];

						break;
					}

					foreach (bool lbStep in fFade(1f, pOEntry.Arguments[0]))
						yield return lbStep;

					mbFadedIn = true;

					break;

				case UWCutsceneScript.Command.FadeOut:
					foreach (bool lbStep in fFade(0f, pOEntry.Arguments[0]))
						yield return lbStep;

					mbFadedIn = false;

					break;

				// SKIP (0x04), UW.EXE cutsop_skip: a click from here on skips to frame argument 1
				// (fStartJump); without speech output the frame before it also holds for argument 2
				// seconds. In the dreams, the intro and the end sequence the target is mostly the
				// segment's end. Ignored while hidden, as the original ignores it while not drawing.
				case UWCutsceneScript.Command.ToFrame:
					if (mbHidden)
						break;

					mbAdvanceRequested = false;
					miSkipTarget = pOEntry.Arguments[0];

					if (!mOTiming.SpeechOutput)
					{
						miHoldFrame = pOEntry.Arguments[0] - 1;
						mfHoldSeconds = pOEntry.Arguments[1];
					}

					break;

				// JUMP (0x0B), UW.EXE cutsop_jump: runs on hidden to the frame before argument 1
				// at once, unless that is the next frame anyway - so the picture goes on with frame
				// argument 1. The intro has one (CS000.N13, frames 1 to 4), CS400 and CS401 begin
				// with one. Not interpreted until 2026-10-24.
				case UWCutsceneScript.Command.Unknown0B:
					if (pOEntry.Frame != pOEntry.Arguments[0] - 1)
					{
						miSkipTarget = pOEntry.Arguments[0] - 1;
						miHoldFrame = NoFrame;
						mbAdvanceRequested = false;
						mbHidden = true;
					}

					break;

				case UWCutsceneScript.Command.TextAndPlay:
					fStartLine(pOEntry.Arguments[0], pOEntry.Arguments[1], pOEntry.Arguments[2]);
					break;

				case UWCutsceneScript.Command.ShowText:
					fStartLine(pOEntry.Arguments[0], pOEntry.Arguments[1], -1);
					break;

				// The "Chime" sound in the intro (reference: cutsplayer, SoundEffectKlang).
				case UWCutsceneScript.Command.Chime:
					mOHost.PlayChime();
					break;

				// 0x0E HOLDS THE PICTURE at its frame. UW.EXE Cutscene_14_Wait_ovr105_5FA: with
				// speech output the hold lasts until the recording has ended (flag 0x80, cleared in
				// CutsceneDataProcessing_ovr105_869 once seg014_1DC5_1C78 reports the sound done) and
				// then argument 2 seconds more; without speech it lasts argument 1 seconds. The unit is
				// seconds: the loop compares (PIT timer - frame time) >> 8, and the timer counts 256 per
				// second (the frame delay is 0x100 / frame rate). In the intro the arguments are 3 1,
				// 5 1 and mostly 2 1.
				//
				// SINCE 2026-10-24 (cutsop_wait and show_anm's hold loop): ignored while hidden; it
				// clears an earlier click and replaces opcode 4's hold; a click ends the whole hold,
				// the recording's part and the seconds after it, and is used up by it. A click while
				// the recording plays counts - the deliberate deviation, see the class comment.
				case UWCutsceneScript.Command.Unknown0E:
					if (mbHidden)
						break;

					fStartHold();

					if (mbSpeechPlaying)
					{
						fWait(WaitKind.SpeechOrClick, 0f);
						yield return true;

						if (!mbAdvanceRequested)
						{
							fWait(WaitKind.HoldOrClick, pOEntry.Arguments[1]);
							yield return true;
						}
					}
					else
					{
						fWait(WaitKind.HoldOrClick, pOEntry.Arguments[0]);
						yield return true;
					}

					mbAdvanceRequested = false;

					break;

				// Pause holds the picture for argument 1 SECONDS, the same hold as 0x0E
				// (UW.EXE Cutscene_3_Pause_ovr105_5CD). Until 2026-09-14 this was half seconds.
				// Since 2026-10-24 handled as 0x0E above (cutsop_pause): ignored while hidden, a
				// click ends the hold and is used up by it.
				case UWCutsceneScript.Command.Pause:
					if (mbHidden)
						break;

					fStartHold();

					fWait(WaitKind.HoldOrClick, pOEntry.Arguments[0]);
					yield return true;

					mbAdvanceRequested = false;

					break;
			}
		}

		/// <summary>A pause or 0x0E begins its hold: the click from before it no longer counts and
		/// opcode 4's hold is replaced (cutsop_pause, cutsop_wait: b1 = 0, frame3D = this frame);
		/// a click during a fade of this frame does count, as the original polls it in the hold.
		/// </summary>
		private void fStartHold()
		{
			mbAdvanceRequested = false;
			miHoldFrame = NoFrame;
			fPromoteFadeClick();
		}

		/// <summary>See mbClickInFade.</summary>
		private void fPromoteFadeClick()
		{
			if (!mbClickInFade)
				return;

			mbClickInFade = false;
			mbAdvanceRequested = true;
		}

		/// <summary>
		/// After a frame's wait, with piNext the frame that comes next: a click with the skip
		/// target still ahead starts the hidden run to it (show_anm: cur_frame &lt; frame3B with b1
		/// set clears b0, frame3D, frame3F and repeat41) - or, with the picture not faded in, is
		/// dropped (fade47 == -2). True if the run started; with no target ahead the click stays.
		///
		/// DEVIATION (per user, 2026-10-24): in a segment WITHOUT a line the dropped click is kept
		/// instead, so it ends the segment as a click does where the original has no target - the
		/// final death's skull repeat in the viewport, never faded in, ended on a click before the
		/// original's rules came in and does so again (Docs/DEVIATIONS.md).
		/// </summary>
		private bool fStartJump(int piNext, bool pbHasLine)
		{
			if (!mbAdvanceRequested || piNext >= miSkipTarget)
				return false;

			if (!mbFadedIn)
			{
				if (pbHasLine)
					mbAdvanceRequested = false;

				return false;
			}

			mbAdvanceRequested = false;

			mbHidden = true;
			miHoldFrame = NoFrame;

			return true;
		}

		/// <summary>The hidden run has reached its target (or its file ended): the picture shows
		/// again, target, hold and click are cleared, and a fade-in met on the way runs now.
		/// </summary>
		private IEnumerable<bool> fShowAgain()
		{
			mbHidden = false;
			miSkipTarget = NoFrame;
			miHoldFrame = NoFrame;
			mbAdvanceRequested = false;

			if (miPendingFadeIn < 0)
				yield break;

			int liRate = miPendingFadeIn;

			miPendingFadeIn = -1;

			foreach (bool lbStep in fFade(1f, liRate))
				yield return lbStep;

			mbFadedIn = true;
		}

		// UNTIL 2026-09-07 THREE HELPER METHODS STOOD HERE: fWaitForPreviousLine, fPlaySegment
		// and fAdvanceTo. They belonged to a different playback model - there speech set
		// the timing, and the picture track ran alongside. The segment model in fRun no longer
		// needs them: the segment's frame counter drives both.

		/// <summary>The frame rate of the running file; it is in its header. Only if nothing
		/// usable is there does the fallback value from the Inspector apply.</summary>
		private int fGetFrameRate()
		{
			return mOAnimation != null && mOAnimation.FrameRate > 0
				? mOAnimation.FrameRate
				: mOTiming.FallbackFrameRate;
		}

		/// <summary>
		/// Fades the picture track to the target brightness. READ 2026-09-25 (the fade commands only
		/// store the rate; CutsceneDataProcessing hands it to the palette fades ovr114_2B9 in and
		/// ovr114_1B3 out): rate 0 switches at once; otherwise the palette runs through rate * 8
		/// equal steps, and each waits for at least 8 PIT ticks (256 a second) before it is set -
		/// so a fade takes rate / 4 seconds, in steps of 1/32 second, while the script stands still.
		/// Fading in shows 1/n to n/n of the palette, fading out (n-1)/n down to 0. Until then ours
		/// took 1 / rate seconds, smoothly - a rate-4 fade was four times too fast.
		/// </summary>
		private IEnumerable<bool> fFade(float pfTarget, int piRate)
		{
			if (piRate <= 0)
			{
				mfBrightness = pfTarget;
				mOHost.SetBrightness(mfBrightness);
				yield break;
			}

			int liSteps = piRate * FadeStepsPerRate;
			float lfStart = mfBrightness;
			float lfStepSeconds = FadeStepPitTicks / (float)UWPlayerTick.PitTicksPerSecond;

			for (int liStep = 1; liStep <= liSteps; liStep++)
			{
				fWait(WaitKind.FadeStep, lfStepSeconds);
				yield return true;

				// Mathf.Lerp until 2026-10-24: a + (b - a) * t, t within 0 and 1.
				mfBrightness = lfStart + ((pfTarget - lfStart) * (liStep / (float)liSteps));
				mOHost.SetBrightness(mfBrightness);
			}
		}

		private void fOpenAnimation(string psFileName)
		{
			mOAnimation = mOHost.OpenAnimation(psFileName);
		}

		/// <summary>Clears the click flag: a new line is running, from here only the
		/// NEXT click counts.
		///
		/// THIS BELONGS IN fStartLine and not in the individual script branches. At first it was
		/// only in TextAndPlay - ShowText did not clear, the flag stayed set, and a
		/// single click then tore through the whole sequence (per user, 2026-09-07). Both
		/// commands start a line, so it applies to both.</summary>
		private void fStartLine(int piColour, int piStringIndex, int piVoiceNumber)
		{
			mbAdvanceRequested = false;

			string lsText = fGetLine(piStringIndex);

			mbSpeechPlaying = false;

			// Lines without a recording get a reading time based on text length, so they too are not
			// immediately overwritten by the next one.
			float lfDuration = Math.Max(mOTiming.MinimumLineDuration,
				(lsText == null ? 0 : lsText.Length) * mOTiming.SecondsPerCharacter);

			if (piVoiceNumber >= 0 && mOTiming.SpeechOutput)
			{
				float lfLength = mOHost.StartSpeech(piVoiceNumber);

				if (lfLength >= 0f)
				{
					lfDuration = lfLength;
					mbSpeechPlaying = true;
				}
			}

			// NO SUBTITLE WHILE SPEECH PLAYS. UW.EXE Cutscene_13_TextAndSound_ovr105_719 only draws
			// the text when speech output is off; with speech it just queues the recording. In the
			// original intro no subtitles appear (per user, 2026-09-14). Show-text (0x00), as in the
			// end sequence, and a line whose recording is missing still show their text.
			//
			// The subtitle disappears with the end of the recording, not only when the
			// next line replaces it (the host holds it for pfSeconds and a little more).
			mOHost.ShowLine(piStringIndex, mbSpeechPlaying ? null : lsText, piColour, lfDuration);

			mdSpeechEndsAt = mdNow + lfDuration;
		}

		private string fGetLine(int piStringIndex)
		{
			if (piStringIndex == UWCutsceneScript.NoText)
				return null;

			// The usual project +1: our parsed string blocks are one entry
			// above the game numbering (as with GetObjectDescription or the partner names
			// of the conversations). Without it the subtitle lags one line behind the
			// sound - checked by user in the original, 2026-08-29: with the sound "At last, you
			// are asleep." entry 0 was wrongly shown.
			//
			// Evidence in the data itself: entry 0 is a truncated fragment
			// ("s brought you starting..."), whose complete version is
			// entry 2. A clean block does not start mid-sentence.
			string lsRaw = UWText.Block(mOStrings, GetSpeechBlock(miCutscene), piStringIndex + 1, null);

			return lsRaw == null ? null : UWTextLayout.CleanText(lsRaw);
		}
	}
}
