// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: writes Hitsound Studio's lanes as a hitsound difficulty the way Mapping Tools' Hitsound Studio does its "Standard" export
    /// (https://github.com/OliBomby/Mapping_Tools, HitsoundConverter and HitsoundExporter): one circle per moment, nothing lost.
    /// </summary>
    /// <remarks>
    /// Hits within <see cref="LENIENCY"/> of each other become one moment. A moment without a hitnormal gets a silent one, so osu! plays only what the lanes play.
    /// The circle's volume is the loudest hit's; quieter hits are made quieter inside their sample (Mapping Tools' volume balancing).
    /// Every moment needs one sample per slot (soft-hitnormal, drum-hitclap, ...). A custom index the set or the skin already plays exactly that way is reused as it is.
    /// Otherwise moments that agree on every slot they use share a new custom index, whose samples are copies of the lane samples, or mixes where a slot plays several.
    /// Unlike Mapping Tools, new indices start at 2: an unnumbered file (index 1) is also what every missing numbered file falls back to, so writing one would change other indices.
    /// </remarks>
    public static class HitsoundExporter
    {
        /// <summary>
        /// Mapping Tools' default for joining layers into one moment, in milliseconds.
        /// </summary>
        public const double LENIENCY = 15;

        /// <summary>
        /// The first custom index a new sample set may get.
        /// </summary>
        public const int FIRST_NEW_INDEX = 2;

        public enum SourceKind
        {
            /// <summary>
            /// A file in the beatmap set.
            /// </summary>
            File,

            /// <summary>
            /// The default skin's sample for a slot, such as soft-hitclap.
            /// </summary>
            Default,

            /// <summary>
            /// Silence (the hitnormal of a moment that has only additions).
            /// </summary>
            Blank,
        }

        /// <summary>
        /// Where a sample's audio comes from.
        /// </summary>
        /// <param name="Kind">The kind of source.</param>
        /// <param name="Name">The file name in the set for <see cref="SourceKind.File"/>, the slot (soft-hitclap) for <see cref="SourceKind.Default"/>.</param>
        public readonly record struct SampleSource(SourceKind Kind, string Name)
        {
            public static readonly SampleSource BLANK = new SampleSource(SourceKind.Blank, string.Empty);

            public override string ToString() => Kind == SourceKind.Blank ? "silence" : Name;
        }

        /// <summary>
        /// A source played at an amplitude relative to the original (1 = unchanged).
        /// </summary>
        public readonly record struct Layer(SampleSource Source, double Amplitude);

        /// <summary>
        /// A sample file the export writes into the beatmap set.
        /// </summary>
        public record GeneratedFile(string Filename, IReadOnlyList<Layer> Layers)
        {
            /// <summary>
            /// Whether the file is a plain copy of a set file (no mixing, no volume change).
            /// </summary>
            public bool IsCopy => Layers.Count == 1 && Layers[0].Amplitude == 1 && Layers[0].Source.Kind == SourceKind.File;
        }

        public class Result
        {
            /// <summary>
            /// The hitsound difficulty's objects: legacy circles in the middle of the playfield (see <see cref="HitsoundProject.ToRulesetObjects"/>).
            /// </summary>
            public readonly List<HitObject> Circles = new List<HitObject>();

            /// <summary>
            /// Sample files to write into the set.
            /// </summary>
            public readonly List<GeneratedFile> Files = new List<GeneratedFile>();

            /// <summary>
            /// Set files the circles play as they are (custom indices that were reused), so they must be kept.
            /// </summary>
            public readonly HashSet<string> UsedSetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// The custom indices the circles use.
            /// </summary>
            public readonly SortedSet<int> Indices = new SortedSet<int>();

            /// <summary>
            /// How many indices are new (their samples are in <see cref="Files"/>).
            /// </summary>
            public int NewIndices;
        }

        private class Hit
        {
            public string Name = HitSampleInfo.HIT_NORMAL;
            public string Bank = HitSampleInfo.BANK_NORMAL;
            public SampleSource Source;
            public int Volume;
            public int Priority;
        }

        private class Moment
        {
            public double Time;
            public readonly List<Hit> Hits = new List<Hit>();
            public string NormalBank = HitSampleInfo.BANK_NORMAL;
            public string AdditionBank = HitSampleInfo.BANK_NORMAL;
            public int Volume;
            public Dictionary<string, HashSet<Layer>> Slots = new Dictionary<string, HashSet<Layer>>();
            public int Index;
        }

        /// <summary>
        /// Works out the circles and sample files for the lanes' hits.
        /// </summary>
        /// <param name="lanes">The lanes, highest priority first (their order in the studio). Muted lanes are left out, as Hitsound Studio does.</param>
        /// <param name="triggers">The hits.</param>
        /// <param name="setFiles">The file names in the beatmap set.</param>
        /// <param name="reservedIndices">Custom indices a new sample set must not take (used by files or other difficulties).</param>
        public static Result Build(IReadOnlyList<HitsoundLane> lanes, IEnumerable<HitsoundTrigger> triggers, IEnumerable<string> setFiles, ISet<int> reservedIndices)
        {
            var files = new Dictionary<string, string>();
            foreach (string f in setFiles)
                files.TryAdd(f.ToLowerInvariant(), f);

            var lowercaseFiles = new HashSet<string>(files.Keys);
            var result = new Result();

            var moments = zip(lanes, triggers, files, lowercaseFiles);

            // Reuse a custom index the set (or the skin) already plays this way.
            var needNew = new List<Moment>();

            foreach (var moment in moments)
            {
                int? native = nativeIndex(moment, files, lowercaseFiles);

                if (native is int index)
                {
                    moment.Index = index;
                    result.Indices.Add(index);

                    foreach (var slot in moment.Slots.Keys)
                    {
                        var source = naturalSource(slot, index, files, lowercaseFiles);
                        if (source.Kind == SourceKind.File)
                            result.UsedSetFiles.Add(source.Name);
                    }
                }
                else
                    needNew.Add(moment);
            }

            // Mapping Tools' OptimizeCustomIndices: moments that agree on every slot they both use share one sample set.
            var sets = new List<(Dictionary<string, HashSet<Layer>> Slots, List<Moment> Moments)>();

            foreach (var moment in needNew)
            {
                int i = sets.FindIndex(s => canMerge(s.Slots, moment.Slots));

                if (i < 0)
                {
                    sets.Add((moment.Slots.ToDictionary(kv => kv.Key, kv => new HashSet<Layer>(kv.Value)), new List<Moment> { moment }));
                    continue;
                }

                foreach (var (slot, layers) in moment.Slots)
                {
                    if (!sets[i].Slots.ContainsKey(slot))
                        sets[i].Slots[slot] = new HashSet<Layer>(layers);
                }

                sets[i].Moments.Add(moment);
            }

            int next = FIRST_NEW_INDEX;

            foreach (var (slots, setMoments) in sets)
            {
                while (reservedIndices.Contains(next) || result.Indices.Contains(next))
                    next++;

                int index = next++;

                result.Indices.Add(index);
                result.NewIndices++;

                foreach (var moment in setMoments)
                    moment.Index = index;

                foreach (var (slot, layers) in slots.OrderBy(kv => kv.Key))
                {
                    var ordered = layers.OrderBy(l => l.Source.Kind).ThenBy(l => l.Source.Name).ThenByDescending(l => l.Amplitude).ToList();
                    string extension = ordered.Count == 1 && ordered[0].Amplitude == 1 && ordered[0].Source.Kind == SourceKind.File
                        ? System.IO.Path.GetExtension(ordered[0].Source.Name).ToLowerInvariant()
                        : ".wav";

                    result.Files.Add(new GeneratedFile($"{slot}{index}{extension}", ordered));
                }
            }

            foreach (var moment in moments)
                result.Circles.Add(circle(moment));

            return result;
        }

        /// <summary>
        /// Mapping Tools' ZipLayers and BalanceVolumes: hits close together become one moment with its slots filled.
        /// </summary>
        private static List<Moment> zip(IReadOnlyList<HitsoundLane> lanes, IEnumerable<HitsoundTrigger> triggers, Dictionary<string, string> files, ISet<string> lowercaseFiles)
        {
            var laneInfo = new Dictionary<string, (HitsoundLane Lane, int Priority)>();

            for (int i = 0; i < lanes.Count; i++)
            {
                if (!lanes[i].Muted)
                    laneInfo[lanes[i].Id] = (lanes[i], i);
            }

            var moments = new List<Moment>();
            Moment? current = null;

            foreach (var trigger in triggers.Where(t => laneInfo.ContainsKey(t.LaneId)).OrderBy(t => t.Time).ThenBy(t => laneInfo[t.LaneId].Priority))
            {
                var (lane, priority) = laneInfo[trigger.LaneId];

                if (current == null || trigger.Time - current.Time > LENIENCY)
                    moments.Add(current = new Moment { Time = Math.Round(trigger.Time) });

                current.Hits.Add(new Hit
                {
                    Name = lane.SampleName,
                    Bank = lane.SampleBank,
                    Source = sourceOf(lane, files, lowercaseFiles),
                    Volume = trigger.VolumeOn(lane),
                    Priority = priority,
                });
            }

            foreach (var moment in moments)
            {
                // Mapping Tools adds its default sample (silent unless set otherwise) where a moment has only additions.
                if (moment.Hits.All(h => h.Name != HitSampleInfo.HIT_NORMAL))
                    moment.Hits.Add(new Hit { Name = HitSampleInfo.HIT_NORMAL, Bank = HitSampleInfo.BANK_NORMAL, Source = SampleSource.BLANK, Priority = int.MaxValue });

                var normals = moment.Hits.Where(h => h.Name == HitSampleInfo.HIT_NORMAL).ToList();
                var additions = moment.Hits.Where(h => h.Name != HitSampleInfo.HIT_NORMAL).ToList();

                moment.NormalBank = normals.MinBy(h => h.Priority)!.Bank;
                moment.AdditionBank = additions.Count > 0 ? additions.MinBy(h => h.Priority)!.Bank : moment.NormalBank;
                moment.Volume = moment.Hits.Where(h => h.Source.Kind != SourceKind.Blank).Max(h => h.Volume);

                foreach (var hit in moment.Hits)
                {
                    string slot = $"{(hit.Name == HitSampleInfo.HIT_NORMAL ? moment.NormalBank : moment.AdditionBank)}-{hit.Name}";
                    double amplitude = hit.Source.Kind == SourceKind.Blank ? 1 : RelativeAmplitude(hit.Volume, moment.Volume);

                    if (!moment.Slots.TryGetValue(slot, out var layers))
                        moment.Slots[slot] = layers = new HashSet<Layer>();

                    layers.Add(new Layer(hit.Source, amplitude));
                }
            }

            return moments;
        }

        /// <summary>
        /// The sample's amplitude, relative to the loudest hit of its moment, for osu! volumes 0 to 100 (Mapping Tools' volume curve).
        /// </summary>
        public static double RelativeAmplitude(int volume, int loudest)
        {
            if (volume >= loudest || loudest <= 0)
                return 1;

            double amplitude = VolumeToAmplitude(volume / 100.0) / VolumeToAmplitude(loudest / 100.0);
            return Math.Abs(amplitude - 1) < 1e-4 ? 1 : Math.Round(amplitude, 4);
        }

        /// <summary>
        /// Mapping Tools' conversion from an osu! volume (0 to 1) to an amplitude multiplier.
        /// </summary>
        public static double VolumeToAmplitude(double volume)
        {
            double heightAt005 = 0.995 * Math.Pow(0.05, 1.5) + 0.005;

            if (volume < 0.05)
                return heightAt005 / 0.05 * volume;

            return 0.995 * Math.Pow(volume, 1.5) + 0.005;
        }

        /// <summary>
        /// What a lane plays, as a source: its custom file, the set file its custom index resolves to, or the default skin's sample.
        /// </summary>
        private static SampleSource sourceOf(HitsoundLane lane, Dictionary<string, string> files, ISet<string> lowercaseFiles)
        {
            if (lane.File != null)
                return new SampleSource(SourceKind.File, files.GetValueOrDefault(lane.File.ToLowerInvariant(), lane.File));

            return naturalSource($"{lane.SampleBank}-{lane.SampleName}", lane.Index, files, lowercaseFiles);
        }

        /// <summary>
        /// What osu! plays for a slot at a custom index: the numbered file, else the unnumbered one, else the skin's sample.
        /// </summary>
        private static SampleSource naturalSource(string slot, int index, Dictionary<string, string> files, ISet<string> lowercaseFiles)
        {
            if (index >= 2 && HitsoundProject.FindAudio(lowercaseFiles, $"{slot}{index}") is string numbered)
                return new SampleSource(SourceKind.File, files[numbered]);

            if (index >= 1 && HitsoundProject.FindAudio(lowercaseFiles, slot) is string unnumbered)
                return new SampleSource(SourceKind.File, files[unnumbered]);

            return new SampleSource(SourceKind.Default, slot);
        }

        /// <summary>
        /// A custom index at which every slot the moment uses already plays exactly its one sample, at full volume. Null if there is none.
        /// </summary>
        private static int? nativeIndex(Moment moment, Dictionary<string, string> files, ISet<string> lowercaseFiles)
        {
            if (moment.Slots.Values.Any(layers => layers.Count != 1 || layers.First().Amplitude != 1 || layers.First().Source.Kind == SourceKind.Blank))
                return null;

            // The candidates are the indices the moment's own samples come from.
            var candidates = new SortedSet<int>();

            foreach (var source in moment.Slots.Values.Select(l => l.First().Source))
            {
                if (source.Kind == SourceKind.Default)
                    candidates.Add(0);
                else if (indexOfFile(source.Name) is int index)
                    candidates.Add(index);
            }

            foreach (int index in candidates)
            {
                if (moment.Slots.All(kv => naturalSource(kv.Key, index, files, lowercaseFiles) == kv.Value.First().Source))
                    return index;
            }

            return null;
        }

        /// <summary>
        /// The custom index a standard sample file name stands for (soft-hitclap3.wav is 3, soft-hitclap.wav is 1), or null for other names.
        /// </summary>
        private static int? indexOfFile(string filename)
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(filename).ToLowerInvariant();

            foreach (string bank in HitsoundProject.BANKS)
            {
                foreach (string name in new[] { HitSampleInfo.HIT_NORMAL, HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_FINISH, HitSampleInfo.HIT_CLAP })
                {
                    string slot = $"{bank}-{name}";

                    if (!stem.StartsWith(slot, StringComparison.Ordinal))
                        continue;

                    string number = stem[slot.Length..];

                    if (number.Length == 0)
                        return 1;

                    if (int.TryParse(number, out int index) && index >= 2)
                        return index;
                }
            }

            return null;
        }

        private static bool canMerge(Dictionary<string, HashSet<Layer>> a, Dictionary<string, HashSet<Layer>> b) =>
            b.All(kv => !a.TryGetValue(kv.Key, out var layers) || layers.SetEquals(kv.Value));

        private static HitObject circle(Moment moment)
        {
            var samples = new List<HitSampleInfo> { sample(HitSampleInfo.HIT_NORMAL, moment.NormalBank) };

            foreach (string name in new[] { HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_FINISH, HitSampleInfo.HIT_CLAP })
            {
                if (moment.Hits.Any(h => h.Name == name))
                    samples.Add(sample(name, moment.AdditionBank));
            }

            var h = HitsoundProject.LegacyCircle(moment.Time, HitsoundProject.POSITION);
            h.Samples = samples;
            return h;

            HitSampleInfo sample(string name, string bank) =>
                new HitSampleInfo(name, bank, moment.Index >= 2 ? moment.Index.ToString() : null, moment.Volume, editorAutoBank: false, useBeatmapSamples: moment.Index >= 1);
        }

        /// <summary>
        /// The custom index a set file name stands for, if it is a standard hit sample name.
        /// </summary>
        public static int? IndexOfFile(string filename) => indexOfFile(filename);
    }
}
