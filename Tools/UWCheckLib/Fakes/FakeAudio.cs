using System.Collections.Generic;
using UWDataImport.Audio;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The audio outputs of the music checks: a music output that is always ready and a MIDI
    /// driver that records what it is sent.
    /// </summary>
    public static partial class SelfChecks
    {
        private sealed class MusicOutput : IUWMusicOutput
        {
            public bool IsReady => true;

            public bool IsPlaying { get; set; } = true;

            public void Play(int piTheme) { }

            public void Stop() { }
        }

        /// <summary>Every event a driver gets, as text.</summary>
        private sealed class RecordingMidiDriver : IUWMidiDriver
        {
            public readonly List<string> Events = new List<string>();

            public void Handle(int piStatus, int piData1, int piData2) => Events.Add(piStatus.ToString("X2") + " " + piData1 + " " + piData2);

            public void AllNotesOff() => Events.Add("notes off");

            public void Reset() => Events.Add("reset");
        }
    }
}
