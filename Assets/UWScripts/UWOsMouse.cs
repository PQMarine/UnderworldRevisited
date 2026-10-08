using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// THE OPERATING SYSTEM'S MOUSE BUTTONS AND WHEEL, pressed for the gamepad's pointer
/// (UWGamepadPointer, per user 2026-10-07). Why the real ones: part of the interface is IMGUI - the
/// modern game menu, the help, the setup bar - and IMGUI takes its clicks from the system, not
/// from the Input System, so a virtual mouse device would not reach it. A real click reaches
/// everything, the Input System's mouse included. The pointer itself moves by
/// Mouse.WarpCursorPosition.
///
/// Windows: SendInput (user32). Linux: XTest (libXtst) on the X display - UNTESTED, and nothing
/// under a native Wayland window; where it fails, the pointer moves but does not click.
/// </summary>
public static class UWOsMouse
{
    public enum ButtonEnum
    {
        Left,
        Right
    }

    private static bool msbFailed;

    /// <summary>Presses or lets go of a button where the system pointer is.</summary>
    public static void Button(ButtonEnum peButton, bool pbDown)
    {
        if (msbFailed)
            return;

        try
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            uint luFlags = peButton == ButtonEnum.Left
                ? (pbDown ? LeftDown : LeftUp)
                : (pbDown ? RightDown : RightUp);

            fSend(luFlags, 0);
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            fXButton(peButton == ButtonEnum.Left ? 1u : 3u, pbDown);
#endif
        }
        catch (Exception lOError)
        {
            fFail(lOError);
        }
    }

    /// <summary>Turns the wheel by notches, positive away from the player (up).</summary>
    public static void Wheel(int piNotches)
    {
        if (msbFailed || piNotches == 0)
            return;

        try
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            fSend(WheelFlag, unchecked((uint)(piNotches * WheelDelta)));
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            uint luButton = piNotches > 0 ? 4u : 5u;

            for (int liNotch = 0; liNotch < Mathf.Abs(piNotches); liNotch++)
            {
                fXButton(luButton, true);
                fXButton(luButton, false);
            }
#endif
        }
        catch (Exception lOError)
        {
            fFail(lOError);
        }
    }

    private static void fFail(Exception pOError)
    {
        msbFailed = true;
        Debug.LogWarning("UWOsMouse: no system clicks for the gamepad pointer (" + pOError.GetType().Name + ": " + pOError.Message + ").");
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const uint InputMouse = 0;

    private const uint LeftDown = 0x0002;

    private const uint LeftUp = 0x0004;

    private const uint RightDown = 0x0008;

    private const uint RightUp = 0x0010;

    private const uint WheelFlag = 0x0800;

    private const int WheelDelta = 120;

    /// <summary>MOUSEINPUT; INPUT's union is as large as this, its largest member.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemInput
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint puCount, SystemInput[] pyInputs, int piSize);

    private static void fSend(uint puFlags, uint puData)
    {
        SystemInput[] lyInputs =
        {
            new SystemInput { Type = InputMouse, Mouse = new MouseInput { Flags = puFlags, MouseData = puData } }
        };

        SendInput(1, lyInputs, Marshal.SizeOf<SystemInput>());
    }
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    private static IntPtr msDisplay = IntPtr.Zero;

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr pName);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr pDisplay);

    [DllImport("libXtst.so.6")]
    private static extern int XTestFakeButtonEvent(IntPtr pDisplay, uint puButton, int piIsPress, ulong puDelay);

    private static void fXButton(uint puButton, bool pbDown)
    {
        if (msDisplay == IntPtr.Zero)
        {
            msDisplay = XOpenDisplay(IntPtr.Zero);

            if (msDisplay == IntPtr.Zero)
                throw new InvalidOperationException("no X display");
        }

        XTestFakeButtonEvent(msDisplay, puButton, pbDown ? 1 : 0, 0);
        XFlush(msDisplay);
    }
#endif
}
