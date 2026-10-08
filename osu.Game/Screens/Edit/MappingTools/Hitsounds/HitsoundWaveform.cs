// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Track;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: the song's waveform for the ruler, as Hitsound Studio computes it (audioEngine.ts computeWaveform):
    /// the peak of each 1/150 s block, and transients where the loudness jumps up from the block before.
    /// </summary>
    public class HitsoundWaveform
    {
        public const int POINTS_PER_SECOND = 150;

        public readonly float[] Peaks;

        public readonly float[] Transients;

        public HitsoundWaveform(float[] peaks, float[] transients)
        {
            Peaks = peaks;
            Transients = transients;
        }

        /// <summary>
        /// From lazer's waveform points (one per millisecond or so, each with a peak amplitude).
        /// </summary>
        public static HitsoundWaveform From(Waveform? waveform, double lengthMs)
        {
            // No audio (a missing file, or a virtual track in tests) has no points.
            var points = waveform?.GetPoints();

            if (points == null || points.Length == 0)
                return EMPTY;

            return From(points.Length, i => Math.Max(points[i].AmplitudeLeft, points[i].AmplitudeRight), lengthMs);
        }

        /// <param name="count">How many amplitude points the song has, spread evenly over <paramref name="lengthMs"/>.</param>
        /// <param name="amplitudeAt">The peak amplitude (0 to 1) of a point.</param>
        /// <param name="lengthMs">The song's length.</param>
        public static HitsoundWaveform From(int count, Func<int, float> amplitudeAt, double lengthMs)
        {
            int total = Math.Max(0, (int)Math.Ceiling(lengthMs / 1000 * POINTS_PER_SECOND));
            float[] peaks = new float[total];
            float[] transients = new float[total];

            if (count == 0 || total == 0)
                return new HitsoundWaveform(peaks, transients);

            double pointsPerBlock = (double)count / total;
            double prevRms = 0;

            for (int i = 0; i < total; i++)
            {
                int start = (int)(i * pointsPerBlock);
                int end = Math.Min(count, Math.Max(start + 1, (int)((i + 1) * pointsPerBlock)));

                float max = 0;
                double sumSq = 0;

                for (int j = start; j < end; j++)
                {
                    float a = amplitudeAt(j);
                    max = Math.Max(max, a);
                    sumSq += a * a;
                }

                double rms = Math.Sqrt(sumSq / Math.Max(1, end - start));

                peaks[i] = max;
                transients[i] = (float)Math.Max(0, rms - prevRms);
                prevRms = rms;
            }

            return new HitsoundWaveform(peaks, transients);
        }

        public static readonly HitsoundWaveform EMPTY = new HitsoundWaveform(Array.Empty<float>(), Array.Empty<float>());
    }
}
