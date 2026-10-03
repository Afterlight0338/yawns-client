// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.MappingTools;
using osuTK;

namespace osu.Game.Tests.Editing
{
    /// <summary>
    /// YAWNS: <see cref="MapBackups"/>.
    /// </summary>
    [TestFixture]
    public class MapBackupsTest
    {
        private TemporaryNativeStorage storage = null!;

        [SetUp]
        public void SetUp() => storage = new TemporaryNativeStorage("map-backups");

        [TearDown]
        public void TearDown() => storage.Dispose();

        private static OsuBeatmap beatmap(string difficulty)
        {
            var b = new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Ruleset = new OsuRuleset().RulesetInfo,
                    DifficultyName = difficulty,
                    Metadata = { Artist = "Artist", Title = "Title", Author = { Username = "mapper" } },
                },
            };
            b.HitObjects.Add(new HitCircle { StartTime = 1000, Position = new Vector2(100, 100) });
            return b;
        }

        [Test]
        public void TestBackupIsTheSavedMap()
        {
            var backups = new MapBackups(storage);
            string path = backups.Backup(beatmap("Hard"), null, null, new DateTime(2026, 10, 2, 14, 30, 5));

            Assert.That(path, Is.EqualTo(Path.Combine("Artist - Title (mapper)", "Hard 2026-10-02 14-30-05.osu")));

            using (var stream = backups.Storage.GetStream(path))
            using (var reader = new LineBufferedReader(stream))
            {
                var decoded = new LegacyBeatmapDecoder().Decode(reader);
                Assert.That(decoded.BeatmapInfo.DifficultyName, Is.EqualTo("Hard"));
                Assert.That(decoded.HitObjects, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void TestKeepsNewestPerDifficulty()
        {
            var backups = new MapBackups(storage);
            var start = new DateTime(2026, 10, 2);

            for (int i = 0; i < MapBackups.KEEP + 5; i++)
                backups.Backup(beatmap("Hard"), null, null, start.AddMinutes(i));

            backups.Backup(beatmap("Hard Extra"), null, null, start);

            string[] files = backups.Storage.GetFiles("Artist - Title (mapper)", "*.osu").Select(Path.GetFileName).OfType<string>().ToArray();

            Assert.That(files.Count(f => f.StartsWith("Hard 2026", StringComparison.Ordinal)), Is.EqualTo(MapBackups.KEEP));
            Assert.That(files, Does.Not.Contain("Hard 2026-10-02 00-00-00.osu"), "oldest pruned");
            Assert.That(files, Does.Contain("Hard Extra 2026-10-02 00-00-00.osu"), "other difficulty untouched");
        }
    }
}
