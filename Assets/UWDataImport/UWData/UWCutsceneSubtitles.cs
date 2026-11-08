using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>The setting CutsceneSubtitles: subtitles while a cutscene's recording speaks.</summary>
	public enum UWCutsceneSubtitlesEnum
	{
		/// <summary>When the language's game texts translate the cutscene's block.</summary>
		Auto = 0,

		/// <summary>As the original: the text only where no recording speaks it.</summary>
		Off = 1,

		On = 2
	}

	/// <summary>
	/// SUBTITLES WITH THE RECORDINGS (per user, 2026-11-08, after a Chinese translation's intro with
	/// subtitles): the original intro shows no text while its recordings speak (UW.EXE draws a line only
	/// without speech output); a player of another language wants to read what is said. Auto shows them
	/// where the language's game texts (UWGameTextPack) hold a row of the cutscene's block (0x0C00 + its
	/// number, UWCutsceneRunner.GetSpeechBlock).
	/// </summary>
	public static class UWCutsceneSubtitles
	{
		/// <summary>Whether a cutscene shows its subtitles while the recordings speak.</summary>
		public static bool WithSpeech(UWCutsceneSubtitlesEnum peSetting, int piCutscene, UWGameTextPack pOPack)
		{
			if (peSetting == UWCutsceneSubtitlesEnum.On)
				return true;

			if (peSetting == UWCutsceneSubtitlesEnum.Off || pOPack == null)
				return false;

			int liBlock = UWCutsceneRunner.GetSpeechBlock(piCutscene);

			foreach (UWGameTextPack.Row lORow in pOPack.Rows)
			{
				if (lORow.Block == liBlock)
					return true;
			}

			return false;
		}
	}
}
