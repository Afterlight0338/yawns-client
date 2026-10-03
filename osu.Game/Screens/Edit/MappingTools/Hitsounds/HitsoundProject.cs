// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: what a lane plays. Port of Hitsound Studio's lanes (https://hitsound.vivlos.dev).
    /// </summary>
    /// <param name="Sample">One of <see cref="HitSampleInfo.HIT_NORMAL"/>, whistle, finish or clap. Ignored when <paramref name="File"/> is set.</param>
    /// <param name="Bank">normal, soft or drum.</param>
    /// <param name="Index">Custom sample index: 0 plays the skin's sample, 1 the beatmap's default custom sample, 2+ numbered ones (soft-hitclap2.wav).</param>
    /// <param name="File">A custom sample file name, played instead of a skin sample.</param>
    public record HitsoundSound(string Sample, string Bank, int Index = 0, string? File = null)
    {
        public bool IsAddition => File == null && Sample != HitSampleInfo.HIT_NORMAL;

        public string Name
        {
            get
            {
                if (File != null)
                    return File;

                string sample = Sample switch
                {
                    HitSampleInfo.HIT_WHISTLE => "Whistle",
                    HitSampleInfo.HIT_FINISH => "Finish",
                    HitSampleInfo.HIT_CLAP => "Clap",
                    _ => "HitNormal",
                };

                return $"{char.ToUpperInvariant(Bank[0])}{Bank[1..]} {sample}{(Index > 0 ? $" #{Index}" : "")}";
            }
        }

        public HitSampleInfo ToSample(int volume) => File != null
            ? new ConvertHitObjectParser.FileHitSampleInfo(File, volume)
            : new HitSampleInfo(Sample, Bank, Index >= 2 ? Index.ToString() : null, volume, editorAutoBank: false, useBeatmapSamples: Index >= 1);

        public static HitsoundSound FromSample(HitSampleInfo sample)
        {
            if (sample is ConvertHitObjectParser.FileHitSampleInfo file)
                return new HitsoundSound(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, File: file.Filename);

            int index = int.TryParse(sample.Suffix, out int parsed) ? parsed : sample.UseBeatmapSamples ? 1 : 0;
            return new HitsoundSound(sample.Name, sample.Bank, index);
        }
    }

    /// <summary>
    /// YAWNS: one hit on a lane.
    /// </summary>
    public record HitsoundTrigger(double Time, HitsoundSound Sound, int Volume);

    /// <summary>
    /// YAWNS: Hitsound Studio's lanes and triggers, read from and written to a hitsound difficulty (every object a circle in the middle of the playfield).
    /// The difficulty's objects are the saved form: there is no separate project file.
    /// </summary>
    public static class HitsoundProject
    {
        public static readonly Vector2 POSITION = new Vector2(256, 192);

        /// <summary>
        /// Every sample the beatmap plays, as triggers: circles at their start, slider heads, repeats and tails at their nodes, spinners at their end.
        /// </summary>
        public static List<HitsoundTrigger> Import(IBeatmap beatmap)
        {
            var triggers = new List<HitsoundTrigger>();

            foreach (var h in beatmap.HitObjects)
            {
                foreach (var (time, samples) in samplePoints(h))
                {
                    foreach (var sample in samples)
                        triggers.Add(new HitsoundTrigger(Math.Round(time), HitsoundSound.FromSample(sample), sample.Volume));
                }
            }

            return triggers.Distinct().OrderBy(t => t.Time).ToList();
        }

        private static IEnumerable<(double Time, IList<HitSampleInfo> Samples)> samplePoints(HitObject h)
        {
            switch (h)
            {
                case IHasRepeats repeats when repeats.NodeSamples.Count > 0:
                    double span = (h.GetEndTime() - h.StartTime) / (repeats.RepeatCount + 1);
                    for (int i = 0; i < repeats.NodeSamples.Count; i++)
                        yield return (h.StartTime + i * span, repeats.NodeSamples[i]);

                    break;

                case IHasDuration:
                    // Spinners sound when they end.
                    yield return (h.GetEndTime(), h.Samples);
                    break;

                default:
                    yield return (h.StartTime, h.Samples);
                    break;
            }
        }

        /// <summary>
        /// The objects a hitsound difficulty needs to play <paramref name="triggers"/>.
        /// An object has one normal bank, one addition bank, one custom index and one volume, so simultaneous triggers that differ in those
        /// become several stacked objects (osu! plays every object's hitnormal, as it always does).
        /// </summary>
        /// <param name="triggers">The hits, in any order.</param>
        /// <param name="createCircle">Creates the ruleset's plain circle at the given time and position.</param>
        public static List<HitObject> Generate(IEnumerable<HitsoundTrigger> triggers, Func<double, Vector2, HitObject> createCircle)
        {
            var objects = new List<HitObject>();

            foreach (var moment in triggers.GroupBy(t => Math.Round(t.Time)).OrderBy(g => g.Key))
            {
                // The loudest hitnormal decides the normal bank, the rest is grouped by what has to be shared on one object.
                var normal = moment.Where(t => t.Sound.File == null && !t.Sound.IsAddition).MaxBy(t => t.Volume);
                var additions = moment.Where(t => t.Sound.IsAddition)
                                      .GroupBy(t => (t.Sound.Bank, t.Sound.Index))
                                      .Select(g => g.GroupBy(t => t.Sound.Sample).Select(s => s.MaxBy(t => t.Volume)!).ToList())
                                      .ToList();
                var files = moment.Where(t => t.Sound.File != null).GroupBy(t => t.Sound).Select(g => g.MaxBy(t => t.Volume)!);

                bool normalPlaced = false;

                foreach (var group in additions)
                {
                    var first = group[0].Sound;

                    // The hitnormal can share the first object if it uses the same custom index.
                    bool withNormal = !normalPlaced && normal != null && normal.Sound.Index == first.Index;
                    var normalSound = withNormal ? normal!.Sound : new HitsoundSound(HitSampleInfo.HIT_NORMAL, first.Bank, first.Index);
                    int volume = Math.Max(group.Max(t => t.Volume), withNormal ? normal!.Volume : 0);

                    var samples = new List<HitSampleInfo> { normalSound.ToSample(volume) };
                    samples.AddRange(group.Select(t => t.Sound.ToSample(volume)));

                    objects.Add(circle(createCircle, moment.Key, samples));
                    normalPlaced |= withNormal;
                }

                if (normal != null && !normalPlaced)
                    objects.Add(circle(createCircle, moment.Key, new List<HitSampleInfo> { normal.Sound.ToSample(normal.Volume) }));

                foreach (var file in files)
                    objects.Add(circle(createCircle, moment.Key, new List<HitSampleInfo> { file.Sound.ToSample(file.Volume) }));
            }

            return objects;
        }

        private static HitObject circle(Func<double, Vector2, HitObject> createCircle, double time, List<HitSampleInfo> samples)
        {
            var h = createCircle(time, POSITION);
            h.Samples = samples;
            return h;
        }

        /// <summary>
        /// Plain circles made as legacy objects (any ruleset can convert those) turned into the ruleset's own objects for <paramref name="beatmap"/>.
        /// </summary>
        public static IEnumerable<HitObject> ToRulesetObjects(List<HitObject> legacy, IBeatmap beatmap)
        {
            // A separate BeatmapInfo: assigning one copies its difficulty settings, which must not touch the edited difficulty.
            var source = new Beatmap
            {
                BeatmapInfo = new BeatmapInfo(beatmap.BeatmapInfo.Ruleset, beatmap.Difficulty.Clone()),
                ControlPointInfo = beatmap.ControlPointInfo,
                HitObjects = legacy,
            };

            return beatmap.BeatmapInfo.Ruleset.CreateInstance().CreateBeatmapConverter(source).Convert().HitObjects;
        }

        /// <summary>
        /// A plain legacy circle, to be turned into the ruleset's own with <see cref="ToRulesetObjects"/>.
        /// </summary>
        public static HitObject LegacyCircle(double time, Vector2 position, bool newCombo = false) => new ConvertHitCircle { StartTime = time, Position = position, NewCombo = newCombo };

        /// <summary>
        /// Whether a beatmap looks like a hitsound difficulty: only circles, all in the middle of the playfield.
        /// </summary>
        public static bool IsHitsoundDifficulty(IBeatmap beatmap) =>
            beatmap.HitObjects.All(h => h is IHasPosition p && h is not IHasDuration && Vector2.DistanceSquared(p.Position, POSITION) < 1);
    }
}
