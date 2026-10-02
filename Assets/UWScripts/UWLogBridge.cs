using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Hands the engine-free log (UWLog) to Unity's console, once before the first scene loads.
/// Part of P0 of the engine separation (2026-09-17): the rules write to UWLog, and only this
/// host side knows about Debug.Log.
/// </summary>
public static class UWLogBridge
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void fConnect()
    {
        UWLog.InfoSink = Debug.Log;
        UWLog.WarningSink = Debug.LogWarning;
        UWLog.ErrorSink = Debug.LogError;

        // The trap log stamps its lines with the game time (P3, 2026-09-18).
        UWTrapLog.Clock = () => Time.time;
    }
}
