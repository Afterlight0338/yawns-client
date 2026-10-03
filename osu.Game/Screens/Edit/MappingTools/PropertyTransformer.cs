// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of the Property Transformer from Mapping Tools (https://github.com/OliBomby/Mapping_Tools).
    /// Every chosen property becomes <c>old * multiplier + offset</c> (multiplier first), optionally only within a time range
    /// or for some values, and clipped to the property's valid range. Applied to the open difficulty as one undo step.
    /// </summary>
    /// <remarks>
    /// Differences from Mapping Tools: lazer keeps slider velocity and hitsound volume/index on each object rather than on green lines,
    /// so those transform per object (slider, or every sample of it). Storyboards and videos are not edited by lazer's editor and are left alone.
    /// </remarks>
    public class PropertyTransformer
    {
        public record Transform(double Multiplier = 1, double Offset = 0)
        {
            public bool IsIdentity => Multiplier == 1 && Offset == 0;

            public double Apply(double value) => value * Multiplier + Offset;
        }

        public Transform TimingPointTime = new Transform();
        public Transform Bpm = new Transform();
        public Transform SliderVelocity = new Transform();
        public Transform HitsoundVolume = new Transform();
        public Transform HitsoundIndex = new Transform();
        public Transform ObjectTime = new Transform();
        public Transform BookmarkTime = new Transform();
        public Transform BreakTime = new Transform();
        public Transform PreviewTime = new Transform();

        /// <summary>
        /// Keep results in each property's valid range (BPM 15 to 10000, velocity 0.1 to 10, volume 5 to 100, index 0 and up).
        /// </summary>
        public bool Clip = true;

        /// <summary>
        /// Only change things at or after this time.
        /// </summary>
        public double? MinTime;

        /// <summary>
        /// Only change things at or before this time.
        /// </summary>
        public double? MaxTime;

        /// <summary>
        /// When not empty, only change values equal to one of these.
        /// </summary>
        public double[] OnlyValues = Array.Empty<double>();

        /// <summary>
        /// Never change values equal to one of these.
        /// </summary>
        public double[] ExceptValues = Array.Empty<double>();

        private bool passes(double value, double time) =>
            (OnlyValues.Length == 0 || OnlyValues.Any(v => Math.Abs(v - value) < 0.001))
            && !ExceptValues.Any(v => Math.Abs(v - value) < 0.001)
            && (MinTime == null || time >= MinTime)
            && (MaxTime == null || time <= MaxTime);

        private double transform(Transform t, double value, double time, double min = double.MinValue, double max = double.MaxValue, bool round = false)
        {
            if (t.IsIdentity || !passes(value, time))
                return value;

            double result = t.Apply(value);

            if (round)
                result = Math.Round(result);

            return Clip ? Math.Clamp(result, min, max) : result;
        }

        public void Apply(EditorBeatmap beatmap)
        {
            beatmap.BeginChange();

            transformControlPoints(beatmap);
            transformObjects(beatmap);

            if (!BookmarkTime.IsIdentity)
            {
                int[] bookmarks = beatmap.Bookmarks.Select(b => (int)Math.Round(transform(BookmarkTime, b, b))).ToArray();
                beatmap.Bookmarks.Clear();
                beatmap.Bookmarks.AddRange(bookmarks);
            }

            if (!BreakTime.IsIdentity)
            {
                var breaks = beatmap.Breaks.Select(b => (BreakPeriod)new ManualBreakPeriod(transform(BreakTime, b.StartTime, b.StartTime), transform(BreakTime, b.EndTime, b.EndTime))).ToList();
                beatmap.Breaks.Clear();
                beatmap.Breaks.AddRange(breaks);
            }

            if (!PreviewTime.IsIdentity && beatmap.BeatmapInfo.Metadata.PreviewTime >= 0)
            {
                int preview = beatmap.BeatmapInfo.Metadata.PreviewTime;
                beatmap.BeatmapInfo.Metadata.PreviewTime = (int)Math.Round(transform(PreviewTime, preview, preview, 0));
            }

            beatmap.EndChange();
        }

        private void transformControlPoints(EditorBeatmap beatmap)
        {
            var info = beatmap.ControlPointInfo;

            if (!Bpm.IsIdentity)
            {
                foreach (var tp in info.TimingPoints)
                {
                    double bpm = transform(Bpm, tp.BPM, tp.Time, 15, 10000);
                    tp.BeatLength = 60000 / bpm;
                }
            }

            if (!TimingPointTime.IsIdentity)
            {
                // Points at one time share a group: move the group's points to the new time.
                var moves = info.Groups.Select(g => (Group: g, Points: g.ControlPoints.ToList(), Time: Math.Round(transform(TimingPointTime, g.Time, g.Time))))
                                .Where(m => m.Time != m.Group.Time)
                                .ToList();

                foreach (var move in moves)
                    info.RemoveGroup(move.Group);

                foreach (var move in moves)
                {
                    foreach (var point in move.Points)
                        info.Add(move.Time, point);
                }
            }
        }

        private void transformObjects(EditorBeatmap beatmap)
        {
            foreach (var h in beatmap.HitObjects.ToList())
            {
                double start = h.StartTime;
                bool changed = false;

                if (!ObjectTime.IsIdentity)
                {
                    double end = h.GetEndTime();
                    double newStart = Math.Round(transform(ObjectTime, start, start));

                    if (newStart != start)
                    {
                        h.StartTime = newStart;

                        // Spinners and holds keep their end on the same transform; sliders get their length from velocity.
                        if (h is IHasDuration duration and not IHasSliderVelocity)
                            duration.Duration = Math.Max(0, Math.Round(transform(ObjectTime, end, end)) - newStart);

                        changed = true;
                    }
                }

                if (!SliderVelocity.IsIdentity && h is IHasSliderVelocity sv)
                {
                    double velocity = transform(SliderVelocity, sv.SliderVelocityMultiplier, start, 0.1, 10);

                    if (velocity != sv.SliderVelocityMultiplier)
                    {
                        sv.SliderVelocityMultiplier = velocity;
                        changed = true;
                    }
                }

                if (!HitsoundVolume.IsIdentity || !HitsoundIndex.IsIdentity)
                {
                    h.Samples = transformSamples(h.Samples, start);

                    if (h is IHasRepeats repeats)
                    {
                        for (int i = 0; i < repeats.NodeSamples.Count; i++)
                            repeats.NodeSamples[i] = transformSamples(repeats.NodeSamples[i], start);
                    }

                    changed = true;
                }

                if (changed)
                    beatmap.Update(h);
            }
        }

        private IList<HitSampleInfo> transformSamples(IList<HitSampleInfo> samples, double time) => samples.Select(s =>
        {
            int volume = (int)transform(HitsoundVolume, s.Volume, time, 5, 100, round: true);

            int index = int.TryParse(s.Suffix, out int parsed) ? parsed : s.UseBeatmapSamples ? 1 : 0;
            int newIndex = (int)transform(HitsoundIndex, index, time, 0, int.MaxValue, round: true);

            return newIndex == index
                ? s.With(newVolume: volume)
                : s.With(newVolume: volume, newSuffix: newIndex >= 2 ? newIndex.ToString() : null, newUseBeatmapSamples: newIndex >= 1);
        }).ToList();
    }
}
