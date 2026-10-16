using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The game's MT-32 music (UW*.XMI) on the MT-32 itself, emulated by Munt's libmt32emu
	/// (LGPL 2.1 or later), which ships as its own unmodified native library (mt32emu.dll,
	/// libmt32emu.so) and is only called through its C interface - nothing of it is compiled into
	/// our code. The emulation needs the ROMs of a real unit, which belong to Roland and never
	/// ship: the player puts them into a folder (FindRoms).
	///
	/// The pieces are written for this device (see UWGmMusicDriver), so the events pass as they
	/// are, apart from the Miles driver's own XMIDI controllers 110-120.
	///
	/// THE INSTRUMENT CHANNEL: the game plays the mandolin and the flute on a channel the
	/// MT-32 does not receive (only 2-10 by default). UW.EXE gets a channel from the Miles
	/// driver for it (PlayMusicalInstrument_seg014_1195), which locks one of the music's: the
	/// music on it falls silent while the instrument plays and gets its sound back afterwards. So
	/// here: events on a channel the device does not receive lock the highest part (channel 9),
	/// the music's events for it only update a shadow of program, volume, pan, expression and
	/// bend, and ReleaseLockedChannel gives the part back with that state.
	/// </summary>
	public sealed class UWMt32MusicDriver : IUWRenderingMidiDriver, IDisposable
	{
		private const string Library = "mt32emu";

		private const int AddedControlRom = 1;

		private const int AddedPcmRom = 2;

		private const int QualityGood = 2;

		/// <summary>The part an instrument borrows: MIDI channel 9, the MT-32's part 8.</summary>
		private const int LockedPart = 8;

		/// <summary>The machines whose ROMs are taken, best first: the MT-32 of 1990-1992 the game
		/// was written for, then the later and the compatible units.</summary>
		private static readonly string[] msMachines =
		{
			"mt32_1_07", "mt32_1_06", "mt32_1_05", "mt32_1_04", "mt32_bluer",
			"mt32_2_07", "mt32_2_06", "mt32_2_04", "mt32_2_03",
			"cm32l_1_02", "cm32l_1_00", "cm32ln_1_00"
		};

		[StructLayout(LayoutKind.Sequential)]
		private struct RomInfo
		{
			public IntPtr ControlRomId;
			public IntPtr ControlRomDescription;
			public IntPtr ControlRomSha1;
			public IntPtr PcmRomId;
			public IntPtr PcmRomDescription;
			public IntPtr PcmRomSha1;
		}

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr mt32emu_create_context(IntPtr piReportHandler, IntPtr piInstanceData);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern void mt32emu_free_context(IntPtr piContext);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int mt32emu_identify_rom_data(ref RomInfo pORomInfo, byte[] pyData, UIntPtr puSize, string psMachineId);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int mt32emu_add_rom_data(IntPtr piContext, IntPtr piData, UIntPtr puSize, IntPtr piSha1);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int mt32emu_get_best_analog_output_mode(double pfSampleRate);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern void mt32emu_set_analog_output_mode(IntPtr piContext, int piMode);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern void mt32emu_set_stereo_output_samplerate(IntPtr piContext, double pfSampleRate);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern void mt32emu_set_samplerate_conversion_quality(IntPtr piContext, int piQuality);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int mt32emu_open_synth(IntPtr piContext);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern void mt32emu_close_synth(IntPtr piContext);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern int mt32emu_play_msg(IntPtr piContext, uint puMessage);

		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
		private static extern void mt32emu_render_float(IntPtr piContext, float[] pfStream, uint puFrames);

		/// <summary>What FindRoms found: the machine and the two ROM images for it.</summary>
		public sealed class RomSet
		{
			public string Machine;

			public string Description;

			public byte[] Control;

			public byte[] Pcm;
		}

		/// <summary>A shadow of what the music set on the locked part.</summary>
		private struct PartState
		{
			public int Program;
			public int Volume;
			public int Pan;
			public int Expression;
			public int BendLow;
			public int BendHigh;
		}

		private IntPtr miContext;

		private readonly List<IntPtr> mORomMemory = new List<IntPtr>();

		private float[] mfInterleaved = new float[0];

		private bool mbLocked;

		private PartState mOShadow;

		public int SampleRate { get; }

		public string Description { get; }

		private UWMt32MusicDriver(RomSet pORoms, int piSampleRate)
		{
			SampleRate = piSampleRate;
			Description = pORoms.Description;

			miContext = mt32emu_create_context(IntPtr.Zero, IntPtr.Zero);

			if (miContext == IntPtr.Zero)
				throw new InvalidOperationException("libmt32emu gave no context");

			fAddRom(pORoms.Control);
			fAddRom(pORoms.Pcm);

			mt32emu_set_analog_output_mode(miContext, mt32emu_get_best_analog_output_mode(piSampleRate));
			mt32emu_set_stereo_output_samplerate(miContext, piSampleRate);
			mt32emu_set_samplerate_conversion_quality(miContext, QualityGood);

			int liResult = mt32emu_open_synth(miContext);

			if (liResult != 0)
			{
				Dispose();
				throw new InvalidOperationException("libmt32emu did not open (" + liResult + ")");
			}

			fResetShadow();
		}

		/// <summary>The emulation for the ROMs in a folder, or null with the reason - no ROMs, or
		/// no library on this system.</summary>
		public static UWMt32MusicDriver TryCreate(string psRomFolder, int piSampleRate, out string psError)
		{
			try
			{
				RomSet lORoms = FindRoms(psRomFolder);

				if (lORoms == null)
				{
					psError = "no MT-32 or CM-32L ROMs in " + psRomFolder;

					return null;
				}

				psError = null;

				return new UWMt32MusicDriver(lORoms, piSampleRate);
			}
			catch (Exception lOError)
			{
				psError = lOError.Message;

				return null;
			}
		}

		/// <summary>
		/// The best full control and PCM ROM pair in a folder (its own files, not below), by the
		/// order of msMachines; Munt tells each file by its SHA1. Null without a pair. Throws when
		/// the library is missing.
		/// </summary>
		public static RomSet FindRoms(string psFolder)
		{
			if (string.IsNullOrEmpty(psFolder) || !Directory.Exists(psFolder))
				return null;

			List<byte[]> lOImages = new List<byte[]>();

			foreach (string lsFile in Directory.GetFiles(psFolder))
			{
				long liLength = new FileInfo(lsFile).Length;

				// Control ROMs are 64 or 128 KB, PCM ROMs 512 KB or 1 MB.
				if (liLength == 65536 || liLength == 131072 || liLength == 524288 || liLength == 1048576)
					lOImages.Add(File.ReadAllBytes(lsFile));
			}

			foreach (string lsMachine in msMachines)
			{
				RomSet lOSet = new RomSet { Machine = lsMachine };

				foreach (byte[] lyImage in lOImages)
				{
					RomInfo lOInfo = new RomInfo();

					if (mt32emu_identify_rom_data(ref lOInfo, lyImage, (UIntPtr)lyImage.Length, lsMachine) != 0)
						continue;

					if (lOInfo.ControlRomId != IntPtr.Zero && lOSet.Control == null)
					{
						lOSet.Control = lyImage;
						lOSet.Description = Marshal.PtrToStringAnsi(lOInfo.ControlRomDescription);
					}
					else if (lOInfo.PcmRomId != IntPtr.Zero && lOSet.Pcm == null)
					{
						lOSet.Pcm = lyImage;
					}
				}

				if (lOSet.Control != null && lOSet.Pcm != null)
					return lOSet;
			}

			return null;
		}

		private void fAddRom(byte[] pyImage)
		{
			// Munt keeps the pointer, so the copy lives as long as the context.
			IntPtr liMemory = Marshal.AllocHGlobal(pyImage.Length);

			Marshal.Copy(pyImage, 0, liMemory, pyImage.Length);
			mORomMemory.Add(liMemory);

			int liResult = mt32emu_add_rom_data(miContext, liMemory, (UIntPtr)pyImage.Length, IntPtr.Zero);

			if (liResult != AddedControlRom && liResult != AddedPcmRom)
				throw new InvalidOperationException("libmt32emu refused a ROM (" + liResult + ")");
		}

		public void Handle(int piStatus, int piData1, int piData2)
		{
			int liChannel = piStatus & 0x0F;
			int liCommand = piStatus & 0xF0;

			if (liCommand == 0xB0 && piData1 >= 110 && piData1 <= 120)
				return;

			// Channels 1 and 11-16 reach no part: an instrument, which borrows part 8.
			if (liChannel == 0 || liChannel > 9)
			{
				if (!mbLocked)
				{
					mbLocked = true;
					fSend(0xB0 | LockedPart, 123, 0);
				}

				fSend(liCommand | LockedPart, piData1, piData2);

				return;
			}

			if (liChannel == LockedPart)
			{
				fRemember(liCommand, piData1, piData2);

				if (mbLocked)
					return;
			}

			fSend(piStatus, piData1, piData2);
		}

		/// <summary>The instrument is put down: part 8 goes back to the music with the state the
		/// music gave it in the meantime.</summary>
		public void ReleaseLockedChannel()
		{
			if (!mbLocked)
				return;

			mbLocked = false;
			fSend(0xB0 | LockedPart, 123, 0);
			fSend(0xC0 | LockedPart, mOShadow.Program, 0);
			fSend(0xB0 | LockedPart, 7, mOShadow.Volume);
			fSend(0xB0 | LockedPart, 10, mOShadow.Pan);
			fSend(0xB0 | LockedPart, 11, mOShadow.Expression);
			fSend(0xE0 | LockedPart, mOShadow.BendLow, mOShadow.BendHigh);
		}

		private void fRemember(int piCommand, int piData1, int piData2)
		{
			switch (piCommand)
			{
				case 0xC0:
					mOShadow.Program = piData1;
					break;

				case 0xE0:
					mOShadow.BendLow = piData1;
					mOShadow.BendHigh = piData2;
					break;

				case 0xB0:
					if (piData1 == 7)
						mOShadow.Volume = piData2;
					else if (piData1 == 10)
						mOShadow.Pan = piData2;
					else if (piData1 == 11)
						mOShadow.Expression = piData2;
					break;
			}
		}

		private void fResetShadow()
		{
			mOShadow = new PartState { Program = 0, Volume = 100, Pan = 64, Expression = 127, BendLow = 0, BendHigh = 64 };
		}

		public void AllNotesOff()
		{
			for (int liChannel = 1; liChannel <= 9; liChannel++)
			{
				fSend(0xB0 | liChannel, 64, 0);
				fSend(0xB0 | liChannel, 123, 0);
			}
		}

		/// <summary>Notes off, controllers and bend back, the instrument's part returned - the
		/// programs stay, every piece sets its own.</summary>
		public void Reset()
		{
			AllNotesOff();
			mbLocked = false;

			for (int liChannel = 1; liChannel <= 9; liChannel++)
			{
				fSend(0xB0 | liChannel, 121, 0);
				fSend(0xE0 | liChannel, 0, 64);
			}

			fResetShadow();
		}

		/// <summary>The next piCount stereo samples into the two buffers, from their start.</summary>
		public void Render(float[] pfLeft, float[] pfRight, int piCount)
		{
			if (miContext == IntPtr.Zero)
				return;

			if (mfInterleaved.Length < piCount * 2)
				mfInterleaved = new float[piCount * 2];

			mt32emu_render_float(miContext, mfInterleaved, (uint)piCount);

			for (int liAt = 0; liAt < piCount; liAt++)
			{
				pfLeft[liAt] = mfInterleaved[liAt * 2];
				pfRight[liAt] = mfInterleaved[(liAt * 2) + 1];
			}
		}

		/// <summary>
		/// One message into Munt's queue, played at the start of the next render. NOT the
		/// "_now" call: that one has no effect while the synth aborts a poly, which a key struck
		/// again does all the time - with it every second note of the pieces went missing (per
		/// user 2026-10-08, "as if only half the notes play"; Munt's poly state changes in Armed
		/// were 775 for 687 notes, through the queue 1356).
		/// </summary>
		private void fSend(int piStatus, int piData1, int piData2)
		{
			if (miContext != IntPtr.Zero)
				mt32emu_play_msg(miContext, (uint)((piStatus & 0xFF) | ((piData1 & 0x7F) << 8) | ((piData2 & 0x7F) << 16)));
		}

		public void Dispose()
		{
			if (miContext != IntPtr.Zero)
			{
				mt32emu_close_synth(miContext);
				mt32emu_free_context(miContext);
				miContext = IntPtr.Zero;
			}

			foreach (IntPtr liMemory in mORomMemory)
				Marshal.FreeHGlobal(liMemory);

			mORomMemory.Clear();
		}
	}
}
