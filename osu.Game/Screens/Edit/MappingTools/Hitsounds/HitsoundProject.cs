// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Screens.Edit.MappingTools.Hitsounds
{
    /// <summary>
    /// YAWNS: what a sample plays: a skin or beatmap sample (bank, sample, custom index) or a custom sample file.
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

                return $"{HitsoundProject.BankName(Bank)} {sample}{(Index > 0 ? $" #{Index}" : "")}";
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
    /// YAWNS: one sample a beatmap plays at a moment.
    /// </summary>
    public record HitsoundSample(double Time, HitsoundSound Sound, int Volume);

    public enum HitsoundAddition
    {
        None,
        Whistle,
        Finish,
        Clap,
    }

    /// <summary>
    /// YAWNS: a lane of Hitsound Studio (https://hitsound.vivlos.dev): what its hits play, and how it is shown.
    /// Editing it changes every hit on it.
    /// </summary>
    public class HitsoundLane
    {
        public string Id { get; set; } = HitsoundProject.NewId("lane");

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The sample set: normal, soft or drum.
        /// </summary>
        public string Bank { get; set; } = HitSampleInfo.BANK_SOFT;

        [JsonConverter(typeof(StringEnumConverter))]
        public HitsoundAddition Addition { get; set; }

        /// <summary>
        /// The addition's own sample set, or null for "Auto" (the lane's <see cref="Bank"/>).
        /// </summary>
        public string? AdditionBank { get; set; }

        /// <summary>
        /// Custom sample index: 0 plays the skin's sample, 1 the beatmap's soft-hitclap.wav, 2 and up soft-hitclap2.wav and so on.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Volume of hits that do not have their own, 0 to 100.
        /// </summary>
        public int Volume { get; set; } = 85;

        public string Colour { get; set; } = HitsoundProject.LANE_COLOURS[0];

        /// <summary>
        /// A custom sample file in the beatmap set, played instead of the bank and addition.
        /// </summary>
        public string? File { get; set; }

        public bool Muted { get; set; }

        public bool Solo { get; set; }

        [JsonIgnore]
        public string SampleName => Addition switch
        {
            HitsoundAddition.Whistle => HitSampleInfo.HIT_WHISTLE,
            HitsoundAddition.Finish => HitSampleInfo.HIT_FINISH,
            HitsoundAddition.Clap => HitSampleInfo.HIT_CLAP,
            _ => HitSampleInfo.HIT_NORMAL,
        };

        /// <summary>
        /// The bank the lane's sample comes from: the addition bank for additions, the lane's bank otherwise.
        /// </summary>
        [JsonIgnore]
        public string SampleBank => Addition == HitsoundAddition.None ? Bank : AdditionBank ?? Bank;

        [JsonIgnore]
        public HitsoundSound Sound => new HitsoundSound(SampleName, SampleBank, Index, File);

        public HitsoundLane Clone() => (HitsoundLane)MemberwiseClone();
    }

    /// <summary>
    /// YAWNS: one hit on a lane.
    /// </summary>
    public class HitsoundTrigger
    {
        public string Id { get; set; } = HitsoundProject.NewId("tr");

        public string LaneId { get; set; } = string.Empty;

        public double Time { get; set; }

        /// <summary>
        /// This hit's own volume, or null to use the lane's.
        /// </summary>
        public int? Volume { get; set; }

        public int VolumeOn(HitsoundLane lane) => Volume ?? lane.Volume;

        public HitsoundTrigger Clone() => (HitsoundTrigger)MemberwiseClone();
    }

    /// <summary>
    /// YAWNS: the lanes and hits of one hitsound difficulty. This is what is edited; the difficulty is written from it on export.
    /// Saved in game storage next to the other difficulties' projects, never inside the beatmap set.
    /// </summary>
    public class HitsoundProjectData
    {
        public int Version { get; set; } = 1;

        public List<HitsoundLane> Lanes { get; set; } = new List<HitsoundLane>();

        public List<HitsoundTrigger> Triggers { get; set; } = new List<HitsoundTrigger>();

        public bool Compact { get; set; }

        /// <summary>
        /// <see cref="HitsoundProject.Hash"/> of the difficulty right after the last export, to notice changes made elsewhere.
        /// </summary>
        public string? ExportedHash { get; set; }

        /// <summary>
        /// Sample files the last export wrote into the set, removed again when a later export no longer needs them.
        /// </summary>
        public List<string> GeneratedFiles { get; set; } = new List<string>();

        public string Serialise() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static HitsoundProjectData? Deserialise(string json)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<HitsoundProjectData>(json);

                // A hit on a lane that no longer exists would never play or export.
                if (data != null)
                    data.Triggers.RemoveAll(t => data.Lanes.All(l => l.Id != t.LaneId));

                return data;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// YAWNS: Hitsound Studio's lanes and hits read from beatmaps, and the helpers for hitsound difficulties (every object a circle in the middle of the playfield).
    /// </summary>
    public static class HitsoundProject
    {
        public static readonly Vector2 POSITION = new Vector2(256, 192);

        public static readonly string[] BANKS = { HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_NORMAL, HitSampleInfo.BANK_DRUM };

        /// <summary>
        /// As Hitsound Studio's importer.
        /// </summary>
        public static readonly string[] LANE_COLOURS =
        {
            "#ff4081", "#00e5ff", "#ffc400", "#76ff03", "#e040fb",
            "#ff6e40", "#40c4ff", "#b2ff59", "#ffd740", "#69f0ae",
            "#ff5252", "#7c4dff", "#18ffff", "#b388ff", "#ffab40",
            "#00b0ff", "#f50057", "#00e676", "#ff9100", "#651fff",
        };

        public static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..10]}";

        public static string BankName(string bank) => bank.Length == 0 ? bank : char.ToUpperInvariant(bank[0]) + bank[1..];

        public static string AdditionName(HitsoundAddition addition) => addition == HitsoundAddition.None ? "HitNormal" : addition.ToString();

        public static HitsoundAddition AdditionOf(string sampleName) => sampleName switch
        {
            HitSampleInfo.HIT_WHISTLE => HitsoundAddition.Whistle,
            HitSampleInfo.HIT_FINISH => HitsoundAddition.Finish,
            HitSampleInfo.HIT_CLAP => HitsoundAddition.Clap,
            _ => HitsoundAddition.None,
        };

        /// <summary>
        /// The lanes Hitsound Studio starts with.
        /// </summary>
        public static List<HitsoundLane> DefaultLanes() => new List<HitsoundLane>
        {
            new HitsoundLane { Name = "Soft Clap", Bank = HitSampleInfo.BANK_SOFT, Addition = HitsoundAddition.Clap, Volume = 90, Colour = "#ff4081" },
            new HitsoundLane { Name = "Soft Whistle", Bank = HitSampleInfo.BANK_SOFT, Addition = HitsoundAddition.Whistle, Volume = 80, Colour = "#00e5ff" },
            new HitsoundLane { Name = "Soft Finish", Bank = HitSampleInfo.BANK_SOFT, Addition = HitsoundAddition.Finish, Volume = 90, Colour = "#ffc400" },
            new HitsoundLane { Name = "Drum Kick", Bank = HitSampleInfo.BANK_DRUM, Addition = HitsoundAddition.None, Volume = 85, Colour = "#76ff03" },
        };

        /// <summary>
        /// Every sample the beatmap plays: circles at their start, slider heads, repeats and tails at their nodes, spinners at their end.
        /// </summary>
        public static List<HitsoundSample> Import(IBeatmap beatmap)
        {
            var samples = new List<HitsoundSample>();

            foreach (var h in beatmap.HitObjects)
            {
                foreach (var (time, nodeSamples) in samplePoints(h))
                {
                    foreach (var sample in nodeSamples)
                        samples.Add(new HitsoundSample(Math.Round(time), HitsoundSound.FromSample(sample), sample.Volume));
                }
            }

            return samples.Distinct().OrderBy(t => t.Time).ToList();
        }

        /// <summary>
        /// The beatmap's hitsounds as lanes and hits, with Hitsound Studio's importer rules:
        /// one lane per bank, addition and custom index that actually plays (a custom index whose file the set lacks plays the skin's sample),
        /// one lane per custom sample file, lanes in order of first use, the lane volume from its first hit.
        /// </summary>
        /// <param name="beatmap">The beatmap to read.</param>
        /// <param name="setFiles">The file names in the beatmap set, to tell which custom indices have files. Null treats every index as present.</param>
        public static (List<HitsoundLane> Lanes, List<HitsoundTrigger> Triggers) ImportLanes(IBeatmap beatmap, IEnumerable<string>? setFiles)
        {
            var files = setFiles == null ? null : new HashSet<string>(setFiles.Select(f => f.ToLowerInvariant()));
            var lanes = new Dictionary<HitsoundSound, HitsoundLane>();
            var triggers = new List<HitsoundTrigger>();
            var placed = new HashSet<(string, double)>();

            foreach (var sample in Import(beatmap))
            {
                var sound = sample.Sound.File != null
                    ? sample.Sound
                    : sample.Sound with { Index = EffectiveIndex(sample.Sound.Bank, sample.Sound.Sample, sample.Sound.Index, files) };

                if (!lanes.TryGetValue(sound, out var lane))
                {
                    lanes[sound] = lane = new HitsoundLane
                    {
                        Name = sound.File != null ? System.IO.Path.GetFileNameWithoutExtension(sound.File) : sound.Name,
                        Bank = sound.File != null ? HitSampleInfo.BANK_SOFT : sound.Bank,
                        Addition = sound.File != null ? HitsoundAddition.None : AdditionOf(sound.Sample),
                        Index = sound.File != null ? 0 : sound.Index,
                        File = sound.File,
                        Volume = sample.Volume > 0 ? sample.Volume : 85,
                        Colour = LANE_COLOURS[lanes.Count % LANE_COLOURS.Length],
                    };
                }

                if (!placed.Add((lane.Id, sample.Time)))
                    continue;

                triggers.Add(new HitsoundTrigger
                {
                    LaneId = lane.Id,
                    Time = sample.Time,
                    Volume = sample.Volume == lane.Volume ? null : sample.Volume,
                });
            }

            return (lanes.Values.ToList(), triggers);
        }

        /// <summary>
        /// The custom index that actually plays, as lazer looks samples up (<see cref="HitSampleInfo.LookupNames"/>):
        /// one whose numbered file the set lacks falls back to the set's unnumbered file (index 1), and then to the skin (index 0).
        /// </summary>
        public static int EffectiveIndex(string bank, string sampleName, int index, ISet<string>? lowercaseFiles)
        {
            if (lowercaseFiles == null || index <= 0)
                return index;

            string stem = $"{bank}-{sampleName}";

            if (index > 1 && FindAudio(lowercaseFiles, $"{stem}{index}") != null)
                return index;

            return FindAudio(lowercaseFiles, stem) != null ? 1 : 0;
        }

        /// <summary>
        /// The set's audio file for a sample name without extension (lowercase), or null.
        /// </summary>
        public static string? FindAudio(ISet<string> lowercaseFiles, string stem) =>
            AUDIO_EXTENSIONS.Select(ext => $"{stem}{ext}").FirstOrDefault(lowercaseFiles.Contains);

        public static readonly string[] AUDIO_EXTENSIONS = { ".wav", ".ogg", ".mp3" };

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
        /// A fingerprint of what the beatmap's objects play, to notice when a hitsound difficulty was changed outside the Hitsounds tab.
        /// </summary>
        public static string Hash(IBeatmap beatmap)
        {
            var text = new StringBuilder();

            foreach (var sample in Import(beatmap))
                text.Append($"{sample.Time}:{sample.Sound}:{sample.Volume};");

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
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
