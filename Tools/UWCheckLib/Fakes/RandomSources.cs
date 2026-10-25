using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The scripted random sources (IUWRandom) the checks hand to UWRandom.Use or to a rule:
    /// each answers in its own way, which is why they stay apart.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A random source stuck at one end: every roll the lowest value, or every roll
        /// the highest - for testing the bounds of a roll without waiting for them.</summary>
        private sealed class FixedRandomSource : IUWRandom
        {
            private readonly bool mbHighest;

            public FixedRandomSource(bool pbHighest)
            {
                mbHighest = pbHighest;
            }

            public int Next(int piMaxExclusive)
            {
                return mbHighest ? Math.Max(0, piMaxExclusive - 1) : 0;
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                return mbHighest ? Math.Max(piMin, piMaxExclusive - 1) : piMin;
            }

            public double NextDouble()
            {
                return mbHighest ? 0.9999999999999999d : 0d;
            }
        }

        /// <summary>A scripted source: the values in order, then the highest of each range; every
        /// range asked for is noted.</summary>
        private sealed class ScriptedAreaRandom : IUWRandom
        {
            private readonly Queue<int> mOValues;

            public readonly List<int> Ranges = new List<int>();

            public ScriptedAreaRandom(params int[] piValues)
            {
                mOValues = new Queue<int>(piValues);
            }

            public int Next(int piMaxExclusive)
            {
                Ranges.Add(piMaxExclusive);

                return mOValues.Count > 0 ? mOValues.Dequeue() : System.Math.Max(0, piMaxExclusive - 1);
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                return piMin + Next(piMaxExclusive - piMin);
            }

            public double NextDouble()
            {
                return 0d;
            }
        }

        /// <summary>A generator that counts its draws.</summary>
        private sealed class CountingRandomSource : IUWRandom
        {
            public int Draws;

            public int Next(int piMaxExclusive)
            {
                Draws++;

                return 0;
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                Draws++;

                return piMin;
            }

            public double NextDouble()
            {
                Draws++;

                return 0d;
            }
        }

        /// <summary>Answers the given values in turn and remembers each range asked for.</summary>
        private sealed class RecordingRandom : IUWRandom
        {
            private readonly Queue<int> mOValues;

            public readonly List<string> Ranges = new List<string>();

            public RecordingRandom(params int[] piValues)
            {
                mOValues = new Queue<int>(piValues);
            }

            public int Next(int piMaxExclusive)
            {
                Ranges.Add(piMaxExclusive.ToString());

                return mOValues.Count > 0 ? mOValues.Dequeue() % Math.Max(1, piMaxExclusive) : 0;
            }

            public int Next(int piMin, int piMaxExclusive)
            {
                Ranges.Add(piMin + ".." + piMaxExclusive);

                return mOValues.Count > 0 ? piMin + (mOValues.Dequeue() % Math.Max(1, piMaxExclusive - piMin)) : piMin;
            }

            public double NextDouble()
            {
                Ranges.Add("double");

                return 0.0;
            }
        }

        /// <summary>A scripted source that always answers the same value (modulo the range).</summary>
        private sealed class ConstantRandom : IUWRandom
        {
            private readonly int miValue;

            public ConstantRandom(int piValue)
            {
                miValue = piValue;
            }

            public int Next(int piMaxExclusive) => piMaxExclusive <= 0 ? 0 : miValue % piMaxExclusive;

            public int Next(int piMin, int piMaxExclusive) => piMin + Next(piMaxExclusive - piMin);

            public double NextDouble() => 0.5;
        }
    }
}
