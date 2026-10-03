// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Storyboards;
using static osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: the parts of Mapping Tools' Map Cleaner that lazer does not do by itself.
    /// (Green lines are written from the objects on save, so redundant ones never exist here.)
    /// </summary>
    public static class MapCleaner
    {
        /// <summary>
        /// Mapping Tools' default snaps: 1/16 (and so 1/1, 1/2, 1/4, 1/8) and 1/12 (and so 1/3, 1/6).
        /// </summary>
        public static readonly int[] DEFAULT_DIVISORS = { 16, 12 };

        /// <summary>
        /// Volumes at or below this are muted (as in lazer's own muted objects check).
        /// </summary>
        public const int MUTED_VOLUME = 5;

        private static readonly Regex hitsound_file = new Regex(@"^(normal|soft|drum)-(hitnormal|hitclap|hitwhistle|hitfinish|slidertick|sliderslide|sliderwhistle)\d*\.(wav|ogg|mp3)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// The closest time to <paramref name="time"/> on any of the divisors.
        /// </summary>
        public static double Snap(ControlPointInfo controlPoints, double time, IEnumerable<int> divisors)
            => divisors.Select(d => controlPoints.GetClosestSnappedTime(time, d)).MinBy(t => Math.Abs(t - time));

        /// <summary>
        /// Resnaps the objects' start times and their ends: slider lengths are scaled so they end on the snap, other durations are set.
        /// </summary>
        /// <returns>How many objects moved.</returns>
        public static int Resnap(EditorBeatmap beatmap, IEnumerable<HitObject> hitObjects, int[] divisors)
        {
            var controlPoints = beatmap.ControlPointInfo;
            int moved = 0;

            foreach (var h in hitObjects.ToArray())
            {
                double oldStart = h.StartTime;
                double oldDuration = h.GetEndTime() - h.StartTime;

                if (!double.IsFinite(oldDuration))
                    oldDuration = 0;
                double start = Snap(controlPoints, oldStart, divisors);
                double end = oldDuration > 0 ? Snap(controlPoints, oldStart + oldDuration, divisors) : start;

                bool changed = Math.Abs(start - oldStart) > 0.5 || (oldDuration > 0 && Math.Abs(end - (oldStart + oldDuration)) > 0.5);

                if (!changed)
                    continue;

                h.StartTime = start;

                if (oldDuration > 0 && end > start)
                {
                    if (h is IHasPath path)
                        path.Path.ExpectedDistance.Value = path.Path.Distance * (end - start) / oldDuration;
                    else if (h is IHasDuration duration)
                        duration.Duration = end - start;
                }

                beatmap.Update(h);
                moved++;
            }

            return moved;
        }

        /// <summary>
        /// Resnaps the bookmarks (duplicates merge).
        /// </summary>
        /// <returns>How many bookmarks moved.</returns>
        public static int ResnapBookmarks(EditorBeatmap beatmap, int[] divisors)
        {
            int[] old = beatmap.Bookmarks.ToArray();
            int[] snapped = old.Select(b => (int)Math.Round(Snap(beatmap.ControlPointInfo, b, divisors))).ToArray();
            int moved = old.Zip(snapped).Count(p => p.First != p.Second);

            if (moved == 0)
                return 0;

            beatmap.Bookmarks.Clear();
            beatmap.Bookmarks.AddRange(snapped.Distinct().Order());
            return moved;
        }

        /// <summary>
        /// Gives muted samples (5% or lower) the volume of the closest earlier object that is not muted, or 100%.
        /// With <c>clickableOnly</c>, only circles and slider heads: muted slider repeats and tails are left alone.
        /// </summary>
        /// <returns>How many objects changed.</returns>
        public static int Unmute(EditorBeatmap beatmap, IEnumerable<HitObject> hitObjects, bool clickableOnly = false)
        {
            int changed = 0;

            foreach (var h in hitObjects.ToArray())
            {
                int volume = beatmap.HitObjects.TakeWhile(o => o != h)
                                    .Select(o => o.Samples.Select(s => s.Volume).DefaultIfEmpty(0).Max())
                                    .LastOrDefault(v => v > MUTED_VOLUME, 100);

                if (setVolume(h, s => s.Volume <= MUTED_VOLUME, volume, clickableOnly))
                {
                    beatmap.Update(h);
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// Mutes the samples of slider repeats and tails, which are not clicked (Mapping Tools' "mute unclickable hitsounds").
        /// </summary>
        /// <returns>How many sliders changed.</returns>
        public static int MuteUnclickable(EditorBeatmap beatmap)
        {
            int changed = 0;

            foreach (var h in beatmap.HitObjects.OfType<IHasRepeats>().Where(h => h.NodeSamples.Count > 1).Cast<HitObject>().ToArray())
            {
                var repeats = (IHasRepeats)h;
                bool any = false;

                for (int i = 1; i < repeats.NodeSamples.Count; i++)
                {
                    if (repeats.NodeSamples[i].Any(s => s.Volume > MUTED_VOLUME))
                    {
                        repeats.NodeSamples[i] = repeats.NodeSamples[i].Select(s => s.With(newVolume: MUTED_VOLUME)).ToList();
                        any = true;
                    }
                }

                if (any)
                {
                    beatmap.Update(h);
                    changed++;
                }
            }

            return changed;
        }

        private static bool setVolume(HitObject h, Func<HitSampleInfo, bool> which, int volume, bool clickableOnly)
        {
            bool any = false;

            if (h.Samples.Any(which) && !(clickableOnly && h is IHasRepeats))
            {
                h.Samples = h.Samples.Select(s => which(s) ? s.With(newVolume: volume) : s).ToList();
                any = true;
            }

            if (h is IHasRepeats repeats)
            {
                for (int i = 0; i < (clickableOnly ? Math.Min(1, repeats.NodeSamples.Count) : repeats.NodeSamples.Count); i++)
                {
                    if (repeats.NodeSamples[i].Any(which))
                    {
                        repeats.NodeSamples[i] = repeats.NodeSamples[i].Select(s => which(s) ? s.With(newVolume: volume) : s).ToList();
                        any = true;
                    }
                }
            }

            return any;
        }

        /// <summary>
        /// Hitsound files of the set (named like "soft-hitclap2.wav") that no difficulty and no storyboard plays.
        /// </summary>
        public static IEnumerable<string> UnusedHitsoundFiles(BeatmapSetInfo set, IEnumerable<IBeatmap> difficulties, Storyboard? storyboard = null)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var h in difficulties.SelectMany(d => d.HitObjects).SelectMany(withNested))
            {
                foreach (var sample in h.Samples.Concat((h as IHasRepeats)?.NodeSamples.SelectMany(s => s) ?? Enumerable.Empty<HitSampleInfo>()))
                {
                    foreach (string name in beatmapFileNames(sample))
                        used.Add(name);

                    // A slider body plays its slide and whistle sounds from its own samples.
                    if (h is IHasPath && h is IHasDuration)
                    {
                        if (sample.Name == HitSampleInfo.HIT_NORMAL)
                            used.UnionWith(beatmapFileNames(sample.With(newName: "sliderslide")));
                        else if (sample.Name == HitSampleInfo.HIT_WHISTLE)
                            used.UnionWith(beatmapFileNames(sample.With(newName: "sliderwhistle")));
                    }
                }
            }

            if (storyboard != null)
            {
                foreach (var sample in storyboard.Layers.SelectMany(l => l.Elements).OfType<StoryboardSampleInfo>())
                    used.Add(Path.ChangeExtension(sample.Path, null).Replace('\\', '/'));
            }

            return set.Files.Select(f => f.Filename)
                      .Where(f => hitsound_file.IsMatch(Path.GetFileName(f)) && !used.Contains(Path.ChangeExtension(f, null).Replace('\\', '/')))
                      .Order();
        }

        private static IEnumerable<HitObject> withNested(HitObject h) => h.NestedHitObjects.SelectMany(withNested).Prepend(h);

        // The name (without extension) a sample is looked up by in the beatmap's folder, if it uses the beatmap's samples at all.
        private static IEnumerable<string> beatmapFileNames(HitSampleInfo sample)
        {
            switch (sample)
            {
                case FileHitSampleInfo file:
                    yield return Path.ChangeExtension(file.Filename, null);

                    break;

                case LegacyHitSampleInfo legacy when legacy.CustomSampleBank > 0:
                    yield return $"{sample.Bank}-{sample.Name}{(legacy.CustomSampleBank > 1 ? legacy.CustomSampleBank.ToString() : string.Empty)}";

                    break;

                default:
                    if (sample.UseBeatmapSamples || !string.IsNullOrEmpty(sample.Suffix))
                        yield return $"{sample.Bank}-{sample.Name}{sample.Suffix}";

                    break;
            }
        }
    }
}
