namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE LINES FOR THE MESSAGE SCROLL GO (cleanup step 5.13, 2026-10-22): the three calls
	/// about 110 places make on Interaction, and the presentations and hosts declare in their own
	/// words. Interaction stays the facade and forwards to UWMessageLog, which keeps the list; a
	/// presentation (IUWPresentation) is a sink with sounds, effects and cutscenes on top; a silent
	/// scope (UWSilentPresentation) drops the lines.
	/// </summary>
	public interface IUWMessageSink
	{
		/// <summary>A composed line for the message scroll; an empty one is dropped.</summary>
		void AddMessage(string psMessage);

		/// <summary>A line of string block 1, our numbering; a missing entry adds nothing.</summary>
		void AddGeneralMessage(int piIndex);

		/// <summary>A line of string block 1 as text, for messages assembled from pieces. Empty for
		/// a number that does not exist, and in a silent scope.</summary>
		string GetGeneralMessage(int piIndex);
	}
}
