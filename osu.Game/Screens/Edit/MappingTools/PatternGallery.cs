// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: a folder of saved patterns, like Mapping Tools' Pattern Gallery.
    /// A pattern is a plain .osu file with its own extension, so it can be shared as is and opened anywhere after renaming it to .osu.
    /// Pattern times start at 0 with the pattern's timing, and are fitted to the target map's timing by beats when placed.
    /// </summary>
    public class PatternGallery
    {
        public const string EXTENSION = ".osupattern";

        public readonly Storage Storage;

        public PatternGallery(Storage gameStorage)
        {
            Storage = gameStorage.GetStorageForDirectory("patterns");
        }

        public IEnumerable<string> List() =>
            Storage.GetFiles(".", $"*{EXTENSION}").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.OrdinalIgnoreCase);

        public static string FileName(string name) => name + EXTENSION;

        /// <summary>
        /// Saves <paramref name="objects"/> of <paramref name="source"/> as a pattern, replacing any pattern with the same name.
        /// </summary>
        /// <returns>The name it was saved under, cleaned up to be a valid file name.</returns>
        public string Save(string name, IEnumerable<HitObject> objects, IBeatmap source)
        {
            name = string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();
            if (name.Length == 0)
                name = "pattern";

            var ordered = objects.OrderBy(h => h.StartTime).ToList();

            if (ordered.Count == 0)
                throw new ArgumentException("A pattern needs at least one object.", nameof(objects));

            double start = ordered[0].StartTime;

            // A serialisation round trip gives independent copies, same as the editor's own copy and paste.
            var copies = new ClipboardContent { HitObjects = ordered }.Serialize().Deserialize<ClipboardContent>().HitObjects;

            foreach (var h in copies)
                h.StartTime -= start;

            var timing = source.ControlPointInfo.TimingPointAt(start);

            var pattern = new Beatmap
            {
                BeatmapInfo = new BeatmapInfo(source.BeatmapInfo.Ruleset, source.Difficulty.Clone(), new BeatmapMetadata
                {
                    Title = name,
                    Artist = source.Metadata.Artist,
                    Author = { Username = source.Metadata.Author.Username },
                })
                {
                    DifficultyName = name,
                },
                HitObjects = copies.ToList(),
            };

            pattern.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = timing.BeatLength, TimeSignature = timing.TimeSignature });

            using (var stream = Storage.CreateFileSafely(FileName(name)))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                new LegacyBeatmapEncoder(pattern, null, null).Encode(writer);

            return name;
        }

        public IBeatmap Load(string name, RulesetInfo ruleset)
        {
            using (var stream = Storage.GetStream(FileName(name)) ?? throw new FileNotFoundException($"No pattern called {name}."))
            using (var reader = new LineBufferedReader(stream))
                return new FlatWorkingBeatmap(new LegacyBeatmapDecoder().Decode(reader)).GetPlayableBeatmap(ruleset);
        }

        public void Delete(string name) => Storage.Delete(FileName(name));

        /// <summary>
        /// Fits a loaded pattern into <paramref name="target"/> with its first object at <paramref name="time"/>.
        /// Objects keep their place in beats, and sliders keep their shape and length in beats.
        /// </summary>
        // ponytail: uses the pattern's first BPM and the target's BPM at the placement time, so a pattern spanning a BPM change in either map is stretched evenly.
        public static IReadOnlyList<HitObject> FitTo(IBeatmap pattern, IBeatmap target, double time)
        {
            double patternBeat = pattern.ControlPointInfo.TimingPointAt(0).BeatLength;
            double targetBeat = target.ControlPointInfo.TimingPointAt(time).BeatLength;

            // Slider length in beats is distance / (100 * slider multiplier * velocity), so make up for a different map slider multiplier.
            double velocityScale = pattern.Difficulty.SliderMultiplier / target.Difficulty.SliderMultiplier;

            var objects = pattern.HitObjects.OrderBy(h => h.StartTime).ToList();
            double first = objects.FirstOrDefault()?.StartTime ?? 0;

            double fit(double t) => time + (t - first) / patternBeat * targetBeat;

            foreach (var h in objects)
            {
                double start = h.StartTime;
                double end = h.GetEndTime();

                h.StartTime = fit(start);

                // Sliders get their length from velocity, everything else with a length (spinners) is stretched directly.
                if (h is IHasSliderVelocity sv)
                    sv.SliderVelocityMultiplier *= velocityScale;
                else if (h is IHasDuration duration)
                    duration.Duration = fit(end) - fit(start);
            }

            return objects;
        }
    }
}
