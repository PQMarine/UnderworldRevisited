using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>A host for the router: the acts pressed, the gates holding, the handlers run.</summary>
        private sealed class FakeHotkeyHost : IUWHotkeyHost
        {
            public bool Modern;

            public UWHotkeyMods HeldMods;

            public readonly HashSet<UWAct> PressedActs = new HashSet<UWAct>();

            public readonly HashSet<UWAct> FromPad = new HashSet<UWAct>();

            /// <summary>The owners the Escape chain gives this frame's back press to.</summary>
            public readonly HashSet<UWBackOwner> BackTakers = new HashSet<UWBackOwner>();

            public bool RawF7;

            public bool RawF10;

            public UWHotkeyGate Holding;

            /// <summary>What CloseChain finds to close.</summary>
            public int Closables;

            public readonly List<string> Ran = new List<string>();

            public bool IsModern => Modern;

            public UWHotkeyMods Mods => HeldMods;

            public bool Fired(UWHotkeyRow pORow)
            {
                switch (pORow.Trigger)
                {
                    case UWHotkeyTrigger.Pressed:
                    case UWHotkeyTrigger.Performed:
                        return PressedActs.Contains(pORow.Act);
                    case UWHotkeyTrigger.Always:
                        return true;
                    case UWHotkeyTrigger.Back:
                        return BackTakers.Contains(pORow.BackOwner);
                    case UWHotkeyTrigger.RawF7:
                        return RawF7;
                    case UWHotkeyTrigger.RawF10:
                        return RawF10;
                    default:
                        return false;
                }
            }

            public bool FromKeyboard(UWAct peAct)
            {
                return !FromPad.Contains(peAct);
            }

            public bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
            {
                return (Holding & peGate) != 0;
            }

            public bool Run(UWHotkeyHandler peHandler)
            {
                // The handlers that change what later rows see, as the game's do; a barrier does nothing.
                switch (peHandler)
                {
                    case UWHotkeyHandler.None:
                        return true;
                    case UWHotkeyHandler.CloseChain:
                        if (Closables == 0)
                            return false;

                        Closables--;
                        break;
                    case UWHotkeyHandler.ToggleMap:
                        Holding ^= UWHotkeyGate.MapVisible;
                        break;
                    case UWHotkeyHandler.HideMap:
                        Holding &= ~UWHotkeyGate.MapVisible;
                        break;
                    case UWHotkeyHandler.OpenOptions:
                        Holding |= UWHotkeyGate.OptionsOpen;
                        break;
                    case UWHotkeyHandler.ToggleModernMenu:
                        Holding ^= UWHotkeyGate.MenuOpen;
                        break;
                }

                Ran.Add(peHandler.ToString());
                return true;
            }
        }
    }
}
