using System;
using System.IO;

namespace UWDataImport.Audio
{
	/// <summary>
	/// MUSIC FROM RECORDINGS (2026-10-27, ROADMAP "Next", suggested on Reddit): recordings the
	/// player supplies, one file per piece, played in place of the synthesizer when present. A
	/// piece without a recording plays on the music device as before, so a folder may hold only
	/// some of them.
	///
	/// A FOLDER PER GAME (per user, 2026-10-27: "think of UW2 right away"): Music in the game's
	/// folder beside settings.json (UWGameFolder), UW1/Music, and UW2/Music once Ultima Underworld
	/// II is in (Docs/UW2-PLAN.md - its pieces are named UWRnn, so the game's own names stay apart
	/// as well).
	///
	/// THE NAMES are the numbers of the game's own pieces, two octal digits as in the game's SOUND
	/// folder: 01.ogg is UW01.XMI, the introduction, 15.ogg is UW15.XMI, Maps and Legends
	/// (UWMusicSelector lists the themes). No prefix: the game's folder already says which game
	/// (per user, 2026-10-27). The case of the extension does not matter, on Linux either. Ogg Vorbis and WAV, the two formats Unity reads at run time on every platform the
	/// port runs on; with both present, Ogg wins.
	///
	/// Engine-free: the folder comes from the host (UWAudioMixer.RecordingFolder), the playing is
	/// Unity's (UWMusicRecordingPlayer).
	/// </summary>
	public static class UWMusicRecordings
	{
		/// <summary>The recordings' folder in the game's folder (UWGameFolder).</summary>
		public const string FolderName = "Music";

		/// <summary>The formats, in the order they are preferred.</summary>
		public static readonly string[] Extensions = { ".ogg", ".wav" };

		/// <summary>The name a recording of a piece carries, without its extension: the piece's file
		/// number (UWMusicSelector.GetFileNumber), "01" for UW01.XMI.</summary>
		public static string BaseName(string psNumber)
		{
			return psNumber;
		}

		/// <summary>The recording of a piece in a folder, null when there is none (or no folder).</summary>
		public static string Find(string psFolder, string psNumber)
		{
			if (string.IsNullOrEmpty(psFolder) || string.IsNullOrEmpty(psNumber) || !Directory.Exists(psFolder))
				return null;

			string lsBase = BaseName(psNumber);
			string[] lsFiles = fFiles(psFolder);

			foreach (string lsExtension in Extensions)
			{
				foreach (string lsFile in lsFiles)
				{
					if (string.Equals(Path.GetFileName(lsFile), lsBase + lsExtension, StringComparison.OrdinalIgnoreCase))
						return lsFile;
				}
			}

			return null;
		}

		/// <summary>How many of the pieces have a recording in the folder - each piece counted once,
		/// whatever its formats.</summary>
		public static int Count(string psFolder)
		{
			if (string.IsNullOrEmpty(psFolder) || !Directory.Exists(psFolder))
				return 0;

			bool[] lbFound = new bool[64];
			int liCount = 0;

			foreach (string lsFile in fFiles(psFolder))
			{
				string lsName = Path.GetFileNameWithoutExtension(lsFile);
				string lsExtension = Path.GetExtension(lsFile);

				if (lsName.Length != 2
					|| Array.FindIndex(Extensions, psKnown => string.Equals(psKnown, lsExtension, StringComparison.OrdinalIgnoreCase)) < 0)
					continue;

				int liHigh = lsName[0] - '0';
				int liLow = lsName[1] - '0';

				if (liHigh < 0 || liHigh > 7 || liLow < 0 || liLow > 7 || lbFound[(liHigh << 3) | liLow])
					continue;

				lbFound[(liHigh << 3) | liLow] = true;
				liCount++;
			}

			return liCount;
		}

		private static string[] fFiles(string psFolder)
		{
			try
			{
				return Directory.GetFiles(psFolder);
			}
			catch (Exception)
			{
				return new string[0];
			}
		}
	}
}
