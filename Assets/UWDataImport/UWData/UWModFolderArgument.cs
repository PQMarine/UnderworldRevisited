using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// ANOTHER MODS FOLDER FOR ONE RUN (2026-11-16, for a tool that starts the game with a mod to try, such as the
	/// Underworld Editor (Avalonia)'s planned "Run in Underworld Revisited"): -mods followed by a folder, or -mods=folder,
	/// makes that folder the Mods folder for this start - its mods and its order.json - and leaves the player's own Mods
	/// folder alone. A relative folder counts from the working directory. No argument, or one without a folder: the
	/// normal Mods folder.
	/// </summary>
	public static class UWModFolderArgument
	{
		public const string Argument = "-mods";

		/// <summary>The folder the arguments name, as a full path; null for none.</summary>
		public static string Find(string[] paArguments, string psWorkingDirectory)
		{
			if (paArguments == null)
				return null;

			for (int liAt = 0; liAt < paArguments.Length; liAt++)
			{
				string lsArgument = paArguments[liAt];

				if (lsArgument == null)
					continue;

				string lsFolder = null;

				if (string.Equals(lsArgument, Argument, StringComparison.OrdinalIgnoreCase))
					lsFolder = liAt + 1 < paArguments.Length ? paArguments[liAt + 1] : null;
				else if (lsArgument.StartsWith(Argument + "=", StringComparison.OrdinalIgnoreCase))
					lsFolder = lsArgument.Substring(Argument.Length + 1);

				if (string.IsNullOrWhiteSpace(lsFolder) || lsFolder.StartsWith("-", StringComparison.Ordinal))
					continue;

				lsFolder = lsFolder.Trim().Trim('"');

				return Path.IsPathRooted(lsFolder) || string.IsNullOrEmpty(psWorkingDirectory)
					? Path.GetFullPath(lsFolder)
					: Path.GetFullPath(Path.Combine(psWorkingDirectory, lsFolder));
			}

			return null;
		}
	}
}
