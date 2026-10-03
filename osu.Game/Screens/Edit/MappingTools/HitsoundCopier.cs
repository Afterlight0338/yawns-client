// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of the Hitsound Copier from Mapping Tools (https://github.com/OliBomby/Mapping_Tools).
    /// Copies hitsounds from one beatmap onto the objects of another that play at the same moment.
    /// </summary>
    /// <remarks>
    /// Lazer keeps sample banks and volumes on each object instead of in greenlines, so where Mapping Tools copies greenlines,
    /// this copies each hitsound's own bank, custom index and volume. Mapping Tools' custom slider tick/slide samples and storyboarded samples are not ported.
    /// </remarks>
    public class HitsoundCopier
    {
        /// <summary>
        /// How far apart two hitsounds may be and still count as the same moment, in milliseconds.
        /// </summary>
        public readonly BindableDouble TemporalLeniency = new BindableDouble(5)
        {
            MinValue = 0,
            MaxValue = 50,
            Precision = 1,
        };

        /// <summary>
        /// Also change hitsounds that have no counterpart in the source: their additions are removed,
        /// and with <see cref="CopySampleSets"/> / <see cref="CopyVolumes"/> they take the source's bank and volume at that moment.
        /// </summary>
        public readonly BindableBool OverwriteEverything = new BindableBool();

        /// <summary>
        /// Copy sample banks and custom sample indices. When off, the target keeps its own and only additions (whistle, finish, clap) are copied.
        /// </summary>
        public readonly BindableBool CopySampleSets = new BindableBool(true);

        /// <summary>
        /// Copy sample volumes. When off, the target keeps its own.
        /// </summary>
        public readonly BindableBool CopyVolumes = new BindableBool(true);

        /// <summary>
        /// Copy the slider body (sliding) sounds between slider heads that line up.
        /// </summary>
        public readonly BindableBool CopySliderBodyHitsounds = new BindableBool(true);

        /// <summary>
        /// Copies hitsounds from <paramref name="source"/> onto <paramref name="target"/>, as one undoable change.
        /// </summary>
        /// <param name="source">The beatmap to copy from. It is not modified.</param>
        /// <param name="sourceOffset">Added to the source's times before matching, in milliseconds.</param>
        /// <param name="target">The beatmap to copy to.</param>
        /// <returns>How many of the target's hitsounds were matched by a source hitsound.</returns>
        public int Copy(IBeatmap source, double sourceOffset, EditorBeatmap target) => Copy(source.HitObjects, sourceOffset, target);

        /// <summary>
        /// Copies hitsounds from <paramref name="source"/> onto <paramref name="target"/>, as one undoable change.
        /// </summary>
        /// <param name="source">The objects to copy from, for example only part of a beatmap. They are not modified.</param>
        /// <param name="sourceOffset">Added to the source's times before matching, in milliseconds.</param>
        /// <param name="target">The beatmap to copy to.</param>
        /// <returns>How many of the target's hitsounds were matched by a source hitsound.</returns>
        public int Copy(IEnumerable<HitObject> source, double sourceOffset, EditorBeatmap target)
        {
            var sourceEvents = HitsoundEvent.From(source, sourceOffset);

            target.BeginChange();

            var targetEvents = HitsoundEvent.From(target.HitObjects, 0);
            double[] targetTimes = targetEvents.Select(e => e.Time).ToArray();

            var matched = new HashSet<HitsoundEvent>();
            var changed = new HashSet<HitObject>();

            foreach (var from in sourceEvents)
            {
                var to = nearestUnmatched(from.Time);

                if (to == null)
                    continue;

                apply(from.Samples, to.Samples);

                if (CopySliderBodyHitsounds.Value && from.IsSliderHead && to.IsSliderHead)
                    apply(from.HitObject.Samples, to.HitObject.Samples);

                matched.Add(to);
                changed.Add(to.HitObject);
            }

            if (OverwriteEverything.Value)
            {
                foreach (var to in targetEvents.Where(e => !matched.Contains(e)))
                {
                    reset(to.Samples, latestAtOrBefore(sourceEvents, to.Time)?.Samples);
                    changed.Add(to.HitObject);
                }
            }

            foreach (var hitObject in changed)
                target.Update(hitObject);

            target.EndChange();

            return matched.Count;

            HitsoundEvent? nearestUnmatched(double time)
            {
                // Like Mapping Tools, compare rounded times, and never copy two source hitsounds onto the same target hitsound.
                int i = Array.BinarySearch(targetTimes, Math.Round(time) - TemporalLeniency.Value - 1);
                if (i < 0) i = ~i;

                HitsoundEvent? best = null;

                for (; i < targetEvents.Count && Math.Round(targetEvents[i].Time) <= Math.Round(time) + TemporalLeniency.Value; i++)
                {
                    var candidate = targetEvents[i];

                    if (matched.Contains(candidate) || Math.Abs(Math.Round(candidate.Time) - Math.Round(time)) > TemporalLeniency.Value)
                        continue;

                    if (best == null || Math.Abs(candidate.Time - time) < Math.Abs(best.Time - time))
                        best = candidate;
                }

                return best;
            }
        }

        /// <summary>
        /// A moment where <see cref="Differences"/> found the target playing something other than the source.
        /// <c>Source</c> is the source's hitsounds at that moment, or null when the source plays nothing there.
        /// </summary>
        public record Difference(HitObject Target, double Time, IList<HitSampleInfo> Has, IList<HitSampleInfo>? Source);

        /// <summary>
        /// The moments where <paramref name="target"/> plays different hitsounds than <paramref name="source"/> (what <see cref="Copy(IEnumerable{HitObject}, double, EditorBeatmap)"/> would change),
        /// and where the target plays additions while the source has nothing.
        /// </summary>
        public List<Difference> Differences(IEnumerable<HitObject> source, IEnumerable<HitObject> target)
        {
            var sourceEvents = HitsoundEvent.From(source, 0);
            var differences = new List<Difference>();

            foreach (var to in HitsoundEvent.From(target, 0))
            {
                var from = sourceEvents.Where(e => Math.Abs(Math.Round(e.Time) - Math.Round(to.Time)) <= TemporalLeniency.Value)
                                       .MinBy(e => Math.Abs(e.Time - to.Time));

                if (from == null)
                {
                    if (to.Samples.Any(s => s.Name != HitSampleInfo.HIT_NORMAL))
                        differences.Add(new Difference(to.HitObject, to.Time, to.Samples, null));

                    continue;
                }

                var copied = new List<HitSampleInfo>(to.Samples);
                apply(from.Samples, copied);

                if (!sameSounds(copied, to.Samples))
                    differences.Add(new Difference(to.HitObject, to.Time, to.Samples, from.Samples));
            }

            return differences;
        }

        private static bool sameSounds(IList<HitSampleInfo> a, IList<HitSampleInfo> b)
        {
            static string key(HitSampleInfo s) => $"{s.Name}/{s.Bank}/{s.Suffix}/{s.UseBeatmapSamples}/{s.Volume}";
            return a.Select(key).Order().SequenceEqual(b.Select(key).Order());
        }

        private void apply(IList<HitSampleInfo> from, IList<HitSampleInfo> to)
        {
            var toNormal = to.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL) ?? to.FirstOrDefault();
            var toAddition = to.FirstOrDefault(s => s.Name != HitSampleInfo.HIT_NORMAL);

            var copied = from.Select(s =>
            {
                if (toNormal == null)
                    return s;

                var target = s.Name == HitSampleInfo.HIT_NORMAL ? toNormal : toAddition ?? toNormal;

                return s.With(
                    newBank: CopySampleSets.Value ? s.Bank : target.Bank,
                    newSuffix: CopySampleSets.Value ? s.Suffix : target.Suffix,
                    newUseBeatmapSamples: CopySampleSets.Value ? s.UseBeatmapSamples : target.UseBeatmapSamples,
                    newVolume: CopyVolumes.Value ? s.Volume : target.Volume);
            }).ToList();

            to.Clear();

            foreach (var sample in copied)
                to.Add(sample);
        }

        private void reset(IList<HitSampleInfo> to, IList<HitSampleInfo>? sourceAtThatTime)
        {
            var normal = to.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL);

            if (normal == null)
                return;

            var sourceNormal = sourceAtThatTime?.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_NORMAL);

            if (sourceNormal != null)
            {
                if (CopySampleSets.Value)
                    normal = normal.With(newBank: sourceNormal.Bank, newSuffix: sourceNormal.Suffix, newUseBeatmapSamples: sourceNormal.UseBeatmapSamples);

                if (CopyVolumes.Value)
                    normal = normal.With(newVolume: sourceNormal.Volume);
            }

            to.Clear();
            to.Add(normal);
        }

        private static HitsoundEvent? latestAtOrBefore(List<HitsoundEvent> events, double time)
            => events.LastOrDefault(e => e.Time <= time) ?? events.FirstOrDefault();

        /// <summary>
        /// A moment an object plays hitsounds: an object's start, each slider node (head, repeats, tail), or the end of an object with duration (spinner).
        /// </summary>
        private class HitsoundEvent
        {
            public readonly double Time;
            public readonly HitObject HitObject;
            public readonly IList<HitSampleInfo> Samples;
            public readonly bool IsSliderHead;

            private HitsoundEvent(double time, HitObject hitObject, IList<HitSampleInfo> samples, bool isSliderHead)
            {
                Time = time;
                HitObject = hitObject;
                Samples = samples;
                IsSliderHead = isSliderHead;
            }

            public static List<HitsoundEvent> From(IEnumerable<HitObject> hitObjects, double offset)
            {
                var events = new List<HitsoundEvent>();

                foreach (var hitObject in hitObjects)
                {
                    switch (hitObject)
                    {
                        case IHasRepeats repeats:
                            double spanDuration = repeats.Duration / repeats.SpanCount();

                            for (int i = 0; i < repeats.NodeSamples.Count; i++)
                                events.Add(new HitsoundEvent(hitObject.StartTime + i * spanDuration + offset, hitObject, repeats.NodeSamples[i], i == 0));

                            break;

                        case IHasDuration duration:
                            events.Add(new HitsoundEvent(duration.EndTime + offset, hitObject, hitObject.Samples, false));
                            break;

                        default:
                            events.Add(new HitsoundEvent(hitObject.StartTime + offset, hitObject, hitObject.Samples, false));
                            break;
                    }
                }

                // A broken object (a slider with no velocity, say) has no meaningful time to match against.
                return events.Where(e => double.IsFinite(e.Time)).OrderBy(e => e.Time).ToList();
            }
        }
    }
}
