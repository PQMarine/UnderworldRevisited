using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Logging for the engine-free rules (P0 of the engine separation, 2026-09-17). The rules
	/// write here, the host decides where it goes: the game hands in Unity's Debug.Log (see
	/// UWLogBridge), a test driver can collect the lines, and without a host nothing is written.
	/// </summary>
	public static class UWLog
	{
		/// <summary>Receives info lines. Null drops them.</summary>
		public static Action<string> InfoSink;

		/// <summary>Receives warnings. Null drops them.</summary>
		public static Action<string> WarningSink;

		/// <summary>Receives errors. Null drops them.</summary>
		public static Action<string> ErrorSink;

		public static void Info(string psMessage)
		{
			InfoSink?.Invoke(psMessage);
		}

		public static void Warning(string psMessage)
		{
			WarningSink?.Invoke(psMessage);
		}

		public static void Error(string psMessage)
		{
			ErrorSink?.Invoke(psMessage);
		}
	}
}
