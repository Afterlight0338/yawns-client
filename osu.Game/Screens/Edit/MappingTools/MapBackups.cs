// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Screens.Edit.MappingTools
{
    /// <summary>
    /// YAWNS: a copy of the .osu on every save (like Mapping Tools' backups), in <c>backups/&lt;set&gt;/&lt;difficulty&gt; &lt;time&gt;.osu</c>
    /// of the game storage. The newest <see cref="KEEP"/> per difficulty are kept.
    /// </summary>
    public class MapBackups
    {
        public const int KEEP = 30;

        private const string time_format = "yyyy-MM-dd HH-mm-ss";

        public readonly Storage Storage;

        public MapBackups(Storage gameStorage)
        {
            Storage = gameStorage.GetStorageForDirectory("backups");
        }

        /// <returns>The backup's path within <see cref="Storage"/>.</returns>
        public string Backup(IBeatmap beatmap, ISkin? skin, Storyboard? storyboard, DateTime time)
        {
            var metadata = beatmap.BeatmapInfo.Metadata;
            string folder = clean($"{metadata.Artist} - {metadata.Title} ({metadata.Author.Username})");
            string difficulty = clean(beatmap.BeatmapInfo.DifficultyName);
            string path = Path.Combine(folder, $"{difficulty} {time.ToString(time_format)}.osu");

            using (var stream = Storage.CreateFileSafely(path))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
                new LegacyBeatmapEncoder(beatmap, skin, storyboard).Encode(writer);

            prune(folder, difficulty);
            return path;
        }

        private void prune(string folder, string difficulty)
        {
            var mine = new Regex($@"^{Regex.Escape(difficulty)} \d{{4}}-\d{{2}}-\d{{2}} \d{{2}}-\d{{2}}-\d{{2}}\.osu$");

            // The time format sorts by name.
            foreach (string old in Storage.GetFiles(folder, "*.osu")
                                          .Where(f => mine.IsMatch(Path.GetFileName(f)))
                                          .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                                          .Skip(KEEP))
                Storage.Delete(old);
        }

        private static string clean(string name)
        {
            name = string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();
            return name.Length == 0 ? "untitled" : name;
        }
    }
}
