/// <summary>
/// WHO LOOKS: what the look (UWLookService) asks of the game around it beyond the player's other questions.
/// Interaction answers for the first game, UWAttackUw2 for the second (UW2 plan phase 4). What only the first
/// game's screen can show yet - the window's and the gravestone's pictures - and what waits for the traps (the look
/// triggers, the trap found on a look) stays with Interaction.
/// </summary>
public interface IUWLookHost : IUWMissileHost
{
    /// <summary>The look's sentence in the scroll.</summary>
    void AddLookLine(string psLine);

    string GetGeneralMessage(int piIndex);

    void ShowPagedText(string psText, params string[] psLeading);
}
