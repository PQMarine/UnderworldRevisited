using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>The values of UWUserSettings.MusicDevice (settings.json): 0 the AdLib, 1 General
	/// MIDI, 2 the MT-32.</summary>
	public enum UWMusicDeviceId
	{
		AdLib,
		GeneralMidi,
		Mt32
	}

	/// <summary>
	/// THE MUSIC DEVICE (2026-10-08, per user: "everything shall work out of the box"): the music
	/// can instead play its MT-32 version (UW*.XMI) on General MIDI - UWGmMusicDevice with the
	/// soundfont in StreamingAssets/UWMusic - or on the MT-32 itself, UWMt32MusicDevice (Munt's
	/// libmt32emu) with the player's own ROMs. A device that cannot start (no soundfont, no ROMs,
	/// no library) leaves the AdLib playing.
	///
	/// Cleanup step 6.6 (2026-10-24): the choice, the fallback and the MT-32 ROM discovery out of
	/// UWAudioEngine. Engine-free: the host gives the soundfont path, the ROM folder and where the
	/// messages go.
	///
	/// Devices are made on the first wish for them and kept: the General MIDI soundfont is tried
	/// once, the MT-32 again on every switch to it until it starts, so ROMs put into the folder
	/// later are taken. Making one happens outside the audio lock (UWAudioMixer), switching under
	/// it (UWAudioMixCore.SwitchDevice).
	/// </summary>
	public sealed class UWMusicDeviceSelector : IDisposable
	{
		private readonly IUWMusicDevice[] mIDevices = new IUWMusicDevice[3];

		private readonly int miOutputRate;

		private readonly string msSoundFontPath;

		private readonly Func<string> mFRomFolder;

		private readonly Action<string> mFLog;

		private readonly Action<string> mFWarn;

		private bool mbGmFailed;

		/// <param name="pOAdlib">The chip's device, always there.</param>
		/// <param name="piOutputRate">The rate General MIDI and the MT-32 render at.</param>
		/// <param name="psSoundFontPath">The General MIDI soundfont.</param>
		/// <param name="pFRomFolder">Where the MT-32 ROMs are looked for, asked on every try.</param>
		/// <param name="pFLog">A device started (may be null).</param>
		/// <param name="pFWarn">A device could not start (may be null).</param>
		public UWMusicDeviceSelector(UWAdlibMusicDevice pOAdlib, int piOutputRate, string psSoundFontPath,
			Func<string> pFRomFolder, Action<string> pFLog, Action<string> pFWarn)
		{
			mIDevices[(int)UWMusicDeviceId.AdLib] = pOAdlib;
			Adlib = pOAdlib;
			miOutputRate = piOutputRate;
			msSoundFontPath = psSoundFontPath;
			mFRomFolder = pFRomFolder;
			mFLog = pFLog;
			mFWarn = pFWarn;
		}

		public UWAdlibMusicDevice Adlib { get; }

		/// <summary>The device the music plays on now: the one the settings ask for, unless it
		/// could not start - then the AdLib.</summary>
		public UWMusicDeviceId Current { get; private set; } = UWMusicDeviceId.AdLib;

		public IUWMusicDevice CurrentDevice => mIDevices[(int)Current];

		/// <summary>How many device slots there are (GetDevice).</summary>
		public int SlotCount => mIDevices.Length;

		/// <summary>The device in a slot (UWMusicDeviceId), null when it was never made. The
		/// lifecycle loops go over the slots in this order: AdLib, General MIDI, MT-32.</summary>
		public IUWMusicDevice GetDevice(int piSlot)
		{
			return mIDevices[piSlot];
		}

		/// <summary>The device a wish comes to: the one asked for when it can start (made now if
		/// it was not yet), else the AdLib. Does not switch - see UWAudioMixCore.SwitchDevice.</summary>
		public UWMusicDeviceId Resolve(UWMusicDeviceId peWanted)
		{
			if (peWanted == UWMusicDeviceId.GeneralMidi && fEnsureGm())
				return UWMusicDeviceId.GeneralMidi;

			if (peWanted == UWMusicDeviceId.Mt32 && fEnsureMt32())
				return UWMusicDeviceId.Mt32;

			return UWMusicDeviceId.AdLib;
		}

		/// <summary>The device the music plays on from now on; it must have been made (Resolve).
		/// Under the audio lock.</summary>
		public void SetCurrent(UWMusicDeviceId peDevice)
		{
			Current = mIDevices[(int)peDevice] != null ? peDevice : UWMusicDeviceId.AdLib;
		}

		private bool fEnsureGm()
		{
			if (mIDevices[(int)UWMusicDeviceId.GeneralMidi] != null)
				return true;

			if (mbGmFailed)
				return false;

			try
			{
				mIDevices[(int)UWMusicDeviceId.GeneralMidi] = new UWGmMusicDevice(msSoundFontPath, miOutputRate);
			}
			catch (Exception lOError)
			{
				mbGmFailed = true;
				mFWarn?.Invoke("General MIDI not available (" + msSoundFontPath + "): " + lOError.Message);

				return false;
			}

			return true;
		}

		/// <summary>The MT-32 emulation, made once from the ROMs found. Asked again on every
		/// switch to the MT-32, so ROMs put into the folder later are taken.</summary>
		private bool fEnsureMt32()
		{
			if (mIDevices[(int)UWMusicDeviceId.Mt32] != null)
				return true;

			string lsError;
			UWMt32MusicDevice lODevice = UWMt32MusicDevice.TryCreate(mFRomFolder != null ? mFRomFolder() : null, miOutputRate, out lsError);

			if (lODevice == null)
			{
				mFWarn?.Invoke("MT-32 not available: " + lsError);

				return false;
			}

			mFLog?.Invoke("MT-32 emulation with " + lODevice.Description);
			mIDevices[(int)UWMusicDeviceId.Mt32] = lODevice;

			return true;
		}

		public void Dispose()
		{
			for (int liSlot = 0; liSlot < mIDevices.Length; liSlot++)
			{
				if (mIDevices[liSlot] != null)
					mIDevices[liSlot].Dispose();
			}

			mIDevices[(int)UWMusicDeviceId.GeneralMidi] = null;
			mIDevices[(int)UWMusicDeviceId.Mt32] = null;
			Current = UWMusicDeviceId.AdLib;
		}

		// ------------------------------------------------------------------
		// The MT-32 ROMs, for the menu
		// ------------------------------------------------------------------

		private static string msMt32CheckedFor;

		private static UWMt32MusicDriver.RomSet msOMt32Roms;

		private static string msMt32Problem;

		/// <summary>The MT-32 ROMs in a folder, looked for again when the folder or its contents
		/// changed (the folder's last write time is the key); null when there are none,
		/// GetMt32Problem then says why.</summary>
		public static UWMt32MusicDriver.RomSet GetMt32Roms(string psFolder)
		{
			string lsKey = psFolder + "|" + (Directory.Exists(psFolder)
				? Directory.GetLastWriteTimeUtc(psFolder).Ticks.ToString() : "-");

			if (lsKey == msMt32CheckedFor)
				return msOMt32Roms;

			msMt32CheckedFor = lsKey;
			msOMt32Roms = null;
			msMt32Problem = null;

			try
			{
				msOMt32Roms = UWMt32MusicDriver.FindRoms(psFolder);

				if (msOMt32Roms == null)
					msMt32Problem = UWText.T("setup.sound.mt32_no_roms");
			}
			catch (Exception lOError)
			{
				msMt32Problem = UWText.F("setup.sound.mt32_missing", lOError.GetType().Name);

			}

			return msOMt32Roms;
		}

		/// <summary>Why GetMt32Roms found nothing in that folder, null when it found ROMs.</summary>
		public static string GetMt32Problem(string psFolder)
		{
			GetMt32Roms(psFolder);

			return msMt32Problem;
		}
	}
}
