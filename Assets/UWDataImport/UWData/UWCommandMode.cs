namespace UWDataImport.UWData
{
	/// <summary>
	/// The command icons of the left bar - what the original calls the interaction mode
	/// (UW.EXE SetInteractionMode_seg024_24DC_13D5, mode word at dseg 268C). None is the
	/// default with no icon lit, in which the mouse buttons do what Interaction measured (look
	/// on the press, use or drag on the release); fight is the drawn weapon, which the icon and
	/// the click on the weapon hand share; options opens the options panel. The numbers here
	/// are ours - which of the original's five numbers is which icon is not resolved: the
	/// routine treats 2 as the weapon and 5 as the options, calls ChangeCursor for 1, 3 and 4
	/// with one argument, and the user sees the red X for look, get and use - so those are
	/// 1, 3 and 4 in some order, and talk, which changes no cursor, would have to be 0, which
	/// the same routine uses as "off". Left as it is until it matters. RESOLVED FOR LOOK
	/// (2026-09-30, see Look_seg024_24DC_D20): it searches for a trap only while the word is 3 and
	/// otherwise goes on to pick up (the plain press), so 3 is look; get and use are 1 and 4.
	///
	/// RULES FROM THE ROUTINE: clicking the icon that is already lit switches the mode off;
	/// selecting another icon first draws the old one unlit; the fight mode is refused while
	/// swimming (player byte 0xB8 bit 0) and puts the weapon away when another icon replaces
	/// it. The viewport click (ClickOnObject_seg024_24DC_EFF) reads the mode and dispatches
	/// through a table; "You cannot reach that." (string 94) is its refusal for getting.
	///
	/// CURSOR (per user on the original, 2026-09-19): look, get and use turn the pointer into
	/// the red X; talk leaves it as it is.
	///
	/// MEASURED FOR TALK (per user on the original, 2026-09-19): a mode changes only the
	/// right-button gesture over its kind of target - over a creature the press does nothing,
	/// no look, and the release starts the conversation, past the drag threshold or not; over
	/// anything else the press answers at once - "You cannot talk to that." over a wall, the
	/// ceiling or nothing, "You cannot talk to that!" over any other thing - and that ends the
	/// gesture (second measurement the same day; the first had seen no change there). IN EVERY MODE the left button keeps its
	/// cursor movement (per user, 2026-09-19), so a mode can only ever change the right-button
	/// gesture. MEASURED FOR GET (per user, 2026-09-19): the press decides at once, no look -
	/// "Nothing to get." over a wall, the ceiling or nothing, "You cannot pick that up." over
	/// a thing that cannot be taken, and a pickable thing hangs on the pointer immediately, as
	/// if dragged, with the drag's rules from there. THE MODES ACT ONLY IN THE VIEWPORT, over the UI
	/// everything behaves as with no mode (per user, 2026-09-19). MEASURED FOR LOOK (per user, 2026-09-19): the look on the press in the
	/// viewport, on the release over the UI - as without a mode. MEASURED FOR USE (per user,
	/// 2026-09-19): "You cannot use that." over a wall, the ceiling or nothing; nothing at all
	/// over a creature; any other thing is remembered on the press and used on the release,
	/// wherever the pointer is by then; no drag starts in the viewport. Doors and switches
	/// print nothing, a container spills, food on the floor is not eaten, a light on the floor
	/// says "Lights may only be used if equipped.".
	/// </summary>
	public enum UWCommandMode
	{
		None,
		Talk,
		Fight,
		Get,
		Look,
		Use,
		Options
	}
}
