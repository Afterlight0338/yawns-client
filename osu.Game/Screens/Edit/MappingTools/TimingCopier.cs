// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of the Timing Copier from Mapping Tools (https://github.com/OliBomby/Mapping_Tools).
    /// Replaces a beatmap's timing (redlines) with another beatmap's.
    /// </summary>
    /// <remarks>
    /// Mapping Tools also adds greenlines to keep slider velocities. That is not needed here:
    /// lazer keeps a velocity multiplier on each slider, so sliders keep their length in beats under the new timing on their own.
    /// </remarks>
    public class TimingCopier
    {
        public enum ObjectHandling
        {
            [Description("Keep them on the same beats")]
            KeepBeats,

            [Description("Keep them at the same times")]
            KeepTimes,
        }

        /// <summary>
        /// What happens to objects, bookmarks and other control points (kiai and such) when the timing changes.
        /// </summary>
        public readonly Bindable<ObjectHandling> Objects = new Bindable<ObjectHandling>();

        /// <summary>
        /// Snap object start times to the closest beat divisor of the new timing afterwards.
        /// </summary>
        public readonly BindableBool Resnap = new BindableBool(true);

        /// <summary>
        /// Replaces the timing of <paramref name="target"/> with the timing of <paramref name="source"/>, as one undoable change.
        /// </summary>
        /// <param name="source">The beatmap to copy timing from. It is not modified.</param>
        /// <param name="sourceOffset">Added to the source's timing point times, in milliseconds.</param>
        /// <param name="target">The beatmap to copy timing to.</param>
        /// <returns>Whether anything was copied (the source has at least one timing point).</returns>
        public bool Copy(IBeatmap source, double sourceOffset, EditorBeatmap target)
        {
            var newTimingPoints = source.ControlPointInfo.TimingPoints;

            if (newTimingPoints.Count == 0)
                return false;

            var controlPoints = target.ControlPointInfo;
            var oldTiming = timingOf(controlPoints.TimingPoints, 0);
            var newTiming = timingOf(newTimingPoints, sourceOffset);
            bool keepBeats = Objects.Value == ObjectHandling.KeepBeats;

            target.BeginChange();

            // Everything that keeps its musical position, measured in beats under the old timing.
            var objectBeats = target.HitObjects.Select(h => (h, start: beatsAt(oldTiming, h.StartTime), end: beatsAt(oldTiming, h.GetEndTime()))).ToList();
            var bookmarkBeats = target.Bookmarks.Select(b => beatsAt(oldTiming, b)).ToList();

            foreach (var timingPoint in controlPoints.TimingPoints.ToArray())
            {
                var group = controlPoints.GroupAt(timingPoint.Time);
                group.Remove(timingPoint);

                if (group.ControlPoints.Count == 0)
                    controlPoints.RemoveGroup(group);
            }

            // What is left are the other control points (kiai and such).
            var otherGroups = controlPoints.Groups.Select(g => (group: g, beats: beatsAt(oldTiming, g.Time))).ToList();

            foreach (var timingPoint in newTimingPoints)
            {
                controlPoints.Add(timingPoint.Time + sourceOffset, new TimingControlPoint
                {
                    BeatLength = timingPoint.BeatLength,
                    TimeSignature = timingPoint.TimeSignature,
                    OmitFirstBarLine = timingPoint.OmitFirstBarLine,
                });
            }

            if (keepBeats)
            {
                foreach (var (hitObject, start, end) in objectBeats)
                {
                    hitObject.StartTime = snap(controlPoints, timeAt(newTiming, start));

                    // Sliders keep their length in beats by themselves, anything else with a duration (spinners) needs it scaled.
                    if (hitObject is IHasDuration hasDuration and not IHasRepeats)
                        hasDuration.Duration = timeAt(newTiming, end) - timeAt(newTiming, start);
                }

                var newBookmarks = bookmarkBeats.Select(b => (int)timeAt(newTiming, b)).ToList();
                target.Bookmarks.Clear();
                target.Bookmarks.AddRange(newBookmarks);

                foreach (var (group, beats) in otherGroups)
                {
                    var points = group.ControlPoints.ToArray();
                    controlPoints.RemoveGroup(group);

                    foreach (var point in points)
                        controlPoints.Add(timeAt(newTiming, beats), point);
                }
            }
            else if (Resnap.Value)
            {
                foreach (var (hitObject, _, _) in objectBeats)
                    hitObject.StartTime = snap(controlPoints, hitObject.StartTime);
            }

            target.UpdateAllHitObjects();
            target.EndChange();

            return true;
        }

        private double snap(ControlPointInfo controlPoints, double time) => Resnap.Value ? controlPoints.GetClosestSnappedTime(time) : time;

        private static List<(double Time, double BeatLength)> timingOf(IEnumerable<TimingControlPoint> timingPoints, double offset)
            => timingPoints.Select(t => (t.Time + offset, t.BeatLength)).OrderBy(t => t.Item1).ToList();

        /// <summary>
        /// Beats from the first timing point to <paramref name="time"/>, negative before it.
        /// </summary>
        private static double beatsAt(List<(double Time, double BeatLength)> timing, double time)
        {
            if (timing.Count == 0)
                return time / TimingControlPoint.DEFAULT_BEAT_LENGTH;

            double beats = 0;

            for (int i = 0; i < timing.Count - 1; i++)
            {
                if (time < timing[i + 1].Time)
                    return beats + (time - timing[i].Time) / timing[i].BeatLength;

                beats += (timing[i + 1].Time - timing[i].Time) / timing[i].BeatLength;
            }

            var last = timing[^1];
            return beats + (time - last.Time) / last.BeatLength;
        }

        /// <summary>
        /// The inverse of <see cref="beatsAt"/>.
        /// </summary>
        private static double timeAt(List<(double Time, double BeatLength)> timing, double beats)
        {
            double cumulative = 0;

            for (int i = 0; i < timing.Count - 1; i++)
            {
                double segmentBeats = (timing[i + 1].Time - timing[i].Time) / timing[i].BeatLength;

                if (beats < cumulative + segmentBeats)
                    return timing[i].Time + (beats - cumulative) * timing[i].BeatLength;

                cumulative += segmentBeats;
            }

            var last = timing[^1];
            return last.Time + (beats - cumulative) * last.BeatLength;
        }
    }
}
