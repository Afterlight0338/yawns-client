// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.ControlPoints;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of Mapping Tools' Timing Helper. Put markers exactly on the sounds (objects, bookmarks, red lines),
    /// and it changes BPMs and adds red lines so every marker is snapped, preferring human BPMs (whole, halves, tenths...).
    /// </summary>
    public class TimingHelper
    {
        public readonly BindableBool UseObjects = new BindableBool(true);
        public readonly BindableBool UseBookmarks = new BindableBool(true);

        /// <summary>
        /// Existing red lines count as markers and stay. When off, every red line but the first is removed and placed again.
        /// </summary>
        public readonly BindableBool UseRedLines = new BindableBool(true);

        public readonly BindableBool OmitFirstBarLine = new BindableBool();

        /// <summary>
        /// How far off a marker may end up, in milliseconds.
        /// </summary>
        public readonly BindableDouble Leniency = new BindableDouble(3) { MinValue = 0, MaxValue = 20, Precision = 0.5 };

        /// <summary>
        /// Beats between markers, or 0 to work it out from the current timing.
        /// </summary>
        public readonly BindableDouble BeatsBetween = new BindableDouble { MinValue = 0, MaxValue = 16, Precision = 0.25 };

        public int[] Divisors = MapCleaner.DEFAULT_DIVISORS;

        private class Marker
        {
            public readonly double Time;
            public double BeatsFromLast;

            public Marker(double time)
            {
                Time = time;
            }
        }

        /// <returns>How many red lines were added.</returns>
        public int Apply(EditorBeatmap beatmap)
        {
            var info = beatmap.ControlPointInfo;
            double leniency = Leniency.Value;
            double smallestStep = Divisors.Min(d => 1.0 / d);

            var times = new List<double>();
            if (UseObjects.Value)
                times.AddRange(beatmap.HitObjects.Select(h => h.StartTime));
            if (UseBookmarks.Value)
                times.AddRange(beatmap.Bookmarks.Select(b => (double)b));
            if (UseRedLines.Value)
                times.AddRange(info.TimingPoints.Select(t => t.Time));

            times.Sort();

            // One marker per tick.
            var markers = times.Where((t, i) => i == 0 || Math.Abs(t - times[i - 1]) >= leniency + 1e-9).Select(t => new Marker(t)).ToList();

            if (markers.Count == 0)
                return 0;

            beatmap.BeginChange();

            if (info.TimingPoints.Count == 0)
                info.Add(0, new TimingControlPoint { BeatLength = 1000 });

            // How many beats each marker is after the one before, from the current timing.
            foreach (var marker in markers)
            {
                double time = marker.Time;
                var redLine = info.TimingPointAt(time - 1);
                double snapped = resnap(time, redLine);
                double beatsFromRedLine = (snapped - redLine.Time) / redLine.BeatLength;

                if (Math.Abs(beatsFromRedLine) < 0.0001)
                    beatsFromRedLine = smallestStep;
                if (time == redLine.Time)
                    beatsFromRedLine = 0;

                double beatsFromLast = beatsFromRedLine;
                var before = markers.Where(o => o.Time < time && o.Time > redLine.Time).ToList();

                if (before.Count > 0)
                {
                    double last = before[^1].Time;
                    beatsFromLast = (snapped - resnap(last, info.TimingPointAt(last))) / redLine.BeatLength;

                    if (Math.Abs(beatsFromLast) < 0.0001)
                        beatsFromLast = smallestStep;
                    if (last == time)
                        beatsFromLast = 0;
                }

                marker.BeatsFromLast = BeatsBetween.Value > 0 ? BeatsBetween.Value : beatsFromLast;
            }

            if (!UseRedLines.Value)
            {
                foreach (var extra in info.TimingPoints.Skip(1).ToArray())
                    info.GroupAt(extra.Time)?.Remove(extra);
            }

            int added = 0;

            foreach (var marker in markers)
            {
                double time = marker.Time;

                if (marker.BeatsFromLast == 0)
                    continue;

                var redLine = info.TimingPointAt(time - 1);
                var markersBefore = markers.Where(o => o.Time < time && o.Time > redLine.Time).Append(marker).ToList();

                // Average beat length over the markers since the red line.
                double beatLength = 0, beats = 0;

                foreach (var m in markersBefore)
                {
                    beats += m.BeatsFromLast;
                    beatLength += beatLengthFor(m.Time - redLine.Time, beats, 0);
                }

                beatLength /= markersBefore.Count;

                if (fits(beatLength, markersBefore, redLine, leniency))
                    redLine.BeatLength = humanRound(beatLength, markersBefore, redLine, leniency);
                else
                {
                    // A new red line on the marker before, timed to land this one.
                    markersBefore.Remove(marker);
                    double last = markersBefore[^1].Time;

                    info.Add(last, new TimingControlPoint
                    {
                        BeatLength = beatLengthFor(time - last, marker.BeatsFromLast, leniency),
                        TimeSignature = redLine.TimeSignature,
                        OmitFirstBarLine = OmitFirstBarLine.Value,
                    });

                    added++;
                }
            }

            beatmap.EndChange();
            return added;
        }

        // The nearest tick on any divisor, from this red line only.
        private double resnap(double time, TimingControlPoint redLine)
        {
            double best = time, bestDistance = double.PositiveInfinity;

            foreach (int divisor in Divisors)
            {
                double step = redLine.BeatLength / divisor;
                double remainder = (time - redLine.Time) % step;
                double tick = remainder < 0.5 * step ? time - remainder : time - remainder + step;

                if (Math.Abs(time - tick) < bestDistance)
                {
                    bestDistance = Math.Abs(time - tick);
                    best = tick;
                }
            }

            return best;
        }

        // A beat length that keeps every marker's beat count and lands it within leniency.
        private static bool fits(double beatLength, IEnumerable<Marker> markers, TimingControlPoint redLine, double leniency)
        {
            double beats = 0;

            foreach (var m in markers)
            {
                beats += m.BeatsFromLast;

                if (Math.Abs(redLine.Time + beatLength * beats - m.Time) > leniency)
                    return false;
            }

            return true;
        }

        private static IEnumerable<double> humanCandidates(double beatLength)
        {
            double bpm = 60000 / beatLength;

            foreach (double steps in new double[] { 1, 2, 10, 100, 1000 })
                yield return 60000 / (Math.Round(bpm * steps) / steps);
        }

        private static double humanRound(double beatLength, IReadOnlyCollection<Marker> markers, TimingControlPoint redLine, double leniency)
            => humanCandidates(beatLength).FirstOrDefault(c => fits(c, markers, redLine, leniency), beatLength);

        private static double beatLengthFor(double timeFromRedLine, double beatsFromRedLine, double leniency)
        {
            double exact = timeFromRedLine / beatsFromRedLine;
            return humanCandidates(exact).FirstOrDefault(c => Math.Abs(c * beatsFromRedLine - timeFromRedLine) <= leniency, exact);
        }
    }
}
