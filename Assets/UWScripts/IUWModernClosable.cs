/// <summary>
/// Something of the modern scheme that Escape and the gamepad's B close before the game menu
/// opens (UWModernUi's closable chain, cleanup 5.16, 2026-10-22): a waiting spell, the bags' use
/// mode, the bags, the character panel, the rune panel. Until then the order was written twice,
/// in UWGameUI's Escape routing and in its PadBackCloses, and had to be kept in step by hand.
/// </summary>
public interface IUWModernClosable
{
    /// <summary>Whether there is something to close now.</summary>
    bool IsClosable { get; }

    /// <summary>Closes it - called only while IsClosable says so.</summary>
    void Close();
}
