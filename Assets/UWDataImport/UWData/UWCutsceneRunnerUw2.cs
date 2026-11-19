using System;
using System.Collections.Generic;
using UWDataImport.Audio;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE SECOND GAME'S CUTSCENE PLAYER, engine-free (UW2 plan phase 5 part C): uw2 gfx/CUTS.C's show_anm,
	/// cuts_process_anm, cuts_process_lp, cuts_process_opcodes and the cutsop_ handlers, over an indexed screen
	/// (UWCutsceneScreenUw2). The first game's UWCutsceneRunner and its segment model stay as they are: the second game
	/// plays a file's records to its end unless next or end comes, runs frame 997 before a file opens and 999 after it,
	/// replays a file with its script pointer where it is, fades its palette with timed tasks and cycles its colours.
	///
	/// THE STATE is the original's CutsState: its flag bits (b0 skipping, b1 input, b2 keep this file, b3 keep the
	/// cutscene, b4 Escape ends it, b5 speech available, b6 speech playing, b7 the wait ignores keys), the subtitle,
	/// the targets of skip and pause (frame3B, frame3D, frame3F), the loop (repeat41, repeat43), speech and fades (fade45,
	/// fade47, fade49), the files (file4B, file4D), the virtual screen flag (vscr4F) and the pan.
	///
	/// THE CLOCK is GAME_TIME, 256 ticks a second, from the host's seconds (Tick). The original's busy loops are waits
	/// that yield to the host: a frame lasts 0x100 / rate ticks from the last one's end, the fades step every 8 ticks,
	/// the palette tasks every 8, a colour cycle every 0x38E / rate. Input reaches the player where the original polls it
	/// (gobble_input_events).
	///
	/// DEVIATIONS: data with a file other than 996 takes its argument (the original leaves the digits to whatever the
	/// stack held; CS040 asks for 40); palsimplefade starts at entry 0 (the original at 1 while Transparency is set, a
	/// flag of the rune and barter screens); the colour cycles start counting when their file opens (the original from
	/// the header's word).
	/// </summary>
	public sealed class UWCutsceneRunnerUw2
	{
		/// <summary>What the player needs from outside.</summary>
		public interface IHost
		{
			/// <summary>An LPF file of the CUTS folder, null when missing.</summary>
			UWCutscene OpenAnimation(string psFileName);

			/// <summary>CUTS\LBACKnnn.BYT, 320 by 200 indices, null when missing.</summary>
			byte[] LoadBack(int piNumber);

			/// <summary>Palette piNumber of PALS.DAT, 768 bytes of 6 bits, null when missing (read_quikpal).</summary>
			byte[] LoadQuickPalette(int piNumber);

			/// <summary>Starts SOUND\BSPnn.VOC; false when it cannot (big_speech_play's 0xFF).</summary>
			bool StartSpeech(int piVoice);

			bool IsSpeechPlaying { get; }

			/// <summary>cutoff_cutscene_speech.</summary>
			void StopSpeech();

			/// <summary>cutsop_music: set_new_music(theme), 0 stop_music.</summary>
			void Music(int piTheme);

			/// <summary>cuts_draw_text: the subtitle's lines into the screen (the bar already cleared), centred, the first
			/// at the top, in palette index piColour; the host splits psText at newlines and to lines of 320 pixels.</summary>
			void DrawText(UWCutsceneScreenUw2 pOScreen, string psText, int piColour);
		}

		public const double TicksPerSecond = 256.0;

		/// <summary>The frames that run before a file opens and after its last record.</summary>
		public const int BeforeFileFrame = 997;

		public const int AfterFileFrame = 999;

		/// <summary>data's file that draws one of CS034 to CS037 (rand() % 4 + 0x1C).</summary>
		public const int RandomPictureFile = 996;

		public const int FirstRandomPicture = 0x1C;

		/// <summary>say's voices that are not one: text only, nothing.</summary>
		public const int TextOnlyVoice = 999;

		public const int NoVoice = 998;

		/// <summary>The cutscene whose Escape does not end it (gobble_input_events: the ending).</summary>
		public const int EndingCutscene = 2;

		/// <summary>cuts_perform_fadein's and cuts_draw_text's bar colour, cuts_do_fadein's.</summary>
		public const byte BarColour = 0xF0;

		public const byte FadeBarColour = 0x1B;

		public const int TaskCount = 16;

		/// <summary>The steps of a timed task and of a fade: 8 ticks.</summary>
		public const int TaskStepTicks = 8;

		/// <summary>anm_cycle's 0x38E divided by a cycle's rate: its interval in ticks.</summary>
		public const int CycleScale = 0x38E;

		private readonly int miCutscene;

		private readonly UWCutsceneScript mOScript;

		private readonly UWStrings mOStrings;

		private readonly IHost mOHost;

		private readonly IEnumerator<bool> mOSteps;

		private readonly bool mbWindowed;

		private readonly int miWindowX;

		private readonly int miWindowTop;

		private readonly int miWindowWidth;

		private readonly int miWindowHeight;

		/// <summary>The script pointer (STDAT.code).</summary>
		private int miCode;

		private double mdTicks;

		// ------------------------------------------------- CutsState

		private bool mbB0Skipping, mbB1Input, mbB2File = true, mbB3Cutscene = true, mbB4Escape = true, mbB5Speech, mbB6Speaking, mbB7IgnoreKeys;

		private int miTextLines;

		private int miTextColour;

		private string msText;

		private int miFrame3B, miFrame3D, miFrame3F;

		private int miRepeat41, miRepeat43;

		private int miFade45 = -1, miFade47 = -1, miFade49 = -2;

		private int miFile4B, miFile4D;

		private bool mbVscr4F;

		private int miPanX, miPanY, miPanDir, miPanStep, miPanRemaining;

		/// <summary>focus_x and focus_y as the original keeps them (the pan starts from them).</summary>
		private int miFocusX, miFocusY = UWCutsceneScreenUw2.ScreenHeight - 1;

		/// <summary>The name's cutscene and page (st-&gt;name, which data may change before a file opens).</summary>
		private int miNameFile, miNameExt;

		/// <summary>st-&gt;palette: the working palette, 6-bit.</summary>
		private readonly byte[] myPalette = new byte[UWCutsceneScreenUw2.PaletteBytes];

		private UWCutscene mOFile;

		private int miFrame;

		private double mdFrameStart;

		private double mdLastTasks;

		private int miPendingInput;

		private bool mbPendingEscape;

		// ------------------------------------------------- The timed tasks (palette fades)

		private readonly bool[] mbTask = new bool[TaskCount];

		private readonly int[] miTaskSoFar = new int[TaskCount], miTaskResidue = new int[TaskCount], miTaskPeriod = new int[TaskCount],
			miTaskTotal = new int[TaskCount], miTaskFirst = new int[TaskCount], miTaskLast = new int[TaskCount];

		private double mdTaskClock;

		private int miTaskClockResidue;

		/// <summary>start_pal and end_pal, shared by every task as in the original.</summary>
		private readonly byte[] myStartPal = new byte[UWCutsceneScreenUw2.PaletteBytes], myEndPal = new byte[UWCutsceneScreenUw2.PaletteBytes];

		private UWCutscene.ColourCycle[] mOCycles = new UWCutscene.ColourCycle[0];

		private double[] mdCycleStarted = new double[0];

		/// <param name="pyStartPalette">The palette shown when it starts (st.palette's copy of the DAC), 6-bit.</param>
		/// <param name="pbSpeech">speech_available: say starts its recording (else its text).</param>
		/// <param name="pbSubtitlesWithSpeech">The port's subtitles beside a recording (CutsceneSubtitles).</param>
		public UWCutsceneRunnerUw2(int piCutscene, UWCutsceneScript pOScript, UWStrings pOStrings, IHost pOHost, byte[] pyStartPalette,
			bool pbSpeech, bool pbSubtitlesWithSpeech)
		{
			miCutscene = piCutscene;
			mOScript = pOScript;
			mOStrings = pOStrings;
			mOHost = pOHost;
			mbB5Speech = pbSpeech;
			SubtitlesWithSpeech = pbSubtitlesWithSpeech;

			// show_cutscene's window: the screen below 0x100, the 3D view's from it (0x103 a few rows taller).
			if (piCutscene < UWCutsceneRunner.FirstViewportCutscene)
			{
				miWindowX = 0;
				miWindowTop = 0;
				miWindowWidth = UWCutsceneScreenUw2.ScreenWidth;
				miWindowHeight = UWCutsceneScreenUw2.ScreenHeight;
			}
			else
			{
				miWindowX = 0x10;
				miWindowTop = UWCutsceneScreenUw2.ScreenHeight - 1 - 0xB6;
				miWindowWidth = 0xD0;
				miWindowHeight = 0x7F;

				if (piCutscene == FinalDeathCutscene)
				{
					miWindowTop -= 4;
					miWindowHeight += 5;
				}
			}

			mbWindowed = miWindowX != 0 || miWindowTop != 0 || miWindowWidth != UWCutsceneScreenUw2.ScreenWidth
				|| miWindowHeight != UWCutsceneScreenUw2.ScreenHeight;

			if (pyStartPalette != null)
				Array.Copy(pyStartPalette, myPalette, Math.Min(pyStartPalette.Length, myPalette.Length));

			Screen.SetPalette(myPalette);
			mOSteps = fShowAnm();
		}

		/// <summary>The final death's cutscene, whose window is taller (show_cutscene).</summary>
		public const int FinalDeathCutscene = 0x103;

		/// <summary>The window it plays in, top-down (show_cutscene's x, y, w, h): the screen, or the 3D view's.</summary>
		public bool IsWindowed => mbWindowed;

		public int WindowX => miWindowX;

		public int WindowTop => miWindowTop;

		public int WindowWidth => miWindowWidth;

		public int WindowHeight => miWindowHeight;

		public UWCutsceneScreenUw2 Screen { get; } = new UWCutsceneScreenUw2();

		public bool SubtitlesWithSpeech { get; set; }

		public bool IsFinished { get; private set; }

		/// <summary>The time since Start, in ticks.</summary>
		public double Ticks => mdTicks;

		/// <summary>The files opened, in order (for the checks).</summary>
		public List<string> FilesOpened { get; } = new List<string>();

		/// <summary>The records drawn (for the checks).</summary>
		public int RecordsDrawn { get; private set; }

		/// <summary>A key or a mouse button (mouse_get_input): taken where the original polls it.</summary>
		public void RequestInput()
		{
			miPendingInput++;
		}

		/// <summary>Escape: ends the cutscene where b4 allows it (not the ending), cuts off the speech, and counts as input.</summary>
		public void RequestEscape()
		{
			mbPendingEscape = true;
		}

		public bool Start()
		{
			return fAdvance();
		}

		/// <summary>Lets pfDeltaSeconds pass and runs what falls due; false once it has ended.</summary>
		public bool Tick(float pfDeltaSeconds)
		{
			if (IsFinished)
				return false;

			mdTicks += pfDeltaSeconds * TicksPerSecond;

			return fAdvance();
		}

		private bool fAdvance()
		{
			if (mOSteps.MoveNext())
				return true;

			IsFinished = true;

			return false;
		}

		// ------------------------------------------------- show_anm

		private IEnumerator<bool> fShowAnm()
		{
			mbPendingEscape = false;
			miPendingInput = 0;
			fFocus(0, UWCutsceneScreenUw2.ScreenHeight - 1);

			if (!mbWindowed)
				Screen.Clear();

			// cuts_init_info
			if (mbWindowed)
				miFade49 = -1;

			if (!mbWindowed)
			{
				foreach (bool lbStep in fFadeOut(myPalette, 2))
					yield return lbStep;
			}

			miFile4B = miFile4D = 1;

			while (mbB3Cutscene)
			{
				miFile4D = miFile4B;
				miNameFile = miCutscene;
				miNameExt = miFile4B;
				miFile4B++;

				foreach (bool lbStep in fProcessAnm())
					yield return lbStep;

				fPuntTasks();
			}

			if (miFade45 != -1)
				mOHost.StopSpeech();

			if (!mbWindowed)
			{
				if (miFade49 != -2)
				{
					foreach (bool lbStep in fFadeOut(myPalette, 2))
						yield return lbStep;
				}

				Screen.Clear();
				Screen.SetVirtualScreen(UWCutsceneScreenUw2.ScreenWidth, UWCutsceneScreenUw2.ScreenHeight, UWCutsceneScreenUw2.NoSplit);
				fFocus(0, UWCutsceneScreenUw2.ScreenHeight - 1);
			}
		}

		// ------------------------------------------------- cuts_process_anm

		private IEnumerable<bool> fProcessAnm()
		{
			mdTaskClock = -1;

			foreach (bool lbStep in fProcessOpcodes(BeforeFileFrame))
				yield return lbStep;

			string lsName = UWCutscene.FileName(miNameFile, miNameExt);

			mOFile = mOHost.OpenAnimation(lsName);

			if (mOFile == null || mOFile.PageCount <= 0)
			{
				mbB3Cutscene = false;

				yield break;
			}

			FilesOpened.Add(lsName);

			if (!mbWindowed)
			{
				for (int liAt = 0; liAt < UWCutsceneScreenUw2.PaletteBytes; liAt++)
					myPalette[liAt] = (byte)(mOFile.PaletteRgb[liAt] >> 2);
			}

			mOCycles = mOFile.Cycles;
			mdCycleStarted = new double[mOCycles.Length];

			for (int liAt = 0; liAt < mdCycleStarted.Length; liAt++)
				mdCycleStarted[liAt] = mdTicks;

			miFrame = 0;
			mbB1Input = false;
			mbB2File = true;

			if (miRepeat41 == 0)
			{
				foreach (bool lbStep in fProcessOpcodes(miFrame))
					yield return lbStep;
			}

			// The large pages in the order of their first record (build_lptab).
			List<int> lOPages = new List<int>();

			for (int liPage = 0; liPage < mOFile.PageCount; liPage++)
				lOPages.Add(liPage);

			lOPages.Sort((a, b) => mOFile.PageFirstRecord(a).CompareTo(mOFile.PageFirstRecord(b)));

			bool lbLooped = false;

			for (int liAt = 0; liAt < lOPages.Count && mbB2File && mbB3Cutscene && !lbLooped; liAt++)
			{
				int liPage = lOPages[liAt];
				int liRecords = mOFile.PageRecords(liPage) - (liAt == lOPages.Count - 1 && mOFile.HasLastDelta ? 1 : 0);
				int liFirst = mOFile.PageFirstRecord(liPage);

				// cuts_process_lp's start of a page.
				mdFrameStart = mdTicks;
				miFrame3D = miFrame3F = 0;
				mbB1Input = false;

				for (int liRecord = 0; liRecord < liRecords && mbB2File && mbB3Cutscene; liRecord++)
				{
					foreach (bool lbStep in fProcessRecord(liFirst + liRecord))
						yield return lbStep;

					if (mbLoopNow)
					{
						mbLoopNow = false;
						lbLooped = true;

						break;
					}
				}
			}

			if (miRepeat41 == 0)
			{
				// reset_inf_values_eof
				mbB0Skipping = mbB1Input = mbB6Speaking = false;
				mbB4Escape = mbB2File = true;
				miTextLines = miFrame3B = miFrame3D = miPanRemaining = miFrame3F = 0;
				mbB7IgnoreKeys = false;

				foreach (bool lbStep in fProcessOpcodes(AfterFileFrame))
					yield return lbStep;

				if (miFade49 == -2)
					Screen.Clear();
			}
		}

		/// <summary>A loop's replay is due (cuts_process_lp returns 1).</summary>
		private bool mbLoopNow;

		// ------------------------------------------------- cuts_process_lp, one record

		private IEnumerable<bool> fProcessRecord(int piRecord)
		{
			int liSize = mOFile.RecordSize(piRecord);
			bool lbFirstPicture = !mbVscr4F && miFrame == 0 && miFade49 > -2 && !mbWindowed;

			if (lbFirstPicture)
				Screen.Clear();

			if (!mbB0Skipping && miPanRemaining != 0)
				fDoPan();

			if (liSize > 4 && !mbB0Skipping)
			{
				if (mOFile.DrawRecord(piRecord, Screen.PictureRows, fPutRecordPixel))
					RecordsDrawn++;
			}

			if (lbFirstPicture)
				Screen.SetPalette(myPalette);

			miFrame++;
			fTimeCritical();
			fGobble();

			if (miRepeat41 == 0)
			{
				foreach (bool lbStep in fProcessOpcodes(miFrame))
					yield return lbStep;
			}

			if (!mbB0Skipping)
			{
				double ldDuration = 0x100 / Math.Max(1, mOFile.FrameRate);

				while (!mbB0Skipping && mdTicks - mdFrameStart < ldDuration)
				{
					yield return true;

					fTimeCritical();
					fGobble();
				}
			}

			if (!mbB0Skipping && miTextLines > 0)
			{
				fDrawText();
				miTextLines = 0;
			}

			if (!mbB0Skipping && miFrame3F != 0 && miFrame3D == miFrame)
			{
				foreach (bool lbStep in fRunPause())
					yield return lbStep;
			}

			if (miFrame3B == miFrame)
			{
				miFrame3B = 0;
				mbB0Skipping = false;
			}

			if (miRepeat41 > 0 && miRepeat43 == miFrame)
			{
				miRepeat41--;
				miFile4B = miFile4D;
				mbLoopNow = true;
			}

			mdFrameStart = mdTicks;
		}

		/// <summary>A record's pixel at the window's place (st-&gt;x + (focus_x &amp; 3), st-&gt;y: the view's first pixel). A
		/// window leaves out the frame's first 320 - w columns and 200 - h rows (show's and draw_rsd's skipx and skipy):
		/// the viewport cutscenes are painted at the frame's bottom right (CS403, rendered 2026-11-17).</summary>
		private void fPutRecordPixel(int piX, int piY, byte pyPixel)
		{
			int liX = piX - (UWCutsceneScreenUw2.ScreenWidth - miWindowWidth);
			int liY = piY - (UWCutsceneScreenUw2.ScreenHeight - miWindowHeight);

			if (liX < 0 || liY < 0)
				return;

			Screen.Put(miWindowX + liX, miWindowTop + liY, pyPixel);
		}

		/// <summary>do_pan: the focus step pixels a frame further in the pan's direction.</summary>
		private void fDoPan()
		{
			int liDx = miPanDir == 1 ? 1 : miPanDir == 3 ? -1 : 0;
			int liDy = miPanDir == 0 ? 1 : miPanDir == 2 ? -1 : 0;

			fFocus(miPanX + ((miFrame + 1) * miPanStep * liDx), miPanY + ((miFrame + 1) * miPanStep * liDy));
			miPanRemaining--;
		}

		private void fFocus(int piX, int piY)
		{
			miFocusX = piX;
			miFocusY = piY;
			Screen.Focus(piX, piY);
		}

		/// <summary>cuts_run_pause: the frame holds frame3F * 256 ticks from its start, until input (keys too while b7, which
		/// only keeps them from the state) or b1.</summary>
		private IEnumerable<bool> fRunPause()
		{
			bool lbInput = false;

			while (!lbInput && !mbB1Input && (int)(mdTicks - mdFrameStart) >> 8 < miFrame3F)
			{
				yield return true;

				lbInput = mbB7IgnoreKeys ? fTakeInputQuietly() : fGobble();
				fTimeCritical();
			}
		}

		// ------------------------------------------------- run_time_critical_things, gobble_input_events

		private void fTimeCritical()
		{
			// anm_cycle
			for (int liAt = 0; liAt < mOCycles.Length; liAt++)
			{
				if (mOCycles[liAt].Rate == 0 || mdTicks - mdCycleStarted[liAt] < CycleScale / mOCycles[liAt].Rate)
					continue;

				Screen.RotateDown(mOCycles[liAt].First, mOCycles[liAt].Last - mOCycles[liAt].First + 1);
				mdCycleStarted[liAt] = mdTicks;
			}

			// anm_sound: the music is the host's (change_music_maybe); the speech's end.
			if (mbB6Speaking && miFade45 != -1 && !mOHost.IsSpeechPlaying)
			{
				mbB6Speaking = false;
				miFade45 = -1;
				mOSpokenLines.Clear();
			}

			if (mOSpokenLines.Count > 0)
				fShowDueSpokenLines();

			if (mdTicks - mdLastTasks >= TaskStepTicks)
			{
				mdLastTasks = mdTicks;
				fRunTasks();
			}
		}

		/// <summary>gobble_input_events(st): any input sets b1, Escape ends the cutscene where allowed and cuts off the
		/// speech, input with a skip target starts skipping. True when there was input.</summary>
		private bool fGobble()
		{
			bool lbEscape = mbPendingEscape;
			bool lbInput = lbEscape || miPendingInput > 0;

			mbPendingEscape = false;
			miPendingInput = 0;

			if (lbEscape)
			{
				if (mbB4Escape && miCutscene != EndingCutscene)
					mbB3Cutscene = mbB2File = false;

				mOHost.StopSpeech();
			}

			if (lbInput)
				mbB1Input = true;

			if (!mbB0Skipping && miFrame3B != 0 && mbB1Input)
			{
				mbB0Skipping = true;
				miRepeat41 = 0;
			}

			return lbInput;
		}

		/// <summary>gobble_input_events(0): the input taken, nothing set.</summary>
		private bool fTakeInputQuietly()
		{
			bool lbInput = mbPendingEscape || miPendingInput > 0;

			mbPendingEscape = false;
			miPendingInput = 0;

			return lbInput;
		}

		// ------------------------------------------------- cuts_process_opcodes and the handlers

		private IEnumerable<bool> fProcessOpcodes(int piFrame)
		{
			while (miCode < mOScript.Entries.Count && mOScript.Entries[miCode].Frame == piFrame && miRepeat41 == 0)
			{
				UWCutsceneScript.Entry lOEntry = mOScript.Entries[miCode++];

				foreach (bool lbStep in fExecute(lOEntry))
					yield return lbStep;
			}
		}

		private IEnumerable<bool> fExecute(UWCutsceneScript.Entry pOEntry)
		{
			int[] liArg = pOEntry.Arguments;

			switch (pOEntry.Command)
			{
				case UWCutsceneScript.Command.ShowText:
					fTxt(liArg[0], liArg[1]);
					break;

				case UWCutsceneScript.Command.SetFlag:
					miTextLines = 0;
					break;

				case UWCutsceneScript.Command.Pause:
					if (mbB0Skipping)
						break;

					mbB1Input = false;
					miFrame3D = pOEntry.Frame;
					miFrame3F = liArg[0];
					break;

				case UWCutsceneScript.Command.ToFrame:
					if (mbB0Skipping)
						break;

					mbB1Input = false;
					miFrame3B = liArg[0];

					if (!mbB5Speech)
					{
						miFrame3F = liArg[1];
						miFrame3D = liArg[0] - 1;
					}

					break;

				case UWCutsceneScript.Command.SetStaticFrame:
					mbB2File = false;
					break;

				case UWCutsceneScript.Command.EndCutscene:
					mbB2File = false;
					mbB3Cutscene = false;
					break;

				case UWCutsceneScript.Command.RepeatSegment:
					miRepeat41 = liArg[0];
					miRepeat43 = pOEntry.Frame;
					miFrame3B = pOEntry.Frame + 1;
					miFrame3D = 0;
					mbB1Input = false;
					break;

				case UWCutsceneScript.Command.OpenFile:
					miNameFile = liArg[0] == RandomPictureFile ? FirstRandomPicture + UWRandom.Next(4) : liArg[0];
					miNameExt = liArg[1];
					miFile4B = liArg[1];
					break;

				case UWCutsceneScript.Command.FadeOut:
					if (!mbWindowed && miFade49 > -2)
						miFade49 = liArg[0];

					if (!mbB0Skipping && miFade49 > -1)
					{
						foreach (bool lbStep in fPerformFadeOut())
							yield return lbStep;
					}

					break;

				case UWCutsceneScript.Command.FadeIn:
					if (!mbWindowed && miFade47 > -2)
						miFade47 = liArg[0];

					// cuts_do_fadein
					Screen.FillBar(FadeBarColour);

					if (!mbB0Skipping)
					{
						if (miFade47 > -1)
						{
							foreach (bool lbStep in fPerformFadeIn())
								yield return lbStep;
						}
						else if (miFrame == 0)
							Screen.SetPalette(myPalette);
					}

					break;

				case UWCutsceneScript.Command.Unknown0B:
					if (pOEntry.Frame != liArg[0] - 1)
					{
						miFrame3B = liArg[0] - 1;
						miFrame3D = 0;
						miFrame3F = 0;
						mbB1Input = true;
					}

					break;

				case UWCutsceneScript.Command.Unknown0C:
					mbB4Escape = liArg[0] != 0;
					break;

				case UWCutsceneScript.Command.TextAndPlay:
					fSay(liArg[0], liArg[1], liArg[2]);
					break;

				case UWCutsceneScript.Command.Unknown0E:
					if (mbB0Skipping)
						break;

					mbB1Input = false;
					miFrame3D = pOEntry.Frame;

					if (mbB5Speech)
					{
						mbB7IgnoreKeys = true;
						miFrame3F = liArg[1];
					}
					else
						miFrame3F = liArg[0];

					break;

				case UWCutsceneScript.Command.PaletteRange:
				{
					int liFirst = liArg[0] * 3;
					int liEnd = liFirst + (liArg[1] * 3);

					for (int liAt = liFirst; liAt < liEnd && liAt < myPalette.Length; liAt++)
						myPalette[liAt] = (byte)liArg[liAt - liFirst + 2];

					break;
				}

				case UWCutsceneScript.Command.PaletteFade:
				{
					int liTask = fInstallTask(liArg[0], liArg[1]);

					miTaskFirst[liTask] = liArg[2];
					miTaskLast[liTask] = liArg[2] + liArg[3];

					int liFirst = liArg[2] * 3;
					int liEnd = liFirst + (liArg[3] * 3);

					for (int liAt = liFirst; liAt < liEnd && liAt < myPalette.Length; liAt++)
					{
						myStartPal[liAt] = myPalette[liAt];
						myEndPal[liAt] = (byte)liArg[liAt - liFirst + 4];
					}

					break;
				}

				case UWCutsceneScript.Command.PaletteSimpleFade:
				{
					int liTask = fInstallTask(liArg[1], liArg[2]);
					byte[] lyEnd = mOHost.LoadQuickPalette(liArg[0]);

					miTaskFirst[liTask] = 0;
					miTaskLast[liTask] = 0x100;

					if (lyEnd == null)
						mbTask[liTask] = false;
					else
					{
						Array.Copy(lyEnd, myEndPal, Math.Min(lyEnd.Length, myEndPal.Length));
						Array.Copy(myPalette, myStartPal, myPalette.Length);
					}

					break;
				}

				case UWCutsceneScript.Command.VirtualScreen:
					Screen.SetVirtualScreen(liArg[0], liArg[1], liArg[2] == 999 ? UWCutsceneScreenUw2.NoSplit : liArg[2] - 1);

					if (liArg[0] == UWCutsceneScreenUw2.ScreenWidth && liArg[1] == UWCutsceneScreenUw2.ScreenHeight && liArg[2] == 999)
					{
						if (!mbWindowed)
							mbVscr4F = false;
					}
					else
						mbVscr4F = true;

					Screen.Focus(miFocusX, miFocusY);
					break;

				case UWCutsceneScript.Command.Focus:
					fFocus(liArg[0], liArg[1]);
					break;

				case UWCutsceneScript.Command.Back:
					Screen.DrawBack(liArg[0], liArg[1], mOHost.LoadBack(liArg[2]));
					break;

				case UWCutsceneScript.Command.Pan:
					miPanX = miFocusX;
					miPanY = miFocusY;
					miPanDir = liArg[0];
					miPanStep = liArg[1];
					miPanRemaining = liArg[2];
					break;

				case UWCutsceneScript.Command.SplitY:
					Screen.SplitY = liArg[0] == 999 ? UWCutsceneScreenUw2.NoSplit : liArg[0] - 1;
					break;

				case UWCutsceneScript.Command.Music:
					mOHost.Music(liArg[0] & 0xFF);
					break;

				case UWCutsceneScript.Command.WaitForSound:
				{
					double ldUntil = mdTicks + (liArg[0] << 8);

					while (mOHost.IsSpeechPlaying)
					{
						if (mdTicks >= ldUntil)
						{
							mOHost.StopSpeech();

							break;
						}

						yield return true;

						if (mOSpokenLines.Count > 0)
							fShowDueSpokenLines();
					}

					break;
				}
			}
		}

		/// <summary>cutsop_txt: the subtitle to draw after this frame, or none (string 0xFFFF). pbSpoken: shown beside its
		/// recording (the port's subtitles with speech), as the recording says it where that differs (UWCutsceneSpoken).</summary>
		private void fTxt(int piColour, int piString, bool pbSpoken = false)
		{
			if (mbB0Skipping)
				return;

			if (piString == UWCutsceneScript.NoText)
			{
				miTextLines = 0;

				return;
			}

			miTextColour = piColour & 0xFF;
			msText = UWText.Block(mOStrings, UWSound.SpeechStringBlock + miCutscene, piString + 1, null);

			if (pbSpoken)
				msText = UWCutsceneSpoken.Subtitle(UWGame.Uw2, miCutscene, piString, msText);
			miTextLines = string.IsNullOrEmpty(msText) ? 0 : 1;
		}

		/// <summary>cutsop_say: the recording BSPnn, or its text without speech or when it fails; 999 text only, 998
		/// nothing (with speech).</summary>
		private void fSay(int piColour, int piString, int piVoice)
		{
			if (!mbB5Speech)
			{
				fTxt(piColour, piString);

				return;
			}

			miFade45 = piVoice;

			if (miFade45 == TextOnlyVoice)
			{
				miFade45 = -1;
				fTxt(piColour, piString);
			}
			else if (miFade45 != NoVoice)
			{
				if (!mOHost.StartSpeech(piVoice))
					fTxt(piColour, piString);
				else
				{
					mbB6Speaking = true;

					if (SubtitlesWithSpeech)
						fStartSpokenSubtitles(piColour, piString);
				}
			}
			else if (SubtitlesWithSpeech && !fIsTimed(piString))
			{
				// 998: the line the running recording goes on to say (the dreams' later lines) - shown with the port's
				// subtitles, where the original, which shows none with speech, shows nothing; at the script's frame
				// unless its time in the recording is known (fStartSpokenSubtitles).
				fTxt(piColour, piString, true);
			}
		}

		// ------------------------------------------------- The port's subtitles with speech

		private sealed class SpokenLine
		{
			public double Due;

			public int String;

			public int Colour;
		}

		private readonly List<SpokenLine> mOSpokenLines = new List<SpokenLine>();

		/// <summary>A recording starts with the port's subtitles: its line at once (drawn now, so it fades in with the
		/// picture instead of after the fade), and the later lines it speaks at their times (UWCutsceneSpoken.Timing).</summary>
		private void fStartSpokenSubtitles(int piColour, int piString)
		{
			mOSpokenLines.Clear();
			fTxt(piColour, piString, true);

			if (miTextLines > 0 && !mbB0Skipping)
			{
				fDrawText(true);
				miTextLines = 0;
			}

			foreach (UWCutsceneSpoken.Timing lOTiming in UWCutsceneSpoken.TimingsOf(UWGame.Uw2, miCutscene))
			{
				if (lOTiming.Index > piString)
					mOSpokenLines.Add(new SpokenLine { Due = mdTicks + (lOTiming.Seconds * TicksPerSecond), String = lOTiming.Index, Colour = piColour });
			}
		}

		private bool fIsTimed(int piString)
		{
			foreach (UWCutsceneSpoken.Timing lOTiming in UWCutsceneSpoken.TimingsOf(UWGame.Uw2, miCutscene))
			{
				if (lOTiming.Index == piString)
					return true;
			}

			return false;
		}

		/// <summary>A spoken line whose time has come: drawn at once (in the time-critical round).</summary>
		private void fShowDueSpokenLines()
		{
			for (int liAt = 0; liAt < mOSpokenLines.Count; liAt++)
			{
				if (mOSpokenLines[liAt].Due > mdTicks)
					continue;

				SpokenLine lOLine = mOSpokenLines[liAt];

				mOSpokenLines.RemoveAt(liAt);
				fTxt(lOLine.Colour, lOLine.String, true);

				if (miTextLines > 0)
				{
					fDrawText(true);
					miTextLines = 0;
				}

				return;
			}
		}

		/// <summary>cuts_draw_text: the bar cleared, the lines drawn - the port's subtitles with speech onto the overlay,
		/// which shows unfaded (UWCutsceneScreenUw2.Overlay).</summary>
		private void fDrawText(bool pbSpoken = false)
		{
			Screen.FillBar(BarColour);
			Screen.ClearOverlay();
			Array.Copy(myPalette, Screen.Unfaded, myPalette.Length);
			Screen.TextOverlay = pbSpoken;
			mOHost.DrawText(Screen, msText, miTextColour);
			Screen.TextOverlay = false;
		}

		// ------------------------------------------------- The fades

		private IEnumerable<bool> fPerformFadeIn()
		{
			Screen.FillBar(BarColour);

			foreach (bool lbStep in fFadeIn(myPalette, miFade47))
				yield return lbStep;

			fGobble();
			mbB1Input = false;
			mbB0Skipping = false;
			miFade47 = -2;
			miFade49 = -1;
		}

		private IEnumerable<bool> fPerformFadeOut()
		{
			foreach (bool lbStep in fFadeOut(myPalette, miFade49))
				yield return lbStep;

			fGobble();
			mbB1Input = false;
			mbB0Skipping = false;
			miFade49 = -2;
			miFade47 = -1;
		}

		/// <summary>fadeout (GRFX.C): to black in count * 8 steps, each 8 ticks after the last; 0 at once.</summary>
		private IEnumerable<bool> fFadeOut(byte[] pySource, int piCount)
		{
			byte[] lyShown = new byte[UWCutsceneScreenUw2.PaletteBytes];

			// The port's subtitle layer does not fade: it goes as the picture fades out.
			Screen.ClearOverlay();

			if (piCount <= 0)
			{
				Screen.SetPalette(lyShown);

				yield break;
			}

			int liScale = piCount << 3;
			int[] liAcc = new int[UWCutsceneScreenUw2.PaletteBytes];

			for (int liAt = 0; liAt < liAcc.Length; liAt++)
				liAcc[liAt] = liScale * pySource[liAt];

			for (int liStep = 0; liStep < liScale; liStep++)
			{
				double ldStart = mdTicks;

				for (int liAt = 0; liAt < liAcc.Length; liAt++)
				{
					liAcc[liAt] -= pySource[liAt];
					lyShown[liAt] = (byte)(liAcc[liAt] / liScale);
				}

				while (mdTicks - ldStart < TaskStepTicks)
				{
					yield return true;

					// The port's spoken subtitles keep their times through a fade (the original pumps its speech there).
					if (mOSpokenLines.Count > 0)
						fShowDueSpokenLines();
				}

				Screen.SetPalette(lyShown);
			}
		}

		/// <summary>fadein: from black to the palette, the reverse of fadeout.</summary>
		private IEnumerable<bool> fFadeIn(byte[] pySource, int piCount)
		{
			if (piCount <= 0)
			{
				Screen.SetPalette(pySource);

				yield break;
			}

			int liScale = piCount << 3;
			int[] liAcc = new int[UWCutsceneScreenUw2.PaletteBytes];
			byte[] lyShown = new byte[UWCutsceneScreenUw2.PaletteBytes];

			for (int liStep = 0; liStep < liScale; liStep++)
			{
				double ldStart = mdTicks;

				for (int liAt = 0; liAt < liAcc.Length; liAt++)
				{
					liAcc[liAt] += pySource[liAt];
					lyShown[liAt] = (byte)(liAcc[liAt] / liScale);
				}

				while (mdTicks - ldStart < TaskStepTicks)
				{
					yield return true;

					// The port's spoken subtitles keep their times through a fade (the original pumps its speech there).
					if (mOSpokenLines.Count > 0)
						fShowDueSpokenLines();
				}

				Screen.SetPalette(lyShown);
			}
		}

		// ------------------------------------------------- The timed tasks (run_timebased_tasks)

		private int fInstallTask(int piPeriod, int piTotal)
		{
			for (int liAt = 0; liAt < TaskCount; liAt++)
			{
				if (mbTask[liAt])
					continue;

				mbTask[liAt] = true;
				miTaskSoFar[liAt] = miTaskResidue[liAt] = 0;
				miTaskPeriod[liAt] = Math.Max(1, piPeriod);
				miTaskTotal[liAt] = Math.Max(1, piTotal);

				return liAt;
			}

			return 0;
		}

		/// <summary>run_timebased_tasks(0): the steps of 8 ticks since the last call, counted against each task's period;
		/// a task runs when its count rises, a last time at its total and goes - and the round ends there.</summary>
		private void fRunTasks()
		{
			int liDiff = 0;

			if (mdTaskClock >= 0)
				liDiff = (int)(mdTicks - mdTaskClock);

			mdTaskClock = mdTicks;

			int liSteps = liDiff >> 3;

			miTaskClockResidue += liDiff & 7;

			if (miTaskClockResidue > 7)
			{
				miTaskClockResidue -= 8;
				liSteps++;
			}

			for (int liAt = 0; liAt < TaskCount; liAt++)
			{
				if (!mbTask[liAt])
					continue;

				int liOld = miTaskSoFar[liAt];

				miTaskSoFar[liAt] += liSteps / miTaskPeriod[liAt];
				miTaskResidue[liAt] += liSteps % miTaskPeriod[liAt];

				if (miTaskResidue[liAt] >= miTaskPeriod[liAt])
				{
					miTaskResidue[liAt] -= miTaskPeriod[liAt];
					miTaskSoFar[liAt]++;
				}

				if (miTaskSoFar[liAt] >= miTaskTotal[liAt])
				{
					fPaletteFade(liAt, miTaskTotal[liAt]);
					mbTask[liAt] = false;

					return;
				}

				if (miTaskSoFar[liAt] > liOld)
					fPaletteFade(liAt, miTaskSoFar[liAt]);
			}
		}

		/// <summary>punt_tasks: every task to its end.</summary>
		private void fPuntTasks()
		{
			for (int liAt = 0; liAt < TaskCount; liAt++)
			{
				if (!mbTask[liAt])
					continue;

				fPaletteFade(liAt, miTaskTotal[liAt]);
				mbTask[liAt] = false;
			}
		}

		/// <summary>palette_fade: entries first to last - 1 of the working palette step / total of the way from start_pal to
		/// end_pal (C's truncating division), shown at once.</summary>
		private void fPaletteFade(int piTask, int piStep)
		{
			int liFrom = miTaskFirst[piTask] * 3;
			int liEnd = Math.Min(UWCutsceneScreenUw2.PaletteBytes, miTaskLast[piTask] * 3);

			for (int liAt = liFrom; liAt < liEnd; liAt++)
				myPalette[liAt] = (byte)(myStartPal[liAt] + (piStep * (myEndPal[liAt] - myStartPal[liAt]) / miTaskTotal[piTask]));

			Screen.SetPaletteRange(myPalette, miTaskFirst[piTask], miTaskLast[piTask] - miTaskFirst[piTask]);
		}
	}
}
