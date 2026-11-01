using System;
using UnityEngine;

/// <summary>
/// AskHowMany IN THE SECOND GAME (uw2 inv/INVPANEL.C:436, per user 2026-10-31: stacks could not be split): the
/// first game's prompt (UWCountPrompt) with its line where the host puts it, the game standing meanwhile as it
/// does in wdialog. The count taken goes to what asked; Escape takes nothing.
/// </summary>
public sealed class UWCountAskUw2
{
    private const string HoldName = "uw2 count";

    private readonly UWCountPrompt mOPrompt;

    private Action<int> mFTaken;

    /// <summary>The frame the prompt ended: the click that answered it does nothing else.</summary>
    private int miEndedFrame = -1;

    private bool mbHeld;

    public UWCountAskUw2(Action<string> pFShow, Action pFEnd)
    {
        mOPrompt = new UWCountPrompt(pFShow, pFEnd);
    }

    public bool IsAsking => mOPrompt.IsAsking;

    /// <summary>The prompt runs, or ended in this very frame.</summary>
    public bool IsBusy => mOPrompt.IsAsking || miEndedFrame == Time.frameCount;

    public void Begin(int piMaximum, Action<int> pFTaken)
    {
        mFTaken = pFTaken;
        mbHeld = true;
        UWGameClock.Hold(HoldName);

        if (UWControlScheme.Shared != null)
            UWControlScheme.Shared.HoldInput(HoldName);

        mOPrompt.Begin(piMaximum);
    }

    /// <summary>Once a frame; the hold goes a frame after the prompt, with the click that answered it.</summary>
    public void Update()
    {
        if (mbHeld && !mOPrompt.IsAsking && Time.frameCount > miEndedFrame)
        {
            mbHeld = false;
            UWGameClock.Release(HoldName);

            if (UWControlScheme.Shared != null)
                UWControlScheme.Shared.ReleaseInput(HoldName);
        }

        if (!mOPrompt.IsAsking)
            return;

        UWCountPrompt.Outcome leOutcome = mOPrompt.Update();

        if (leOutcome == UWCountPrompt.Outcome.Running)
            return;

        miEndedFrame = Time.frameCount;
        UWPointer.IsShown = true;

        Action<int> lFTaken = mFTaken;

        mFTaken = null;

        if (leOutcome == UWCountPrompt.Outcome.Taken)
            lFTaken?.Invoke(mOPrompt.TakenCount);
    }
}
