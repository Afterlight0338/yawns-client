// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Utils;
using static osu.Game.Rulesets.Objects.Legacy.ConvertHitObjectParser;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: port of Mapping Tools' Mapset Merger, for lazer: brings difficulties of another beatmap set in the library (a guest difficulty, say)
    /// into the set being edited. Custom hitsound indices are moved past the ones this set uses and their files come along renamed,
    /// sample files called by name come along too (into a folder when the name is taken by a different file).
    /// The new difficulty takes this set's metadata, audio and background.
    /// </summary>
    public static class MapsetMerger
    {
        private static readonly Regex hitsound_file = new Regex(@"^(?<name>(normal|soft|drum)-(hitnormal|hitclap|hitwhistle|hitfinish|slidertick|sliderslide|sliderwhistle))(?<index>\d*)(?<ext>\.(wav|ogg|mp3))$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <returns>The new difficulty.</returns>
        public static BeatmapInfo Merge(BeatmapManager beatmaps, BeatmapSetInfo target, BeatmapInfo source)
        {
            var sourceWorking = beatmaps.GetWorkingBeatmap(source);
            var sourceSet = source.BeatmapSet!;
            var targetDifficulties = target.Beatmaps.Select(b => beatmaps.GetWorkingBeatmap(b).GetPlayableBeatmap(b.Ruleset)).ToList();

            // Custom indices of the guest difficulty go after everything this set already uses.
            int next = Math.Max(target.Files.Select(f => fileIndex(f.Filename)).DefaultIfEmpty(0).Max(),
                targetDifficulties.SelectMany(allSamples).Select(customIndex).DefaultIfEmpty(0).Max()) + 1;

            var copy = beatmaps.CopyExistingDifficulty(target, sourceWorking);
            var beatmap = copy.GetPlayableBeatmap(copy.BeatmapInfo.Ruleset);

            var indices = new Dictionary<int, int>();
            var renamedFiles = new Dictionary<string, string>();

            foreach (int index in beatmap.HitObjects.SelectMany(withNested).SelectMany(h => samplesOf(h)).Select(customIndex).Where(i => i > 0).Distinct().Order())
                indices[index] = next++;

            // Hitsound files for the moved indices.
            foreach (var file in sourceSet.Files)
            {
                var match = hitsound_file.Match(Path.GetFileName(file.Filename));

                if (match.Success && indices.TryGetValue(fileIndex(file.Filename), out int newIndex))
                    addFile(beatmaps, target, sourceWorking, file.File.GetStoragePath(), match.Groups["name"].Value + (newIndex == 1 ? string.Empty : newIndex.ToString()) + match.Groups["ext"].Value);
            }

            // Samples played by file name.
            foreach (string name in beatmap.HitObjects.SelectMany(withNested).SelectMany(h => samplesOf(h)).OfType<FileHitSampleInfo>().Select(f => f.Filename).Distinct())
            {
                var sourceFile = findFile(sourceSet, name);

                if (sourceFile == null)
                    continue;

                var existing = target.Files.FirstOrDefault(f => f.Filename.Equals(sourceFile.Filename, StringComparison.OrdinalIgnoreCase));
                string newName = existing == null || existing.File.Hash == sourceFile.File.Hash ? sourceFile.Filename : $"{folderName(source)}/{sourceFile.Filename}";

                if (existing?.File.Hash != sourceFile.File.Hash)
                    addFile(beatmaps, target, sourceWorking, sourceFile.File.GetStoragePath(), newName);

                if (newName != sourceFile.Filename)
                    renamedFiles[name] = newName;
            }

            foreach (var h in beatmap.HitObjects.SelectMany(withNested))
            {
                h.Samples = h.Samples.Select(s => remap(s, indices, renamedFiles)).ToList();

                if (h is IHasRepeats repeats)
                {
                    for (int i = 0; i < repeats.NodeSamples.Count; i++)
                        repeats.NodeSamples[i] = repeats.NodeSamples[i].Select(s => remap(s, indices, renamedFiles)).ToList();
                }
            }

            var info = copy.BeatmapInfo;
            var reference = target.Beatmaps.First(b => !b.Equals(info));
            info.Metadata = reference.Metadata.DeepClone();
            info.DifficultyName = NamingUtils.GetNextBestName(target.Beatmaps.Where(b => !b.Equals(info)).Select(b => b.DifficultyName), source.DifficultyName);

            beatmaps.Save(info, beatmap, copy.Skin);
            return info;
        }

        private static HitSampleInfo remap(HitSampleInfo sample, Dictionary<int, int> indices, Dictionary<string, string> renamedFiles)
        {
            switch (sample)
            {
                case FileHitSampleInfo file:
                    return renamedFiles.TryGetValue(file.Filename, out string? renamed) ? new FileHitSampleInfo(renamed, file.Volume) : file;

                case LegacyHitSampleInfo legacy when indices.TryGetValue(legacy.CustomSampleBank, out int index):
                    return legacy.With(newCustomSampleBank: index);

                default:
                    return sample;
            }
        }

        private static void addFile(BeatmapManager beatmaps, BeatmapSetInfo target, WorkingBeatmap sourceWorking, string storagePath, string filename)
        {
            using var stream = sourceWorking.GetStream(storagePath);

            if (stream != null)
                beatmaps.AddFile(target, stream, filename);
        }

        // Samples called by name may leave out the extension.
        private static Models.RealmNamedFileUsage? findFile(BeatmapSetInfo set, string name)
            => set.Files.FirstOrDefault(f => f.Filename.Equals(name, StringComparison.OrdinalIgnoreCase))
               ?? set.Files.FirstOrDefault(f => Path.ChangeExtension(f.Filename, null).Equals(Path.ChangeExtension(name, null), StringComparison.OrdinalIgnoreCase));

        private static string folderName(BeatmapInfo source)
            => string.Concat($"{source.Metadata.Author.Username} {source.DifficultyName}".Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != '/')).Trim();

        private static int fileIndex(string filename)
        {
            var match = hitsound_file.Match(Path.GetFileName(filename));

            if (!match.Success)
                return 0;

            return match.Groups["index"].Value.Length == 0 ? 1 : int.Parse(match.Groups["index"].Value);
        }

        private static int customIndex(HitSampleInfo sample) => sample is LegacyHitSampleInfo legacy and not FileHitSampleInfo ? legacy.CustomSampleBank : 0;

        private static IEnumerable<HitSampleInfo> allSamples(IBeatmap beatmap) => beatmap.HitObjects.SelectMany(withNested).SelectMany(h => samplesOf(h));

        private static IEnumerable<HitSampleInfo> samplesOf(HitObject h)
            => h.Samples.Concat((h as IHasRepeats)?.NodeSamples.SelectMany(s => s) ?? Enumerable.Empty<HitSampleInfo>());

        private static IEnumerable<HitObject> withNested(HitObject h) => h.NestedHitObjects.SelectMany(withNested).Prepend(h);
    }
}
