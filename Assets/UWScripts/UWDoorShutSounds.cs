using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE SHUT SOUNDS OF DOORS ENDING TOGETHER (2026-11-18, per user: two portcullises shutting at once go "Tock, Tock" in
/// the original, one tone in ours): the doors' movers ask here instead of playing at once; at the frame's end the sounds
/// of that frame go through the original's animation list order (UWAnimListRules.EndInOneStep) - the first part plays
/// now, the rest one animation step (a quarter second of game time) later, and so on. A single door plays at once.
/// </summary>
public sealed class UWDoorShutSounds : MonoBehaviour
{
    private struct Shut
    {
        public int Effect;

        public Vector3 At;

        public int Order;
    }

    /// <summary>The order the doors began to move in: add_animobj puts each at the list's end, so this is their list
    /// order (per user 2026-11-18: in Unity's update order the two near grates of three fell together and the far one
    /// came late, one tone instead of the original's two).</summary>
    private static int msStarted;

    /// <summary>A door begins to move: its place in the list.</summary>
    public static int NextStartOrder()
    {
        return ++msStarted;
    }

    private static UWDoorShutSounds msInstance;

    private readonly List<Shut> mOAsked = new List<Shut>();

    private List<Shut> mOWaiting = new List<Shut>();

    private float mfWaitUntil;

    private readonly List<int> mONow = new List<int>();

    private readonly List<int> mOLeft = new List<int>();

    /// <summary>A door has come to rest shut: its sound, in the original's order among the others of this frame.</summary>
    public static void Request(int piEffect, Vector3 pOAt, int piStartOrder)
    {
        if (msInstance == null)
            msInstance = new GameObject("UW Door Shut Sounds").AddComponent<UWDoorShutSounds>();

        msInstance.mOAsked.Add(new Shut { Effect = piEffect, At = pOAt, Order = piStartOrder });
    }

    private void LateUpdate()
    {
        // Game time: a held game (a conversation, the options) holds the waiting ones too.
        if (mOWaiting.Count > 0 && Time.time >= mfWaitUntil)
        {
            List<Shut> lOStep = mOWaiting;

            mOWaiting = new List<Shut>();
            fStep(lOStep);
        }

        if (mOAsked.Count > 0)
        {
            List<Shut> lOStep = new List<Shut>(mOAsked);

            mOAsked.Clear();
            lOStep.Sort((pOA, pOB) => pOA.Order.CompareTo(pOB.Order));
            fStep(lOStep);
        }
    }

    /// <summary>One step of update_animobj over these: the ones that end now sound, the others wait a step.</summary>
    private void fStep(List<Shut> pOStep)
    {
        UWAnimListRules.EndInOneStep(pOStep.Count, mONow, mOLeft);

        foreach (int liAt in mONow)
            UWAudioService.PlayEffectAt(pOStep[liAt].Effect, pOStep[liAt].At);

        if (mOLeft.Count == 0)
            return;

        foreach (int liAt in mOLeft)
            mOWaiting.Add(pOStep[liAt]);

        mfWaitUntil = Time.time + UWAnimListRules.StepSeconds;
    }
}
