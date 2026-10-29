using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GAME'S FOLDER beside settings.json (per user, 2026-10-27, thinking of Ultima Underworld
	/// II): what belongs to one game lies in a folder of its own - UW1/rules.json, UW1/Music -,
	/// what belongs to the player stays beside settings.json for both games (settings.json,
	/// graphics.json, the MT-32 ROMs). The extracted game data has its own cache per game
	/// (GameData/UW1), the saves lie in the game's own folder (UNDEROM1).
	///
	/// The folder is the game's profile's (IUWGameProfile.GameFolder): the caller names the game
	/// (UW2 plan phase 0 step 3, 2026-10-28; until then a Name that was always the first game's).
	///
	/// A file that an earlier version kept beside settings.json moves into the game's folder on
	/// the first start (AdoptLegacyFile): rules.json, there since 0.9.2. Those versions knew the
	/// first game only, so only its folder adopts them.
	/// </summary>
	public static class UWGameFolder
	{
		/// <summary>The folder of Ultima Underworld 1 (its profile's).</summary>
		public const string Uw1 = UWGameProfileUw1.GameFolderName;

		/// <summary>The game's folder under psRoot (the folder of settings.json).</summary>
		public static string Under(string psRoot, IUWGameProfile pOGame)
		{
			return Path.Combine(psRoot ?? string.Empty, pOGame.GameFolder);
		}

		/// <summary>
		/// The path of psFileName in the game's folder under psRoot. When an earlier version left the
		/// file beside settings.json and the first game's folder has none yet, it is moved there first
		/// (pFLog says so); when both exist, nothing is overwritten - the game's file counts and
		/// pFWarn names the old one. A move that fails leaves the old file where it is, used from
		/// there, and warns. Another game's folder never takes the old file: it was the first game's.
		/// </summary>
		public static string AdoptLegacyFile(string psRoot, IUWGameProfile pOGame, string psFileName, Action<string> pFLog, Action<string> pFWarn)
		{
			string lsFolder = Under(psRoot, pOGame);
			string lsTarget = Path.Combine(lsFolder, psFileName);
			string lsLegacy = Path.Combine(psRoot ?? string.Empty, psFileName);

			if (pOGame.Game != UWGame.Uw1 || !File.Exists(lsLegacy))
				return lsTarget;

			if (File.Exists(lsTarget))
			{
				pFWarn?.Invoke(psFileName + " exists twice: " + lsTarget + " is used, " + lsLegacy
					+ " (from an earlier version) is not - merge or remove it.");

				return lsTarget;
			}

			try
			{
				Directory.CreateDirectory(lsFolder);
				File.Move(lsLegacy, lsTarget);
				pFLog?.Invoke(psFileName + " moved into the game's folder: " + lsTarget);
			}
			catch (Exception lOError)
			{
				pFWarn?.Invoke(psFileName + " not moved into " + lsFolder + " (" + lOError.Message + "): used from its old place, " + lsLegacy + ".");

				return lsLegacy;
			}

			return lsTarget;
		}
	}
}
