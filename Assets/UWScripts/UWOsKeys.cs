using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// THE OPERATING SYSTEM'S KEYS, pressed by the gamepad's letter grid (UWLetterGrid, per user
/// 2026-10-07). Real key presses for the same reason as UWOsMouse's real clicks: the text inputs
/// are of several kinds - IMGUI fields (the modern save name), the Input System's text events
/// (UWTypedKeys), the keys read directly (Backspace, Enter) - and a real key reaches them all.
///
/// Windows: SendInput, a character as a Unicode packet, Backspace, Enter and Escape as keys with
/// their scan code. Linux: XTest with the keysym's keycode, Shift for capitals - UNTESTED, and
/// nothing under a native Wayland window.
/// </summary>
public static class UWOsKeys
{
    public enum KeyEnum
    {
        Backspace,
        Enter,
        Escape
    }

    private static bool msbFailed;

    /// <summary>Types one printable character.</summary>
    public static void Type(char pcChar)
    {
        if (msbFailed)
            return;

        try
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            fSend(new[]
            {
                fKey(0, pcChar, Unicode),
                fKey(0, pcChar, Unicode | KeyUp)
            });
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            fXType((ulong)pcChar, char.IsUpper(pcChar));
#endif
        }
        catch (Exception lOError)
        {
            fFail(lOError);
        }
    }

    /// <summary>Presses and lets go of one of the editing keys.</summary>
    public static void Press(KeyEnum peKey)
    {
        if (msbFailed)
            return;

        try
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            ushort luVk = peKey == KeyEnum.Backspace ? (ushort)0x08 : peKey == KeyEnum.Enter ? (ushort)0x0D : (ushort)0x1B;
            ushort luScan = (ushort)MapVirtualKey(luVk, 0);

            fSend(new[]
            {
                fKey(luVk, luScan, 0),
                fKey(luVk, luScan, KeyUp)
            });
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            fXType(peKey == KeyEnum.Backspace ? 0xFF08UL : peKey == KeyEnum.Enter ? 0xFF0DUL : 0xFF1BUL, false);
#endif
        }
        catch (Exception lOError)
        {
            fFail(lOError);
        }
    }

    /// <summary>
    /// Lets go of what was held over into this frame - on Linux the Shift of a capital: pressed and
    /// let go in one batch, the game read the key after Shift was up again and typed a small letter
    /// (per user in the Linux VM, 2026-10-08: "aaaaaa"). Called once a frame (UWLetterGrid).
    /// </summary>
    public static void ReleaseHeld()
    {
        // The same order of the platforms as everywhere here: the Windows editor building for
        // Linux has both.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        if (msuHeldShift == 0 || msbFailed)
            return;

        try
        {
            XTestFakeKeyEvent(msDisplay, msuHeldShift, 0, 0);
            XFlush(msDisplay);
        }
        catch (Exception lOError)
        {
            fFail(lOError);
        }

        msuHeldShift = 0;
#endif
    }

    private static void fFail(Exception pOError)
    {
        msbFailed = true;
        Debug.LogWarning("UWOsKeys: no system keys for the gamepad's letter grid (" + pOError.GetType().Name + ": " + pOError.Message + ").");
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const uint InputKeyboard = 1;

    private const uint KeyUp = 0x0002;

    private const uint Unicode = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    /// <summary>INPUT on 64 bits (the only player built): the union at 8, as large as its largest
    /// member MOUSEINPUT (32), 40 in all.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct SystemInput
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KeyInput Key;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint puCount, SystemInput[] pyInputs, int piSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint puCode, uint puMapType);

    private static SystemInput fKey(ushort puVk, ushort puScan, uint puFlags)
    {
        return new SystemInput { Type = InputKeyboard, Key = new KeyInput { Vk = puVk, Scan = puScan, Flags = puFlags } };
    }

    private static void fSend(SystemInput[] pyInputs)
    {
        SendInput((uint)pyInputs.Length, pyInputs, Marshal.SizeOf<SystemInput>());
    }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    private static IntPtr msDisplay = IntPtr.Zero;

    private const ulong ShiftLeft = 0xFFE1;

    /// <summary>The Shift held over to the next frame (ReleaseHeld), 0 for none.</summary>
    private static uint msuHeldShift;

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr pName);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr pDisplay);

    [DllImport("libX11.so.6")]
    private static extern byte XKeysymToKeycode(IntPtr pDisplay, ulong puKeysym);

    [DllImport("libXtst.so.6")]
    private static extern int XTestFakeKeyEvent(IntPtr pDisplay, uint puKeycode, int piIsPress, ulong puDelay);

    /// <summary>A keysym pressed and let go - the printable ASCII ones are their own code.</summary>
    private static void fXType(ulong puKeysym, bool pbShift)
    {
        if (msDisplay == IntPtr.Zero)
        {
            msDisplay = XOpenDisplay(IntPtr.Zero);

            if (msDisplay == IntPtr.Zero)
                throw new InvalidOperationException("no X display");
        }

        uint luKey = XKeysymToKeycode(msDisplay, puKeysym);

        if (luKey == 0)
            return;

        uint luShift = pbShift ? XKeysymToKeycode(msDisplay, ShiftLeft) : 0u;

        // A Shift still held from the last capital goes first, unless this one needs it too.
        if (msuHeldShift != 0 && luShift == 0)
        {
            XTestFakeKeyEvent(msDisplay, msuHeldShift, 0, 0);
            msuHeldShift = 0;
        }

        if (luShift != 0 && msuHeldShift == 0)
            XTestFakeKeyEvent(msDisplay, luShift, 1, 0);

        XTestFakeKeyEvent(msDisplay, luKey, 1, 0);
        XTestFakeKeyEvent(msDisplay, luKey, 0, 0);

        // SHIFT IS LET GO ONE FRAME LATER (ReleaseHeld): the game reads the key with Shift down.
        if (luShift != 0)
            msuHeldShift = luShift;

        XFlush(msDisplay);
    }
#endif
}
